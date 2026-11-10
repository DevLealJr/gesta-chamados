import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:http/http.dart' as http;
import 'package:sqflite/sqflite.dart';
import 'package:sqflite_common_ffi_web/sqflite_ffi_web.dart';
import 'package:uuid/uuid.dart';

import 'local_store.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  if (kIsWeb) {
    databaseFactory = databaseFactoryFfiWeb;
  }
  runApp(const AlunosTechApp());
}

class ApiConfig {
  static const _configuredUrl = String.fromEnvironment('API_BASE_URL');
  static String _baseUrl = _configuredUrl.isNotEmpty
      ? _removeTrailingSlash(_configuredUrl)
      : Platform.isAndroid
          ? 'http://10.0.2.2:5079'
          : 'http://localhost:5079';

  static String get baseUrl => _baseUrl;

  static void configure(String value) => _baseUrl = _removeTrailingSlash(value);

  static String _removeTrailingSlash(String value) => value.replaceFirst(RegExp(r'/$'), '');
}

class AlunosTechApp extends StatefulWidget {
  const AlunosTechApp({super.key});

  @override
  State<AlunosTechApp> createState() => _AlunosTechAppState();
}

class _AlunosTechAppState extends State<AlunosTechApp> {
  static const _secureStorage = FlutterSecureStorage();
  LocalProfile? _profile;
  String? _token;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _restoreSession();
  }

  Future<void> _restoreSession() async {
    try {
      final profile = await LocalStore.instance.getProfile();
      final token = await _secureStorage.read(key: 'accessToken');
      final savedApiUrl = await LocalStore.instance.getApiBaseUrl();
      if (savedApiUrl != null) ApiConfig.configure(savedApiUrl);
      if (mounted) {
        setState(() {
          _profile = profile;
          _token = token;
          _loading = false;
        });
      }
    } catch (error) {
      if (mounted) {
        setState(() => _loading = false);
        _showMessage('Não foi possível abrir os dados locais: $error');
      }
    }
  }

  Future<void> _openTickets(LocalProfile profile, String token) async {
    await LocalStore.instance.saveProfile(profile);
    await _secureStorage.write(key: 'accessToken', value: token);
    if (mounted) {
      setState(() {
        _profile = profile;
        _token = token;
      });
    }
  }

  Future<void> _logout() async {
    await _secureStorage.delete(key: 'accessToken');
    await LocalStore.instance.clearProfile();
    if (mounted) {
      setState(() {
        _profile = null;
        _token = null;
      });
    }
  }

  void _showMessage(String message) {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
    });
  }

  @override
  Widget build(BuildContext context) {
    final colorScheme = ColorScheme.fromSeed(
      seedColor: const Color(0xFF087F8C),
      brightness: Brightness.light,
      surface: const Color(0xFFF8FAFC),
    );
    return MaterialApp(
      title: 'AlunosTech Chamados',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        useMaterial3: true,
        colorScheme: colorScheme,
        scaffoldBackgroundColor: const Color(0xFFF6F8FA),
        inputDecorationTheme: const InputDecorationTheme(
          filled: true,
          fillColor: Colors.white,
          border: OutlineInputBorder(),
        ),
        cardTheme: CardThemeData(
          color: Colors.white,
          elevation: 0,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
        ),
      ),
      home: _loading
          ? const Scaffold(body: Center(child: CircularProgressIndicator()))
          : _profile == null
              ? LoginScreen(onLoggedIn: _openTickets)
              : TicketsScreen(
                  profile: _profile!,
                  token: _token ?? '',
                  onLogout: _logout,
                ),
    );
  }
}

class LoginScreen extends StatefulWidget {
  const LoginScreen({required this.onLoggedIn, super.key});

  final Future<void> Function(LocalProfile profile, String token) onLoggedIn;

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  bool _loading = false;

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _login() async {
    final email = _emailController.text.trim();
    final password = _passwordController.text;
    if (email.isEmpty || password.isEmpty) {
      _showMessage('Informe seu e-mail e sua senha.');
      return;
    }

    setState(() => _loading = true);
    try {
      final response = await http
          .post(
            Uri.parse('${ApiConfig.baseUrl}/api/auth/login'),
            headers: {'Content-Type': 'application/json'},
            body: jsonEncode({'email': email, 'password': password}),
          )
          .timeout(const Duration(seconds: 12));
      final payload = _decodeResponse(response);
      if (response.statusCode != 200) {
        _showMessage(payload['message'] as String? ?? 'Não foi possível entrar.');
        return;
      }

      if (payload['perfil'] != 'COLABORADOR') {
        _showMessage('O aplicativo é destinado aos colaboradores. Use o portal web para administrar chamados.');
        return;
      }
      final token = payload['token'] as String? ?? '';
      if (token.isEmpty) {
        _showMessage('A API retornou uma sessão inválida.');
        return;
      }
      await widget.onLoggedIn(LocalProfile.fromJson(payload), token);
    } on SocketException {
      _showMessage('Sem conexão. Entre uma vez com a internet para habilitar o uso offline.');
    } on http.ClientException {
      _showMessage('Não foi possível conectar à API em ${ApiConfig.baseUrl}.');
    } catch (error) {
      _showMessage('Falha ao entrar: $error');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _openRegistration() async {
    await Navigator.of(context).push(
      MaterialPageRoute<void>(builder: (_) => const RegistrationScreen()),
    );
  }

  Future<void> _configureApi() async {
    final controller = TextEditingController(text: ApiConfig.baseUrl);
    final value = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Servidor da API'),
        content: TextField(
          controller: controller,
          keyboardType: TextInputType.url,
          decoration: const InputDecoration(
            labelText: 'Endereço base',
            hintText: 'https://api.suaempresa.com',
            helperText: 'No aparelho físico, use um endereço HTTPS acessível na rede.',
          ),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
          FilledButton(onPressed: () => Navigator.pop(context, controller.text.trim()), child: const Text('Salvar')),
        ],
      ),
    );
    controller.dispose();
    if (value == null || value.isEmpty) return;

    final uri = Uri.tryParse(value);
    if (uri == null || !uri.hasAuthority || uri.host.isEmpty || !['http', 'https'].contains(uri.scheme)) {
      _showMessage('Informe um endereço completo começando com http:// ou https://.');
      return;
    }

    final normalizedUrl = value.replaceFirst(RegExp(r'/$'), '');
    await LocalStore.instance.saveApiBaseUrl(normalizedUrl);
    ApiConfig.configure(normalizedUrl);
    if (mounted) {
      setState(() {});
      _showMessage('Servidor configurado.');
    }
  }

  void _showMessage(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 430),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Container(
                    padding: const EdgeInsets.all(24),
                    decoration: BoxDecoration(
                      color: Theme.of(context).colorScheme.primary,
                      borderRadius: BorderRadius.circular(24),
                    ),
                    child: const Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Icon(Icons.support_agent_rounded, color: Colors.white, size: 38),
                        SizedBox(height: 28),
                        Text(
                          'AlunosTech',
                          style: TextStyle(color: Colors.white, fontSize: 28, fontWeight: FontWeight.w700),
                        ),
                        SizedBox(height: 6),
                        Text('Atendimento interno, sem complicação.', style: TextStyle(color: Colors.white70)),
                      ],
                    ),
                  ),
                  const SizedBox(height: 24),
                  Align(
                    alignment: Alignment.centerRight,
                    child: TextButton.icon(
                      onPressed: _loading ? null : _configureApi,
                      icon: const Icon(Icons.settings_ethernet),
                      label: const Text('Configurar servidor'),
                    ),
                  ),
                  TextField(
                    controller: _emailController,
                    keyboardType: TextInputType.emailAddress,
                    autofillHints: const [AutofillHints.username],
                    decoration: const InputDecoration(labelText: 'E-mail', prefixIcon: Icon(Icons.email_outlined)),
                  ),
                  const SizedBox(height: 14),
                  TextField(
                    controller: _passwordController,
                    obscureText: true,
                    autofillHints: const [AutofillHints.password],
                    onSubmitted: (_) => _login(),
                    decoration: const InputDecoration(labelText: 'Senha', prefixIcon: Icon(Icons.lock_outline)),
                  ),
                  const SizedBox(height: 20),
                  FilledButton(
                    onPressed: _loading ? null : _login,
                    child: Padding(
                      padding: const EdgeInsets.all(13),
                      child: _loading
                          ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
                          : const Text('Entrar'),
                    ),
                  ),
                  TextButton(
                    onPressed: _loading ? null : _openRegistration,
                    child: const Text('Ainda não tem acesso? Solicite seu cadastro'),
                  ),
                  const SizedBox(height: 8),
                  const Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Icon(Icons.offline_bolt_outlined, size: 16, color: Colors.black54),
                      SizedBox(width: 6),
                      Text('Seus chamados ficam salvos mesmo sem internet', style: TextStyle(color: Colors.black54)),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class RegistrationScreen extends StatefulWidget {
  const RegistrationScreen({super.key});

  @override
  State<RegistrationScreen> createState() => _RegistrationScreenState();
}

class _RegistrationScreenState extends State<RegistrationScreen> {
  final _nameController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  List<Sector> _sectors = [];
  String? _sectorId;
  bool _loading = false;
  String? _sectorLoadError;

  @override
  void initState() {
    super.initState();
    _loadSectors();
  }

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _loadSectors() async {
    try {
      final response = await http
          .get(Uri.parse('${ApiConfig.baseUrl}/api/setores'))
          .timeout(const Duration(seconds: 8));
      if (response.statusCode != 200) throw const FormatException('Resposta inválida da API.');
      final data = jsonDecode(response.body) as List<dynamic>;
      if (mounted) {
        setState(() {
          _sectors = data.map((item) => Sector.fromJson(item as Map<String, dynamic>)).toList();
          _sectorLoadError = null;
        });
      }
    } catch (_) {
      if (mounted) setState(() => _sectorLoadError = 'Setores indisponíveis. O responsável poderá vincular depois.');
    }
  }

  Future<void> _register() async {
    final name = _nameController.text.trim();
    final email = _emailController.text.trim();
    final password = _passwordController.text;
    if (name.isEmpty || email.isEmpty || password.length < 8) {
      _showMessage('Preencha seu nome, e-mail e uma senha com pelo menos 8 caracteres.');
      return;
    }

    setState(() => _loading = true);
    try {
      final response = await http
          .post(
            Uri.parse('${ApiConfig.baseUrl}/api/auth/cadastro'),
            headers: {'Content-Type': 'application/json'},
            body: jsonEncode({
              'nomeCompleto': name,
              'email': email,
              'password': password,
              'setorId': _sectorId,
            }),
          )
          .timeout(const Duration(seconds: 12));
      final payload = _decodeResponse(response);
      if (response.statusCode == 202) {
        if (!mounted) return;
        await showDialog<void>(
          context: context,
          builder: (context) => AlertDialog(
            icon: const Icon(Icons.mark_email_read_outlined),
            title: const Text('Solicitação enviada'),
            content: const Text('Seu acesso ficará aguardando aprovação da equipe responsável. Você poderá entrar após a aprovação.'),
            actions: [FilledButton(onPressed: () => Navigator.pop(context), child: const Text('Entendi'))],
          ),
        );
        if (mounted) Navigator.pop(context);
      } else {
        _showMessage(payload['message'] as String? ?? 'Não foi possível enviar o cadastro.');
      }
    } on SocketException {
      _showMessage('É necessário ter internet para enviar a solicitação de cadastro.');
    } catch (error) {
      _showMessage('Falha no cadastro: $error');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _showMessage(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Solicitar acesso')),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(20),
          children: [
            const Text('Crie sua conta', style: TextStyle(fontSize: 26, fontWeight: FontWeight.bold)),
            const SizedBox(height: 8),
            const Text('A equipe responsável analisará seu cadastro antes de liberar o acesso.'),
            const SizedBox(height: 24),
            TextField(controller: _nameController, textCapitalization: TextCapitalization.words, decoration: const InputDecoration(labelText: 'Nome completo')),
            const SizedBox(height: 14),
            TextField(controller: _emailController, keyboardType: TextInputType.emailAddress, decoration: const InputDecoration(labelText: 'E-mail')),
            const SizedBox(height: 14),
            TextField(controller: _passwordController, obscureText: true, decoration: const InputDecoration(labelText: 'Senha (mínimo 8 caracteres)')),
            const SizedBox(height: 14),
            DropdownButtonFormField<String>(
              initialValue: _sectorId,
              items: _sectors.map((sector) => DropdownMenuItem(value: sector.id, child: Text(sector.name))).toList(),
              onChanged: _loading ? null : (value) => setState(() => _sectorId = value),
              decoration: const InputDecoration(labelText: 'Setor desejado (opcional)', prefixIcon: Icon(Icons.apartment_outlined)),
            ),
            if (_sectorLoadError != null) ...[
              const SizedBox(height: 8),
              Text(_sectorLoadError!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ],
            const SizedBox(height: 22),
            FilledButton(
              onPressed: _loading ? null : _register,
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: _loading ? const CircularProgressIndicator() : const Text('Enviar para aprovação'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class TicketsScreen extends StatefulWidget {
  const TicketsScreen({
    required this.profile,
    required this.token,
    required this.onLogout,
    super.key,
  });

  final LocalProfile profile;
  final String token;
  final Future<void> Function() onLogout;

  @override
  State<TicketsScreen> createState() => _TicketsScreenState();
}

class _TicketsScreenState extends State<TicketsScreen> {
  static const _uuid = Uuid();
  List<LocalTicket> _tickets = [];
  bool _loading = true;
  bool _syncing = false;
  bool _offline = false;

  @override
  void initState() {
    super.initState();
    _refresh();
  }

  Future<void> _refresh() async {
    setState(() {
      _loading = true;
      _offline = false;
      _syncing = true;
    });
    await _loadLocal();

    try {
      await _syncPending();
      if (widget.token.isNotEmpty) {
        final response = await http
            .get(
              Uri.parse('${ApiConfig.baseUrl}/api/chamados'),
              headers: {'Authorization': 'Bearer ${widget.token}'},
            )
            .timeout(const Duration(seconds: 10));
        if (response.statusCode == 200) {
          final remoteTickets = jsonDecode(response.body) as List<dynamic>;
          for (final item in remoteTickets) {
            await LocalStore.instance.saveTicket(
              LocalTicket.fromJson(item as Map<String, dynamic>, ownerId: widget.profile.userId),
            );
          }
        } else if (response.statusCode == 401) {
          _offline = true;
        } else {
          throw HttpException('A API retornou ${response.statusCode}.');
        }
      }
    } on SocketException {
      _offline = true;
    } on TimeoutException {
      _offline = true;
    } on http.ClientException {
      _offline = true;
    } on HttpException {
      _offline = true;
    } on FormatException catch (error) {
      _showMessage('Resposta inválida da API: $error');
    } finally {
      await _loadLocal();
      if (mounted) {
        setState(() {
          _loading = false;
          _syncing = false;
        });
      }
    }
  }

  Future<void> _loadLocal() async {
    final tickets = await LocalStore.instance.getTickets(widget.profile.userId);
    if (mounted) setState(() => _tickets = tickets);
  }

  Future<void> _syncPending() async {
    if (widget.token.isEmpty) {
      _offline = true;
      return;
    }
    final pending = await LocalStore.instance.getPendingTickets(widget.profile.userId);
    for (final ticket in pending) {
      final response = await http
          .post(
            Uri.parse('${ApiConfig.baseUrl}/api/chamados'),
            headers: {
              'Content-Type': 'application/json',
              'Authorization': 'Bearer ${widget.token}',
            },
            body: jsonEncode({
              'id': ticket.id,
              'titulo': ticket.title,
              'descricao': ticket.description,
              'prioridade': ticket.priority,
              'dataAbertura': ticket.createdAt.toUtc().toIso8601String(),
            }),
          )
          .timeout(const Duration(seconds: 10));
      if (response.statusCode == 200 || response.statusCode == 201) {
        final saved = _decodeResponse(response);
        await LocalStore.instance.saveTicket(
          LocalTicket(
            id: ticket.id,
            ownerId: ticket.ownerId,
            title: saved['titulo'] as String? ?? ticket.title,
            description: saved['descricao'] as String? ?? ticket.description,
            priority: saved['prioridade'] as String? ?? ticket.priority,
            status: saved['status'] as String? ?? 'Aberto',
            createdAt: ticket.createdAt,
            synced: true,
            number: saved['numero'] as String? ?? ticket.number,
          ),
        );
      } else if (response.statusCode == 401) {
        _offline = true;
        break;
      } else {
        final payload = _decodeResponse(response);
        _showMessage(payload['message'] as String? ?? 'Um chamado local não foi aceito pela API.');
        break;
      }
    }
  }

  Future<void> _createTicket() async {
    final formKey = GlobalKey<FormState>();
    final titleController = TextEditingController();
    final descriptionController = TextEditingController();
    var priority = 'Media';
    final shouldSave = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: const Text('Novo chamado'),
          content: SizedBox(
            width: 460,
            child: Form(
              key: formKey,
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    TextFormField(
                      controller: titleController,
                      maxLength: 120,
                      decoration: const InputDecoration(labelText: 'Assunto'),
                      validator: (value) => value == null || value.trim().isEmpty ? 'Informe o assunto.' : null,
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: descriptionController,
                      minLines: 3,
                      maxLines: 6,
                      maxLength: 1000,
                      decoration: const InputDecoration(labelText: 'Descreva o problema ou dificuldade'),
                      validator: (value) => value == null || value.trim().isEmpty ? 'Descreva o que aconteceu.' : null,
                    ),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<String>(
                      initialValue: priority,
                      items: const [
                        DropdownMenuItem(value: 'Baixa', child: Text('Baixa')),
                        DropdownMenuItem(value: 'Media', child: Text('Média')),
                        DropdownMenuItem(value: 'Alta', child: Text('Alta')),
                        DropdownMenuItem(value: 'Urgente', child: Text('Urgente')),
                      ],
                      onChanged: (value) => setDialogState(() => priority = value ?? 'Media'),
                      decoration: const InputDecoration(labelText: 'Prioridade'),
                    ),
                  ],
                ),
              ),
            ),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(dialogContext, false), child: const Text('Cancelar')),
            FilledButton(
              onPressed: () {
                if (formKey.currentState?.validate() ?? false) Navigator.pop(dialogContext, true);
              },
              child: const Text('Salvar chamado'),
            ),
          ],
        ),
      ),
    );

    if (shouldSave == true) {
      final ticket = LocalTicket(
        id: _uuid.v4(),
        ownerId: widget.profile.userId,
        title: titleController.text.trim(),
        description: descriptionController.text.trim(),
        priority: priority,
        status: 'Pendente de envio',
        createdAt: DateTime.now(),
        synced: false,
        number: 'Local',
      );
      await LocalStore.instance.saveTicket(ticket);
      await _loadLocal();
      await _refresh();
      if (mounted) {
        _showMessage(ticket.synced
            ? 'Chamado salvo.'
            : _offline
                ? 'Salvo neste aparelho. Será enviado quando houver conexão.'
                : 'Chamado salvo e enviado.');
      }
    }
    titleController.dispose();
    descriptionController.dispose();
  }

  Future<void> _logout() async {
    final pending = await LocalStore.instance.getPendingTickets(widget.profile.userId);
    if (pending.isNotEmpty && mounted) {
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Chamados não enviados'),
          content: Text('Há ${pending.length} chamado(s) salvo(s) neste aparelho, ainda aguardando envio. Deseja sair mesmo assim?'),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Continuar aqui')),
            FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Sair')),
          ],
        ),
      );
      if (confirmed != true) return;
    }
    await widget.onLogout();
  }

  void _showMessage(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    final unsentCount = _tickets.where((ticket) => !ticket.synced).length;
    return Scaffold(
      appBar: AppBar(
        title: const Text('Meus chamados'),
        actions: [
          IconButton(
            tooltip: 'Sincronizar',
            onPressed: _syncing ? null : _refresh,
            icon: _syncing ? const SizedBox.square(dimension: 20, child: CircularProgressIndicator(strokeWidth: 2)) : const Icon(Icons.sync),
          ),
          IconButton(tooltip: 'Sair', onPressed: _logout, icon: const Icon(Icons.logout)),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _createTicket,
        icon: const Icon(Icons.add),
        label: const Text('Abrir chamado'),
      ),
      body: RefreshIndicator(
        onRefresh: _refresh,
        child: _loading && _tickets.isEmpty
            ? ListView(children: const [SizedBox(height: 260), Center(child: CircularProgressIndicator())])
            : ListView(
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 100),
                children: [
                  Text('Olá, ${widget.profile.name.split(' ').first}', style: const TextStyle(fontSize: 24, fontWeight: FontWeight.w700)),
                  const SizedBox(height: 4),
                  Text(widget.profile.email, style: const TextStyle(color: Colors.black54)),
                  const SizedBox(height: 16),
                  if (_offline || unsentCount > 0)
                    Container(
                      padding: const EdgeInsets.all(14),
                      decoration: BoxDecoration(
                        color: const Color(0xFFFFF4D6),
                        borderRadius: BorderRadius.circular(14),
                      ),
                      child: Row(
                        children: [
                          const Icon(Icons.cloud_off_outlined, color: Color(0xFF8A5A00)),
                          const SizedBox(width: 10),
                          Expanded(
                            child: Text(
                              unsentCount > 0
                                  ? '$unsentCount chamado(s) salvo(s) no aparelho aguardando conexão.'
                                  : 'Modo offline. Seus chamados salvos continuam disponíveis.',
                            ),
                          ),
                        ],
                      ),
                    ),
                  const SizedBox(height: 20),
                  if (_tickets.isEmpty)
                    Card(
                      child: Padding(
                        padding: const EdgeInsets.symmetric(vertical: 38, horizontal: 24),
                        child: Column(
                          children: [
                            Icon(Icons.inbox_outlined, size: 42, color: Theme.of(context).colorScheme.primary),
                            const SizedBox(height: 12),
                            const Text('Nenhum chamado por enquanto', style: TextStyle(fontWeight: FontWeight.w600)),
                            const SizedBox(height: 4),
                            const Text('Toque em “Abrir chamado” para contar o que aconteceu.', textAlign: TextAlign.center),
                          ],
                        ),
                      ),
                    )
                  else
                    ..._tickets.map(
                      (ticket) => Padding(
                        padding: const EdgeInsets.only(bottom: 12),
                        child: Card(
                          child: ListTile(
                            contentPadding: const EdgeInsets.all(14),
                            title: Text(ticket.title, style: const TextStyle(fontWeight: FontWeight.w600)),
                            subtitle: Padding(
                              padding: const EdgeInsets.only(top: 8),
                              child: Text(_ticketSummary(ticket), maxLines: 4, overflow: TextOverflow.ellipsis),
                            ),
                            isThreeLine: true,
                            trailing: Column(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                Icon(ticket.synced ? Icons.cloud_done_outlined : Icons.schedule, color: ticket.synced ? Colors.teal : Colors.orange),
                                const SizedBox(height: 5),
                                Text(ticket.synced ? ticket.status : 'Aguardando envio', style: const TextStyle(fontSize: 10)),
                              ],
                            ),
                            onTap: () => Navigator.of(context).push(
                              MaterialPageRoute<void>(
                                builder: (_) => TicketDetailsScreen(
                                  ticket: ticket,
                                  token: widget.token,
                                  onUpdated: _refresh,
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                    ),
                ],
              ),
      ),
    );
  }

  String _formatDate(DateTime date) =>
      '${date.day.toString().padLeft(2, '0')}/${date.month.toString().padLeft(2, '0')}/${date.year}';

  String _ticketSummary(LocalTicket ticket) {
    final nextAction = switch (ticket.status) {
      'Resolvido' => 'Seu problema foi resolvido? Toque para responder.',
      'AguardandoSolicitante' => 'A equipe aguarda sua resposta. Toque para responder.',
      _ => '',
    };
    return '${ticket.number} · ${_formatDate(ticket.createdAt)}\n${ticket.description}'
        '${nextAction.isEmpty ? '' : '\n$nextAction'}';
  }
}

class TicketDetailsScreen extends StatefulWidget {
  const TicketDetailsScreen({
    required this.ticket,
    required this.token,
    required this.onUpdated,
    super.key,
  });

  final LocalTicket ticket;
  final String token;
  final Future<void> Function() onUpdated;

  @override
  State<TicketDetailsScreen> createState() => _TicketDetailsScreenState();
}

class _TicketDetailsScreenState extends State<TicketDetailsScreen> {
  List<Map<String, dynamic>> _comments = [];
  final _replyController = TextEditingController();
  bool _loading = true;
  bool _sending = false;
  late String _status = widget.ticket.status;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadComments();
  }

  Future<void> _loadComments() async {
    if (!widget.ticket.synced || widget.token.isEmpty) {
      setState(() => _loading = false);
      return;
    }

    try {
      final response = await http.get(
        Uri.parse('${ApiConfig.baseUrl}/api/chamados/${widget.ticket.id}/comentarios'),
        headers: {'Authorization': 'Bearer ${widget.token}'},
      ).timeout(const Duration(seconds: 10));
      if (response.statusCode != 200) throw HttpException('API retornou ${response.statusCode}.');
      final data = jsonDecode(response.body) as List<dynamic>;
      if (mounted) {
        setState(() {
          _comments = data.cast<Map<String, dynamic>>();
          _loading = false;
        });
      }
    } catch (error) {
      if (mounted) {
        setState(() {
          _error = 'Não foi possível carregar as respostas: $error';
          _loading = false;
        });
      }
    }
  }

  @override
  void dispose() {
    _replyController.dispose();
    super.dispose();
  }

  Future<void> _reply() async {
    final text = _replyController.text.trim();
    if (text.isEmpty) {
      _showMessage('Escreva sua resposta antes de enviar.');
      return;
    }

    await _postTicketAction(
      'comentarios',
      {'texto': text},
      successMessage: 'Resposta enviada. A equipe dará continuidade ao chamado.',
    );
  }

  Future<void> _confirmResolution(bool resolved) async {
    String? comment;
    if (!resolved) {
      final controller = TextEditingController();
      comment = await showDialog<String>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('O problema continua'),
          content: TextField(
            controller: controller,
            minLines: 2,
            maxLines: 4,
            decoration: const InputDecoration(labelText: 'Conte o que ainda precisa ser resolvido (opcional)'),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context), child: const Text('Voltar')),
            FilledButton(onPressed: () => Navigator.pop(context, controller.text.trim()), child: const Text('Enviar')),
          ],
        ),
      );
      controller.dispose();
      if (comment == null) return;
    }

    await _postTicketAction(
      'confirmacao',
      {'resolvido': resolved, if (comment?.isNotEmpty == true) 'comentario': comment},
      successMessage: resolved ? 'Chamado finalizado. Obrigado pela confirmação.' : 'Chamado reaberto para a equipe.',
    );
  }

  Future<void> _postTicketAction(
    String route,
    Map<String, dynamic> body, {
    required String successMessage,
  }) async {
    if (_sending) return;
    setState(() => _sending = true);
    try {
      final response = await http
          .post(
            Uri.parse('${ApiConfig.baseUrl}/api/chamados/${widget.ticket.id}/$route'),
            headers: {
              'Content-Type': 'application/json',
              'Authorization': 'Bearer ${widget.token}',
            },
            body: jsonEncode(body),
          )
          .timeout(const Duration(seconds: 10));
      final payload = _decodeResponse(response);
      if (response.statusCode != 200) {
        _showMessage(payload['message'] as String? ?? 'Não foi possível atualizar o chamado.');
        return;
      }

      final updated = LocalTicket(
        id: widget.ticket.id,
        ownerId: widget.ticket.ownerId,
        title: widget.ticket.title,
        description: widget.ticket.description,
        priority: widget.ticket.priority,
        status: payload['status'] as String? ?? _status,
        createdAt: widget.ticket.createdAt,
        synced: true,
        number: widget.ticket.number,
      );
      await LocalStore.instance.saveTicket(updated);
      _replyController.clear();
      setState(() {
        _status = updated.status;
        _error = null;
      });
      await _loadComments();
      await widget.onUpdated();
      _showMessage(successMessage);
    } on SocketException {
      _showMessage('É necessário estar conectado para enviar a resposta.');
    } on TimeoutException {
      _showMessage('A API demorou para responder. Tente novamente.');
    } catch (error) {
      _showMessage('Não foi possível atualizar o chamado: $error');
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  void _showMessage(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    final ticket = widget.ticket;
    return Scaffold(
      appBar: AppBar(title: Text(ticket.number)),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          Text(ticket.title, style: const TextStyle(fontSize: 24, fontWeight: FontWeight.bold)),
          const SizedBox(height: 12),
          Text(ticket.description),
          const SizedBox(height: 16),
          Wrap(
            spacing: 8,
            children: [
              Chip(label: Text(ticket.synced ? _status : 'Aguardando envio')),
              Chip(label: Text('Prioridade ${ticket.priority}')),
            ],
          ),
          const SizedBox(height: 20),
          const Text('Retornos da equipe', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
          const SizedBox(height: 10),
          if (!ticket.synced)
            const Card(child: Padding(padding: EdgeInsets.all(16), child: Text('As respostas aparecerão aqui após o envio do chamado.')))
          else if (_loading)
            const Center(child: Padding(padding: EdgeInsets.all(20), child: CircularProgressIndicator()))
          else if (_error != null)
            Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error))
          else if (_comments.isEmpty)
            const Card(child: Padding(padding: EdgeInsets.all(16), child: Text('Ainda não há respostas para este chamado.')))
          else
            ..._comments.map(
              (comment) => Card(
                child: ListTile(
                  leading: const CircleAvatar(child: Icon(Icons.support_agent)),
                  title: Text(comment['texto'] as String? ?? ''),
                  subtitle: Text(_formatDate(comment['dataHora'] as String?)),
                ),
              ),
            ),
          if (ticket.synced && _status == 'AguardandoSolicitante') ...[
            const SizedBox(height: 20),
            const Text('A equipe precisa de uma informação sua', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
            const SizedBox(height: 8),
            TextField(
              controller: _replyController,
              minLines: 2,
              maxLines: 5,
              decoration: const InputDecoration(labelText: 'Sua resposta'),
            ),
            const SizedBox(height: 10),
            FilledButton(
              onPressed: _sending ? null : _reply,
              child: _sending ? const CircularProgressIndicator() : const Text('Enviar resposta'),
            ),
          ],
          if (ticket.synced && _status == 'Resolvido') ...[
            const SizedBox(height: 20),
            const Card(
              child: Padding(
                padding: EdgeInsets.all(16),
                child: Text('Seu problema foi resolvido?'),
              ),
            ),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: _sending ? null : () => _confirmResolution(false),
                    child: const Text('Não, ainda preciso de ajuda'),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: FilledButton(
                    onPressed: _sending ? null : () => _confirmResolution(true),
                    child: const Text('Sim, finalizar chamado'),
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }

  String _formatDate(String? value) {
    final date = DateTime.tryParse(value ?? '')?.toLocal();
    if (date == null) return '';
    return '${date.day.toString().padLeft(2, '0')}/${date.month.toString().padLeft(2, '0')}/${date.year} ${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';
  }
}

class Sector {
  const Sector({required this.id, required this.name});

  final String id;
  final String name;

  factory Sector.fromJson(Map<String, dynamic> json) =>
      Sector(id: json['id'] as String, name: json['nome'] as String);
}

Map<String, dynamic> _decodeResponse(http.Response response) {
  if (response.body.isEmpty) return {};
  final decoded = jsonDecode(response.body);
  return decoded is Map<String, dynamic> ? decoded : {};
}

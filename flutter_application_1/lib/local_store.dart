import 'package:path/path.dart' as path;
import 'package:sqflite/sqflite.dart';

class LocalProfile {
  const LocalProfile({
    required this.userId,
    required this.email,
    required this.name,
    this.sectorId,
    this.clientId,
  });

  final String userId;
  final String email;
  final String name;
  final String? sectorId;
  final String? clientId;

  factory LocalProfile.fromJson(Map<String, dynamic> json) => LocalProfile(
        userId: json['id'] as String,
        email: json['email'] as String,
        name: json['nomeCompleto'] as String,
        sectorId: json['setorId'] as String?,
        clientId: json['clienteId'] as String?,
      );
}

class LocalTicket {
  const LocalTicket({
    required this.id,
    required this.ownerId,
    required this.title,
    required this.description,
    required this.priority,
    required this.status,
    required this.createdAt,
    required this.synced,
    required this.number,
  });

  final String id;
  final String ownerId;
  final String title;
  final String description;
  final String priority;
  final String status;
  final DateTime createdAt;
  final bool synced;
  final String number;

  factory LocalTicket.fromJson(
    Map<String, dynamic> json, {
    required String ownerId,
  }) {
    final openedAt = DateTime.tryParse(json['dataAbertura'] as String? ?? '');
    return LocalTicket(
      id: json['id'] as String,
      ownerId: ownerId,
      title: json['titulo'] as String? ?? 'Sem título',
      description: json['descricao'] as String? ?? '',
      priority: json['prioridade'] as String? ?? 'Media',
      status: json['status'] as String? ?? 'Aberto',
      createdAt: openedAt?.toLocal() ?? DateTime.now(),
      synced: true,
      number: json['numero'] as String? ?? 'Chamado',
    );
  }

  Map<String, Object?> toMap() => {
        'id': id,
        'ownerId': ownerId,
        'number': number,
        'title': title,
        'description': description,
        'priority': priority,
        'status': status,
        'createdAt': createdAt.toUtc().toIso8601String(),
        'synced': synced ? 1 : 0,
      };

  factory LocalTicket.fromMap(Map<String, Object?> row) => LocalTicket(
        id: row['id']! as String,
        ownerId: row['ownerId']! as String,
        number: row['number']! as String,
        title: row['title']! as String,
        description: row['description']! as String,
        priority: row['priority']! as String,
        status: row['status']! as String,
        createdAt: DateTime.parse(row['createdAt']! as String).toLocal(),
        synced: row['synced'] == 1,
      );
}

class LocalStore {
  LocalStore._();

  static final LocalStore instance = LocalStore._();
  Database? _database;

  Future<Database> get _db async {
    if (_database != null) return _database!;
    final databasePath = path.join(await getDatabasesPath(), 'alunostech_chamados.db');
    _database = await openDatabase(
      databasePath,
      version: 2,
      onCreate: (db, version) async {
        await db.execute('''
          CREATE TABLE profile (
            id INTEGER PRIMARY KEY CHECK (id = 1),
            userId TEXT NOT NULL,
            email TEXT NOT NULL,
            name TEXT NOT NULL,
            sectorId TEXT,
            clientId TEXT
          )
        ''');
        await db.execute('''
          CREATE TABLE tickets (
            id TEXT PRIMARY KEY,
            ownerId TEXT NOT NULL,
            number TEXT NOT NULL,
            title TEXT NOT NULL,
            description TEXT NOT NULL,
            priority TEXT NOT NULL,
            status TEXT NOT NULL,
            createdAt TEXT NOT NULL,
            synced INTEGER NOT NULL DEFAULT 0
          )
        ''');
        await db.execute('CREATE INDEX IX_tickets_ownerId ON tickets(ownerId, createdAt)');
        await db.execute('CREATE TABLE settings (key TEXT PRIMARY KEY, value TEXT NOT NULL)');
      },
      onUpgrade: (db, oldVersion, newVersion) async {
        if (oldVersion < 2) {
          await db.execute('CREATE TABLE settings (key TEXT PRIMARY KEY, value TEXT NOT NULL)');
        }
      },
    );
    return _database!;
  }

  Future<void> saveProfile(LocalProfile profile) async {
    final db = await _db;
    await db.insert(
      'profile',
      {
        'id': 1,
        'userId': profile.userId,
        'email': profile.email,
        'name': profile.name,
        'sectorId': profile.sectorId,
        'clientId': profile.clientId,
      },
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  Future<LocalProfile?> getProfile() async {
    final db = await _db;
    final rows = await db.query('profile', where: 'id = ?', whereArgs: [1], limit: 1);
    if (rows.isEmpty) return null;

    final row = rows.first;
    return LocalProfile(
      userId: row['userId']! as String,
      email: row['email']! as String,
      name: row['name']! as String,
      sectorId: row['sectorId'] as String?,
      clientId: row['clientId'] as String?,
    );
  }

  Future<void> clearProfile() async {
    final db = await _db;
    await db.delete('profile', where: 'id = ?', whereArgs: [1]);
  }

  Future<String?> getApiBaseUrl() async {
    final db = await _db;
    final rows = await db.query('settings', where: 'key = ?', whereArgs: ['apiBaseUrl'], limit: 1);
    return rows.isEmpty ? null : rows.first['value']! as String;
  }

  Future<void> saveApiBaseUrl(String value) async {
    final db = await _db;
    await db.insert(
      'settings',
      {'key': 'apiBaseUrl', 'value': value},
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  Future<List<LocalTicket>> getTickets(String ownerId) async {
    final db = await _db;
    final rows = await db.query(
      'tickets',
      where: 'ownerId = ?',
      whereArgs: [ownerId],
      orderBy: 'createdAt DESC',
    );
    return rows.map(LocalTicket.fromMap).toList();
  }

  Future<List<LocalTicket>> getPendingTickets(String ownerId) async {
    final db = await _db;
    final rows = await db.query(
      'tickets',
      where: 'ownerId = ? AND synced = 0',
      whereArgs: [ownerId],
      orderBy: 'createdAt',
    );
    return rows.map(LocalTicket.fromMap).toList();
  }

  Future<void> saveTicket(LocalTicket ticket) async {
    final db = await _db;
    await db.insert('tickets', ticket.toMap(), conflictAlgorithm: ConflictAlgorithm.replace);
  }
}

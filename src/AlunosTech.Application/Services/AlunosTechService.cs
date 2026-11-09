using AlunosTech.Domain.Entities;

namespace AlunosTech.Application.Services;

public interface IAlunosTechService
{
    IReadOnlyCollection<UsuarioDto> GetUsuarios();
    UsuarioDto CreateUsuario(CreateUsuarioRequest request);
    UsuarioDto? AuthenticateUser(string email, string senha);
    IReadOnlyCollection<ClienteDto> GetClientes();
    ClienteDto CreateCliente(CreateClienteRequest request);
    IReadOnlyCollection<SetorDto> GetSetores();
    SetorDto CreateSetor(CreateSetorRequest request);
    IReadOnlyCollection<ServicoDto> GetServicos();
    ServicoDto CreateServico(CreateServicoRequest request);
    IReadOnlyCollection<ChamadoDto> GetChamados(string? status = null, string? prioridade = null, Guid? tecnicoId = null);
    ChamadoDto? GetChamado(Guid id);
    ChamadoDto CreateChamado(CreateChamadoRequest request);
    ChamadoDto UpdateChamado(Guid id, UpdateChamadoRequest request);
    DashboardDto GetDashboard();
}

public sealed class AlunosTechService : IAlunosTechService
{
    private readonly List<Usuario> _usuarios;
    private readonly List<Cliente> _clientes;
    private readonly List<Setor> _setores;
    private readonly List<Servico> _servicos;
    private readonly List<Chamado> _chamados;
    private readonly List<ComentarioChamado> _comentarios;
    private readonly List<ApontamentoHora> _apontamentos;
    private readonly List<HistoricoChamado> _historicos;
    private int _numeroChamado = 1000;

    public AlunosTechService()
    {
        _clientes = new List<Cliente>
        {
            new Cliente { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Nome = "Alunos Tech", Documento = "12.345.678/0001-90", Segmento = "Educa??o", Ativo = true },
            new Cliente { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Nome = "Campus Digital", Documento = "98.765.432/0001-10", Segmento = "Tecnologia", Ativo = true }
        };

        _setores = new List<Setor>
        {
            new Setor { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), ClienteId = _clientes[0].Id, Nome = "Administrativo", Responsavel = "Ana Paula" },
            new Setor { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), ClienteId = _clientes[1].Id, Nome = "Opera??es", Responsavel = "Bruno Costa" }
        };

        _servicos = new List<Servico>
        {
            new Servico { Id = Guid.Parse("55555555-5555-5555-5555-555555555555"), Nome = "Help Desk", Descricao = "Suporte operacional e acesso corporativo", PrazoPadraoHoras = 8 },
            new Servico { Id = Guid.Parse("66666666-6666-6666-6666-666666666666"), Nome = "Infraestrutura", Descricao = "Acesso, rede, servidores e ambientes", PrazoPadraoHoras = 24 },
            new Servico { Id = Guid.Parse("77777777-7777-7777-7777-777777777777"), Nome = "Aplica??es", Descricao = "Sistemas internos e integra??es", PrazoPadraoHoras = 16 }
        };

        _usuarios = new List<Usuario>
        {
            new Usuario { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), NomeCompleto = "Administrador AlunosTech", Email = "admin@alunostech.local", SenhaHash = "Senha@123", Perfil = PerfilUsuario.ADMIN, Ativo = true, ClienteId = _clientes[0].Id },
            new Usuario { Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), NomeCompleto = "Gestor de Opera??es", Email = "gestor@alunostech.local", SenhaHash = "Senha@123", Perfil = PerfilUsuario.GESTOR, Ativo = true, ClienteId = _clientes[0].Id },
            new Usuario { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), NomeCompleto = "T?cnico de Suporte", Email = "tecnico@alunostech.local", SenhaHash = "Senha@123", Perfil = PerfilUsuario.TECNICO, Ativo = true, ClienteId = _clientes[1].Id },
            new Usuario { Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), NomeCompleto = "Colaborador de TI", Email = "colaborador@alunostech.local", SenhaHash = "Senha@123", Perfil = PerfilUsuario.COLABORADOR, Ativo = true, ClienteId = _clientes[0].Id }
        };

        _chamados = new List<Chamado>
        {
            new Chamado
            {
                Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
                Numero = "AT-1001",
                Titulo = "Acesso ao sistema de gest?o",
                Descricao = "Usu?rio n?o consegue acessar o sistema de gest?o e solicita apoio imediato.",
                ClienteId = _clientes[0].Id,
                SolicitanteId = _usuarios[3].Id,
                TecnicoId = _usuarios[2].Id,
                ServicoId = _servicos[0].Id,
                Status = StatusChamado.EmAtendimento,
                Prioridade = PrioridadeChamado.Alta,
                DataAbertura = DateTime.UtcNow.AddDays(-1),
                PrazoSla = DateTime.UtcNow.AddHours(10),
                RowVersion = new byte[] { 1, 2, 3 }
            }
        };

        _comentarios = new List<ComentarioChamado>
        {
            new ComentarioChamado { Id = Guid.NewGuid(), ChamadoId = _chamados[0].Id, AutorId = _usuarios[3].Id, Texto = "Solicita??o registrada pelo colaborador.", DataHora = DateTime.UtcNow.AddHours(-2) }
        };

        _historicos = new List<HistoricoChamado>
        {
            new HistoricoChamado { Id = Guid.NewGuid(), ChamadoId = _chamados[0].Id, StatusAnterior = StatusChamado.Aberto, StatusAtual = StatusChamado.EmAtendimento, UsuarioResponsavelId = _usuarios[1].Id, Observacao = "Registro e atribui??o do t?cnico." }
        };
    }

    public IReadOnlyCollection<UsuarioDto> GetUsuarios() => _usuarios.Select(MapUsuario).ToList();

    public UsuarioDto? AuthenticateUser(string email, string senha)
    {
        var usuario = _usuarios.FirstOrDefault(u =>
            u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) &&
            u.Ativo &&
            u.SenhaHash == senha);

        return usuario is null ? null : MapUsuario(usuario);
    }

    public UsuarioDto CreateUsuario(CreateUsuarioRequest request)
    {
        var usuario = new Usuario
        {
            NomeCompleto = request.NomeCompleto,
            Email = request.Email,
            SenhaHash = request.Senha,
            Perfil = Enum.Parse<PerfilUsuario>(request.Perfil, ignoreCase: true),
            Ativo = request.Ativo,
            ClienteId = request.ClienteId,
            SetorId = request.SetorId
        };

        _usuarios.Add(usuario);
        return MapUsuario(usuario);
    }

    public IReadOnlyCollection<ClienteDto> GetClientes() => _clientes.Select(c => new ClienteDto(c.Id, c.Nome, c.Documento, c.Segmento, c.Ativo)).ToList();

    public ClienteDto CreateCliente(CreateClienteRequest request)
    {
        var cliente = new Cliente { Nome = request.Nome, Documento = request.Documento, Segmento = request.Segmento, Ativo = request.Ativo };
        _clientes.Add(cliente);
        return new ClienteDto(cliente.Id, cliente.Nome, cliente.Documento, cliente.Segmento, cliente.Ativo);
    }

    public IReadOnlyCollection<SetorDto> GetSetores() => _setores.Select(s => new SetorDto(s.Id, s.ClienteId, s.Nome, s.Responsavel, s.Ativo)).ToList();

    public SetorDto CreateSetor(CreateSetorRequest request)
    {
        var setor = new Setor { ClienteId = request.ClienteId, Nome = request.Nome, Responsavel = request.Responsavel, Ativo = request.Ativo };
        _setores.Add(setor);
        return new SetorDto(setor.Id, setor.ClienteId, setor.Nome, setor.Responsavel, setor.Ativo);
    }

    public IReadOnlyCollection<ServicoDto> GetServicos() => _servicos.Select(s => new ServicoDto(s.Id, s.Nome, s.Descricao, s.PrazoPadraoHoras, s.Ativo)).ToList();

    public ServicoDto CreateServico(CreateServicoRequest request)
    {
        var servico = new Servico { Nome = request.Nome, Descricao = request.Descricao, PrazoPadraoHoras = request.PrazoPadraoHoras, Ativo = request.Ativo };
        _servicos.Add(servico);
        return new ServicoDto(servico.Id, servico.Nome, servico.Descricao, servico.PrazoPadraoHoras, servico.Ativo);
    }

    public IReadOnlyCollection<ChamadoDto> GetChamados(string? status = null, string? prioridade = null, Guid? tecnicoId = null)
    {
        var query = _chamados.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(prioridade))
        {
            query = query.Where(c => c.Prioridade.ToString().Equals(prioridade, StringComparison.OrdinalIgnoreCase));
        }

        if (tecnicoId.HasValue)
        {
            query = query.Where(c => c.TecnicoId == tecnicoId.Value);
        }

        return query.Select(MapChamado).ToList();
    }

    public ChamadoDto? GetChamado(Guid id) => _chamados.FirstOrDefault(c => c.Id == id) is { } chamado ? MapChamado(chamado) : null;

    public ChamadoDto CreateChamado(CreateChamadoRequest request)
    {
        var chamado = new Chamado
        {
            Numero = $"AT-{++_numeroChamado}",
            Titulo = request.Titulo,
            Descricao = request.Descricao,
            ClienteId = request.ClienteId,
            SolicitanteId = request.SolicitanteId,
            TecnicoId = request.TecnicoId,
            ServicoId = request.ServicoId,
            Status = StatusChamado.Aberto,
            Prioridade = Enum.Parse<PrioridadeChamado>(request.Prioridade, ignoreCase: true),
            PrazoSla = request.PrazoSla ?? DateTime.UtcNow.AddDays(2)
        };

        _chamados.Add(chamado);
        _historicos.Add(new HistoricoChamado
        {
            ChamadoId = chamado.Id,
            StatusAnterior = StatusChamado.Aberto,
            StatusAtual = StatusChamado.Aberto,
            UsuarioResponsavelId = request.SolicitanteId,
            Observacao = "Chamado aberto pelo colaborador."
        });

        return MapChamado(chamado);
    }

    public ChamadoDto UpdateChamado(Guid id, UpdateChamadoRequest request)
    {
        var chamado = _chamados.First(c => c.Id == id);
        var statusAnterior = chamado.Status;

        chamado.Titulo = request.Titulo ?? chamado.Titulo;
        chamado.Descricao = request.Descricao ?? chamado.Descricao;
        chamado.Status = string.IsNullOrWhiteSpace(request.Status) ? chamado.Status : Enum.Parse<StatusChamado>(request.Status, ignoreCase: true);
        chamado.Prioridade = string.IsNullOrWhiteSpace(request.Prioridade) ? chamado.Prioridade : Enum.Parse<PrioridadeChamado>(request.Prioridade, ignoreCase: true);
        chamado.TecnicoId = request.TecnicoId ?? chamado.TecnicoId;
        chamado.DataUltimaAtualizacao = DateTime.UtcNow;

        _historicos.Add(new HistoricoChamado
        {
            ChamadoId = chamado.Id,
            StatusAnterior = statusAnterior,
            StatusAtual = chamado.Status,
            UsuarioResponsavelId = request.UsuarioResponsavelId,
            Observacao = request.Observacao ?? "Atualiza??o do chamado."
        });

        return MapChamado(chamado);
    }

    public DashboardDto GetDashboard()
    {
        var total = _chamados.Count;
        var abertos = _chamados.Count(c => c.Status == StatusChamado.Aberto);
        var emAtendimento = _chamados.Count(c => c.Status == StatusChamado.EmAtendimento || c.Status == StatusChamado.EmAnalise);
        var resolvidos = _chamados.Count(c => c.Status == StatusChamado.Resolvido || c.Status == StatusChamado.Fechado);
        var atrasados = _chamados.Count(c => c.PrazoSla.HasValue && c.PrazoSla < DateTime.UtcNow && c.Status != StatusChamado.Resolvido && c.Status != StatusChamado.Fechado);
        var horas = _apontamentos.Sum(a => a.HorasRegistradas);

        return new DashboardDto(total, abertos, emAtendimento, resolvidos, atrasados, horas);
    }

    private static UsuarioDto MapUsuario(Usuario usuario) => new(
        usuario.Id,
        usuario.NomeCompleto,
        usuario.Email,
        usuario.Perfil.ToString(),
        usuario.Ativo,
        usuario.ClienteId,
        usuario.SetorId);

    private static ChamadoDto MapChamado(Chamado chamado) => new(
        chamado.Id,
        chamado.Numero,
        chamado.Titulo,
        chamado.Descricao,
        chamado.Status.ToString(),
        chamado.Prioridade.ToString(),
        chamado.ClienteId,
        chamado.SolicitanteId,
        chamado.TecnicoId,
        chamado.ServicoId,
        chamado.DataAbertura,
        chamado.PrazoSla,
        chamado.RowVersion);
}

public record UsuarioDto(Guid Id, string NomeCompleto, string Email, string Perfil, bool Ativo, Guid? ClienteId, Guid? SetorId);
public record ClienteDto(Guid Id, string Nome, string Documento, string Segmento, bool Ativo);
public record SetorDto(Guid Id, Guid ClienteId, string Nome, string Responsavel, bool Ativo);
public record ServicoDto(Guid Id, string Nome, string Descricao, int PrazoPadraoHoras, bool Ativo);
public record ChamadoDto(Guid Id, string Numero, string Titulo, string Descricao, string Status, string Prioridade, Guid ClienteId, Guid SolicitanteId, Guid? TecnicoId, Guid ServicoId, DateTime DataAbertura, DateTime? PrazoSla, byte[] RowVersion, Guid? SetorId = null);
public record DashboardDto(int Total, int Abertos, int EmAtendimento, int Resolvidos, int Atrasados, decimal HorasRegistradas);
public record CreateUsuarioRequest(string NomeCompleto, string Email, string Senha, string Perfil, bool Ativo = true, Guid? ClienteId = null, Guid? SetorId = null);
public record CreateClienteRequest(string Nome, string Documento, string Segmento, bool Ativo = true);
public record CreateSetorRequest(Guid ClienteId, string Nome, string Responsavel, bool Ativo = true);
public record CreateServicoRequest(string Nome, string Descricao, int PrazoPadraoHoras = 8, bool Ativo = true);
public record CreateChamadoRequest(string Titulo, string Descricao, Guid ClienteId = default, Guid SolicitanteId = default, Guid ServicoId = default, Guid? TecnicoId = null, string Prioridade = "Media", DateTime? PrazoSla = null, Guid? Id = null, DateTime? DataAbertura = null);
public record UpdateChamadoRequest(string? Titulo = null, string? Descricao = null, string? Status = null, string? Prioridade = null, Guid? TecnicoId = null, Guid? UsuarioResponsavelId = null, string? Observacao = null);

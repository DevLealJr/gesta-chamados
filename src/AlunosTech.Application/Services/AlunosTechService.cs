namespace AlunosTech.Application.Services;

public record UsuarioDto(Guid Id, string NomeCompleto, string Email, string Perfil, bool Ativo, Guid? ClienteId, Guid? SetorId);
public record ClienteDto(Guid Id, string Nome, string? Documento, string? Segmento, bool Ativo);
public record SetorDto(Guid Id, Guid ClienteId, string Nome, string Responsavel, bool Ativo);
public record ServicoDto(Guid Id, string Nome, string? Descricao, int PrazoPadraoHoras, bool Ativo);
public record ChamadoDto(Guid Id, string Numero, string Titulo, string Descricao, string Status, string Prioridade, Guid ClienteId, Guid SolicitanteId, Guid? TecnicoId, Guid ServicoId, DateTime DataAbertura, DateTime? PrazoSla, byte[] RowVersion, Guid? SetorId = null);
public record DashboardDto(int Total, int Abertos, int EmAtendimento, int Resolvidos, int Atrasados, decimal HorasRegistradas);
public record CreateUsuarioRequest(string NomeCompleto, string Email, string Senha, string Perfil, bool Ativo = true, Guid? ClienteId = null, Guid? SetorId = null);
public record CreateClienteRequest(string Nome, string? Documento, string? Segmento, bool Ativo = true);
public record CreateSetorRequest(Guid ClienteId, string Nome, string Responsavel, bool Ativo = true);
public record CreateServicoRequest(string Nome, string? Descricao, int PrazoPadraoHoras = 8, bool Ativo = true);
public record CreateChamadoRequest(string Titulo, string Descricao, Guid ClienteId = default, Guid SolicitanteId = default, Guid ServicoId = default, Guid? TecnicoId = null, string Prioridade = "Media", DateTime? PrazoSla = null, Guid? Id = null, DateTime? DataAbertura = null);
public record UpdateChamadoRequest(string? Titulo = null, string? Descricao = null, string? Status = null, string? Prioridade = null, Guid? TecnicoId = null, Guid? UsuarioResponsavelId = null, string? Observacao = null);

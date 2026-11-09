namespace AlunosTech.Domain.Entities;

public class Chamado
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Numero { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public Guid ClienteId { get; set; }
    public Guid SolicitanteId { get; set; }
    public Guid? SetorId { get; set; }
    public Guid? TecnicoId { get; set; }
    public Guid ServicoId { get; set; }
    public StatusChamado Status { get; set; } = StatusChamado.Aberto;
    public PrioridadeChamado Prioridade { get; set; } = PrioridadeChamado.Media;
    public DateTime DataAbertura { get; set; } = DateTime.UtcNow;
    public DateTime? DataUltimaAtualizacao { get; set; }
    public DateTime? PrazoSla { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

namespace AlunosTech.Domain.Entities;

public class HistoricoChamado
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChamadoId { get; set; }
    public StatusChamado StatusAnterior { get; set; }
    public StatusChamado StatusAtual { get; set; }
    public Guid? UsuarioResponsavelId { get; set; }
    public DateTime DataHora { get; set; } = DateTime.UtcNow;
    public string Observacao { get; set; } = string.Empty;
}

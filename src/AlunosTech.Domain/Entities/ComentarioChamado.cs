namespace AlunosTech.Domain.Entities;

public class ComentarioChamado
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChamadoId { get; set; }
    public Guid AutorId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public DateTime DataHora { get; set; } = DateTime.UtcNow;
}

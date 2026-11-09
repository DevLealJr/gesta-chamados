namespace AlunosTech.Domain.Entities;

public class ApontamentoHora
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChamadoId { get; set; }
    public Guid TecnicoId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public DateTime DataInicio { get; set; } = DateTime.UtcNow;
    public DateTime? DataFim { get; set; }
    public decimal HorasRegistradas { get; set; }
}

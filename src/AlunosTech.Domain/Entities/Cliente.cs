namespace AlunosTech.Domain.Entities;

public class Cliente
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nome { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string Segmento { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

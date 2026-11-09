namespace AlunosTech.Domain.Entities;

public class Setor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Responsavel { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
}

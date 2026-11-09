namespace AlunosTech.Domain.Entities;

public class Servico
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nome { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public int PrazoPadraoHoras { get; set; } = 8;
    public bool Ativo { get; set; } = true;
}

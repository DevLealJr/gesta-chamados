namespace AlunosTech.Domain.Entities;

public enum PerfilUsuario
{
    ADMIN,
    GESTOR,
    TECNICO,
    COLABORADOR
}

public enum StatusChamado
{
    Aberto,
    EmAnalise,
    EmAtendimento,
    AguardandoSolicitante,
    Resolvido,
    Fechado,
    Cancelado
}

public enum PrioridadeChamado
{
    Baixa,
    Media,
    Alta,
    Urgente
}

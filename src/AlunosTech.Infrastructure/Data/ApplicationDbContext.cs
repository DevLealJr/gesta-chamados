using AlunosTech.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AlunosTech.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Setor> Setores => Set<Setor>();
    public DbSet<Servico> Servicos => Set<Servico>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Chamado> Chamados => Set<Chamado>();
    public DbSet<ComentarioChamado> ComentariosChamado => Set<ComentarioChamado>();
    public DbSet<ApontamentoHora> ApontamentosHora => Set<ApontamentoHora>();
    public DbSet<HistoricoChamado> HistoricosChamado => Set<HistoricoChamado>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.NomeCompleto).HasMaxLength(200);
            entity.Property(u => u.Perfil).HasConversion<string>();
        });

        builder.Entity<Chamado>(entity =>
        {
            entity.HasIndex(x => x.Numero).IsUnique();
            entity.Property(x => x.Numero).HasMaxLength(50);
            entity.Property(x => x.Titulo).HasMaxLength(200);
            entity.Property(x => x.Descricao).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsRowVersion();
        });

        builder.Entity<Setor>(entity =>
        {
            entity.HasIndex(x => new { x.ClienteId, x.Nome }).IsUnique();
            entity.Property(x => x.Nome).HasMaxLength(100);
            entity.Property(x => x.Responsavel).HasMaxLength(200);
        });

        builder.Entity<ApontamentoHora>(entity =>
        {
            entity.Property(x => x.HorasRegistradas).HasColumnType("decimal(10,2)");
        });
    }
}

public class ApplicationUser : IdentityUser
{
    public string NomeCompleto { get; set; } = string.Empty;
    public PerfilUsuario Perfil { get; set; } = PerfilUsuario.COLABORADOR;
    public bool Ativo { get; set; } = true;
    public Guid? ClienteId { get; set; }
    public Guid? SetorId { get; set; }
    public Guid? SetorSolicitadoId { get; set; }
    public string StatusCadastro { get; set; } = "Pendente";
}

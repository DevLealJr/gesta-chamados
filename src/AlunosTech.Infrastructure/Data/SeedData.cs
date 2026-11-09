using AlunosTech.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlunosTech.Infrastructure.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await context.Database.EnsureCreatedAsync();

        var clienteId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var setoresIniciais = new[]
        {
            new Setor { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), ClienteId = clienteId, Nome = "Produção", Responsavel = "Equipe de Produção" },
            new Setor { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), ClienteId = clienteId, Nome = "Administrativo", Responsavel = "Equipe Administrativa" },
            new Setor { Id = Guid.Parse("55555555-5555-5555-5555-555555555555"), ClienteId = clienteId, Nome = "RH", Responsavel = "Recursos Humanos" }
        };

        if (!await context.Clientes.AnyAsync())
        {
            context.Clientes.Add(new Cliente { Id = clienteId, Nome = "Alunos Tech", Segmento = "Empresa" });
        }

        foreach (var setor in setoresIniciais)
        {
            if (!await context.Setores.AnyAsync(x => x.ClienteId == setor.ClienteId && x.Nome == setor.Nome))
            {
                context.Setores.Add(setor);
            }
        }

        var servicoId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        if (!await context.Servicos.AnyAsync())
        {
            context.Servicos.Add(new Servico
            {
                Id = servicoId,
                Nome = "Atendimento interno",
                Descricao = "Solicitações e dificuldades dos colaboradores",
                PrazoPadraoHoras = 24
            });
        }

        await context.SaveChangesAsync();

        foreach (var role in new[] { "ADMIN", "GESTOR", "TECNICO", "COLABORADOR" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var demoUsers = new[]
        {
            new { Email = "admin@alunostech.local", Password = "Senha@123", Nome = "Administrador AlunosTech", Perfil = PerfilUsuario.ADMIN },
            new { Email = "gestor@alunostech.local", Password = "Senha@123", Nome = "Gestor de Operações", Perfil = PerfilUsuario.GESTOR },
            new { Email = "tecnico@alunostech.local", Password = "Senha@123", Nome = "Técnico de Suporte", Perfil = PerfilUsuario.TECNICO },
            new { Email = "colaborador@alunostech.local", Password = "Senha@123", Nome = "Colaborador", Perfil = PerfilUsuario.COLABORADOR }
        };

        foreach (var demo in demoUsers)
        {
            var existing = await userManager.FindByEmailAsync(demo.Email);
            if (existing is null)
            {
                var user = new ApplicationUser
                {
                    UserName = demo.Email,
                    Email = demo.Email,
                    NomeCompleto = demo.Nome,
                    Perfil = demo.Perfil,
                    Ativo = true,
                    StatusCadastro = "Aprovado",
                    ClienteId = clienteId,
                    SetorId = demo.Perfil == PerfilUsuario.ADMIN ? null : setoresIniciais[1].Id,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user, demo.Password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, demo.Perfil.ToString());
                }
            }
        }
    }
}

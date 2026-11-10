using AlunosTech.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        await context.Database.EnsureCreatedAsync();

        var clienteId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        if (!await context.Clientes.AnyAsync())
        {
            context.Clientes.Add(new Cliente
            {
                Id = clienteId,
                Nome = configuration["Company:Name"] ?? "Empresa",
                Segmento = configuration["Company:Segment"] ?? string.Empty
            });
        }
        else
        {
            var legacyCompany = await context.Clientes.FirstOrDefaultAsync(client =>
                client.Id == clienteId && client.Nome == "Alunos Tech");
            if (legacyCompany is not null)
            {
                legacyCompany.Nome = configuration["Company:Name"] ?? "Empresa";
                legacyCompany.Segmento = configuration["Company:Segment"] ?? string.Empty;
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
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Não foi possível criar o perfil {role}: {string.Join(" ", result.Errors.Select(error => error.Description))}");
                }
            }
        }

        var demoEmails = new[]
        {
            "admin@alunostech.local",
            "gestor@alunostech.local",
            "tecnico@alunostech.local",
            "colaborador@alunostech.local"
        };

        foreach (var email in demoEmails)
        {
            var demoUser = await userManager.FindByEmailAsync(email);
            if (demoUser is not null)
            {
                var result = await userManager.DeleteAsync(demoUser);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Não foi possível remover a conta fictícia {email}: {string.Join(" ", result.Errors.Select(error => error.Description))}");
                }
            }
        }

        var legacySectorIds = new[]
        {
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("55555555-5555-5555-5555-555555555555")
        };
        foreach (var sectorId in legacySectorIds)
        {
            var isReferenced = await context.Users.AnyAsync(user =>
                user.SetorId == sectorId || user.SetorSolicitadoId == sectorId) ||
                await context.Chamados.AnyAsync(ticket => ticket.SetorId == sectorId);
            if (!isReferenced)
            {
                var legacySector = await context.Setores.FirstOrDefaultAsync(sector => sector.Id == sectorId);
                if (legacySector is not null)
                {
                    context.Setores.Remove(legacySector);
                }
            }
        }
        await context.SaveChangesAsync();

        var adminEmail = configuration["BootstrapAdmin:Email"] ?? "adm_kauan@chamados.com";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            var adminPassword = configuration["BootstrapAdmin:Password"];
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                throw new InvalidOperationException(
                    "Defina BootstrapAdmin:Password com a senha inicial antes da primeira execução da API.");
            }

            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                NomeCompleto = "Kauan",
                Perfil = PerfilUsuario.ADMIN,
                Ativo = true,
                StatusCadastro = "Aprovado",
                ClienteId = clienteId,
                EmailConfirmed = true
            };
            var createResult = await userManager.CreateAsync(admin, adminPassword);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Não foi possível criar o administrador inicial: {string.Join(" ", createResult.Errors.Select(error => error.Description))}");
            }
        }
        else
        {
            admin.Perfil = PerfilUsuario.ADMIN;
            admin.Ativo = true;
            admin.StatusCadastro = "Aprovado";
            admin.ClienteId ??= clienteId;
            var updateResult = await userManager.UpdateAsync(admin);
            if (!updateResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Não foi possível atualizar o administrador inicial: {string.Join(" ", updateResult.Errors.Select(error => error.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(admin, PerfilUsuario.ADMIN.ToString()))
        {
            var roleResult = await userManager.AddToRoleAsync(admin, PerfilUsuario.ADMIN.ToString());
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Não foi possível atribuir o perfil de administrador: {string.Join(" ", roleResult.Errors.Select(error => error.Description))}");
            }
        }
    }
}

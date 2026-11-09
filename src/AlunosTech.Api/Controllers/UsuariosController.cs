using AlunosTech.Domain.Entities;
using AlunosTech.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlunosTech.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN,GESTOR")]
public class UsuariosController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    public UsuariosController(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _userManager.Users.AsNoTracking().ToListAsync();
        return Ok(users.Select(user => new UsuarioDto(
                user.Id,
                user.NomeCompleto,
                user.Email!,
                user.Perfil.ToString(),
                user.Ativo,
                user.StatusCadastro,
                user.SetorId,
                user.SetorSolicitadoId)));
    }

    [HttpGet("pendentes")]
    public async Task<IActionResult> GetPending() =>
        Ok(await _userManager.Users
            .Where(user => user.StatusCadastro == "Pendente")
            .Select(user => new UsuarioPendenteDto(
                user.Id,
                user.NomeCompleto,
                user.Email!,
                user.SetorSolicitadoId))
            .ToListAsync());

    [HttpPost("{id}/aprovar")]
    public async Task<IActionResult> Approve(string id, [FromBody] ApproveUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null || user.StatusCadastro != "Pendente")
        {
            return NotFound(new { message = "Cadastro pendente não encontrado." });
        }

        var sector = await _db.Setores.FirstOrDefaultAsync(item => item.Id == request.SetorId && item.Ativo);
        if (sector is null)
        {
            return BadRequest(new { message = "Selecione um setor ativo." });
        }

        user.SetorId = sector.Id;
        user.Ativo = true;
        user.StatusCadastro = "Aprovado";
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return BadRequest(new { message = string.Join(" ", updateResult.Errors.Select(error => error.Description)) });
        }

        var roleResult = await _userManager.AddToRoleAsync(user, PerfilUsuario.COLABORADOR.ToString());
        if (!roleResult.Succeeded && !await _userManager.IsInRoleAsync(user, PerfilUsuario.COLABORADOR.ToString()))
        {
            return BadRequest(new { message = string.Join(" ", roleResult.Errors.Select(error => error.Description)) });
        }

        return Ok(new { message = "Cadastro aprovado e vinculado ao setor." });
    }

    [HttpPost("{id}/rejeitar")]
    public async Task<IActionResult> Reject(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null || user.StatusCadastro != "Pendente")
        {
            return NotFound(new { message = "Cadastro pendente não encontrado." });
        }

        user.StatusCadastro = "Rejeitado";
        user.Ativo = false;
        var result = await _userManager.UpdateAsync(user);
        return result.Succeeded
            ? Ok(new { message = "Cadastro rejeitado." })
            : BadRequest(new { message = string.Join(" ", result.Errors.Select(error => error.Description)) });
    }
}

public sealed record UsuarioDto(
    string Id,
    string NomeCompleto,
    string Email,
    string Perfil,
    bool Ativo,
    string StatusCadastro,
    Guid? SetorId,
    Guid? SetorSolicitadoId);

public sealed record UsuarioPendenteDto(string Id, string NomeCompleto, string Email, Guid? SetorSolicitadoId);
public sealed record ApproveUserRequest(Guid SetorId);

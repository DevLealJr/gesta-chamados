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
        var usersQuery = _userManager.Users.AsNoTracking();
        if (User.IsInRole(PerfilUsuario.GESTOR.ToString()))
        {
            usersQuery = usersQuery.Where(user => user.Perfil == PerfilUsuario.TECNICO);
        }

        var users = await usersQuery.ToListAsync();
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUsuarioRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NomeCompleto) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Nome, e-mail e senha são obrigatórios." });
        }

        if (!Enum.TryParse<PerfilUsuario>(request.Perfil, true, out var profile) ||
            profile is not (PerfilUsuario.GESTOR or PerfilUsuario.TECNICO) ||
            (profile == PerfilUsuario.GESTOR && !User.IsInRole(PerfilUsuario.ADMIN.ToString())))
        {
            return Forbid();
        }

        if (request.SetorId.HasValue &&
            !await _db.Setores.AnyAsync(sector => sector.Id == request.SetorId && sector.Ativo))
        {
            return BadRequest(new { message = "O setor selecionado não existe ou está inativo." });
        }

        var email = request.Email.Trim();
        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            return Conflict(new { message = "Já existe uma conta cadastrada com este e-mail." });
        }

        var clientId = await _db.Clientes.Select(client => (Guid?)client.Id).FirstOrDefaultAsync();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            NomeCompleto = request.NomeCompleto.Trim(),
            Perfil = profile,
            Ativo = true,
            StatusCadastro = "Aprovado",
            ClienteId = clientId,
            SetorId = request.SetorId,
            EmailConfirmed = true
        };
        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return BadRequest(new { message = string.Join(" ", createResult.Errors.Select(error => error.Description)) });
        }

        var roleResult = await _userManager.AddToRoleAsync(user, profile.ToString());
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return BadRequest(new { message = string.Join(" ", roleResult.Errors.Select(error => error.Description)) });
        }

        return CreatedAtAction(nameof(GetAll), new { id = user.Id }, new UsuarioDto(
            user.Id,
            user.NomeCompleto,
            user.Email,
            user.Perfil.ToString(),
            user.Ativo,
            user.StatusCadastro,
            user.SetorId,
            user.SetorSolicitadoId));
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

        var sector = request.SetorId.HasValue
            ? await _db.Setores.FirstOrDefaultAsync(item => item.Id == request.SetorId && item.Ativo)
            : null;
        if (request.SetorId.HasValue && sector is null)
        {
            return BadRequest(new { message = "O setor selecionado não existe ou está inativo." });
        }

        user.SetorId = sector?.Id;
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
public sealed record ApproveUserRequest(Guid? SetorId);
public sealed record CreateUsuarioRequest(string NomeCompleto, string Email, string Password, string Perfil, Guid? SetorId = null);

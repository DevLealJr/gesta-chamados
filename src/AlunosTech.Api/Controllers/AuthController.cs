using System.Security.Claims;
using AlunosTech.Infrastructure.Data;
using AlunosTech.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlunosTech.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    public AuthController(
        IJwtTokenService jwtTokenService,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db)
    {
        _jwtTokenService = jwtTokenService;
        _userManager = userManager;
        _db = db;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "E-mail e senha são obrigatórios." });
        }

        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.Ativo || user.StatusCadastro != "Aprovado")
        {
            return Unauthorized(new { message = "Conta aguardando aprovação ou credenciais inválidas." });
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { message = "Credenciais inválidas." });
        }

        var role = user.Perfil.ToString();
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Email, user.Email ?? request.Email),
        };
        if (user.SetorId.HasValue)
        {
            claims.Add(new Claim("setorId", user.SetorId.Value.ToString()));
        }

        var token = _jwtTokenService.GenerateToken(user.Id, user.Email ?? request.Email, user.NomeCompleto, claims.ToArray());
        return Ok(new AuthResponse(
            token,
            user.Email ?? request.Email,
            user.NomeCompleto,
            role,
            user.Id,
            user.SetorId,
            user.ClienteId));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "usuario@chamados.com";
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = id is null ? null : await _userManager.FindByIdAsync(id);
        return user is null
            ? NotFound(new { message = "Usuário não encontrado." })
            : Ok(new { name = user.NomeCompleto, email, perfil = user.Perfil.ToString(), user.SetorId, user.ClienteId });
    }

    [AllowAnonymous]
    [HttpPost("cadastro")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NomeCompleto) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Nome, e-mail e senha são obrigatórios." });
        }

        if (await _userManager.FindByEmailAsync(request.Email.Trim()) is not null)
        {
            return Conflict(new { message = "Já existe uma conta cadastrada com este e-mail." });
        }

        var setorExiste = request.SetorId.HasValue &&
            await _db.Setores.AnyAsync(x => x.Id == request.SetorId && x.Ativo);
        if (request.SetorId.HasValue && !setorExiste)
        {
            return BadRequest(new { message = "O setor selecionado não existe ou está inativo." });
        }

        var clienteId = await _db.Clientes.Select(x => (Guid?)x.Id).FirstOrDefaultAsync();
        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            NomeCompleto = request.NomeCompleto.Trim(),
            Perfil = AlunosTech.Domain.Entities.PerfilUsuario.COLABORADOR,
            Ativo = false,
            StatusCadastro = "Pendente",
            ClienteId = clienteId,
            SetorSolicitadoId = request.SetorId
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = string.Join(" ", result.Errors.Select(error => error.Description)) });
        }

        return Accepted(new { message = "Cadastro enviado para aprovação. Você poderá entrar após a aprovação da empresa." });
    }
}

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string NomeCompleto, string Email, string Password, Guid? SetorId = null);
public record AuthResponse(string Token, string Email, string NomeCompleto, string Perfil, string Id, Guid? SetorId, Guid? ClienteId);

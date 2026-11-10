using AlunosTech.Infrastructure.Data;
using AlunosTech.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlunosTech.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SetoresController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public SetoresController(ApplicationDbContext db)
    {
        _db = db;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.Setores
            .Where(sector => sector.Ativo)
            .OrderBy(sector => sector.Nome)
            .Select(sector => new SetorResponse(sector.Id, sector.Nome))
            .ToListAsync());

    [Authorize(Roles = "ADMIN")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSetorApiRequest request)
    {
        var name = request.Nome.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "O nome do setor é obrigatório." });
        }

        var clienteId = request.ClienteId ?? await _db.Clientes.Select(client => (Guid?)client.Id).FirstOrDefaultAsync();
        if (clienteId is null || !await _db.Clientes.AnyAsync(client => client.Id == clienteId))
        {
            return BadRequest(new { message = "Não há uma empresa cadastrada para vincular o setor." });
        }

        if (await _db.Setores.AnyAsync(sector => sector.ClienteId == clienteId && sector.Nome.ToLower() == name.ToLower()))
        {
            return Conflict(new { message = "Este setor já está cadastrado." });
        }

        var sector = new Setor
        {
            ClienteId = clienteId.Value,
            Nome = name,
            Responsavel = request.Responsavel?.Trim() ?? string.Empty,
            Ativo = true
        };
        _db.Setores.Add(sector);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { id = sector.Id }, new SetorResponse(sector.Id, sector.Nome));
    }
}

public sealed record CreateSetorApiRequest(string Nome, string? Responsavel = null, Guid? ClienteId = null);
public sealed record SetorResponse(Guid Id, string Nome);

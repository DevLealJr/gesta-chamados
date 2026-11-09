using AlunosTech.Application.Services;
using AlunosTech.Domain.Entities;
using AlunosTech.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlunosTech.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN,GESTOR")]
public class ClientesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ClientesController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.Clientes.AsNoTracking()
            .Select(client => new ClienteDto(client.Id, client.Nome, client.Documento, client.Segmento, client.Ativo))
            .ToListAsync());

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Create([FromBody] CreateClienteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
        {
            return BadRequest(new { message = "Nome do cliente obrigatório." });
        }

        var client = new Cliente
        {
            Nome = request.Nome.Trim(),
            Documento = request.Documento?.Trim() ?? string.Empty,
            Segmento = request.Segmento?.Trim() ?? string.Empty,
            Ativo = request.Ativo
        };
        _db.Clientes.Add(client);
        await _db.SaveChangesAsync();
        return Created(string.Empty, new ClienteDto(client.Id, client.Nome, client.Documento, client.Segmento, client.Ativo));
    }
}

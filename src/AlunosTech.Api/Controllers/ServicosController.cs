using AlunosTech.Application.Services;
using AlunosTech.Domain.Entities;
using AlunosTech.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlunosTech.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ServicosController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ServicosController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize(Roles = "ADMIN,GESTOR,TECNICO,COLABORADOR")]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.Servicos.AsNoTracking()
            .Select(service => new ServicoDto(
                service.Id,
                service.Nome,
                service.Descricao,
                service.PrazoPadraoHoras,
                service.Ativo))
            .ToListAsync());

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Create([FromBody] CreateServicoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
        {
            return BadRequest(new { message = "Nome do serviço obrigatório." });
        }

        var service = new Servico
        {
            Nome = request.Nome.Trim(),
            Descricao = request.Descricao?.Trim() ?? string.Empty,
            PrazoPadraoHoras = request.PrazoPadraoHoras,
            Ativo = request.Ativo
        };
        _db.Servicos.Add(service);
        await _db.SaveChangesAsync();
        return Created(string.Empty, new ServicoDto(
            service.Id,
            service.Nome,
            service.Descricao,
            service.PrazoPadraoHoras,
            service.Ativo));
    }
}

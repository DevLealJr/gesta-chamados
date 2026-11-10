using System.Security.Claims;
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
public class ChamadosController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ChamadosController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] string? prioridade = null,
        [FromQuery] Guid? tecnicoId = null)
    {
        var query = VisibleTickets();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<StatusChamado>(status, true, out var parsedStatus))
            {
                return BadRequest(new { message = "Status inválido." });
            }

            query = query.Where(ticket => ticket.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(prioridade))
        {
            if (!Enum.TryParse<PrioridadeChamado>(prioridade, true, out var parsedPriority))
            {
                return BadRequest(new { message = "Prioridade inválida." });
            }

            query = query.Where(ticket => ticket.Prioridade == parsedPriority);
        }

        if (tecnicoId.HasValue)
        {
            query = query.Where(ticket => ticket.TecnicoId == tecnicoId.Value);
        }

        var tickets = await query
            .OrderByDescending(ticket => ticket.DataAbertura)
            .Select(ticket => Map(ticket))
            .ToListAsync();
        return Ok(tickets);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var ticket = await VisibleTickets().FirstOrDefaultAsync(item => item.Id == id);
        return ticket is null ? NotFound() : Ok(Map(ticket));
    }

    [HttpGet("{id:guid}/comentarios")]
    public async Task<IActionResult> GetComments(Guid id)
    {
        if (!await VisibleTickets().AnyAsync(ticket => ticket.Id == id))
        {
            return NotFound();
        }

        var comments = await _db.ComentariosChamado
            .Where(comment => comment.ChamadoId == id)
            .OrderBy(comment => comment.DataHora)
            .Select(comment => new CommentResponse(comment.Id, comment.AutorId, comment.Texto, comment.DataHora))
            .ToListAsync();
        return Ok(comments);
    }

    [HttpPost]
    [Authorize(Roles = "COLABORADOR")]
    public async Task<IActionResult> Create([FromBody] CreateChamadoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Titulo) || string.IsNullOrWhiteSpace(request.Descricao))
        {
            return BadRequest(new { message = "Título e descrição são obrigatórios." });
        }

        if (!Enum.TryParse<PrioridadeChamado>(request.Prioridade, true, out var priority))
        {
            return BadRequest(new { message = "Prioridade inválida." });
        }

        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var requesterId))
        {
            return Unauthorized(new { message = "Identificação do usuário inválida." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(item => item.Id == requesterId.ToString());
        if (user is null || !user.Ativo || user.StatusCadastro != "Aprovado")
        {
            return Forbid();
        }

        var requestedId = request.Id ?? Guid.NewGuid();
        var duplicate = await _db.Chamados.FirstOrDefaultAsync(ticket => ticket.Id == requestedId);
        if (duplicate is not null)
        {
            return duplicate.SolicitanteId == requesterId
                ? Ok(Map(duplicate))
                : Conflict(new { message = "O identificador do chamado já está em uso." });
        }

        var clientId = user.ClienteId ?? await _db.Clientes.Select(client => (Guid?)client.Id).FirstOrDefaultAsync();
        var serviceId = request.ServicoId == Guid.Empty
            ? await _db.Servicos.Select(service => (Guid?)service.Id).FirstOrDefaultAsync()
            : request.ServicoId;
        if (clientId is null || serviceId is null || !await _db.Servicos.AnyAsync(service => service.Id == serviceId))
        {
            return BadRequest(new { message = "A empresa ou o serviço padrão não está configurado." });
        }

        var ticket = new Chamado
        {
            Id = requestedId,
            Numero = $"CH-{Guid.NewGuid():N}",
            Titulo = request.Titulo.Trim(),
            Descricao = request.Descricao.Trim(),
            ClienteId = clientId.Value,
            SolicitanteId = requesterId,
            SetorId = user.SetorId,
            ServicoId = serviceId.Value,
            Status = StatusChamado.Aberto,
            Prioridade = priority,
            DataAbertura = request.DataAbertura ?? DateTime.UtcNow,
            PrazoSla = request.PrazoSla ?? DateTime.UtcNow.AddDays(2)
        };

        _db.Chamados.Add(ticket);
        _db.HistoricosChamado.Add(new HistoricoChamado
        {
            ChamadoId = ticket.Id,
            StatusAnterior = StatusChamado.Aberto,
            StatusAtual = StatusChamado.Aberto,
            UsuarioResponsavelId = requesterId,
            Observacao = "Chamado recebido pela API."
        });
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, Map(ticket));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN,GESTOR,TECNICO")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateChamadoRequest request)
    {
        var ticket = await VisibleTickets(tracking: true).FirstOrDefaultAsync(item => item.Id == id);
        if (ticket is null)
        {
            return NotFound();
        }

        if (ticket.Status is StatusChamado.Fechado or StatusChamado.Cancelado)
        {
            return Conflict(new { message = "Chamados finalizados não podem ser alterados." });
        }

        var previousStatus = ticket.Status;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<StatusChamado>(request.Status, true, out var status))
            {
                return BadRequest(new { message = "Status inválido." });
            }

            if (status is StatusChamado.Fechado or StatusChamado.Cancelado)
            {
                return BadRequest(new { message = "Somente o solicitante pode confirmar a conclusão do chamado." });
            }

            ticket.Status = status;
        }

        ticket.Titulo = request.Titulo?.Trim() ?? ticket.Titulo;
        ticket.Descricao = request.Descricao?.Trim() ?? ticket.Descricao;
        if (!string.IsNullOrWhiteSpace(request.Prioridade))
        {
            if (!Enum.TryParse<PrioridadeChamado>(request.Prioridade, true, out var priority))
            {
                return BadRequest(new { message = "Prioridade inválida." });
            }

            ticket.Prioridade = priority;
        }

        var role = User.FindFirstValue(ClaimTypes.Role);
        if (request.TecnicoId.HasValue)
        {
            if (role == "TECNICO")
            {
                return Forbid();
            }

            var technician = await _db.Users.FirstOrDefaultAsync(user =>
                user.Id == request.TecnicoId.Value.ToString() &&
                user.Perfil == PerfilUsuario.TECNICO &&
                user.Ativo &&
                user.StatusCadastro == "Aprovado");
            if (technician is null)
            {
                return BadRequest(new { message = "Selecione um técnico ativo." });
            }

            ticket.TecnicoId = request.TecnicoId;
        }

        ticket.DataUltimaAtualizacao = DateTime.UtcNow;
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? actorGuid = Guid.TryParse(actorId, out var parsedActorId) ? parsedActorId : null;

        _db.HistoricosChamado.Add(new HistoricoChamado
        {
            ChamadoId = ticket.Id,
            StatusAnterior = previousStatus,
            StatusAtual = ticket.Status,
            UsuarioResponsavelId = actorGuid,
            Observacao = request.Observacao?.Trim() ?? "Chamado atualizado pelo suporte."
        });
        if (!string.IsNullOrWhiteSpace(request.Observacao))
        {
            _db.ComentariosChamado.Add(new ComentarioChamado
            {
                ChamadoId = ticket.Id,
                AutorId = actorGuid ?? Guid.Empty,
                Texto = request.Observacao.Trim()
            });
        }

        await _db.SaveChangesAsync();
        return Ok(Map(ticket));
    }

    [HttpPost("{id:guid}/comentarios")]
    [Authorize(Roles = "COLABORADOR")]
    public async Task<IActionResult> Reply(Guid id, [FromBody] CreateCommentRequest request)
    {
        var ticket = await VisibleTickets(tracking: true).FirstOrDefaultAsync(item => item.Id == id);
        if (ticket is null)
        {
            return NotFound();
        }

        if (ticket.Status != StatusChamado.AguardandoSolicitante)
        {
            return Conflict(new { message = "Este chamado não está aguardando uma resposta sua." });
        }

        if (string.IsNullOrWhiteSpace(request.Texto))
        {
            return BadRequest(new { message = "Escreva uma resposta antes de enviar." });
        }

        var actorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var previousStatus = ticket.Status;
        ticket.Status = StatusChamado.EmAtendimento;
        ticket.DataUltimaAtualizacao = DateTime.UtcNow;
        _db.ComentariosChamado.Add(new ComentarioChamado
        {
            ChamadoId = ticket.Id,
            AutorId = actorId,
            Texto = request.Texto.Trim()
        });
        _db.HistoricosChamado.Add(new HistoricoChamado
        {
            ChamadoId = ticket.Id,
            StatusAnterior = previousStatus,
            StatusAtual = ticket.Status,
            UsuarioResponsavelId = actorId,
            Observacao = "O solicitante respondeu ao chamado."
        });

        await _db.SaveChangesAsync();
        return Ok(Map(ticket));
    }

    [HttpPost("{id:guid}/confirmacao")]
    [Authorize(Roles = "COLABORADOR")]
    public async Task<IActionResult> ConfirmResolution(Guid id, [FromBody] ConfirmResolutionRequest request)
    {
        var ticket = await VisibleTickets(tracking: true).FirstOrDefaultAsync(item => item.Id == id);
        if (ticket is null)
        {
            return NotFound();
        }

        if (ticket.Status != StatusChamado.Resolvido)
        {
            return Conflict(new { message = "Este chamado não está aguardando a confirmação da solução." });
        }

        var previousStatus = ticket.Status;
        ticket.Status = request.Resolvido ? StatusChamado.Fechado : StatusChamado.EmAtendimento;
        ticket.DataUltimaAtualizacao = DateTime.UtcNow;
        var actorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var observation = request.Resolvido
            ? "Solicitante confirmou que o problema foi resolvido."
            : "Solicitante informou que o problema não foi resolvido.";
        if (!string.IsNullOrWhiteSpace(request.Comentario))
        {
            _db.ComentariosChamado.Add(new ComentarioChamado
            {
                ChamadoId = ticket.Id,
                AutorId = actorId,
                Texto = request.Comentario.Trim()
            });
        }
        _db.HistoricosChamado.Add(new HistoricoChamado
        {
            ChamadoId = ticket.Id,
            StatusAnterior = previousStatus,
            StatusAtual = ticket.Status,
            UsuarioResponsavelId = actorId,
            Observacao = observation
        });

        await _db.SaveChangesAsync();
        return Ok(Map(ticket));
    }

    private IQueryable<Chamado> VisibleTickets(bool tracking = false)
    {
        var tickets = tracking ? _db.Chamados.AsQueryable() : _db.Chamados.AsNoTracking();
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role == "ADMIN")
        {
            return tickets;
        }

        if (role == "GESTOR")
        {
            return tickets;
        }

        if (role == "TECNICO" && Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var technicianId))
        {
            return tickets.Where(ticket => ticket.TecnicoId == technicianId);
        }

        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return tickets.Where(ticket => ticket.SolicitanteId == userId);
        }

        return tickets.Where(_ => false);
    }

    private static ChamadoDto Map(Chamado ticket) => new(
        ticket.Id,
        ticket.Numero,
        ticket.Titulo,
        ticket.Descricao,
        ticket.Status.ToString(),
        ticket.Prioridade.ToString(),
        ticket.ClienteId,
        ticket.SolicitanteId,
        ticket.TecnicoId,
        ticket.ServicoId,
        ticket.DataAbertura,
        ticket.PrazoSla,
        ticket.RowVersion,
        ticket.SetorId);
}

public sealed record CommentResponse(Guid Id, Guid AutorId, string Texto, DateTime DataHora);
public sealed record CreateCommentRequest(string Texto);
public sealed record ConfirmResolutionRequest(bool Resolvido, string? Comentario = null);

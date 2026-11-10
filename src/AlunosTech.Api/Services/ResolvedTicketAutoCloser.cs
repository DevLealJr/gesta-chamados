using AlunosTech.Domain.Entities;
using AlunosTech.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlunosTech.Api.Services;

public sealed class ResolvedTicketAutoCloser : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ResolvedTicketAutoCloser> _logger;

    public ResolvedTicketAutoCloser(
        IServiceScopeFactory scopeFactory,
        ILogger<ResolvedTicketAutoCloser> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await CloseExpiredTickets(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Falha ao fechar chamados sem confirmação após 15 dias.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CloseExpiredTickets(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cutoff = DateTime.UtcNow.AddDays(-15);
        var tickets = await db.Chamados
            .Where(ticket =>
                ticket.Status == StatusChamado.Resolvido &&
                ticket.DataUltimaAtualizacao.HasValue &&
                ticket.DataUltimaAtualizacao.Value <= cutoff)
            .ToListAsync(cancellationToken);

        foreach (var ticket in tickets)
        {
            ticket.Status = StatusChamado.Fechado;
            ticket.DataUltimaAtualizacao = DateTime.UtcNow;
            db.HistoricosChamado.Add(new HistoricoChamado
            {
                ChamadoId = ticket.Id,
                StatusAnterior = StatusChamado.Resolvido,
                StatusAtual = StatusChamado.Fechado,
                Observacao = "Chamado fechado automaticamente após 15 dias sem confirmação do solicitante."
            });
        }

        if (tickets.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Fechados automaticamente {TicketCount} chamados sem confirmação.", tickets.Count);
        }
    }
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using AlunosTech.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AlunosTech.Web.Controllers;

public class DashboardController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;

    public DashboardController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!CanManageTickets())
        {
            return RedirectToAction("Login", "Account");
        }

        var client = _httpClientFactory.CreateClient("ApiClient");
        AddAuthorization(client);

        var ticketsResponse = await client.GetAsync("api/chamados");
        var sectorsResponse = await client.GetAsync("api/setores");
        var canManageUsers = CanManageUsers();
        var pendingResponse = canManageUsers ? await client.GetAsync("api/usuarios/pendentes") : null;
        if (!ticketsResponse.IsSuccessStatusCode ||
            (pendingResponse is not null && !pendingResponse.IsSuccessStatusCode) ||
            !sectorsResponse.IsSuccessStatusCode)
        {
            TempData["StatusError"] = "Não foi possível carregar todos os dados da API. Confira se o serviço está disponível.";
        }

        var tickets = ticketsResponse.IsSuccessStatusCode
            ? await ticketsResponse.Content.ReadFromJsonAsync<List<ChamadoApiItem>>() ?? new()
            : new List<ChamadoApiItem>();
        ViewBag.PendingUsers = pendingResponse?.IsSuccessStatusCode == true
            ? await pendingResponse.Content.ReadFromJsonAsync<List<PendingUserApiItem>>() ?? new()
            : new List<PendingUserApiItem>();
        ViewBag.Sectors = sectorsResponse.IsSuccessStatusCode
            ? await sectorsResponse.Content.ReadFromJsonAsync<List<SectorApiItem>>() ?? new()
            : new List<SectorApiItem>();
        var dashboard = BuildDashboard(tickets);

        ViewBag.UserName = HttpContext.Session.GetString("userName") ?? "Usuário";
        ViewBag.CanManageUsers = canManageUsers;
        ViewBag.Tickets = tickets;
        return View(dashboard);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AtualizarStatus(Guid id, string status, string? observacao)
    {
        if (!CanManageTickets())
        {
            return RedirectToAction("Login", "Account");
        }

        var client = _httpClientFactory.CreateClient("ApiClient");
        AddAuthorization(client);

        var ticket = await client.GetFromJsonAsync<ChamadoApiItem>($"api/chamados/{id}");
        if (ticket is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var payload = new
        {
            titulo = ticket.Titulo,
            descricao = ticket.Descricao,
            status,
            prioridade = ticket.Prioridade,
            tecnicoId = ticket.TecnicoId,
            observacao = string.IsNullOrWhiteSpace(observacao)
                ? (status == "AguardandoSolicitante" ? "Aguardando informações do solicitante." :
                    status == "Resolvido" || status == "Fechado" ? "Chamado finalizado pelo suporte." : "Chamado atualizado pelo suporte.")
                : observacao
        };

        var response = await client.PutAsJsonAsync($"api/chamados/{id}", payload);
        if (response.IsSuccessStatusCode)
        {
            TempData["StatusMessage"] = $"Chamado {ticket.Numero} atualizado para {status}.";
        }
        else
        {
            TempData["StatusError"] = "A API não conseguiu atualizar o chamado.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AprovarCadastro(string id, Guid setorId)
    {
        if (!CanManageUsers()) return RedirectToAction("Login", "Account");

        var client = _httpClientFactory.CreateClient("ApiClient");
        AddAuthorization(client);
        var response = await client.PostAsJsonAsync($"api/usuarios/{Uri.EscapeDataString(id)}/aprovar", new { setorId });
        TempData[response.IsSuccessStatusCode ? "StatusMessage" : "StatusError"] =
            response.IsSuccessStatusCode ? "Cadastro aprovado e vinculado ao setor." : "Não foi possível aprovar o cadastro.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejeitarCadastro(string id)
    {
        if (!CanManageUsers()) return RedirectToAction("Login", "Account");

        var client = _httpClientFactory.CreateClient("ApiClient");
        AddAuthorization(client);
        var response = await client.PostAsync($"api/usuarios/{Uri.EscapeDataString(id)}/rejeitar", null);
        TempData[response.IsSuccessStatusCode ? "StatusMessage" : "StatusError"] =
            response.IsSuccessStatusCode ? "Cadastro rejeitado." : "Não foi possível rejeitar o cadastro.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CriarSetor(string nome, string? responsavel)
    {
        if (!CanManageUsers()) return RedirectToAction("Login", "Account");
        if (string.IsNullOrWhiteSpace(nome))
        {
            TempData["StatusError"] = "Informe o nome do setor.";
            return RedirectToAction(nameof(Index));
        }

        var client = _httpClientFactory.CreateClient("ApiClient");
        AddAuthorization(client);
        var response = await client.PostAsJsonAsync("api/setores", new { nome, responsavel });
        TempData[response.IsSuccessStatusCode ? "StatusMessage" : "StatusError"] =
            response.IsSuccessStatusCode ? "Setor cadastrado." : "Não foi possível cadastrar o setor.";
        return RedirectToAction(nameof(Index));
    }

    private bool CanManageTickets()
    {
        var profile = HttpContext.Session.GetString("perfil");
        return !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("jwt")) &&
            (profile == "ADMIN" || profile == "GESTOR" || profile == "TECNICO");
    }

    private bool CanManageUsers()
    {
        var profile = HttpContext.Session.GetString("perfil");
        return CanManageTickets() && (profile == "ADMIN" || profile == "GESTOR");
    }

    private void AddAuthorization(HttpClient client)
    {
        var jwt = HttpContext.Session.GetString("jwt");
        if (!string.IsNullOrWhiteSpace(jwt))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        }
    }

    private static DashboardDto BuildDashboard(IReadOnlyCollection<ChamadoApiItem> tickets)
    {
        var total = tickets.Count;
        var abertos = tickets.Count(t => string.Equals(t.Status, "Aberto", StringComparison.OrdinalIgnoreCase));
        var emAtendimento = tickets.Count(t =>
            string.Equals(t.Status, "EmAtendimento", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Status, "EmAnalise", StringComparison.OrdinalIgnoreCase));
        var resolvidos = tickets.Count(t =>
            string.Equals(t.Status, "Resolvido", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Status, "Fechado", StringComparison.OrdinalIgnoreCase));
        var atrasados = tickets.Count(t =>
            t.PrazoSla.HasValue &&
            t.PrazoSla.Value < DateTime.UtcNow &&
            !string.Equals(t.Status, "Resolvido", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(t.Status, "Fechado", StringComparison.OrdinalIgnoreCase));

        return new DashboardDto(total, abertos, emAtendimento, resolvidos, atrasados, 0);
    }

    public sealed record ChamadoApiItem(
        Guid Id,
        string Numero,
        string Titulo,
        string Descricao,
        string Status,
        string Prioridade,
        Guid ClienteId,
        Guid SolicitanteId,
        Guid? TecnicoId,
        Guid ServicoId,
        DateTime DataAbertura,
        DateTime? PrazoSla,
        byte[] RowVersion,
        Guid? SetorId);

    public sealed record PendingUserApiItem(string Id, string NomeCompleto, string Email, Guid? SetorSolicitadoId);
    public sealed record SectorApiItem(Guid Id, string Nome);
}

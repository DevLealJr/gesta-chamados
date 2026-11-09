using Microsoft.AspNetCore.Mvc;

namespace AlunosTech.Web.Controllers;

public class AccountController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AccountController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password)
    {
        var client = _httpClientFactory.CreateClient("ApiClient");
        var response = await client.PostAsJsonAsync("api/auth/login", new { email, password });

        if (!response.IsSuccessStatusCode)
        {
            TempData["Error"] = "Credenciais inválidas.";
            return View();
        }

        var payload = await response.Content.ReadFromJsonAsync<AuthLoginResult>();
        if (payload is null || !new[] { "ADMIN", "GESTOR", "TECNICO" }.Contains(payload.Perfil))
        {
            TempData["Error"] = "Este acesso é exclusivo da equipe responsável pelos chamados.";
            return View();
        }

        HttpContext.Session.SetString("jwt", payload.Token);
        HttpContext.Session.SetString("userName", payload.NomeCompleto);
        HttpContext.Session.SetString("perfil", payload.Perfil);

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Remove("jwt");
        HttpContext.Session.Remove("userName");
        HttpContext.Session.Remove("perfil");
        return RedirectToAction("Login");
    }

    private sealed record AuthLoginResult(string Token, string Email, string NomeCompleto, string Perfil);
}

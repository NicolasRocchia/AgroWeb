using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Text.Json;

namespace WebApplication1.Controllers;

public class AccountController : Controller
{
    private const string TokenCookie = "agro_token";
    private const string ExpiresCookie = "agro_token_expires"; // opcional

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [HttpGet]
    public IActionResult Login()
    {
        if (Request.Cookies.ContainsKey(TokenCookie))
            return RedirectToAction("Index", "Home");

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        string email,
        string password,
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Ingresá email y password.";
            return View();
        }

        var client = httpClientFactory.CreateClient("AgroApi");

        // OJO: ajustá el path si tu controller usa otro route
        var resp = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password
        });

        if (!resp.IsSuccessStatusCode)
        {
            ViewBag.Error = "Credenciales inválidas.";
            return View();
        }

        var json = await resp.Content.ReadFromJsonAsync<LoginResponseDto>(JsonOpts);
        if (json is null || string.IsNullOrWhiteSpace(json.Token))
        {
            ViewBag.Error = "Respuesta inválida del servidor.";
            return View();
        }

        // Guardar token
        Response.Cookies.Append(TokenCookie, json.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = json.ExpiresAt != default
                ? new DateTimeOffset(json.ExpiresAt)
                : DateTimeOffset.UtcNow.AddHours(12)
        });

        // (Opcional) guardar expires para debug/UX (NO HttpOnly)
        if (json.ExpiresAt != default)
        {
            Response.Cookies.Append(ExpiresCookie, json.ExpiresAt.ToString("O"), new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = new DateTimeOffset(json.ExpiresAt)
            });
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(TokenCookie);
        Response.Cookies.Delete(ExpiresCookie);
        return RedirectToAction("Login");
    }

    private sealed class LoginResponseDto
    {
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public List<string>? Roles { get; set; }
        public string? Token { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}

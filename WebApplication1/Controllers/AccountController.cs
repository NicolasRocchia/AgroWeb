using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Text.Json;

namespace WebApplication1.Controllers;

public class AccountController : Controller
{
    private const string TokenCookie = "agro_token";
    private const string ExpiresCookie = "agro_token_expires";
    private const string UserNameCookie = "agro_user_name";
    private const string UserEmailCookie = "agro_user_email";

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

        // Llamar a la API de login
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

        var expiresAt = json.ExpiresAt != default
            ? new DateTimeOffset(json.ExpiresAt)
            : DateTimeOffset.UtcNow.AddHours(12);

        // Guardar token (HttpOnly para seguridad)
        Response.Cookies.Append(TokenCookie, json.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = expiresAt
        });

        // Guardar fecha de expiración (opcional, para debug)
        if (json.ExpiresAt != default)
        {
            Response.Cookies.Append(ExpiresCookie, json.ExpiresAt.ToString("O"), new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = expiresAt
            });
        }

        // Guardar nombre de usuario (NO HttpOnly para que el Layout pueda leerlo)
        Response.Cookies.Append(UserNameCookie, json.UserName ?? "Usuario", new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = expiresAt
        });

        // Guardar email del usuario (NO HttpOnly para mostrarlo en el UI)
        Response.Cookies.Append(UserEmailCookie, json.Email ?? "", new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = expiresAt
        });

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        // Eliminar todas las cookies relacionadas con la sesión
        Response.Cookies.Delete(TokenCookie);
        Response.Cookies.Delete(ExpiresCookie);
        Response.Cookies.Delete(UserNameCookie);
        Response.Cookies.Delete(UserEmailCookie);

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
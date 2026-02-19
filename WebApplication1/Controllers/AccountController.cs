using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

public class AccountController : Controller
{
    private const string TokenCookie = "agro_token";
    private const string ExpiresCookie = "agro_token_expires";
    private const string UserNameCookie = "agro_user_name";
    private const string UserEmailCookie = "agro_user_email";

    private readonly AgroApiClient _api;
    public AccountController(AgroApiClient api) => _api = api;

    [HttpGet]
    public IActionResult Login()
    {
        if (Request.Cookies.ContainsKey(TokenCookie))
            return RedirectToAction("Index", "Home");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Ingresá email y password.";
            return View();
        }

        var result = await _api.LoginAsync(email, password);

        if (!result.Success)
        {
            ViewBag.Error = "Credenciales inválidas.";
            return View();
        }

        // Parse login response
        LoginResponseDto? json;
        try
        {
            json = JsonSerializer.Deserialize<LoginResponseDto>(result.Data!, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            ViewBag.Error = "Respuesta inválida del servidor.";
            return View();
        }

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

        // Guardar nombre de usuario
        Response.Cookies.Append(UserNameCookie, json.UserName ?? "Usuario", new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = expiresAt
        });

        // Guardar email del usuario
        Response.Cookies.Append(UserEmailCookie, json.Email ?? "", new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = expiresAt
        });

        // Loguear la WEB (cookie auth) para [Authorize] y User.IsInRole(...)
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, json.UserId.ToString()),
            new(ClaimTypes.Name, json.UserName ?? json.Email ?? "Usuario"),
            new(ClaimTypes.Email, json.Email ?? string.Empty),
        };

        if (json.Roles != null)
        {
            foreach (var role in json.Roles.Where(r => !string.IsNullOrWhiteSpace(r)))
                claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = expiresAt.UtcDateTime
            });

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        Response.Cookies.Delete(TokenCookie);
        Response.Cookies.Delete(ExpiresCookie);
        Response.Cookies.Delete(UserNameCookie);
        Response.Cookies.Delete(UserEmailCookie);

        return RedirectToAction("Login");
    }

    // =============================================
    // REGISTRO
    // =============================================

    [HttpGet]
    public IActionResult Register()
    {
        if (Request.Cookies.ContainsKey(TokenCookie))
            return RedirectToAction("Index", "Home");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        string userName, string email, string password,
        string confirmPassword, string taxId, string? phoneNumber)
    {
        ViewBag.UserName = userName;
        ViewBag.Email = email;
        ViewBag.TaxId = taxId;
        ViewBag.PhoneNumber = phoneNumber;

        if (string.IsNullOrWhiteSpace(userName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(taxId))
        {
            ViewBag.Error = "Completá todos los campos obligatorios.";
            return View();
        }

        if (password != confirmPassword)
        {
            ViewBag.Error = "Las contraseñas no coinciden.";
            return View();
        }

        var result = await _api.RegisterAsync(new
        {
            userName = userName.Trim(),
            email = email.Trim(),
            password,
            confirmPassword,
            taxId = taxId.Trim(),
            phoneNumber = phoneNumber?.Trim()
        });

        if (result.Success)
        {
            TempData["Success"] = "¡Cuenta creada exitosamente! Ya podés iniciar sesión.";
            return RedirectToAction("Login");
        }

        ViewBag.Error = result.Error;
        return View();
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
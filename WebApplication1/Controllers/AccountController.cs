using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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

        TempData.Clear();

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
        string confirmPassword, string taxId, string? phoneNumber,
        string roleType = "productor")
    {
        ViewBag.UserName = userName;
        ViewBag.Email = email;
        ViewBag.TaxId = taxId;
        ViewBag.PhoneNumber = phoneNumber;
        ViewBag.RoleType = roleType;

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
            phoneNumber = phoneNumber?.Trim(),
            roleType = roleType?.Trim() ?? "productor"
        });

        if (result.Success)
        {
            TempData["Success"] = "¡Cuenta creada exitosamente! Ya podés iniciar sesión.";
            return RedirectToAction("Login");
        }

        ViewBag.Error = result.Error;
        return View();
    }

    // =============================================
    // PERFIL DE USUARIO
    // =============================================

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var result = await _api.GetProfileAsync();
        ViewBag.ProfileJson = result.Success ? result.Data : "{}";
        if (!result.Success) ViewBag.Error = result.Error;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> UpdateProfile(string userName, string email, string? phoneNumber, string? taxId)
    {
        var result = await _api.UpdateProfileAsync(new
        {
            userName = userName?.Trim(),
            email = email?.Trim(),
            phoneNumber = phoneNumber?.Trim(),
            taxId = taxId?.Trim()
        });

        if (result.Success)
        {
            TempData["Success"] = "Perfil actualizado correctamente.";

            // Update cookie claims with new name/email
            var claims = new List<System.Security.Claims.Claim>
            {
                new(System.Security.Claims.ClaimTypes.NameIdentifier,
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0"),
                new(System.Security.Claims.ClaimTypes.Name, userName?.Trim() ?? "Usuario"),
                new(System.Security.Claims.ClaimTypes.Email, email?.Trim() ?? ""),
            };

            foreach (var role in User.Claims
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                .Select(c => c.Value))
            {
                claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role));
            }

            var identity = new System.Security.Claims.ClaimsIdentity(claims,
                Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new Microsoft.AspNetCore.Authentication.AuthenticationProperties { IsPersistent = true });
        }
        else
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction("Profile");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        if (newPassword != confirmPassword)
        {
            TempData["Error"] = "Las contraseñas no coinciden.";
            return RedirectToAction("Profile");
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            TempData["Error"] = "La nueva contraseña debe tener al menos 6 caracteres.";
            return RedirectToAction("Profile");
        }

        var result = await _api.ChangePasswordAsync(new
        {
            currentPassword,
            newPassword,
            confirmPassword
        });

        TempData[result.Success ? "Success" : "Error"] =
            result.Success ? "Contraseña actualizada correctamente." : result.Error;

        return RedirectToAction("Profile");
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WebApplication1.Models.Admin;
using WebApplication1.Models.Users;

namespace WebApplication1.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // =============================================
    // LISTADO DE USUARIOS
    // =============================================

    [HttpGet]
    public async Task<IActionResult> Users(
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient("AgroApi");

        var usersTask = client.GetAsync("/api/admin/users");
        var rolesTask = client.GetAsync("/api/admin/roles");

        await Task.WhenAll(usersTask, rolesTask);

        var usersResp = usersTask.Result;
        var rolesResp = rolesTask.Result;

        var users = new List<UserListItemDto>();
        var roles = new List<RoleDto>();

        if (usersResp.IsSuccessStatusCode)
            users = await usersResp.Content.ReadFromJsonAsync<List<UserListItemDto>>(JsonOpts) ?? new();

        if (rolesResp.IsSuccessStatusCode)
            roles = await rolesResp.Content.ReadFromJsonAsync<List<RoleDto>>(JsonOpts) ?? new();

        if (!usersResp.IsSuccessStatusCode)
            ViewBag.Error = $"No se pudo obtener el listado de usuarios. HTTP {(int)usersResp.StatusCode}";

        return View(new UsersIndexViewModel
        {
            Users = users,
            Roles = roles
        });
    }

    // =============================================
    // CREAR USUARIO
    // =============================================

    [HttpGet]
    public async Task<IActionResult> CreateUser(
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient("AgroApi");
        var rolesResp = await client.GetAsync("/api/admin/roles");

        var roles = new List<RoleDto>();
        if (rolesResp.IsSuccessStatusCode)
            roles = await rolesResp.Content.ReadFromJsonAsync<List<RoleDto>>(JsonOpts) ?? new();

        return View(new CreateUserViewModel { Roles = roles });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(
        string userName,
        string email,
        string password,
        string taxId,
        string? phoneNumber,
        long roleId,
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient("AgroApi");

        // Cargar roles para re-mostrar el form en caso de error
        var rolesResp = await client.GetAsync("/api/admin/roles");
        var roles = new List<RoleDto>();
        if (rolesResp.IsSuccessStatusCode)
            roles = await rolesResp.Content.ReadFromJsonAsync<List<RoleDto>>(JsonOpts) ?? new();

        var model = new CreateUserViewModel
        {
            Roles = roles,
            UserName = userName,
            Email = email,
            TaxId = taxId,
            PhoneNumber = phoneNumber,
            RoleId = roleId
        };

        // Validación básica
        if (string.IsNullOrWhiteSpace(userName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(taxId) ||
            roleId <= 0)
        {
            ViewBag.Error = "Completá todos los campos obligatorios.";
            return View(model);
        }

        var resp = await client.PostAsJsonAsync("/api/admin/users", new
        {
            userName = userName.Trim(),
            email = email.Trim(),
            password,
            taxId = taxId.Trim(),
            phoneNumber = phoneNumber?.Trim(),
            roleId
        });

        if (resp.IsSuccessStatusCode)
        {
            TempData["Success"] = "Usuario creado exitosamente.";
            return RedirectToAction("Users");
        }

        // Manejar errores
        try
        {
            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("error", out var errorProp))
            {
                ViewBag.Error = errorProp.GetString();
                return View(model);
            }

            if (doc.RootElement.TryGetProperty("errors", out var errorsProp))
            {
                var errorList = new List<string>();
                foreach (var err in errorsProp.EnumerateArray())
                    errorList.Add(err.GetString() ?? "Error desconocido");
                ViewBag.Errors = errorList;
                return View(model);
            }
        }
        catch { }

        ViewBag.Error = $"Error al crear el usuario. (HTTP {(int)resp.StatusCode})";
        return View(model);
    }

    // =============================================
    // BLOQUEAR / DESBLOQUEAR
    // =============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleBlock(
        long id,
        bool isBlocked,
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient("AgroApi");

        var resp = await client.PutAsJsonAsync($"/api/admin/users/{id}/block", new
        {
            isBlocked
        });

        if (resp.IsSuccessStatusCode)
        {
            TempData["Success"] = isBlocked ? "Usuario bloqueado." : "Usuario desbloqueado.";
        }
        else
        {
            try
            {
                var body = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var err))
                    TempData["Error"] = err.GetString();
                else
                    TempData["Error"] = $"Error al cambiar estado. HTTP {(int)resp.StatusCode}";
            }
            catch
            {
                TempData["Error"] = $"Error al cambiar estado. HTTP {(int)resp.StatusCode}";
            }
        }

        return RedirectToAction("Users");
    }

    // =============================================
    // CAMBIAR ROL
    // =============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(
        long id,
        long roleId,
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient("AgroApi");

        var resp = await client.PutAsJsonAsync($"/api/admin/users/{id}/role", new
        {
            roleId
        });

        if (resp.IsSuccessStatusCode)
        {
            TempData["Success"] = "Rol actualizado correctamente.";
        }
        else
        {
            try
            {
                var body = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var err))
                    TempData["Error"] = err.GetString();
                else
                    TempData["Error"] = $"Error al cambiar rol. HTTP {(int)resp.StatusCode}";
            }
            catch
            {
                TempData["Error"] = $"Error al cambiar rol. HTTP {(int)resp.StatusCode}";
            }
        }

        return RedirectToAction("Users");
    }

    // =============================================
    // INSIGHTS
    // =============================================

    [HttpGet]
    public async Task<IActionResult> Insights(
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient("AgroApi");
        var resp = await client.GetAsync("/api/admin/insights");

        if (!resp.IsSuccessStatusCode)
        {
            ViewBag.Error = $"No se pudieron obtener las estadísticas. HTTP {(int)resp.StatusCode}";
            return View(new InsightsDto());
        }

        var data = await resp.Content.ReadFromJsonAsync<InsightsDto>(JsonOpts) ?? new InsightsDto();
        return View(data);
    }
}

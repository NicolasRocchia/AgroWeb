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
    // GESTIÓN DE MUNICIPIOS
    // =============================================

    private static decimal? ParseCoord(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var s = raw.Trim();

        // Aceptar "-32.692014" o "-32,692014"
        if (decimal.TryParse(s, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.CurrentCulture, out var v))
            return v;

        if (decimal.TryParse(s.Replace(",", "."),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out v))
            return v;

        return null;
    }

    private static string? ValidateCoords(decimal? lat, decimal? lng)
    {
        if (lat is not null && (lat < -90 || lat > 90))
            return "Latitud inválida. Debe estar entre -90 y 90.";

        if (lng is not null && (lng < -180 || lng > 180))
            return "Longitud inválida. Debe estar entre -180 y 180.";

        return null;
    }

    [HttpGet]
    public async Task<IActionResult> Municipalities(
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient("AgroApi");

        var munResp = await client.GetAsync("/api/municipalities");
        var municipalities = new List<WebApplication1.Models.Admin.MunicipalityAdminDto>();

        if (munResp.IsSuccessStatusCode)
            municipalities = await munResp.Content.ReadFromJsonAsync<List<WebApplication1.Models.Admin.MunicipalityAdminDto>>(JsonOpts) ?? new();

        return View(new WebApplication1.Models.Admin.MunicipalitiesViewModel
        {
            Municipalities = municipalities
        });
    }

    [HttpGet]
    public async Task<IActionResult> CreateMunicipality(
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient("AgroApi");
        var users = await GetAvailableMunicipioUsers(client);

        return View(new WebApplication1.Models.Admin.CreateMunicipalityViewModel
        {
            AvailableUsers = users
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMunicipality(
        string name,
        string? province,
        string? department,
        string? latitude,    // 👈 antes decimal?
        string? longitude,   // 👈 antes decimal?
        long? userId,
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient("AgroApi");

        var lat = ParseCoord(latitude);
        var lng = ParseCoord(longitude);

        var model = new WebApplication1.Models.Admin.CreateMunicipalityViewModel
        {
            AvailableUsers = await GetAvailableMunicipioUsers(client),
            Name = name,
            Province = province,
            Department = department,
            Latitude = lat,      // 👈 el VM sigue siendo decimal?
            Longitude = lng,     // 👈 el VM sigue siendo decimal?
            UserId = userId
        };

        if (string.IsNullOrWhiteSpace(name))
        {
            ViewBag.Error = "El nombre del municipio es obligatorio.";
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(latitude) && lat is null)
        {
            ViewBag.Error = "Latitud inválida. Usá formato -32.692014 o -32,692014.";
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(longitude) && lng is null)
        {
            ViewBag.Error = "Longitud inválida. Usá formato -62.1026759 o -62,1026759.";
            return View(model);
        }

        var coordErr = ValidateCoords(lat, lng);
        if (coordErr is not null)
        {
            ViewBag.Error = coordErr;
            return View(model);
        }

        var resp = await client.PostAsJsonAsync("/api/municipalities", new
        {
            name = name.Trim(),
            province = province?.Trim(),
            department = department?.Trim(),
            latitude = lat,
            longitude = lng,
            userId
        });

        if (resp.IsSuccessStatusCode)
        {
            TempData["Success"] = $"Municipio '{name}' creado exitosamente.";
            return RedirectToAction("Municipalities");
        }

        try
        {
            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            ViewBag.Error = doc.RootElement.TryGetProperty("error", out var err)
                ? err.GetString()
                : $"Error al crear municipio. (HTTP {(int)resp.StatusCode})";
        }
        catch
        {
            ViewBag.Error = $"Error al crear municipio. (HTTP {(int)resp.StatusCode})";
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> EditMunicipality(
        long id,
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient("AgroApi");

        var munResp = await client.GetAsync($"/api/municipalities/{id}");
        if (!munResp.IsSuccessStatusCode)
        {
            TempData["Error"] = "Municipio no encontrado.";
            return RedirectToAction("Municipalities");
        }

        var municipality = await munResp.Content.ReadFromJsonAsync<WebApplication1.Models.Admin.MunicipalityAdminDto>(JsonOpts);
        var users = await GetAvailableMunicipioUsers(client);

        // Agregar el usuario actual asignado a la lista si existe
        if (municipality?.UserId != null && municipality.UserName != null)
        {
            if (!users.Any(u => u.Id == municipality.UserId))
            {
                users.Insert(0, new WebApplication1.Models.Admin.MunicipioUserDto
                {
                    Id = municipality.UserId.Value,
                    UserName = municipality.UserName,
                    EmailNormalized = ""
                });
            }
        }

        return View(new WebApplication1.Models.Admin.EditMunicipalityViewModel
        {
            Municipality = municipality ?? new(),
            AvailableUsers = users
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMunicipality(
        long id,
        string? name,
        string? province,
        string? department,
        string? latitude,   // 👈 antes decimal?
        string? longitude,  // 👈 antes decimal?
        long? userId,
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient("AgroApi");

        var lat = ParseCoord(latitude);
        var lng = ParseCoord(longitude);

        if (!string.IsNullOrWhiteSpace(latitude) && lat is null)
        {
            TempData["Error"] = "Latitud inválida. Usá formato -32.692014 o -32,692014.";
            return RedirectToAction("EditMunicipality", new { id });
        }

        if (!string.IsNullOrWhiteSpace(longitude) && lng is null)
        {
            TempData["Error"] = "Longitud inválida. Usá formato -62.1026759 o -62,1026759.";
            return RedirectToAction("EditMunicipality", new { id });
        }

        var coordErr = ValidateCoords(lat, lng);
        if (coordErr is not null)
        {
            TempData["Error"] = coordErr;
            return RedirectToAction("EditMunicipality", new { id });
        }

        var resp = await client.PutAsJsonAsync($"/api/municipalities/{id}", new
        {
            name,
            province,
            department,
            latitude = lat,
            longitude = lng,
            userId
        });

        if (resp.IsSuccessStatusCode)
        {
            TempData["Success"] = "Municipio actualizado correctamente.";
            return RedirectToAction("Municipalities");
        }

        try
        {
            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            TempData["Error"] = doc.RootElement.TryGetProperty("error", out var err)
                ? err.GetString()
                : $"Error al actualizar. (HTTP {(int)resp.StatusCode})";
        }
        catch
        {
            TempData["Error"] = $"Error al actualizar. (HTTP {(int)resp.StatusCode})";
        }

        return RedirectToAction("EditMunicipality", new { id });
    }

    private async Task<List<WebApplication1.Models.Admin.MunicipioUserDto>> GetAvailableMunicipioUsers(HttpClient client)
    {
        var resp = await client.GetAsync("/api/admin/users/available-municipio");
        if (resp.IsSuccessStatusCode)
            return await resp.Content.ReadFromJsonAsync<List<WebApplication1.Models.Admin.MunicipioUserDto>>(JsonOpts) ?? new();
        return new();
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

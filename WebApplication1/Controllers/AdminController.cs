using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WebApplication1.Models.Admin;
using WebApplication1.Models.Municipio;
using WebApplication1.Models.Users;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AgroApiClient _api;
    public AdminController(AgroApiClient api) => _api = api;

    // =============================================
    // LISTADO DE USUARIOS
    // =============================================

    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var usersResult = await _api.GetUsersAsync();
        var rolesResult = await _api.GetRolesAsync();

        if (!usersResult.Success)
            ViewBag.Error = $"No se pudo obtener el listado de usuarios. {usersResult.Error}";

        return View(new UsersIndexViewModel
        {
            Users = usersResult.Data ?? new(),
            Roles = rolesResult.Data ?? new()
        });
    }

    // =============================================
    // CREAR USUARIO
    // =============================================

    [HttpGet]
    public async Task<IActionResult> CreateUser()
    {
        var rolesResult = await _api.GetRolesAsync();

        return View(new CreateUserViewModel
        {
            Roles = rolesResult.Data ?? new()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(
        string userName, string email, string password,
        string taxId, string? phoneNumber, long roleId)
    {
        var rolesResult = await _api.GetRolesAsync();

        var model = new CreateUserViewModel
        {
            Roles = rolesResult.Data ?? new(),
            UserName = userName,
            Email = email,
            TaxId = taxId,
            PhoneNumber = phoneNumber,
            RoleId = roleId
        };

        if (string.IsNullOrWhiteSpace(userName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(taxId) ||
            roleId <= 0)
        {
            ViewBag.Error = "Completá todos los campos obligatorios.";
            return View(model);
        }

        var result = await _api.CreateUserAsync(new
        {
            userName = userName.Trim(),
            email = email.Trim(),
            password,
            taxId = taxId.Trim(),
            phoneNumber = phoneNumber?.Trim(),
            roleId
        });

        if (result.Success)
        {
            TempData["Success"] = "Usuario creado exitosamente.";
            return RedirectToAction("Users");
        }

        // Si ExtractErrorDetailed unió múltiples errores con " | ", separarlos
        if (result.Error?.Contains(" | ") == true)
            ViewBag.Errors = result.Error.Split(" | ", StringSplitOptions.RemoveEmptyEntries).ToList();
        else
            ViewBag.Error = result.Error;

        return View(model);
    }

    // =============================================
    // BLOQUEAR / DESBLOQUEAR
    // =============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleBlock(long id, bool isBlocked)
    {
        var result = await _api.ToggleBlockUserAsync(id, isBlocked);

        TempData[result.Success ? "Success" : "Error"] =
            result.Success
                ? (isBlocked ? "Usuario bloqueado." : "Usuario desbloqueado.")
                : result.Error;

        return RedirectToAction("Users");
    }

    // =============================================
    // CAMBIAR ROL
    // =============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(long id, long roleId)
    {
        var result = await _api.ChangeUserRoleAsync(id, roleId);

        TempData[result.Success ? "Success" : "Error"] =
            result.Success
                ? "Rol actualizado correctamente."
                : result.Error;

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
    public async Task<IActionResult> Municipalities()
    {
        var result = await _api.GetMunicipalitiesAsync();

        if (!result.Success)
            ViewBag.Error = $"No se pudo obtener el listado de municipios. {result.Error}";

        return View(new MunicipalitiesViewModel
        {
            Municipalities = result.Data ?? new()
        });
    }

    [HttpGet]
    public async Task<IActionResult> CreateMunicipality()
    {
        var usersResult = await _api.GetAvailableMunicipioUsersAsync();

        return View(new CreateMunicipalityViewModel
        {
            AvailableUsers = usersResult.Data ?? new()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMunicipality(
        string name, string? province, string? department,
        string? latitude, string? longitude, long? userId)
    {
        var lat = ParseCoord(latitude);
        var lng = ParseCoord(longitude);

        var usersResult = await _api.GetAvailableMunicipioUsersAsync();

        var model = new CreateMunicipalityViewModel
        {
            AvailableUsers = usersResult.Data ?? new(),
            Name = name,
            Province = province,
            Department = department,
            Latitude = lat,
            Longitude = lng,
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

        var result = await _api.CreateMunicipalityAsync(new
        {
            name = name.Trim(),
            province = province?.Trim(),
            department = department?.Trim(),
            latitude = lat,
            longitude = lng,
            userId
        });

        if (result.Success)
        {
            TempData["Success"] = $"Municipio '{name}' creado exitosamente.";
            return RedirectToAction("Municipalities");
        }

        ViewBag.Error = result.Error;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> EditMunicipality(long id)
    {
        var munResult = await _api.GetMunicipalityAsync(id);

        if (!munResult.Success)
        {
            TempData["Error"] = "Municipio no encontrado.";
            return RedirectToAction("Municipalities");
        }

        var municipality = munResult.Data!;
        var usersResult = await _api.GetAvailableMunicipioUsersAsync();
        var users = usersResult.Data ?? new();

        // Agregar el usuario actual asignado a la lista si existe
        if (municipality.UserId != null && municipality.UserName != null)
        {
            if (!users.Any(u => u.Id == municipality.UserId))
            {
                users.Insert(0, new MunicipioUserDto
                {
                    Id = municipality.UserId.Value,
                    UserName = municipality.UserName,
                    EmailNormalized = ""
                });
            }
        }

        return View(new EditMunicipalityViewModel
        {
            Municipality = municipality,
            AvailableUsers = users
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMunicipality(
        long id, string? name, string? province, string? department,
        string? latitude, string? longitude, long? userId)
    {
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

        var result = await _api.UpdateMunicipalityAsync(id, new
        {
            name,
            province,
            department,
            latitude = lat,
            longitude = lng,
            userId
        });

        if (result.Success)
        {
            TempData["Success"] = "Municipio actualizado correctamente.";
            return RedirectToAction("Municipalities");
        }

        TempData["Error"] = result.Error;
        return RedirectToAction("EditMunicipality", new { id });
    }

    // =============================================
    // INSIGHTS
    // =============================================

    [HttpGet]
    public async Task<IActionResult> Insights()
    {
        var result = await _api.GetInsightsAsync();

        if (!result.Success)
            ViewBag.Error = $"No se pudieron obtener las estadísticas. {result.Error}";

        return View(result.Data ?? new InsightsDto());
    }

    // =============================================
    // VERIFICACIÓN DE APLICADORES
    // =============================================

    [HttpGet]
    public IActionResult ApplicatorVerification() => View();

    [HttpGet]
    public async Task<IActionResult> ApplicatorProfilesJson()
    {
        var result = await _api.GetApplicatorProfilesAsync();

        if (!result.Success)
            return StatusCode((int)result.StatusCode);

        return Content(result.Data ?? "[]", "application/json");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyApplicator(long profileId, bool approve)
    {
        var result = await _api.VerifyApplicatorProfileAsync(profileId, approve);

        TempData[result.Success ? "Success" : "Error"] =
            result.Success
                ? (approve ? "Aplicador aprobado." : "Verificación revocada.")
                : "Error al procesar la verificación.";

        return RedirectToAction("ApplicatorVerification");
    }

    // =============================================
    // GEO INSIGHTS (Mapa Territorial Global)
    // =============================================

    [HttpGet]
    public async Task<IActionResult> GeoInsights(
        long? municipalityId = null,
        string? dateFrom = null, string? dateTo = null,
        string? crop = null, string? toxClass = null,
        string? productName = null, string? advisorName = null,
        string? requesterName = null)
    {
        // Cargar lista de municipios para el select
        var munResult = await _api.GetMunicipalitiesAsync();
        ViewBag.Municipalities = munResult.Data ?? new List<MunicipalityAdminDto>();
        ViewBag.SelectedMunicipalityId = municipalityId;

        var result = await _api.GetGeoInsightsAsync(
            municipalityId: municipalityId,
            dateFrom: dateFrom, dateTo: dateTo,
            crop: crop, toxClass: toxClass,
            productName: productName, advisorName: advisorName,
            requesterName: requesterName);

        if (!result.Success)
            ViewBag.Error = $"No se pudieron obtener datos geoespaciales. {result.Error}";

        return View(new GeoInsightsViewModel
        {
            JsonData = result.Data ?? ""
        });
    }

    // =============================================
    // GEO INSIGHTS DATA (AJAX JSON) - Proxy a AgroApi
    // =============================================

    [HttpGet]
    public async Task<IActionResult> GeoInsightsData(
        long? municipalityId = null,
        string? dateFrom = null, string? dateTo = null,
        string? crop = null, string? toxClass = null,
        string? productName = null, string? advisorName = null,
        string? requesterName = null)
    {
        var result = await _api.GetGeoInsightsAsync(
            municipalityId: municipalityId,
            dateFrom: dateFrom, dateTo: dateTo,
            crop: crop, toxClass: toxClass,
            productName: productName, advisorName: advisorName,
            requesterName: requesterName);

        if (!result.Success)
        {
            return StatusCode(500, new
            {
                message = $"No se pudieron obtener datos geoespaciales. {result.Error}"
            });
        }

        return Content(result.Data ?? "{}", "application/json");
    }

    // =============================================
    // EIQ — PARSEO DE ACTIVOS (temporal)
    // =============================================

    [HttpPost]
    public async Task<IActionResult> RunEiqParse()
    {
        var result = await _api.PostAsync("/api/admin/eiq/parse-products", new { });
        if (!result.Success)
            return StatusCode(500, new { error = result.Error });

        return Content(result.Data ?? "{}", "application/json");
    }

    [HttpGet]
    public async Task<IActionResult> GetEiqStats()
    {
        var result = await _api.GetRawJsonAsync("/api/admin/eiq/stats");
        if (!result.Success)
            return StatusCode(500, new { error = result.Error });

        return Content(result.Data ?? "{}", "application/json");
    }
}

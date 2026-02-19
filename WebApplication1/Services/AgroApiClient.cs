using System.Net;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using WebApplication1.Models.Admin;
using WebApplication1.Models.Municipio;
using WebApplication1.Models.Recipes;
using WebApplication1.Models.Users;

namespace WebApplication1.Services;

/// <summary>
/// Typed HTTP client for all AgroConnect API calls.
/// Centralizes: serialization options, querystring building, 
/// error extraction, and endpoint routing.
/// </summary>
public class AgroApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AgroApiClient(HttpClient http)
    {
        _http = http;
    }

    // ══════════════════════════════════════════════
    // GENERIC HELPERS
    // ══════════════════════════════════════════════

    /// <summary>
    /// GET + deserialize. Returns (data, null) on success, (default, error) on failure.
    /// </summary>
    public async Task<ApiResult<T>> GetAsync<T>(string url) where T : class, new()
    {
        var resp = await _http.GetAsync(url);

        if (!resp.IsSuccessStatusCode)
            return new ApiResult<T>(new T(), resp.StatusCode, await ExtractError(resp));

        var data = await resp.Content.ReadFromJsonAsync<T>(JsonOpts);
        return new ApiResult<T>(data ?? new T(), resp.StatusCode);
    }

    /// <summary>
    /// GET raw JSON string (for GeoInsights and other passthrough scenarios).
    /// </summary>
    public async Task<ApiResult<string>> GetRawJsonAsync(string url)
    {
        var resp = await _http.GetAsync(url);

        if (!resp.IsSuccessStatusCode)
            return new ApiResult<string>("", resp.StatusCode, await ExtractError(resp));

        var json = await resp.Content.ReadAsStringAsync();
        return new ApiResult<string>(json, resp.StatusCode);
    }

    /// <summary>
    /// POST with JSON body. Returns (responseBody, null) on success, (null, error) on failure.
    /// </summary>
    public async Task<ApiResult<string>> PostAsync<TBody>(string url, TBody body)
    {
        var resp = await _http.PostAsJsonAsync(url, body);

        if (!resp.IsSuccessStatusCode)
            return new ApiResult<string>(null, resp.StatusCode, await ExtractErrorDetailed(resp));

        var raw = await resp.Content.ReadAsStringAsync();
        return new ApiResult<string>(raw, resp.StatusCode);
    }

    /// <summary>
    /// POST with multipart/form-data (for file upload).
    /// </summary>
    public async Task<ApiResult<string>> PostMultipartAsync(string url, MultipartFormDataContent content)
    {
        var resp = await _http.PostAsync(url, content);

        if (!resp.IsSuccessStatusCode)
            return new ApiResult<string>(null, resp.StatusCode, await ExtractErrorDetailed(resp));

        var raw = await resp.Content.ReadAsStringAsync();
        return new ApiResult<string>(raw, resp.StatusCode);
    }

    /// <summary>
    /// PUT with JSON body. Returns success/failure with error extraction.
    /// </summary>
    public async Task<ApiResult<string>> PutAsync<TBody>(string url, TBody body)
    {
        var resp = await _http.PutAsJsonAsync(url, body);

        if (!resp.IsSuccessStatusCode)
            return new ApiResult<string>(null, resp.StatusCode, await ExtractErrorDetailed(resp));

        var raw = await resp.Content.ReadAsStringAsync();
        return new ApiResult<string>(raw, resp.StatusCode);
    }

    // ══════════════════════════════════════════════
    // RECIPES
    // ══════════════════════════════════════════════

    public Task<ApiResult<PagedResponse<RecipeListItemDto>>> GetRecipesAsync(
        int page = 1, int pageSize = 20,
        string? status = null, string? searchText = null, long? rfdNumber = null)
    {
        var qs = new QueryBuilder()
            .Add("Page", page)
            .Add("PageSize", pageSize)
            .Add("Status", status)
            .Add("SearchText", searchText)
            .Add("RfdNumber", rfdNumber);

        return GetAsync<PagedResponse<RecipeListItemDto>>($"/api/recipes{qs}");
    }

    public Task<ApiResult<RecipeDetailDto>> GetRecipeAsync(long id)
        => GetAsync<RecipeDetailDto>($"/api/recipes/{id}");

    public Task<ApiResult<string>> ChangeRecipeStatusAsync(long id, string status)
        => PutAsync($"/api/recipes/{id}/status", new { status });

    public Task<ApiResult<string>> AssignRecipeToMunicipalityAsync(long id, long municipalityId)
        => PostAsync($"/api/recipes/{id}/assign-municipality", new { municipalityId });

    public Task<ApiResult<string>> ReviewRecipeAsync(long id, string action, string? observation, long? targetMunicipalityId)
        => PostAsync($"/api/recipes/{id}/review", new { action, observation, targetMunicipalityId });

    public Task<ApiResult<string>> SendRecipeMessageAsync(long id, string message)
        => PostAsync($"/api/recipes/{id}/messages", new { message });

    public Task<ApiResult<string>> ImportPdfAsync(MultipartFormDataContent content)
        => PostMultipartAsync("/api/recipes/import-pdf", content);

    // ══════════════════════════════════════════════
    // GEO INSIGHTS
    // ══════════════════════════════════════════════

    public Task<ApiResult<string>> GetGeoInsightsAsync(
        long? municipalityId = null,
        string? dateFrom = null, string? dateTo = null,
        string? crop = null, string? toxClass = null,
        string? productName = null, string? advisorName = null)
    {
        var qs = new QueryBuilder()
            .Add("municipalityId", municipalityId)
            .Add("DateFrom", dateFrom)
            .Add("DateTo", dateTo)
            .Add("Crop", crop)
            .Add("ToxClass", toxClass)
            .Add("ProductName", productName)
            .Add("AdvisorName", advisorName);

        return GetRawJsonAsync($"/api/recipes/geo-insights{qs}");
    }

    // ══════════════════════════════════════════════
    // MUNICIPALITIES
    // ══════════════════════════════════════════════

    public Task<ApiResult<List<MunicipalityAdminDto>>> GetMunicipalitiesAsync()
        => GetAsync<List<MunicipalityAdminDto>>("/api/municipalities");

    public Task<ApiResult<MunicipalityAdminDto>> GetMunicipalityAsync(long id)
        => GetAsync<MunicipalityAdminDto>($"/api/municipalities/{id}");

    public Task<ApiResult<List<MunicipalityDto>>> GetNearbyMunicipalitiesAsync(decimal lat, decimal lng, int limit = 15)
    {
        var latStr = lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lngStr = lng.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return GetAsync<List<MunicipalityDto>>($"/api/municipalities/nearby?lat={latStr}&lng={lngStr}&limit={limit}");
    }

    public Task<ApiResult<string>> CreateMunicipalityAsync(object body)
        => PostAsync("/api/municipalities", body);

    public Task<ApiResult<string>> UpdateMunicipalityAsync(long id, object body)
        => PutAsync($"/api/municipalities/{id}", body);

    // ══════════════════════════════════════════════
    // ADMIN - USERS
    // ══════════════════════════════════════════════

    public Task<ApiResult<List<UserListItemDto>>> GetUsersAsync()
        => GetAsync<List<UserListItemDto>>("/api/admin/users");

    public Task<ApiResult<List<RoleDto>>> GetRolesAsync()
        => GetAsync<List<RoleDto>>("/api/admin/roles");

    public Task<ApiResult<List<MunicipioUserDto>>> GetAvailableMunicipioUsersAsync()
        => GetAsync<List<MunicipioUserDto>>("/api/admin/users/available-municipio");

    public Task<ApiResult<string>> CreateUserAsync(object body)
        => PostAsync("/api/admin/users", body);

    public Task<ApiResult<string>> ToggleBlockUserAsync(long id, bool isBlocked)
        => PutAsync($"/api/admin/users/{id}/block", new { isBlocked });

    public Task<ApiResult<string>> ChangeUserRoleAsync(long id, long roleId)
        => PutAsync($"/api/admin/users/{id}/role", new { roleId });

    // ══════════════════════════════════════════════
    // ADMIN - INSIGHTS
    // ══════════════════════════════════════════════

    public Task<ApiResult<InsightsDto>> GetInsightsAsync()
        => GetAsync<InsightsDto>("/api/admin/insights");

    // ══════════════════════════════════════════════
    // AUTH
    // ══════════════════════════════════════════════

    public Task<ApiResult<string>> LoginAsync(string email, string password)
        => PostAsync("/api/auth/login", new { email, password });

    public Task<ApiResult<string>> RegisterAsync(object body)
        => PostAsync("/api/auth/register", body);

    // ══════════════════════════════════════════════
    // ERROR EXTRACTION (private)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Extracts a single error message from API response.
    /// Tries "error" property first, then falls back to status code message.
    /// </summary>
    private static async Task<string> ExtractError(HttpResponseMessage resp)
    {
        try
        {
            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("error", out var err))
                return err.GetString() ?? $"Error HTTP {(int)resp.StatusCode}";
        }
        catch { }

        return $"Error HTTP {(int)resp.StatusCode}";
    }

    /// <summary>
    /// Extracts error with support for both "error" (string) and "errors" (array) formats.
    /// Returns the ApiError for richer handling in controllers.
    /// </summary>
    private static async Task<string> ExtractErrorDetailed(HttpResponseMessage resp)
    {
        try
        {
            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("error", out var err))
                return err.GetString() ?? $"Error HTTP {(int)resp.StatusCode}";

            if (doc.RootElement.TryGetProperty("errors", out var errors))
            {
                var list = new List<string>();
                foreach (var e in errors.EnumerateArray())
                    list.Add(e.GetString() ?? "Error desconocido");
                return string.Join(" | ", list);
            }
        }
        catch { }

        return $"Error HTTP {(int)resp.StatusCode}";
    }
}

// ══════════════════════════════════════════════
// SUPPORT TYPES
// ══════════════════════════════════════════════

/// <summary>
/// Result wrapper for all API calls. 
/// Success: Data is populated, Error is null.
/// Failure: Error has the message, StatusCode indicates HTTP status.
/// </summary>
public class ApiResult<T>
{
    public T? Data { get; }
    public HttpStatusCode StatusCode { get; }
    public string? Error { get; }
    public bool Success => Error == null;
    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;
    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;

    public ApiResult(T? data, HttpStatusCode statusCode, string? error = null)
    {
        Data = data;
        StatusCode = statusCode;
        Error = error;
    }
}

/// <summary>
/// Fluent querystring builder. Skips null/empty values automatically.
/// </summary>
public class QueryBuilder
{
    private readonly List<string> _params = new();

    public QueryBuilder Add(string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _params.Add($"{key}={UrlEncoder.Default.Encode(value)}");
        return this;
    }

    public QueryBuilder Add(string key, int? value)
    {
        if (value.HasValue)
            _params.Add($"{key}={value.Value}");
        return this;
    }

    public QueryBuilder Add(string key, int value)
    {
        _params.Add($"{key}={value}");
        return this;
    }

    public QueryBuilder Add(string key, long? value)
    {
        if (value.HasValue)
            _params.Add($"{key}={value.Value}");
        return this;
    }

    public override string ToString()
        => _params.Count > 0 ? "?" + string.Join("&", _params) : "";
}
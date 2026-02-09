using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

namespace WebApplication1.Controllers;

[Authorize(Roles = "Aplicador")]
public class RecipeViewController : Controller
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [HttpGet]
    public IActionResult Upload()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Upload(
        IFormFile pdf,
        bool dryRun,
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        if (pdf == null || pdf.Length == 0)
        {
            ViewBag.Error = "Seleccioná un archivo PDF.";
            return View();
        }

        if (!pdf.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) &&
            !pdf.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            ViewBag.Error = "El archivo debe ser un PDF.";
            return View();
        }

        var client = httpClientFactory.CreateClient("AgroApi");

        using var content = new MultipartFormDataContent();

        // Archivo PDF
        var streamContent = new StreamContent(pdf.OpenReadStream());
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(pdf.ContentType);
        content.Add(streamContent, "Pdf", pdf.FileName);

        // DryRun flag
        content.Add(new StringContent(dryRun.ToString().ToLower()), "DryRun");

        var resp = await client.PostAsync("/api/recipes/import-pdf", content);

        if (resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);

            if (dryRun)
            {
                ViewBag.Success = "Vista previa generada correctamente. Revisá los datos y volvé a subir sin 'Solo vista previa' para confirmar.";
                ViewBag.Preview = body;
            }
            else
            {
                var recipeId = doc.RootElement.TryGetProperty("recipeId", out var rid) ? rid.GetInt64() : 0;
                var rfdNumber = doc.RootElement.TryGetProperty("rfdNumber", out var rfd) ? rfd.GetInt64() : 0;

                TempData["Success"] = $"Receta RFD #{rfdNumber} importada exitosamente (ID: {recipeId}).";
                return RedirectToAction("Upload");
            }
        }
        else
        {
            try
            {
                var body = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(body);

                if (doc.RootElement.TryGetProperty("error", out var errorProp))
                {
                    ViewBag.Error = errorProp.GetString();
                }
                else
                {
                    ViewBag.Error = $"Error al importar el PDF. (HTTP {(int)resp.StatusCode})";
                }
            }
            catch
            {
                ViewBag.Error = $"Error al importar el PDF. (HTTP {(int)resp.StatusCode})";
            }
        }

        return View();
    }
}

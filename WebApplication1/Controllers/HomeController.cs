using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AgroApiClient _api;

        public HomeController(ILogger<HomeController> logger, AgroApiClient api)
        {
            _logger = logger;
            _api = api;
        }

        public IActionResult Index()
        {
            if (User.IsInRole("Municipio"))
                return RedirectToAction("Dashboard", "Municipio");

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        /// <summary>
        /// Proxy para zonas de exclusión activas.
        /// Accesible por cualquier usuario autenticado.
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ExclusionZones()
        {
            try
            {
                var result = await _api.GetActiveExclusionZonesAsync();
                if (result.Success && !string.IsNullOrEmpty(result.Data))
                    return Content(result.Data, "application/json");
                return Json(Array.Empty<object>());
            }
            catch
            {
                return Json(Array.Empty<object>());
            }
        }

        /// <summary>
        /// Proxy para puntos sensibles activos.
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> SensitivePoints()
        {
            try
            {
                var result = await _api.GetActiveSensitivePointsAsync();
                if (result.Success && !string.IsNullOrEmpty(result.Data))
                    return Content(result.Data, "application/json");
                return Json(Array.Empty<object>());
            }
            catch
            {
                return Json(Array.Empty<object>());
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

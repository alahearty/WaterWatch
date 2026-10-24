using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WaterWatch.Controllers;

[Authorize]
public class ForecastingController : Controller
{
    public IActionResult Index()
    {
        ViewData["Active"] = "Forecasting";
        ViewData["Title"] = "Forecasting";
        return View();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WaterWatch.Controllers;

public class AnalyticsController : Controller
{
    public IActionResult Index()
    {
        ViewData["Active"] = "Analytics";
        ViewData["Title"] = "Analytics";
        return View();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WaterWatch.Controllers;

public class WaterQualityController : Controller
{
    public IActionResult Index()
    {
        ViewData["Active"] = "WaterQuality";
        ViewData["Title"] = "WaterQuality";
        return View();
    }

    public IActionResult Station(Guid id)
    {
        ViewData["Active"] = "WaterQuality";
        ViewData["Title"] = "Sampling station";
        ViewData["StationId"] = id;
        return View();
    }
}

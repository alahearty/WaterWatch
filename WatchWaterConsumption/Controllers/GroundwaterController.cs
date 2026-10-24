using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WaterWatch.Controllers;

public class GroundwaterController : Controller
{
    public IActionResult Index()
    {
        ViewData["Active"] = "Groundwater";
        ViewData["Title"] = "Groundwater";
        return View();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WaterWatch.Controllers;

public class HydrologyController : Controller
{
    public IActionResult Index()
    {
        ViewData["Active"] = "Hydrology";
        ViewData["Title"] = "Hydrology";
        return View();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WaterWatch.Controllers;

public class GisController : Controller
{
    public IActionResult Index()
    {
        ViewData["Active"] = "Gis";
        ViewData["Title"] = "Gis";
        ViewData["FullHeight"] = true;
        return View();
    }
}

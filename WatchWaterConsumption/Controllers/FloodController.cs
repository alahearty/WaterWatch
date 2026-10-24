using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WaterWatch.Controllers;

public class FloodController : Controller
{
    public IActionResult Index()
    {
        ViewData["Active"] = "Flood";
        ViewData["Title"] = "Flood";
        return View();
    }
}

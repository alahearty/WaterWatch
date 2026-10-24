using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WaterWatch.Controllers;

public class InfrastructureController : Controller
{
    public IActionResult Index()
    {
        ViewData["Active"] = "Infrastructure";
        ViewData["Title"] = "Infrastructure";
        return View();
    }

    public IActionResult Details(Guid id)
    {
        ViewData["Active"] = "Infrastructure";
        ViewData["Title"] = "Asset details";
        ViewData["AssetId"] = id;
        return View();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WaterWatch.Controllers;

public class ConsumptionController : Controller
{
    public IActionResult Index()
    {
        ViewData["Active"] = "Consumption";
        ViewData["Title"] = "Consumption";
        return View();
    }
}

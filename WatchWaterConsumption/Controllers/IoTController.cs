using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WaterWatch.Controllers;

[Authorize]
public class IoTController : Controller
{
    public IActionResult Index()
    {
        ViewData["Active"] = "IoT";
        ViewData["Title"] = "IoT";
        return View();
    }
}

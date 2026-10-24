using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WaterWatch.Controllers;

[Authorize]
public class ReportsController : Controller
{
    public IActionResult Index()
    {
        ViewData["Active"] = "Reports";
        ViewData["Title"] = "Reports";
        return View();
    }
}

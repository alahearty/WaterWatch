using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WaterWatch.Controllers;

public class DashboardController : Controller
{
    public IActionResult Index()
    {
        ViewData["Active"] = "Dashboard";
        ViewData["Title"] = "Dashboard";
        return View();
    }
}

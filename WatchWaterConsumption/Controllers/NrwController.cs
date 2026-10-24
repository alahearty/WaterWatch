using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WaterWatch.Controllers;

public class NrwController : Controller
{
    public IActionResult Index()
    {
        ViewData["Active"] = "Nrw";
        ViewData["Title"] = "Nrw";
        return View();
    }
}

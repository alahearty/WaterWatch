using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WaterWatch.Controllers;

public class RemoteSensingController : Controller
{
    public IActionResult Index()
    {
        ViewData["Active"] = "RemoteSensing";
        ViewData["Title"] = "RemoteSensing";
        return View();
    }
}

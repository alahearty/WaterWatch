using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WaterWatch.Controllers;

[Authorize(Policy = WaterWatch.Security.Policies.CanAdminister)]
public class AdministrationController : Controller
{
    public IActionResult Index()
    {
        ViewData["Active"] = "Administration";
        ViewData["Title"] = "Administration";
        return View();
    }
}

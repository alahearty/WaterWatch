using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WaterWatch.Controllers;

[Authorize]
public class FieldOperationsController : Controller
{
    public IActionResult Index()
    {
        ViewData["Active"] = "FieldOperations";
        ViewData["Title"] = "FieldOperations";
        return View();
    }
}

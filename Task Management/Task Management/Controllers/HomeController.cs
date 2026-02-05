using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Task_Management.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet("/")]
        public IActionResult CurrentTasks()
        {
            return View();
        }

        public IActionResult Archive()
        {
            return View();
        }
    }
         
}

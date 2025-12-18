using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace KlodTattooWeb.Controllers
{
    public class RobotsController : Controller
    {
        [Route("robots.txt")]
        public IActionResult Index()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var sb = new StringBuilder();

            sb.AppendLine("User-agent: *");
            sb.AppendLine("Allow: /");
            sb.AppendLine($"Sitemap: {baseUrl}/sitemap.xml");

            return Content(sb.ToString(), "text/plain", Encoding.UTF8);
        }
    }
}

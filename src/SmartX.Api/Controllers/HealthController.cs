using Microsoft.AspNetCore.Mvc;

namespace SmartX.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "Healthy",
            service = "SmartX.Api",
            timestamp = DateTime.UtcNow
        });
    }
}
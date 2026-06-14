using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ScheduleBot.Services;

namespace ScheduleBot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BotController(DatabaseService db, ILogger<BotController> logger) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            Status = "Online",
            Uptime = DateTime.UtcNow,
            Mode = "Polling"
        });
    }
}
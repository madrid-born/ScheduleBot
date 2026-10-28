using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ScheduleBot.Models;
using ScheduleBot.Services;

namespace ScheduleBot.Controllers;

[ApiController]
[Route("downloads")]
public class FileDownloadController(IOptions<FileLinkOptions> options, IServiceProvider serviceProvider) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task Download(string token)
    {
        if (!options.Value.Enabled)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await serviceProvider.GetRequiredService<FileDownloadService>().HandleAsync(HttpContext, token);
    }
}

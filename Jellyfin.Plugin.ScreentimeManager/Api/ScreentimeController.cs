using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ScreentimeManager.Api;

/// <summary>
/// Screentime api controller.
/// </summary>
[ApiController]
[Route("ScreentimeManager")]
[Produces("application/json")]
public class ScreentimeController : ControllerBase
{
    /// <summary>
    /// Gets the current usage for a specified user.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <returns>A dictionary of usage by library id.</returns>
    [HttpGet("Usage/{userId}")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<Dictionary<string, double>> GetUserUsage([FromRoute] string userId)
    {
        var usage = ScreentimeTracker.Instance?.GetUserUsage(userId) ?? new Dictionary<string, double>();
        return Ok(usage);
    }
}

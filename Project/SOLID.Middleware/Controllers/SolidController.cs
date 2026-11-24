/*
 * Projeto: SOLID - Service-Oriented Layer for Interoperable Data
 * Ficheiro: SolidController.cs
 * Autor: Gabriel
 * Descrição: Controlador RESTful com operações CRUD e Discovery para os recursos
 *            'application' e 'channel', com parâmetros expostos no Swagger.
 */

using Microsoft.AspNetCore.Mvc;
using SOLID.Middleware.Data;
using SOLID.Middleware.Models;

namespace SOLID.Middleware.Controllers;

[ApiController]
public class SolidController : ControllerBase
{
    private static readonly SolidDatabase _db = new();
    private static readonly HashSet<string> ValidResourceTypes = new()
    {
        "application", "channel", "data-record", "subscription"
    };

    private bool TryGetDiscoveryType(string? headerValue, out string? discoveryType, out IActionResult? errorResult)
    {
        discoveryType = null;
        errorResult = null;

        if (string.IsNullOrWhiteSpace(headerValue))
            return false;

        string value = headerValue.Trim();
        if (!ValidResourceTypes.Contains(value))
        {
            errorResult = BadRequest(new
            {
                error = $"Invalid solid-discovery value '{value}'. Allowed: application, channel, data-record, subscription."
            });
            return true;
        }

        discoveryType = value;
        return true;
    }

    // =========================================================================
    // RAIZ: /api/solid
    // =========================================================================

    [HttpGet("/api/solid")]
    public IActionResult DiscoverAtRoot([FromHeader(Name = "solid-discovery")] string? solidDiscovery = null)
    {
        if (!TryGetDiscoveryType(solidDiscovery, out var discoveryType, out var errorResult))
        {
            return BadRequest(new
            {
                error = "Generic 'Get all' is not supported. Include the 'solid-discovery' HTTP header."
            });
        }

        if (errorResult != null)
            return errorResult;

        return Ok(_db.DiscoverFromRoot(discoveryType!));
    }

    [HttpPost("/api/solid")]
    public IActionResult CreateApplication([FromBody] SolidApplication? app)
    {
        if (app == null || app.ResType != "application")
        {
            return BadRequest(new
            {
                error = "The 'res-type' property is mandatory and must be 'application'."
            });
        }

        var created = _db.CreateApplication(app);
        return Created($"/api/solid/{created.ResourceName}", created);
    }

    // =========================================================================
    // NÍVEL 1: /api/solid/{appName}  (Application)
    // =========================================================================

    [HttpGet("/api/solid/{appName}")]
    public IActionResult GetOrDiscoverApplication(
        string appName,
        [FromHeader(Name = "solid-discovery")] string? solidDiscovery = null)
    {
        var app = _db.GetApplication(appName);
        if (app == null)
            return NotFound(new { error = $"Application '{appName}' not found." });

        if (TryGetDiscoveryType(solidDiscovery, out var discoveryType, out var errorResult))
        {
            if (errorResult != null)
                return errorResult;

            return Ok(_db.DiscoverFromApplication(appName, discoveryType!));
        }

        return Ok(app);
    }

    [HttpPut("/api/solid/{appName}")]
    public IActionResult UpdateApplication(string appName, [FromBody] SolidApplication? app)
    {
        if (app == null || app.ResType != "application")
        {
            return BadRequest(new
            {
                error = "The 'res-type' property is mandatory and must be 'application'."
            });
        }

        var updated = _db.UpdateApplication(appName, app);
        if (updated == null)
            return NotFound(new { error = $"Application '{appName}' not found." });

        return Ok(updated);
    }

    [HttpDelete("/api/solid/{appName}")]
    public IActionResult DeleteApplication(string appName)
    {
        if (!_db.DeleteApplication(appName))
            return NotFound(new { error = $"Application '{appName}' not found." });

        return NoContent();
    }

    // =========================================================================
    // NÍVEL 2: /api/solid/{appName}/{channelName}  (Channel)
    // =========================================================================

    [HttpPost("/api/solid/{appName}")]
    public IActionResult CreateChannel(string appName, [FromBody] SolidChannel? channel)
    {
        if (channel == null || channel.ResType != "channel")
        {
            return BadRequest(new
            {
                error = "The 'res-type' property is mandatory and must be 'channel'."
            });
        }

        if (_db.GetApplication(appName) == null)
            return NotFound(new { error = $"Parent application '{appName}' not found." });

        var created = _db.CreateChannel(appName, channel);
        return Created($"/api/solid/{appName}/{created!.ResourceName}", created);
    }

    [HttpGet("/api/solid/{appName}/{channelName}")]
    public IActionResult GetOrDiscoverChannel(
        string appName,
        string channelName,
        [FromHeader(Name = "solid-discovery")] string? solidDiscovery = null)
    {
        var channel = _db.GetChannel(appName, channelName);
        if (channel == null)
            return NotFound(new { error = $"Channel '{channelName}' not found in application '{appName}'." });

        if (TryGetDiscoveryType(solidDiscovery, out var discoveryType, out var errorResult))
        {
            if (errorResult != null)
                return errorResult;

            return Ok(_db.DiscoverFromChannel(appName, channelName, discoveryType!));
        }

        return Ok(channel);
    }

    [HttpPut("/api/solid/{appName}/{channelName}")]
    public IActionResult UpdateChannel(string appName, string channelName, [FromBody] SolidChannel? channel)
    {
        if (channel == null || channel.ResType != "channel")
        {
            return BadRequest(new
            {
                error = "The 'res-type' property is mandatory and must be 'channel'."
            });
        }

        var updated = _db.UpdateChannel(appName, channelName, channel);
        if (updated == null)
            return NotFound(new { error = $"Channel '{channelName}' not found in application '{appName}'." });

        return Ok(updated);
    }

    [HttpDelete("/api/solid/{appName}/{channelName}")]
    public IActionResult DeleteChannel(string appName, string channelName)
    {
        if (!_db.DeleteChannel(appName, channelName))
            return NotFound(new { error = $"Channel '{channelName}' not found in application '{appName}'." });

        return NoContent();
    }
}
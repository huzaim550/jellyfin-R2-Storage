using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CloudBucket.Configuration;
using Jellyfin.Plugin.CloudBucket.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CloudBucket.Controllers;

/// <summary>
/// REST endpoints used by the dashboard config page and by the edge redirect (Caddy/Worker).
/// </summary>
[ApiController]
[Route("CloudBucket")]
public sealed class CloudBucketController : ControllerBase
{
    private readonly StrmSyncService _syncService;
    private readonly CloudLibraryService _libraryService;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudBucketController"/> class.
    /// </summary>
    public CloudBucketController(
        StrmSyncService syncService,
        CloudLibraryService libraryService,
        ILibraryManager libraryManager)
    {
        _syncService = syncService;
        _libraryService = libraryService;
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Gets the plugin configuration.
    /// </summary>
    [HttpGet("Config")]
    [Authorize(Policy = "RequiresElevation")]
    public ActionResult<PluginConfiguration> GetConfig()
    {
        return Plugin.Instance?.Configuration ?? new PluginConfiguration();
    }

    /// <summary>
    /// Saves the plugin configuration.
    /// </summary>
    [HttpPost("Config")]
    [Authorize(Policy = "RequiresElevation")]
    public ActionResult SaveConfig([FromBody] PluginConfiguration config)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return StatusCode(503);
        }

        plugin.UpdateConfiguration(config);
        return NoContent();
    }

    /// <summary>
    /// Tests connectivity to the bucket using the saved configuration.
    /// </summary>
    [HttpPost("TestConnection")]
    [Authorize(Policy = "RequiresElevation")]
    public async Task<ActionResult> TestConnection(CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return StatusCode(503);
        }

        var settings = CloudBucketSettings.From(plugin.Configuration);

        try
        {
            var count = await _syncService.TestConnectionAsync(settings, cancellationToken).ConfigureAwait(false);
            return Ok(new { ok = true, message = $"Connected. Sample objects returned: {count}" });
        }
        catch (Exception ex)
        {
            return Ok(new { ok = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Runs a sync immediately using the saved configuration.
    /// </summary>
    [HttpPost("Sync")]
    [Authorize(Policy = "RequiresElevation")]
    public async Task<ActionResult> Sync(CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return StatusCode(503);
        }

        var settings = CloudBucketSettings.From(plugin.Configuration);

        try
        {
            var result = await _syncService.SyncAsync(settings, null, cancellationToken).ConfigureAwait(false);

            string? libraryMessage = null;
            if (settings.AutoCreateLibrary)
            {
                libraryMessage = await _libraryService.EnsureLibraryAsync(settings).ConfigureAwait(false);
            }

            if (settings.TriggerScanAfterSync)
            {
                _libraryManager.QueueLibraryScan();
            }

            return Ok(new
            {
                ok = true,
                result.Listed,
                result.Written,
                result.Unchanged,
                result.Deleted,
                Library = libraryMessage
            });
        }
        catch (Exception ex)
        {
            return Ok(new { ok = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Creates or updates the Jellyfin library for the configured .strm folder.
    /// </summary>
    [HttpPost("CreateLibrary")]
    [Authorize(Policy = "RequiresElevation")]
    public async Task<ActionResult> CreateLibrary()
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return StatusCode(503);
        }

        var settings = CloudBucketSettings.From(plugin.Configuration);

        try
        {
            var message = await _libraryService.EnsureLibraryAsync(settings).ConfigureAwait(false);
            return Ok(new { ok = true, message });
        }
        catch (Exception ex)
        {
            return Ok(new { ok = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Resolves a Jellyfin item id to the public URL of its backing object. Called by the edge
    /// redirect proxy (Caddy forward_auth / Cloudflare Worker) with the shared secret header.
    /// </summary>
    [HttpGet("Resolve/{itemId}")]
    [AllowAnonymous]
    public ActionResult Resolve([FromRoute] Guid itemId)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return NotFound();
        }

        var settings = CloudBucketSettings.From(plugin.Configuration);

        if (string.IsNullOrEmpty(settings.SharedSecret))
        {
            return StatusCode(403, "Resolver is not configured (no shared secret).");
        }

        var provided = Request.Headers["X-CloudBucket-Secret"].ToString();
        if (!FixedTimeEquals(provided, settings.SharedSecret))
        {
            return Unauthorized();
        }

        // Always return 200 so an unmatched item falls through to Jellyfin (the proxy only
        // redirects when the X-R2-Url header is present). Unauthorized/not-configured still
        // fail closed above.
        var item = _libraryManager.GetItemById(itemId);
        var url = item is null ? null : GetObjectUrl(item.Path);
        if (!string.IsNullOrWhiteSpace(url))
        {
            Response.Headers["X-R2-Url"] = url;
        }

        return Ok();
    }

    private static string? GetObjectUrl(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(path))
        {
            return System.IO.File.ReadAllText(path).Trim();
        }

        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return null;
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var left = Encoding.UTF8.GetBytes(a);
        var right = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}

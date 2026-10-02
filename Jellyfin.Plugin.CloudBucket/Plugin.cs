using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Jellyfin.Plugin.CloudBucket.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.CloudBucket;

/// <summary>
/// The Cloud Bucket plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// The stable plugin id. Do not change this after publishing.
    /// </summary>
    public static readonly Guid PluginId = Guid.Parse("7f3c9d2a-5b61-4e8f-a0c4-9d2b6e1f8a73");

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        EnsureSharedSecret();
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Cloud Bucket";

    /// <inheritdoc />
    public override Guid Id => PluginId;

    /// <inheritdoc />
    public override string Description =>
        "Mirrors an S3-compatible bucket (Cloudflare R2, Backblaze B2, ...) into a separate Jellyfin " +
        "library using .strm files and, optionally, redirects playback straight to the bucket so remote " +
        "users direct-play from the network edge.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
            }
        };
    }

    /// <summary>
    /// Generates a shared secret on first run so the redirect resolver works out of the box.
    /// </summary>
    private void EnsureSharedSecret()
    {
        if (!string.IsNullOrWhiteSpace(Configuration.SharedSecret))
        {
            return;
        }

        Configuration.SharedSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        UpdateConfiguration(Configuration);
    }
}

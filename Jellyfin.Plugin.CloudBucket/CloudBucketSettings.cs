using System;
using System.Linq;
using Jellyfin.Plugin.CloudBucket.Configuration;

namespace Jellyfin.Plugin.CloudBucket;

/// <summary>
/// The effective settings for a sync/resolve operation, derived from the plugin configuration.
/// </summary>
public sealed class CloudBucketSettings
{
    public string Endpoint { get; init; } = string.Empty;

    public string BucketName { get; init; } = string.Empty;

    public string AccessKeyId { get; init; } = string.Empty;

    public string SecretAccessKey { get; init; } = string.Empty;

    public string Prefix { get; init; } = string.Empty;

    public string PublicBaseUrl { get; init; } = string.Empty;

    public string StrmRootPath { get; init; } = string.Empty;

    public string LibraryName { get; init; } = string.Empty;

    public string LibraryType { get; init; } = "movies";

    public bool AutoCreateLibrary { get; init; }

    public string VideoExtensions { get; init; } = string.Empty;

    public bool DeleteOrphans { get; init; }

    public bool TriggerScanAfterSync { get; init; }

    public string SharedSecret { get; init; } = string.Empty;

    public int MaxObjects { get; init; }

    /// <summary>
    /// Gets the normalized list of video extensions (each starting with a dot, lower case).
    /// </summary>
    public string[] VideoExtensionList => VideoExtensions
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(e => e.StartsWith('.') ? e : "." + e)
        .Select(e => e.ToLowerInvariant())
        .ToArray();

    /// <summary>
    /// Builds settings from the persisted configuration.
    /// </summary>
    public static CloudBucketSettings From(PluginConfiguration config)
    {
        return new CloudBucketSettings
        {
            Endpoint = config.Endpoint.Trim(),
            BucketName = config.BucketName.Trim(),
            AccessKeyId = config.AccessKeyId.Trim(),
            SecretAccessKey = config.SecretAccessKey.Trim(),
            Prefix = config.Prefix.Trim(),
            PublicBaseUrl = config.PublicBaseUrl.Trim(),
            StrmRootPath = config.StrmRootPath.Trim(),
            LibraryName = string.IsNullOrWhiteSpace(config.LibraryName) ? "Cloud" : config.LibraryName.Trim(),
            LibraryType = string.IsNullOrWhiteSpace(config.LibraryType) ? "movies" : config.LibraryType.Trim(),
            AutoCreateLibrary = config.AutoCreateLibrary,
            VideoExtensions = config.VideoExtensions,
            DeleteOrphans = config.DeleteOrphans,
            TriggerScanAfterSync = config.TriggerScanAfterSync,
            SharedSecret = config.SharedSecret,
            MaxObjects = config.MaxObjects
        };
    }
}

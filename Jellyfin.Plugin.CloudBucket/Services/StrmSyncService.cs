using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CloudBucket.Services;

/// <summary>
/// Mirrors an S3-compatible bucket into .strm files.
/// </summary>
public sealed class StrmSyncService
{
    private readonly ILogger<StrmSyncService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StrmSyncService"/> class.
    /// </summary>
    public StrmSyncService(ILogger<StrmSyncService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Performs a full sync of the bucket into the configured .strm root.
    /// </summary>
    public async Task<SyncResult> SyncAsync(CloudBucketSettings settings, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        Validate(settings);

        Directory.CreateDirectory(settings.StrmRootPath);

        using var s3 = CreateClient(settings);

        var keys = await ListAllKeysAsync(s3, settings, progress, cancellationToken).ConfigureAwait(false);

        var extensions = new HashSet<string>(settings.VideoExtensionList, StringComparer.OrdinalIgnoreCase);
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var written = 0;
        var skipped = 0;

        for (var i = 0; i < keys.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = keys[i];
            var extension = Path.GetExtension(key);
            if (string.IsNullOrEmpty(extension) || !extensions.Contains(extension))
            {
                continue;
            }

            var relative = GetRelativeKey(key, settings.Prefix);
            if (string.IsNullOrEmpty(relative))
            {
                continue;
            }

            var strmRelative = relative[..^extension.Length] + ".strm";
            var strmPath = CombineSafe(settings.StrmRootPath, strmRelative);
            if (strmPath is null)
            {
                _logger.LogWarning("Skipping unsafe object key {Key}", key);
                continue;
            }

            expected.Add(Path.GetFullPath(strmPath));

            var url = BuildUrl(settings.PublicBaseUrl, key);
            var directory = Path.GetDirectoryName(strmPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var existing = File.Exists(strmPath)
                ? await File.ReadAllTextAsync(strmPath, cancellationToken).ConfigureAwait(false)
                : null;

            if (string.Equals(existing?.Trim(), url, StringComparison.Ordinal))
            {
                skipped++;
            }
            else
            {
                await File.WriteAllTextAsync(strmPath, url, cancellationToken).ConfigureAwait(false);
                written++;
            }

            progress?.Report(keys.Count == 0 ? 0 : ((i + 1) * 100.0) / keys.Count);
        }

        var deleted = settings.DeleteOrphans
            ? DeleteOrphans(settings.StrmRootPath, expected)
            : 0;

        progress?.Report(100);
        _logger.LogInformation(
            "Cloud Bucket sync finished: {Listed} objects listed, {Written} .strm written, {Skipped} unchanged, {Deleted} orphaned removed",
            keys.Count,
            written,
            skipped,
            deleted);

        return new SyncResult(keys.Count, written, skipped, deleted);
    }

    /// <summary>
    /// Verifies the credentials/bucket by listing a single object.
    /// </summary>
    public async Task<int> TestConnectionAsync(CloudBucketSettings settings, CancellationToken cancellationToken)
    {
        Validate(settings);

        using var s3 = CreateClient(settings);

        var response = await s3.ListObjectsV2Async(
            new ListObjectsV2Request
            {
                BucketName = settings.BucketName,
                Prefix = string.IsNullOrEmpty(settings.Prefix) ? null : settings.Prefix,
                MaxKeys = 1
            },
            cancellationToken).ConfigureAwait(false);

        return response.S3Objects?.Count ?? 0;
    }

    private static async Task<List<string>> ListAllKeysAsync(
        IAmazonS3 s3,
        CloudBucketSettings settings,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var keys = new List<string>();
        string? continuationToken = null;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await s3.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = settings.BucketName,
                    Prefix = string.IsNullOrEmpty(settings.Prefix) ? null : settings.Prefix,
                    ContinuationToken = continuationToken
                },
                cancellationToken).ConfigureAwait(false);

            if (response.S3Objects is not null)
            {
                keys.AddRange(response.S3Objects.Select(o => o.Key));
            }

            if (settings.MaxObjects > 0 && keys.Count >= settings.MaxObjects)
            {
                break;
            }

            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (continuationToken is not null);

        if (settings.MaxObjects > 0 && keys.Count > settings.MaxObjects)
        {
            keys = keys.Take(settings.MaxObjects).ToList();
        }

        progress?.Report(0);
        return keys;
    }

    private static int DeleteOrphans(string root, HashSet<string> expected)
    {
        var deleted = 0;

        foreach (var file in Directory.EnumerateFiles(root, "*.strm", SearchOption.AllDirectories))
        {
            if (expected.Contains(Path.GetFullPath(file)))
            {
                continue;
            }

            try
            {
                File.Delete(file);
                deleted++;
            }
            catch (IOException)
            {
                // Best effort; the next sync will retry.
            }
        }

        return deleted;
    }

    private static AmazonS3Client CreateClient(CloudBucketSettings settings)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = settings.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "auto"
        };

        return new AmazonS3Client(settings.AccessKeyId, settings.SecretAccessKey, config);
    }

    private static string GetRelativeKey(string key, string prefix)
    {
        var normalizedPrefix = prefix.Trim().Trim('/');

        if (string.IsNullOrEmpty(normalizedPrefix))
        {
            return key.TrimStart('/');
        }

        var withSlash = normalizedPrefix + "/";
        return key.StartsWith(withSlash, StringComparison.Ordinal)
            ? key[withSlash.Length..]
            : key.TrimStart('/');
    }

    private static string BuildUrl(string publicBaseUrl, string key)
    {
        var encoded = string.Join("/", key.Split('/').Select(Uri.EscapeDataString));
        return publicBaseUrl.TrimEnd('/') + "/" + encoded;
    }

    private static string? CombineSafe(string root, string relative)
    {
        var rootFull = Path.GetFullPath(root);
        var relativePath = relative.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var combined = Path.GetFullPath(Path.Combine(rootFull, relativePath));

        if (!combined.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }

        return combined;
    }

    private static void Validate(CloudBucketSettings settings)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(settings.Endpoint))
        {
            missing.Add("Endpoint");
        }

        if (string.IsNullOrWhiteSpace(settings.BucketName))
        {
            missing.Add("BucketName");
        }

        if (string.IsNullOrWhiteSpace(settings.AccessKeyId) || string.IsNullOrWhiteSpace(settings.SecretAccessKey))
        {
            missing.Add("AccessKeyId/SecretAccessKey");
        }

        if (string.IsNullOrWhiteSpace(settings.PublicBaseUrl))
        {
            missing.Add("PublicBaseUrl");
        }

        if (string.IsNullOrWhiteSpace(settings.StrmRootPath))
        {
            missing.Add("StrmRootPath");
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException("Cloud Bucket is not configured. Missing: " + string.Join(", ", missing));
        }
    }
}

/// <summary>
/// The result of a sync operation.
/// </summary>
public sealed record SyncResult(int Listed, int Written, int Unchanged, int Deleted);

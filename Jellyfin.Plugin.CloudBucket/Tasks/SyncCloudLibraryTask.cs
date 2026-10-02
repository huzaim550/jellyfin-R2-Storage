using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CloudBucket.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.CloudBucket.Tasks;

/// <summary>
/// Scheduled task that keeps the cloud library in sync with the bucket.
/// </summary>
public sealed class SyncCloudLibraryTask : IScheduledTask
{
    private readonly StrmSyncService _syncService;
    private readonly CloudLibraryService _libraryService;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncCloudLibraryTask"/> class.
    /// </summary>
    public SyncCloudLibraryTask(
        StrmSyncService syncService,
        CloudLibraryService libraryService,
        ILibraryManager libraryManager)
    {
        _syncService = syncService;
        _libraryService = libraryService;
        _libraryManager = libraryManager;
    }

    /// <inheritdoc />
    public string Name => "Sync Cloud Bucket library";

    /// <inheritdoc />
    public string Key => "CloudBucketSyncLibrary";

    /// <inheritdoc />
    public string Description =>
        "Lists the configured bucket and creates/updates .strm files for the cloud library. " +
        "Nothing is uploaded and local media is never touched.";

    /// <inheritdoc />
    public string Category => "Cloud Bucket";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return;
        }

        var settings = CloudBucketSettings.From(plugin.Configuration);

        await _syncService.SyncAsync(settings, progress, cancellationToken).ConfigureAwait(false);

        if (settings.AutoCreateLibrary)
        {
            await _libraryService.EnsureLibraryAsync(settings).ConfigureAwait(false);
        }

        if (settings.TriggerScanAfterSync)
        {
            _libraryManager.QueueLibraryScan();
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromHours(12).Ticks
            }
        };
    }
}

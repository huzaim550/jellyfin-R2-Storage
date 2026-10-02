using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CloudBucket.Services;

/// <summary>
/// Creates or updates the Jellyfin library that points at the .strm output folder.
/// </summary>
public sealed class CloudLibraryService
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<CloudLibraryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudLibraryService"/> class.
    /// </summary>
    public CloudLibraryService(ILibraryManager libraryManager, ILogger<CloudLibraryService> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <summary>
    /// Ensures a virtual folder with the configured name exists and contains the .strm path.
    /// </summary>
    /// <returns>A human readable status message.</returns>
    public async Task<string> EnsureLibraryAsync(CloudBucketSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.StrmRootPath))
        {
            throw new InvalidOperationException(".strm output path is not configured.");
        }

        // Make sure the path exists before Jellyfin is pointed at it.
        Directory.CreateDirectory(settings.StrmRootPath);

        var name = settings.LibraryName;

        var existing = _libraryManager.GetVirtualFolders()
            .FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            var options = new LibraryOptions
            {
                PathInfos = new[] { new MediaPathInfo(settings.StrmRootPath) }
            };

            await _libraryManager
                .AddVirtualFolder(name, ParseCollectionType(settings.LibraryType), options, true)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Cloud Bucket created library {Name} ({Type}) at {Path}.",
                name,
                settings.LibraryType,
                settings.StrmRootPath);

            return $"Created library '{name}' ({settings.LibraryType}).";
        }

        if (existing.Locations is null
            || !existing.Locations.Any(l => string.Equals(l, settings.StrmRootPath, StringComparison.OrdinalIgnoreCase)))
        {
            _libraryManager.AddMediaPath(name, new MediaPathInfo(settings.StrmRootPath));
            _libraryManager.QueueLibraryScan();
            _logger.LogInformation(
                "Cloud Bucket added media path {Path} to existing library {Name}.",
                settings.StrmRootPath,
                name);
            return $"Added '{settings.StrmRootPath}' to existing library '{name}'.";
        }

        return $"Library '{name}' is already configured.";
    }

    private static CollectionTypeOptions ParseCollectionType(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "tvshows" or "shows" or "tv" => CollectionTypeOptions.tvshows,
            "mixed" => CollectionTypeOptions.mixed,
            "music" => CollectionTypeOptions.music,
            "musicvideos" => CollectionTypeOptions.musicvideos,
            "homevideos" => CollectionTypeOptions.homevideos,
            "books" => CollectionTypeOptions.books,
            _ => CollectionTypeOptions.movies
        };
    }
}

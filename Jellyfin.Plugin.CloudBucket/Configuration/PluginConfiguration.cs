using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.CloudBucket.Configuration;

/// <summary>
/// Persisted plugin configuration, edited from the Jellyfin dashboard.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the S3-compatible endpoint, e.g. https://&lt;accountid&gt;.r2.cloudflarestorage.com.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets the bucket name.</summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>Gets or sets the access key id.</summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>Gets or sets the secret access key.</summary>
    public string SecretAccessKey { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional key prefix to restrict syncing to a folder inside the bucket.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>Gets or sets the public base URL used to build the .strm links, e.g. https://media.example.com.</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the directory (inside the Jellyfin container) where .strm files are written.</summary>
    public string StrmRootPath { get; set; } = "/data/cloud-strm";

    /// <summary>Gets or sets the name of the Jellyfin library that will hold these files.</summary>
    public string LibraryName { get; set; } = "Cloud";

    /// <summary>Gets or sets the library content type: movies, tvshows, mixed, music, homevideos or books.</summary>
    public string LibraryType { get; set; } = "movies";

    /// <summary>Gets or sets a value indicating whether the library is created/updated automatically after a sync.</summary>
    public bool AutoCreateLibrary { get; set; } = true;

    /// <summary>Gets or sets the comma separated list of video extensions to mirror.</summary>
    public string VideoExtensions { get; set; } = ".mp4,.mkv,.m4v,.mov,.avi,.webm,.ts,.mpg,.mpeg,.wmv,.flv";

    /// <summary>Gets or sets a value indicating whether .strm files whose object no longer exists are deleted.</summary>
    public bool DeleteOrphans { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether a library scan is queued after a sync.</summary>
    public bool TriggerScanAfterSync { get; set; } = true;

    /// <summary>Gets or sets the shared secret required by the /CloudBucket/Resolve endpoint. Generated automatically.</summary>
    public string SharedSecret { get; set; } = string.Empty;

    /// <summary>Gets or sets a limit on the number of objects listed. 0 means unlimited.</summary>
    public int MaxObjects { get; set; }
}

using Jellyfin.Plugin.CloudBucket.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.CloudBucket;

/// <summary>
/// Registers the plugin's services with Jellyfin's DI container.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<StrmSyncService>();
        serviceCollection.AddSingleton<CloudLibraryService>();
    }
}

// Core.Abstractions/Plugins/IPlugin.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace Core.Abstractions.Plugins
{
    public interface IPlugin
    {
        string Name { get; }
        string Version { get; }
        string Description { get; }

        void ConfigureServices(IServiceCollection services);
        void Configure(IApplicationBuilder app);
    }
}

// Core.Abstractions/Plugins/IPluginManager.cs

namespace Core.Abstractions.Plugins
{
    public interface IPluginManager
    {
        IEnumerable<IPlugin> LoadedPlugins { get; }
        IPlugin GetPlugin(string name);
        void LoadPlugin(IPlugin plugin);
        void UnloadPlugin(string name);
        bool IsPluginLoaded(string name);
    }
}

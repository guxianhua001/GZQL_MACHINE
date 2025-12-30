using Core.Abstractions.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Services
{
    public class PluginManager : IPluginManager
    {
        private readonly Dictionary<string, IPlugin> _plugins = new Dictionary<string, IPlugin>(StringComparer.OrdinalIgnoreCase);
        private readonly IServiceProvider _serviceProvider;

        public PluginManager(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IEnumerable<IPlugin> LoadedPlugins => _plugins.Values;

        public IPlugin GetPlugin(string name)
        {
            return _plugins.TryGetValue(name, out var plugin) ? plugin : null;
        }

        public void LoadPlugin(IPlugin plugin)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));

            if (_plugins.ContainsKey(plugin.Name))
                throw new InvalidOperationException($"插件 '{plugin.Name}' 已加载");

            _plugins[plugin.Name] = plugin;

            // 自动配置插件的服务
            ConfigurePluginServices(plugin);
        }

        public void UnloadPlugin(string name)
        {
            if (_plugins.ContainsKey(name))
            {
                _plugins.Remove(name);
            }
        }

        public bool IsPluginLoaded(string name)
        {
            return _plugins.ContainsKey(name);
        }

        private void ConfigurePluginServices(IPlugin plugin)
        {
            try
            {
                // 创建服务集合并配置插件服务
                var services = new ServiceCollection();
                plugin.ConfigureServices(services);

                // 这里可以添加将服务注册到主容器的逻辑
                // 在实际应用中，可能需要将服务注册到主 DI 容器

                Console.WriteLine($"插件 '{plugin.Name}' 服务配置完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"配置插件 '{plugin.Name}' 服务失败: {ex.Message}");
                throw;
            }
        }

        public void ConfigureAllPlugins(IApplicationBuilder app)
        {
            foreach (var plugin in _plugins.Values)
            {
                try
                {
                    plugin.Configure(app);
                    Console.WriteLine($"插件 '{plugin.Name}' 配置完成");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"配置插件 '{plugin.Name}' 失败: {ex.Message}");
                }
            }
        }
    }
}

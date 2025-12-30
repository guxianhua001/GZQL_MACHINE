using Core.Abstractions.Plugins;
using Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Recipe.Extensions;
using Recipe.Interfaces;
using Recipe.Services;

namespace Recipe.Plugin
{
    public class RecipeManagementPlugin : IPlugin
    {
        public string Name => "RecipeManagement";
        public string Version => "1.0.0";
        public string Description => "Advanced recipe management system with extensible parameter support";

        public void ConfigureServices(IServiceCollection services)
        {
            // 注册存储服务
            services.AddScoped<Core.Abstractions.Storages.IGenericStorage, Core.Services.JsonRecipeFileStorage>();

            services.AddScoped<IRecipeStorage, RecipeStorage>();

            // 注册领域服务
            services.AddRecipeManagementDomain();

            // 注册应用服务
            services.AddRecipeManagementApplication();

            // 注册基础设施
            services.AddRecipeManagementInfrastructure();

            // 注册后台服务
            services.AddHostedService<RecipeBackgroundService>();

            Console.WriteLine($"RecipeManagement plugin services configured.");
        }

        public void Configure(IApplicationBuilder app)
        {
            // 配置中间件、路由等
            Console.WriteLine($"RecipeManagement plugin configured.");
        }
    }
}

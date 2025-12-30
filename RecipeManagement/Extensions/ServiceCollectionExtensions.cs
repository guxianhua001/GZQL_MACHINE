// RecipeManagement/Extensions/ServiceCollectionExtensions.cs
using Microsoft.Extensions.DependencyInjection;
using Recipe.Services;
using Recipe.Interfaces;

namespace Recipe.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRecipeManagementDomain(this IServiceCollection services)
        {
            // 注册领域服务
            return services;
        }

        public static IServiceCollection AddRecipeManagementApplication(this IServiceCollection services)
        {
            services.AddScoped<IRecipeManager, RecipeManager>();
            return services;
        }

        public static IServiceCollection AddRecipeManagementInfrastructure(this IServiceCollection services)
        {
            services.AddScoped<IRecipeStorage, RecipeStorage>();
            return services;
        }
    }
}
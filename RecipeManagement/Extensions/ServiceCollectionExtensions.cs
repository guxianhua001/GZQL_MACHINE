using Microsoft.Extensions.DependencyInjection;
using Recipe.Services;
using Recipe.Interfaces;

namespace Recipe.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRecipeManagementDomain(this IServiceCollection services)
        {
            return services;
        }

        public static IServiceCollection AddRecipeManagementApplication(this IServiceCollection services)
        {
            return services;
        }

        public static IServiceCollection AddRecipeManagementInfrastructure(this IServiceCollection services)
        {
            services.AddScoped<IRecipeStorage, RecipeStorage>();
            return services;
        }
    }
}

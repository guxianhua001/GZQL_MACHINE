// Services/RecipeBackgroundService.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Recipe.Interfaces;

public class RecipeBackgroundService : BackgroundService
{
    private readonly IRecipeManager _recipeManager;
    private readonly ILogger<RecipeBackgroundService> _logger;

    public RecipeBackgroundService(IRecipeManager recipeManager, ILogger<RecipeBackgroundService> logger)
    {
        _recipeManager = recipeManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 定期备份配方数据
                var pools = _recipeManager.GetAllRecipePools();
                foreach (var pool in pools)
                {
                    _recipeManager.ExportRecipePool(pool.Id, $"backup_{pool.Id}_{DateTime.Now:yyyyMMdd}.json");
                }

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in recipe background service");
            }
        }
    }
}
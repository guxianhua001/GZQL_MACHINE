// Controllers/RecipeController.cs
using Microsoft.AspNetCore.Mvc;
using Recipe.Interfaces;
using Recipe.Models;
using System.Runtime.InteropServices;
[ApiController]
[Route("api/[controller]")]
public class RecipeController : ControllerBase
{
    private readonly IRecipeManager _recipeManager;

    public RecipeController(IRecipeManager recipeManager)
    {
        _recipeManager = recipeManager;
    }

    [HttpGet("pools")]
    public IActionResult GetRecipePools()
    {
        var pools = _recipeManager.GetAllRecipePools();
        return Ok(pools);
    }

    [HttpGet("pools/{poolId}/recipes/{recipeId}")]
    public IActionResult GetRecipe(string poolId, string recipeId)
    {
        var recipe = _recipeManager.GetRecipe(poolId, recipeId);
        if (recipe == null)
            return NotFound();

        return Ok(recipe);
    }

    [HttpPost("pools/{poolId}/recipes")]
    public IActionResult CreateRecipe(string poolId, [FromBody] Recipe.Models.RecipeInfo recipe)
    {
        var result = _recipeManager.SaveRecipe(poolId, recipe);
        return result ? Ok() : BadRequest();
    }
}
using Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public static class RecipeFileService
{
    public static void SaveRecipe(Recipe recipe, string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            filePath = $"Recipes/{DateTime.Now:yyyyMMddHHmmss}.recipe";

        var filteredPath = Regex.Replace(filePath, @"[\p{C}]", "");
        File.WriteAllText(filteredPath, JsonConvert.SerializeObject(recipe, Formatting.Indented));
    }

    public static Recipe LoadRecipe(string path)
    {
        try
        {
            if (!IsFileExist(path))
            {
                Interfaces.IMessage.Logger.Warn($"配方文件不存在: {path}");
                return CreateNewRecipe();
            }
            var filteredPath = Regex.Replace(path, @"[\p{C}]", "");
            var json = File.ReadAllText(filteredPath);
            var recipe = JsonConvert.DeserializeObject<Recipe>(json);
            return recipe;
        }
        catch (Exception ex)
        {
            Interfaces.IMessage.Logger.Error(ex, $"配方加载失败: {path}");
            return CreateNewRecipe();
        }
    }
    private static Recipe CreateNewRecipe() => new Recipe
    {
        CreateDate = DateTime.Now,
        Version = 2
    };

    private static void MigrateV1ToV2(Recipe recipe)
    {
        // V1到V2的迁移逻辑
    }
    public static bool IsFileExist(string filePath)
    {
        // 使用正则表达式过滤掉文件路径中的不可视Unicode字符串
        var filteredPath = Regex.Replace(filePath, @"[\p{C}]", "");
        return File.Exists(filteredPath);
    }

    public static Recipe CreateDefaultRecipe()
    {
        return new Recipe
        {
            Name = "default.recipe",
            FilePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Recipes",
                "default.recipe")
        };
    }
}
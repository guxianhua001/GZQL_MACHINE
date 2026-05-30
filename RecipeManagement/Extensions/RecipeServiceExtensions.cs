// Recipe/Extensions/RecipeServiceExtensions.cs
using System;
using Core.Abstraction;
using Recipe.Interfaces;

namespace Recipe.Extensions
{
    public static class RecipeServiceExtensions
    {
        /// <summary>
        /// 获取强类型参数（推荐在工站内部使用）
        /// </summary>
        public static TParameters GetParameters<TParameters>(this IRecipeService service)
            where TParameters : TaskParametersBase
        {
            return (TParameters)service.Parameters;
        }

        /// <summary>
        /// 订阅强类型参数应用事件（推荐在工站内部使用）
        /// </summary>
        public static void SubscribeParametersApplied<TParameters>(
            this IRecipeService service,
            EventHandler<TParameters> handler)
            where TParameters : TaskParametersBase
        {
            service.ParametersApplied += (sender, obj) =>
            {
                if (obj is TParameters typedParams)
                    handler(sender, typedParams);
            };
        }

        /// <summary>
        /// 订阅强类型参数加载事件
        /// </summary>
        public static void SubscribeParametersLoaded<TParameters>(
            this IRecipeService service,
            EventHandler<TParameters> handler)
            where TParameters : TaskParametersBase
        {
            service.ParametersLoaded += (sender, obj) =>
            {
                if (obj is TParameters typedParams)
                    handler(sender, typedParams);
            };
        }
    }
}
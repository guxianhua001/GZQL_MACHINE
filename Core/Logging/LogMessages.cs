using Core.Attributes;
using Core.Abstraction;
using System;

namespace Core.Logging
{
    public static partial class LogMessages
    {
        #region 初始化与依赖注入

        private static ILocalizationService _localization;

        public static void Initialize(ILocalizationService localization)
        {
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        }

        internal static void EnsureInitialized()
        {
            if (_localization == null)
            {
                throw new InvalidOperationException(
                    "LogMessages 未初始化！请在 App.OnStartup 中调用 LogMessages.Initialize(service)");
            }
        }

        internal static ILocalizationService Localization => _localization;

        #endregion
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class GenerateLogMessagesAttribute : Attribute
    {
    }
}

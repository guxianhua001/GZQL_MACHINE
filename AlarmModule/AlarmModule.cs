using System.IO;
using AlarmModule.Converters;
using AlarmModule.Data;
using AlarmModule.Interfaces;
using AlarmModule.Services;
using Core.Abstraction;
using Microsoft.EntityFrameworkCore;
using Prism.Ioc;
using Prism.Modularity;

namespace AlarmModule
{
    /// <summary>
    /// 报警管理模块：提供工业4级报警系统
    /// 支持报警触发、确认、复位、消除完整生命周期
    /// 使用SQLite本地数据库持久化报警数据
    /// </summary>
    public class AlarmModule : IModule
    {
        /// <summary>
        /// 注册模块所需的服务和类型到DI容器
        /// </summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 数据库上下文配置：注册DbContextOptions供Repository每次创建新DbContext实例
            // 避免Singleton DbContext在fire-and-forget调用时被disposed
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "alarms.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            var optionsBuilder = new DbContextOptionsBuilder<AlarmDbContext>();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
            var dbOptions = optionsBuilder.Options;

            // 注册DbContextOptions为单例（配置对象可安全共享）
            containerRegistry.RegisterInstance(dbOptions);

            // 仓储注册为Singleton，内部每次操作创建新DbContext
            containerRegistry.RegisterSingleton<IAlarmRepository, AlarmRepository>();

            // 服务注册
            containerRegistry.RegisterSingleton<IAlarmService, AlarmService>();
            containerRegistry.RegisterSingleton<IAlarmNotificationService, AlarmNotificationService>();

            // 导航视图注册
            containerRegistry.RegisterForNavigation<Views.AlarmListView>();
            containerRegistry.RegisterForNavigation<Views.AlarmHistoryView>();
            containerRegistry.RegisterForNavigation<Views.AlarmThresholdView>();
            containerRegistry.RegisterForNavigation<Views.AlarmStatsView>();
        }

        /// <summary>
        /// 模块初始化：确保数据库表结构已创建
        /// </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var dbOptions = containerProvider.Resolve<DbContextOptions<AlarmDbContext>>();
            using var context = new AlarmDbContext(dbOptions);
            context.Database.EnsureCreated();

            var localizationService = containerProvider.Resolve<ILocalizationService>();
            AlarmLevelToTextConverter.Initialize(localizationService);
            AlarmStatusToTextConverter.Initialize(localizationService);
            AlarmTypeToTextConverter.Initialize(localizationService);
        }
    }
}

using Core.Abstraction;
using Core.Services;
using Interfaces;
using Prism.Ioc;
using SmarterMotion;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SmarterMotion
{
    // 扩展方法修改：记录已注册的Task类型
    public static class ContainerRegistryExtensions
    {
        private static readonly ConcurrentDictionary<Type, byte> RegisteredTaskTypes = new ConcurrentDictionary<Type, byte>();
        // 改为存储具体Task类型与参数的元数据
        private static readonly ConcurrentBag<TaskMetadata> _registeredTasks = new();
        private static int settingId = 0;
        public static void RegisterXTask<TTask, TParams>(this IContainerRegistry container, AppConfig appConfig)
            where TParams : TaskParametersBase, new()
            where TTask : XTask<TParams>
        {
            // 参数初始化
            var parameters = new TParams();
            //parameters = (TParams)parameters;
            container.RegisterInstance<TParams>((TParams)parameters);
            container.RegisterSingleton<TTask>();

            // 记录Task类型
            RegisteredTaskTypes.TryAdd(typeof(TTask), 0);
            // 记录具体类型元数据
            _registeredTasks.Add(new TaskMetadata(
                TaskType: typeof(TTask),
                ParamsType: typeof(TParams)
            ));
            settingId++;
            // 绑定到设置管理器
            //XSettingManager.Instance.BindSetting(
            //    setId: settingId,
            //    setting: parameters  // 直接传递参数实例（继承自XSetting）
            //);
        }

        public static IEnumerable<Type> GetRegisteredTaskTypes()
        {
            return RegisteredTaskTypes.Keys.ToList();
        }
        public static IEnumerable<TaskMetadata> GetRegisteredTasks()
        {
            return _registeredTasks.ToList();
        }

        public record TaskMetadata(Type TaskType, Type ParamsType);
        // 自动发现所有任务
        public static void AutoRegisterXTasks(this IContainerRegistry container, Assembly assembly)
        {
            var taskTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract &&
                          t.BaseType != null &&
                          t.BaseType.IsGenericType &&
                          t.BaseType.GetGenericTypeDefinition() == typeof(XTask<>));

            foreach (var type in taskTypes)
            {
                var paramType = type.BaseType!.GetGenericArguments()[0];
                var method = typeof(ContainerRegistryExtensions)
                    .GetMethod(nameof(RegisterXTask))!
                    .MakeGenericMethod(type, paramType);

                method.Invoke(null, new object[] { container });
            }
        }

        /*
            //获取XTaskBase
            var allTasks = _taskManager.GetAllTasks().ToList();
            foreach (var task in allTasks)
            {
                // 执行Task相关操作
            }
            // 模式匹配处理具体类型(优化成从文件读取task类型)
            var allTasks = _taskManager.GetAllConcreteTasks();
            foreach (var task in allTasks)
            {
                switch (task)
                {
                    case Task1 t1:
                        t1.Parameters.SetCurProduct(_appConfig.Name, _appConfig.LastName);
                        t1.Parameters.SetXmlPath();
                        break;
                    case Task2 t2:
                        t2.Parameters.SetCurProduct(_appConfig.Name, _appConfig.LastName);
                        t2.Parameters.SetXmlPath();
                        break;
                    case Task3 t3:
                        t3.Parameters.SetCurProduct(_appConfig.Name, _appConfig.LastName);
                        t3.Parameters.SetXmlPath();
                        break;
                    case Task4 t4:
                        t4.Parameters.SetCurProduct(_appConfig.Name, _appConfig.LastName);
                        t4.Parameters.SetXmlPath();
                        break;
                }
            }
         */
    }
}

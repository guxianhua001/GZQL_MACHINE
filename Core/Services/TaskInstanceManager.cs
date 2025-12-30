// Core/Services/TaskInstanceManager.cs
using Core.Abstraction;
using Prism.Ioc;
using System.Collections.Concurrent;

public class TaskInstanceManager
{
    private readonly IContainerExtension _container;
    private readonly Dictionary<int, ITask> _taskInstances = new Dictionary<int, ITask>();
    private readonly Dictionary<Type, ITask> _taskInstancesByType = new Dictionary<Type, ITask>();
    //private readonly ConcurrentDictionary<Type, Lazy<object>> _tasks
    //      = new ConcurrentDictionary<Type, Lazy<object>>();

    public TaskInstanceManager(IContainerExtension container)
    {
        _container = container;
    }

    public ITask GetOrCreateTask(Type taskType, int taskId)
    {
        if (_taskInstances.TryGetValue(taskId, out ITask existingInstance))
        {
            return existingInstance;
        }

        // 从容器解析新实例
        var newInstance = _container.Resolve(taskType) as ITask;
        if (newInstance != null)
        {
            newInstance.SetTaskId(taskId);
            _taskInstances[taskId] = newInstance;
            _taskInstancesByType[taskType] = newInstance;
        }

        return newInstance;
    }

    public T GetTask<T>() where T : class, ITask
    {
        _taskInstancesByType.TryGetValue(typeof(T), out ITask instance);
        return instance as T;
    }

    public ITask GetTask(int taskId)
    {
        _taskInstances.TryGetValue(taskId, out ITask instance);
        return instance;
    }
    public ITask GetTask(Type taskType)
    {
        _taskInstancesByType.TryGetValue(taskType, out ITask instance);
        return instance;
    }
    public IEnumerable<ITask> GetAllTasks()
    {
        return _taskInstances.Values;
    }
}

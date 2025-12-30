
using Core.Abstraction;
using Prism.Events;

namespace SmarterMotion
{
    // 接口约束：允许任何类型
    public interface ITask<TParams>
    {
        TParams Parameters { get; }
    }

    public delegate void OnLogRaised(string msg);

    public delegate void OnWarningChangedTaskDelegate(string warningCode, string warningInfo, int level = 2);

    // 泛型增强任务基类
    public abstract class XTask<TParams> : XTaskBase<TParams>, ITask<TParams>
         where TParams : TaskParametersBase, new()
    {
        public new TParams Parameters => base.Parameters;

        protected XTask(int taskId, string taskName, IEventAggregator eventAggregator = null, IParameterStorage parameterStorage = null)
                : base(taskId, taskName, eventAggregator, parameterStorage)
        {
            // 基类已初始化
        }

        /// <summary>
        /// 外部参数注入构造函数
        /// </summary>
        protected XTask(int taskId, string taskName,
            TParams parameters,
            IEventAggregator eventAggregator,
             IParameterStorage parameterStorage = null)
            : this(taskId, taskName, eventAggregator, parameterStorage)
        {
            base.Parameters = parameters;  // 设置基类参数
            base._eventAggregator = eventAggregator;
        }

    }

}

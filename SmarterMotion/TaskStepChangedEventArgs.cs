

using Core.Abstraction;
using System.Windows.Media;

namespace SmarterMotion.Events
{
    public class TaskStepChangedEventArgs
    {
        public ITask Task { get; set; }  // 使用接口代替具体类型
        public string StepMessage { get; set; }
        public Color Color { get; set; }
    }
}

using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Core.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public class FlexiblePosition : INotifyPropertyChanged
    {
        [JsonProperty]
        public Dictionary<string, double?> Axes { get; set; } = new Dictionary<string, double?>();
        [JsonProperty]
        public string Comment { get; set; } = "";   // 注释字段

        public event PropertyChangedEventHandler PropertyChanged;

        public double? this[string axisName]
        {
            get => Axes.TryGetValue(axisName, out var value) ? value : null;
            set
            {
                if (value.HasValue)
                    Axes[axisName] = value;
                else
                    Axes.Remove(axisName);
                OnPropertyChanged($"Item[{axisName}]");
            }
        }


        public IEnumerable<string> GetAxisNames() => Axes.Keys;

        public void SetAxisValue(string axisName, double? value) => this[axisName] = value;

        // 便捷方法：从配置的轴列表初始化所有轴为默认值（可选）
        public void InitializeWithDefaultAxes(IEnumerable<AxisDefinition> axes)
        {
            foreach (var axis in axes)
            {
                if (!Axes.ContainsKey(axis.Name))
                    Axes[axis.Name] = axis.DefaultValue;
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

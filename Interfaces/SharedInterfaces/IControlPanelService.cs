using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces.SharedInterfaces
{
    public interface IControlPanelService
    {
        string CurrentSystemName { get; } // 添加当前系统名称属性
        GantrySystemInfo GetCurrentSystemInfo();
        IGantrySyncService CurrentSystem { get; }
        void SelectSystem(int systemId);
        ICurrentSystemService SystemManager { get; }
        ObservableCollection<GantrySystemInfo> AvailableSystems { get; }
        event EventHandler SystemChanged;
    }
    public class GantrySystemInfo
    {
        public int Id { get; }
        public string Name { get; }

        public GantrySystemInfo(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }
    public enum JogDirection
    {
        Left,
        Right,
        Up,
        Down
    }
}

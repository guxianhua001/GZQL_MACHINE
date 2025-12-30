using AxisConfiguration.Models;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ModuleCore.ViewModels
{
    public class ManageSystemAxesViewModel : BindableBase
    {
        public List<AxisInfo> AllAxes { get; set; } = new List<AxisInfo>();
        public ObservableCollection<string> SelectedAxisIds { get; set; } = new ObservableCollection<string>();

        private ICommand _addAxisCommand;
        public ICommand AddAxisCommand =>
            _addAxisCommand ??= new DelegateCommand<AxisInfo>(AddAxis);

        private ICommand _removeAxisCommand;
        public ICommand RemoveAxisCommand =>
            _removeAxisCommand ??= new DelegateCommand<string>(RemoveAxis);

        private void AddAxis(AxisInfo axis)
        {
            if (axis == null) return;

            if (!SelectedAxisIds.Contains(axis.ConfigId))
            {
                SelectedAxisIds.Add(axis.ConfigId);
            }
        }

        private void RemoveAxis(string axisId)
        {
            if (string.IsNullOrEmpty(axisId)) return;

            if (SelectedAxisIds.Contains(axisId))
            {
                SelectedAxisIds.Remove(axisId);
            }
        }

        public ObservableCollection<AxisInfo> AvailableAxes =>
            new ObservableCollection<AxisInfo>(
                AllAxes.Where(a => !SelectedAxisIds.Contains(a.ConfigId))
            );
    }

}

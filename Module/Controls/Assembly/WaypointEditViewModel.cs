using Module.Models;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Module.ViewModels
{
    public class WaypointEditViewModel : BindableBase
    {
        private ObservableCollection<WaypointItem> _waypoints;
        public ObservableCollection<WaypointItem> Waypoints
        {
            get { return _waypoints; }
            set { SetProperty(ref _waypoints, value); }
        }

        private WaypointItem _selectedWaypoint;
        public WaypointItem SelectedWaypoint
        {
            get { return _selectedWaypoint; }
            set { SetProperty(ref _selectedWaypoint, value); }
        }

        public DelegateCommand AddWaypointCommand { get; }
        public DelegateCommand<WaypointItem> DeleteWaypointCommand { get; }
        public DelegateCommand SaveCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public Action<bool?> CloseDialog { get; set; }

        public WaypointEditViewModel()
        {
            AddWaypointCommand = new DelegateCommand(ExecuteAddWaypoint);
            DeleteWaypointCommand = new DelegateCommand<WaypointItem>(ExecuteDeleteWaypoint);
            SaveCommand = new DelegateCommand(ExecuteSave);
            CancelCommand = new DelegateCommand(ExecuteCancel);
        }

        private void ExecuteAddWaypoint()
        {
            int newIndex = Waypoints.Count > 0 ? Waypoints.Max(w => w.Index) + 1 : 1;
            var newItem = new WaypointItem { Index = newIndex };
            Waypoints.Add(newItem);
        }

        private void ExecuteDeleteWaypoint(WaypointItem item)
        {
            if (item != null)
            {
                Waypoints.Remove(item);
                for (int i = 0; i < Waypoints.Count; i++)
                {
                    Waypoints[i].Index = i + 1;
                }
            }
        }

        private void ExecuteSave()
        {
            CloseDialog?.Invoke(true);
        }

        private void ExecuteCancel()
        {
            CloseDialog?.Invoke(false);
        }
    }
}
using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using SmarterMotion;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.ViewModels
{
    public class CylinderViewModel : BindableBase
    {
        private string cylName;
        public string CylName
        {
            get => cylName;
            set => SetProperty(ref cylName, value);
        }
        private string readyPos = "准备位";
        public string ReadyPos
        {
            get => readyPos;
            set => SetProperty(ref readyPos, value);
        }
        private string workPos = "工作位";
        public string WorkPos
        {
            get => workPos;
            set => SetProperty(ref workPos, value);
        }
        private int setDi1Id;
        public int SetDi1Id
        {
            get => setDi1Id;
            set
            {
                if (setDi1Id != value)
                {
                    SetProperty(ref setDi1Id, value);
                    Sensor1.Sensor = XDevice.Instance.FindDiById(setDi1Id);
                }
            }
        }
        private int setDi2Id;
        public int SetDi2Id
        {
            get => setDi2Id;
            set
            {
                if (setDi1Id != value)
                {
                    SetProperty(ref setDi2Id, value);
                    Sensor2.Sensor = XDevice.Instance.FindDiById(setDi2Id);
                }
            }
        }
        private int setDo1Id;
        public int SetDo1Id
        {
            get => setDo1Id;
            set
            {
                if (setDi1Id != value)
                {
                    SetProperty(ref setDo1Id, value);
                    CylinderReady = XDevice.Instance.FindDoById(setDo1Id);
                }
            }
        }
        private int setDo2Id;
        public int SetDo2Id
        {
            get => setDo2Id;
            set
            {
                if (setDi1Id != value)
                {
                    SetProperty(ref setDo2Id, value);
                    CylinderWork = XDevice.Instance.FindDoById(setDo2Id);
                }
            }
        }
        private SensorViewModel _sensor1 = new SensorViewModel();
        public SensorViewModel Sensor1
        {
            get => _sensor1;
            set => SetProperty(ref _sensor1, value);
        }
        private SensorViewModel _sensor2 = new SensorViewModel();
        public SensorViewModel Sensor2
        {
            get => _sensor2;
            set => SetProperty(ref _sensor2, value);
        }

        private XDo _cylinderReady;
        public XDo CylinderReady
        {
            get => _cylinderReady;
            set => SetProperty(ref _cylinderReady, value);
        }
        private XDo _cylinderWork;
        public XDo CylinderWork
        {
            get => _cylinderWork;
            set => SetProperty(ref _cylinderWork, value);
        }

        public CylinderViewModel()
        {

        }

        private DelegateCommand _cylinderOnCommand;
        public DelegateCommand CylinderOnCommand =>
             _cylinderOnCommand ??= new DelegateCommand(ExecuteCylinderOn);
        private DelegateCommand _cylinderOffCommand;
        public DelegateCommand CylinderOffCommand =>
             _cylinderOffCommand ??= new DelegateCommand(ExecuteCylinderOff);

        private void ExecuteCylinderOn()
        {
            CylinderReady.SetDo(0);
            CylinderWork.SetDo(1);
        }
        private void ExecuteCylinderOff()
        {
            CylinderWork.SetDo(0);
            CylinderReady.SetDo(1);
        }

        public void UpdateCylinder()
        {
            if (Sensor1.Sensor != null && Sensor2.Sensor != null)
            {
                Sensor1.Sensor.Update();
                Sensor2.Sensor.Update();
            }

        }
    }
}

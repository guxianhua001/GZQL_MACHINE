using Core.Extensions;
using Core.Models;
using MathNet.Numerics.LinearAlgebra;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace Module.ViewModels
{
    public class CalibrationPoint : BindableBase
    {
        private string _name;
        private double _cameraX, _cameraY, _cameraZ;
        private double _gripperX, _gripperY, _gripperZ;

        public string Name { get => _name; set => SetProperty(ref _name, value); }
        public double CameraX { get => _cameraX; set => SetProperty(ref _cameraX, value); }
        public double CameraY { get => _cameraY; set => SetProperty(ref _cameraY, value); }
        public double CameraZ { get => _cameraZ; set => SetProperty(ref _cameraZ, value); }
        public double GripperX { get => _gripperX; set => SetProperty(ref _gripperX, value); }
        public double GripperY { get => _gripperY; set => SetProperty(ref _gripperY, value); }
        public double GripperZ { get => _gripperZ; set => SetProperty(ref _gripperZ, value); }
    }

    public class CoordinateCalibrationDialogViewModel : BindableBase, IDialogAware
    {
        private ObservableCollection<CalibrationPoint> _points;
        private string _resultMessage;
        private Matrix3x3 _resultR;
        private Vector3 _resultT;

        public ObservableCollection<CalibrationPoint> Points
        {
            get => _points;
            set => SetProperty(ref _points, value);
        }

        public string ResultMessage
        {
            get => _resultMessage;
            set => SetProperty(ref _resultMessage, value);
        }

        public ICommand AddPointCommand { get; }
        public ICommand DeletePointCommand { get; }
        public ICommand ComputeCommand { get; }
        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        public string Title => "Coordinate Calibration";

        public event Action<IDialogResult> RequestClose;

        public CoordinateCalibrationDialogViewModel()
        {
            Points = new ObservableCollection<CalibrationPoint>();
            AddPointCommand = new DelegateCommand(AddPoint);
            DeletePointCommand = new DelegateCommand<CalibrationPoint>(DeletePoint);
            ComputeCommand = new DelegateCommand(Compute, CanCompute);
            OkCommand = new DelegateCommand(OnOk);
            CancelCommand = new DelegateCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel)));
        }

        private void AddPoint()
        {
            Points.Add(new CalibrationPoint { Name = $"P{Points.Count + 1}" });
        }

        private void DeletePoint(CalibrationPoint point)
        {
            if (point != null) Points.Remove(point);
        }

        private bool CanCompute() => Points.Count >= 3;

        private void Compute()
        {
            var cameraPoints = Points.Select(p => new Point3D(p.CameraX, p.CameraY, p.CameraZ)).ToList();
            var gripperPoints = Points.Select(p => new Point3D(p.GripperX, p.GripperY, p.GripperZ)).ToList();
            (_resultR, _resultT) = ComputeTransform(cameraPoints, gripperPoints);
            ResultMessage = $"Rotation Matrix:\n{_resultR.M11:F4} {_resultR.M12:F4} {_resultR.M13:F4}\n" +
                            $"{_resultR.M21:F4} {_resultR.M22:F4} {_resultR.M23:F4}\n" +
                            $"{_resultR.M31:F4} {_resultR.M32:F4} {_resultR.M33:F4}\n\n" +
                            $"Translation: ({_resultT.X:F3}, {_resultT.Y:F3}, {_resultT.Z:F3})";
        }

        private (Matrix3x3 R, Vector3 t) ComputeTransform(List<Point3D> cameraPoints, List<Point3D> gripperPoints)
        {
            // 调用 CadAlignmentViewModel 中的静态方法
            return CadAlignmentViewModel.ComputeCameraToGripperTransform(cameraPoints, gripperPoints);
        }

        private void OnOk()
        {
            var parameters = new DialogParameters
            {
                { "rotation", _resultR },
                { "translation", _resultT }
            };
            RequestClose?.Invoke(new DialogResult(ButtonResult.OK, parameters));
        }

        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }
        public void OnDialogOpened(IDialogParameters parameters) { }
    }
}
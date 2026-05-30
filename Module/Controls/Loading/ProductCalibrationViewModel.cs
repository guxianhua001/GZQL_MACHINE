using Core.Abstraction;
using Core.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Windows.Input;

namespace Module.ViewModels
{
    public class ProductCalibrationViewModel : BindableBase, IDialogAware
    {
        private readonly IDialogService _dialogService;
        private readonly IStageCalibrationService _calibrationService;

        public FiducialData Fiducial1 { get; }
        public FiducialData Fiducial2 { get; }

        public ICommand ApplyAllCorrectionsCommand { get; }
        public ICommand SaveCalibrationCommand { get; }
        public ICommand LoadCalibrationCommand { get; }

        public string Title => "Stage Align";

        public ProductCalibrationViewModel(IDialogService dialogService)
            : this(dialogService, null)
        {
        }

        public ProductCalibrationViewModel(IDialogService dialogService, IStageCalibrationService calibrationService)
        {
            _dialogService = dialogService;
            _calibrationService = calibrationService;
            Fiducial1 = new FiducialData(_dialogService, "Fiducial 1", _calibrationService, 1);
            Fiducial2 = new FiducialData(_dialogService, "Fiducial 2", _calibrationService, 2);
            ApplyAllCorrectionsCommand = new DelegateCommand(OnApplyAllCorrections);
            SaveCalibrationCommand = new DelegateCommand(OnSaveCalibration);
            LoadCalibrationCommand = new DelegateCommand(OnLoadCalibration);
        }

        private async void OnApplyAllCorrections()
        {
            if (Fiducial1.CorrectCommand.CanExecute(null))
                Fiducial1.CorrectCommand.Execute(null);
            if (Fiducial2.CorrectCommand.CanExecute(null))
                Fiducial2.CorrectCommand.Execute(null);

            if (_calibrationService != null)
            {
                SyncToService();
                await _calibrationService.SaveCalibrationDataAsync();
            }

            RequestClose?.Invoke(new DialogResult(ButtonResult.OK));
        }

        private async void OnSaveCalibration()
        {
            if (_calibrationService == null) return;

            try
            {
                SyncToService();
                await _calibrationService.SaveCalibrationDataAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowDialog("NotificationDialog", new DialogParameters
                {
                    { "title", "Error" },
                    { "message", $"Failed to save calibration data: {ex.Message}" },
                    { "icon", MaterialDesignThemes.Wpf.PackIconKind.ContentSaveAlert }
                }, null);
            }
        }

        private async void OnLoadCalibration()
        {
            if (_calibrationService == null) return;

            try
            {
                await _calibrationService.LoadCalibrationDataAsync();
                SyncFromService();
            }
            catch (Exception ex)
            {
                _dialogService.ShowDialog("NotificationDialog", new DialogParameters
                {
                    { "title", "Error" },
                    { "message", $"Failed to load calibration data: {ex.Message}" },
                    { "icon", MaterialDesignThemes.Wpf.PackIconKind.Download }
                }, null);
            }
        }

        private void SyncToService()
        {
            if (_calibrationService == null) return;

            var data = new StageCalibrationData
            {
                Fiducial1 = Fiducial1.ToData(),
                Fiducial2 = Fiducial2.ToData()
            };
            _calibrationService.ApplyCalibrationData(data);
        }

        private void SyncFromService()
        {
            if (_calibrationService == null) return;

            var data = _calibrationService.GetCurrentCalibrationData();
            if (data == null) return;

            if (data.Fiducial1 != null)
                Fiducial1.FromData(data.Fiducial1);
            if (data.Fiducial2 != null)
                Fiducial2.FromData(data.Fiducial2);
        }

        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }
        public void OnDialogOpened(IDialogParameters parameters) { }

        public event Action<IDialogResult> RequestClose;
    }

    public class FiducialData : BindableBase
    {
        private readonly IDialogService _dialogService;
        private readonly string _name;
        private readonly IStageCalibrationService _calibrationService;
        private readonly int _fiducialIndex;

        private double _photoX, _photoY, _photoZ, _photoRx, _photoRz;
        private double _refX, _refY, _refAngle;
        private double _measuredX, _measuredY, _measuredAngle;

        private bool _hasCaptured;
        public bool HasCaptured
        {
            get => _hasCaptured;
            private set => SetProperty(ref _hasCaptured, value);
        }

        public FiducialData(IDialogService dialogService, string name, IStageCalibrationService calibrationService = null, int fiducialIndex = 0)
        {
            _dialogService = dialogService;
            _name = name;
            _calibrationService = calibrationService;
            _fiducialIndex = fiducialIndex;

            PhotoX = 100; PhotoY = 150; PhotoZ = 20; PhotoRx = 0; PhotoRz = 0;
            RefX = 100; RefY = 150; RefAngle = 0;
            _measuredX = RefX; _measuredY = RefY; _measuredAngle = RefAngle;

            TeachPhotoPosCommand = new DelegateCommand(OnTeachPhotoPos);
            GoToPhotoCommand = new DelegateCommand(OnGoToPhoto);
            CaptureCommand = new DelegateCommand(OnCapture);
            CorrectCommand = new DelegateCommand(OnCorrect, () => HasCaptured)
                                                .ObservesProperty(() => MeasuredX)
                                                .ObservesProperty(() => MeasuredY)
                                                .ObservesProperty(() => MeasuredAngle);
        }

        public double PhotoX { get => _photoX; set => SetProperty(ref _photoX, value); }
        public double PhotoY { get => _photoY; set => SetProperty(ref _photoY, value); }
        public double PhotoZ { get => _photoZ; set => SetProperty(ref _photoZ, value); }
        public double PhotoRx { get => _photoRx; set => SetProperty(ref _photoRx, value); }
        public double PhotoRz { get => _photoRz; set => SetProperty(ref _photoRz, value); }

        public double RefX { get => _refX; set => SetProperty(ref _refX, value); }
        public double RefY { get => _refY; set => SetProperty(ref _refY, value); }
        public double RefAngle { get => _refAngle; set => SetProperty(ref _refAngle, value); }

        public double MeasuredX { get => _measuredX; private set => SetProperty(ref _measuredX, value); }
        public double MeasuredY { get => _measuredY; private set => SetProperty(ref _measuredY, value); }
        public double MeasuredAngle { get => _measuredAngle; private set => SetProperty(ref _measuredAngle, value); }

        public string OffsetDisplay => $"ΔX:{MeasuredX - RefX:+0.000;-0.000}  ΔY:{MeasuredY - RefY:+0.000;-0.000}  ΔAngle:{MeasuredAngle - RefAngle:+0.000;-0.000}";

        public ICommand TeachPhotoPosCommand { get; }
        public ICommand GoToPhotoCommand { get; }
        public ICommand CaptureCommand { get; }
        public ICommand CorrectCommand { get; }

        private async void OnGoToPhoto()
        {
            if (_calibrationService != null)
            {
                try
                {
                    await _calibrationService.GoToPhotoPositionAsync(PhotoX, PhotoY, PhotoZ, PhotoRx, PhotoRz);
                }
                catch (Exception ex)
                {
                    _dialogService.ShowDialog("NotificationDialog", new DialogParameters
                    {
                        { "title", "Error" },
                        { "message", $"{_name}: Failed to move to photo position - {ex.Message}" },
                        { "icon", MaterialDesignThemes.Wpf.PackIconKind.Error }
                    }, null);
                }
            }
            else
            {
                _dialogService.ShowDialog("NotificationDialog", new DialogParameters
                {
                    { "title", "Info" },
                    { "message", $"{_name}: Moving to photo position (X:{PhotoX}, Y:{PhotoY}, Z:{PhotoZ}, Rx:{PhotoRx}, Rz:{PhotoRz})" },
                    { "icon", MaterialDesignThemes.Wpf.PackIconKind.Information }
                }, null);
            }
        }

        private async void OnCapture()
        {
            if (_calibrationService != null)
            {
                try
                {
                    var result = await _calibrationService.CaptureFiducialAsync(_fiducialIndex);
                    if (result.Success)
                    {
                        OnCaptureFromService(result);
                    }
                    else
                    {
                        _dialogService.ShowDialog("NotificationDialog", new DialogParameters
                        {
                            { "title", "Error" },
                            { "message", $"{_name}: Capture failed - {result.ErrorMessage}" },
                            { "icon", MaterialDesignThemes.Wpf.PackIconKind.Error }
                        }, null);
                    }
                }
                catch (Exception ex)
                {
                    _dialogService.ShowDialog("NotificationDialog", new DialogParameters
                    {
                        { "title", "Error" },
                        { "message", $"{_name}: Capture failed - {ex.Message}" },
                        { "icon", MaterialDesignThemes.Wpf.PackIconKind.Error }
                    }, null);
                }
            }
            else
            {
                var rand = new Random();
                MeasuredX = RefX + (rand.NextDouble() - 0.5) * 0.2;
                MeasuredY = RefY + (rand.NextDouble() - 0.5) * 0.2;
                MeasuredAngle = RefAngle + (rand.NextDouble() - 0.5) * 0.1;
                RaisePropertyChanged(nameof(OffsetDisplay));
                (CorrectCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                HasCaptured = true;
            }
        }

        private async void OnCorrect()
        {
            double dx = MeasuredX - RefX;
            double dy = MeasuredY - RefY;
            double dAngle = MeasuredAngle - RefAngle;

            if (_calibrationService != null)
            {
                try
                {
                    await _calibrationService.ApplyCorrectionAsync(dx, dy, dAngle);
                }
                catch (Exception ex)
                {
                    _dialogService.ShowDialog("NotificationDialog", new DialogParameters
                    {
                        { "title", "Error" },
                        { "message", $"{_name}: Correction failed - {ex.Message}" },
                        { "icon", MaterialDesignThemes.Wpf.PackIconKind.Error }
                    }, null);
                    return;
                }
            }

            MeasuredX = RefX;
            MeasuredY = RefY;
            MeasuredAngle = RefAngle;
            RaisePropertyChanged(nameof(OffsetDisplay));
            (CorrectCommand as DelegateCommand)?.RaiseCanExecuteChanged();
            HasCaptured = false;
        }

        public void OnTeachPhotoPos()
        {
            if (_calibrationService != null)
            {
                OnTeachFromServiceAsync();
            }
            else
            {
                PhotoX = 120;
                PhotoY = 180;
                PhotoZ = 25;
                PhotoRx = 0.5;
                PhotoRz = -0.2;
            }
        }

        private async void OnTeachFromServiceAsync()
        {
            try
            {
                var result = await _calibrationService.TeachCurrentPositionAsync();
                OnTeachFromService(result);
            }
            catch
            {
            }
        }

        public void OnCaptureFromService(FiducialCaptureResult result)
        {
            if (result == null || !result.Success) return;

            MeasuredX = result.X;
            MeasuredY = result.Y;
            MeasuredAngle = result.Angle;
            RaisePropertyChanged(nameof(OffsetDisplay));
            (CorrectCommand as DelegateCommand)?.RaiseCanExecuteChanged();
            HasCaptured = true;
        }

        public void OnTeachFromService(CurrentPositionResult result)
        {
            if (result == null) return;

            PhotoX = result.X;
            PhotoY = result.Y;
            PhotoZ = result.Z;
            PhotoRx = result.Rx;
            PhotoRz = result.Rz;
        }

        public StageCalibrationFiducialData ToData()
        {
            return new StageCalibrationFiducialData
            {
                PhotoX = PhotoX,
                PhotoY = PhotoY,
                PhotoZ = PhotoZ,
                PhotoRx = PhotoRx,
                PhotoRz = PhotoRz,
                RefX = RefX,
                RefY = RefY,
                RefAngle = RefAngle,
                MeasuredX = MeasuredX,
                MeasuredY = MeasuredY,
                MeasuredAngle = MeasuredAngle
            };
        }

        public void FromData(StageCalibrationFiducialData data)
        {
            if (data == null) return;

            PhotoX = data.PhotoX;
            PhotoY = data.PhotoY;
            PhotoZ = data.PhotoZ;
            PhotoRx = data.PhotoRx;
            PhotoRz = data.PhotoRz;
            RefX = data.RefX;
            RefY = data.RefY;
            RefAngle = data.RefAngle;
            MeasuredX = data.MeasuredX;
            MeasuredY = data.MeasuredY;
            MeasuredAngle = data.MeasuredAngle;
            RaisePropertyChanged(nameof(OffsetDisplay));
        }
    }
}

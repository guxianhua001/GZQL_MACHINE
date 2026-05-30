using Prism.Mvvm;

public class AssyCalibrationPoint : BindableBase
{
    private string _id;
    private double _x, _y, _z, _rx, _rz,_ofs;
    private string _assySite;

    public string Id { get => _id; set => SetProperty(ref _id, value); }
    public double X { get => _x; set => SetProperty(ref _x, value); }
    public double Y { get => _y; set => SetProperty(ref _y, value); }
    public double Z { get => _z; set => SetProperty(ref _z, value); }
    public double Rx { get => _rx; set => SetProperty(ref _rx, value); }
    public double Rz { get => _rz; set => SetProperty(ref _rz, value); }
    public double Ofs { get => _ofs; set => SetProperty(ref _ofs, value); }
    public string AssySite { get => _assySite; set => SetProperty(ref _assySite, value); }
}
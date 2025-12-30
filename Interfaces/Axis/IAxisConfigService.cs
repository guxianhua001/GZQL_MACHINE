using AxisConfiguration.Models;
using Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public interface IAxisConfigService
{
    IEnumerable<AxisInfo> LoadAllAxes();
    Task DownloadSingleAxisAsync(AxisInfo axis);
    Task DownloadAllParametersAsync(IProgressReporter progressReporter = null);
    void SaveAxisParameters(AxisInfo axis);
    void UploadParameters(AxisInfo axis);
    AxisParams LoadAxisParameters(int cardId, int axisId);
    IEnumerable<InterpolationSystem> LoadInterpolationSystems();
    void ApplyInterpolationParameters(InterpolationSystem interpolationSystem);
    void SaveInterpolationSystem(InterpolationSystem interpolationSystemParams);
    double GetAxisSpeed(int cardId, int axisId);
    double GetInterpolationSpeeds(int cardId, int coordId);
}


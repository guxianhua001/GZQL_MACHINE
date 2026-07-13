using System.Collections.Generic;
using Core.Models;
using Core.Services;

namespace Core.Abstraction
{
    /// <summary>
    /// ZMAP高度图Z值提取服务——核心作用是把ROI像素轨迹的XYZ与机械坐标系对齐。
    /// 主要流程对齐参考Plugin.DispensePath：ROI按目标CadPoint数量生成像素轨迹点，
    /// 双线性采样Z，并将像素XY正向转换为机械XY供预览，确认后由Step3仅写回CadPoint.Z。
    /// 同时保留按机械XY反查像素的接口，供标定验证和后续扩展使用。
    ///
    /// 对齐链路：
    ///   ROI --等距采样到目标点数--> ZMAP像素(PixelCol,PixelRow)
    ///   ZMAP像素 --双线性采样--> RawZ --+ZOffset--> CorrectedZ
    ///   ZMAP像素 --像素↔机械仿射正变换--> 机械坐标(MachineX,MachineY)
    ///
    /// 接口方法签名只使用基础数值类型，不暴露Halcon类型，保证Core层可独立编译；
    /// 具体图像读取/像素采样由Module层实现（依赖Halcon，见 ZMapHeightExtractionService）。
    /// </summary>
    public interface IZMapHeightExtractionService
    {
        /// <summary>是否已成功加载ZMAP高度图</summary>
        bool IsHeightMapLoaded { get; }

        /// <summary>当前加载的ZMAP图像宽度（像素列数）</summary>
        int HeightMapWidth { get; }

        /// <summary>当前加载的ZMAP图像高度（像素行数）</summary>
        int HeightMapHeight { get; }

        /// <summary>当前加载的ZMAP文件路径</summary>
        string LoadedFilePath { get; }

        /// <summary>用于预览显示的归一化灰度图路径（PNG临时文件），未加载时为空</summary>
        string PreviewImagePath { get; }

        /// <summary>ZMAP图像中代表"无效/未测量"的高度值，默认-1</summary>
        double InvalidHeightValue { get; set; }

        /// <summary>Z基准偏移量：CorrectedZ = RawZ + ZOffset</summary>
        double ZOffset { get; set; }

        /// <summary>当前生效的"像素↔机械"仿射标定结果；未标定时为 null</summary>
        AffineCalibrationResult CurrentCalibration { get; }

        /// <summary>
        /// 加载ZMAP高度图文件（单通道32位浮点tif，像素灰度值即为高度值mm）。
        /// 加载时会校验通道数与图像类型，非法文件返回false并给出错误说明。
        /// </summary>
        bool LoadHeightMap(string filePath, out string error);

        /// <summary>
        /// 用标定点求解"ZMAP像素坐标↔机械坐标"仿射矩阵（复用 AffineCalibrationService.Solve，
        /// 数学上与DXF↔机械标定完全相同，只是标定点的物理意义不同）。
        /// 求解成功后自动写入 CurrentCalibration。
        /// </summary>
        /// <param name="calibrationPoints">≥3个不共线的标定点</param>
        /// <param name="error">失败时的错误说明</param>
        AffineCalibrationResult ComputeCalibration(IList<ZMapCalibrationPoint> calibrationPoints, out string error);

        /// <summary>直接设置标定结果（如从配置文件恢复）</summary>
        void SetCalibration(AffineCalibrationResult calibration);

        /// <summary>按机械坐标反查ZMAP像素位置（仿射逆变换），未标定时返回false</summary>
        bool TryGetPixelForMachinePoint(double machineX, double machineY, out double pixelCol, out double pixelRow);

        /// <summary>按像素坐标（可为亚像素浮点）双线性采样高度原始值，越界或无效值时返回false</summary>
        bool TrySampleRawHeightAtPixel(double pixelCol, double pixelRow, out double rawZ);

        /// <summary>
        /// 按机械坐标一步提取修正后的高度值（= 反查像素 + 双线性采样 + ZOffset修正）。
        /// </summary>
        bool TrySampleHeightAtMachinePoint(double machineX, double machineY, out double correctedZ);

        /// <summary>批量提取——对一组机械坐标点逐一执行 TrySampleHeightAtMachinePoint，返回详细结果供预览</summary>
        List<ZMapHeightSampleResult> SampleHeights(IEnumerable<(double MachineX, double MachineY)> machinePoints);

        /// <summary>
        /// 按ROI生成的ZMAP像素轨迹点提取XYZ：像素XY经当前标定正向转换为机械XY，
        /// 像素灰度经双线性采样与ZOffset修正后得到Z。对齐参考Plugin.DispensePath的
        /// “ROI轨迹点采样Z→像素XY转机械XY”处理顺序。
        /// </summary>
        List<ZMapHeightSampleResult> SamplePixelHeights(IEnumerable<ZMapPixelPoint> pixelPoints);

        /// <summary>
        /// Z基准标定：已知某已知机械Z高度的参考点，在该点对应像素处采样得到原始灰度值后，
        /// 计算 ZOffset = referenceMachineZ - rawZAtReference，并写入 ZOffset 属性。
        /// </summary>
        void CalibrateZOffset(double referenceMachineZ, double rawZAtReference);

        /// <summary>导出当前标定点/仿射矩阵/ZOffset等，供持久化保存</summary>
        ZMapCalibrationConfig ExportConfig();

        /// <summary>从持久化配置恢复标定点/仿射矩阵/ZOffset（不加载图像本身）</summary>
        void ImportConfig(ZMapCalibrationConfig config);

        /// <summary>释放已加载的高度图及预览图临时文件</summary>
        void Unload();
    }
}

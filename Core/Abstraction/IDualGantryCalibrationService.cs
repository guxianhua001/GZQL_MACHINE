using Core.Models;
using Core.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Abstraction
{
    /// <summary>
    /// 双龙门标定服务接口——提供双龙门独立仿射标定、公共基准点采集、跨龙门Y基准对齐功能
    /// 机构特点：龙门1(Dx+Dy独立) + 龙门2(X2+共用Y) + 双上相机(Cam1/Cam2)
    /// 以共用下层Y轴为公共基准，融合两套龙门坐标系，消除跨龙门XY误差
    /// </summary>
    public interface IDualGantryCalibrationService
    {
        /// <summary>是否正在自动标定中（龙门1或龙门2）</summary>
        bool IsAutoCalibrating { get; }

        /// <summary>
        /// 启动指定龙门的自动标定流程
        /// 流程：移动到点位 → 发送触发命令 → 读取当前位置 → 等待视觉数据 → 填充 → 标记已标定 → 延时 → 下一点 → 全部完成后计算仿射
        /// </summary>
        /// <param name="gantryId">龙门编号：1 或 2</param>
        /// <param name="points">标定点集合</param>
        /// <param name="config">机构配置</param>
        /// <param name="ct">取消令牌（支持急停）</param>
        Task StartAutoCalibrationAsync(int gantryId, IList<DualGantryCalibrationPoint> points, DualGantryCalibrationConfig config, CancellationToken ct);

        /// <summary>停止自动标定（取消所有龙门的标定流程）</summary>
        void StopAutoCalibration();

        /// <summary>
        /// 示教指定龙门的单点机械坐标（读取当前轴位置）
        /// 龙门1: 读取 Gantry1AxisX + Gantry1AxisY
        /// 龙门2: 读取 Gantry2AxisX + CommonAxisY
        /// </summary>
        /// <param name="gantryId">龙门编号：1 或 2</param>
        /// <param name="config">机构配置</param>
        /// <returns>包含当前机械坐标的标定点</returns>
        Task<DualGantryCalibrationPoint> TeachPointAsync(int gantryId, DualGantryCalibrationConfig config);

        /// <summary>
        /// 移动到指定龙门的单点机械坐标
        /// 龙门1: 移动 Gantry1AxisX + Gantry1AxisY
        /// 龙门2: 移动 Gantry2AxisX + CommonAxisY
        /// </summary>
        /// <param name="gantryId">龙门编号：1 或 2</param>
        /// <param name="point">目标点位</param>
        /// <param name="config">机构配置</param>
        Task MoveToPointAsync(int gantryId, DualGantryCalibrationPoint point, DualGantryCalibrationConfig config);

        /// <summary>
        /// 订阅指定龙门的TCP视觉数据
        /// 龙门1: 订阅 Gantry1TcpConnection
        /// 龙门2: 订阅 Gantry2TcpConnection
        /// </summary>
        /// <param name="gantryId">龙门编号：1 或 2</param>
        /// <param name="connectionName">TCP连接名称</param>
        void SubscribeVisionData(int gantryId, string connectionName);

        /// <summary>取消订阅指定龙门的TCP视觉数据</summary>
        /// <param name="gantryId">龙门编号：1 或 2</param>
        void UnsubscribeVisionData(int gantryId);

        /// <summary>取消所有TCP订阅（龙门1和龙门2）</summary>
        void UnsubscribeAllVisionData();

        /// <summary>
        /// 计算指定龙门的仿射标定结果（N点最小二乘法，>=3点）
        /// 视觉坐标作为CAD坐标（输入），机械坐标作为输出
        /// </summary>
        /// <param name="points">已标定的点集合</param>
        /// <returns>仿射标定结果，包含6个参数和RMS误差</returns>
        AffineCalibrationResult ComputeCalibration(IList<DualGantryCalibrationPoint> points);

        /// <summary>
        /// 采集Cam1公共基准数据（Y轴在Cam1视野内时调用）
        /// 流程：读取当前共用Y位置 → 触发Cam1拍照 → 等待视觉数据 → 返回采集结果
        /// </summary>
        /// <param name="config">机构配置</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>(CommonY1, Gantry1VisionX, Gantry1VisionY)</returns>
        Task<(double CommonY1, double VisionX, double VisionY)> CaptureReferenceGantry1Async(DualGantryCalibrationConfig config, CancellationToken ct);

        /// <summary>
        /// 采集Cam2公共基准数据（Y轴在Cam2视野内时调用）
        /// 流程：读取当前共用Y位置 → 触发Cam2拍照 → 等待视觉数据 → 返回采集结果
        /// </summary>
        /// <param name="config">机构配置</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>(CommonY2, Gantry2VisionX, Gantry2VisionY)</returns>
        Task<(double CommonY2, double VisionX, double VisionY)> CaptureReferenceGantry2Async(DualGantryCalibrationConfig config, CancellationToken ct);

        /// <summary>
        /// 跨龙门Y基准对齐计算（基于公共基准点计算变换参数）
        /// 算法：通过两套龙门仿射结果将视觉坐标转为机械坐标，使用最小二乘法拟合 OffsetX/OffsetY/RotationDeg
        /// </summary>
        /// <param name="referencePoints">公共基准点列表（>=2点）</param>
        /// <param name="gantry1Result">龙门1仿射标定结果</param>
        /// <param name="gantry2Result">龙门2仿射标定结果</param>
        /// <returns>跨龙门变换参数（龙门1→龙门2）</returns>
        GantryTransform ComputeGantryTransform(IList<CommonReferencePoint> referencePoints, AffineCalibrationResult gantry1Result, AffineCalibrationResult gantry2Result);

        /// <summary>单点标定完成事件：(龙门编号, 点序号, 标定点数据)</summary>
        event Action<int, int, DualGantryCalibrationPoint> PointCalibrated;

        /// <summary>视觉数据到达事件：(龙门编号, 视觉X, 视觉Y)</summary>
        event Action<int, double, double> VisionDataReceived;

        /// <summary>单龙门标定完成事件：(龙门编号, 仿射结果)</summary>
        event Action<int, AffineCalibrationResult> GantryCalibrationCompleted;

        /// <summary>公共基准点采集完成事件</summary>
        event Action<CommonReferencePoint> CommonReferenceCaptured;

        /// <summary>跨龙门对齐完成事件</summary>
        event Action<GantryTransform> GantryTransformComputed;

        /// <summary>
        /// 获取当前跨龙门变换参数（供其他模块如CadAlignment夹爪定位使用）
        /// </summary>
        /// <returns>已对齐的变换参数；未标定返回null</returns>
        GantryTransform? GetGantryTransform();

        /// <summary>标定错误事件：(龙门编号, 错误信息) gantryId=0 表示通用错误</summary>
        event Action<int, string> CalibrationError;
    }
}

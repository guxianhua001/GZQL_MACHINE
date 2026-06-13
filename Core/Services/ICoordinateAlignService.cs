// Core/Services/ICoordinateAlignService.cs
using System.Collections.Generic;
using Core.Models;

namespace Core.Services
{
    /// <summary>
    /// 坐标对齐模式枚举
    /// </summary>
    public enum AlignMode
    {
        /// <summary>N点仿射标定——使用>=3对对应点求解仿射变换矩阵</summary>
        Affine,

        /// <summary>逐点映射——每个CAD点独立映射到机械坐标</summary>
        PointMapping
    }

    /// <summary>
    /// 坐标对齐服务接口——负责CAD坐标系到机械坐标系的映射转换，
    /// 支持两种对齐模式：基准点偏移模式和逐点映射模式
    /// </summary>
    public interface ICoordinateAlignService
    {
        /// <summary>当前对齐模式（只读）</summary>
        AlignMode CurrentMode { get; }

        /// <summary>设置对齐模式</summary>
        /// <param name="mode">目标对齐模式</param>
        void SetMode(AlignMode mode);

        /// <summary>设置CAD图纸中的基准点（Mark/Fiducial）坐标</summary>
        /// <param name="x">CAD基准点X</param>
        /// <param name="y">CAD基准点Y</param>
        /// <param name="z">CAD基准点Z</param>
        void SetMapFiducial(double x, double y, double z);

        /// <summary>设置机械坐标系下的基准点坐标及旋转量</summary>
        /// <param name="x">机械基准点X</param>
        /// <param name="y">机械基准点Y</param>
        /// <param name="z">机械基准点Z</param>
        /// <param name="rx">绕X轴旋转角度（度数）</param>
        /// <param name="rz">绕Z轴旋转角度（度数）</param>
        void SetMachineFiducial(double x, double y, double z, double rx, double rz);

        /// <summary>
        /// 模式1自动计算——根据已设置的基准点偏移构建变换矩阵，
        /// 并对所有已注册的CadPoint执行坐标转换
        /// </summary>
        void AutoCalculate();

        /// <summary>注册需要参与坐标变换的点集（Mode1使用）</summary>
        /// <param name="cadPoints">待变换的CAD点集合</param>
        void RegisterPoints(IEnumerable<CadPoint> cadPoints);

        /// <summary>
        /// 模式2逐点映射——为指定ID的点设置其对应的机械坐标
        /// </summary>
        /// <param name="pointId">点的唯一标识</param>
        /// <param name="mx">目标机械X坐标</param>
        /// <param name="my">目标机械Y坐标</param>
        /// <param name="mz">目标机械Z坐标</param>
        void SetPointMapping(string pointId, double mx, double my, double mz);

        /// <summary>
        /// 将单个CAD坐标点转换为机械坐标点
        /// Mode1: 使用内部变换矩阵计算；Mode2: 在映射表中查找
        /// </summary>
        /// <param name="cadPoint">输入的CAD坐标点</param>
        /// <returns>包含机械坐标的CadPoint副本</returns>
        CadPoint TransformToMachine(CadPoint cadPoint);

        /// <summary>获取当前的坐标变换对象（含Tx/Ty/Tz/Rotation/Scale参数）</summary>
        /// <returns>当前生效的CoordinateTransform实例</returns>
        CoordinateTransform GetTransform();

        /// <summary>设置方向点距离（仿射模式下自动生成虚拟方向点B的偏移距离，默认100mm）</summary>
        void SetDirectionLength(double length);

        /// <summary>仿射模式自动计算——自动生成方向点B，使用Halcon VectorToHomMat2D计算仿射矩阵</summary>
        void AutoCalculateAffine();

        /// <summary>获取仿射矩阵参数文本（用于UI显示）</summary>
        string GetAffineMatrixDisplay();
    }
}

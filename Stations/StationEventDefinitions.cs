using System;
using System.Threading;

/// <summary>
/// 工站间通信事件集中管理器
/// </summary>
public static class StationEvents
{
    #region 上料站相关事件

    /// <summary>
    /// 上料站物料就绪信号 (上料站 → 装配站)
    /// </summary>
    public static AutoResetEvent MaterialReadySignal = new AutoResetEvent(false);

    /// <summary>
    /// 上料站完成信号 (上料站 → 所有工站)
    /// </summary>
    public static AutoResetEvent LoadingCompletedEvent = new AutoResetEvent(false);

    /// <summary>
    /// 上料站物料3D扫描就绪 (上料站 → 点胶站)
    /// </summary>
    public static AutoResetEvent Material3DScanReady = new AutoResetEvent(false);

    #endregion

    #region 装配站相关事件

    /// <summary>
    /// 装配站拍照完成信号 (装配站 → 上料站)
    /// </summary>
    public static AutoResetEvent AssemblyPhotoCompleted = new AutoResetEvent(false);

    /// <summary>
    /// 装配站取料完成信号 (装配站 → 上料站)
    /// </summary>
    public static AutoResetEvent AssemblyPickupCompleted = new AutoResetEvent(false);

    /// <summary>
    /// 装配站组装完成信号 (装配站 → 点胶站)
    /// </summary>
    public static AutoResetEvent AssemblyCompleted = new AutoResetEvent(false);

    /// <summary>
    /// 装配站触发3D扫描开始 (装配站 → 点胶站)
    /// </summary>
    public static AutoResetEvent AssemblyTrigger3DScan = new AutoResetEvent(false);

    /// <summary>
    /// 装配站请求物料信号 (装配站 → 上料站)
    /// </summary>
    public static AutoResetEvent AssemblyRequestMaterial = new AutoResetEvent(false);

    #endregion

    #region 点胶站相关事件
    /// <summary>
    /// 点胶站回零完成信号 (点胶站 → 组装站)
    /// </summary>
    public static AutoResetEvent DispensingStationZeroCompleted = new AutoResetEvent(false);

    /// <summary>
    /// 点胶站就绪信号 (点胶站 → 装配站)
    /// </summary>
    public static AutoResetEvent DispensingStationReady = new AutoResetEvent(false);

    /// <summary>
    /// 点胶站完成信号 (点胶站 → 装配站)
    /// </summary>
    public static AutoResetEvent DispensingCompleted = new AutoResetEvent(false);

    /// <summary>
    /// UV固化完成信号 (点胶站 → 装配站)
    /// </summary>
    public static AutoResetEvent UVFixCompleted = new AutoResetEvent(false);

    /// <summary>
    /// 点胶站3D扫描完成信号 (点胶站 → 上料站)
    /// </summary>
    public static AutoResetEvent Dispensing3DScanCompleted = new AutoResetEvent(false);

    #endregion

    #region 物料流转相关事件

    /// <summary>
    /// 物料移入信号 (上料站 → 装配站)
    /// </summary>
    public static AutoResetEvent MaterialMoveInSignal = new AutoResetEvent(false);

    /// <summary>
    /// 物料移出信号 (装配站 → 上料站)
    /// </summary>
    public static AutoResetEvent MaterialMoveOutSignal = new AutoResetEvent(false);

    /// <summary>
    /// 物料到位确认信号 (上料站 → 装配站)
    /// </summary>
    public static AutoResetEvent MaterialInPlaceConfirmed = new AutoResetEvent(false);

    #endregion

    #region 全局状态信号

    /// <summary>
    /// 急停信号 (任何工站 → 所有工站)
    /// </summary>
    public static AutoResetEvent EmergencyStopSignal = new AutoResetEvent(false);

    /// <summary>
    /// 系统暂停信号 (主控 → 所有工站)
    /// </summary>
    public static AutoResetEvent SystemPauseSignal = new AutoResetEvent(false);

    /// <summary>
    /// 系统继续信号 (主控 → 所有工站)
    /// </summary>
    public static AutoResetEvent SystemResumeSignal = new AutoResetEvent(false);

    #endregion

    #region 数据传递字段

    /// <summary>
    /// 当前请求的工位索引
    /// </summary>
    public static int CurrentStationIndex { get; set; } = 0;

    /// <summary>
    /// 拍照结果数据
    /// </summary>
    public static string PhotoResultData { get; set; } = string.Empty;

    /// <summary>
    /// 错误消息
    /// </summary>
    public static string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// 操作结果
    /// </summary>
    public static bool OperationResult { get; set; } = false;

    #endregion

    #region 视觉系统相关事件

    /// <summary>
    /// Tab拍照请求信号 (装配站 → 视觉系统)
    /// </summary>
    public static AutoResetEvent VisionTabPhotoRequest = new AutoResetEvent(false);

    /// <summary>
    /// Pillar拍照请求信号 (装配站 → 视觉系统)
    /// </summary>
    public static AutoResetEvent VisionPillarPhotoRequest = new AutoResetEvent(false);

    /// <summary>
    /// 底部拍照请求信号 (装配站 → 视觉系统)
    /// </summary>
    public static AutoResetEvent VisionBottomPhotoRequest = new AutoResetEvent(false);

    /// <summary>
    /// 侧部拍照请求信号 (装配站 → 视觉系统)
    /// </summary>
    public static AutoResetEvent VisionSidePhotoRequest = new AutoResetEvent(false);

    /// <summary>
    /// 视觉系统拍照完成信号 (视觉系统 → 装配站)
    /// </summary>
    public static AutoResetEvent VisionPhotoCompleted = new AutoResetEvent(false);

    /// <summary>
    /// 视觉系统就绪信号 (视觉系统 → 所有工站)
    /// </summary>
    public static AutoResetEvent VisionSystemReady = new AutoResetEvent(false);
    /// <summary>
    /// Tab拍照完成信号 (视觉系统 → 上料站)
    /// </summary>
    public static AutoResetEvent TabPhotoCompleted = new AutoResetEvent(false);

    /// <summary>
    /// Pillar1拍照完成信号 (视觉系统 → 上料站)
    /// </summary>
    public static AutoResetEvent Pillar1PhotoCompleted = new AutoResetEvent(false);

    /// <summary>
    /// Pillar2拍照完成信号 (视觉系统 → 上料站)
    /// </summary>
    public static AutoResetEvent Pillar2PhotoCompleted = new AutoResetEvent(false);

    /// <summary>
    /// 取料拍照完成信号 (视觉系统 → 上料站)
    /// </summary>
    public static AutoResetEvent PickupPhotoCompleted = new AutoResetEvent(false);

    #endregion

    #region 视觉系统数据字段

    /// <summary>
    /// 视觉请求类型
    /// </summary>
    public static VisionRequestType CurrentVisionRequestType { get; set; }

    /// <summary>
    /// 拍照组号
    /// </summary>
    public static int VisionPhotoGroup { get; set; }

    /// <summary>
    /// 拍照位置索引
    /// </summary>
    public static int VisionPositionIndex { get; set; }

    /// <summary>
    /// 视觉系统返回的数据
    /// </summary>
    public static string VisionResultData { get; set; } = string.Empty;

    /// <summary>
    /// 视觉系统处理结果
    /// </summary>
    public static bool VisionProcessResult { get; set; }

    #endregion

    #region 视觉系统专用方法

    /// <summary>
    /// 发送视觉拍照请求
    /// </summary>
    public static void SendVisionRequest(VisionRequestType requestType, int photoGroup, int positionIndex, string additionalData = "")
    {
        lock (_visionLock)
        {
            CurrentVisionRequestType = requestType;
            VisionPhotoGroup = photoGroup;
            VisionPositionIndex = positionIndex;
            VisionResultData = additionalData;
            VisionProcessResult = false;

            // 根据请求类型触发相应的事件
            switch (requestType)
            {
                case VisionRequestType.Tab:
                    VisionTabPhotoRequest.Set();
                    break;
                case VisionRequestType.Pillar:
                    VisionPillarPhotoRequest.Set();
                    break;
                case VisionRequestType.Bottom:
                    VisionBottomPhotoRequest.Set();
                    break;
                case VisionRequestType.Side:
                    VisionSidePhotoRequest.Set();
                    break;
            }
        }
    }

    /// <summary>
    /// 发送视觉系统响应
    /// </summary>
    public static void SendVisionResponse(bool success, string resultData = "", string errorMessage = "")
    {
        lock (_visionLock)
        {
            VisionProcessResult = success;
            VisionResultData = resultData;
            if (!success)
            {
                ErrorMessage = errorMessage;
            }
            VisionPhotoCompleted.Set();
        }
    }

    /// <summary>
    /// 等待视觉系统响应
    /// </summary>
    public static (bool success, string data) WaitForVisionResponse(int timeoutMs, CancellationToken cancellationToken = default)
    {
        var waitHandles = new WaitHandle[] { VisionPhotoCompleted, cancellationToken.WaitHandle };
        int result = WaitHandle.WaitAny(waitHandles, timeoutMs);

        if (result == 0) // 视觉系统响应
        {
            return (VisionProcessResult, VisionResultData);
        }
        else if (result == 1) // 取消
        {
            throw new OperationCanceledException();
        }
        else // 超时
        {
            return (false, "视觉系统响应超时");
        }
    }

    // 添加视觉系统专用的锁对象
    private static object _visionLock = new object();
    #endregion

    #region 枚举定义

    /// <summary>
    /// 视觉请求类型枚举
    /// </summary>
    public enum VisionRequestType
    {
        Tab,
        Pillar,
        Pillar1,
        Pillar2,
        Bottom,
        Side,
        Pickup
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 重置所有信号（系统启动时调用）
    /// </summary>
    public static void ResetAllSignals()
    {
        MaterialReadySignal.Reset();
        LoadingCompletedEvent.Reset();
        AssemblyPhotoCompleted.Reset();
        AssemblyCompleted.Reset();
        AssemblyRequestMaterial.Reset();
        DispensingStationReady.Reset();
        DispensingCompleted.Reset();
        MaterialMoveInSignal.Reset();
        MaterialMoveOutSignal.Reset();
        MaterialInPlaceConfirmed.Reset();
        EmergencyStopSignal.Reset();
        SystemPauseSignal.Reset();
        SystemResumeSignal.Reset();

        CurrentStationIndex = 0;
        PhotoResultData = string.Empty;
        ErrorMessage = string.Empty;
        OperationResult = false;
    }

    /// <summary>
    /// 等待信号（带超时和取消检查）
    /// </summary>
    public static bool WaitForSignal(AutoResetEvent signal, int timeoutMs, CancellationToken cancellationToken = default)
    {
        var waitHandles = new WaitHandle[] { signal, cancellationToken.WaitHandle };
        int result = WaitHandle.WaitAny(waitHandles, timeoutMs);

        if (result == 0) // 信号触发
            return true;
        else if (result == 1) // 取消触发
            throw new OperationCanceledException();
        else // 超时
            return false;
    }

    /// <summary>
    /// 发送信号并设置相关数据
    /// </summary>
    public static void SendSignal(AutoResetEvent signal, int stationIndex = 0, string data = "", bool result = true)
    {
        CurrentStationIndex = stationIndex;
        PhotoResultData = data;
        OperationResult = result;
        signal.Set();
    }

    /// <summary>
    /// 发送错误信号
    /// </summary>
    public static void SendErrorSignal(AutoResetEvent signal, string errorMessage, int stationIndex = 0)
    {
        CurrentStationIndex = stationIndex;
        ErrorMessage = errorMessage;
        OperationResult = false;
        signal.Set();
    }

    #endregion
}
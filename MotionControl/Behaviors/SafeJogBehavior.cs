using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MotionControl.Interfaces;
using MotionControl.ViewModels;

namespace MotionControl.Behaviors
{
    /// <summary>
    /// 安全的 Jog 点动附加属性
    /// 
    /// 安全机制：
    /// 1. 鼠标按下 → 强制捕获鼠标 + 开始 Jog
    /// 2. 鼠标松开（捕获后按钮可收到）→ 立即停止
    /// 3. 失去鼠标捕获 → 立即停止
    /// 4. 窗口失活 → 立即停止
    /// 5. 按钮卸载 → 立即停止
    /// 
    /// 修复：拖动鼠标离开按钮后松开不停止的严重漏洞
    /// 根因：未捕获鼠标时，PreviewMouseUp 仅在按钮范围内触发
    /// 方案：Mouse.Capture(button) 后按钮可接收全局鼠标松开事件
    /// </summary>
    public static class SafeJogBehavior
    {
        public static readonly DependencyProperty AxisIdProperty =
            DependencyProperty.RegisterAttached("AxisId", typeof(int), typeof(SafeJogBehavior),
                new PropertyMetadata(0, OnJogParamsChanged));

        public static int GetAxisId(DependencyObject obj) => (int)obj.GetValue(AxisIdProperty);
        public static void SetAxisId(DependencyObject obj, int value) => obj.SetValue(AxisIdProperty, value);

        public static readonly DependencyProperty DirectionProperty =
            DependencyProperty.RegisterAttached("Direction", typeof(string), typeof(SafeJogBehavior),
                new PropertyMetadata("Positive", OnJogParamsChanged));

        public static string GetDirection(DependencyObject obj) => (string)obj.GetValue(DirectionProperty);
        public static void SetDirection(DependencyObject obj, string value) => obj.SetValue(DirectionProperty, value);

        public static readonly DependencyProperty IsJoggingProperty =
            DependencyProperty.RegisterAttached("IsJogging", typeof(bool), typeof(SafeJogBehavior),
                new FrameworkPropertyMetadata(
                    false,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnAttachedIsJoggingChanged));

        public static bool GetIsJogging(DependencyObject obj) => (bool)obj.GetValue(IsJoggingProperty);
        public static void SetIsJogging(DependencyObject obj, bool value) => obj.SetValue(IsJoggingProperty, value);

        public static readonly DependencyProperty MotionServiceProperty =
            DependencyProperty.RegisterAttached("MotionService", typeof(IMotionService), typeof(SafeJogBehavior),
                new PropertyMetadata(null, OnJogParamsChanged));

        public static IMotionService GetMotionService(DependencyObject obj) => (IMotionService)obj.GetValue(MotionServiceProperty);
        public static void SetMotionService(DependencyObject obj, IMotionService value) => obj.SetValue(MotionServiceProperty, value);

        public static readonly DependencyProperty SafetyZoneMonitorProperty =
            DependencyProperty.RegisterAttached("SafetyZoneMonitor", typeof(ISafetyZoneMonitor), typeof(SafeJogBehavior),
                new PropertyMetadata(null, OnJogParamsChanged));

        public static ISafetyZoneMonitor GetSafetyZoneMonitor(DependencyObject obj) => (ISafetyZoneMonitor)obj.GetValue(SafetyZoneMonitorProperty);
        public static void SetSafetyZoneMonitor(DependencyObject obj, ISafetyZoneMonitor value) => obj.SetValue(SafetyZoneMonitorProperty, value);

        /// <summary>Jog 速度（mm/s），绑定 SingleAxisViewModel.Speed</summary>
        public static readonly DependencyProperty SpeedProperty =
            DependencyProperty.RegisterAttached("Speed", typeof(double), typeof(SafeJogBehavior),
                new PropertyMetadata(10.0));

        public static double GetSpeed(DependencyObject obj) => (double)obj.GetValue(SpeedProperty);
        public static void SetSpeed(DependencyObject obj, double value) => obj.SetValue(SpeedProperty, value);

        private static readonly DependencyProperty JogStateProperty =
            DependencyProperty.RegisterAttached("JogState", typeof(JogState), typeof(SafeJogBehavior),
                new PropertyMetadata(null));

        private static JogState GetJogState(DependencyObject obj) => (JogState)obj.GetValue(JogStateProperty);
        private static void SetJogState(DependencyObject obj, JogState value) => obj.SetValue(JogStateProperty, value);

        /// <summary>
        /// 附加属性变更时确保按钮已注册事件（支持虚拟化回收后重新绑定）
        /// </summary>
        private static void OnJogParamsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Button button)
                EnsureHandlersRegistered(button);
        }

        /// <summary>
        /// 将附加属性 IsJogging 同步到 ViewModel，保证 LED 与状态文本可靠刷新
        /// </summary>
        private static void OnAttachedIsJoggingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Button button || e.NewValue is not bool isJogging)
                return;

            if (button.DataContext is SingleAxisViewModel vm && vm.IsJogging != isJogging)
                vm.IsJogging = isJogging;
        }

        private static void EnsureHandlersRegistered(Button button)
        {
            var state = GetJogState(button);
            if (state?.HandlersRegistered == true)
                return;

            state ??= new JogState();
            SetJogState(button, state);

            if (!state.HandlersRegistered)
            {
                button.PreviewMouseLeftButtonDown += OnPreviewMouseDown;
                button.PreviewMouseLeftButtonUp += OnPreviewMouseUp;
                button.Loaded += OnButtonLoaded;
                button.Unloaded += OnUnloaded;
                state.HandlersRegistered = true;
            }
        }

        private static void OnButtonLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
                EnsureHandlersRegistered(button);
        }

        /// <summary>
        /// 鼠标按下：强制捕获鼠标 + 开始 Jog
        /// </summary>
        private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var button = (Button)sender;
            EnsureHandlersRegistered(button);

            var state = GetJogState(button);
            if (state?.IsJogging == true) return;

            e.Handled = true;

            var motionService = GetMotionService(button);
            if (motionService == null)
            {
                System.Diagnostics.Debug.WriteLine("[SafeJog] MotionService 未绑定，无法点动");
                return;
            }

            int axisId = GetAxisId(button);
            string directionStr = GetDirection(button);
            bool positiveDirection = directionStr.Equals("Positive", StringComparison.OrdinalIgnoreCase);

            // 强制捕获鼠标：捕获后即使鼠标拖出按钮，松开事件仍会路由到按钮
            Mouse.Capture(button, CaptureMode.Element);

            // 监听失去鼠标捕获
            button.LostMouseCapture += OnLostMouseCapture;

            // 监听窗口失活
            var window = Window.GetWindow(button);
            if (window != null)
            {
                window.Deactivated -= OnWindowDeactivated;
                window.Deactivated += OnWindowDeactivated;
                state.WindowRef = window;
            }

            StartJog(button, state, motionService, axisId, positiveDirection);
        }

        /// <summary>
        /// 鼠标松开：立即停止
        /// 由于 Mouse.Capture，即使鼠标在按钮外松开也会触发此事件
        /// </summary>
        private static void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            var button = (Button)sender;
            var state = GetJogState(button);
            if (state?.IsJogging == true)
            {
                e.Handled = true;
                StopJog(button, state);
            }
        }

        /// <summary>
        /// 失去鼠标捕获：立即停止
        /// </summary>
        private static void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            var button = (Button)sender;
            var state = GetJogState(button);
            if (state?.IsJogging == true)
            {
                StopJog(button, state);
            }
        }

        /// <summary>
        /// 窗口失活：停止所有 Jog
        /// </summary>
        private static void OnWindowDeactivated(object sender, EventArgs e)
        {
            StopAllActiveJog();
        }

        /// <summary>
        /// 按钮卸载时清理
        /// </summary>
        private static void OnUnloaded(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var state = GetJogState(button);
            if (state?.IsJogging == true)
                StopJog(button, state);

            // 虚拟化回收时仅停止运动，保留 Handler 以便再次进入可视区时可用
            if (state?.WindowRef != null)
            {
                state.WindowRef.Deactivated -= OnWindowDeactivated;
                state.WindowRef = null;
            }
        }

        // ========== 核心逻辑 ==========

        private static void StartJog(Button button, JogState state, IMotionService motionService, int axisId, bool positiveDirection)
        {
            state.IsJogging = true;
            state.ButtonRef = new WeakReference(button);
            state.MotionService = motionService;
            state.AxisId = axisId;

            if (!button.IsEnabled)
            {
                state.IsJogging = false;
                return;
            }

            // 安全互锁：仅使用轮询缓存位置，禁止 UI 线程同步读卡（避免 Jog 卡顿）
            var safetyMonitor = GetSafetyZoneMonitor(button);
            if (safetyMonitor != null)
            {
                // 使用轮询线程推送的缓存位置，绝不在 UI 线程调用 GetAxisPosition（硬件读取）
                var axisState = motionService.GetAxisState(axisId);
                if (axisState == null)
                {
                    // 缓存不可用时拒绝操作（安全优先）：轮询线程启动后 ~100ms 即可用
                    System.Diagnostics.Debug.WriteLine($"[SafeJog] 轴{axisId} 缓存位置不可用，拒绝Jog（安全优先）");
                    state.IsJogging = false;
                    return;
                }

                double currentPosition = axisState.ActualPosition;
                double jogOffset = safetyMonitor.JogEstimateOffset > 0 ? safetyMonitor.JogEstimateOffset : 10.0;
                double targetPosition = positiveDirection
                    ? currentPosition + jogOffset
                    : currentPosition - jogOffset;

                var (allowed, reason) = safetyMonitor.CheckMoveAllowed(axisId, targetPosition);
                if (!allowed)
                {
                    System.Diagnostics.Debug.WriteLine($"[SafeJog] 安全互锁阻止Jog | 轴:{axisId} | 方向:{(positiveDirection ? "正向" : "负向")} | 原因:{reason}");
                    state.IsJogging = false;
                    return;
                }
            }

            lock (_syncRoot)
            {
                _activeJogStates.Add(state);
            }

            SetIsJogging(button, true);
            SyncIsJoggingToViewModel(button, true);

            double speed = GetSpeed(button);
            if (button.DataContext is SingleAxisViewModel vmSpeed && speed <= 0)
                speed = vmSpeed.Speed;

            // Jog 必须同步立即下发，异步会导致 Stop 先于 Start 执行
            try
            {
                motionService.JogStart(axisId, positiveDirection, speed);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SafeJog] Jog start failed for axis {axisId}: {ex.Message}");
                StopJog(button, state);
            }
        }

        /// <summary>
        /// 停止单个按钮的 Jog
        /// </summary>
        private static void StopJog(Button button, JogState state)
        {
            if (!state.IsJogging) return;

            state.IsJogging = false;

            lock (_syncRoot)
            {
                _activeJogStates.Remove(state);
            }

            button.LostMouseCapture -= OnLostMouseCapture;

            // 释放鼠标捕获
            try { Mouse.Capture(null); } catch { }

            SetIsJogging(button, false);
            SyncIsJoggingToViewModel(button, false);

            EnsureStop(state);
        }

        private static void SyncIsJoggingToViewModel(Button button, bool isJogging)
        {
            if (button.DataContext is SingleAxisViewModel vm)
                vm.IsJogging = isJogging;
        }

        /// <summary>
        /// 停止所有活跃的 Jog（用于全局事件触发）
        /// </summary>
        private static void StopAllActiveJog()
        {
            lock (_syncRoot)
            {
                foreach (var state in _activeJogStates.ToArray())
                {
                    if (!state.IsJogging) continue;

                    var button = state.ButtonRef?.Target as Button;
                    if (button != null)
                    {
                        StopJog(button, state);
                    }
                    else
                    {
                        state.IsJogging = false;
                        EnsureStop(state);
                    }
                }
                _activeJogStates.Clear();
            }
        }

        /// <summary>
        /// 确保轴已停止（双重保障：JogStop + StopAxis）
        /// </summary>
        private static void EnsureStop(JogState state)
        {
            try
            {
                if (state.MotionService != null)
                {
                    state.MotionService.JogStop(state.AxisId);
                    state.MotionService.StopAxis(state.AxisId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SafeJog] Ensure stop failed for axis {state.AxisId}: {ex.Message}");
            }
        }

        private static readonly List<JogState> _activeJogStates = new();
        private static readonly object _syncRoot = new();

        private class JogState
        {
            public bool IsJogging;
            public bool HandlersRegistered;
            public IMotionService MotionService;
            public int AxisId;
            public WeakReference ButtonRef;
            public Window WindowRef;
        }
    }
}

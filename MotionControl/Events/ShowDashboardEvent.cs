// 本文件中的 ShowDashboardEvent / ShowDashboardPayload / DashboardConfirmedEvent
// 已迁移至 StationTasks.Events 命名空间（StationTasks/Events/ShowDashboardEvent.cs）
// 原因：ShowDashboardPayload 引用了 StationTasks.Models.ProcessStep，
//       放在 MotionControl 会造成 StationTasks ↔ MotionControl 循环依赖
// 请使用 using StationTasks.Events; 替代原来的 using MotionControl.Events;

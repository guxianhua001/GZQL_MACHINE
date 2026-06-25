using System;
using Core.Abstraction;
using Prism.Ioc;

namespace MotionControl.Exceptions
{
    /// <summary>
    /// 位置未找到异常：配方引用的位置名在工站位置表中不存在。
    /// 属配置错误（位置被重命名或删除），按致命异常处理 ——
    /// RunStep 的 catch(Exception) 分支会触发 STEP_FATAL_ERROR + AlarmLevel.Serious 报警并中止流程，
    /// 避免轴移动到机械 0 位引发撞机。
    /// </summary>
    public class PositionNotFoundException : Exception
    {
        /// <summary> 引用的位置名（已失效） </summary>
        public string PositionName { get; }

        /// <summary> 引用的轴名 </summary>
        public string AxisName { get; }

        /// <summary> 目标工站标识 </summary>
        public string StationId { get; }

        public PositionNotFoundException(string positionName, string axisName, string stationId)
            : base(BuildMessage(positionName, axisName, stationId))
        {
            PositionName = positionName;
            AxisName = axisName;
            StationId = stationId;
        }

        /// <summary>
        /// 构建多语言异常消息：优先从 ILocalizationService 获取模板并格式化，
        /// 容器未就绪或 key 缺失时回退中文硬编码（遵循 LogMessages.g.cs / BoolToLedColorConverter.cs 先例）。
        /// </summary>
        private static string BuildMessage(string positionName, string axisName, string stationId)
        {
            try
            {
                var loc = ContainerLocator.Container?.Resolve<ILocalizationService>();
                if (loc != null)
                {
                    var formatted = loc.GetResource("Exception_PositionNotFound", positionName, axisName, stationId);
                    // 未找到 key 时 GetResource 返回 "[key]"，此时回退硬编码
                    if (!string.IsNullOrEmpty(formatted) && !formatted.StartsWith("["))
                        return formatted;
                }
            }
            catch
            {
                // 容器未就绪或解析失败，走硬编码回退
            }
            return $"位置 [{positionName}] 的轴 [{axisName}] 在工站 [{stationId}] 中未找到，请检查配方配置";
        }
    }
}

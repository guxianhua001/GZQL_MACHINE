using Core.Abstraction;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Core.Services
{
    public class ParameterService : IParameterService
    {
        public async Task<IEnumerable<ParameterGroup>> LoadParametersAsync()
        {

            return new List<ParameterGroup>
            {
                new ParameterGroup("基本设置", new List<ParameterItem>
                {
                    new StringParameterItem
                    {
                        Name = "TaskName",
                        DisplayName = "任务名称",
                        Description = "设置此任务的名称",
                        Value = "默认任务",
                        DefaultValue = "默认任务",
                        IsRequired = true
                    },
                    new NumberParameterItem
                    {
                        Name = "Timeout",
                        DisplayName = "超时时间(秒)",
                        Description = "任务执行最长等待时间",
                        Value = 60,
                        DefaultValue = 60,
                        MinValue = 10,
                        MaxValue = 300
                    },
                    new BooleanParameterItem
                    {
                        Name = "IsEnabled",
                        DisplayName = "启用任务",
                        Description = "是否启用此任务",
                        Value = true,
                        DefaultValue = true
                    }
                }),
                new ParameterGroup("高级选项", new List<ParameterItem>
                {
                    new EnumParameterItem
                    {
                        Name = "LogLevel",
                        DisplayName = "日志级别",
                        Description = "设置日志记录详细程度",
                        Value = LogLevel.Info,
                        EnumType = typeof(LogLevel), // 添加EnumType属性
                        DefaultValue = LogLevel.Info
                    },
                    new ColorParameterItem
                    {
                        Name = "ThemeColor",
                        DisplayName = "主题颜色",
                        Description = "应用程序主题颜色",
                        Value = Color.FromRgb(103, 58, 183), // MaterialDesign 默认主色调
                        DefaultValue = Color.FromRgb(103, 58, 183)
                    }
                })
            };
        }
        public async Task SaveParametersAsync(IEnumerable<ParameterGroup> parameterGroups)
        {
            // 模拟保存到配置文件的延迟
            await Task.Delay(300);

            // 这里实际应该将参数保存到配置文件或数据库
            // 示例中不实现具体逻辑
        }
        public async Task<IEnumerable<ParameterGroup>> ResetToDefaultsAsync()
        {
            // 重置为默认值时重新加载初始配置
            return await LoadParametersAsync();
        }
    }
}

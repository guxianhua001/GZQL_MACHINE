using Interfaces;
using Interfaces.Mvvm;
using ModuleCore.Models;
using ModuleCore.Services;
using OpenCvSharp.Flann;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using SmarterMotion;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Framework.ViewModels
{
    public class SpeedRatioViewModel : BindableBase, IDisposable
    {
        private readonly IEventAggregator _ea;
        private readonly IDialogService _dialogService;
        private double _velocity;
        private const string configPath = @"Config\HWConfig\hwcfg.xml";
        string rootPath = AppDomain.CurrentDomain.BaseDirectory;
        string fullPath;
        public LoginModel loginModel { get; set; }
        public SpeedRatioViewModel(IDialogService dialogService, IContainerProvider container, IEventAggregator ea)
        {
            _dialogService = dialogService;
            loginModel = container.Resolve<LoginModel>();
            LoadFromXml();
            //InitializeMotionSystem();
        }
        public double Velocity
        {
            get => _velocity;
            set
            {
                if (!ValidatePermission())
                {
                    RaisePropertyChanged(nameof(Velocity)); // 恢复原值
                    ShowError("权限不足，无法操作");
                    return;
                }

                if (SetProperty(ref _velocity, value))
                {
                    UpdateMotionSpeed();
                    SaveToXml();
                }
            }
        }
        private void LoadFromXml()
        {
            try
            {
                fullPath = System.IO.Path.Combine(rootPath, configPath);
                if (!File.Exists(fullPath)) return;

                var doc = XDocument.Load(fullPath);
                string ratio = doc.Descendants("station")
                                     .FirstOrDefault(n => n.Attribute("name")?.Value == "SpeedRatio")
                                     ?.Attribute("MotionSpeedRatio")?.Value;

                if (!string.IsNullOrEmpty(ratio))
                {
                    _velocity = double.Parse(ratio);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("配置文件加载失败", ex);
            }
        }
        private void SaveToXml()
        {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(fullPath);
                // 查找 SpeedRatio 节点
                XmlNode ratioNode = xmlDoc.SelectSingleNode("//SpeedRatio/station[@name='SpeedRatio']");
                if (ratioNode != null)
                {
                    // 更新 MotionSpeedRatio 属性
                    ratioNode.Attributes["MotionSpeedRatio"].Value = Velocity.ToString();
                    xmlDoc.Save(fullPath);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("配置保存失败", ex);
            }
        }
        private void UpdateMotionSpeed()
        {
            var ratio = Velocity / 100;
            foreach (var axis in XDevice.Instance.AxisMap.Values)
            {
                axis.MotionSpeedRatio = ratio;
            }
        }

        private bool ValidatePermission()
        {
            if ((int)loginModel.LoginUser.Authority >= 2)
            {
                return true;
            }
            else
            {
               return false; ;
            }
        }
        private void ShowError(string errMsg)
        {
            _ea.GetEvent<MessageEvent>().Publish(new()
            {
                Target = "errLog",
                Content = errMsg
            });
        }

        private void ShowErrorMessage(string title, Exception ex)
        {
            _dialogService.ShowDialog("ErrorDialog", new DialogParameters {
                { "title", title },
                { "message", ex.Message }
            }, _ => { });

            IMessage.Logger.Error($"{ex.Message}");
        }

        public void Dispose()
        {
            // 清理资源
        }
    }
}

using HSMS;
using Interfaces;
using Interfaces.Services;
using SmarterMotion;
using System;

namespace Stations
{
    public class RegisterAlarm
    {
        private readonly IAlarmService _alarmService;
        private readonly ISecsGemService _secsGemService;
        public event Action<int> UploadAlarmAction;

       public RegisterAlarm(IAlarmService alarmService, ISecsGemService secsGemService)
        {
            _alarmService = alarmService;
            _secsGemService = secsGemService;
        } 
        public void RegisterEvent()
        {
            //注册告警事件
            XAlarmReporter.Instance.OnWarningChanged -= Instance_OnWarningChanged;
            XAlarmReporter.Instance.OnWarningChanged += Instance_OnWarningChanged;
            XAlarmReporter.Instance.OnProductingPage -= Instance_OnProductingPage;
            XAlarmReporter.Instance.OnProductingPage += Instance_OnProductingPage;
            XAlarmReporter.Instance.OnAlarmingPage -= Instance_OnAlarmingPage;
            XAlarmReporter.Instance.OnAlarmingPage += Instance_OnAlarmingPage;
            XAlarmReporter.Instance.OnAlarmCleared -= Instance_OnAlarmCleared;
            XAlarmReporter.Instance.OnAlarmCleared += Instance_OnAlarmCleared;
            XAlarmReporter.Instance.OnSaveAlarmReport -= Instance_OnSaveAlarmReport;
            XAlarmReporter.Instance.OnSaveAlarmReport += Instance_OnSaveAlarmReport;
            XAlarmReporter.Instance.OnAlarmClearedForHive -= Instance_OnAlarmClearedForHive;
            XAlarmReporter.Instance.OnAlarmClearedForHive += Instance_OnAlarmClearedForHive;
            //UploadAlarmAction += HsmsClass.GetInstance().UploadAlarmProcess; //SetUploadAlarm(1);
        }

        private void Instance_OnAlarmClearedForHive(XAlarmEventArgs obj)
        {
            //
            //var msg = "Station" + obj.StationId
            //                    + " => AlarmCode:" + obj.Code
            //                    + " => Category:" + obj.Category
            //                    + " => " + obj.Description;

            //ProjectData.Alarmlist.Add(new AlarmInfo.AlarmStruct(msg, DateTime.Now.ToString()));
            //NLogService.Error(msg);
        }

        private void Instance_OnSaveAlarmReport(XAlarmEventArgs obj)
        {
            //报警
            DateTime dateTime = new DateTime();
            XAlarmEventArgs currentAlarm = new XAlarmEventArgs(obj.IntValue, obj.Code, obj.Category, obj.Description);
            currentAlarm.StationId = obj.StationId;
            currentAlarm.Code = (SysAlarmId(obj.IntValue, obj.IntValue));
            currentAlarm.StartTime = obj.StartTime; 
            currentAlarm.AlarmLevel = obj.AlarmLevel;
            IMessage.Logger.Warn($"obj.IntValue:{obj.IntValue},obj.Code:{obj.Code},obj.Category:{obj.Category},obj.Description:{obj.Description}");
            _alarmService.RaiseNewAlarm(currentAlarm);
            //_secsGemService.UploadAlarmProcess(SysAlarmId(obj.Code, obj.IntValue));
        }
        private int SysAlarmId(int Code, int IntValue)
        {
            if (Code == 1)//ESTOP
            {
                return IntValue;
            }
            else if (Code == 2)//DOOR_OPEN
            {
                return IntValue;
            }
            else if (Code == 6)//AXIS_ALM
            {
                int alarmid = IntValue + 200;
                return IntValue;
            }
            else if (Code == 10)//WAITDI_TIMEOUT
            {
                return IntValue;
            }
            else
            {
                return IntValue;
            }
        }
        private void Instance_OnAlarmCleared(XAlarmEventArgs obj)
        {
            //
        }

        private void Instance_OnAlarmingPage(XAlarmEventArgs obj)
        {

        }

        private void Instance_OnWarningChanged(string warningCode, string warningInfo, int level = 2)
        {
            //
        }

        private void Instance_OnProductingPage(XAlarmEventArgs obj)
        {
            //
        }
    }
}

using System;

namespace Interfaces
{
    [Serializable]
    public class XAlarmEventArgs : EventArgs
    {
        // ID属性
        public int Id { get; set; }
        //显示序号
        public int Index { get; set; }
        private int intvalue;
        private int code;
        private string category;
        private string description;
        public XAlarmEventArgs(int intvalue, int code, string category, string description)
        {
            Id = intvalue; // 使用intvalue设置ID
            this.code = code;
            this.category = category;
            this.description = description;
        }
        public int IntValue
        {
            get { return this.intvalue; }
            set { this.intvalue = value; }
        }
        public int Code
        {
            get { return this.code; }
            set { this.code = value; }
        }

        public int StationId { get; set; }

        public string StartTime { get; set; }

        public string Category
        {
            get { return this.category; }
            set { this.category = value; }
        }

        public string Description
        {
            get { return this.description; }
            set { this.description = value; }
        }

        public TimeSpan Duration { get; set; }
        public int AlarmLevel { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stations
{
    public class StationConstants
    {
        /// <summary>
        /// 主流线上料工位 MainLineLoaderStation
        /// </summary>
        public static class MAIN_ONLOAD
        {
            // 1000 - 1999
            public const int NULL_ACTION = 0;
            public const int IDLE = 1000;
            public const int STOP = 1998;
            public const int ERROR = 1999;

            public const int WAIT_START = 1001;
            public const int WAIT_IN = 1002;
            public const int WAIT_ARRIVAL = 1003;
            public const int ARRIVALED = 1004;
            public const int PICK = 1005;
            public const int POST_PICK = 1006;
            public const int LOAD_ACTION = 1007;
            public const int PRE_Unload = 1008;
            public const int CACHE2_PICK = 1009;
            public const int POST_Unload = 1010;

            public const int PRE_UNLOAD = 1120;
            public const int UNLOAD = 1121;
            public const int POST_UNLOAD = 1122;
            public const int AUTO_FEEDER = 1123;
            public const int MANUNAL_FEEDER = 1124;
            public const int NEXT = 1800;
        }
        /// <summary>
        /// 主流线下料工位 MainLineUnloaderStation
        /// </summary>
        public static class MAIN_UNLOAD
        {
            // 1000 - 1999
            public const int NULL_ACTION = 0;
            public const int IDLE = 1000;
            public const int STOP = 1998;
            public const int ERROR = 1999;

            public const int PRE_UNLOADING = 1110;
            public const int UNLOADING = 1112;
            public const int CACHE1 = 1113;
            public const int POST_UNLOAD = 1114;
            public const int NEXT = 1800;
        }
        /// <summary>
        /// 龙门检测模组左工位 GateLoaderLeftStation
        /// </summary>
        public static class GL_ACTION
        {
            // 1000 - 1999
            public const int NULL_ACTION = 0;
            public const int IDLE = 1000;
            public const int STOP = 1998;
            public const int ERROR = 1999;

            public const int PRE_SYSTEM = 1001;
            public const int CARRIER_CODING = 1002;
            public const int POST_CARRIER_CODING = 1003;
            public const int PRE_PRODUCT_CODING = 1004;
            public const int PRODUCT_UPCODING = 1005;
            public const int PICK = 1006;
            public const int PRODUCT_CODING = 1007;
            public const int POST_PRODUCT_CODING = 1008;
            public const int POST_PICK = 1009;
            public const int PLACE = 1010;
            public const int POST_PLACE = 1011;
            public const int PRE_INSP = 1012;
            public const int CAMERA_INSP = 1013;
            public const int CAMERA_INSP_DONE = 1014;
            public const int POST_INSP = 1015;
            public const int RELEASE_DONE = 1016;
            public const int OUT_STATION = 1017;
            public const int POST_OUTSTATION = 1018;
            public const int NEXT = 1800;
        }

        /// <summary>
        /// 龙门检测模组右工位 GateLoaderRightStation
        /// </summary>
        public static class GR_ACTION
        {
            // 1000 - 1999
            public const int NULL_ACTION = 0;
            public const int IDLE = 1000;
            public const int STOP = 1998;
            public const int ERROR = 1999;

            public const int PRE_DIAL = 1001;
            public const int FIXED = 1002;
            public const int INISP = 1003;
            public const int INISP_DONE = 1004;
            public const int MOVETO_PIN_POS = 1005;
            public const int DIAL_PIN = 1006;
            public const int POST_DIAL = 1007;
            public const int CAM_RECHECK_DONE = 1008;
            public const int NG_ACTION = 1501;
        }
        /// <summary>
        /// 拨针模组2 TransplantModule
        /// </summary>
        public static class PIN2_ACTION
        {
            // 1000 - 1999
            public const int NULL_ACTION = 0;
            public const int IDLE = 1000;
            public const int STOP = 1998;
            public const int ERROR = 1999;

            public const int PRE_DIAL = 1001;
            public const int FIXED = 1002;
            public const int INISP = 1003;
            public const int INISP_DONE = 1004;
            public const int MOVETO_PIN_POS = 1005;
            public const int DIAL_PIN = 1006;
            public const int POST_DIAL = 1007;
            public const int CAM_RECHECK_DONE = 1008;
            public const int NG_ACTION = 1501;

        }
        /// <summary>
        /// 拨针模组3 TransplantModule
        /// </summary>
        public static class PIN3_ACTION
        {
            // 1000 - 1999
            public const int NULL_ACTION = 0;
            public const int IDLE = 1000;
            public const int STOP = 1998;
            public const int ERROR = 1999;

            public const int PRE_DIAL = 1001;
            public const int FIXED = 1002;
            public const int INISP = 1003;
            public const int INISP_DONE = 1004;
            public const int MOVETO_PIN_POS = 1005;
            public const int DIAL_PIN = 1006;
            public const int POST_DIAL = 1007;
            public const int CAM_RECHECK_DONE = 1008;
            public const int NG_ACTION = 1501;

        }
        /// <summary>
        /// 拨针模组4 TransplantModule
        /// </summary>
        public static class PIN4_ACTION
        {
            // 1000 - 1999
            public const int NULL_ACTION = 0;
            public const int IDLE = 1000;
            public const int STOP = 1998;
            public const int ERROR = 1999;

            public const int PRE_DIAL = 1001;
            public const int FIXED = 1002;
            public const int INISP = 1003;
            public const int INISP_DONE = 1004;
            public const int MOVETO_PIN_POS = 1005;
            public const int DIAL_PIN = 1006;
            public const int POST_DIAL = 1007;
            public const int CAM_RECHECK_DONE = 1008;
            public const int NG_ACTION = 1501;

        }

        /// <summary>
        /// 上料模组 OnLoaderModule
        /// </summary>
        public static class ONLOAD_ACTION
        {
            // 1000 - 1999
            public const int NULL_ACTION = 0;
            public const int IDLE = 1000;
            public const int STOP = 1998;
            public const int ERROR = 1999;

            public const int PRE_ONLOADER = 1001;
            public const int ONLOADINGCACHE = 1002;
            public const int WAIT_ARRIVE = 1003;
            public const int CHECK_STACKCOUNT = 1004;
            public const int ONLOADING = 1005;
            public const int PRE_OUT = 1006;
            public const int OUT = 1007;
            public const int POST_OUT = 1008;
            public const int POST_ONLOADER = 1009;
            public const int SAFETY_POSITION = 1501;

        }
        /// <summary>
        /// 下料模组 UnLoaderModule
        /// </summary>
        public static class UNLOAD_ACTION
        {
            // 1000 - 1999
            public const int NULL_ACTION = 0;
            public const int IDLE = 1000;
            public const int STOP = 1998;
            public const int ERROR = 1999;

            public const int PRE_FEED = 1001;
            public const int PRE_UNLOADER = 1002;
            public const int UNLOADING = 1003;
            public const int FEED = 1004;
            public const int POST_FEEDER = 1005;
            public const int PRE_CACHE = 1006;
            public const int CACHE = 1007;
            public const int SAFETY_POSITION = 1501;
        }
    }
}


///* plc地址类(Modbus地址以40000开头) *///

namespace SmarterMotion.Framework.Plc
{
    /// <summary>
    /// 轴设置的ID
    /// </summary>
    public enum AxisNum
    {
        //轴编号
        左侧移栽X轴 = 1,
        左侧吸头Z轴 = 2,
        左侧吸头R轴 = 3,
        顶料Z轴 = 4,
        上料托盘X轴 = 5,
        上料托盘Y轴 = 6,
        翻转R轴 = 7,
        翻转Z轴 = 8,
        翻转Y轴 = 9,
        右侧移栽X轴 = 10, // 0x0000000A
        右侧吸头Z轴 = 11, // 0x0000000B
        右侧吸头R轴 = 12, // 0x0000000C
        放料托盘X轴 = 13, // 0x0000000D
        放料托盘Y轴 = 14, // 0x0000000E
    }
    public enum InoutIO
    {
        翻转手臂真空吸气反馈 = 6,
        翻转手臂真空吹气反馈 = 7,
        中转台真空吸气反馈 = 8,
        中转台真空吹气反馈 = 9
    }
    public enum OutputIO
    {
        上料吸头吸气 = 4,
        上料吸头吹气 = 5,
        下料吸头吸气 = 6,
        下料吸头吹气 = 7,
        翻转手臂真空吸气 = 8,
        翻转手臂真空吹气 = 9,
        中转台真空吸气 = 10,
        中转台真空吹气 = 11,
        日光灯 = 14,
        上料光源控制 = 15,
        翻转光源控制 = 16,
        下料光源控制 = 17,
        上料飞拍光源控制 = 18,
        下料飞拍光源控制 = 19,
        顶针吸气 = 20,
        顶针吹气 = 21
    }
    /// <summary>
    /// 轴地址
    /// </summary>
    public class AxisID : XObject
    {
        public const int Axis0 = 6;    //平移X轴
        public const int Axis1 = 8;    //取料吸头Z轴
        public const int Axis2 = 10;    //取料吸头R轴
        public const int Axis3 = 4;   //顶针Z轴
        public const int Axis4 = 0;   //上料托盘X轴
        public const int Axis5 = 2;   //上料托盘Y轴
        public const int Axis6 = 140;   //翻转R轴
        public const int Axis7 = 144;   //翻转Z轴
        public const int Axis8 = 142;  //翻转Y轴
        public const int Axis9 = 204;    //平移X轴
        public const int Axis10 = 206;    //放料吸头Z轴
        public const int Axis11 = 208;    //放料吸头R轴
        public const int Axis12 = 200;  //放料托盘X轴
        public const int Axis13 = 202;  //放料托盘Y轴
    }

    /// <summary>
    /// 轴参数地址
    /// </summary>
    public class AxisParam
    {
        public const int Axis0_Speed = 82;      //上料移栽轴  定位速度
        public const int Axis0_CmdPos = 18;
        public const int Axis0_ActPos = 96;
        public const int Axis0_ADcc = 112;
        public const int Axis0_HomeVel = 58;
        public const int Axis0_JogVel = 70;

        public const int Axis1_Speed = 84;      //取料吸头Z轴
        public const int Axis1_CmdPos = 20;
        public const int Axis1_ActPos = 98;
        public const int Axis1_ADcc = 114;
        public const int Axis1_HomeVel = 60;
        public const int Axis1_JogVel = 72;

        public const int Axis2_Speed = 86;     //取料吸头R轴(角度)
        public const int Axis2_CmdPos = 22;
        public const int Axis2_ActPos = 100;
        public const int Axis2_ADcc = 116;
        public const int Axis2_HomeVel = 62;
        public const int Axis2_JogVel = 74;

        public const int Axis3_Speed = 88;    //顶针Z轴
        public const int Axis3_CmdPos = 24;
        public const int Axis3_ActPos = 102;
        public const int Axis3_ADcc = 118;
        public const int Axis3_HomeVel = 64;
        public const int Axis3_JogVel = 76;

        public const int Axis4_Speed = 90;    //上料托盘X轴
        public const int Axis4_CmdPos = 26;
        public const int Axis4_ActPos = 104;
        public const int Axis4_ADcc = 120;
        public const int Axis4_HomeVel = 66;
        public const int Axis4_JogVel = 78;

        public const int Axis5_Speed = 92;    //上料托盘Y轴
        public const int Axis5_CmdPos = 28;
        public const int Axis5_ActPos = 106;
        public const int Axis5_ADcc = 122;
        public const int Axis5_HomeVel = 68;
        public const int Axis5_JogVel = 80;

        public const int Axis6_Speed = 182;     //翻转R轴(角度)
        public const int Axis6_CmdPos = 150;
        public const int Axis6_ActPos = 188;
        public const int Axis6_ADcc = 194;
        public const int Axis6_HomeVel = 170;
        public const int Axis6_JogVel = 176;

        public const int Axis7_Speed = 186;    //翻转Z轴
        public const int Axis7_CmdPos = 154;
        public const int Axis7_ActPos = 192;
        public const int Axis7_ADcc = 198;
        public const int Axis7_HomeVel = 174;
        public const int Axis7_JogVel = 180;

        public const int Axis8_Speed = 184;     //翻转Y轴
        public const int Axis8_CmdPos = 152;
        public const int Axis8_ActPos = 190;
        public const int Axis8_ADcc = 196;
        public const int Axis8_HomeVel = 172;
        public const int Axis8_JogVel = 178;

        public const int Axis9_Speed = 270;     //下料平移X轴
        public const int Axis9_CmdPos = 220;
        public const int Axis9_ActPos = 280;
        public const int Axis9_ADcc = 290;
        public const int Axis9_HomeVel = 250;
        public const int Axis9_JogVel = 260;

        public const int Axis10_Speed = 272;    //放料吸头Z轴
        public const int Axis10_CmdPos = 222;
        public const int Axis10_ActPos = 282;
        public const int Axis10_ADcc = 292;
        public const int Axis10_HomeVel = 252;
        public const int Axis10_JogVel = 262;

        public const int Axis11_Speed = 274;   //放料吸头R轴(角度)
        public const int Axis11_CmdPos = 224;
        public const int Axis11_ActPos = 284;
        public const int Axis11_ADcc = 294;
        public const int Axis11_HomeVel = 254;
        public const int Axis11_JogVel = 264;

        public const int Axis12_Speed = 276;     //放料托盘X轴
        public const int Axis12_CmdPos = 226;
        public const int Axis12_ActPos = 286;
        public const int Axis12_ADcc = 296;
        public const int Axis12_HomeVel = 256;
        public const int Axis12_JogVel = 266;

        public const int Axis13_Speed = 278;     //放料托盘Y轴
        public const int Axis13_CmdPos = 228;
        public const int Axis13_ActPos = 288;
        public const int Axis13_ADcc = 298;
        public const int Axis13_HomeVel = 258;
        public const int Axis13_JogVel = 268;
    }

    /// <summary>
    /// 反馈地址
    /// </summary>
    public class Status
    {
        //状态反馈字
        public const int Sts = 350;  //di反馈
        public const int LeftNozzleVacuoAnalogValue = 124;          //上料吸头真空反馈值
        public const int LeftNozzleVacuoPressureAnalogValue = 126;  //上料吸头压力反馈值
        public const int RightNozzleVacuoAnalogValue = 300;           //下料吸头真空反馈值
        public const int RightNozzleVacuoPressureAnalogValue = 302;   //下料吸头压力反馈值
    }

    /// <summary>
    /// 输出地址
    /// </summary>
    public class Command
    {
        //控制输出字
        public const int Cmd = 352;  //do输出
    }

    #region 上料状态字
    public class Input1
    {
        //反馈位(低字)
        public const int bit0 = 0;           //上料位掉料信号
        public const int bit1 = 1;           //申请拍照坐标
        public const int bit2 = 2;           //申请拍照 
        public const int bit3 = 3;           //拍照完成 
        public const int bit4 = 4;           //申请放料坐标
        public const int bit5 = 5;
        public const int bit6 = 6;
        public const int bit7 = 7;
        public const int bit8 = 8;           //翻转位掉料信号
        public const int bit9 = 9;           //申请拍照坐标
        public const int bit10 = 10;         //申请拍照 
        public const int bit11 = 11;         //拍照完成 
        public const int bit12 = 12;         //申请放料坐标
        public const int bit13 = 13;
        public const int bit14 = 14;         //1#吸头真空反馈
        public const int bit15 = 15;         //1#吸头压力反馈
    }

    public class Output1
    {
        //输出位(高字)
        public const int bit0 = 0;   //1#吸头吸气
        public const int bit1 = 1;   //1#吸头吹气
    }
    #endregion

    #region 翻转状态字
    public class Input2
    {
        //反馈位(低字)
        public const int bit0 = 0;           //
        public const int bit1 = 1;           //
        public const int bit2 = 2;           //
        public const int bit3 = 3;           //
        public const int bit4 = 4;           //
        public const int bit5 = 5;
        public const int bit6 = 6;
        public const int bit7 = 7;
        public const int bit8 = 8;
        public const int bit9 = 9;
        public const int bit10 = 10;
        public const int bit11 = 11;
        public const int bit12 = 12;
        public const int bit13 = 13;
        public const int bit14 = 14;         //翻转手臂真空反馈
        public const int bit15 = 15;
    }

    public class Output2
    {
        //输出位(高字)
        public const int bit0 = 0;   //中转台真空吸气(输出)
        public const int bit1 = 1;   //中转台真空吹气(输出)
        public const int bit2 = 2;
        public const int bit3 = 3;
        public const int bit4 = 4;
        public const int bit5 = 5;
        public const int bit6 = 6;
        public const int bit7 = 7;
        public const int bit8 = 8;
        public const int bit9 = 9;
        public const int bit10 = 10;
        public const int bit11 = 11;
        public const int bit12 = 12;
        public const int bit13 = 13;
        public const int bit14 = 14;
        public const int bit15 = 15;
    }
    #endregion

    #region 下料状态字
    public class Input3
    {
        //反馈位(低字)
        public const int bit0 = 0;           //
        public const int bit1 = 1;           //
        public const int bit2 = 2;           //
        public const int bit3 = 3;           //
        public const int bit4 = 4;           //
        public const int bit5 = 5;
        public const int bit6 = 6;
        public const int bit7 = 7;
        public const int bit8 = 8;
        public const int bit9 = 9;
        public const int bit10 = 10;
        public const int bit11 = 11;
        public const int bit12 = 12;
        public const int bit13 = 13;
        public const int bit14 = 14;         //2#吸头真空反馈
        public const int bit15 = 15;         //2#吸头压力反馈
    }

    public class Output3
    {
        //输出位(高字)
        public const int bit0 = 0;   //2#吸头吸气(输出)
        public const int bit1 = 1;   //2#吸头吹气(输出)
        public const int bit2 = 2;
        public const int bit3 = 3;
        public const int bit4 = 4;
        public const int bit5 = 5;
        public const int bit6 = 6;
        public const int bit7 = 7;
        public const int bit8 = 8;
        public const int bit9 = 9;
        public const int bit10 = 10;
        public const int bit11 = 11;
        public const int bit12 = 12;
        public const int bit13 = 13;
        public const int bit14 = 14;
        public const int bit15 = 15;
    }
    #endregion

    #region 设备控制字 32位

    public class Input
    {
        //反馈位(低字)
        public const int start = 1;           //启动按钮反馈
        public const int stop = 2;            //停止按钮反馈
        public const int reset = 3;           //复位按钮反馈 
        public const int eStop = 4;           //急停按钮反馈
        public const int bit4 = 5;            //前门门锁
        public const int bit5 = 6;            //翻转手臂真空吸气反馈
        public const int bit6 = 7;            //翻转手臂真空吹气反馈
        public const int bit7 = 8;            //中转台真空吸气反馈
        public const int bit8 = 9;            //中转台真空吹气反馈
        public const int bit9 = 10;           //移栽轴防撞传感器
        public const int bit10 = 11;          //左门门吸
        public const int bit11 = 12;          //右门门吸
        public const int bit12 = 13;          //速度变更
        public const int bit13 = 14;
        public const int bit14 = 15;
        public const int bit15 = 16;
        public const int bit16 = 17;
        public const int bit17 = 18;
        public const int bit18 = 19;
        public const int bit19 = 20;
        public const int bit20 = 21;
        public const int bit21 = 22;
        public const int bit22 = 23;
        public const int bit23 = 24;
        public const int bit24 = 25;
        public const int bit25 = 26;
        public const int bit26 = 27;
        public const int bit27 = 28;
        public const int bit28 = 29;
        public const int bit29 = 30;
        public const int bit30 = 31;
        public const int bit31 = 32;
    }

    public class OutPut
    {
        //输出位(高字)
        public const int bit0 = 1;   //黄灯
        public const int bit1 = 2;   //绿灯
        public const int bit2 = 3;   //红灯
        public const int bit3 = 4;   //上料吸头吸气
        public const int bit4 = 5;   //上料吸头吹气
        public const int bit5 = 6;   //下料吸头吸气
        public const int bit6 = 7;   //下料吸头吹气
        public const int bit7 = 8;   //翻转手臂真空吸气
        public const int bit8 = 9;   //翻转手臂真空吹气
        public const int bit9 = 10;  //中转台真空吸气
        public const int bit10 = 11; //中转台真空吹气
        public const int bit11 = 12; //上料移栽轴紧急刹车
        public const int bit12 = 13; //下料移栽轴紧急刹车
        public const int bit13 = 14; //日光灯
        public const int bit14 = 15; //备用
        public const int bit15 = 16; //备用
        public const int bit16 = 17; //上料光源控制
        public const int bit17 = 18; //翻转光源控制
        public const int bit18 = 19; //下料光源控制
        public const int bit19 = 20; //上料飞拍光源控制
        public const int bit20 = 21; //下料飞拍光源控制
        public const int bit21 = 22;
        public const int bit22 = 23;
        public const int bit23 = 24;
        public const int bit24 = 25;
        public const int bit25 = 26;
        public const int bit26 = 27;
        public const int bit27 = 28;
        public const int bit28 = 29;
        public const int bit29 = 30;
        public const int bit30 = 31;
        public const int bit31 = 32;
    }

    #endregion

    /// <summary>
    /// 轴状态位定义 反馈占低字 输出占高字
    /// </summary>
    public class XPLC_Define
    {
        //轴状态反馈位
        public const int Axis_IsALM = 0;
        public const int Axis_IsPEL = 1;
        public const int Axis_IsMEL = 2;
        public const int Axis_IsORG = 3;
        public const int Axis_IsSVON = 4;
        public const int Axis_HasMoveDone = 5; //轴运动中(1表示停止，0表示运动中)
        public const int Axis_IsMDN = 6;       //轴定位完成(1表示停止，0表示运动中,用于流程)
        public const int Axis_IsHomeD = 7;     //回零完成

        public const int Axis_IsASTP = 8;      //紧急停止中
        public const int bit9 = 9;
        public const int bit10 = 10;
        public const int bit11 = 11;
        public const int bit12 = 12;
        public const int bit13 = 13;
        public const int bit14 = 14;
        public const int bit15 = 15;

        //轴操作位
        public const int Axis_Enable = 0;
        public const int Axis_Disable = 1;
        public const int Axis_Stop = 2;        //轴暂停
        public const int Axis_MoveJog_P = 3;
        public const int Axis_MoveJog_N = 4;
        public const int bit5 = 5;
        public const int Axis_GoHome = 6;
        public const int Axis_CleanALM = 7;

        public const int Move_EStop = 8;       //飞拍位置更新    轴紧急停止
        public const int Axis_MoveAbs = 9;     //绝对定位
        public const int MoveAbs2 = 10;
        public const int HasEStoped = 11;      //急停信号
        public const int bit28 = 12;
        public const int bit29 = 13;
        public const int bit30 = 14;
        public const int bit31 = 15;
    }

}

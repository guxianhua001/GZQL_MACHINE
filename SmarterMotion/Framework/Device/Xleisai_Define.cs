namespace SmarterMotion
{
    class Xleisai_Define : XObject
    {
        public const int MIO_ALM = 0x01 << 0;//伺服报警
        public const int MIO_PEL = 0x01 << 1;//正硬限位   // if ((IoState & 2) == 2)//检测正限位信号
        public const int MIO_MEL = 0x01 << 2;//负硬限位  //  if ((IoState & 4) == 4)//检测负限位信号
        public const int MIO_EMG = 0x01 << 3;//急停信号
        public const int MIO_ORG = 0x01 << 4;//原点信号  // if ((IoState &16) == 16)//检测原点信号

        public const int MIO_SPEL = 0x01 << 6;//正软限位
        public const int MIO_SMEL = 0x01 << 7;//负软限位
        public const int MIO_INP = 0x01 << 8; //EtherCat版本保留
        public const int MIO_EZ = 0x01 << 9;

        public const int MIO_DSTP = 0x01 << 11;


        public const int MTS_MDN = 0x01 << 0;//正常停止
        public const int MTS_ALM = 0x01 << 1;//ALM 立即停止
        public const int MTS_EMG = 0x01 << 4;//EMG 立即停止
        public const int MTS_OTHER = 0x01 << 15;//其它轴引起的立即停止

        public const int MTS_PEL = 0x01 << 5;//正硬限位立即停止
        public const int MTS_MEL = 0x01 << 6;//负硬限位立即停止

        public const int MTS_SVON = 0x01 << 10;
        //总线轴状态机
        public const int NOT_READY = 0x01 << 0;
        public const int DISABLE = 0x01 << 1;
        public const int READY = 0x01 << 2;
        public const int ON = 0x01 << 3;
        public const int ENABLE = 0x01 << 4;
        public const int QUICK_STOP = 0x01 << 5;
        public const int FAULT_ACTIVE = 0x01 << 6;
        public const int FAULT = 0x01 << 7;

    }
}

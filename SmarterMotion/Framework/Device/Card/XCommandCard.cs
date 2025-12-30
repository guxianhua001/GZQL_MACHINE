
/*----------------------------------------------------------------
* 命名空间: SmarterMotion.Framework.Device.Card
*
* 类 名： XCommandCard
* 功 能： N/A
* 唯一标识：c8ad9d3d-0349-4710-93d5-a47acd8354f6
* 
* 变更日期：2023/8/22 22:49:19
* 作者：szb
* 公司：CYG
*----------------------------------------------------------------*/

using System;

namespace SmarterMotion
{
    public abstract class XCommandCard : XObject
    {

        public int[] DI_Data = new int[200];
        public int[] DO_Data = new int[200];
        public int[] DI_Data_2 = new int[200];
        public int[] DO_Data_2 = new int[200];
        public bool[,] PCIE_8338_DI_Data = new bool[4, 8];
        public bool[,] PCIE_8338_DO_Data = new bool[4, 8];
        public CardStyle CurrentCard { get; set; }
        public ushort CardID { get; set; }
        public virtual int ConnectIp(string ipaddress) { return -1; }
        public virtual int Disconnect() { return -1; }
        public virtual int Commut(int axisId) { return -1; }
        public virtual bool IsConnect { get; set; }
        public virtual int Initial() { return -1; }
        public virtual int Register(int cardId) { return -1; }
        public virtual int Close() { return -1; }
        public virtual int LoadParam(ushort cardnum, string configFn) { return -1; }
        public virtual int CheckEtherCatStatus(ushort cardnum) { return -1; }
        public virtual int Update(int actCardId) { return -1; }

        public virtual int SetDo(int actCardId, int channel, int index, int sts) { return -1; }
        public virtual int GetDo(int actCardId, int channel, int index, ref int sts) { return -1; }
        public virtual int GetDi(int actCardId, int channel, int index, ref int sts) { return -1; }
        public virtual int ReadDi(int actCardId, int channel) { return -1; }
        public virtual int ReadChannel(int actCardId, int channel, out double value) { value = 0; return -1; }
        public virtual int WriteChannel(int actCardId, int channel, double value) { return -1; }

        public virtual int SetServo(int actCardId, int axisId, bool on) { return -1; }
        public virtual int GoHome(int actCardId, int axisId) { return -1; }
        public virtual int SetHomeMode(int actCardId, int axisId, int mode, double dMinVel, double dMaxVel) { return -1; }
        public virtual int SetPosition(int acdCardId, int axisId, double position) { return -1; }
        public virtual int SetPosition(int acdCardId, int axisId, double position, double lead) { return -1; }

        public virtual int ClearALM(int actCardId, int axisId)
        {
            return -1;
        }
        public virtual int ClearPosition(int actCardId, int axisId) { return -1; }

        public virtual int MoveAbs(int actCardId, int axisId, int position, int vel) { return -1; }
        public virtual int MoveRel(int actCardId, int axisId, int distance, int vel) { return -1; }
        public virtual int MoveAbs(int actCardId, int axisId, double position, double vel) { return -1; }
        public virtual int MoveRel(int actCardId, int axisId, double distance, double vel) { return -1; }
        public virtual int MoveLinear(int actCardId, int coordId, ushort[] axes, double[] positions, double vel) { return -1; }
        public virtual bool APS_SetLimitParam(int actCardId, int axisId, int pos) { return false; }
        public virtual int SetAxisJogVel(int actCardId, int axisId, double vel) { return -1; }
        public virtual int SetJogParam(int actCardId, int axisId, double acc, double dec, double vel) { return -1; }
        public virtual int ChangeAxisSpeed(int actCardId, int axisId, double newVel, double taccdcc) { return -1; }
        public virtual int ChangeAxisTargetPosn(int actCardId, int axisId, double newPos) { return -1; }

        public virtual int MoveJog(int actCardId, int axisId, int dir) { return -1; }
        public virtual int MoveJog(int actCardId, int axisId, double vel, int dir) { return -1; }
        public virtual int Stop(int actCardId, int axisId) { return -1; }
        public virtual int EStop(int actCardId, int axisId) { return -1; }
        public virtual int GetEtherCatSts(int actCardId, int axisId, ref int sts) { return -1; }
        public virtual int GetMotionIo(int actCardId, int axisId, ref int sts) { return -1; }
        public virtual int GetMotionSts(int actCardId, int axisId, ref int sts) { return -1; }
        public virtual int GetMotionPos(int actCardId, int axisId, ref int pos) { return -1; }
        public virtual int GetCommandPos(int actCardId, int axisId, ref int pos) { return -1; }

        public virtual int WaitMotionEnd(int axisId, int timeOutMilliseconds) { return -1; }
        public virtual int GetMotionPos(int actCardId, int axisId, ref double pos) { return -1; }
        public virtual int GetCommandPos(int actCardId, int axisId, ref double pos) { return -1; }

        public virtual int SetAxisJerk(int actCardId, int axisId, double jerkvalue) { return -1; }
        public virtual int SetAxisAccAndDec(int actCardId, int axisId, double acc, double dec) { return -1; }
        public virtual int SetAxisAccAndDec(int actCardId, int axisId, double dMinVel, double dMaxVel, double acc, double dec, double dStopVel) { return -1; }
        public virtual int SetAxisAccAndDec(int actCardId, int axisId, double dMinVel, double dMaxVel, double acc, double dec, double dStopVel, double sPara) { return -1; }
        public virtual int SetStopDec(int actCardId, int axisId, double lead, double dec) { return -1; }
        public virtual int ChangeAxisSpeed(int actCardId, int axisId, int vel) { return -1; }

        public virtual int MoveLineAbs(int actCardId, int coordId, int[] axisId, double[] pos, double vel) { return -1; }
        public virtual int MoveLineRel(int[] axisId, double[] pos, double vel) { return -1; }
        public virtual int MoveArcAbs(int[] axisId, double[] center, double angle, double vel) { return -1; }
        public virtual int MoveArcAbs(int[] axisId, double[] center, double[] end, short dir, double vel) { return -1; }
        public virtual int MoveArcRel(int[] axisId, double[] center, double angle, double vel) { return -1; }
        public virtual int MoveArcRel(int[] axisId, double[] center, double[] end, short dir, double vel) { return -1; }

        public virtual int APS_pt_start(int actCardId, int ptbId) { return -1; }

        public virtual int CheckMoveDone(ushort actCardId, ushort axisId) { return -1; }

        public virtual int CheckMoveDone(ushort actCardId, ushort axisId, bool bWait = true) { return -1; }

        public virtual int CheckHomeDone(ushort actCardId, ushort axisId) { return -1; }


        public virtual int SetDAfunction(ushort actCardId, ushort enable) { return -1; }


        public virtual int GetADinput(ushort actCardId, ushort channel, ref double Value) { return -1; }

        public virtual int SetDAoutput(ushort actCardId, ushort channel, double value) { return -1; }

        public virtual int SetConnectStata(ushort CardId, ushort NodeNum, ushort Stata) { return -1; }

        public virtual int Write_rxpdo(int CardId, int portNum, int address, int dataLen, int value) { return -1; }
        public virtual double GetEncPos(int actCardId, int axisId) { return -1; }
        public virtual int ClearEncoder(int actCardId, int channel) { return -1; }
        public virtual int SetExtraEncoder(int actCardId, int encNum, int pos) { return -1; }

        public virtual int SetTriggerPosition(int nAxisID, int myhcmp, int mycmp_source, int mycmp_logic, int mytime, double[] adPositionArray) { return -1; }

        public virtual int SetElmoDriveHomeSearch(int actCardId, int axisId, int mode, double dMinVel, double dMaxVel, double acc, double dec) { return -1; }

        public virtual int SoftReset(ushort cardnum) { return -1; }
    }
    public enum CardStyle
    {
        ADLINK,
        ACS,
        Advantech,
        Leisai
    }
}

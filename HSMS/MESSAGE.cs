using System;
using System.Collections.Generic;
using System.Text;
using CHSMS;
using System.Collections;
///
/// Remove this if using message Dll file
///
namespace SECS
{

    class MSG_S1F0 : StreamFunction
    {
        public MSG_S1F0()
        {
            Stream = 1;		    //stream id
            Function = 0;		//function id
            NeedReply = false;	//reply bit
            this.Description = "Abort Transaction";
        }
        //only header
        public override void ToBuffer()
        {

        }

        //only header
        public override void FromBuffer()
        {
        }

    }

    class MSG_S1F1 : StreamFunction
    {
        public MSG_S1F1()
        {
            Stream = 1;		    //stream id
            Function = 1;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Are You There Request";
        }

        //only header
        public override void ToBuffer()
        {
        }

        //only header
        public override void FromBuffer()
        {
        }

    }
    class MSG_S1F2 : StreamFunction
    {
        private byte L_count = 2;
        public string MDLN = "";
        public string SOFTREV = "";

        public MSG_S1F2()
        {
            Stream = 1;		    //stream id
            Function = 2;		//function id
            NeedReply = false;	//reply bit
            this.Description = "On Line Data";
        }
        //only header
        public override void ToBuffer()
        {
            SETS_LIST(L_count); //List
            SETS_ASC2(MDLN);
            SETS_ASC2(SOFTREV);
        }
        //only header
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            GETS_ASC2(ref MDLN);
            GETS_ASC2(ref SOFTREV);
        }
    }

    class MSG_S1F3 : StreamFunction
    {
        public byte L_count = 1;
        public uint SVID = 0;
        public ArrayList arrSVID = new ArrayList();

        public MSG_S1F3()
        {
            Stream = 1;		    //stream id
            Function = 3;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Selected Equipment Status Request";
        }

        //only header
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            for (int i = 0; i < L_count; i++)
            {
                string str = arrSVID[i].ToString();
                SETS_ASC2(str);
                //uint str = (uint)arrSVID[i];
                //SETS_4bUI(str);
            }
        }

        //only header
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            for (int k1 = 0; k1 < L_count; k1++)
            {
                GETS_4bUI(ref SVID);
                arrSVID.Add(SVID);
            }
        }

    }

    class MSG_S1F4 : StreamFunction
    {
        public byte L_count = 1;
        public uint SV = 0;
        public ArrayList arrSV = new ArrayList();

        public MSG_S1F4()
        {
            Stream = 1;		    //stream id
            Function = 4;		//function id
            NeedReply = false;	//reply bit
            this.Description = "Selected Equipment Status Data";
        }

        //only header
        public override void ToBuffer()
        {
            SETS_LIST(L_count);

            for (int i = 0; i < L_count; i++)
            {
                if (i < arrSV.Count)
                {
                    if (typeof(byte) == arrSV[i].GetType())
                    {
                        SETS_BINA((byte)arrSV[i]);
                    }
                    else if (typeof(string) == arrSV[i].GetType())
                    {
                        SETS_ASC2((string)arrSV[i]);
                    }
                }
            }
        }

        //only header
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            GETS_4bUI(ref SV);
        }

    }

    class MSG_S1F11 : StreamFunction
    {
        public byte L_count = 1;
        public uint SVID = 0;
        public ArrayList arrSVID = new ArrayList();

        public MSG_S1F11()
        {
            Stream = 1;		    //stream id
            Function = 11;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Status Variable Namelist Request";
        }

        //only header
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            for (int i = 0; i < L_count; i++)
            {
                uint str = (uint)arrSVID[i];
                SETS_4bUI(str);
            }
        }

        //only header
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            for (int k1 = 0; k1 < L_count; k1++)
            {
                GETS_4bUI(ref SVID);
                arrSVID.Add(SVID);
            }
        }

    }

    class MSG_S1F12 : StreamFunction
    {
        public byte L_count = 1;
        public byte L_count1 = 3;
        public uint SVID = 0;
        public string SVNAME = "";
        public string UNITS = "";
        public ArrayList arrSVID = new ArrayList();
        public ArrayList arrSVNAME = new ArrayList();
        public ArrayList arrUNITS = new ArrayList();
        public MSG_S1F12()
        {
            Stream = 1;		    //stream id
            Function = 12;		//function id
            NeedReply = false;	//reply bit
            this.Description = "Status Variable Namelist Reply";
        }

        //only header
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            for (int i = 0; i < L_count; i++)
            {
                SETS_LIST(L_count1); //List    
                uint str = Convert.ToUInt32(arrSVID[i]);
                SETS_4bUI(str);
                if (i < arrSVNAME.Count)
                {
                    string str1 = (string)arrSVNAME[i];
                    SETS_ASC2(str1);
                    SETS_ASC2("");
                }
                
            }
        }

        //only header
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            GETS_LIST(ref L_count1);
            GETS_4bUI(ref SVID);
            GETS_ASC2(ref SVNAME);
            GETS_ASC2(ref UNITS);
        }

    }

    class MSG_S1F13 : StreamFunction
    {
        public byte L_count1 = 2;

        //Message item definition
        public String MDLN = " ";
        public String SOFTREV = " ";
        //public byte  SOFTREV =0;
        public MSG_S1F13()
        {
            Stream = 1;		    //stream id
            Function = 13;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Establish Communications Request";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(L_count1);
            SETS_ASC2(MDLN);
           // SETS_BINA(SOFTREV);
            SETS_ASC2(SOFTREV);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count1);
            GETS_ASC2(ref MDLN);
           // GETS_BINA(ref SOFTREV);
            GETS_ASC2(ref SOFTREV);
        }
    }

    class MSG_S1F14 : StreamFunction
    {
        private byte L_count1 = 2;
        private byte L_count2 = 2;
        public byte COMMACK;
        public String MDLN = " ";
        public String SOFTREV = " ";

        byte[] arrbyte = new byte[1];

        public MSG_S1F14()
        {
            Stream = 1; //stream id
            Function = 14; //function id
            NeedReply = false; //reply bit
            this.Description = "Establish Communications Request Acknowledge";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(L_count1); //List
            SETS_BINA(COMMACK);
            SETS_LIST(L_count2);
            SETS_ASC2(MDLN);
            SETS_ASC2(SOFTREV);
            
        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count1);
            GETS_BINA(ref arrbyte);
            COMMACK = arrbyte[0];
            GETS_LIST(ref L_count2);
            GETS_ASC2(ref MDLN);
            GETS_ASC2(ref SOFTREV);
        }
    }

    class MSG_S1F15 : StreamFunction
    {
        public MSG_S1F15()
        {
            Stream = 1;		    //stream id
            Function = 15;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Request OFF-LINE";
        }

        //only header
        public override void ToBuffer()
        {
        }

        //only header
        public override void FromBuffer()
        {
        }

    }

    class MSG_S1F16 : StreamFunction
    {
        public byte OFLACK = 0;

        public MSG_S1F16()
        {
            Stream = 1;		    //stream id
            Function = 16;		//function id
            NeedReply = false;	//reply bit
            this.Description = "OFF-LINE Acknowledge";
        }

        //only header
        public override void ToBuffer()
        {
            SETS_BINA(OFLACK);
        }

        //only header
        public override void FromBuffer()
        {
        }

    }

    class MSG_S1F17 : StreamFunction
    {
        public MSG_S1F17()
        {
            Stream = 1;		    //stream id
            Function = 17;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Request ON-LINE";
        }

        //only header
        public override void ToBuffer()
        {
        }

        //only header
        public override void FromBuffer()
        {
        }

    }

    class MSG_S1F18 : StreamFunction
    {
        public byte ONLACK;

        public MSG_S1F18()
        {
            Stream = 1;		    //stream id
            Function = 18;		//function id
            NeedReply = false;	//reply bit
            this.Description = "ON-LINE Acknowledge";
        }

        //only header
        public override void ToBuffer()
        {
            SETS_BINA(ONLACK);
        }

        //only header
        public override void FromBuffer()
        {
        }

    }

    class MSG_S2F13 : StreamFunction
    {
        public byte L_count = 1;
        public uint ECID = 0;
        public ArrayList arrECID = new ArrayList();

        public MSG_S2F13()
        {
            Stream = 2;		    //stream id
            Function = 13;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Selected Equipment Status Request";
        }

        //only header
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            for (int i = 0; i < L_count; i++)
            {
                uint str = (uint)arrECID[i];
                SETS_4bUI(str);
                //SETS_2bUI(str);
            }
        }

        //only header
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            for (int k1 = 0; k1 < L_count; k1++)
            {
                GETS_4bUI(ref ECID);
                arrECID.Add(ECID);
            }
        }
    }

    class MSG_S2F14 : StreamFunction
    {
        public byte L_count = 1;
        public string ECV = "";
        public ArrayList arrECV = new ArrayList();

        public MSG_S2F14()
        {
            Stream = 2;		    //stream id
            Function = 14;		//function id
            NeedReply = false ;	//reply bit
            this.Description = "Equipment Constant Date";
        }

        //only header
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            for (int i = 0; i < L_count; i++)
            {
                string str = "";
                if (arrECV.Count > 0 )
                    str = (string)arrECV[i];
                SETS_ASC2(str);
                //SETS_4bUI(str);
            }
        }

        //only header
        public override void FromBuffer()
        {
            //GETS_LIST(ref L_count);
            //for (int k1 = 0; k1 < L_count; k1++)
            //{
            //    //GETS_4bUI(ref ECV);
            //    GETS_ASC2(ref ECV);
            //    arrECID.Add(ECV);
            //}
        }
    }

    class MSG_S2F15 : StreamFunction
    {
        public byte L_count = 1;
        public byte L_count1 = 2;
        public uint ECID = 0;
        public string ECV = "";
        public ArrayList arrECID = new ArrayList();
        public ArrayList arrECV = new ArrayList();

        public MSG_S2F15()
        {
            Stream = 2;		    //stream id
            Function = 15;		//function id
            NeedReply = true;	//reply bit
            this.Description = "New Equipment Constant Send";
        }

        //only header
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            for (int i = 0; i < L_count; i++)
            {
                SETS_LIST(L_count1);
                uint str = (uint)arrECID[i];
                SETS_4bUI(str);
                string st = (string)arrECV[i];
                SETS_ASC2(st);
            }
        }

        //only header
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            for (int k1 = 0; k1 < L_count; k1++)
            {
                GETS_LIST(ref  L_count1);
                GETS_4bUI(ref ECID);
                arrECID.Add(ECID);
                GETS_ASC2(ref ECV);
                arrECV.Add(ECV);
            }
        }

    }

    class MSG_S2F16 : StreamFunction
    {
        public byte L_count = 1;
        public byte ECV = 0;
        public ArrayList arrECV = new ArrayList();

        public MSG_S2F16()
        {
            Stream = 2;		    //stream id
            Function = 16;		//function id
            NeedReply = false;	//reply bit
            this.Description = "New Equipment Constant Acknowledge";
        }

        //only header
        public override void ToBuffer()
        {
            SETS_BINA(ECV);
        }

        //only header
        public override void FromBuffer()
        {
           
        }
    }


    class MSG_S2F17 : StreamFunction
    {
        public MSG_S2F17()
        {
            Stream = 2;		    //stream id
            Function = 17;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Date and Time Request";
        }

        //only body
        public override void ToBuffer()
        {
        }

        //only body
        public override void FromBuffer()
        {
        }
    }

    class MSG_S2F18 : StreamFunction
    {
        //private byte L_count1 = 1;
        public String DATETIME = "";

        public MSG_S2F18()
        {
            Stream = 2; //stream id
            Function = 18; //function id
            NeedReply = false; //reply bit
            this.Description = "Date and Time Data";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_ASC2(DATETIME);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_ASC2(ref DATETIME);
        }
    }


    class MSG_S2F21 : StreamFunction
    {
        public String RCMD = "";
        public MSG_S2F21()
        {
            Stream = 2;		    //stream id
            Function = 21;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Remote Command Send";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_ASC2(RCMD);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_ASC2(ref RCMD);
        }
    }

    class MSG_S2F22 : StreamFunction
    {
        //private byte L_count1 = 1;
        
        public ushort CMDA = 0;
        public MSG_S2F22()
        {
            Stream = 2; //stream id
            Function = 22; //function id
            NeedReply = false; //reply bit
            this.Description = "Remote Command Acknowledge";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_1bUI(CMDA);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_1bUI(ref CMDA);
        }
    }

    class MSG_S2F23 : StreamFunction
    {
        public byte L_count = 5;
        public uint TRID = 0;
        public String DSPER = "";
        public uint REPGSZ = 0;
        public byte L_count1 = 0;
        public uint SVID = 0;

        public MSG_S2F23()
        {
            Stream = 2;		    //stream id
            Function = 23;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Trace Initialize Send";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            SETS_4bUI(TRID);
            SETS_ASC2(DSPER);
            SETS_4bUI(REPGSZ);
            SETS_LIST(L_count1);
            for (int k1 = 0; k1 < L_count1; k1++)
                SETS_4bUI(SVID);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            GETS_4bUI(ref TRID);
            GETS_ASC2(ref DSPER);
            GETS_4bUI(ref REPGSZ);
            GETS_LIST(ref L_count1);
            for (int k1 = 0; k1 < L_count1; k1++)
                GETS_4bUI(ref SVID);
        }
    }

    class MSG_S2F24 : StreamFunction
    {
        //private byte L_count1 = 1;

        public byte TIAACK = 0;
        public MSG_S2F24()
        {
            Stream = 2; //stream id
            Function = 24; //function id
            NeedReply = false; //reply bit
            this.Description = "Trace Initialize Acknowledge";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_BINA(TIAACK);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_BINA(ref TIAACK);
        }
    }

    class MSG_S2F29 : StreamFunction
    {
        public byte L_count = 1;
        public uint ECID = 0;
        public ArrayList arrECID = new ArrayList();

        public MSG_S2F29()
        {
            Stream = 2;		    //stream id
            Function = 29;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Equipment Constant Namelist Request";
        }

        //only header
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            for (int i = 0; i < L_count; i++)
            {
                uint str = (uint)arrECID[i];
                SETS_4bUI(str);
            }
        }

        //only header
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            for (int k1 = 0; k1 < L_count; k1++)
            {
                GETS_4bUI(ref ECID);
                arrECID.Add(ECID);
            }
        }
    }

    class MSG_S2F30 : StreamFunction
    {
        public byte L_count = 1;
        public byte L_count1 = 6;
        public uint ECID = 0;
        public string ECNAME = "";
        public uint ECMIN = 0;
        public uint ECMAX = 0;
        public string ECDEF = "";
        public string UNITS = "";
        public ArrayList arrECID = new ArrayList();
        public ArrayList arrECNAME = new ArrayList();
        public ArrayList arrECMIN = new ArrayList();
        public ArrayList arrECMAX = new ArrayList();
        public ArrayList arrECDEF = new ArrayList();
        public ArrayList arrUNITS = new ArrayList();
        public MSG_S2F30()
        {
            Stream = 2;		    //stream id
            Function = 30;		//function id
            NeedReply = false;	//reply bit
            this.Description = "Equipment Constant Namelist";
        }

        //only header
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            for (int i = 0; i < L_count; i++)
            {
                SETS_LIST(L_count1); //List    

                uint str = (uint)arrECID[i];
                SETS_4bUI(str);

                if (i < arrECNAME.Count)
                {
                    string str1 = (string)arrECNAME[i];
                    SETS_ASC2(str1);
                }
                else
                {
                    SETS_ASC2(ECNAME);
                }

                if (i < arrECMIN.Count)
                {
                    uint str3 = (uint)arrECMIN[i];
                    SETS_4bUI(str3);
                }
                else
                {
                    SETS_4bUI(ECMIN);
                }

                if (i < arrECMAX.Count)
                {
                    uint str4 = (uint)arrECMAX[i];
                    SETS_4bUI(str4);
                }
                else
                {
                    SETS_4bUI(ECMAX);
                }

                if (i < arrECDEF.Count)
                {
                    string str5 = (string)arrECDEF[i];
                    SETS_ASC2(str5);
                }
                else
                {
                    SETS_ASC2(ECDEF);
                }

                if (i < arrUNITS.Count)
                {
                    string str2 = (string)arrUNITS[i];
                    SETS_ASC2(str2);
                }
                else
                {
                    SETS_ASC2(UNITS);
                }
            }
        }

        //only header
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            GETS_LIST(ref L_count1);
            GETS_4bUI(ref ECID);
            GETS_ASC2(ref ECNAME);
            GETS_4bUI(ref ECMIN);
            GETS_4bUI(ref ECMAX);
            GETS_ASC2(ref ECDEF);
            GETS_ASC2(ref UNITS);
        }
    }

    class MSG_S2F31 : StreamFunction
    {
        public String DATETIME = "";

        public MSG_S2F31()
        {
            Stream = 2; //stream id
            Function = 31; //function id
            NeedReply = true; //reply bit
            this.Description = "Date and Time Set Request";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_ASC2(DATETIME);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_ASC2(ref DATETIME);
        }
    }

    class MSG_S2F32 : StreamFunction
    {
        public byte TIACK = 0;
        //public String DRACK = " ";

        public MSG_S2F32()
        {
            Stream = 2; //stream id
            Function = 32; //function id
            NeedReply = false; //reply bit
            this.Description = "Date and Time Set Acknowledge";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_BINA(TIACK);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_BINA(ref  TIACK);
        }
    }

    class MSG_S2F33 : StreamFunction
    {
        public byte L_count = 2;
        public byte L_count1 = 1;
        public byte L_count2 = 2;
        public byte L_count3 = 1;
        public uint DATAID = 0;
        public uint RPTID = 1;
        public uint VID = 1;
        public ArrayList arrRPTID = new ArrayList();
        public ArrayList arrVID = new ArrayList();
        public ArrayList arrL_count3 = new ArrayList();

        public MSG_S2F33()
        {
            Stream = 2;		    //stream id
            Function = 33;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Define Report";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            SETS_4bUI(DATAID);
            int cou3 = 0;

            SETS_LIST(L_count1); //List    

            for (int k1 = 0; k1 < arrL_count3.Count; k1++)
            {
                SETS_LIST(L_count2); //List  

                uint str = (uint)arrRPTID[k1];
                SETS_4bUI(str);

                byte str1 = (byte)Int32.Parse(arrL_count3[k1].ToString());
                SETS_LIST(str1);
                for (int k2 = 0; k2 < Int32.Parse(arrL_count3[k1].ToString()); k2++)
                {
                    uint str2 = (uint)arrVID[cou3];
                    SETS_4bUI(str2);
                    cou3 = cou3 + 1;
                }
            }
        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            GETS_4bUI(ref  DATAID);
            GETS_LIST(ref L_count1);
            for (int k1 = 0; k1 < L_count1; k1++)
            {
                GETS_LIST(ref L_count2);
                GETS_4bUI(ref  RPTID);
                arrRPTID.Add(RPTID);
                GETS_LIST(ref L_count3);
                arrL_count3.Add(L_count3);
                for (int k3 = 0; k3 < L_count3; k3++)
                {
                    GETS_4bUI(ref  VID);
                    arrVID.Add(VID);
                }
            }
        }
    }

    class MSG_S2F34 : StreamFunction
    {
        public byte DRACK = 0;
        //public String DRACK = " ";

        public MSG_S2F34()
        {
            Stream = 2; //stream id
            Function = 34; //function id
            NeedReply = false; //reply bit
            this.Description = "Define Report Acknowledge";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_BINA(DRACK);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_BINA(ref  DRACK);
        }
    }

    class MSG_S2F35 : StreamFunction
    {
        public byte L_count = 2;
        public byte L_count1 = 1;
        public byte L_count2 = 2;
        public byte L_count3 = 0;
        public uint DATAID = 0;
        public uint CEID = 1;
        public uint RPTID = 1;
        public ArrayList arrCEID = new ArrayList();
        public ArrayList arrRPTID = new ArrayList();
        public ArrayList arrL_count2 = new ArrayList();
        public ArrayList arrL_count3 = new ArrayList();

        public MSG_S2F35()
        {
            Stream = 2;		    //stream id
            Function = 35;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Link Event Report";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            SETS_4bUI(DATAID);
            int cou3 = 0;

            SETS_LIST(L_count1); //List    

            for (int k1 = 0; k1 < arrL_count3.Count; k1++)
            {
                SETS_LIST(L_count2); //List  

                uint str = (uint)arrCEID[k1];
                SETS_4bUI(str);

                byte str1 = (byte)Int32.Parse(arrL_count3[k1].ToString());
                SETS_LIST(str1);
                for (int k2 = 0; k2 < Int32.Parse(arrL_count3[k1].ToString()); k2++)
                {
                    uint str2 = (uint)arrRPTID[cou3];
                    //for (int k3 = 0; k3 < arrRPTID.Count ; k3++)
                    //{
                    //  uint str2 = (uint)arrRPTID[k3];
                    SETS_4bUI(str2);
                    cou3 = cou3 + 1;
                    //}

                }
            }
        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            GETS_4bUI(ref  DATAID);
            GETS_LIST(ref L_count1);
            for (int k1 = 0; k1 < L_count1; k1++)
            {
                GETS_LIST(ref L_count2);
                arrL_count2.Add(L_count2);
                GETS_4bUI(ref  CEID);
                arrCEID.Add(CEID);
                GETS_LIST(ref L_count3);
                arrL_count3.Add(L_count3);
                for (int k3 = 0; k3 < L_count3; k3++)
                {
                    GETS_4bUI(ref  RPTID);
                    arrRPTID.Add(RPTID);
                }
            }
        }
    }

    class MSG_S2F36 : StreamFunction
    {
        public byte LRACK = 0;
        //public String DRACK = " ";

        public MSG_S2F36()
        {
            Stream = 2; //stream id
            Function = 36; //function id
            NeedReply = false; //reply bit
            this.Description = "Link Event Report Acknowledge";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_BINA(LRACK);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_BINA(ref  LRACK);
        }
    }

    class MSG_S2F37 : StreamFunction
    {
        public byte L_count = 2;
        public byte L_count1 = 0;
        public uint CEID = 0;
        public Boolean CEED = false;
        public ArrayList arrCEID = new ArrayList();

        public MSG_S2F37()
        {
            Stream = 2;		    //stream id
            Function = 37;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Enable/Disable Event Report";
        }

        //only header
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            SETS_BOOL(CEED);
            SETS_LIST(L_count1);
            for (int i = 0; i < L_count1; i++)
            {
                uint str = (uint)arrCEID[i];
                SETS_4bUI(str);
            }
        }

        //only header
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            GETS_BOOL(ref CEED);
            GETS_LIST(ref L_count1);
            for (int k1 = 0; k1 < L_count1; k1++)
            {
                GETS_4bUI(ref CEID);
                arrCEID.Add(CEID);
            }
        }
    }

    class MSG_S2F38 : StreamFunction
    {
        public byte ERACK = 0;
        //public String DRACK = " ";

        public MSG_S2F38()
        {
            Stream = 2; //stream id
            Function = 38; //function id
            NeedReply = false; //reply bit
            this.Description = "Enable/Disable Event Report Acknowledge";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_BINA(ERACK);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_BINA(ref  ERACK);
        }
    }

    class MSG_S2F41 : StreamFunction
    {
        public byte L_count = 2;
        public byte L_count1 = 0;
        public byte L_count2 = 2;
        public string RCMD = "";
        public string CPNAME = "";
        public string CPVAL = "";
        public ArrayList arrL_count1 = new ArrayList();
        public ArrayList arrCPNAME = new ArrayList();
        public ArrayList arrCPVAL = new ArrayList();
        // public ushort[] ALID = new ushort[1];
        public MSG_S2F41()
        {
            Stream = 2; //stream id
            Function = 41; //function id
            NeedReply = true; //reply bit
            this.Description = "Host Command Send";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(L_count); //List
            SETS_ASC2(RCMD);
            SETS_LIST(L_count1);
            if (L_count1 != 0)
            {
                SETS_LIST(L_count2);
                SETS_ASC2(CPNAME);
                SETS_ASC2(CPVAL);
            }
            
        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            GETS_ASC2(ref RCMD);
            GETS_LIST(ref L_count1);
            for (int k1 = 0; k1 < L_count1; k1++)
            {
                GETS_LIST(ref  L_count2);
                GETS_ASC2(ref CPNAME);
                arrCPNAME.Add(CPNAME);
                GETS_ASC2(ref CPVAL);
                arrCPVAL.Add(CPVAL);
            }
        }
    }

    class MSG_S2F42 : StreamFunction
    {
        public byte L_count = 2;
        public byte L_count1 = 1;
        public byte L_count2 = 2;
        public byte HCACK = 0;
        public string CPNAME = "";
        public byte CPACK = 0;
        public ArrayList arrL_count1 = new ArrayList();
        public ArrayList arrCPNAME = new ArrayList();
        public ArrayList arrCPACK = new ArrayList();
        public MSG_S2F42()
        {
            Stream = 2; //stream id
            Function = 42; //function id
            NeedReply = false; //reply bit
            this.Description = "Host Command Acknowledge";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            SETS_BINA(HCACK);
            SETS_LIST(L_count1);
            for (int k1 = 0; k1 < L_count1; k1++)
            {
                SETS_LIST(L_count2);
                string str2 = (string)arrCPNAME[k1];
                SETS_ASC2(str2);
                byte str = (byte)arrCPACK[k1];
                SETS_BINA(str);
            }
        }

        //only body
        public override void FromBuffer()
        {
           
        }
    }

    class MSG_S5F1 : StreamFunction
    {
        private byte L_count1 = 3;
        public byte ALCD = 0;
        public uint ALID = 0;
        public string ALTX = "";

        public MSG_S5F1()
        {
            Stream = 5; //stream id
            Function = 1; //function id
            NeedReply = true; //reply bit
            this.Description = "Alarm Report Send";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(L_count1); //List
            SETS_BINA(ALCD);
            SETS_4bUI(ALID);
            SETS_ASC2(ALTX);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count1);
            GETS_BINA(ref ALCD);
            GETS_4bUI(ref ALID);
            GETS_ASC2(ref ALTX);

        }
    }

    class MSG_S5F2 : StreamFunction
    {
        public byte ACKC5 = 0;
        //public String DRACK = " ";

        public MSG_S5F2()
        {
            Stream = 5; //stream id
            Function = 2; //function id
            NeedReply = false; //reply bit
            this.Description = "Alarm Report Acknowledge";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_BINA(ACKC5);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_BINA(ref  ACKC5);
        }
    }

    class MSG_S5F3 : StreamFunction
    {
        private byte L_count1 = 2;
        public byte ALED = 0;
        public uint ALID = 0;
        //public string ALTX = "";

        public MSG_S5F3()
        {
            Stream = 5; //stream id
            Function = 3; //function id
            NeedReply = true; //reply bit
            this.Description = "Enable/Disable Alarm Send";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(L_count1); //List
            SETS_BINA(ALED);
            SETS_4bUI(ALID);
            //SETS_ASC2(ALTX.PadRight(80));
        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count1);
            GETS_BINA(ref ALED);
            GETS_4bUI(ref ALID);
            //GETS_ASC2(ref ALTX);
        }
    }

    class MSG_S5F4 : StreamFunction
    {
        public byte ACKC5 = 0;
        //public String DRACK = " ";

        public MSG_S5F4()
        {
            Stream = 5; //stream id
            Function = 4; //function id
            NeedReply = false; //reply bit
            this.Description = "Enable/Disable Alarm Acknowledge";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_BINA(ACKC5);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_BINA(ref  ACKC5);
        }
    }

    class MSG_S5F5 : StreamFunction
    {
        //public uint ALID = 0;
        public byte L_count = 0;
        public ushort[] ALID = new ushort[1];
        public MSG_S5F5()
        {
            Stream = 5; //stream id
            Function = 5; //function id
            NeedReply = true; //reply bit
            this.Description = "List Alarms Request";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            SETS_2bUI(ref ALID);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            GETS_2bUI(ref ALID);
        }
    }

    class MSG_S5F6 : StreamFunction
    {
        public byte L_count = 0;
        private byte L_count1 = 3;
        public byte ALCD = 0;
        public uint ALID = 0;
        public string ALTX = "";
        public ArrayList arrALCD = new ArrayList();
        public ArrayList arrALID = new ArrayList();
        public ArrayList arrALTX = new ArrayList();

        public MSG_S5F6()
        {
            Stream = 5; //stream id
            Function = 6; //function id
            NeedReply = false; //reply bit
            this.Description = "List Alarm Data";
        }
        //only body
        public override void ToBuffer()
        {
            //SETS_BINA(EACKC6);
            SETS_LIST(L_count);
            for (int k1 = 0; k1 < L_count; k1++)
            {
                SETS_LIST(L_count1);

                byte str = (byte)arrALCD[k1];
                SETS_BINA(str);
                uint str1 = (uint)arrALID[k1];
                SETS_4bUI(str1);
                string str2 = (string)arrALTX[k1];
                SETS_ASC2(str2);

            }
        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            for (int k1 = 0; k1 < L_count; k1++)
            {
                GETS_LIST(ref L_count1);
                GETS_BINA(ref ALCD);
                arrALCD.Add(ALCD);
                GETS_4bUI(ref ALID);
                arrALID.Add(ALID);
                GETS_ASC2(ref ALTX);
                arrALTX.Add(ALTX);
            }
            //GETS_BINA(ref  ACKC5);
        }
    }

    class MSG_S5F7 : StreamFunction
    {
        public MSG_S5F7()
        {
            Stream = 5;		    //stream id
            Function = 7;		//function id
            NeedReply = true;	//reply bit
            this.Description = "List Enabled Alarm Request";
        }

        //only header
        public override void ToBuffer()
        {
        }
        //only header
        public override void FromBuffer()
        {
        }
    }

    class MSG_S5F8 : StreamFunction
    {
        public byte L_count = 1;
        private byte L_count1 = 3;
        public byte ALCD = 0;
        public uint ALID = 0;
        public string ALTX = "";
        public ArrayList arrALCD = new ArrayList();
        public ArrayList arrALID = new ArrayList();
        public ArrayList arrALTX = new ArrayList();

        public MSG_S5F8()
        {
            Stream = 5; //stream id
            Function = 8; //function id
            NeedReply = false; //reply bit
            this.Description = "List Enabled Alarm Data";
        }
        //only body
        public override void ToBuffer()
        {
            //SETS_BINA(EACKC6);
            SETS_LIST(L_count);
            for (int k1 = 0; k1 < L_count; k1++)
            {
                SETS_LIST(L_count1);

                byte str = (byte)arrALCD[k1];
                SETS_BINA(str);
                uint str1 = (uint)arrALID[k1];
                SETS_4bUI(str1);
                string str2 = (string)arrALTX[k1];
                SETS_ASC2(str2);

            }
        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            for (int k1 = 0; k1 < L_count; k1++)
            {
                GETS_LIST(ref L_count1);
                GETS_BINA(ref ALCD);
                arrALCD.Add(ALCD);
                GETS_4bUI(ref ALID);
                arrALID.Add(ALID);
                GETS_ASC2(ref ALTX);
                arrALTX.Add(ALTX);
            }
            //GETS_BINA(ref  ACKC5);
        }
    }

    class MSG_S6F1 : StreamFunction
    {
        public byte L_count = 4;
        public uint TRID = 0;
        public uint SMPLN = 0;
        public string STIME = "";
        public byte L_count1 = 0;
        public byte SV = 0;
//        L,4
//1.<U4 TRID>
//2.<U4 SMPLN>
//3.<A STIME>
//4. L, n
//1.<* SV>
        public MSG_S6F1()
        {
            Stream = 6;		    //stream id
            Function = 1;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Trace Data Send";
        }

        //only header
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            SETS_4bUI(TRID);
            SETS_4bUI(SMPLN);
            SETS_ASC2(STIME);
            SETS_LIST(L_count1);
            for (int k1 = 0; k1 < L_count1; k1++)
                SETS_BINA(SV);
        }
        //only header
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            GETS_4bUI(ref TRID);
            GETS_4bUI(ref SMPLN);
            GETS_ASC2(ref STIME);
            GETS_LIST(ref L_count1);
            for (int k1 = 0; k1 < L_count1; k1++)
                GETS_BINA(ref SV);
        }
    }

    class MSG_S6F2 : StreamFunction
    {
       
        public MSG_S6F2()
        {
            Stream = 6; //stream id
            Function = 2; //function id
            NeedReply = false; //reply bit
            this.Description = "Trace Data Acknowledge";
        }
        //only body
        public override void ToBuffer()
        {
           
        }

        //only body
        public override void FromBuffer()
        {
           
        }
    }

    class MSG_S6F11 : StreamFunction
    {
        public byte L_count = 3;
        public byte L_count1 = 1;
        public byte L_count2 = 2;
        public byte L_count3 = 2;
        public uint DATAID = 0;   //随便写
        public uint CEID = 0;    //设备触发的事件ID
        public uint RPTID = 1;   //绑定CEID和 VID
        public uint VID = 1;     //设备的参数
        public ArrayList arrRPTID = new ArrayList();
        public ArrayList arrVID = new ArrayList();
        public ArrayList arrVID2 = new ArrayList();
        public ArrayList arrVID3 = new ArrayList();
        public ArrayList arrVID4 = new ArrayList();

        public ArrayList arrL_count3 = new ArrayList();
        public ArrayList arrRPT = new ArrayList();

        public MSG_S6F11()
        {
            Stream = 6;		    //stream id
            Function = 11;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Event Report Send";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            SETS_4bUI(DATAID);
            SETS_4bUI(CEID);
            int cou3 = 0;
            SETS_LIST(L_count1); //List    

            for (int k1 = 0; k1 < arrRPTID.Count; k1++)
            {
                SETS_LIST(L_count2); //List  

                uint str = (uint)arrRPTID[k1];
                SETS_4bUI(str);

                if (arrL_count3.Count != 0)
                {
                    byte str1 = (byte)Int32.Parse(arrRPT[k1].ToString());
                    SETS_LIST(str1);
                    for (int k3 = 0; k3 < arrVID.Count; k3++)
                    {

                        //for (int k2 = 0; k2 < arrL_count3.Count; k2++)
                        //{

                        if (typeof(byte) == arrVID[cou3].GetType())
                        {
                            byte str3 = (byte)arrVID[cou3];
                            SETS_BINA(str3);
                        }
                        else if (typeof(string) == arrVID[cou3].GetType())
                        {
                            string str2 = (string)arrVID[cou3];
                            SETS_ASC2(str2);
                        }
                        //uint str2 = (uint)arrVID[cou3];
                        //SETS_4bUI(str2);
                        cou3 = cou3 + 1;
                        //}
                    }

                    byte str8 = (byte)arrVID4.Count;
                    if (str8 > 0)
                    {
                        SETS_LIST(str8);
                        for (int k3 = 0; k3 < arrVID4.Count; k3++)
                        {

                            //for (int k2 = 0; k2 < arrL_count3.Count; k2++)
                            //{


                            {
                                uint str9 = (uint)int.Parse(arrVID4[k3].ToString());
                                SETS_4bUI(str9);
                            }
                            //}
                        }
                    }
                    byte str4 = (byte)arrVID2.Count;
                    if (str4 > 0)
                    {
                        SETS_LIST(str4);
                        for (int k3 = 0; k3 < arrVID2.Count; k3++)
                        {
                            //for (int k2 = 0; k2 < arrL_count3.Count; k2++)
                            //{


                            {
                                double str5 = double.Parse(arrVID2[k3].ToString());
                                SETS_8bFP(ref str5);
                            }
                            //}
                        }
                    }
                    byte str6 = (byte)arrVID3.Count;
                    if (str6 > 0)
                    {
                        SETS_LIST(str6);
                        for (int k3 = 0; k3 < arrVID3.Count; k3++)
                        {

                            //for (int k2 = 0; k2 < arrL_count3.Count; k2++)
                            //{


                            {
                                string str7 = (string)arrVID3[k3];
                                SETS_ASC2(str7);
                            }
                            //}
                        }
                    }

                }
            }
        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            GETS_4bUI(ref  DATAID);
            GETS_LIST(ref L_count1);
            for (int k1 = 0; k1 < L_count1; k1++)
            {
                GETS_LIST(ref L_count2);
                GETS_4bUI(ref  RPTID);
                arrRPTID.Add(RPTID);

                GETS_LIST(ref L_count3);
                arrL_count3.Add(L_count3);
                for (int k3 = 0; k3 < L_count3; k3++)
                {
                    GETS_4bUI(ref  VID);
                    arrVID.Add(VID);
                }
            }
        }
    }

    class MSG_S6F12 : StreamFunction
    {
        public byte EACKC6 = 0;
        //public String DRACK = " ";

        public MSG_S6F12()
        {
            Stream = 6; //stream id
            Function = 12; //function id
            NeedReply = false; //reply bit
            this.Description = "Event Report Acknowledge";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_BINA(EACKC6);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_BINA(ref  EACKC6);
        }
    }

    class MSG_S6F15 : StreamFunction
    {
        //private byte L_count1 = 1;
        public uint CEID = 0;

        public MSG_S6F15()
        {
            Stream = 6; //stream id
            Function = 15; //function id
            NeedReply = true ; //reply bit
            this.Description = "Event Report Request";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_4bUI(CEID);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_4bUI(ref CEID);
        }
    }

    class MSG_S6F16 : StreamFunction
    {
        public byte L_count = 3;
        public uint DATAID = 0;
        public uint CEID = 0;
        public byte L_count1 = 0;
        public byte L_count2 = 2;
        public uint RPTID = 0;
        public byte L_count3 = 0;
        public ArrayList arrRPTID = new ArrayList();
        public ArrayList arrSVID = new ArrayList();
        public string SVID = "";
        public MSG_S6F16()
        {
            Stream = 6; //stream id
            Function = 16; //function id
            NeedReply = false; //reply bit
            this.Description = "Event Report Data";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            SETS_4bUI(DATAID);
            SETS_4bUI(CEID);
            SETS_LIST(L_count1);
            for (int k1 = 0; k1 < L_count1; k1++)
            {
                SETS_LIST(L_count2);
                uint str = (uint)arrRPTID[k1];
                SETS_4bUI(str);
                
                SETS_LIST(L_count3);
                for (int k2 = 0; k2 < arrSVID.Count; k2++)
                {
                    if (arrSVID[k2].GetType() == typeof(byte))
                        SETS_BINA((byte)arrSVID[k2]);
                    else if (arrSVID[k2].GetType() == typeof(string))
                        SETS_ASC2((string)arrSVID[k2]);
                }
            }
        }

        //only body
        public override void FromBuffer()
        {
            GETS_4bUI(ref CEID);
        }
    }





    class MSG_S7F3 : StreamFunction
    {

        private byte L_count1 = 2;
        public String PPID = " ";
        public String PPBODY = " ";
        //public byte[] PPBODY = new byte[200];
        //public string PARAVAL = " ";
        public MSG_S7F3()
        {
            Stream = 7;		    //stream id
            Function = 3;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Process Program Send";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(L_count1); //List  //20201124 SPEC⊿Τlist
            SETS_ASC2(PPID);
            SETS_ASC2(PPBODY);
            //SETS_BINA(ref PPBODY);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count1);     //20201124 SPEC⊿Τlist
            GETS_ASC2(ref PPID);
           // GETS_BINA(ref PPBODY);
            GETS_ASC2(ref PPBODY);
            
        }
    }

    class MSG_S7F4 : StreamFunction
    {
        public byte ACKC7 = 0;

        public MSG_S7F4()
        {
            Stream = 7;		    //stream id
            Function = 4;		//function id
            NeedReply = false;	//reply bit
            this.Description = "Process Program Acknowledge";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_BINA(ACKC7);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_BINA(ref ACKC7);
        }
    }

    class MSG_S7F5 : StreamFunction
    {
        public String PPID = " ";

        public MSG_S7F5()
        {
            Stream = 7;		    //stream id
            Function = 5;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Process Program Request";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_ASC2(PPID);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_ASC2(ref PPID);
        }
    }

    class MSG_S7F6 : StreamFunction
    {
        private byte L_count1 = 2;
        public String PPID = " ";
        //public byte[] PPBODY = new byte[200];
        public String PPBODY = " ";
        public MSG_S7F6()
        {
            Stream = 7;		    //stream id
            Function = 6;		//function id
            NeedReply = false;	//reply bit
            this.Description = "Process Program Data";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(L_count1); //List
            SETS_ASC2(PPID);
            SETS_ASC2(PPBODY);
            //SETS_BINA(ref PPBODY);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count1);
            GETS_ASC2(ref PPID);
            //GETS_BINA(ref PPBODY);
            GETS_ASC2(ref PPBODY);
           
        }
    }

    class MSG_S7F17 : StreamFunction
    {
        public byte L_count = 1;
        public String PPID = "";
        public ArrayList arrPPID = new ArrayList();
        public MSG_S7F17()
        {
            Stream = 7;		    //stream id
            Function = 17;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Delete Process Program Send";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            for (int i = 0; i < L_count; i++)
            {
                String str = (String)arrPPID[i];
                SETS_ASC2(str);
            }
        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            for (int k1 = 0; k1 < L_count; k1++)
            {
                GETS_ASC2(ref PPID);
                arrPPID.Add(PPID);
            }
            //GETS_ASC2(ref PPID);
        }
    }

    class MSG_S7F18 : StreamFunction
    {
        public byte ACKC7 = 0;

        public MSG_S7F18()
        {
            Stream = 7;		    //stream id
            Function = 18;		//function id
            NeedReply = false;	//reply bit
            this.Description = "Delete Process Program Acknowledge";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_BINA(ACKC7);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_BINA(ref ACKC7);
        }
    }

    class MSG_S7F19 : StreamFunction
    {
        public MSG_S7F19()
        {
            Stream = 7;		    //stream id
            Function = 19;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Current EPPD Request";
        }

        //only header
        public override void ToBuffer()
        {
        }

        //only header
        public override void FromBuffer()
        {
        }

    }

    class MSG_S7F20 : StreamFunction
    {
        public byte L_count = 1;
        public String PPID = "";
        public ArrayList arrPPID = new ArrayList();

        public MSG_S7F20()
        {
            Stream = 7;		    //stream id
            Function = 20;		//function id
            NeedReply = false;	//reply bit
            this.Description = "Current EPPD Data";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            for (int i = 0; i < L_count; i++)
            {
                String str = (String)arrPPID[i];
                SETS_ASC2(str);
            }
        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            for (int k1 = 0; k1 < L_count; k1++)
            {
                GETS_ASC2(ref PPID);
                arrPPID.Add(PPID);

            }
        }
    }

    class MSG_S7F23 : StreamFunction
    {
        public byte L_count = 4;
        public String PPID = "";
        public String MDLN = "";
        public String SOFTREV = "";
        public byte L_count1 = 1;
        public byte L_count2 = 2;
        // public ushort CCODE = 0;
        public string CCODE = "";
        public byte L_count3 = 1;
        public String PPARM = "";
        public ArrayList arrL_count1 = new ArrayList();
        public ArrayList arrL_count3 = new ArrayList();
        public ArrayList arrPPARM = new ArrayList();

        public MSG_S7F23()
        {
            Stream = 7;		    //stream id
            Function = 23;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Formatted Process Program Send";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            SETS_ASC2(PPID);
            SETS_ASC2(MDLN);
            SETS_ASC2(SOFTREV);
            SETS_LIST(L_count1);
            int k4 = 0;
            for (int k1 = 0; k1 < L_count1; k1++)
            {
                SETS_LIST(L_count2);
                SETS_ASC2(CCODE);
                if (arrL_count3.Count != 0)
                {
                    byte str1 = (byte)Int32.Parse(arrL_count3[k1].ToString());
                    SETS_LIST(str1);
                    for (int k5 = 0; k5 < Int32.Parse(arrL_count3[k1].ToString()); k5++)
                    {
                        String str = (String)arrPPARM[k4];
                        SETS_ASC2(str);
                        k4 = k4 + 1;
                    }
                }

            }
        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            GETS_ASC2(ref PPID);
            GETS_ASC2(ref MDLN);
            GETS_ASC2(ref SOFTREV);
            GETS_LIST(ref L_count1);
            for (int k1 = 0; k1 < L_count1; k1++)
            {
                GETS_LIST(ref L_count2);
                GETS_ASC2(ref  CCODE);
                GETS_LIST(ref L_count3);
                arrL_count3.Add(L_count3);
                for (int k4 = 0; k4 < Int32.Parse(arrL_count3[k1].ToString()); k4++)
                {
                    GETS_ASC2(ref PPARM);
                    arrPPARM.Add(PPARM);
                }

            }
        }
    }

    class MSG_S7F24 : StreamFunction
    {
        public byte ACKC7 = 0;

        public MSG_S7F24()
        {
            Stream = 7;		    //stream id
            Function = 24;		//function id
            NeedReply = false;	//reply bit
            this.Description = "Formatted Process Program Acknowledge";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_BINA(ACKC7);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_BINA(ref ACKC7);
        }
    }

    class MSG_S7F25 : StreamFunction
    {
        public String PPID = " ";

        public MSG_S7F25()
        {
            Stream = 7;		    //stream id
            Function = 25;		//function id
            NeedReply = true;	//reply bit
            this.Description = "Formatted Process Program Request";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_ASC2(PPID);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_ASC2(ref PPID);
        }
    }

    class MSG_S7F26 : StreamFunction
    {
        public byte L_count = 4;
        public String PPID = "";
        public String MDLN = "";
        public String SOFTREV = "";
        public byte L_count1 = 1;
        public byte L_count2 = 2;
        //public ushort CCODE = 0;
        public string CCODE = "";
        public byte L_count3 = 0;
        public String PPARM = "";
        public ArrayList arrL_count1 = new ArrayList();
        public ArrayList arrL_count3 = new ArrayList();
        public ArrayList arrPPARM = new ArrayList();
        public ArrayList arrCCODE = new ArrayList();
        public MSG_S7F26()
        {
            Stream = 7;		    //stream id
            Function = 26;		//function id
            NeedReply = false;	//reply bit
            this.Description = "Formatted Process Program Data";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(L_count);
            SETS_ASC2(PPID);
            SETS_ASC2(MDLN);
            SETS_ASC2(SOFTREV);
            SETS_LIST(L_count1);
            int k4 = 0;
            for (int k1 = 0; k1 < L_count1; k1++)
            {
                SETS_LIST(L_count2);
                String str7 = (String)arrCCODE[k1];
                SETS_ASC2(str7);
                if (L_count3 != 0)
                {
                    byte str1 = (byte)L_count3;
                    SETS_LIST(str1);
                    for (int k5 = 0; k5 < L_count3; k5++)
                    {
                        String str = (String)arrPPARM[k5];
                        SETS_ASC2(str);
                        
                    }
                }
            }
        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            GETS_ASC2(ref PPID);
            GETS_ASC2(ref MDLN);
            GETS_ASC2(ref SOFTREV);
            GETS_LIST(ref L_count1);
            for (int k1 = 0; k1 < L_count1; k1++)
            {
                GETS_LIST(ref L_count2);
                //GETS_1bUI(ref CCODE);
                GETS_ASC2(ref CCODE);
                // GETS_LIST(ref L_count3);
                //for (int k3 = 0; k3 < arrL_count3.Count; k3++)
                //{
                GETS_LIST(ref L_count3);
                arrL_count3.Add(L_count3);
                for (int k4 = 0; k4 < Int32.Parse(arrL_count3[k1].ToString()); k4++)
                {
                    GETS_ASC2(ref PPARM);
                    arrPPARM.Add(PPARM);
                }

                //}
            }
        }
    }

    class MSG_S9F1 : StreamFunction
    {
        public byte[] MHEAD = new byte[200];
        //public byte MHEAD = 0;
        //<Bi MHEAD>
        public MSG_S9F1()
        {
            Stream = 9;		    //stream id
            Function = 1;		//function id
            NeedReply = false;	//reply bit
            this.Description = "Unrecognized Device ID";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_BINA(ref MHEAD);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_BINA(ref MHEAD);
        }
    }

    class MSG_S9F3 : StreamFunction
    {
        public byte[] MHEAD = new byte[200];
        //public byte MHEAD = 0;
        //<Bi MHEAD>
        public MSG_S9F3()
        {
            Stream = 9;		    //stream id
            Function = 3;		//function id
            NeedReply = false;	//reply bit
            this.Description = "Unrecognized Stream Type";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_BINA(ref MHEAD);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_BINA(ref MHEAD);
        }
    }

    class MSG_S9F5 : StreamFunction
    {
        public byte[] MHEAD = new byte[200];
       // public byte MHEAD = 0;
        //<Bi MHEAD>
        public MSG_S9F5()
        {
            Stream = 9;		    //stream id
            Function = 5;		//function id
            NeedReply = false;	//reply bit
            this.Description = "Unrecognized Function Type";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_BINA(ref MHEAD);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_BINA(ref MHEAD);
        }
    }

    class MSG_S9F7 : StreamFunction
    {
        public byte[] MHEAD = new byte[200];
       // public byte MHEAD = 0;
        //<Bi MHEAD>
        public MSG_S9F7()
        {
            Stream = 9;		    //stream id
            Function = 7;		//function id
            NeedReply = false;	//reply bit
            this.Description = "Illegal Data";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_BINA(ref MHEAD);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_BINA(ref MHEAD);
        }
    }

    class MSG_S9F9 : StreamFunction
    {
        public byte[] SMHEAD = new byte[200];
        //public byte SMHEAD = 0;
        //<Bi SMHEAD>
        public MSG_S9F9()
        {
            Stream = 9;		    //stream id
            Function = 9;		//function id
            NeedReply = false;	//reply bit
            this.Description = "Transaction Timer Timeout";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_BINA(ref SMHEAD);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_BINA(ref SMHEAD);
        }
    }

    class MSG_S9F11 : StreamFunction
    {
        public byte[] MHEAD = new byte[200];
        //public byte MHEAD = 0;
        //<Bi MHEAD>
        public MSG_S9F11()
        {
            Stream = 9;		    //stream id
            Function = 11;		//function id
            NeedReply = false;	//reply bit
            this.Description = "Data Too Long";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_BINA(ref MHEAD);
        }

        //only body
        public override void FromBuffer()
        {
            GETS_BINA(ref MHEAD);
        }
    }
    class MSG_S10F3 : StreamFunction
    {
        public byte L_count = 2;
        public byte TID = 0;
        public String TEXT = "";
        public byte ACKC10 = 0;
        //public byte MHEAD = 0;
        //<Bi MHEAD>
        public MSG_S10F3()
        {
            Stream = 10;		    //stream id
            Function = 3;		//function id
            NeedReply = false;	//reply bit
            this.Description = "Terminal display,Single";
        }

        //only body
        public override void ToBuffer()
        {

        }

        //only body
        public override void FromBuffer()
        {
            GETS_LIST(ref L_count);
            GETS_BINA(ref TID);
            GETS_ASC2(ref TEXT);

        }
    }
    class MSG_S10F4 : StreamFunction
    {
        public byte ACKC10 = 0;
        //public byte MHEAD = 0;
        //<Bi MHEAD>
        public MSG_S10F4()
        {
            Stream = 10;		    //stream id
            Function = 4;		//function id
            NeedReply = false;	//reply bit
            this.Description = "Terminal display,Single Acknowledge";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_BINA(ACKC10);
        }

        //only body
        public override void FromBuffer()
        {

        }
    }
    class MSG_S14F1 : StreamFunction   //请求获取maping
    {
        public string OBJSPEC = "";
        public string OBJTYPE = "";
        public byte L_count = 1;
        public ArrayList arrL_count = new ArrayList();


        public MSG_S14F1()
        {
            Stream = 14;		    //stream id
            Function = 1;		//function id
            NeedReply = true;	//reply bit
            this.Description = " GetAttr Request";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(5);
            SETS_ASC2(OBJSPEC);
            SETS_ASC2(OBJTYPE);
            SETS_LIST(L_count);
            for (int k1 = 0; k1 < L_count; k1++)
            {
                SETS_ASC2(arrL_count[k1].ToString());
            }
            SETS_LIST(0);
            SETS_LIST(0);
        }

        //only body
        public override void FromBuffer()
        {
            
        }
    }
    class MSG_S14F2 : StreamFunction   //获取maping
    {
        public byte L_count = 2;
        public byte L_count2 = 1;
        public byte L_count3 = 2;
        public byte L_count4 = 1;
        public byte L_count5 = 3;
        public string OBJID = "";
        public string ATTRID = "";
        public string ATTRDATA = "";
        public string ATTRDATA2 = "";
        public byte OBJACK = 0;
        public byte L_count6 = 0;
        public byte L_count7 = 2;
        public string ERRCODE = "";
        public string ERRTEXT = "";
        //public byte MHEAD = 0;
        //<Bi MHEAD>
        public MSG_S14F2()
        {
            Stream = 14;		    //stream id
            Function = 2;		//function id
            NeedReply = false;	//reply bit
            this.Description = "GetAttr Data";
        }

        //only body
        public override void ToBuffer()
        {
            GETS_LIST(ref L_count);
            for (int i = 0; i < L_count; i++)
            {
                if(i==0)
                {
                    GETS_LIST(ref L_count2);
                    GETS_LIST(ref L_count3);
                    GETS_ASC2(ref OBJID);
                    GETS_LIST(ref L_count4);
                    GETS_LIST(ref L_count5);
                    GETS_ASC2(ref ATTRID);
                    GETS_ASC2(ref ATTRDATA);   //解析数据  xml
                    GETS_ASC2(ref ATTRDATA2);   //解析数据  xml
                }
                else
                {
                    GETS_BINA(ref OBJACK);
                    GETS_LIST(ref L_count6);
                    for (int j = 0; j < L_count6; j++)
                    {
                        GETS_LIST(ref L_count7);
                        GETS_ASC2(ref ERRCODE);
                        GETS_ASC2(ref ERRTEXT);
                    }
                }
            }
        }
        //only body
        public override void FromBuffer()
        {
           // GETS_BINA(ref MHEAD);
        }
    }
    class MSG_S14F3 : StreamFunction
    {
        public string OBJSPEC = "";
        public string OBJTYPE = "";
        public byte L_count = 1;
        public ArrayList arrL_count = new ArrayList();
        //public byte MHEAD = 0;
        //<Bi MHEAD>
        public MSG_S14F3()
        {
            Stream = 14;		    //stream id
            Function = 3;		//function id
            NeedReply = false;	//reply bit
            this.Description = "SetAttr Request";
        }

        //only body
        public override void ToBuffer()
        {
            SETS_LIST(4);
            SETS_ASC2(OBJSPEC);
            SETS_ASC2(OBJTYPE);
            SETS_LIST(L_count);
            for (int i = 0; i < arrL_count.Count; i++)
            {
                SETS_ASC2(arrL_count[i].ToString());
            }
            SETS_LIST(0);
        }
        //only body
        public override void FromBuffer()
        {

        }
    }
    class UnKnownMSG : StreamFunction 
    {
        public UnKnownMSG(String StreamFunction) // SnF0
        {
            Stream = int.Parse(StreamFunction.Substring(1, 3)); //Stream   ID
            Function = int.Parse(StreamFunction.Substring(5, 3)); //Function ID
            NeedReply = false;                                     //True=1, False=0 (WAIT BIT)
            IsNgStrFun = true;                                      //
            Description = "UnKnownMessage: S" + Stream.ToString() + "F0";
        }
        public override void ToBuffer()
        {
        }
        public override void FromBuffer()
        {
        }

    } 
}

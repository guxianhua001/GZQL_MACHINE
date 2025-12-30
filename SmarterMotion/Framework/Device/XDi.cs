using Core.Abstraction;
using System.ComponentModel;
using System.Xml.Linq;

namespace SmarterMotion
{
    /// <summary>
    /// 输入IO
    /// </summary>
    public class XDi : XObject, INotifyPropertyChanged, IDigitalInput
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private XCard card;
        private int channel;
        private int actDiId;
        private string name;
        private int m_STS;
        private bool m_PLS;
        private bool m_PLF;
        private int m_DiStsLast;
        private string cardname;

        private object obj = new object();

        public XDi(XCard card, int channel, int actDiId, string name, string cardname)
        {
            this.card = card;
            this.channel = channel;
            this.actDiId = actDiId;
            this.name = name;
            this.cardname = cardname;


        }

        public int CardId { get; set; }

        public int TaskId { get; set; }

        public int Update()
        {
            int sts = 0;
            int ret = GetDi(ref sts);
            lock (obj)
            {
                if ((sts > 0) && (m_DiStsLast <= 0))
                {
                    PLS = true;
                    PLF = false;
                }
                else if ((sts <= 0) && (m_DiStsLast > 0))
                {
                    PLF = true;
                    PLS = false;
                }
                else
                {
                    PLS = false;
                    PLF = false;
                }

                m_STS = sts;
                m_DiStsLast = m_STS;
            }
            return ret;
        }

        public int GetDi(ref int sts)
        {
            return card.GetDi(channel, actDiId, ref sts);
        }
        public int ReadDi()
        {
            int sts = 0;
            return card.ReadDi(channel);
        }

        public int SetState(bool state)
        {
            throw new System.NotImplementedException();
        }

        public int Channel
        {
            get
            {
                return channel;
            }
        }
        public int ActId
        {
            get
            {
                return actDiId;
            }
        }

        public int SetId { get; set; }

        public string Name
        {
            get => name;
            set
            {
                if (name != value)
                {
                    name = value;
                    OnPropertyChanged(nameof(Name)); // 触发通知
                }
            }
        }

        public string CardName
        {
            get
            {
                return cardname;
            }
        }
        public bool STS
        {
            set
            {
                lock (obj)
                {
                    int newValue = value ? 1 : 0;
                    if (m_STS != newValue)
                    {
                        m_STS = newValue;
                        OnPropertyChanged(nameof(STS)); // 触发通知
                    }
                }
            }
            get
            {
                lock (obj)
                {
                    return m_STS > 0;
                }
            }
        }

        public bool PLS
        {
            set
            {
                lock (obj)
                {
                    m_PLS = value;
                }
            }
            get
            {
                lock (obj)
                {
                    return m_PLS;
                }
            }
        }

        public bool PLF
        {
            set
            {
                lock (obj)
                {
                    m_PLF = value;
                }
            }
            get
            {
                lock (obj)
                {
                    return m_PLF;
                }
            }
        }
        public int Id => throw new System.NotImplementedException();

        public bool RisingEdge => throw new System.NotImplementedException();

        public bool FallingEdge => throw new System.NotImplementedException();

        public bool Status => throw new System.NotImplementedException();
    }
}

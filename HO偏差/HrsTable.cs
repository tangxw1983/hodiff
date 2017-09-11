using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;

namespace HO偏差
{
    [Serializable()]
    class Hrs
    {
        private Hrs()
        {

        }

        public Hrs(string no, double win, double plc)
        {
            this.No = no;
            this.Win = win;
            this.Plc = plc;
        }

        public string No { get; private set; }
        public double Win { get; private set; }
        public double Plc { get; private set; }
        public double Mean { get; set; }
        public double Var { get; set; }
    }

    [Serializable()]
    class HrsTable : List<Hrs>
    {
        private Comb2Table _sp_q = null;
        private Comb2Table _sp_qp = null;

        public double E { get; set; }

        public Hrs this[string no]
        {
            get
            {
                int inx = this.FindIndex(x => x.No == no);
                if (inx < 0)
                    return null;
                else
                    return this[inx];
            }
        }

        public void ClearSCR()
        {
            for (int i = 0; i < this.Count; i++)
            {
                if (this[i].Win == 0 || this[i].Plc == 0)
                {
                    this.RemoveAt(i);
                    i--;
                }
            }
        }

        public double[] SpWin
        {
            get
            {
                return this.Select(x => x.Win).ToArray();
            }
        }

        public double[] SpPlc
        {
            get
            {
                return this.Select(x => x.Plc).ToArray();
            }
        }

        public bool HasSpQ
        {
            get
            {
                return _sp_q != null;
            }
        }
        
        public Comb2Table SpQ
        {
            get
            {
                lock(this)
                {
                    if (_sp_q == null)
                        _sp_q = new Comb2Table(this);
                }

                return _sp_q;
            }
        }

        public bool HasSpQp
        {
            get
            {
                return _sp_qp != null;
            }
        }

        public Comb2Table SpQp
        {
            get
            {
                lock(this)
                {
                    if (_sp_qp == null)
                        _sp_qp = new Comb2Table(this);
                }

                return _sp_qp;
            }
        }

        public void Save(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(fs, this);
            }
        }

        public static HrsTable Load(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                BinaryFormatter bf = new BinaryFormatter();
                return (HrsTable)bf.Deserialize(fs);
            }
        }
    }

    [Serializable()]
    class Comb2Table : Dictionary<string, double>
    {
        public Comb2Table(HrsTable owner)
        {
            _owner = owner;
        }

        protected Comb2Table(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            _owner = (HrsTable)info.GetValue("Comb2Table_owner", typeof(HrsTable));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Comb2Table_owner", _owner);
        }

        private HrsTable _owner;

        private double getByNo(string no)
        {
            if (this.ContainsKey(no))
                return this[no];
            else
                return 0;
        }

        public double[] Sp
        {
            get
            {
                common.Math.Combination comb = new common.Math.Combination(_owner.Count, 2);
                return comb.GetCombinations().Select(x => this.getByNo(string.Format("{0}-{1}", _owner[x[0]].No, _owner[x[1]].No))).ToArray();
            }
        }
    }
}

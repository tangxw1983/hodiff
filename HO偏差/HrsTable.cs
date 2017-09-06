using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HO偏差
{
    class Hrs
    {
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

    class HrsTable : List<Hrs>
    {
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
    }

    class Comb2Table : Dictionary<string, double>
    {
        public Comb2Table(HrsTable owner)
        {
            _owner = owner;
        }

        private HrsTable _owner;


    }
}

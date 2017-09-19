using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HO偏差
{
    public enum WaterWPType : int
    {
        None = 0,
        Win = 1,
        Plc = 2,
        WinPlc = 3
    }

    [Serializable()]
    public class WaterWPItem
    {

        public double Percent { get; set; }
        public double WinAmount { get; set; }
        public double WinLimit { get; set; }
        public double PlcAmount { get; set; }
        public double PlcLimit { get; set; }
    }

    public class WaterWPList : List<WaterWPItem>
    {
        public WaterWPList() : base() { }
        public WaterWPList(IEnumerable<WaterWPItem> collection) : base(collection) { }

        public double GetTotalAmount(WaterWPType type)
        {
            switch (type)
            {
                case WaterWPType.Win:
                    return this.Where(x => x.PlcAmount == 0).Select(x => x.WinAmount).Sum();
                case WaterWPType.Plc:
                    return this.Where(x => x.WinAmount == 0).Select(x => x.PlcAmount).Sum();
                case WaterWPType.WinPlc:
                    return this.Where(x => x.PlcAmount > 0 && x.WinAmount > 0).Select(x => x.WinAmount).Sum();
                case WaterWPType.None:
                    return this.Select(x => x.WinAmount + x.PlcAmount).Sum();
                default:
                    return 0;
            }
        }
    }

    [Serializable()]
    public class WaterWP : List<WaterWPItem>
    {
        public WaterWP(string type)
        {
            if (type == "BET")
            {
                _bet = true;
            }
            else
            {
                _bet = false;
            }
        }

        private bool _bet;

        public string Type
        {
            get
            {
                return _bet ? "BET" : "EAT";
            }
            set
            {
                _bet = (value == "BET");
            }
        }

        public double GetBestWater(WaterWPType type)
        {
            if (_bet)
            {
                return this.Where(x => ((type & WaterWPType.Win) != WaterWPType.None ? x.WinAmount > 0 : true) && ((type & WaterWPType.Plc) != WaterWPType.None ? x.PlcAmount > 0 : true)).Max(x => x.Percent);
            }
            else
            {
                return this.Where(x => ((type & WaterWPType.Win) != WaterWPType.None ? x.WinAmount > 0 : true) && ((type & WaterWPType.Plc) != WaterWPType.None ? x.PlcAmount > 0 : true)).Min(x => x.Percent);
            }
        }

        public WaterWPList GetValuableWater(double minR, double oddsWin, double pWin, double oddsPlc, double pPlc)
        {
            if (_bet)
            {
                return new WaterWPList(this.Where(x =>
                    (x.WinAmount > 0 ? (1 + x.Percent / 100 / Math.Min(oddsWin, x.WinLimit / 10)) * (1 - pWin) > minR : true) &&
                    (x.PlcAmount > 0 ? (1 + x.Percent / 100 / Math.Min(oddsPlc, x.PlcLimit / 10)) * (1 - pPlc) > minR : true)
                ));
            }
            else
            {
                return new WaterWPList(this.Where(x =>
                    (x.WinAmount > 0 ? (Math.Min(oddsWin, x.WinLimit / 10) * 100 / x.Percent) * pWin > minR : true) &&
                    (x.PlcAmount > 0 ? (Math.Min(oddsPlc, x.PlcLimit / 10) * 100 / x.Percent) * pPlc > minR : true)
                ));
            }
        }
    }

    [Serializable()]
    class WaterQnItem
    {
        public double Percent { get; set; }
        public double Amount { get; set; }
        public double Limit { get; set; }
    }

    class WaterQnList : List<WaterQnItem>
    {
        public WaterQnList() { }
        public WaterQnList(IEnumerable<WaterQnItem> collection) : base(collection) { }
    }

    [Serializable()]
    class WaterQn : List<WaterQnItem>
    {
        public WaterQn(string type)
        {
            if (type == "BET")
            {
                _bet = true;
            }
            else
            {
                _bet = false;
            }
        }

        private bool _bet;

        public string Type
        {
            get
            {
                return _bet ? "BET" : "EAT";
            }
            set
            {
                _bet = (value == "BET");
            }
        }

        public double GetBestWater()
        {
            if (_bet)
            {
                return this.Max(x => x.Percent);
            }
            else
            {
                return this.Min(x => x.Percent);
            }
        }

        public WaterQnList GetValuableWater(double minR, double odds, double p)
        {
            if (_bet)
            {
                return new WaterQnList(this.Where(x => (1 + x.Percent / 100 / Math.Min(odds, x.Limit / 10)) * (1 - p) > minR) );
            }
            else
            {
                return new WaterQnList(this.Where(x => (Math.Min(odds, x.Limit / 10) * 100 / x.Percent) * p > minR));
            }
        }
    }

    [Serializable()]
    class WaterTable
    {
        public WaterTable()
        {
            _wp_bet_waters = new Dictionary<string, WaterWP>();
            _wp_eat_waters = new Dictionary<string, WaterWP>();
            _qn_bet_waters = new Dictionary<string, WaterQn>();
            _qn_eat_waters = new Dictionary<string, WaterQn>();
            _qp_bet_waters = new Dictionary<string, WaterQn>();
            _qp_eat_waters = new Dictionary<string, WaterQn>();
            
        }

        private Dictionary<string, WaterWP> _wp_bet_waters;
        private Dictionary<string, WaterWP> _wp_eat_waters;
        private Dictionary<string, WaterQn> _qn_bet_waters;
        private Dictionary<string, WaterQn> _qn_eat_waters;
        private Dictionary<string, WaterQn> _qp_bet_waters;
        private Dictionary<string, WaterQn> _qp_eat_waters;

        public void Clear()
        {
            _wp_bet_waters.Clear();
            _wp_eat_waters.Clear();
            _qn_bet_waters.Clear();
            _qn_eat_waters.Clear();
            _qp_bet_waters.Clear();
            _qp_eat_waters.Clear();
        }

        public bool IsEmpty
        {
            get
            {
                return _wp_bet_waters.Count == 0 &&
                    _wp_eat_waters.Count == 0 &&
                    _qn_bet_waters.Count == 0 &&
                    _qn_eat_waters.Count == 0 &&
                    _qp_bet_waters.Count == 0 &&
                    _qp_eat_waters.Count == 0;
            }
        }

        public void FixWaterType()
        {
            if (_wp_eat_waters != null)
            {
                foreach (KeyValuePair<string, WaterWP> kvp in _wp_eat_waters)
                {
                    kvp.Value.Type = "EAT";
                }
            }
            if (_qn_eat_waters != null)
            {
                foreach (KeyValuePair<string, WaterQn> kvp in _qn_eat_waters)
                {
                    kvp.Value.Type = "EAT";
                }
            }
            if (_qn_eat_waters != null)
            {
                foreach (KeyValuePair<string, WaterQn> kvp in _qp_eat_waters)
                {
                    kvp.Value.Type = "EAT";
                }
            }
        }

        public WaterWP GetWpBetWater(string hrsNo)
        {
            lock(_wp_bet_waters)
            {
                if (!_wp_bet_waters.ContainsKey(hrsNo))
                {
                    _wp_bet_waters[hrsNo] = new WaterWP("BET");
                }
            }
            
            return _wp_bet_waters[hrsNo];
        }

        public WaterWP GetWpEatWater(string hrsNo)
        {
            lock (_wp_eat_waters)
            {
                if (!_wp_eat_waters.ContainsKey(hrsNo))
                {
                    _wp_eat_waters[hrsNo] = new WaterWP("EAT");
                }
            }

            return _wp_eat_waters[hrsNo];
        }

        public WaterQn GetQnBetWater(string hrsNo)
        {
            lock (_qn_bet_waters)
            {
                if (!_qn_bet_waters.ContainsKey(hrsNo))
                {
                    _qn_bet_waters[hrsNo] = new WaterQn("BET");
                }
            }

            return _qn_bet_waters[hrsNo];
        }

        public WaterQn GetQnEatWater(string hrsNo)
        {
            lock (_qn_eat_waters)
            {
                if (!_qn_eat_waters.ContainsKey(hrsNo))
                {
                    _qn_eat_waters[hrsNo] = new WaterQn("EAT");
                }
            }

            return _qn_eat_waters[hrsNo];
        }

        public WaterQn GetQpBetWater(string hrsNo)
        {
            lock (_qp_bet_waters)
            {
                if (!_qp_bet_waters.ContainsKey(hrsNo))
                {
                    _qp_bet_waters[hrsNo] = new WaterQn("BET");
                }
            }

            return _qp_bet_waters[hrsNo];
        }

        public WaterQn GetQpEatWater(string hrsNo)
        {
            lock (_qp_eat_waters)
            {
                if (!_qp_eat_waters.ContainsKey(hrsNo))
                {
                    _qp_eat_waters[hrsNo] = new WaterQn("EAT");
                }
            }

            return _qp_eat_waters[hrsNo];
        }
    }
}

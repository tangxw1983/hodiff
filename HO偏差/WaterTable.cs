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
    }

    [Serializable()]
    class WaterQnItem
    {
        public double Percent { get; set; }
        public double Amount { get; set; }
        public double Limit { get; set; }
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
                    _wp_eat_waters[hrsNo] = new WaterWP("BET");
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
                    _qn_eat_waters[hrsNo] = new WaterQn("BET");
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
                    _qp_eat_waters[hrsNo] = new WaterQn("BET");
                }
            }

            return _qp_eat_waters[hrsNo];
        }
    }
}

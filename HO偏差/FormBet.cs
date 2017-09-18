using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HO偏差
{
    public partial class FormBet : Form
    {
        public FormBet()
        {
            InitializeComponent();
            this.iptRaceDate.Value = DateTime.Now;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// deamon负责检测进入什么阶段，创建进入处理阶段的RaceHandler实例、通知RaceHandler进行下单
        /// RaceHandler实例负责获取最新数据，拟合概率，决定如何下单
        /// </summary>
        private void deamon()
        {

        }

        class InvestRecordWp
        {
            public long TimeKey { get; set; }
            public int Model { get; set; }
            public ulong CardID { get; set; }
            public int RaceNo { get; set; }
            public string Direction { get; set; }
            public string HorseNo { get; set; }
            public double Percent { get; set; }
            public double WinAmount { get; set; }
            public double WinLimit { get; set; }
            public double WinOdds { get; set; }
            public double WinProbility { get; set; }
            public double PlcAmount { get; set; }
            public double PlcLimit { get; set; }
            public double PlcOdds { get; set; }
            public double PlcProbility { get; set; }
            public double FittingLoss { get; set; }
        }

        class InvestRecordQn
        {
            public long TimeKey { get; set; }
            public int Model { get; set; }
            public ulong CardID { get; set; }
            public int RaceNo { get; set; }
            public string Direction { get; set; }
            public string Type { get; set; }
            public string HorseNo { get; set; }
            public double Percent { get; set; }
            public double Amount { get; set; }
            public double Limit { get; set; }
            public double Odds { get; set; }
            public double Probility { get; set; }
            public double FittingLoss { get; set; }
        }

        class RaceHandler
        {
            public RaceHandler()
            {

            }

            private ulong _race_id;
            private ulong _card_id;
            private int _race_no;
            private DateTime _start_time;
            
            private HrsTable _latest_odds;
            private HrsTable _fitted_odds;
            private WaterTable _latest_waters;

            private int _stage;

            private Thread _tDaemon;
            private Thread _tFix;
            private Thread _tBet;

            private const double E_THRESHOLD_SCALE = 1.1;
            private const double MIN_R = 1.1;
            private const double T = 10000;
            private const double LOSS_RATE_COEFFICIENT = 5.43;
            private const double WP_STEP = 5;
            private const double QN_STEP = 10;
            private const double LIMIT_SCALE = 10;
            private const int MODEL = 101;

            private static DateTime UNIXTIME_BASE = new DateTime(1970, 1, 1, 8, 0, 0);

            private long ToUnixTime(DateTime time)
            {
                return (long)time.Subtract(UNIXTIME_BASE).TotalMilliseconds;
            }

            /// <summary>
            /// 启动守护线程
            /// </summary>
            public void startDaemon()
            {
                if (_tDaemon == null)
                {
                    _tDaemon = new Thread(new ThreadStart(daemon));
                    _tDaemon.IsBackground = true;
                    _tDaemon.Start();
                }
            }

            public void startBet()
            {
                if (_tDaemon == null)
                {
                    _tBet = new Thread(delegate()
                    {
                        this.bet();
                    });
                    _tBet.IsBackground = true;
                    _tBet.Start();
                }
            }


            private void daemon()
            {
                while (true)
                {
                    if (_stage == 0)
                    {
                        if (DateTime.Now.Subtract(_start_time).TotalMinutes > 5)
                        {
                            Thread.Sleep(10000);
                        }
                        else
                        {
                            _stage = 1;
                        }
                    }
                    else if (_stage == 1)
                    {
                        if (_tFix != null)
                        {
                            _tFix = new Thread(new ThreadStart(fix));
                            _tFix.IsBackground = true;
                            _tFix.Start();
                        }
                    }


                }
            }

            private void fix()
            {
                while(true)
                {
                    if (this.updateWpOddsData() && this.updateQnOddsData())
                    {
                        HrsTable fittingOdds = _latest_odds;
                        double E = Fitting.fit(fittingOdds, 0.0001);
                        fittingOdds.E = E;
                        _fitted_odds = fittingOdds;
                    }

                    Thread.Sleep(1000);
                }
            }

            private void bet()
            {
                if (_fitted_odds != null && this.updateWpOddsData() && this.updateQnOddsData() && this.updateBetWpWatersData() && this.updateEatWpWatersData() && this.updateBetQnWatersData() && this.updateEatQnWatersData() && this.updateBetQpWatersData() && this.updateEatQpWatersData())
                {
                    double[] p1, p3, pq_win, pq_plc;
                    Fitting.calcProbility(_fitted_odds, out p1, out p3, out pq_win, out pq_plc);

                    // 计算预计概率与当前赔率下下注比例的交叉熵
                    double[] q1, qq;
                    double r1, rq;
                    q1 = Fitting.calcBetRateForWin(_latest_odds, out r1);
                    qq = Fitting.calcBetRateForQn(_latest_odds, out rq);
                    double E = cross_entropy(p1, q1) + cross_entropy(pq_win, qq);

                    // 当前赔率下交叉熵过大则退出，不下单
                    if (E > _fitted_odds.E * E_THRESHOLD_SCALE)
                    {
                        _fitted_odds = null;        // 已经拟合的数据无效
                        return;
                    }

                    for (int i = 0; i < _latest_odds.Count; i++)
                    {
                        Hrs h = _latest_odds[i];

                        double sp_w_min = Math.Min(h.Win, _fitted_odds[i].Win);
                        double sp_w_max = Math.Max(h.Win, _fitted_odds[i].Win);
                        double sp_p_min = Math.Min(h.Plc, _fitted_odds[i].Plc);
                        double sp_p_max = Math.Max(h.Plc, _fitted_odds[i].Plc);

                        // For Bet
                        {
                            WaterWPList vlist = _latest_waters.GetWpEatWater(h.No).GetValuableWater(MIN_R, sp_w_min, p1[i], sp_p_min, p3[i]);
                            double bet_amount_win = 0, bet_amount_plc = 0;
                            bool full_win = false, full_plc = false;
                            foreach (WaterWPItem w in vlist.OrderBy(x => x.Percent))
                            {
                                if (w.WinAmount > 0 && full_win) continue;
                                if (w.PlcAmount > 0 && full_plc) continue;

                                double bet_amount = -1;

                                if (w.WinAmount > 0)
                                {
                                    double O = Math.Min(w.WinLimit / LIMIT_SCALE, sp_w_min) * 100 / w.Percent;
                                    double max_bet = (T * Math.Pow(O * p1[i] - 1, 2)) / (LOSS_RATE_COEFFICIENT * Math.Pow(O, 2) * p1[i] * (1 - p1[i]));
                                    if (bet_amount_win >= max_bet)
                                    {
                                        full_win = true;
                                        continue;
                                    }
                                    else
                                    {
                                        bet_amount = Math.Min(max_bet - bet_amount_win, w.WinAmount);
                                    }
                                }

                                if (w.PlcAmount > 0)
                                {
                                    double O = Math.Min(w.PlcLimit / LIMIT_SCALE, sp_p_min) * 100 / w.Percent;
                                    double max_bet = (T * Math.Pow(O * p3[i] - 1, 2)) / (LOSS_RATE_COEFFICIENT * Math.Pow(O, 2) * p3[i] * (1 - p3[i]));
                                    if (bet_amount_plc >= max_bet)
                                    {
                                        full_plc = true;
                                        continue;
                                    }
                                    else
                                    {
                                        if (bet_amount == -1)
                                        {
                                            bet_amount = Math.Min(max_bet - bet_amount_plc, w.PlcAmount);
                                        }
                                        else
                                        {
                                            bet_amount = Math.Min(bet_amount, Math.Min(max_bet - bet_amount_plc, w.PlcAmount));   // 有Win也有Plc, 那么Plc肯定和Win一样
                                        }
                                    }
                                }

                                if (bet_amount > 0)
                                {
                                    bet_amount = Math.Round(bet_amount / WP_STEP) * WP_STEP;
                                    if (bet_amount > 0)
                                    {
                                        InvestRecordWp ir = new InvestRecordWp()
                                        {
                                            TimeKey = ToUnixTime(DateTime.Now),
                                            Model = MODEL,
                                            CardID = _card_id,
                                            RaceNo = _race_no,
                                            Direction = "BET",
                                            HorseNo = h.No,
                                            Percent = w.Percent,
                                            WinLimit = w.WinLimit,
                                            PlcLimit = w.PlcLimit,
                                            FittingLoss = E
                                        };

                                        if (w.WinLimit > 0)
                                        {
                                            ir.WinAmount = bet_amount;
                                            ir.WinOdds = sp_w_min;
                                            ir.WinProbility = p1[i];
                                        }
                                        if (w.PlcLimit > 0)
                                        {
                                            ir.PlcAmount = bet_amount;
                                            ir.PlcOdds = sp_p_min;
                                            ir.PlcProbility = p3[i];
                                        }

                                        //wp_records.Add(ir);

                                        bet_amount_win += ir.WinAmount;
                                        bet_amount_plc += ir.PlcAmount;
                                    }
                                }
                            }
                        }

                        // For Eat
                        {
                            WaterWPList vlist = _latest_waters.GetWpBetWater(h.No).GetValuableWater(MIN_R, sp_w_max, p1[i], sp_p_max, p3[i]);
                            double eat_amount_win = 0, eat_amount_plc = 0;
                            bool full_win = false, full_plc = false;
                            foreach (WaterWPItem w in vlist.OrderByDescending(x => x.Percent))
                            {
                                if (w.WinAmount > 0 && full_win) continue;
                                if (w.PlcAmount > 0 && full_plc) continue;

                                double eat_amount = -1;

                                if (w.WinAmount > 0)
                                {
                                    double O = 1 + w.Percent / 100 / Math.Min(w.WinLimit / LIMIT_SCALE, sp_w_max);
                                    double max_eat = (T * Math.Pow(O * (1 - p1[i]) - 1, 2)) / (LOSS_RATE_COEFFICIENT * Math.Pow(O, 2) * (1 - p1[i]) * p1[i]);
                                    max_eat = max_eat / Math.Min(w.WinLimit / LIMIT_SCALE, sp_w_max);
                                    if (eat_amount_win >= max_eat)
                                    {
                                        full_win = true;
                                        continue;
                                    }
                                    else
                                    {
                                        eat_amount = Math.Min(max_eat - eat_amount_win, w.WinAmount);
                                    }
                                }

                                if (w.PlcAmount > 0)
                                {
                                    double O = 1 + w.Percent / 100 / Math.Min(w.PlcLimit / LIMIT_SCALE, sp_p_max);
                                    double max_eat = (T * Math.Pow(O * (1 - p3[i]) - 1, 2)) / (LOSS_RATE_COEFFICIENT * Math.Pow(O, 2) * (1 - p3[i]) * p3[i]);
                                    max_eat = max_eat / Math.Min(w.PlcLimit / LIMIT_SCALE, sp_p_max);
                                    if (eat_amount_plc >= max_eat)
                                    {
                                        full_plc = true;
                                        continue;
                                    }
                                    else
                                    {
                                        if (eat_amount == -1)
                                        {
                                            eat_amount = Math.Min(max_eat - eat_amount_plc, w.PlcAmount);
                                        }
                                        else
                                        {
                                            eat_amount = Math.Min(eat_amount, Math.Min(max_eat - eat_amount_plc, w.PlcAmount));
                                        }
                                    }
                                }

                                if (eat_amount > 0)
                                {
                                    eat_amount = Math.Round(eat_amount / WP_STEP) * WP_STEP;
                                    if (eat_amount > 0)
                                    {
                                        InvestRecordWp ir = new InvestRecordWp()
                                        {
                                            TimeKey = ToUnixTime(DateTime.Now),
                                            Model = MODEL,
                                            CardID = _card_id,
                                            RaceNo = _race_no,
                                            Direction = "EAT",
                                            HorseNo = h.No,
                                            Percent = w.Percent,
                                            WinLimit = w.WinLimit,
                                            PlcLimit = w.PlcLimit,
                                            FittingLoss = E
                                        };

                                        if (w.WinLimit > 0)
                                        {
                                            ir.WinAmount = eat_amount;
                                            ir.WinOdds = sp_w_max;
                                            ir.WinProbility = p1[i];
                                        }
                                        if (w.PlcLimit > 0)
                                        {
                                            ir.PlcAmount = eat_amount;
                                            ir.PlcOdds = sp_p_max;
                                            ir.PlcProbility = p3[i];
                                        }

                                        //wp_records.Add(ir);

                                        eat_amount_win += ir.WinAmount;
                                        eat_amount_plc += ir.PlcAmount;
                                    }
                                }
                            }
                        }

                    }

                    common.Math.Combination comb2 = new common.Math.Combination(_latest_odds.Count, 2);
                    int[][] combinations = comb2.GetCombinations();
                    for (int i = 0; i < combinations.Length; i++)
                    {
                        int[] c = combinations[i];
                        string horseNo = string.Format("{0}-{1}", _latest_odds[c[0]].No, _latest_odds[c[1]].No);

                        if (pq_win != null)
                        {
                            double sp_min = Math.Min(_latest_odds.SpQ[horseNo], _fitted_odds.SpQ[horseNo]);
                            double sp_max = Math.Max(_latest_odds.SpQ[horseNo], _fitted_odds.SpQ[horseNo]);

                            // For Bet
                            {
                                WaterQnList vlist = _latest_waters.GetQnEatWater(horseNo).GetValuableWater(MIN_R, sp_min, pq_win[i]);
                                double bet_amount = 0;
                                foreach (WaterQnItem w in vlist.OrderBy(x => x.Percent))
                                {
                                    double O = Math.Min(w.Limit / LIMIT_SCALE, sp_min) * 100 / w.Percent;
                                    double max_bet = (T * Math.Pow(O * pq_win[i] - 1, 2)) / (LOSS_RATE_COEFFICIENT * Math.Pow(O, 2) * pq_win[i] * (1 - pq_win[i]));

                                    if (bet_amount >= max_bet)
                                    {
                                        break;
                                    }

                                    double current_amount = Math.Min(max_bet - bet_amount, w.Amount);
                                    current_amount = Math.Round(current_amount / QN_STEP) * QN_STEP;
                                    if (current_amount > 0)
                                    {
                                        InvestRecordQn ir = new InvestRecordQn()
                                        {
                                            TimeKey = ToUnixTime(DateTime.Now),
                                            Model = MODEL,
                                            CardID = _card_id,
                                            RaceNo = _race_no,
                                            Direction = "BET",
                                            Type = "Q",
                                            HorseNo = horseNo,
                                            Percent = w.Percent,
                                            Amount = current_amount,
                                            Limit = w.Limit,
                                            Odds = sp_min,
                                            Probility = pq_win[i],
                                            FittingLoss = E
                                        };
                                        //qn_records.Add(ir);
                                        bet_amount += ir.Amount;
                                    }
                                }
                            }

                            // For Eat
                            {
                                WaterQnList vlist = _latest_waters.GetQnBetWater(horseNo).GetValuableWater(MIN_R, sp_max, pq_win[i]);
                                double eat_amount = 0;
                                foreach (WaterQnItem w in vlist.OrderByDescending(x => x.Percent))
                                {
                                    double O = 1 + w.Percent / 100 / Math.Min(w.Limit / LIMIT_SCALE, sp_max);
                                    double max_eat = (T * Math.Pow(O * (1 - pq_win[i]) - 1, 2)) / (LOSS_RATE_COEFFICIENT * Math.Pow(O, 2) * (1 - pq_win[i]) * pq_win[i]);
                                    max_eat = max_eat / Math.Min(w.Limit / LIMIT_SCALE, sp_max);
                                    if (eat_amount >= max_eat)
                                    {
                                        break;
                                    }

                                    double current_amount = Math.Min(max_eat - eat_amount, w.Amount);
                                    current_amount = Math.Round(current_amount / QN_STEP) * QN_STEP;
                                    if (current_amount > 0)
                                    {
                                        InvestRecordQn ir = new InvestRecordQn()
                                        {
                                            TimeKey = ToUnixTime(DateTime.Now),
                                            Model = MODEL,
                                            CardID = _card_id,
                                            RaceNo = _race_no,
                                            Direction = "EAT",
                                            Type = "Q",
                                            HorseNo = horseNo,
                                            Percent = w.Percent,
                                            Amount = current_amount,
                                            Limit = w.Limit,
                                            Odds = sp_max,
                                            Probility = pq_win[i],
                                            FittingLoss = E
                                        };
                                        //qn_records.Add(ir);
                                        eat_amount += ir.Amount;
                                    }
                                }
                            }
                        }

                        if (pq_plc != null)
                        {
                            double sp_min = Math.Min(_latest_odds.SpQp[horseNo], _fitted_odds.SpQp[horseNo]);
                            double sp_max = Math.Max(_latest_odds.SpQp[horseNo], _fitted_odds.SpQp[horseNo]);

                            // For Bet
                            {
                                WaterQnList vlist = _latest_waters.GetQpEatWater(horseNo).GetValuableWater(MIN_R, sp_min, pq_plc[i]);
                                double bet_amount = 0;
                                foreach (WaterQnItem w in vlist.OrderBy(x => x.Percent))
                                {
                                    double O = Math.Min(w.Limit / LIMIT_SCALE, sp_min) * 100 / w.Percent;
                                    double max_bet = (T * Math.Pow(O * pq_plc[i] - 1, 2)) / (LOSS_RATE_COEFFICIENT * Math.Pow(O, 2) * pq_plc[i] * (1 - pq_plc[i]));

                                    if (bet_amount >= max_bet)
                                    {
                                        break;
                                    }

                                    double current_amount = Math.Min(max_bet - bet_amount, w.Amount);
                                    current_amount = Math.Round(current_amount / QN_STEP) * QN_STEP;
                                    if (current_amount > 0)
                                    {
                                        InvestRecordQn ir = new InvestRecordQn()
                                        {
                                            TimeKey = ToUnixTime(DateTime.Now),
                                            Model = MODEL,
                                            CardID = _card_id,
                                            RaceNo = _race_no,
                                            Direction = "BET",
                                            Type = "QP",
                                            HorseNo = horseNo,
                                            Percent = w.Percent,
                                            Amount = current_amount,
                                            Limit = w.Limit,
                                            Odds = sp_min,
                                            Probility = pq_plc[i],
                                            FittingLoss = E
                                        };
                                        //qn_records.Add(ir);
                                        bet_amount += ir.Amount;
                                    }
                                }
                            }

                            // For Eat
                            {
                                WaterQnList vlist = _latest_waters.GetQpBetWater(horseNo).GetValuableWater(MIN_R, sp_max, pq_win[i]);
                                double eat_amount = 0;
                                foreach (WaterQnItem w in vlist.OrderByDescending(x => x.Percent))
                                {
                                    double O = 1 + w.Percent / 100 / Math.Min(w.Limit / LIMIT_SCALE, sp_max);
                                    double max_eat = (T * Math.Pow(O * (1 - pq_plc[i]) - 1, 2)) / (LOSS_RATE_COEFFICIENT * Math.Pow(O, 2) * (1 - pq_plc[i]) * pq_plc[i]);
                                    max_eat = max_eat / Math.Min(w.Limit / LIMIT_SCALE, sp_max);
                                    if (eat_amount >= max_eat)
                                    {
                                        break;
                                    }

                                    double current_amount = Math.Min(max_eat - eat_amount, w.Amount);
                                    current_amount = Math.Round(current_amount / QN_STEP) * QN_STEP;
                                    if (current_amount > 0)
                                    {
                                        InvestRecordQn ir = new InvestRecordQn()
                                        {
                                            TimeKey = ToUnixTime(DateTime.Now),
                                            Model = MODEL,
                                            CardID = _card_id,
                                            RaceNo = _race_no,
                                            Direction = "EAT",
                                            Type = "QP",
                                            HorseNo = horseNo,
                                            Percent = w.Percent,
                                            Amount = current_amount,
                                            Limit = w.Limit,
                                            Odds = sp_max,
                                            Probility = pq_plc[i],
                                            FittingLoss = E
                                        };
                                        // qn_records.Add(ir);
                                        eat_amount += ir.Amount;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            private double cross_entropy(double[] p, double[] q)
            {
                if (p == null && q == null) return 0;
                if (p == null || q == null) return double.MaxValue;     // 一项为空另一项不为空，返回无穷大
                if (p.Length != q.Length) return double.MaxValue;       // 两项长度不一，代表马有退出，返回无穷大

                double E = 0;
                for (int i = 0; i < p.Length; i++)
                {
                    E += -p[i] * Math.Log(q[i]);
                }
                return E;
            }

            private bool updateWpOddsData()
            {
                string url, rawInfo;
                url = string.Format("http://120.24.210.35:3000/data/market/get_tote_wp_latest_raw_info?race_id={0}&card_id={1}", _race_id, _card_id);
                rawInfo = this.getRawInfoByAPI(url);

                HrsTable table = new HrsTable();
                if (rawInfo != null)
                {
                    JArray ja = (JArray)JsonConvert.DeserializeObject(rawInfo);
                    foreach (JObject jo in ja)
                    {
                        double win = (double)jo["win"];
                        double plc = (double)jo["plc"];
                        if (win != 0 && plc != 0)
                        {
                            table.Add(new Hrs(jo["horseNo"].ToString(), win, plc));
                        }
                    }
                    if (_fitted_odds != null)
                    {
                        // 复制最后的拟合结果
                        for (int i = 0, j = 0; i < _fitted_odds.Count && j < table.Count;)
                        {
                            if (_fitted_odds[i].No == table[j].No)
                            {
                                table[j].Mean = _fitted_odds[i].Mean;
                                table[j].Var = _fitted_odds[i].Var;
                                i++;
                                j++;
                            }
                            else
                            {
                                int fno = int.Parse(_fitted_odds[i].No);
                                int tno = int.Parse(table[j].No);

                                if (fno < tno)
                                {
                                    i++;
                                }
                                else
                                {
                                    j++;
                                }
                            }

                        }
                    }

                    _latest_odds = table;

                    return true;
                }
                else
                {
                    return false;
                }
            }

            private bool updateQnOddsData()
            {
                string url, rawInfo;
                url = string.Format("http://120.24.210.35:3000/data/market/get_tote_qn_latest_raw_info?race_id={0}&card_id={1}", _race_id, _card_id);
                rawInfo = this.getRawInfoByAPI(url);

                if (rawInfo != null)
                {
                    JObject jo = (JObject)JsonConvert.DeserializeObject(rawInfo);
                    this.parseQnTote((string)jo["text_q"], _latest_odds.SpQ);
                    this.parseQnTote((string)jo["text_qp"], _latest_odds.SpQp);

                    return true;
                }
                else
                {
                    return false;
                }
            }

            private bool updateBetWpWatersData()
            {
                string url, rawInfo;
                url = string.Format("http://120.24.210.35:3000/data/market/get_discount_wp_latest_raw_info?card_id={0}&direction=0", _card_id);
                rawInfo = this.getRawInfoByAPI(url);

                if (rawInfo != null)
                {
                    this.parseWpDiscount(rawInfo, "0");

                    return true;
                }
                else
                {
                    return false;
                }
            }

            private bool updateEatWpWatersData()
            {
                string url, rawInfo;
                url = string.Format("http://120.24.210.35:3000/data/market/get_discount_wp_latest_raw_info?card_id={0}&direction=1", _card_id);
                rawInfo = this.getRawInfoByAPI(url);

                if (rawInfo != null)
                {
                    this.parseWpDiscount(rawInfo, "1");

                    return true;
                }
                else
                {
                    return false;
                }
            }

            private bool updateBetQnWatersData()
            {
                string url, rawInfo;
                url = string.Format("http://120.24.210.35:3000/data/market/get_discount_qn_latest_raw_info?card_id={0}&type=Q&direction=0", _card_id);
                rawInfo = this.getRawInfoByAPI(url);

                if (rawInfo != null)
                {
                    this.parseQnDiscount(rawInfo, "0", "Q");

                    return true;
                }
                else
                {
                    return false;
                }
            }

            private bool updateEatQnWatersData()
            {
                string url, rawInfo;
                url = string.Format("http://120.24.210.35:3000/data/market/get_discount_qn_latest_raw_info?card_id={0}&type=Q&direction=1", _card_id);
                rawInfo = this.getRawInfoByAPI(url);

                if (rawInfo != null)
                {
                    this.parseQnDiscount(rawInfo, "1", "Q");

                    return true;
                }
                else
                {
                    return false;
                }
            }

            private bool updateBetQpWatersData()
            {
                string url, rawInfo;
                url = string.Format("http://120.24.210.35:3000/data/market/get_discount_qn_latest_raw_info?card_id={0}&type=QP&direction=0", _card_id);
                rawInfo = this.getRawInfoByAPI(url);

                if (rawInfo != null)
                {
                    this.parseQnDiscount(rawInfo, "0", "QP");

                    return true;
                }
                else
                {
                    return false;
                }
            }

            private bool updateEatQpWatersData()
            {
                string url, rawInfo;
                url = string.Format("http://120.24.210.35:3000/data/market/get_discount_qn_latest_raw_info?card_id={0}&type=QP&direction=1", _card_id);
                rawInfo = this.getRawInfoByAPI(url);

                if (rawInfo != null)
                {
                    this.parseQnDiscount(rawInfo, "1", "QP");

                    return true;
                }
                else
                {
                    return false;
                }
            }

            private void parseQnTote(string text, Dictionary<string, double> dict)
            {
                text = Regex.Replace(text, @"^\s+", "", RegexOptions.Singleline);
                text = Regex.Replace(text, @"\s+$", "", RegexOptions.Singleline);
                string[] rows = text.Split('\n');
                int splitIndex = 0;
                bool bottom_end = false;
                for (int i = 1; i < rows.Length; i++)
                {
                    string[] segments = rows[i].Split('\t');

                    string hrs_1, hrs_2, hrs_key;
                    int j;

                    // 第二行获取分割索引
                    if (i == 1)
                    {
                        splitIndex = int.Parse(Regex.Replace(segments[1], @"^H(\d+)$", "$1"));
                    }

                    // 下方的赔率
                    if (!bottom_end)
                    {
                        for (j = 1; j < i; j++)
                        {
                            if (segments[j] == "")
                            {
                                bottom_end = true;
                                break;
                            }
                            else
                            {
                                hrs_1 = (splitIndex + j - 1).ToString();
                                hrs_2 = (splitIndex + i - 1).ToString();
                                hrs_key = hrs_1 + "-" + hrs_2;

                                if (segments[j] != "SCR")
                                {
                                    Match m = Regex.Match(segments[j], @"^(?:\[[123]\])?(\d+(?:\.\d+)?)$");
                                    if (m.Success)
                                    {
                                        dict.Add(hrs_key, double.Parse(m.Groups[1].Value));
                                    }
                                }
                            }
                        }
                    }

                    // 右方的赔率
                    for (j = i + 2; j < segments.Length - 1; j++)
                    {
                        if (segments[j] == "") break;

                        hrs_1 = i.ToString();
                        hrs_2 = (j - 1).ToString();
                        hrs_key = hrs_1 + "-" + hrs_2;
                        if (segments[j] != "SCR")
                        {
                            Match m = Regex.Match(segments[j], @"^(?:\[[123]\])?(\d+(?:\.\d+)?)$");
                            if (m.Success)
                            {
                                dict.Add(hrs_key, double.Parse(m.Groups[1].Value));
                            }
                        }
                    }
                }
            }

            private void parseWpDiscount(string text, string direction)
            {
                foreach (string line in text.Split('\n'))
                {
                    if (line.Length > 0)
                    {
                        string[] elements = line.Split('\t');
                        int raceNo = int.Parse(elements[0]);
                        if (raceNo != _race_no) continue;

                        Match mLimit = Regex.Match(elements[5], @"^(!)?(\d+(?:\.\d+)?)\/(\d+(?:\.\d+)?)$");

                        string horseNo = elements[1];
                        WaterWP water;
                        if (direction == "0")
                        {
                            water = _latest_waters.GetWpBetWater(horseNo);
                        }
                        else
                        {
                            water = _latest_waters.GetWpEatWater(horseNo);
                        }

                        water.Add(new WaterWPItem()
                        {
                            Percent = double.Parse(elements[4]),
                            WinAmount = double.Parse(elements[2]),
                            WinLimit = double.Parse(mLimit.Groups[2].Value),
                            PlcAmount = double.Parse(elements[3]),
                            PlcLimit = double.Parse(mLimit.Groups[3].Value)
                        });
                    }
                }
            }

            private void parseQnDiscount(string text, string direction, string type)
            {
                foreach (string line in text.Split('\n'))
                {
                    if (line.Length > 0)
                    {
                        string[] elements = line.Split('\t');
                        int raceNo = int.Parse(elements[0]);
                        if (raceNo != _race_no) continue;

                        string horseNo = Regex.Replace(elements[1], @"^\((\d+\-\d+)\)$", "$1");
                        WaterQn water;
                        if (type == "Q")
                        {
                            if (direction == "0")
                            {
                                water = _latest_waters.GetQnBetWater(horseNo);
                            }
                            else
                            {
                                water = _latest_waters.GetQnEatWater(horseNo);
                            }
                        }
                        else
                        {
                            if (direction == "0")
                            {
                                water = _latest_waters.GetQpBetWater(horseNo);
                            }
                            else
                            {
                                water = _latest_waters.GetQpEatWater(horseNo);
                            }
                        }

                        water.Add(new WaterQnItem()
                        {
                            Percent = double.Parse(elements[3]),
                            Amount = double.Parse(elements[2]),
                            Limit = double.Parse(elements[4])
                        });
                    }
                }
            }

            private string getRawInfoByAPI(string url)
            {
                System.Net.WebClient wc = new System.Net.WebClient();
                using (System.IO.Stream s = wc.OpenRead(url))
                {
                    using (System.IO.StreamReader sr = new System.IO.StreamReader(s))
                    {
                        JObject jo = (JObject)JsonConvert.DeserializeObject(sr.ReadToEnd());
                        if ((string)jo["STS"] == "OK")
                        {
                            return (string)jo["data"];
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
        }

    }
}

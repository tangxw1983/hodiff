using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Threading;

namespace HO偏差
{
    public partial class FormTestBet : Form
    {
        public FormTestBet()
        {
            InitializeComponent();
        }

        private void btnTest_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.InitialDirectory = Application.StartupPath;
                dlg.Multiselect = true;

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    this.btnTest.Enabled = false;

                    Thread t = new Thread(delegate(object args)
                    {
                        string[] filenames = (string[])args;
                        foreach (string filename in dlg.FileNames)
                        {
                            this.Invoke(new MethodInvoker(delegate
                            {
                                this.txtLog.AppendText(string.Format("{0:HH:mm:ss} > file {1} testing...\r\n", DateTime.Now, filename));
                            }));
                            this.handle(filename);
                        }

                        this.Invoke(new MethodInvoker(delegate
                        {
                            this.txtLog.AppendText(string.Format("{0:HH:mm:ss} > all finished\r\n", DateTime.Now));
                            this.btnTest.Enabled = true;
                        }));
                    });
                    t.Start(dlg.FileNames);
                }
            }
        }

        private const double MIN_R = 1.1;
        private const double T = 10000;
        private const double LOSS_RATE_COEFFICIENT = 5.43;
        private const double WP_STEP = 5;
        private const double QN_STEP = 10;
        private const double LIMIT_SCALE = 10;

        class InvestRecordWp
        {
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
        }

        class InvestRecordQn
        {
            public string Direction { get; set; }
            public string Type { get; set; }
            public string HorseNo { get; set; }
            public double Percent { get; set; }
            public double Amount { get; set; }
            public double Limit { get; set; }
            public double Odds { get; set; }
            public double Probility { get; set; }
        }

        private void handle(string filename)
        {
            RaceData race = RaceData.Load(filename);
            RaceDataItem item = race.First().Value;
            double[] p1, p3, pq_win, pq_plc;
            Fitting.calcProbility(item.Odds, out p1, out p3, out pq_win, out pq_plc);
            
            //// 根据概率计算各项投注的最低Odds
            //double[] o1 = p1.Select(x => MIN_R / x).ToArray();
            //double[] o3 = p3.Select(x => MIN_R / x).ToArray();
            //double[] oq_win = pq_win == null ? null : pq_win.Select(x => MIN_R / x).ToArray();
            //double[] oq_plc = pq_plc == null ? null : pq_plc.Select(x => MIN_R / x).ToArray();

            List<InvestRecordWp> wp_records = new List<InvestRecordWp>();
            for(int i=0;i<item.Odds.Count;i++)
            {
                Hrs h = item.Odds[i];

                // For Bet
                {
                    WaterWPList vlist = item.Waters.GetWpEatWater(h.No).GetValuableWater(MIN_R, h.Win, p1[i], h.Plc, p3[i]);
                    double bet_amount_win = 0, bet_amount_plc = 0;
                    bool full_win = false, full_plc = false;
                    foreach (WaterWPItem w in vlist.OrderBy(x => x.Percent))
                    {
                        if (w.WinAmount > 0 && full_win) continue;
                        if (w.PlcAmount > 0 && full_plc) continue;

                        double bet_amount = -1;

                        if (w.WinAmount > 0)
                        {
                            double O = Math.Min(w.WinLimit / LIMIT_SCALE, h.Win) * 100 / w.Percent;
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
                            double O = Math.Min(w.PlcLimit / LIMIT_SCALE, h.Plc) * 100 / w.Percent;
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
                                    Direction = "BET",
                                    HorseNo = h.No,
                                    Percent = w.Percent,
                                    WinAmount = Math.Min(bet_amount, w.WinAmount),   // 这里的Min目的是适用单Win或单Plc，另一个为0的时候
                                    WinLimit = w.WinLimit,
                                    WinOdds = h.Win,
                                    WinProbility = p1[i],
                                    PlcAmount = Math.Min(bet_amount, w.PlcAmount),
                                    PlcLimit = w.PlcLimit,
                                    PlcOdds = h.Plc,
                                    PlcProbility = p3[i]
                                };

                                wp_records.Add(ir);

                                bet_amount_win += ir.WinAmount;
                                bet_amount_plc += ir.PlcAmount;
                            }
                        }
                    }
                }

                // For Eat
                {
                    WaterWPList vlist = item.Waters.GetWpBetWater(h.No).GetValuableWater(MIN_R, h.Win, p1[i], h.Plc, p3[i]);
                    double eat_amount_win = 0, eat_amount_plc = 0;
                    bool full_win = false, full_plc = false;
                    foreach (WaterWPItem w in vlist.OrderByDescending(x => x.Percent))
                    {
                        if (w.WinAmount > 0 && full_win) continue;
                        if (w.PlcAmount > 0 && full_plc) continue;

                        double eat_amount = -1;

                        if (w.WinAmount > 0)
                        {
                            double O = 1 + w.Percent / 100 / Math.Min(w.WinLimit / LIMIT_SCALE, h.Win);
                            double max_eat = (T * Math.Pow(O * (1 - p1[i]) - 1, 2)) / (LOSS_RATE_COEFFICIENT * Math.Pow(O, 2) * (1 - p1[i]) * p1[i]);
                            max_eat = max_eat / Math.Min(w.WinLimit / LIMIT_SCALE, h.Win);
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
                            double O = 1 + w.Percent / 100 / Math.Min(w.PlcLimit / LIMIT_SCALE, h.Plc);
                            double max_eat = (T * Math.Pow(O * (1 - p3[i]) - 1, 2)) / (LOSS_RATE_COEFFICIENT * Math.Pow(O, 2) * (1 - p3[i]) * p3[i]);
                            max_eat = max_eat / Math.Min(w.PlcLimit / LIMIT_SCALE, h.Plc);
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
                                    Direction = "EAT",
                                    HorseNo = h.No,
                                    Percent = w.Percent,
                                    WinAmount = Math.Min(eat_amount, w.WinAmount),
                                    WinLimit = w.WinLimit,
                                    WinOdds = h.Win,
                                    WinProbility = p1[i],
                                    PlcAmount = Math.Min(eat_amount, w.PlcAmount),
                                    PlcLimit = w.PlcLimit,
                                    PlcOdds = h.Plc,
                                    PlcProbility = p3[i]
                                };

                                wp_records.Add(ir);

                                eat_amount_win += ir.WinAmount;
                                eat_amount_plc += ir.PlcAmount;
                            }
                        }
                    }
                }
                
            }

            List<InvestRecordQn> qn_records = new List<InvestRecordQn>();
            common.Math.Combination comb2 = new common.Math.Combination(item.Odds.Count, 2);
            int[][] combinations = comb2.GetCombinations();
            for (int i = 0; i < combinations.Length; i++)
            {
                int[] c = combinations[i];
                string horseNo = string.Format("{0}-{1}", item.Odds[c[0]].No, item.Odds[c[1]].No);

                if (pq_win != null)
                {
                    double sp = item.Odds.SpQ[horseNo];

                    // For Bet
                    {
                        WaterQnList vlist = item.Waters.GetQnEatWater(horseNo).GetValuableWater(MIN_R, sp, pq_win[i]);
                        double bet_amount = 0;
                        foreach (WaterQnItem w in vlist.OrderBy(x => x.Percent))
                        {
                            double O = Math.Min(w.Limit / LIMIT_SCALE, sp) * 100 / w.Percent;
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
                                    Direction = "BET",
                                    Type = "Q",
                                    HorseNo = horseNo,
                                    Percent = w.Percent,
                                    Amount = current_amount,
                                    Limit = w.Limit,
                                    Odds = sp,
                                    Probility = pq_win[i]
                                };
                                qn_records.Add(ir);
                                bet_amount += ir.Amount;
                            }
                        }
                    }

                    // For Eat
                    {
                        WaterQnList vlist = item.Waters.GetQnBetWater(horseNo).GetValuableWater(MIN_R, sp, pq_win[i]);
                        double eat_amount = 0;
                        foreach (WaterQnItem w in vlist.OrderByDescending(x => x.Percent))
                        {
                            double O = 1 + w.Percent / 100 / Math.Min(w.Limit / LIMIT_SCALE, sp);
                            double max_eat = (T * Math.Pow(O * (1 - pq_win[i]) - 1, 2)) / (LOSS_RATE_COEFFICIENT * Math.Pow(O, 2) * (1 - pq_win[i]) * pq_win[i]);
                            max_eat = max_eat / Math.Min(w.Limit / LIMIT_SCALE, sp);
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
                                    Direction = "EAT",
                                    Type = "Q",
                                    HorseNo = horseNo,
                                    Percent = w.Percent,
                                    Amount = current_amount,
                                    Limit = w.Limit,
                                    Odds = sp,
                                    Probility = pq_win[i]
                                };
                                qn_records.Add(ir);
                                eat_amount += ir.Amount;
                            }
                        }
                    }
                }

                if (pq_plc != null)
                {
                    double sp = item.Odds.SpQp[horseNo];

                    // For Bet
                    {
                        WaterQnList vlist = item.Waters.GetQpEatWater(horseNo).GetValuableWater(MIN_R, sp, pq_plc[i]);
                        double bet_amount = 0;
                        foreach (WaterQnItem w in vlist.OrderBy(x => x.Percent))
                        {
                            double O = Math.Min(w.Limit / LIMIT_SCALE, sp) * 100 / w.Percent;
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
                                    Direction = "BET",
                                    Type = "QP",
                                    HorseNo = horseNo,
                                    Percent = w.Percent,
                                    Amount = current_amount,
                                    Limit = w.Limit,
                                    Odds = sp,
                                    Probility = pq_plc[i]
                                };
                                qn_records.Add(ir);
                                bet_amount += ir.Amount;
                            }
                        }
                    }

                    // For Eat
                    {
                        WaterQnList vlist = item.Waters.GetQpBetWater(horseNo).GetValuableWater(MIN_R, sp, pq_win[i]);
                        double eat_amount = 0;
                        foreach (WaterQnItem w in vlist.OrderByDescending(x => x.Percent))
                        {
                            double O = 1 + w.Percent / 100 / Math.Min(w.Limit / LIMIT_SCALE, sp);
                            double max_eat = (T * Math.Pow(O * (1 - pq_plc[i]) - 1, 2)) / (LOSS_RATE_COEFFICIENT * Math.Pow(O, 2) * (1 - pq_plc[i]) * pq_plc[i]);
                            max_eat = max_eat / Math.Min(w.Limit / LIMIT_SCALE, sp);
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
                                    Direction = "EAT",
                                    Type = "QP",
                                    HorseNo = horseNo,
                                    Percent = w.Percent,
                                    Amount = current_amount,
                                    Limit = w.Limit,
                                    Odds = sp,
                                    Probility = pq_plc[i]
                                };
                                qn_records.Add(ir);
                                eat_amount += ir.Amount;
                            }
                        }
                    }
                }
            }

            using (FileStream fs = new FileStream(filename + ".wp.log", FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                fs.SetLength(0);
                using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
                {
                    foreach (InvestRecordWp ir in wp_records)
                    {
                        sw.WriteLine(string.Format("{0},{1},{2},,{3},{4},{5},{6},,{7},{8},{9},{10}", ir.Direction, ir.HorseNo, ir.Percent, ir.WinAmount, ir.WinLimit, ir.WinOdds, ir.WinProbility, ir.PlcAmount, ir.PlcLimit, ir.PlcOdds, ir.PlcProbility));
                    }
                }
            }

            using (FileStream fs = new FileStream(filename + ".qn.log", FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                fs.SetLength(0);
                using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
                {
                    foreach (InvestRecordQn ir in qn_records)
                    {
                        sw.WriteLine(string.Format("{0},{1},{2},{3},,{4},{5},{6},{7}", ir.Direction, ir.HorseNo, ir.Type, ir.Percent, ir.Amount, ir.Limit, ir.Odds, ir.Probility));
                    }
                }
            }
        }
    }
}

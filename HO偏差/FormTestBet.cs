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
using MySql.Data.MySqlClient;

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
            public long TimeKey { get; set; }
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

        private void handle(string filename)
        {
            RaceData race = RaceData.Load(filename);
            long tp = race.First().Key;
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
                                    TimeKey = tp,
                                    CardID = race.CardID,
                                    RaceNo = race.RaceNo,
                                    Direction = "BET",
                                    HorseNo = h.No,
                                    Percent = w.Percent,
                                    WinLimit = w.WinLimit,
                                    PlcLimit = w.PlcLimit,
                                    FittingLoss = item.Odds.E
                                };

                                if (w.WinLimit > 0)
                                {
                                    ir.WinAmount = bet_amount;
                                    ir.WinOdds = h.Win;
                                    ir.WinProbility = p1[i];
                                }
                                if (w.PlcLimit > 0)
                                {
                                    ir.PlcAmount = bet_amount;
                                    ir.PlcOdds = h.Plc;
                                    ir.PlcProbility = p3[i];
                                }

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
                                    TimeKey = tp,
                                    CardID = race.CardID,
                                    RaceNo = race.RaceNo,
                                    Direction = "EAT",
                                    HorseNo = h.No,
                                    Percent = w.Percent,
                                    WinLimit = w.WinLimit,
                                    PlcLimit = w.PlcLimit,
                                    FittingLoss = item.Odds.E
                                };

                                if (w.WinLimit > 0)
                                {
                                    ir.WinAmount = eat_amount;
                                    ir.WinOdds = h.Win;
                                    ir.WinProbility = p1[i];
                                }
                                if (w.PlcLimit > 0)
                                {
                                    ir.PlcAmount = eat_amount;
                                    ir.PlcOdds = h.Plc;
                                    ir.PlcProbility = p3[i];
                                }

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
                                    TimeKey = tp,
                                    CardID = race.CardID,
                                    RaceNo = race.RaceNo,
                                    Direction = "BET",
                                    Type = "Q",
                                    HorseNo = horseNo,
                                    Percent = w.Percent,
                                    Amount = current_amount,
                                    Limit = w.Limit,
                                    Odds = sp,
                                    Probility = pq_win[i],
                                    FittingLoss = item.Odds.E
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
                                    TimeKey = tp,
                                    CardID = race.CardID,
                                    RaceNo = race.RaceNo,
                                    Direction = "EAT",
                                    Type = "Q",
                                    HorseNo = horseNo,
                                    Percent = w.Percent,
                                    Amount = current_amount,
                                    Limit = w.Limit,
                                    Odds = sp,
                                    Probility = pq_win[i],
                                    FittingLoss = item.Odds.E
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
                                    TimeKey = tp,
                                    CardID = race.CardID,
                                    RaceNo = race.RaceNo,
                                    Direction = "BET",
                                    Type = "QP",
                                    HorseNo = horseNo,
                                    Percent = w.Percent,
                                    Amount = current_amount,
                                    Limit = w.Limit,
                                    Odds = sp,
                                    Probility = pq_plc[i],
                                    FittingLoss = item.Odds.E
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
                                    TimeKey = tp,
                                    CardID = race.CardID,
                                    RaceNo = race.RaceNo,
                                    Direction = "EAT",
                                    Type = "QP",
                                    HorseNo = horseNo,
                                    Percent = w.Percent,
                                    Amount = current_amount,
                                    Limit = w.Limit,
                                    Odds = sp,
                                    Probility = pq_plc[i],
                                    FittingLoss = item.Odds.E
                                };
                                qn_records.Add(ir);
                                eat_amount += ir.Amount;
                            }
                        }
                    }
                }
            }

            using (MySqlConnection conn = new MySqlConnection("server=120.24.210.35;user id=hrsdata;password=abcd0000;database=hrsdata;port=3306;charset=utf8"))
            {
                conn.Open();

                using (MySqlCommand cmd = new MySqlCommand(@"
insert into sl_invest_wp (time_key,cd_id,rc_no,direction,hs_no,percent,w_limit,p_limit,fitting_loss,w_amt,w_od,w_prob,p_amt,p_od,p_prob)
values (?time_key,?cd_id,?rc_no,?direction,?hs_no,?percent,?w_limit,?p_limit,?fitting_loss,?w_amt,?w_od,?w_prob,?p_amt,?p_od,?p_prob)
on duplicate key update fitting_loss=?fitting_loss,w_amt=?w_amt,w_od=?w_od,w_prob=?w_prob,p_amt=?p_amt,p_od=?p_od,p_prob=?p_prob,lmt=CURRENT_TIMESTAMP()
", conn))
                {
                    cmd.Parameters.Add("?time_key", MySqlDbType.Int64);
                    cmd.Parameters.Add("?cd_id", MySqlDbType.UInt64);
                    cmd.Parameters.Add("?rc_no", MySqlDbType.Int32);
                    cmd.Parameters.Add("?direction", MySqlDbType.VarChar, 10);
                    cmd.Parameters.Add("?hs_no", MySqlDbType.Int32);
                    cmd.Parameters.Add("?percent", MySqlDbType.Decimal);
                    cmd.Parameters.Add("?w_limit", MySqlDbType.Decimal);
                    cmd.Parameters.Add("?p_limit", MySqlDbType.Decimal);
                    cmd.Parameters.Add("?fitting_loss", MySqlDbType.Decimal);
                    cmd.Parameters.Add("?w_amt", MySqlDbType.Decimal);
                    cmd.Parameters.Add("?w_od", MySqlDbType.Decimal);
                    cmd.Parameters.Add("?w_prob", MySqlDbType.Decimal);
                    cmd.Parameters.Add("?p_amt", MySqlDbType.Decimal);
                    cmd.Parameters.Add("?p_od", MySqlDbType.Decimal);
                    cmd.Parameters.Add("?p_prob", MySqlDbType.Decimal);

                    foreach (InvestRecordWp ir in wp_records)
                    {
                        cmd.Parameters["?time_key"].Value = ir.TimeKey;
                        cmd.Parameters["?cd_id"].Value = ir.CardID;
                        cmd.Parameters["?rc_no"].Value = ir.RaceNo;
                        cmd.Parameters["?direction"].Value = ir.Direction;
                        cmd.Parameters["?hs_no"].Value = int.Parse(ir.HorseNo);
                        cmd.Parameters["?percent"].Value = ir.Percent;
                        cmd.Parameters["?w_limit"].Value = ir.WinLimit;
                        cmd.Parameters["?p_limit"].Value = ir.PlcLimit;
                        cmd.Parameters["?fitting_loss"].Value = ir.FittingLoss;
                        cmd.Parameters["?w_amt"].Value = ir.WinAmount;
                        cmd.Parameters["?w_od"].Value = ir.WinOdds;
                        cmd.Parameters["?w_prob"].Value = ir.WinProbility;
                        cmd.Parameters["?p_amt"].Value = ir.PlcAmount;
                        cmd.Parameters["?p_od"].Value = ir.PlcOdds;
                        cmd.Parameters["?p_prob"].Value = ir.PlcProbility;

                        cmd.ExecuteNonQuery();
                    }
                }

                using (MySqlCommand cmd = new MySqlCommand(@"
insert into sl_invest_qn(time_key,cd_id,rc_no,direction,q_type,hs_no,percent,limit,fitting_loss,amt,od,prob)
values (?time_key,?cd_id,?rc_no,?direction,?q_type,?hs_no,?percent,?limit,?fitting_loss,?amt,?od,?prob)
on duplicate key update fitting_loss=?fitting_loss,amt=?amt,od=?od,prob=?prob,lmt=CURRENT_TIMESTAMP()
", conn))
                {
                    cmd.Parameters.Add("?time_key", MySqlDbType.Int64);
                    cmd.Parameters.Add("?cd_id", MySqlDbType.UInt64);
                    cmd.Parameters.Add("?rc_no", MySqlDbType.Int32);
                    cmd.Parameters.Add("?direction", MySqlDbType.VarChar, 10);
                    cmd.Parameters.Add("?q_type", MySqlDbType.VarChar, 10);
                    cmd.Parameters.Add("?hs_no", MySqlDbType.VarChar, 20);
                    cmd.Parameters.Add("?percent", MySqlDbType.Decimal);
                    cmd.Parameters.Add("?limit", MySqlDbType.Decimal);
                    cmd.Parameters.Add("?fitting_loss", MySqlDbType.Decimal);
                    cmd.Parameters.Add("?amt", MySqlDbType.Decimal);
                    cmd.Parameters.Add("?od", MySqlDbType.Decimal);
                    cmd.Parameters.Add("?prob", MySqlDbType.Decimal);

                    foreach (InvestRecordQn ir in qn_records)
                    {
                        cmd.Parameters["?time_key"].Value = ir.TimeKey;
                        cmd.Parameters["?cd_id"].Value = ir.CardID;
                        cmd.Parameters["?rc_no"].Value = ir.RaceNo;
                        cmd.Parameters["?direction"].Value = ir.Direction;
                        cmd.Parameters["?q_type"].Value = ir.Type;
                        cmd.Parameters["?hs_no"].Value = ir.HorseNo;
                        cmd.Parameters["?percent"].Value = ir.Percent;
                        cmd.Parameters["?limit"].Value = ir.Limit;
                        cmd.Parameters["?fitting_loss"].Value = ir.FittingLoss;
                        cmd.Parameters["?amt"].Value = ir.Amount;
                        cmd.Parameters["?od"].Value = ir.Odds;
                        cmd.Parameters["?prob"].Value = ir.Probility;

                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}

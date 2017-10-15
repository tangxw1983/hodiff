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

        private List<RaceHandler> _handlers = new List<RaceHandler>();

        private void btnStart_Click(object sender, EventArgs e)
        {
            this.btnStart.Enabled = false;
            using (MySqlConnection conn = new MySqlConnection("server=120.24.210.35;user id=hrsdata;password=abcd0000;database=hrsdata;port=3306;charset=utf8"))
            {
                conn.Open();

                string sql = @"
SELECT a.*, b.`id` card_id, b.tote_type, b.card_char, c.country, c.city, d.* FROM ct_race a
INNER JOIN ct_card b ON b.`tournament_id` = a.`tournament_id`
INNER JOIN ct_tournament_loc c on c.loc_id = a.race_loc
INNER JOIN sg_odds_deviation_setting d on d.loc_id = a.race_loc
WHERE a.time_text IS NOT NULL
AND a.race_date = ?race_date";

                sql += " AND a.`race_loc` in ('" + string.Join("','", iptRaceLoc.Text.Split(',')) + "')";
                sql += " AND b.`tote_type` in ('" + string.Join("','", iptToteType.Text.Split(',')) + "')";

                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("?race_date", MySqlDbType.VarChar, 50).Value = string.Format("{0:dd-MM-yyyy}", iptRaceDate.Value);

                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            string date_str = (string)dr["race_date"];
                            string time_str = (string)dr["time_text"];

                            Match md = Regex.Match(date_str, @"^(\d{2})-(\d{2})-(\d{4})$");
                                // Match mt = Regex.Match(time_str, @"^(\d{2})\:(\d{2})(pm|am)$");
                            if (md.Success)
                            {
                                RaceHandler rh = new RaceHandler();
                                rh.StartTime = DateTime.Parse(string.Format("{0}-{1}-{2} {3}",
                                        md.Groups[3].Value,
                                        md.Groups[2].Value,
                                        md.Groups[1].Value,
                                    //mt.Groups[3].Value == "pm" ? int.Parse(mt.Groups[1].Value) + 12 : int.Parse(mt.Groups[1].Value),
                                    //mt.Groups[2].Value
                                        time_str));
                                if (rh.StartTime < DateTime.Now.AddMinutes(-10)) 
                                    continue;  // 十分钟之前开赛的忽略
                                rh.RaceID = (ulong)dr["id"];
                                rh.CardID = (ulong)dr["card_id"];
                                rh.RaceDate = (string)dr["race_date"];
                                rh.RaceLoc = (string)dr["race_loc"];
                                rh.RaceType = (string)dr["race_loc"] + (string)dr["card_char"];
                                rh.RaceNo = (int)dr["race_no"];
                                rh.RaceName = string.Format("({0}){1}-{2}/{3}", dr["country"], dr["city"], dr["race_no"], dr["tote_type"]);
                                rh.PLC_SPLIT_POS = (int)dr["data_plc_split_pos"];
                                rh.MIN_R = (double)((decimal)dr["risk_min_r"]);
                                rh.MAX_R = (double)((decimal)dr["risk_max_r"]);
                                rh.T = (double)((decimal)dr["risk_total_bet"]);
                                rh.LOSS_RATE_COEFFICIENT = (double)((decimal)dr["risk_loss_rate_coefficient"]);
                                rh.BET_MAX_DISCOUNT_WP = (double)((decimal)dr["risk_bet_max_discount_wp"]);
                                rh.EAT_MIN_DISCOUNT_WP = (double)((decimal)dr["risk_eat_min_discount_wp"]);
                                rh.BET_MAX_DISCOUNT_QN = (double)((decimal)dr["risk_bet_max_discount_qn"]);
                                rh.EAT_MIN_DISCOUNT_QN = (double)((decimal)dr["risk_eat_min_discount_qn"]);
                                rh.WP_STEP = (double)((decimal)dr["order_wp_step"]);
                                rh.QN_STEP = (double)((decimal)dr["order_qn_step"]);
                                rh.LIMIT_SCALE = (double)((decimal)dr["data_limit_scale"]);
                                rh.FitTimeInAdvance = (int)dr["strategy_fit_time_in_advance"];
                                rh.BetTimeInAdvance = (int)dr["strategy_bet_time_in_advance"];
                                rh.R_PLC = (double)((decimal)dr["data_r_plc"]);
                                rh.R_QP = (double)((decimal)dr["data_r_qp"]);
                                rh.ODDS_MODE = (int)dr["odds_mode"];
                                rh.startDaemon();
                                rh.Process += rh_Process;
                                _handlers.Add(rh);
                            }
                        }
                    }
                }
            }

            this.Log("main", "0", string.Format("加载获得{0}个比赛", _handlers.Count), null);
        }

        void rh_Process(FormBet.RaceHandler sender, FormBet.RaceProcessEventArgs e)
        {
            this.Log(sender.RaceName, ((int)sender.StartTime.Subtract(DateTime.Now).TotalSeconds).ToString(), e.Description, e.Detail);
        }

        void Log(string source, string countdown, string description, string detail)
        {
            lock (this)
            {
                using (System.IO.FileStream fs = new System.IO.FileStream(string.Format("{0:yyyy-MM-dd}.log", DateTime.Now), System.IO.FileMode.Append))
                {
                    using (System.IO.StreamWriter sw = new System.IO.StreamWriter(fs))
                    {
                        sw.WriteLine("{0:HH:mm:ss}:{1},{2},{3}", DateTime.Now, source, countdown, description);
                        if (detail != null)
                        {
                            sw.WriteLine(detail);
                        }
                    }
                }
            }
            this.Invoke(new MethodInvoker(delegate
            {
                lvwLog.SuspendLayout();
                ListViewItem lvi = lvwLog.Items.Add(string.Format("{0:HH:mm:ss}", DateTime.Now));
                lvi.SubItems.Add(source);
                lvi.SubItems.Add(countdown);
                lvi.SubItems.Add(description);
                lvwLog.EnsureVisible(lvi.Index);
                lvwLog.ResumeLayout();
            }));
        }

        class InvestRecordWp
        {
            public uint ID { get; set; }
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
            public double CloseAmount { get; set; }
            public bool CloseFlag { get; set; }
            public InvestRecordWp RefItem { get; set; }
            public uint OrderId { get; set; }
            public double CloseRiskIncWin { get; set; }
            public double CloseRiskIncPlc { get; set; }
        }

        class OrderWp
        {
            public OrderWp()
            {
                this.Records = new List<InvestRecordWp>();
            }

            public uint ID { get; set; }
            public ulong CardID { get; set; }
            public string RaceDate { get; set; }
            public string RaceType { get; set; }
            public int RaceNo { get; set; }
            public string Direction { get; set; }
            public string HorseNo { get; set; }
            public double Percent { get; set; }
            public double WinLimit { get; set; }
            public double PlcLimit { get; set; }
            public double Amount { get; set; }
            public List<InvestRecordWp> Records { get; private set; }
        }

        class InvestRecordQn
        {
            public uint ID { get; set; }
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
            public double CloseAmount { get; set; }
            public bool CloseFlag { get; set; }
            public InvestRecordQn RefItem { get; set; }
            public uint OrderId { get; set; }
            public double CloseRiskInc { get; set; }
        }

        class OrderQn
        {
            public OrderQn()
            {
                this.Records = new List<InvestRecordQn>();
            }

            public uint ID { get; set; }
            public ulong CardID { get; set; }
            public string RaceDate { get; set; }
            public string RaceType { get; set; }
            public int RaceNo { get; set; }
            public string Direction { get; set; }
            public string Type { get; set; }
            public string HorseNo { get; set; }
            public double Percent { get; set; }
            public double Limit { get; set; }
            public double Amount { get; set; }
            public List<InvestRecordQn> Records { get; private set; }
        }

        public class RaceProcessEventArgs : EventArgs
        {
            public string Description { get; set; }
            public string Detail { get; set; }
        }

        public delegate void RaceProcessEventHandler(RaceHandler sender, RaceProcessEventArgs e);

        public class RaceHandler
        {
            public RaceHandler()
            {
                this.FitTimeInAdvance = 300;    // 5分钟之前开始拟合
                this.BetTimeInAdvance = 20;     // 20秒前开始下单
            }

            private static Random __global_rand__ = new Random();

            public ulong RaceID { get; set; }
            public ulong CardID { get; set; }
            public string RaceDate { get; set; }
            public string RaceType { get; set; }
            public string RaceLoc { get; set; }
            public int RaceNo { get; set; }
            public DateTime StartTime { get; set; }

            public string RaceName { get; set; }

            /// <summary>
            /// 开始拟合的时间提前量(s)
            /// 预计开赛时间前多少秒开始拟合
            /// </summary>
            public int FitTimeInAdvance { get; set; }
            /// <summary>
            /// 开始下单的时间提前量(s)
            /// 预计开赛时间前多少秒开始下单
            /// </summary>
            public int BetTimeInAdvance { get; set; }
            public int PLC_SPLIT_POS { get; set; }
            /// <summary>
            /// PLC的预计赔付
            /// </summary>
            public double R_PLC { get; set; }
            /// <summary>
            /// QP的预计赔付
            /// </summary>
            public double R_QP { get; set; }

            private HrsTable _latest_odds;
            private HrsTable _fitted_odds;
            private WaterTable _latest_waters = new WaterTable();

            private int _stage;

            private Thread _tDaemon;
            private Thread _tFix;
            private Thread _tBet;

            private bool _bet_again = false;

            private Dictionary<string, double> _bet_amount_win = new Dictionary<string, double>();
            private Dictionary<string, double> _bet_amount_plc = new Dictionary<string, double>();
            private Dictionary<string, double> _bet_amount_qn = new Dictionary<string, double>();
            private Dictionary<string, double> _bet_amount_qp = new Dictionary<string, double>();
            private Dictionary<string, double> _close_risk_win = new Dictionary<string, double>();
            private Dictionary<string, double> _close_risk_plc = new Dictionary<string, double>();
            private Dictionary<string, double> _close_risk_qn = new Dictionary<string, double>();
            private Dictionary<string, double> _close_risk_qp = new Dictionary<string, double>();

            private Dictionary<string, List<InvestRecordWp>> _orders_bet_wp = new Dictionary<string, List<InvestRecordWp>>();
            private Dictionary<string, List<InvestRecordWp>> _orders_eat_wp = new Dictionary<string, List<InvestRecordWp>>();
            private Dictionary<string, List<InvestRecordQn>> _orders_bet_qn = new Dictionary<string, List<InvestRecordQn>>();
            private Dictionary<string, List<InvestRecordQn>> _orders_eat_qn = new Dictionary<string, List<InvestRecordQn>>();
            private Dictionary<string, List<InvestRecordQn>> _orders_bet_qp = new Dictionary<string, List<InvestRecordQn>>();
            private Dictionary<string, List<InvestRecordQn>> _orders_eat_qp = new Dictionary<string, List<InvestRecordQn>>();

            private List<InvestRecordWp> _batch_orders_wp = new List<InvestRecordWp>();
            private List<InvestRecordQn> _batch_orders_qn = new List<InvestRecordQn>();

            private const double E_THRESHOLD_SCALE = 1.1;
            public double MIN_R { get; set; }
            public double MAX_R { get; set; }
            public double BET_MAX_DISCOUNT_WP { get; set; }
            public double EAT_MIN_DISCOUNT_WP { get; set; }
            public double BET_MAX_DISCOUNT_QN { get; set; }
            public double EAT_MIN_DISCOUNT_QN { get; set; }
            public double T { get; set; }
            public double LOSS_RATE_COEFFICIENT { get; set; }
            public double WP_STEP { get; set; }
            public double QN_STEP { get; set; }
            public int ODDS_MODE { get; set; }
            public double LIMIT_SCALE { get; set; }
            private const int MODEL = 108;

            private static DateTime UNIXTIME_BASE = new DateTime(1970, 1, 1, 8, 0, 0);

            public event RaceProcessEventHandler Process;

            protected void OnProcess(RaceProcessEventArgs e)
            {
                if (Process != null)
                {
                    Process.Invoke(this, e);
                }
            }

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
                    _tDaemon.Name = string.Format("守护-{0}-{1}", this.CardID, this.RaceNo);
                    _tDaemon.IsBackground = true;
                    _tDaemon.Start();
                }
            }

            public void startBet()
            {
                if (_tBet == null)
                {
                    _tBet = new Thread(delegate()
                    {
                        try
                        {
                            _bet_again = true;
                            while (_bet_again && _stage == 2)
                            {
                                _bet_again = false;
                                this.OnProcess(new RaceProcessEventArgs() { Description = "开始下单" });
                                this.bet();
                            }
                        }
                        catch (ThreadAbortException) { }
                        catch (Exception ex)
                        {
                            this.OnProcess(new RaceProcessEventArgs() { Description = "下单线程错误：" + ex.Message, Detail = ex.ToString() });
                        }
                        finally
                        {
                            lock (this)
                            {
                                _tBet = null;
                            }
                        }
                    });
                    _tBet.Name = string.Format("下单-{0}-{1}", this.CardID, this.RaceNo);
                    _tBet.IsBackground = true;
                    _tBet.Start();
                }
                else
                {
                    this.OnProcess(new RaceProcessEventArgs() { Description = "正在下单，完成后重试" });
                    _bet_again = true;
                }
            }

            public void Stop()
            {
                lock (this)
                {
                    if (_tFix != null) _tFix.Abort();
                    if (_tBet != null && !object.Equals(Thread.CurrentThread, _tBet)) _tBet.Abort();
                    if (_tDaemon != null && !object.Equals(Thread.CurrentThread, _tDaemon)) _tDaemon.Abort();
                }
            }

            private void daemon()
            {
                try
                {
                    int lastUpdateRaceInfo = 0;
                    for (int i = 0; true; i++)
                    {
                        int update_time_threshold; 
                        if (StartTime.Subtract(DateTime.Now).TotalSeconds < 3600)
                        {
                            update_time_threshold = 40;
                        }
                        else
                        {
                            update_time_threshold = 10;
                        }

                        if (i - lastUpdateRaceInfo > update_time_threshold && __global_rand__.Next(i - lastUpdateRaceInfo) > update_time_threshold)
                        {
                            using (MySqlConnection conn = new MySqlConnection("server=120.24.210.35;user id=hrsdata;password=abcd0000;database=hrsdata;port=3306;charset=utf8"))
                            {
                                conn.Open();

                                using (MySqlCommand cmd = new MySqlCommand("select * from ct_race where race_date = ?race_date and race_loc = ?race_loc and race_no = ?race_no", conn))
                                {
                                    cmd.Parameters.AddWithValue("?race_date", this.RaceDate);
                                    cmd.Parameters.AddWithValue("?race_loc", this.RaceLoc);
                                    cmd.Parameters.AddWithValue("?race_no", this.RaceNo);

                                    using (MySqlDataReader dr = cmd.ExecuteReader())
                                    {
                                        if (!dr.Read())
                                        {
                                            throw new Exception("无法找到比赛信息");
                                        }
                                        else
                                        {
                                            string date_str = (string)dr["race_date"];
                                            string time_str = (string)dr["time_text"];

                                            Match md = Regex.Match(date_str, @"^(\d{2})-(\d{2})-(\d{4})$");
                                            if (md.Success)
                                            {
                                                this.StartTime = DateTime.Parse(string.Format("{0}-{1}-{2} {3}",
                                                        md.Groups[3].Value,
                                                        md.Groups[2].Value,
                                                        md.Groups[1].Value,
                                                        time_str));
                                            }
                                            else
                                            {
                                                throw new Exception("无法识别的race_date格式");
                                            }
                                        }
                                    }
                                }
                            }

                            lastUpdateRaceInfo = i;
                            this.OnProcess(new RaceProcessEventArgs() { Description = "赛事信息已更新" });
                        }

                        if (_stage == 0)
                        {
                            if (StartTime.Subtract(DateTime.Now).TotalSeconds > this.FitTimeInAdvance)
                            {
                                Thread.Sleep(25000 + __global_rand__.Next(10000));
                            }
                            else
                            {
                                _stage = 1;
                                this.OnProcess(new RaceProcessEventArgs() { Description = "进入第1阶段" });
                            }
                        }
                        else if (_stage == 1)
                        {
                            if (_tFix == null)
                            {
                                _tFix = new Thread(new ThreadStart(fix));
                                _tFix.Name = string.Format("拟合-{0}-{1}", this.CardID, this.RaceNo);
                                _tFix.IsBackground = true;
                                _tFix.Start();
                            }

                            if (StartTime.Subtract(DateTime.Now).TotalSeconds > this.BetTimeInAdvance)
                            {
                                Thread.Sleep(2000);
                            }
                            else
                            {
                                _stage = 2;
                                this.OnProcess(new RaceProcessEventArgs() { Description = "进入第2阶段" });
                                this.startBet();
                            }
                        }
                        else if (_stage == 2)
                        {
                            if (DateTime.Now.Subtract(StartTime).TotalSeconds > 600)
                            {
                                _stage = 3;
                                this.Stop();
                            }

                            // 由拟合线程驱动下单，不需要守护线程做什么
                            Thread.Sleep(10000);
                        }
                        else
                        {
                            // 结束
                            break;
                        }
                    }
                }
                catch (ThreadAbortException)
                {

                }
                catch (Exception ex)
                {
                    this.OnProcess(new RaceProcessEventArgs() { Description = "守护线程错误：" + ex.Message, Detail = ex.ToString() });
                }
                finally
                {
                    lock (this)
                    {
                        _tDaemon = null;
                    }
                }
            }

            private void fix()
            {
                try
                {
                    while (true)
                    {
                        this.OnProcess(new RaceProcessEventArgs() { Description = "拟合前更新赔率数据" });
                        if (this.updateOddsData())
                        {
                            this.OnProcess(new RaceProcessEventArgs() { Description = "更新赔率数据完成，开始拟合" });
                            HrsTable fittingOdds = _latest_odds;
                            double E = Fitting.fit(fittingOdds, 0.0001);
                            this.OnProcess(new RaceProcessEventArgs() { Description = "拟合完成" });
                            fittingOdds.E = E;
                            lock (this)
                            {
                                _fitted_odds = fittingOdds;
                            }
                            if (_stage == 2)
                            {
                                this.startBet();
                            }
                        }

                        Thread.Sleep(1000);
                    }
                }
                catch (ThreadAbortException) { }
                catch (Exception ex)
                {
                    this.OnProcess(new RaceProcessEventArgs() { Description = "拟合线程错误：" + ex.Message, Detail = ex.ToString() });
                }
                finally
                {
                    lock (this)
                    {
                        _tFix = null;
                    }
                }
            }

            /// <summary>
            /// 计算Eat单的平仓利润
            /// </summary>
            /// <param name="orderDiscount"></param>
            /// <param name="orderLimit"></param>
            /// <param name="waterDiscount"></param>
            /// <param name="waterLimit"></param>
            /// <param name="worstOdds"></param>
            /// <param name="probability"></param>
            /// <returns></returns>
            private double calcCloseProfitForEat(double orderDiscount, double orderLimit, double waterDiscount, double waterLimit, double worstOdds, double probability, out double risk, out double maxCloseAmount)
            {
                if (waterLimit >= orderLimit)
                {
                    risk = 0;
                    maxCloseAmount = double.MaxValue;
                    return (orderDiscount - waterDiscount) / 100;
                }
                else
                {
                    double profit;
                    if (worstOdds <= waterLimit)
                    {
                        risk = (orderLimit - waterLimit) / 2 / LIMIT_SCALE;
                        profit = (orderDiscount - waterDiscount) / 100;
                    }
                    else
                    {
                        risk = Math.Max(Math.Min(orderLimit, worstOdds) - waterLimit, (orderLimit - waterLimit) / 2) / LIMIT_SCALE;
                        profit = (orderDiscount - waterDiscount) / 100 * (1 - probability) - (Math.Min(orderLimit, worstOdds) - waterLimit) / 100 * probability;
                    }

                    double r = (risk + profit) / risk;
                    double O = r / (1 - probability);
                    maxCloseAmount = (T * Math.Pow(r - 1, 2)) / (LOSS_RATE_COEFFICIENT * r * (O - r)) / risk;
                    return profit;
                }
            }

            /// <summary>
            /// 计算Bet单的平仓利润
            /// </summary>
            /// <param name="orderDiscount"></param>
            /// <param name="orderLimit"></param>
            /// <param name="waterDiscount"></param>
            /// <param name="waterLimit"></param>
            /// <param name="worstOdds"></param>
            /// <param name="probability"></param>
            /// <returns></returns>
            private double calcCloseProfitForBet(double orderDiscount, double orderLimit, double waterDiscount, double waterLimit, double worstOdds, double probability, out double risk, out double maxCloseAmount)
            {
                if (waterLimit <= orderLimit)
                {
                    risk = 0;
                    maxCloseAmount = double.MaxValue;
                    return (waterDiscount - orderDiscount) / 100;
                }
                else
                {
                    double profit;
                    if (worstOdds <= orderLimit)
                    {
                        risk = (waterLimit - orderLimit) / 2 / LIMIT_SCALE;
                        profit = (waterDiscount - orderDiscount) / 100;
                    }
                    else
                    {
                        risk = Math.Max(Math.Min(waterLimit, worstOdds) - orderLimit, (waterLimit - orderLimit) / 2) / LIMIT_SCALE;
                        profit = (orderDiscount - waterDiscount) / 100 * (1 - probability) - (Math.Min(waterLimit, worstOdds) - orderLimit) / 100 * probability;
                    }

                    double r = (risk + profit) / risk;
                    double O = r / (1 - probability);
                    maxCloseAmount = (T * Math.Pow(r - 1, 2)) / (LOSS_RATE_COEFFICIENT * r * (O - r)) / risk;
                    return profit;
                }
            }

            /// <summary>
            /// 计算Eat单的当前预计盈利
            /// </summary>
            /// <param name="orderDiscount"></param>
            /// <param name="orderLimit"></param>
            /// <param name="waterDiscount"></param>
            /// <param name="waterLimit"></param>
            /// <param name="worstOdds"></param>
            /// <param name="probability"></param>
            /// <returns></returns>
            private double calcForcastProfitForEat(double orderDiscount, double orderLimit, double worstOdds, double probability)
            {
                //return (1 + (orderDiscount / 100) / (Math.Min(orderLimit / LIMIT_SCALE, worstOdds) - (orderDiscount / 100))) * (1 - probability) - 1;
                return (orderDiscount / 100) - Math.Min(orderLimit / LIMIT_SCALE, worstOdds) * probability;
            }

            /// <summary>
            /// 计算Bet单的当前预计盈利
            /// </summary>
            /// <param name="orderDiscount"></param>
            /// <param name="orderLimit"></param>
            /// <param name="worstOdds"></param>
            /// <param name="probability"></param>
            /// <returns></returns>
            private double calcForcastProfitForBet(double orderDiscount, double orderLimit, double worstOdds, double probability)
            {
                return Math.Min(orderLimit / LIMIT_SCALE, worstOdds) * probability - (orderDiscount / 100);
            }

            private bool checkCloseRisk(double risk, double maxCloseAmount, double roundStep, string betId, ref Dictionary<string, double> riskContainer, ref double closeAmount)
            {
                if (!riskContainer.ContainsKey(betId)) riskContainer[betId] = 0;
                if (risk > 0)
                {
                    if (closeAmount > maxCloseAmount - riskContainer[betId] / risk)
                        closeAmount = Math.Round((maxCloseAmount - riskContainer[betId] / risk) / roundStep) * roundStep;
                    return closeAmount > 0;
                }
                else
                {
                    return true;
                }
            }

            private void bet()
            {
                if (_fitted_odds != null)
                {
                    this.OnProcess(new RaceProcessEventArgs() { Description = "下单前更新赔率及水位数据" });
                    _latest_waters.Clear();
                    if (this.updateOddsData() && this.updateBetWpWatersData() && this.updateEatWpWatersData() && this.updateBetQnWatersData() && this.updateEatQnWatersData() && this.updateBetQpWatersData() && this.updateEatQpWatersData())
                    {
                        if (_latest_waters.IsEmpty)
                        {
                            this.OnProcess(new RaceProcessEventArgs() { Description = "没有水位信息，赛事已经停止下注，结束！" });
                            _stage = 3; // 第三阶段：停止下注阶段
                            this.Stop();
                            return;
                        }
                        HrsTable latestOdds = _latest_odds;
                        HrsTable fittedOdds = _fitted_odds;

                        this.OnProcess(new RaceProcessEventArgs() { Description = "赔率及水位数据更新完成，开始判断下单" });
                        double[] p1, p3, pq_win, pq_plc;
                        Fitting.calcProbility(fittedOdds, out p1, out p3, out pq_win, out pq_plc);

                        // 计算预计概率与当前赔率下下注比例的交叉熵
                        double[] q1, q3, qqn, qqp;
                        double r1, r3 = R_PLC, rqn, rqp = R_QP;
                        q1 = Fitting.calcBetRateForWin(latestOdds, out r1);
                        q3 = Fitting.calcBetRateForPlcWithStyle2(latestOdds, ref r3);
                        qqn = Fitting.calcBetRateForQn(latestOdds, out rqn);
                        qqp = Fitting.calcBetRateForQpWithStyle2(latestOdds, ref rqp);
                        double E = cross_entropy(q1, p1) + cross_entropy(qqn, pq_win);
                        if (r1 < 0.8 || r1 >= 1) this.OnProcess(new RaceProcessEventArgs() { Description = string.Format("赔付率错误：WIN/{0:0.00000}", r1) });
                        if (r3 < 0.8 || r3 >= 1) this.OnProcess(new RaceProcessEventArgs() { Description = string.Format("赔付率错误：PLC/{0:0.00000}", r3) });
                        if (rqn < 0.8 || rqn >= 1) this.OnProcess(new RaceProcessEventArgs() { Description = string.Format("赔付率错误：Q/{0:0.00000}", rqn) });
                        if (rqp < 0.8 || rqp >= 1) this.OnProcess(new RaceProcessEventArgs() { Description = string.Format("赔付率错误：QP/{0:0.00000}", rqp) });

                        // 当前赔率下交叉熵过大则退出，不下单
                        if (E > fittedOdds.E * E_THRESHOLD_SCALE)
                        {
                            fittedOdds = null;        // 已经拟合的数据无效
                            this.OnProcess(new RaceProcessEventArgs() { Description = string.Format("下单时赔率变化过大，取消下单。E={0:0.00000}", E) });
                            return;
                        }

                        // 计算FittedOdds的PLC/QP下注比率与赔付
                        double[] b3_fitted, bqp_fitted;
                        double r3_fitted = R_PLC, rqp_fitted = R_QP;
                        b3_fitted = Fitting.calcBetRateForPlcWithStyle2(fittedOdds, ref r3_fitted);
                        bqp_fitted = Fitting.calcBetRateForQpWithStyle2(fittedOdds, ref rqp_fitted);

                        #region W/P

                        // 计算PLC最大与最小的Odds
                        double[] plc_min_odds_latest = ODDS_MODE == 1 ? Fitting.calcMinOddsForPlc(latestOdds, q3, r3) : latestOdds.SpPlc;
                        double[] plc_max_odds_latest = ODDS_MODE == 1 ? Fitting.calcMaxOddsForPlc(latestOdds, q3, r3) : latestOdds.SpPlc;
                        double[] plc_min_odds_fitted = ODDS_MODE == 1 ? Fitting.calcMinOddsForPlc(fittedOdds, b3_fitted, r3_fitted) : fittedOdds.SpPlc;
                        double[] plc_max_odds_fitted = ODDS_MODE == 1 ? Fitting.calcMaxOddsForPlc(fittedOdds, b3_fitted, r3_fitted) : fittedOdds.SpPlc;
                        for (int i = 0; i < latestOdds.Count; i++)
                        {
                            Hrs h = latestOdds[i];

                            double sp_w_min, sp_w_max, sp_p_min, sp_p_max;
                            sp_w_min = Math.Min(h.Win, fittedOdds[i].Win);
                            sp_w_max = Math.Max(h.Win, fittedOdds[i].Win);
                            sp_p_min = Math.Min(plc_min_odds_latest[i], plc_min_odds_fitted[i]);
                            sp_p_max = Math.Max(plc_max_odds_latest[i], plc_max_odds_fitted[i]);

                            if (r1 < 0.8 || r1 >= 1)
                            {
                                // 错误数据
                                sp_w_min = 0;
                                sp_w_max = double.MaxValue;
                            }
                            if (r3 < 0.8 || r3 >= 1)
                            {
                                // 错误数据
                                sp_p_min = 0;
                                sp_p_max = double.MaxValue;
                            }

                            // For Bet
                            {
                                WaterWPList vlist = _latest_waters.GetWpEatWater(h.No).GetValuableWater(MIN_R, sp_w_min, p1[i], sp_p_min, p3[i]);
                                double bet_amount_win = 0, bet_amount_plc = 0;
                                if (_bet_amount_win.ContainsKey(h.No))
                                    bet_amount_win = _bet_amount_win[h.No];
                                if (_bet_amount_plc.ContainsKey(h.No))
                                    bet_amount_plc = _bet_amount_plc[h.No];
                                bool full_win = false, full_plc = false;
                                foreach (WaterWPItem w in vlist.OrderBy(x => x.Percent))
                                {
                                    if (full_win && full_plc) break;
                                    if (w.Percent > BET_MAX_DISCOUNT_WP) break;  // 超过水位上限
                                    if (w.WinAmount > 0 && full_win) continue;
                                    if (w.PlcAmount > 0 && full_plc) continue;

                                    double bet_amount = -1;

                                    if (w.WinAmount > 0)
                                    {
                                        double O = Math.Min(w.WinLimit / LIMIT_SCALE, sp_w_min) * 100 / w.Percent;
                                        if (O * p1[i] > MAX_R)
                                        {
                                            // 超过回报率上限
                                            full_win = true;
                                            continue;
                                        }

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
                                        if (O * p3[i] > MAX_R)
                                        {
                                            // 超过回报率上限
                                            full_plc = true;
                                            continue;
                                        }

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
                                                CardID = CardID,
                                                RaceNo = RaceNo,
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

                                            if (this.order(ir))
                                            {
                                                w.TradedAmount += bet_amount;
                                                bet_amount_win += ir.WinAmount;
                                                bet_amount_plc += ir.PlcAmount;
                                            }
                                        }
                                    }
                                }

                                _bet_amount_win[h.No] = bet_amount_win;
                                _bet_amount_plc[h.No] = bet_amount_plc;

                                // 平仓
                                #region 平仓
                                if (_orders_eat_wp.ContainsKey(h.No))  // _orders_eat_wp 在order方法中记录
                                {
                                    // order从优到劣排序，如果当前order不能平仓，后续的order也肯定不能平仓
                                    IEnumerator<InvestRecordWp> ordersSW = _orders_eat_wp[h.No].Where(x => x.WinAmount > 0 && x.PlcAmount == 0 && x.OrderId > 0 && x.CloseAmount < x.WinAmount).OrderByDescending(x => x.Percent).GetEnumerator();
                                    IEnumerator<InvestRecordWp> ordersSP = _orders_eat_wp[h.No].Where(x => x.PlcAmount > 0 && x.WinAmount == 0 && x.OrderId > 0 && x.CloseAmount < x.PlcAmount).OrderByDescending(x => x.Percent).GetEnumerator();
                                    IEnumerator<InvestRecordWp> ordersWP = _orders_eat_wp[h.No].Where(x => x.PlcAmount == x.WinAmount && x.OrderId > 0 && x.CloseAmount < x.PlcAmount).OrderByDescending(x => x.Percent).GetEnumerator();
                                    bool flagSW = ordersSW.MoveNext();
                                    bool flagSP = ordersSP.MoveNext();
                                    bool flagWP = ordersWP.MoveNext();

                                    List<WaterWPItem> watersSW = new List<WaterWPItem>();
                                    List<WaterWPItem> watersSP = new List<WaterWPItem>();
                                    List<WaterWPItem> watersWP = new List<WaterWPItem>();
                                    foreach (WaterWPItem wi in _latest_waters.GetWpEatWater(h.No))
                                    {
                                        if (wi.WinAmount > 0 && wi.PlcAmount == 0)
                                        {
                                            if (wi.WinAmount - wi.TradedAmount <= 0) continue;
                                            watersSW.Add(wi);
                                            while (flagSW)
                                            {
                                                InvestRecordWp currentOrder = ordersSW.Current;
                                                double risk, maxCloseAmount;
                                                double closeProfit = this.calcCloseProfitForEat(currentOrder.Percent, currentOrder.WinLimit, wi.Percent, wi.WinLimit, h.Win, p1[i], out risk, out maxCloseAmount);
                                                double forcastProfit = this.calcForcastProfitForEat(currentOrder.Percent, currentOrder.WinLimit, h.Win, p1[i]);
                                                if (closeProfit > forcastProfit)
                                                {
                                                    double bet_amount = Math.Min(currentOrder.WinAmount - currentOrder.CloseAmount, Math.Ceiling((wi.WinAmount - wi.TradedAmount) / WP_STEP) * WP_STEP);
                                                    // 平仓风险限制
                                                    if (!this.checkCloseRisk(risk, maxCloseAmount, WP_STEP, h.No, ref _close_risk_win, ref bet_amount))
                                                    {
                                                        flagSW = ordersSW.MoveNext();
                                                        continue;
                                                    }
                                                    InvestRecordWp ir = new InvestRecordWp()
                                                    {
                                                        TimeKey = ToUnixTime(DateTime.Now),
                                                        Model = MODEL,
                                                        CardID = CardID,
                                                        RaceNo = RaceNo,
                                                        Direction = "BET",
                                                        HorseNo = h.No,
                                                        Percent = wi.Percent,
                                                        WinLimit = wi.WinLimit,
                                                        WinAmount = bet_amount,
                                                        WinProbility = p1[i],
                                                        WinOdds = h.Win,
                                                        CloseFlag = true,
                                                        RefItem = currentOrder,
                                                        CloseRiskIncWin = bet_amount * risk
                                                    };
                                                    if (this.order(ir))
                                                    {
                                                        wi.TradedAmount += bet_amount;
                                                        currentOrder.CloseAmount += bet_amount;
                                                        _bet_amount_win[h.No] += bet_amount;
                                                        _close_risk_win[h.No] += ir.CloseRiskIncWin;

                                                        if (currentOrder.CloseAmount >= currentOrder.WinAmount)
                                                        {
                                                            // 下一个订单
                                                            flagSW = ordersSW.MoveNext();
                                                        }
                                                        if (wi.TradedAmount >= wi.WinAmount)
                                                        {
                                                            // 下一条水位
                                                            break;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // 下单失败，尝试下一条水位
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    // 当前order不能平仓，后续order不需要再看了
                                                    flagSW = false;
                                                }
                                            }
                                        }
                                        else if (wi.PlcAmount > 0 && wi.WinAmount == 0)
                                        {
                                            if (wi.PlcAmount - wi.TradedAmount <= 0) continue;
                                            watersSP.Add(wi);
                                            while (flagSP)
                                            {
                                                InvestRecordWp currentOrder = ordersSP.Current;
                                                double risk, maxCloseAmount;
                                                double closeProfit = this.calcCloseProfitForEat(currentOrder.Percent, currentOrder.PlcLimit, wi.Percent, wi.PlcLimit, plc_max_odds_latest[i], p3[i], out risk, out maxCloseAmount);
                                                double forcastProfit = this.calcForcastProfitForEat(currentOrder.Percent, currentOrder.PlcLimit, plc_max_odds_latest[i], p3[i]);
                                                if (closeProfit > forcastProfit)
                                                {
                                                    double bet_amount = Math.Min(currentOrder.PlcAmount - currentOrder.CloseAmount, Math.Ceiling((wi.PlcAmount - wi.TradedAmount) / WP_STEP) * WP_STEP);
                                                    // 平仓风险限制
                                                    if (!this.checkCloseRisk(risk, maxCloseAmount, WP_STEP, h.No, ref _close_risk_plc, ref bet_amount))
                                                    {
                                                        flagSP = ordersSP.MoveNext();
                                                        continue;
                                                    }
                                                    InvestRecordWp ir = new InvestRecordWp()
                                                    {
                                                        TimeKey = ToUnixTime(DateTime.Now),
                                                        Model = MODEL,
                                                        CardID = CardID,
                                                        RaceNo = RaceNo,
                                                        Direction = "BET",
                                                        HorseNo = h.No,
                                                        Percent = wi.Percent,
                                                        PlcLimit = wi.PlcLimit,
                                                        PlcAmount = bet_amount,
                                                        PlcProbility = p3[i],
                                                        PlcOdds = plc_max_odds_latest[i],
                                                        CloseFlag = true,
                                                        RefItem = currentOrder,
                                                        CloseRiskIncPlc = bet_amount * risk
                                                    };
                                                    if (this.order(ir))
                                                    {
                                                        wi.TradedAmount += bet_amount;
                                                        currentOrder.CloseAmount += bet_amount;
                                                        _bet_amount_plc[h.No] += bet_amount;
                                                        _close_risk_plc[h.No] += ir.CloseRiskIncPlc;

                                                        if (currentOrder.CloseAmount >= currentOrder.WinAmount)
                                                        {
                                                            // 下一个订单
                                                            flagSP = ordersSP.MoveNext();
                                                        }
                                                        if (wi.TradedAmount >= wi.WinAmount)
                                                        {
                                                            // 下一条水位
                                                            break;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // 下单失败，尝试下一条水位
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    // 当前order不能平仓，后续order不需要再看了
                                                    flagSP = false;
                                                }
                                            }
                                        }
                                        else if (wi.WinAmount == wi.PlcAmount)
                                        {
                                            if (wi.WinAmount - wi.TradedAmount <= 0) continue;
                                            watersWP.Add(wi);
                                            while (flagWP)
                                            {
                                                InvestRecordWp currentOrder = ordersWP.Current;
                                                double riskW, riskP, maxCloseAmountW, maxCloseAmountP;
                                                double closeProfit =
                                                    this.calcCloseProfitForEat(currentOrder.Percent, currentOrder.WinLimit, wi.Percent, wi.WinLimit, h.Win, p1[i], out riskW, out maxCloseAmountW) +
                                                    this.calcCloseProfitForEat(currentOrder.Percent, currentOrder.PlcLimit, wi.Percent, wi.PlcLimit, plc_max_odds_latest[i], p3[i], out riskP, out maxCloseAmountP);
                                                double forcastProfit =
                                                    this.calcForcastProfitForEat(currentOrder.Percent, currentOrder.WinLimit, h.Win, p1[i]) +
                                                    this.calcForcastProfitForEat(currentOrder.Percent, currentOrder.PlcLimit, plc_max_odds_latest[i], p3[i]);
                                                if (closeProfit > forcastProfit)
                                                {
                                                    double bet_amount = Math.Min(currentOrder.WinAmount - currentOrder.CloseAmount, Math.Ceiling((wi.WinAmount - wi.TradedAmount) / WP_STEP) * WP_STEP);
                                                    // 平仓风险限制
                                                    if (!this.checkCloseRisk(riskW, maxCloseAmountW, WP_STEP, h.No, ref _close_risk_win, ref bet_amount))
                                                    {
                                                        flagWP = ordersWP.MoveNext();
                                                        continue;
                                                    }
                                                    if (!this.checkCloseRisk(riskP, maxCloseAmountP, WP_STEP, h.No, ref _close_risk_plc, ref bet_amount))
                                                    {
                                                        flagWP = ordersWP.MoveNext();
                                                        continue;
                                                    }
                                                    InvestRecordWp ir = new InvestRecordWp()
                                                    {
                                                        TimeKey = ToUnixTime(DateTime.Now),
                                                        Model = MODEL,
                                                        CardID = CardID,
                                                        RaceNo = RaceNo,
                                                        Direction = "BET",
                                                        HorseNo = h.No,
                                                        Percent = wi.Percent,
                                                        WinLimit = wi.WinLimit,
                                                        WinAmount = bet_amount,
                                                        WinProbility = p1[i],
                                                        WinOdds = h.Win,
                                                        PlcLimit = wi.PlcLimit,
                                                        PlcAmount = bet_amount,
                                                        PlcProbility = p3[i],
                                                        PlcOdds = plc_max_odds_latest[i],
                                                        CloseFlag = true,
                                                        RefItem = currentOrder,
                                                        CloseRiskIncWin = bet_amount * riskW,
                                                        CloseRiskIncPlc = bet_amount * riskP
                                                    };
                                                    if (this.order(ir))
                                                    {
                                                        wi.TradedAmount += bet_amount;
                                                        currentOrder.CloseAmount += bet_amount;
                                                        _bet_amount_win[h.No] += bet_amount;
                                                        _bet_amount_plc[h.No] += bet_amount;
                                                        _close_risk_win[h.No] += ir.CloseRiskIncWin;
                                                        _close_risk_plc[h.No] += ir.CloseRiskIncPlc;

                                                        if (currentOrder.CloseAmount >= currentOrder.WinAmount)
                                                        {
                                                            // 下一个订单
                                                            flagWP = ordersWP.MoveNext();
                                                        }
                                                        if (wi.TradedAmount >= wi.WinAmount)
                                                        {
                                                            // 下一条水位
                                                            break;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // 下单失败，尝试下一条水位
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    // 当前order不能平仓，后续order不需要再看了
                                                    flagWP = false;
                                                }
                                            }
                                        }

                                        if (!flagSW && !flagSP && !flagWP) break;
                                    }
                                }
                                #endregion
                            }

                            // For Eat
                            {
                                WaterWPList vlist = _latest_waters.GetWpBetWater(h.No).GetValuableWater(MIN_R, sp_w_max, p1[i], sp_p_max, p3[i]);
                                double eat_amount_win = 0, eat_amount_plc = 0;
                                if (_bet_amount_win.ContainsKey(h.No)) eat_amount_win = -_bet_amount_win[h.No];
                                if (_bet_amount_plc.ContainsKey(h.No)) eat_amount_plc = -_bet_amount_plc[h.No];
                                bool full_win = false, full_plc = false;
                                foreach (WaterWPItem w in vlist.OrderByDescending(x => x.Percent))
                                {
                                    if (full_win && full_plc) break;
                                    if (w.Percent < EAT_MIN_DISCOUNT_WP) break;   // 超过水位下限
                                    if (w.WinAmount > 0 && full_win) continue;
                                    if (w.PlcAmount > 0 && full_plc) continue;

                                    double eat_amount = -1;

                                    if (w.WinAmount > 0)
                                    {
                                        // 投入= SP - percent
                                        // 赢获得 = 投入+吃得 = SP - percent + percent = SP
                                        // 输获得 = 0
                                        // Odds = 赢获得 / 投入 = SP / (SP - percent) = 1 + percent / (SP - percent)
                                        double O = 1 + (w.Percent / 100) / (Math.Min(w.WinLimit / LIMIT_SCALE, sp_w_max) - (w.Percent / 100));
                                        if (O * (1 - p1[i]) > MAX_R)
                                        {
                                            // 超过回报率上限
                                            full_win = true;
                                            continue;
                                        }

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
                                        double O = 1 + (w.Percent / 100) / (Math.Min(w.PlcLimit / LIMIT_SCALE, sp_p_max) - (w.Percent / 100));
                                        if (O * (1 - p3[i]) > MAX_R)
                                        {
                                            // 超过回报率上限
                                            full_plc = true;
                                            continue;
                                        }

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
                                                CardID = CardID,
                                                RaceNo = RaceNo,
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

                                            if (this.order(ir))
                                            {
                                                w.TradedAmount += eat_amount;
                                                eat_amount_win += ir.WinAmount;
                                                eat_amount_plc += ir.PlcAmount;
                                            }
                                        }
                                    }
                                }

                                _bet_amount_win[h.No] = -eat_amount_win;
                                _bet_amount_plc[h.No] = -eat_amount_plc;

                                // 平仓
                                #region 平仓
                                if (_orders_bet_wp.ContainsKey(h.No))  // _orders_bet_wp 在order方法中记录
                                {
                                    // order从优到劣排序，如果当前order不能平仓，后续的order也肯定不能平仓
                                    IEnumerator<InvestRecordWp> ordersSW = _orders_bet_wp[h.No].Where(x => x.WinAmount > 0 && x.PlcAmount == 0 && x.OrderId > 0 && x.CloseAmount < x.WinAmount).OrderBy(x => x.Percent).GetEnumerator();
                                    IEnumerator<InvestRecordWp> ordersSP = _orders_bet_wp[h.No].Where(x => x.PlcAmount > 0 && x.WinAmount == 0 && x.OrderId > 0 && x.CloseAmount < x.PlcAmount).OrderBy(x => x.Percent).GetEnumerator();
                                    IEnumerator<InvestRecordWp> ordersWP = _orders_bet_wp[h.No].Where(x => x.PlcAmount == x.WinAmount && x.OrderId > 0 && x.CloseAmount < x.PlcAmount).OrderBy(x => x.Percent).GetEnumerator();
                                    bool flagSW = ordersSW.MoveNext();
                                    bool flagSP = ordersSP.MoveNext();
                                    bool flagWP = ordersWP.MoveNext();

                                    List<WaterWPItem> watersSW = new List<WaterWPItem>();
                                    List<WaterWPItem> watersSP = new List<WaterWPItem>();
                                    List<WaterWPItem> watersWP = new List<WaterWPItem>();
                                    foreach (WaterWPItem wi in _latest_waters.GetWpBetWater(h.No))
                                    {
                                        if (wi.WinAmount > 0 && wi.PlcAmount == 0)
                                        {
                                            if (wi.WinAmount <= wi.TradedAmount) continue;
                                            watersSW.Add(wi);
                                            while (flagSW)
                                            {
                                                InvestRecordWp currentOrder = ordersSW.Current;
                                                double risk, maxCloseAmount;
                                                double closeProfit = this.calcCloseProfitForBet(currentOrder.Percent, currentOrder.WinLimit, wi.Percent, wi.WinLimit, h.Win, p1[i], out risk, out maxCloseAmount);
                                                double forcastProfit = this.calcForcastProfitForBet(currentOrder.Percent, currentOrder.WinLimit, h.Win, p1[i]);
                                                if (closeProfit > forcastProfit)
                                                {
                                                    double eat_amount = Math.Min(currentOrder.WinAmount - currentOrder.CloseAmount, Math.Ceiling((wi.WinAmount - wi.TradedAmount) / WP_STEP) * WP_STEP);
                                                    // 平仓风险限制
                                                    if (!this.checkCloseRisk(risk, maxCloseAmount, WP_STEP, h.No, ref _close_risk_win, ref eat_amount))
                                                    {
                                                        flagSW = ordersSW.MoveNext();
                                                        continue;
                                                    }
                                                    InvestRecordWp ir = new InvestRecordWp()
                                                    {
                                                        TimeKey = ToUnixTime(DateTime.Now),
                                                        Model = MODEL,
                                                        CardID = CardID,
                                                        RaceNo = RaceNo,
                                                        Direction = "EAT",
                                                        HorseNo = h.No,
                                                        Percent = wi.Percent,
                                                        WinLimit = wi.WinLimit,
                                                        WinAmount = eat_amount,
                                                        WinProbility = p1[i],
                                                        WinOdds = h.Win,
                                                        CloseFlag = true,
                                                        RefItem = currentOrder,
                                                        CloseRiskIncWin = eat_amount * risk
                                                    };
                                                    if (this.order(ir))
                                                    {
                                                        wi.TradedAmount += eat_amount;
                                                        currentOrder.CloseAmount += eat_amount;
                                                        _bet_amount_win[h.No] -= eat_amount;
                                                        _close_risk_win[h.No] += ir.CloseRiskIncWin;

                                                        if (currentOrder.CloseAmount >= currentOrder.WinAmount)
                                                        {
                                                            // 下一个订单
                                                            flagSW = ordersSW.MoveNext();
                                                        }
                                                        if (wi.TradedAmount >= wi.WinAmount)
                                                        {
                                                            // 下一条水位
                                                            break;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // 下单失败，尝试下一条水位
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    // 当前order不能平仓，后续order不需要再看了
                                                    flagSW = false;
                                                }
                                            }
                                        }
                                        else if (wi.PlcAmount > 0 && wi.WinAmount == 0)
                                        {
                                            if (wi.PlcAmount <= wi.TradedAmount) continue;
                                            watersSP.Add(wi);
                                            while (flagSP)
                                            {
                                                InvestRecordWp currentOrder = ordersSP.Current;
                                                double risk, maxCloseAmount;
                                                double closeProfit = this.calcCloseProfitForBet(currentOrder.Percent, currentOrder.PlcLimit, wi.Percent, wi.PlcLimit, plc_max_odds_latest[i], p3[i], out risk, out maxCloseAmount);
                                                double forcastProfit = this.calcForcastProfitForBet(currentOrder.Percent, currentOrder.PlcLimit, plc_min_odds_latest[i], p3[i]); // Bet单forcast的最差赔率是最低赔率
                                                if (closeProfit > forcastProfit)
                                                {
                                                    double eat_amount = Math.Min(currentOrder.PlcAmount - currentOrder.CloseAmount, Math.Ceiling((wi.PlcAmount - wi.TradedAmount) / WP_STEP) * WP_STEP);
                                                    // 平仓风险限制
                                                    if (!this.checkCloseRisk(risk, maxCloseAmount, WP_STEP, h.No, ref _close_risk_plc, ref eat_amount))
                                                    {
                                                        flagSP = ordersSP.MoveNext();
                                                        continue;
                                                    }
                                                    InvestRecordWp ir = new InvestRecordWp()
                                                    {
                                                        TimeKey = ToUnixTime(DateTime.Now),
                                                        Model = MODEL,
                                                        CardID = CardID,
                                                        RaceNo = RaceNo,
                                                        Direction = "EAT",
                                                        HorseNo = h.No,
                                                        Percent = wi.Percent,
                                                        PlcLimit = wi.PlcLimit,
                                                        PlcAmount = eat_amount,
                                                        PlcProbility = p3[i],
                                                        PlcOdds = plc_max_odds_latest[i],
                                                        CloseFlag = true,
                                                        RefItem = currentOrder,
                                                        CloseRiskIncPlc = eat_amount * risk
                                                    };
                                                    if (this.order(ir))
                                                    {
                                                        wi.TradedAmount += eat_amount;
                                                        currentOrder.CloseAmount += eat_amount;
                                                        _bet_amount_plc[h.No] -= eat_amount;
                                                        _close_risk_plc[h.No] += ir.CloseRiskIncPlc;

                                                        if (currentOrder.CloseAmount >= currentOrder.WinAmount)
                                                        {
                                                            // 下一个订单
                                                            flagSP = ordersSP.MoveNext();
                                                        }
                                                        if (wi.TradedAmount >= wi.WinAmount)
                                                        {
                                                            // 下一条水位
                                                            break;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // 下单失败，尝试下一条水位
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    // 当前order不能平仓，后续order不需要再看了
                                                    flagSP = false;
                                                }
                                            }
                                        }
                                        else if (wi.WinAmount == wi.PlcAmount)
                                        {
                                            if (wi.WinAmount <= wi.TradedAmount) continue;
                                            watersWP.Add(wi);
                                            while (flagWP)
                                            {
                                                InvestRecordWp currentOrder = ordersWP.Current;
                                                double riskW, riskP, maxCloseAmountW, maxCloseAmountP;
                                                double closeProfit =
                                                    this.calcCloseProfitForBet(currentOrder.Percent, currentOrder.WinLimit, wi.Percent, wi.WinLimit, h.Win, p1[i], out riskW, out maxCloseAmountW) +
                                                    this.calcCloseProfitForBet(currentOrder.Percent, currentOrder.PlcLimit, wi.Percent, wi.PlcLimit, plc_max_odds_latest[i], p3[i], out riskP, out maxCloseAmountP);
                                                double forcastProfit =
                                                    this.calcForcastProfitForBet(currentOrder.Percent, currentOrder.WinLimit, h.Win, p1[i]) +
                                                    this.calcForcastProfitForBet(currentOrder.Percent, currentOrder.PlcLimit, plc_min_odds_latest[i], p3[i]);  // Bet单forcast的最差赔率是最低赔率
                                                if (closeProfit > forcastProfit)
                                                {
                                                    double eat_amount = Math.Min(currentOrder.WinAmount - currentOrder.CloseAmount, Math.Ceiling((wi.WinAmount - wi.TradedAmount) / WP_STEP) * WP_STEP);
                                                    // 平仓风险限制
                                                    if (!this.checkCloseRisk(riskW, maxCloseAmountW, WP_STEP, h.No, ref _close_risk_win, ref eat_amount))
                                                    {
                                                        flagWP = ordersWP.MoveNext();
                                                        continue;
                                                    }
                                                    if (!this.checkCloseRisk(riskP, maxCloseAmountP, WP_STEP, h.No, ref _close_risk_plc, ref eat_amount))
                                                    {
                                                        flagWP = ordersWP.MoveNext();
                                                        continue;
                                                    }
                                                    InvestRecordWp ir = new InvestRecordWp()
                                                    {
                                                        TimeKey = ToUnixTime(DateTime.Now),
                                                        Model = MODEL,
                                                        CardID = CardID,
                                                        RaceNo = RaceNo,
                                                        Direction = "EAT",
                                                        HorseNo = h.No,
                                                        Percent = wi.Percent,
                                                        WinLimit = wi.WinLimit,
                                                        WinAmount = eat_amount,
                                                        WinProbility = p1[i],
                                                        WinOdds = h.Win,
                                                        PlcLimit = wi.PlcLimit,
                                                        PlcAmount = eat_amount,
                                                        PlcProbility = p3[i],
                                                        PlcOdds = plc_max_odds_latest[i],
                                                        CloseFlag = true,
                                                        RefItem = currentOrder,
                                                        CloseRiskIncWin = eat_amount * riskW,
                                                        CloseRiskIncPlc = eat_amount * riskP
                                                    };
                                                    if (this.order(ir))
                                                    {
                                                        wi.TradedAmount += eat_amount;
                                                        currentOrder.CloseAmount += eat_amount;
                                                        _bet_amount_win[h.No] -= eat_amount;
                                                        _bet_amount_plc[h.No] -= eat_amount;
                                                        _close_risk_win[h.No] += ir.CloseRiskIncWin;
                                                        _close_risk_plc[h.No] += ir.CloseRiskIncPlc;

                                                        if (currentOrder.CloseAmount >= currentOrder.WinAmount)
                                                        {
                                                            // 下一个订单
                                                            flagWP = ordersWP.MoveNext();
                                                        }
                                                        if (wi.TradedAmount >= wi.WinAmount)
                                                        {
                                                            // 下一条水位
                                                            break;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // 下单失败，尝试下一条水位
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    // 当前order不能平仓，后续order不需要再看了
                                                    flagWP = false;
                                                }
                                            }
                                        }

                                        if (!flagSW && !flagSP && !flagWP) break;
                                    }
                                }
                                #endregion
                            }

                        }
                        #endregion

                        common.Math.Combination comb2 = new common.Math.Combination(latestOdds.Count, 2);
                        int[][] combinations = comb2.GetCombinations();
                        // 计算QP最大与最小的Odds
                        double[][] qp_minmax_odds_latest = ODDS_MODE == 1 ? Fitting.calcMinMaxOddsForQp(latestOdds, qqp, rqp) : new double[][] { latestOdds.SpQp.Sp, latestOdds.SpQp.Sp };
                        double[][] qp_minmax_odds_fitted = ODDS_MODE == 1 ? Fitting.calcMinMaxOddsForQp(fittedOdds, bqp_fitted, rqp_fitted) : new double[][] { fittedOdds.SpQp.Sp, fittedOdds.SpQp.Sp };
                        for (int i = 0; i < combinations.Length; i++)
                        {
                            int[] c = combinations[i];
                            string horseNo = string.Format("{0}-{1}", latestOdds[c[0]].No, latestOdds[c[1]].No);

                            #region Q
                            if (pq_win != null
                                && rqn > 0.8 && rqn < 1)   // 0.8-1范围之外的赔付率认为是错误数据
                            {
                                double sp_min, sp_max;
                                lock (this)
                                {
                                    sp_min = Math.Min(latestOdds.SpQ[horseNo], fittedOdds.SpQ[horseNo]);
                                    sp_max = Math.Max(latestOdds.SpQ[horseNo], fittedOdds.SpQ[horseNo]);
                                }

                                // For Bet
                                {
                                    WaterQnList vlist = _latest_waters.GetQnEatWater(horseNo).GetValuableWater(MIN_R, sp_min, pq_win[i]);
                                    double bet_amount = 0;
                                    if (_bet_amount_qn.ContainsKey(horseNo)) bet_amount = _bet_amount_qn[horseNo];
                                    foreach (WaterQnItem w in vlist.OrderBy(x => x.Percent))
                                    {
                                        if (w.Percent > BET_MAX_DISCOUNT_QN) break;  // 超过水位上限 

                                        double O = Math.Min(w.Limit / LIMIT_SCALE, sp_min) * 100 / w.Percent;
                                        if (O * pq_win[i] > MAX_R)
                                        {
                                            // 超过回报率上限
                                            break;
                                        }

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
                                                CardID = CardID,
                                                RaceNo = RaceNo,
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
                                            if (this.order(ir))
                                            {
                                                bet_amount += ir.Amount;
                                                w.TradedAmount += ir.Amount;
                                            }
                                        }
                                    }
                                    _bet_amount_qn[horseNo] = bet_amount;

                                    //平仓EAT Q单
                                    #region 平仓
                                    if (_orders_eat_qn.ContainsKey(horseNo))  // _orders_eat_qn 在order方法中记录
                                    {
                                        // order从优到劣排序，如果当前order不能平仓，后续的order也肯定不能平仓
                                        IEnumerator<InvestRecordQn> orders = _orders_eat_qn[horseNo].Where(x => x.CloseAmount < x.Amount && x.OrderId > 0).OrderByDescending(x => x.Percent).GetEnumerator();
                                        bool flag = orders.MoveNext();
                                        foreach (WaterQnItem wi in _latest_waters.GetQnBetWater(horseNo))
                                        {
                                            if (wi.Amount <= wi.TradedAmount) continue;
                                            while(flag)
                                            {
                                                InvestRecordQn currentOrder = orders.Current;
                                                double risk, maxCloseAmount;
                                                double closeProfit = this.calcCloseProfitForEat(currentOrder.Percent, currentOrder.Limit, wi.Percent, wi.Limit, latestOdds.SpQ[horseNo], pq_win[i], out risk, out maxCloseAmount);
                                                double forcastProfit = this.calcForcastProfitForEat(currentOrder.Percent, currentOrder.Limit, latestOdds.SpQ[horseNo], pq_win[i]);
                                                if (closeProfit > forcastProfit)
                                                {
                                                    double close_amount = Math.Min(currentOrder.Amount - currentOrder.CloseAmount, Math.Ceiling((wi.Amount - wi.TradedAmount) / QN_STEP) * QN_STEP);
                                                    if (!this.checkCloseRisk(risk, maxCloseAmount, QN_STEP, horseNo, ref _close_risk_qn, ref close_amount))
                                                    {
                                                        flag = orders.MoveNext();
                                                        continue;
                                                    }
                                                    InvestRecordQn ir = new InvestRecordQn()
                                                    {
                                                        TimeKey = ToUnixTime(DateTime.Now),
                                                        Model = MODEL,
                                                        CardID = CardID,
                                                        RaceNo = RaceNo,
                                                        Direction = "BET",
                                                        Type = "Q",
                                                        HorseNo = horseNo,
                                                        Percent = wi.Percent,
                                                        Amount = close_amount,
                                                        Limit = wi.Limit,
                                                        Odds = latestOdds.SpQ[horseNo],
                                                        Probility = pq_win[i],
                                                        CloseFlag = true,
                                                        RefItem = currentOrder,
                                                        CloseRiskInc = close_amount * risk
                                                    };
                                                    if (this.order(ir))
                                                    {
                                                        wi.TradedAmount += close_amount;
                                                        currentOrder.CloseAmount += close_amount;
                                                        _bet_amount_qn[horseNo] += close_amount;
                                                        _close_risk_qn[horseNo] += ir.CloseRiskInc;

                                                        if (currentOrder.CloseAmount >= currentOrder.Amount)
                                                        {
                                                            // 下一个订单
                                                            flag = orders.MoveNext();
                                                        }
                                                        if (wi.TradedAmount >= wi.Amount)
                                                        {
                                                            // 下一条水位
                                                            break;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // 下单失败，尝试下一条水位
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    // 当前order不能平仓，后续order不需要再看了
                                                    flag = false;
                                                }
                                            }
                                        }
                                    }
                                    #endregion
                                }

                                // For Eat
                                {
                                    WaterQnList vlist = _latest_waters.GetQnBetWater(horseNo).GetValuableWater(MIN_R, sp_max, pq_win[i]);
                                    double eat_amount = 0;
                                    if (_bet_amount_qn.ContainsKey(horseNo)) eat_amount = -_bet_amount_qn[horseNo];
                                    foreach (WaterQnItem w in vlist.OrderByDescending(x => x.Percent))
                                    {
                                        if (w.Percent < EAT_MIN_DISCOUNT_QN) break;  // 超过水位下限 
                                        double O = 1 + (w.Percent / 100) / (Math.Min(w.Limit / LIMIT_SCALE, sp_max) - (w.Percent / 100));
                                        if (O * (1 - pq_win[i]) > MAX_R)
                                        {
                                            // 超过回报率上限
                                            break;
                                        }

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
                                                CardID = CardID,
                                                RaceNo = RaceNo,
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
                                            if (this.order(ir)) eat_amount += ir.Amount;
                                        }
                                    }
                                    _bet_amount_qn[horseNo] = -eat_amount;

                                    //平仓BET Q单
                                    #region 平仓
                                    if (_orders_bet_qn.ContainsKey(horseNo))  // _orders_bet_qn 在order方法中记录
                                    {
                                        // order从优到劣排序，如果当前order不能平仓，后续的order也肯定不能平仓
                                        IEnumerator<InvestRecordQn> orders = _orders_bet_qn[horseNo].Where(x => x.CloseAmount < x.Amount && x.OrderId > 0).OrderBy(x => x.Percent).GetEnumerator();
                                        bool flag = orders.MoveNext();
                                        foreach (WaterQnItem wi in _latest_waters.GetQnBetWater(horseNo))
                                        {
                                            if (wi.Amount <= wi.TradedAmount) continue;
                                            while (flag)
                                            {
                                                InvestRecordQn currentOrder = orders.Current;
                                                double risk, maxCloseAmount;
                                                double closeProfit = this.calcCloseProfitForBet(currentOrder.Percent, currentOrder.Limit, wi.Percent, wi.Limit, latestOdds.SpQ[horseNo], pq_win[i], out risk, out maxCloseAmount);
                                                double forcastProfit = this.calcForcastProfitForBet(currentOrder.Percent, currentOrder.Limit, latestOdds.SpQ[horseNo], pq_win[i]);
                                                if (closeProfit > forcastProfit)
                                                {
                                                    double close_amount = Math.Min(currentOrder.Amount - currentOrder.CloseAmount, Math.Ceiling((wi.Amount - wi.TradedAmount) / QN_STEP) * QN_STEP);
                                                    if (!this.checkCloseRisk(risk, maxCloseAmount, QN_STEP, horseNo, ref _close_risk_qn, ref close_amount))
                                                    {
                                                        flag = orders.MoveNext();
                                                        continue;
                                                    }
                                                    InvestRecordQn ir = new InvestRecordQn()
                                                    {
                                                        TimeKey = ToUnixTime(DateTime.Now),
                                                        Model = MODEL,
                                                        CardID = CardID,
                                                        RaceNo = RaceNo,
                                                        Direction = "EAT",
                                                        Type = "Q",
                                                        HorseNo = horseNo,
                                                        Percent = wi.Percent,
                                                        Amount = close_amount,
                                                        Limit = wi.Limit,
                                                        Odds = latestOdds.SpQ[horseNo],
                                                        Probility = pq_win[i],
                                                        CloseFlag = true,
                                                        RefItem = currentOrder,
                                                        CloseRiskInc = close_amount * risk
                                                    };
                                                    if (this.order(ir))
                                                    {
                                                        wi.TradedAmount += close_amount;
                                                        currentOrder.CloseAmount += close_amount;
                                                        _bet_amount_qn[horseNo] -= close_amount;
                                                        _close_risk_qn[horseNo] += ir.CloseRiskInc;

                                                        if (currentOrder.CloseAmount >= currentOrder.Amount)
                                                        {
                                                            // 下一个订单
                                                            flag = orders.MoveNext();
                                                        }
                                                        if (wi.TradedAmount >= wi.Amount)
                                                        {
                                                            // 下一条水位
                                                            break;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // 下单失败，尝试下一条水位
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    // 当前order不能平仓，后续order不需要再看了
                                                    flag = false;
                                                }
                                            }
                                        }
                                    }
                                    #endregion
                                }
                            }
                            #endregion

                            #region QP
                            if (pq_plc != null
                                && rqp > 0.8 && rqp < 1)
                            {
                                double sp_min = Math.Min(qp_minmax_odds_latest[0][i], qp_minmax_odds_fitted[0][i]);
                                double sp_max = Math.Max(qp_minmax_odds_latest[1][i], qp_minmax_odds_fitted[1][i]);

                                // For Bet
                                {
                                    WaterQnList vlist = _latest_waters.GetQpEatWater(horseNo).GetValuableWater(MIN_R, sp_min, pq_plc[i]);
                                    double bet_amount = 0;
                                    if (_bet_amount_qp.ContainsKey(horseNo)) bet_amount = _bet_amount_qp[horseNo];
                                    foreach (WaterQnItem w in vlist.OrderBy(x => x.Percent))
                                    {
                                        if (w.Percent > BET_MAX_DISCOUNT_QN) break;  // 超过水位上限 
                                        double O = Math.Min(w.Limit / LIMIT_SCALE, sp_min) * 100 / w.Percent;
                                        if (O * pq_plc[i] > MAX_R)
                                        {
                                            // 超过回报率上限
                                            break;
                                        }

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
                                                CardID = CardID,
                                                RaceNo = RaceNo,
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
                                            if (this.order(ir)) bet_amount += ir.Amount;
                                        }
                                    }
                                    _bet_amount_qp[horseNo] = bet_amount;

                                    //平仓EAT QP单
                                    #region 平仓
                                    if (_orders_eat_qp.ContainsKey(horseNo))  // _orders_eat_qp 在order方法中记录
                                    {
                                        // order从优到劣排序，如果当前order不能平仓，后续的order也肯定不能平仓
                                        IEnumerator<InvestRecordQn> orders = _orders_eat_qp[horseNo].Where(x => x.CloseAmount < x.Amount && x.OrderId > 0).OrderByDescending(x => x.Percent).GetEnumerator();
                                        bool flag = orders.MoveNext();
                                        foreach (WaterQnItem wi in _latest_waters.GetQpBetWater(horseNo))
                                        {
                                            if (wi.Amount <= wi.TradedAmount) continue;
                                            while (flag)
                                            {
                                                InvestRecordQn currentOrder = orders.Current;
                                                double risk, maxCloseAmount;
                                                double closeProfit = this.calcCloseProfitForEat(currentOrder.Percent, currentOrder.Limit, wi.Percent, wi.Limit, qp_minmax_odds_latest[1][i], pq_plc[i], out risk, out maxCloseAmount);
                                                double forcastProfit = this.calcForcastProfitForEat(currentOrder.Percent, currentOrder.Limit, qp_minmax_odds_latest[1][i], pq_plc[i]);
                                                if (closeProfit > forcastProfit)
                                                {
                                                    double close_amount = Math.Min(currentOrder.Amount - currentOrder.CloseAmount, Math.Ceiling((wi.Amount - wi.TradedAmount) / QN_STEP) * QN_STEP);
                                                    if (!this.checkCloseRisk(risk, maxCloseAmount, QN_STEP, horseNo, ref _close_risk_qp, ref close_amount))
                                                    {
                                                        flag = orders.MoveNext();
                                                        continue;
                                                    }
                                                    InvestRecordQn ir = new InvestRecordQn()
                                                    {
                                                        TimeKey = ToUnixTime(DateTime.Now),
                                                        Model = MODEL,
                                                        CardID = CardID,
                                                        RaceNo = RaceNo,
                                                        Direction = "BET",
                                                        Type = "QP",
                                                        HorseNo = horseNo,
                                                        Percent = wi.Percent,
                                                        Amount = close_amount,
                                                        Limit = wi.Limit,
                                                        Odds = qp_minmax_odds_latest[1][i],
                                                        Probility = pq_plc[i],
                                                        CloseFlag = true,
                                                        RefItem = currentOrder,
                                                        CloseRiskInc = close_amount * risk
                                                    };
                                                    if (this.order(ir))
                                                    {
                                                        wi.TradedAmount += close_amount;
                                                        currentOrder.CloseAmount += close_amount;
                                                        _bet_amount_qp[horseNo] += close_amount;
                                                        _close_risk_qp[horseNo] += ir.CloseRiskInc;

                                                        if (currentOrder.CloseAmount >= currentOrder.Amount)
                                                        {
                                                            // 下一个订单
                                                            flag = orders.MoveNext();
                                                        }
                                                        if (wi.TradedAmount >= wi.Amount)
                                                        {
                                                            // 下一条水位
                                                            break;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // 下单失败，尝试下一条水位
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    // 当前order不能平仓，后续order不需要再看了
                                                    flag = false;
                                                }
                                            }
                                        }
                                    }
                                    #endregion
                                }

                                // For Eat
                                {
                                    WaterQnList vlist = _latest_waters.GetQpBetWater(horseNo).GetValuableWater(MIN_R, sp_max, pq_win[i]);
                                    double eat_amount = 0;
                                    if (_bet_amount_qp.ContainsKey(horseNo)) eat_amount = -_bet_amount_qp[horseNo];
                                    foreach (WaterQnItem w in vlist.OrderByDescending(x => x.Percent))
                                    {
                                        if (w.Percent < EAT_MIN_DISCOUNT_QN) break;  // 超过水位下限 
                                        double O = 1 + (w.Percent / 100) / (Math.Min(w.Limit / LIMIT_SCALE, sp_max) - (w.Percent / 100));
                                        if (O * (1 - pq_plc[i]) > MAX_R)
                                        {
                                            // 超过回报率上限
                                            break;
                                        }

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
                                                CardID = CardID,
                                                RaceNo = RaceNo,
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
                                            if (this.order(ir)) eat_amount += ir.Amount;
                                        }
                                    }
                                    _bet_amount_qp[horseNo] = -eat_amount;

                                    //平仓BET Q单
                                    #region 平仓
                                    if (_orders_bet_qp.ContainsKey(horseNo))  // _orders_bet_qp 在order方法中记录
                                    {
                                        // order从优到劣排序，如果当前order不能平仓，后续的order也肯定不能平仓
                                        IEnumerator<InvestRecordQn> orders = _orders_bet_qp[horseNo].Where(x => x.CloseAmount < x.Amount && x.OrderId > 0).OrderBy(x => x.Percent).GetEnumerator();
                                        bool flag = orders.MoveNext();
                                        foreach (WaterQnItem wi in _latest_waters.GetQpBetWater(horseNo))
                                        {
                                            if (wi.Amount <= wi.TradedAmount) continue;
                                            while (flag)
                                            {
                                                InvestRecordQn currentOrder = orders.Current;
                                                double risk, maxCloseAmount;
                                                double closeProfit = this.calcCloseProfitForBet(currentOrder.Percent, currentOrder.Limit, wi.Percent, wi.Limit, qp_minmax_odds_latest[1][i], pq_plc[i], out risk, out maxCloseAmount);
                                                double forcastProfit = this.calcForcastProfitForBet(currentOrder.Percent, currentOrder.Limit, qp_minmax_odds_latest[0][i], pq_plc[i]); // Bet单forcast的最差赔率是最低赔率
                                                if (closeProfit > forcastProfit)
                                                {
                                                    double close_amount = Math.Min(currentOrder.Amount - currentOrder.CloseAmount, Math.Ceiling((wi.Amount - wi.TradedAmount) / QN_STEP) * QN_STEP);
                                                    if (!this.checkCloseRisk(risk, maxCloseAmount, QN_STEP, horseNo, ref _close_risk_qp, ref close_amount))
                                                    {
                                                        flag = orders.MoveNext();
                                                        continue;
                                                    }
                                                    InvestRecordQn ir = new InvestRecordQn()
                                                    {
                                                        TimeKey = ToUnixTime(DateTime.Now),
                                                        Model = MODEL,
                                                        CardID = CardID,
                                                        RaceNo = RaceNo,
                                                        Direction = "EAT",
                                                        Type = "QP",
                                                        HorseNo = horseNo,
                                                        Percent = wi.Percent,
                                                        Amount = close_amount,
                                                        Limit = wi.Limit,
                                                        Odds = qp_minmax_odds_latest[1][i],
                                                        Probility = pq_plc[i],
                                                        CloseFlag = true,
                                                        RefItem = currentOrder,
                                                        CloseRiskInc = close_amount * risk
                                                    };
                                                    if (this.order(ir))
                                                    {
                                                        wi.TradedAmount += close_amount;
                                                        currentOrder.CloseAmount += close_amount;
                                                        _bet_amount_qp[horseNo] -= close_amount;
                                                        _close_risk_qp[horseNo] += ir.CloseRiskInc;

                                                        if (currentOrder.CloseAmount >= currentOrder.Amount)
                                                        {
                                                            // 下一个订单
                                                            flag = orders.MoveNext();
                                                        }
                                                        if (wi.TradedAmount >= wi.Amount)
                                                        {
                                                            // 下一条水位
                                                            break;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // 下单失败，尝试下一条水位
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    // 当前order不能平仓，后续order不需要再看了
                                                    flag = false;
                                                }
                                            }
                                        }
                                    }
                                    #endregion
                                }
                            }
                            #endregion
                        }

                        this.orderExecute(_batch_orders_wp);
                        this.orderExecute(_batch_orders_qn);

                        this.OnProcess(new RaceProcessEventArgs() { Description = "下单完成" });
                    }
                    else
                    {
                        this.OnProcess(new RaceProcessEventArgs() { Description = "更新赔率及水位失败" });
                    }
                }
                else
                {
                    this.OnProcess(new RaceProcessEventArgs() { Description = "没有拟合好的数据" });
                }
            }

            private bool order(InvestRecordWp ir)
            {
                if (!ir.CloseFlag)
                {
                    if (ir.Direction == "BET")
                    {
                        lock (_orders_bet_wp)
                        {
                            if (!_orders_bet_wp.ContainsKey(ir.HorseNo)) _orders_bet_wp.Add(ir.HorseNo, new List<InvestRecordWp>());
                            _orders_bet_wp[ir.HorseNo].Add(ir);
                        }
                    }
                    else
                    {
                        lock (_orders_eat_wp)
                        {
                            if (!_orders_eat_wp.ContainsKey(ir.HorseNo)) _orders_eat_wp.Add(ir.HorseNo, new List<InvestRecordWp>());
                            _orders_eat_wp[ir.HorseNo].Add(ir);
                        }
                    }
                }

                if (!ir.CloseFlag)
                    this.OnProcess(new RaceProcessEventArgs() { Description = string.Format("下单：{0}-{1} {2}: {3}%*{4}/{5}({6}/{7})", ir.RaceNo, ir.Direction, ir.HorseNo, ir.Percent, ir.WinAmount, ir.PlcAmount, ir.WinLimit, ir.PlcLimit) });
                else
                    this.OnProcess(new RaceProcessEventArgs() { Description = string.Format("平仓：{0}-{1} {2}: {3}%*{4}/{5}({6}/{7})", ir.RaceNo, ir.Direction, ir.HorseNo, ir.Percent, ir.WinAmount, ir.PlcAmount, ir.WinLimit, ir.PlcLimit) });

                using (MySqlConnection conn = new MySqlConnection("server=120.24.210.35;user id=hrsdata;password=abcd0000;database=hrsdata;port=3306;charset=utf8"))
                {
                    conn.Open();

                    using (MySqlCommand cmd = new MySqlCommand(@"
insert into sl_invest_wp (time_key,model,cd_id,rc_no,direction,hs_no,percent,w_limit,p_limit,rc_time,fitting_loss,w_amt,w_od,w_prob,p_amt,p_od,p_prob,is_close,ref_id)
values (?time_key,?model,?cd_id,?rc_no,?direction,?hs_no,?percent,?w_limit,?p_limit,?rc_time,?fitting_loss,?w_amt,?w_od,?w_prob,?p_amt,?p_od,?p_prob,?is_close,?ref_id);
SELECT LAST_INSERT_ID()
", conn))
                    {
                        cmd.Parameters.Add("?time_key", MySqlDbType.Int64).Value = ir.TimeKey;
                        cmd.Parameters.Add("?model", MySqlDbType.Int32).Value = ir.Model;
                        cmd.Parameters.Add("?cd_id", MySqlDbType.UInt64).Value = ir.CardID;
                        cmd.Parameters.Add("?rc_no", MySqlDbType.Int32).Value = ir.RaceNo;
                        cmd.Parameters.Add("?direction", MySqlDbType.VarChar, 10).Value = ir.Direction;
                        cmd.Parameters.Add("?hs_no", MySqlDbType.Int32).Value = int.Parse(ir.HorseNo);
                        cmd.Parameters.Add("?percent", MySqlDbType.Decimal).Value = ir.Percent;
                        cmd.Parameters.Add("?w_limit", MySqlDbType.Decimal).Value = ir.WinLimit;
                        cmd.Parameters.Add("?p_limit", MySqlDbType.Decimal).Value = ir.PlcLimit;
                        cmd.Parameters.Add("?rc_time", MySqlDbType.DateTime).Value = StartTime;
                        cmd.Parameters.Add("?fitting_loss", MySqlDbType.Decimal).Value = ir.FittingLoss;
                        cmd.Parameters.Add("?w_amt", MySqlDbType.Decimal).Value = ir.WinAmount;
                        cmd.Parameters.Add("?w_od", MySqlDbType.Decimal).Value = ir.WinOdds;
                        cmd.Parameters.Add("?w_prob", MySqlDbType.Decimal).Value = ir.WinProbility;
                        cmd.Parameters.Add("?p_amt", MySqlDbType.Decimal).Value = ir.PlcAmount;
                        cmd.Parameters.Add("?p_od", MySqlDbType.Decimal).Value = ir.PlcOdds;
                        cmd.Parameters.Add("?p_prob", MySqlDbType.Decimal).Value = ir.PlcProbility;
                        cmd.Parameters.Add("?is_close", MySqlDbType.UByte).Value = ir.CloseFlag ? 1 : 0;
                        cmd.Parameters.Add("?ref_id", MySqlDbType.UInt32).Value = ir.RefItem == null ? 0 : ir.RefItem.ID;

                        ir.ID = Convert.ToUInt32(cmd.ExecuteScalar());
                    }
                }

                _batch_orders_wp.Add(ir);

                return true;
            }

            private bool order(InvestRecordQn ir)
            {
                if (!ir.CloseFlag)
                {
                    if (ir.Direction == "BET")
                    {
                        if (ir.Type == "Q")
                        {
                            lock (_orders_bet_qn)
                            {
                                if (!_orders_bet_qn.ContainsKey(ir.HorseNo)) _orders_bet_qn.Add(ir.HorseNo, new List<InvestRecordQn>());
                                _orders_bet_qn[ir.HorseNo].Add(ir);
                            }
                        }
                        else
                        {
                            lock (_orders_bet_qp)
                            {
                                if (!_orders_bet_qp.ContainsKey(ir.HorseNo)) _orders_bet_qp.Add(ir.HorseNo, new List<InvestRecordQn>());
                                _orders_bet_qp[ir.HorseNo].Add(ir);
                            }
                        }
                    }
                    else
                    {
                        if (ir.Type == "Q")
                        {
                            lock (_orders_eat_qn)
                            {
                                if (!_orders_eat_qn.ContainsKey(ir.HorseNo)) _orders_eat_qn.Add(ir.HorseNo, new List<InvestRecordQn>());
                                _orders_eat_qn[ir.HorseNo].Add(ir);
                            }
                        }
                        else
                        {
                            lock (_orders_eat_qp)
                            {
                                if (!_orders_eat_qp.ContainsKey(ir.HorseNo)) _orders_eat_qp.Add(ir.HorseNo, new List<InvestRecordQn>());
                                _orders_eat_qp[ir.HorseNo].Add(ir);
                            }
                        }
                    }
                }

                if (!ir.CloseFlag)
                    this.OnProcess(new RaceProcessEventArgs() { Description = string.Format("下单：{0}-{1} {2} {3}: {4}%*{5}({6})", ir.RaceNo, ir.Direction, ir.Type, ir.HorseNo, ir.Percent, ir.Amount, ir.Limit) });
                else
                    this.OnProcess(new RaceProcessEventArgs() { Description = string.Format("平仓：{0}-{1} {2} {3}: {4}%*{5}({6})", ir.RaceNo, ir.Direction, ir.Type, ir.HorseNo, ir.Percent, ir.Amount, ir.Limit) });

                using (MySqlConnection conn = new MySqlConnection("server=120.24.210.35;user id=hrsdata;password=abcd0000;database=hrsdata;port=3306;charset=utf8"))
                {
                    conn.Open();

                    using (MySqlCommand cmd = new MySqlCommand(@"
insert into sl_invest_qn(time_key,model,cd_id,rc_no,direction,q_type,hs_no,percent,q_limit,rc_time,fitting_loss,amt,od,prob,is_close,ref_id)
values (?time_key,?model,?cd_id,?rc_no,?direction,?q_type,?hs_no,?percent,?q_limit,?rc_time,?fitting_loss,?amt,?od,?prob,?is_close,?ref_id);
SELECT LAST_INSERT_ID()
", conn))
                    {
                        cmd.Parameters.Add("?time_key", MySqlDbType.Int64).Value = ir.TimeKey;
                        cmd.Parameters.Add("?model", MySqlDbType.Int32).Value = ir.Model;
                        cmd.Parameters.Add("?cd_id", MySqlDbType.UInt64).Value = ir.CardID;
                        cmd.Parameters.Add("?rc_no", MySqlDbType.Int32).Value = ir.RaceNo;
                        cmd.Parameters.Add("?direction", MySqlDbType.VarChar, 10).Value = ir.Direction;
                        cmd.Parameters.Add("?q_type", MySqlDbType.VarChar, 10).Value = ir.Type;
                        cmd.Parameters.Add("?hs_no", MySqlDbType.VarChar, 20).Value = ir.HorseNo;
                        cmd.Parameters.Add("?percent", MySqlDbType.Decimal).Value = ir.Percent;
                        cmd.Parameters.Add("?q_limit", MySqlDbType.Decimal).Value = ir.Limit;
                        cmd.Parameters.Add("?rc_time", MySqlDbType.DateTime).Value = StartTime;
                        cmd.Parameters.Add("?fitting_loss", MySqlDbType.Decimal).Value = ir.FittingLoss;
                        cmd.Parameters.Add("?amt", MySqlDbType.Decimal).Value = ir.Amount;
                        cmd.Parameters.Add("?od", MySqlDbType.Decimal).Value = ir.Odds;
                        cmd.Parameters.Add("?prob", MySqlDbType.Decimal).Value = ir.Probility;
                        cmd.Parameters.Add("?is_close", MySqlDbType.UByte).Value = ir.CloseFlag ? 1 : 0;
                        cmd.Parameters.Add("?ref_id", MySqlDbType.UInt32).Value = ir.RefItem == null ? 0 : ir.RefItem.ID;

                        ir.ID = Convert.ToUInt32(cmd.ExecuteScalar());
                    }
                }

                _batch_orders_qn.Add(ir);

                return true;
            }

            private void orderExecute(List<InvestRecordWp> batch)
            {
                if (batch.Count == 0) return;

                Dictionary<string, OrderWp> grouped = new Dictionary<string, OrderWp>();
                foreach (InvestRecordWp item in batch)
                {
                    string key = string.Format("{0}-{1}-{2}-{3}-{4}", item.Direction, item.HorseNo, item.Percent, item.WinLimit, item.PlcLimit);
                    if (!grouped.ContainsKey(key))
                    {
                        grouped.Add(key, new OrderWp()
                        {
                            CardID = item.CardID,
                            RaceNo = item.RaceNo,
                            RaceDate = this.RaceDate,
                            RaceType = this.RaceType,
                            Direction = item.Direction,
                            HorseNo = item.HorseNo,
                            Percent = item.Percent,
                            WinLimit = item.WinLimit,
                            PlcLimit = item.PlcLimit
                        });
                    }
                    grouped[key].Amount += item.WinLimit > 0 ? item.WinAmount : item.PlcAmount;
                    grouped[key].Records.Add(item);
                }

                using (MySqlConnection conn = new MySqlConnection("server=120.24.210.35;user id=hrsdata;password=abcd0000;database=hrsdata;port=3306;charset=utf8"))
                {
                    conn.Open();

                    using (MySqlCommand cmd = new MySqlCommand(@"
insert into sl_order_wp(cd_id,rc_date,rc_type,rc_no,direction,hs_no,percent,w_limit,p_limit,amount)
values (?cd_id,?rc_date,?rc_type,?rc_no,?direction,?hs_no,?percent,?w_limit,?p_limit,?amount);
SELECT LAST_INSERT_ID()
", conn))
                    {
                        cmd.Parameters.Add("?cd_id", MySqlDbType.UInt64).Value = this.CardID;
                        cmd.Parameters.Add("?rc_date", MySqlDbType.VarChar, 20).Value = this.RaceDate;
                        cmd.Parameters.Add("?rc_type", MySqlDbType.VarChar, 10).Value = this.RaceType;
                        cmd.Parameters.Add("?rc_no", MySqlDbType.Int32).Value = this.RaceNo;
                        cmd.Parameters.Add("?direction", MySqlDbType.VarChar, 10);
                        cmd.Parameters.Add("?hs_no", MySqlDbType.Int32);
                        cmd.Parameters.Add("?percent", MySqlDbType.Decimal);
                        cmd.Parameters.Add("?w_limit", MySqlDbType.Decimal);
                        cmd.Parameters.Add("?p_limit", MySqlDbType.Decimal);
                        cmd.Parameters.Add("?amount", MySqlDbType.Decimal);

                        foreach (OrderWp order in grouped.Values)
                        {
                            cmd.Parameters["?direction"].Value = order.Direction;
                            cmd.Parameters["?hs_no"].Value = int.Parse(order.HorseNo);
                            cmd.Parameters["?percent"].Value = order.Percent;
                            cmd.Parameters["?w_limit"].Value = order.WinLimit;
                            cmd.Parameters["?p_limit"].Value = order.PlcLimit;
                            cmd.Parameters["?amount"].Value = order.Amount;

                            order.ID = Convert.ToUInt32(cmd.ExecuteScalar());

                            using (MySqlCommand cmd2 = new MySqlCommand())
                            {
                                cmd2.CommandText = "update sl_invest_wp set order_id = ?order_id where id in (" + string.Join(",", order.Records.Select(x => x.ID.ToString()).ToArray()) + ")";
                                cmd2.Connection = conn;
                                cmd2.Parameters.AddWithValue("?order_id", order.ID);
                                cmd2.ExecuteNonQuery();
                            }

                            if (this.orderApi(order))
                            {
                                foreach (InvestRecordWp item in order.Records)
                                {
                                    item.OrderId = order.ID;
                                }

                                using (MySqlCommand cmd2 = new MySqlCommand())
                                {
                                    cmd2.CommandText = "update sl_order_wp set state = 1 where id = ?order_id";
                                    cmd2.Connection = conn;
                                    cmd2.Parameters.AddWithValue("?order_id", order.ID);
                                    cmd2.ExecuteNonQuery();
                                }
                            }
                            else
                            {
                                foreach (InvestRecordWp item in order.Records)
                                {
                                    if (item.CloseFlag)
                                    {
                                        item.RefItem.CloseAmount -= (item.WinLimit > 0 ? item.WinAmount : item.PlcAmount);
                                        _close_risk_win[item.HorseNo] -= item.CloseRiskIncWin;
                                        _close_risk_plc[item.HorseNo] -= item.CloseRiskIncPlc;
                                    }
                                    if (item.Direction == "BET")
                                    {
                                        _bet_amount_win[item.HorseNo] -= item.WinAmount;
                                        _bet_amount_plc[item.HorseNo] -= item.PlcAmount;
                                    }
                                    else
                                    {
                                        _bet_amount_win[item.HorseNo] += item.WinAmount;
                                        _bet_amount_plc[item.HorseNo] += item.PlcAmount;
                                    }
                                }
                            }
                        }
                    }
                    
                }

                batch.Clear();
            }

            private void orderExecute(List<InvestRecordQn> batch)
            {
                if (batch.Count == 0) return;

                Dictionary<string, OrderQn> grouped = new Dictionary<string, OrderQn>();
                foreach (InvestRecordQn item in batch)
                {
                    string key = string.Format("{0}-{1}-{2}-{3}-{4}", item.Direction, item.Type, item.HorseNo, item.Percent, item.Limit);
                    if (!grouped.ContainsKey(key))
                    {
                        grouped.Add(key, new OrderQn()
                        {
                            CardID = item.CardID,
                            RaceNo = item.RaceNo,
                            RaceDate = this.RaceDate,
                            RaceType = this.RaceType,
                            Direction = item.Direction,
                            Type = item.Type,
                            HorseNo = item.HorseNo,
                            Percent = item.Percent,
                            Limit = item.Limit
                        });
                    }
                    grouped[key].Amount += item.Amount;
                    grouped[key].Records.Add(item);
                }

                using (MySqlConnection conn = new MySqlConnection("server=120.24.210.35;user id=hrsdata;password=abcd0000;database=hrsdata;port=3306;charset=utf8"))
                {
                    conn.Open();

                    using (MySqlCommand cmd = new MySqlCommand(@"
insert into sl_order_qn(cd_id,rc_date,rc_type,rc_no,direction,q_type,hs_no,percent,q_limit,amount)
values (?cd_id,?rc_date,?rc_type,?rc_no,?direction,?q_type,?hs_no,?percent,?q_limit,?amount);
SELECT LAST_INSERT_ID()
", conn))
                    {
                        cmd.Parameters.Add("?cd_id", MySqlDbType.UInt64).Value = this.CardID;
                        cmd.Parameters.Add("?rc_date", MySqlDbType.VarChar, 20).Value = this.RaceDate;
                        cmd.Parameters.Add("?rc_type", MySqlDbType.VarChar, 10).Value = this.RaceType;
                        cmd.Parameters.Add("?rc_no", MySqlDbType.Int32).Value = this.RaceNo;
                        cmd.Parameters.Add("?direction", MySqlDbType.VarChar, 10);
                        cmd.Parameters.Add("?q_type", MySqlDbType.VarChar, 10);
                        cmd.Parameters.Add("?hs_no", MySqlDbType.VarChar, 20);
                        cmd.Parameters.Add("?percent", MySqlDbType.Decimal);
                        cmd.Parameters.Add("?q_limit", MySqlDbType.Decimal);
                        cmd.Parameters.Add("?amount", MySqlDbType.Decimal);

                        foreach (OrderQn order in grouped.Values)
                        {
                            cmd.Parameters["?direction"].Value = order.Direction;
                            cmd.Parameters["?hs_no"].Value = order.HorseNo;
                            cmd.Parameters["?q_type"].Value = order.Type;
                            cmd.Parameters["?percent"].Value = order.Percent;
                            cmd.Parameters["?q_limit"].Value = order.Limit;
                            cmd.Parameters["?amount"].Value = order.Amount;

                            order.ID = Convert.ToUInt32(cmd.ExecuteScalar());

                            using (MySqlCommand cmd2 = new MySqlCommand())
                            {
                                cmd2.CommandText = "update sl_invest_qn set order_id = ?order_id where id in (" + string.Join(",", order.Records.Select(x => x.ID.ToString()).ToArray()) + ")";
                                cmd2.Connection = conn;
                                cmd2.Parameters.AddWithValue("?order_id", order.ID);
                                cmd2.ExecuteNonQuery();
                            }

                            if (this.orderApi(order))
                            {
                                foreach (InvestRecordQn item in order.Records)
                                {
                                    item.OrderId = order.ID;
                                }

                                using (MySqlCommand cmd2 = new MySqlCommand())
                                {
                                    cmd2.CommandText = "update sl_order_qn set state = 1 where id = ?order_id";
                                    cmd2.Connection = conn;
                                    cmd2.Parameters.AddWithValue("?order_id", order.ID);
                                    cmd2.ExecuteNonQuery();
                                }
                            }
                            else
                            {
                                foreach (InvestRecordQn item in order.Records)
                                {
                                    if (item.CloseFlag)
                                    {
                                        item.RefItem.CloseAmount -= item.Amount;
                                        if (item.Type == "Q")
                                            _close_risk_qn[item.HorseNo] -= item.CloseRiskInc;
                                        else
                                            _close_risk_qp[item.HorseNo] -= item.CloseRiskInc;
                                    }
                                    if (item.Direction == "BET")
                                    {
                                        if (item.Type == "Q")
                                            _bet_amount_qn[item.HorseNo] -= item.Amount;
                                        else
                                            _bet_amount_qp[item.HorseNo] -= item.Amount;
                                    }
                                    else
                                    {
                                        if (item.Type == "Q")
                                            _bet_amount_qn[item.HorseNo] += item.Amount;
                                        else
                                            _bet_amount_qp[item.HorseNo] += item.Amount;
                                    }
                                }
                            }
                        }
                    }

                }

                batch.Clear();
            }

            private static string[] __accounts__ = new string[] { "cttjoag03ur03", "cttjoag03ur04" }; 

            private bool orderApi(OrderWp order)
            {
                string account = __accounts__[order.ID % __accounts__.Length];

                List<string> parameters = new List<string>();
                parameters.Add("acc_name=" + account);
                parameters.Add("acc_channel=CT");
                parameters.Add("race_type=" + order.RaceType);
                parameters.Add("race_date=" + order.RaceDate);
                parameters.Add("race_no=" + order.RaceNo.ToString());
                parameters.Add("horse_no=" + order.HorseNo);
                parameters.Add("direction=" + order.Direction);
                parameters.Add("percent=" + order.Percent.ToString());
                parameters.Add("im_order=1");
                if (order.WinLimit > 0 && order.PlcLimit == 0)
                {
                    parameters.Add("amount1=" + order.Amount.ToString());
                    parameters.Add("limit1=" + order.WinLimit.ToString());
                    parameters.Add("amount2=0");
                    parameters.Add("limit2=0");
                    parameters.Add("wptck=0");
                    parameters.Add("wtck=1");
                    parameters.Add("ptck=0");
                }
                else if (order.PlcLimit > 0)
                {
                    parameters.Add("amount1=0");
                    parameters.Add("limit1=0");
                    parameters.Add("amount2=" + order.Amount.ToString());
                    parameters.Add("limit2=" + order.PlcLimit.ToString());
                    parameters.Add("wptck=0");
                    parameters.Add("wtck=0");
                    parameters.Add("ptck=1");
                }
                else
                {
                    parameters.Add("amount1=" + order.Amount.ToString());
                    parameters.Add("limit1=" + order.WinLimit.ToString());
                    parameters.Add("amount2=" + order.Amount.ToString());
                    parameters.Add("limit2=" + order.PlcLimit.ToString());
                    parameters.Add("wptck=1");
                    parameters.Add("wtck=0");
                    parameters.Add("ptck=0");
                }

                string url, response;
                url = string.Format("http://120.24.210.35:3000/biz/trading/order_wp?{0}", string.Join("&", parameters.ToArray()));
                response = callApi(url);
                if (response != null)
                {
                    JObject jo = (JObject)JsonConvert.DeserializeObject(response);
                    if (jo != null && (string)jo["STS"] == "OK")
                    {
                        this.OnProcess(new RaceProcessEventArgs()
                        {
                            Description = string.Format("实际下单成功：{0}-{1} {2}: {3}%*{4}({5}/{6})", order.RaceNo, order.Direction, order.HorseNo, order.Percent, order.Amount, order.WinLimit, order.PlcLimit),
                            Detail = string.Format("url: {0}\nresponse: {1}", url, response)
                        });
                        return true;
                    }
                }

                this.OnProcess(new RaceProcessEventArgs()
                {
                    Description = string.Format("实际下单失败：{0}-{1} {2}: {3}%*{4}({5}/{6})", order.RaceNo, order.Direction, order.HorseNo, order.Percent, order.Amount, order.WinLimit, order.PlcLimit),
                    Detail = string.Format("url: {0}\nresponse: {1}", url, response)
                });
                return false;
            }

            private bool orderApi(OrderQn order)
            {
                string account = __accounts__[order.ID % __accounts__.Length];

                List<string> parameters = new List<string>();
                parameters.Add("acc_name=" + account);
                parameters.Add("acc_channel=CT");
                parameters.Add("race_type=" + order.RaceType);
                parameters.Add("race_date=" + order.RaceDate);
                parameters.Add("race_no=" + order.RaceNo.ToString());
                parameters.Add("horse_arr=" + order.HorseNo.Replace('-', '|'));
                parameters.Add("combo=0");
                parameters.Add("fc_type=" + (order.Type == "Q" ? "0" : "1"));
                parameters.Add("direction=" + order.Direction);
                parameters.Add("amount=" + order.Amount.ToString());
                parameters.Add("percent=" + order.Percent.ToString());
                parameters.Add("limit=" + order.Limit.ToString());
                parameters.Add("im_order=1");

                string url, response;
                url = string.Format("http://120.24.210.35:3000/biz/trading/order_qn?{0}", string.Join("&", parameters.ToArray()));
                response = callApi(url);

                if (response != null)
                {
                    JObject jo = (JObject)JsonConvert.DeserializeObject(response);
                    if (jo != null && (string)jo["STS"] == "OK")
                    {
                        this.OnProcess(new RaceProcessEventArgs()
                        {
                            Description = string.Format("实际下单成功：{0}-{1} {2} {3}: {4}%*{5}({6})", order.RaceNo, order.Direction, order.Type, order.HorseNo, order.Percent, order.Amount, order.Limit),
                            Detail = string.Format("url: {0}\nresponse: {1}", url, response)
                        });
                        return true;
                    }
                }

                this.OnProcess(new RaceProcessEventArgs()
                {
                    Description = string.Format("实际下单失败：{0}-{1} {2} {3}: {4}%*{5}({6})", order.RaceNo, order.Direction, order.Type, order.HorseNo, order.Percent, order.Amount, order.Limit),
                    Detail = string.Format("url: {0}\nresponse: {1}", url, response)
                });
                return false;
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

            public bool updateOddsData()
            {
                HrsTable table = this.updateWpOddsData();
                if (table == null)
                {
                    return false;
                }
                else
                {
                    if (!this.updateQnOddsData(table))
                    {
                        return false;
                    }

                    lock (this)
                    {
                        if (_fitted_odds != null)
                        {
                            // 复制最后的拟合结果
                            for (int i = 0, j = 0; i < _fitted_odds.Count && j < table.Count; )
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

                            table.E = double.MaxValue;
                        }

                        _latest_odds = table;

                        return true;
                    }
                }
            }

            private HrsTable updateWpOddsData()
            {
                string url, rawInfo;
                url = string.Format("http://120.24.210.35:3000/data/market/get_tote_wp_latest_raw_info?race_id={0}&card_id={1}", RaceID, CardID);
                rawInfo = getRawInfoByAPI(url);

                HrsTable table = new HrsTable() { PLC_SPLIT_POS = this.PLC_SPLIT_POS };
                if (parseWpOddsRaw(rawInfo, table))
                    return table;
                else
                    return null;
            }

            public static bool parseWpOddsRaw(string rawInfo, HrsTable table)
            {
                if (rawInfo != null)
                {
                    JArray ja = (JArray)JsonConvert.DeserializeObject(rawInfo);
                    if (ja != null)
                    {
                        foreach (JObject jo in ja)
                        {
                            double win = (double)jo["win"];
                            double plc = (double)jo["plc"];
                            if (win != 0 && plc != 0)
                            {
                                table.Add(new Hrs(jo["horseNo"].ToString(), win, plc));
                            }
                        }
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            private bool updateQnOddsData(HrsTable table)
            {
                string url, rawInfo;
                url = string.Format("http://120.24.210.35:3000/data/market/get_tote_qn_latest_raw_info?race_id={0}&card_id={1}", RaceID, CardID);
                rawInfo = getRawInfoByAPI(url);
                return parseQnOddsRaw(rawInfo, table);
            }

            public static bool parseQnOddsRaw(string rawInfo, HrsTable table)
            {
                if (rawInfo != null)
                {
                    JObject jo = (JObject)JsonConvert.DeserializeObject(rawInfo);
                    if (jo != null)
                    {
                        parseQnTote((string)jo["text_q"], table.SpQ);
                        parseQnTote((string)jo["text_qp"], table.SpQp);

                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            private bool updateBetWpWatersData()
            {
                string url, rawInfo;
                url = string.Format("http://120.24.210.35:3000/data/market/get_discount_wp_latest_raw_info?card_id={0}&direction=0", CardID);
                rawInfo = getRawInfoByAPI(url);

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
                url = string.Format("http://120.24.210.35:3000/data/market/get_discount_wp_latest_raw_info?card_id={0}&direction=1", CardID);
                rawInfo = getRawInfoByAPI(url);

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
                url = string.Format("http://120.24.210.35:3000/data/market/get_discount_qn_latest_raw_info?card_id={0}&type=Q&direction=0", CardID);
                rawInfo = getRawInfoByAPI(url);

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
                url = string.Format("http://120.24.210.35:3000/data/market/get_discount_qn_latest_raw_info?card_id={0}&type=Q&direction=1", CardID);
                rawInfo = getRawInfoByAPI(url);

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
                url = string.Format("http://120.24.210.35:3000/data/market/get_discount_qn_latest_raw_info?card_id={0}&type=QP&direction=0", CardID);
                rawInfo = getRawInfoByAPI(url);

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
                url = string.Format("http://120.24.210.35:3000/data/market/get_discount_qn_latest_raw_info?card_id={0}&type=QP&direction=1", CardID);
                rawInfo = getRawInfoByAPI(url);

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

            private static void parseQnTote(string text, Dictionary<string, double> dict)
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
                        if (raceNo != RaceNo) continue;

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
                        if (raceNo != RaceNo) continue;

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

            public static string getRawInfoByAPI(string url)
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

            public static string callApi(string url)
            {
                System.Net.WebClient wc = new System.Net.WebClient();
                using (System.IO.Stream s = wc.OpenRead(url))
                {
                    using (System.IO.StreamReader sr = new System.IO.StreamReader(s))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
        }

    }
}

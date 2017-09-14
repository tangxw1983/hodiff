using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Threading;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HO偏差
{
    public partial class FormBuildTestDataSpecifiedPoint : Form
    {
        public FormBuildTestDataSpecifiedPoint()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.button1.Enabled = false;

            Thread t = new Thread(delegate()
            {
                this.handle();

                this.Invoke(new MethodInvoker(delegate()
                {
                    this.button1.Enabled = true;
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        private static DateTime UNIXTIME_BASE = new DateTime(1970, 1, 1, 8, 0, 0);

        private long ToUnixTime(DateTime time)
        {
            return (long)time.Subtract(UNIXTIME_BASE).TotalMilliseconds;
        }

        private MySqlDataReader TryGetReader(MySqlCommand cmd)
        {
            DateTime st = DateTime.Now;
            Exception lastEx = null;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    if (cmd.Connection.State == ConnectionState.Closed) cmd.Connection.Open();
                    MySqlDataReader dr = cmd.ExecuteReader();

                    using (System.IO.FileStream fs = new System.IO.FileStream("db.time.log", System.IO.FileMode.Append))
                    {
                        using (System.IO.StreamWriter sw = new System.IO.StreamWriter(fs))
                        {
                            double ts = DateTime.Now.Subtract(st).TotalMilliseconds;

                            if (ts > 5000)
                            {
                                StringBuilder sb = new StringBuilder();
                                foreach (MySqlParameter p in cmd.Parameters)
                                {
                                    sb.Append(string.Format("{0}:{1}\r\n", p.ParameterName, p.Value));
                                }

                                sw.WriteLine("{0:yyyy-MM-dd HH:mm:ss} > consume time:{1,6:0}ms, failed times:{2}\r\nsql:\r\n{3}\r\nparams:\r\n{4}", DateTime.Now, ts, i, cmd.CommandText, sb.ToString());
                            }
                            else
                            {
                                sw.WriteLine("{0:yyyy-MM-dd HH:mm:ss} > consume time:{1,6:0}ms", DateTime.Now, ts);
                            }
                        }
                    }

                    return dr;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                }

                if (i < 9)
                {
                    Thread.Sleep(10000);
                }
            }
            throw new Exception("Try get reader failed 10 times", lastEx);
        }

        private bool TryRead(MySqlDataReader dr)
        {
            Exception lastEx = null;
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    return dr.Read();
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                }

                if (i<9)
                {
                    Thread.Sleep(10000);
                }
            }

            throw new Exception("Try read data failed 10 times", lastEx);
        }

        private string getRawInfoByAPI(string type, string id)
        {
            System.Net.WebClient wc = new System.Net.WebClient();
            using (System.IO.Stream s = wc.OpenRead(string.Format("http://120.24.210.35:3000/data/market/{0}?record_id={1}", type, id)))
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

        private void handle()
        {
            using (MySqlConnection conn = new MySqlConnection("server=120.24.210.35;user id=hrsdata;password=abcd0000;database=hrsdata;port=3306;charset=utf8"))
            {
                conn.Open();

                using (MySqlCommand cmd = new MySqlCommand(@"
SELECT a.*, b.`id` card_id FROM ct_race a
INNER JOIN ct_card b ON b.`tournament_id` = a.`tournament_id` AND b.`tote_type` = 'HK'
WHERE a.time_text IS NOT NULL
AND a.race_loc = 3 
and a.id >= 699540
LIMIT 10
", conn))
                {
                    using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                    {
                        using (DataTable table = new DataTable())
                        {
                            da.Fill(table);

                            List<RaceData> data = new List<RaceData>();

                            #region 建立时间点并获取Tote
                            foreach (DataRow row in table.Rows)
                            {
                                string date_str = (string)row["race_date"];
                                string time_str = (string)row["time_text"];

                                Match md = Regex.Match(date_str, @"^(\d{2})-(\d{2})-(\d{4})$");
                                // Match mt = Regex.Match(time_str, @"^(\d{2})\:(\d{2})(pm|am)$");
                                if (md.Success)
                                {
                                    RaceData race = new RaceData();
                                    race.StartTime = DateTime.Parse(string.Format("{0}-{1}-{2} {3}",
                                        md.Groups[3].Value,
                                        md.Groups[2].Value,
                                        md.Groups[1].Value,
                                        //mt.Groups[3].Value == "pm" ? int.Parse(mt.Groups[1].Value) + 12 : int.Parse(mt.Groups[1].Value),
                                        //mt.Groups[2].Value
                                        time_str));
                                    race.CardID = (ulong)row["card_id"];
                                    race.RaceNo = (int)row["race_no"];
                                    data.Add(race);

                                    // 从开始时间往前推，每5分钟一个点，直到开赛前55分钟共12个点
                                    // 每个点前后2.5分钟区间，取最靠近的数据
                                    // 如果WP_TOTE/QN_TOTE/WP_BET_DISCOUNT/WP_EAT_DISCOUNT/QN_BET_DISCOUNT/QN_EAT_DISCOUNT/QP_BET_DISCOUNT/QP_EAT_DISCOUNT任意一项没有数据，则丢弃该时间点

                                    // 建立时间点
                                    long start_time_key = ToUnixTime(race.StartTime);
                                    for (int i = 0; i < 12; i++)
                                    {
                                        race.Add(start_time_key - i * 5 * 60 * 1000, new RaceDataItem());
                                    }

                                    foreach (long tp in race.Keys.ToArray())
                                    {
                                        using (MySqlCommand cmd2 = new MySqlCommand("select * from ct_raw_wp_tote where rc_id = ?rc_id and cd_id = ?cd_id and record_time between ?tb and ?te order by abs(record_time-?tp) limit 1", conn))
                                        {
                                            cmd2.CommandTimeout = 120000;
                                            cmd2.Parameters.AddWithValue("?rc_id", row["id"]);
                                            cmd2.Parameters.AddWithValue("?cd_id", row["card_id"]);
                                            cmd2.Parameters.AddWithValue("?tb", tp - 150 * 1000);
                                            cmd2.Parameters.AddWithValue("?te", tp + 150 * 1000);
                                            cmd2.Parameters.AddWithValue("?tp", tp);

                                            using (MySqlDataReader dr = this.TryGetReader(cmd2))
                                            {
                                                if (this.TryRead(dr))
                                                {
                                                    RaceDataItem item = race[tp];

                                                    string rawInfo;
                                                    if (!object.Equals(dr["raw_info"], DBNull.Value))
                                                    {
                                                        rawInfo = (string)dr["raw_info"];
                                                    }
                                                    else
                                                    {
                                                        rawInfo = this.getRawInfoByAPI("get_tote_wp_raw_info", dr["id"].ToString());
                                                    }

                                                    if (rawInfo != null)
                                                    {
                                                        JArray ja = (JArray)JsonConvert.DeserializeObject(rawInfo);
                                                        foreach (JObject jo in ja)
                                                        {
                                                            double win = (double)jo["win"];
                                                            double plc = (double)jo["plc"];
                                                            if (win != 0 && plc != 0)
                                                            {
                                                                item.Odds.Add(new Hrs(jo["horseNo"].ToString(), win, plc));
                                                            }

                                                        }
                                                    }
                                                    else
                                                    {
                                                        race.Remove(tp);
                                                        continue;
                                                    }
                                                }
                                                else
                                                {
                                                    race.Remove(tp);
                                                    continue;
                                                }
                                            }
                                        }

                                        using (MySqlCommand cmd2 = new MySqlCommand("select * from ct_raw_qn_tote where rc_id = ?rc_id and cd_id = ?cd_id and record_time between ?tb and ?te order by abs(record_time-?tp) limit 1", conn))
                                        {
                                            cmd2.CommandTimeout = 120000;
                                            cmd2.Parameters.AddWithValue("?rc_id", row["id"]);
                                            cmd2.Parameters.AddWithValue("?cd_id", row["card_id"]);
                                            cmd2.Parameters.AddWithValue("?tb", tp - 150 * 1000);
                                            cmd2.Parameters.AddWithValue("?te", tp + 150 * 1000);
                                            cmd2.Parameters.AddWithValue("?tp", tp);

                                            using (MySqlDataReader dr = this.TryGetReader(cmd2))
                                            {
                                                if (this.TryRead(dr))
                                                {
                                                    RaceDataItem item = race[tp];

                                                    string rawInfo;
                                                    if (!object.Equals(dr["raw_info"], DBNull.Value))
                                                    {
                                                        rawInfo = (string)dr["raw_info"];
                                                    }
                                                    else
                                                    {
                                                        rawInfo = this.getRawInfoByAPI("get_tote_qn_raw_info", dr["id"].ToString());
                                                    }

                                                    if (rawInfo != null)
                                                    {
                                                        JObject jo = (JObject)JsonConvert.DeserializeObject(rawInfo);
                                                        this.parseQnTote((string)jo["text_q"], item.Odds.SpQ);
                                                        this.parseQnTote((string)jo["text_qp"], item.Odds.SpQp);
                                                    }
                                                    else
                                                    {
                                                        race.Remove(tp);
                                                        continue;
                                                    }
                                                }
                                                else
                                                {
                                                    race.Remove(tp);
                                                    continue;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            #endregion

                            #region 获取Discount
                            foreach (ulong cardId in data.Where(x => x.Count > 0).Select(x => x.CardID).Distinct().ToArray())
                            {
                                foreach (long tp in data.Where(x => x.CardID == cardId).SelectMany(x => x.Keys).Distinct().ToArray())
                                {
                                    Dictionary<int, RaceDataItem> items = new Dictionary<int, RaceDataItem>();
                                    foreach (RaceData race in data.Where(x => x.CardID == cardId))
                                    {
                                        if (race.ContainsKey(tp))
                                        {
                                            items.Add(race.RaceNo, race[tp]);
                                        }
                                    }

                                    using (MySqlCommand cmd2 = new MySqlCommand("select * from ct_raw_wp_discount where cd_id = ?cd_id and direction = ?d and record_time between ?tb and ?te order by abs(record_time-?tp) limit 1", conn))
                                    {
                                        cmd2.CommandTimeout = 120000;
                                        cmd2.Parameters.AddWithValue("?cd_id", cardId);
                                        cmd2.Parameters.AddWithValue("?tb", tp - 150 * 1000);
                                        cmd2.Parameters.AddWithValue("?te", tp + 150 * 1000);
                                        cmd2.Parameters.AddWithValue("?tp", tp);
                                        MySqlParameter pD = cmd2.Parameters.Add("?d", MySqlDbType.Byte);

                                        pD.Value = 0;
                                        using (MySqlDataReader dr = this.TryGetReader(cmd2))
                                        {
                                            if (this.TryRead(dr))
                                            {
                                                string rawInfo;
                                                if (!object.Equals(dr["raw_info"], DBNull.Value))
                                                {
                                                    rawInfo = (string)dr["raw_info"];
                                                }
                                                else
                                                {
                                                    rawInfo = this.getRawInfoByAPI("get_discount_wp_raw_info", dr["id"].ToString());
                                                }

                                                if (rawInfo != null)
                                                {
                                                    this.parseWpDiscount(rawInfo, items, dr["direction"].ToString());
                                                }
                                                else
                                                {
                                                    foreach (RaceData race in data.Where(x => x.CardID == cardId))
                                                    {
                                                        race.Remove(tp);
                                                    }
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                foreach (RaceData race in data.Where(x => x.CardID == cardId))
                                                {
                                                    race.Remove(tp);
                                                }
                                                continue;
                                            }
                                        }

                                        pD.Value = 1;
                                        using (MySqlDataReader dr = this.TryGetReader(cmd2))
                                        {
                                            if (this.TryRead(dr))
                                            {
                                                string rawInfo;
                                                if (!object.Equals(dr["raw_info"], DBNull.Value))
                                                {
                                                    rawInfo = (string)dr["raw_info"];
                                                }
                                                else
                                                {
                                                    rawInfo = this.getRawInfoByAPI("get_discount_wp_raw_info", dr["id"].ToString());
                                                }

                                                if (rawInfo != null)
                                                {
                                                    this.parseWpDiscount(rawInfo, items, dr["direction"].ToString());
                                                }
                                                else
                                                {
                                                    foreach (RaceData race in data.Where(x => x.CardID == cardId))
                                                    {
                                                        race.Remove(tp);
                                                    }
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                foreach (RaceData race in data.Where(x => x.CardID == cardId))
                                                {
                                                    race.Remove(tp);
                                                }
                                                continue;
                                            }
                                        }
                                    }

                                    using (MySqlCommand cmd2 = new MySqlCommand("select * from ct_raw_qn_discount where cd_id = ?cd_id and direction = ?d and `type` = ?type and record_time between ?tb and ?te order by abs(record_time-?tp) limit 1", conn))
                                    {
                                        cmd2.CommandTimeout = 120000;
                                        cmd2.Parameters.AddWithValue("?cd_id", cardId);
                                        cmd2.Parameters.AddWithValue("?tb", tp - 150 * 1000);
                                        cmd2.Parameters.AddWithValue("?te", tp + 150 * 1000);
                                        cmd2.Parameters.AddWithValue("?tp", tp);
                                        MySqlParameter pD = cmd2.Parameters.Add("?d", MySqlDbType.Byte);
                                        MySqlParameter pT = cmd2.Parameters.Add("?type", MySqlDbType.VarChar, 20);

                                        pD.Value = 0;
                                        pT.Value = "Q";
                                        using (MySqlDataReader dr = this.TryGetReader(cmd2))
                                        {
                                            if (this.TryRead(dr))
                                            {
                                                string rawInfo;
                                                if (!object.Equals(dr["raw_info"], DBNull.Value))
                                                {
                                                    rawInfo = (string)dr["raw_info"];
                                                }
                                                else
                                                {
                                                    rawInfo = this.getRawInfoByAPI("get_discount_qn_raw_info", dr["id"].ToString());
                                                }

                                                if (rawInfo != null)
                                                {
                                                    this.parseQnDiscount(rawInfo, items, dr["direction"].ToString(), dr["type"].ToString());
                                                }
                                                else
                                                {
                                                    foreach (RaceData race in data.Where(x => x.CardID == cardId))
                                                    {
                                                        race.Remove(tp);
                                                    }
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                foreach (RaceData race in data.Where(x => x.CardID == cardId))
                                                {
                                                    race.Remove(tp);
                                                }
                                                continue;
                                            }
                                        }

                                        pD.Value = 1;
                                        pT.Value = "Q";
                                        using (MySqlDataReader dr = this.TryGetReader(cmd2))
                                        {
                                            if (this.TryRead(dr))
                                            {
                                                string rawInfo;
                                                if (!object.Equals(dr["raw_info"], DBNull.Value))
                                                {
                                                    rawInfo = (string)dr["raw_info"];
                                                }
                                                else
                                                {
                                                    rawInfo = this.getRawInfoByAPI("get_discount_qn_raw_info", dr["id"].ToString());
                                                }

                                                if (rawInfo != null)
                                                {
                                                    this.parseQnDiscount(rawInfo, items, dr["direction"].ToString(), dr["type"].ToString());
                                                }
                                                else
                                                {
                                                    foreach (RaceData race in data.Where(x => x.CardID == cardId))
                                                    {
                                                        race.Remove(tp);
                                                    }
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                foreach (RaceData race in data.Where(x => x.CardID == cardId))
                                                {
                                                    race.Remove(tp);
                                                }
                                                continue;
                                            }
                                        }

                                        pD.Value = 0;
                                        pT.Value = "QP";
                                        using (MySqlDataReader dr = this.TryGetReader(cmd2))
                                        {
                                            if (this.TryRead(dr))
                                            {
                                                string rawInfo;
                                                if (!object.Equals(dr["raw_info"], DBNull.Value))
                                                {
                                                    rawInfo = (string)dr["raw_info"];
                                                }
                                                else
                                                {
                                                    rawInfo = this.getRawInfoByAPI("get_discount_qn_raw_info", dr["id"].ToString());
                                                }

                                                if (rawInfo != null)
                                                {
                                                    this.parseQnDiscount((string)rawInfo, items, dr["direction"].ToString(), dr["type"].ToString());
                                                }
                                                else
                                                {
                                                    foreach (RaceData race in data.Where(x => x.CardID == cardId))
                                                    {
                                                        race.Remove(tp);
                                                    }
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                foreach (RaceData race in data.Where(x => x.CardID == cardId))
                                                {
                                                    race.Remove(tp);
                                                }
                                                continue;
                                            }
                                        }

                                        pD.Value = 1;
                                        pT.Value = "QP";
                                        using (MySqlDataReader dr = this.TryGetReader(cmd2))
                                        {
                                            if (this.TryRead(dr))
                                            {
                                                string rawInfo;
                                                if (!object.Equals(dr["raw_info"], DBNull.Value))
                                                {
                                                    rawInfo = (string)dr["raw_info"];
                                                }
                                                else
                                                {
                                                    rawInfo = this.getRawInfoByAPI("get_discount_qn_raw_info", dr["id"].ToString());
                                                }

                                                if (rawInfo != null)
                                                {
                                                    this.parseQnDiscount(rawInfo, items, dr["direction"].ToString(), dr["type"].ToString());
                                                }
                                                else
                                                {
                                                    foreach (RaceData race in data.Where(x => x.CardID == cardId))
                                                    {
                                                        race.Remove(tp);
                                                    }
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                foreach (RaceData race in data.Where(x => x.CardID == cardId))
                                                {
                                                    race.Remove(tp);
                                                }
                                                continue;
                                            }
                                        }
                                    }
                                }

                                foreach (RaceData race in data.Where(x => x.CardID == cardId && x.Count > 0))
                                {
                                    race.Save(string.Format("sp-{0:yyyy-MM-dd}-{1}-{2}.dat", race.StartTime, race.CardID, race.RaceNo));
                                }
                            }
                            #endregion

                            
                        }
                    }
                }
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

        private void parseWpDiscount(string text, Dictionary<int, RaceDataItem> items, string direction)
        {
            foreach (string line in text.Split('\n'))
            {
                if (line.Length > 0)
                {
                    string[] elements = line.Split('\t');
                    int raceNo = int.Parse(elements[0]);
                    if (!items.ContainsKey(raceNo)) continue;

                    Match mLimit = Regex.Match(elements[5], @"^(!)?(\d+(?:\.\d+)?)\/(\d+(?:\.\d+)?)$");

                    string horseNo = elements[1];
                    WaterWP water;
                    if (direction == "0")
                    {
                        water = items[raceNo].Waters.GetWpBetWater(horseNo);
                    }
                    else
                    {
                        water = items[raceNo].Waters.GetWpEatWater(horseNo);
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

        private void parseQnDiscount(string text, Dictionary<int, RaceDataItem> items, string direction, string type)
        {
            foreach (string line in text.Split('\n'))
            {
                if (line.Length > 0)
                {
                    string[] elements = line.Split('\t');
                    int raceNo = int.Parse(elements[0]);
                    if (!items.ContainsKey(raceNo)) continue;

                    string horseNo = Regex.Replace(elements[1], @"^\((\d+\-\d+)\)$", "$1");
                    WaterQn water;
                    if (type == "Q")
                    {
                        if (direction == "0")
                        {
                            water = items[raceNo].Waters.GetQnBetWater(horseNo);
                        }
                        else
                        {
                            water = items[raceNo].Waters.GetQnEatWater(horseNo);
                        }
                    }
                    else
                    {
                        if (direction == "0")
                        {
                            water = items[raceNo].Waters.GetQpBetWater(horseNo);
                        }
                        else
                        {
                            water = items[raceNo].Waters.GetQpEatWater(horseNo);
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
    }
}

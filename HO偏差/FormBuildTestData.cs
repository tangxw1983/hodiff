/**
 * WP_TOTE为基础，时间在10秒以内其他资料，合并为一组数据
 * 数据量大，没有测试成功过 
 * 
 * */

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
    public partial class FormBuildTestData : Form
    {
        public FormBuildTestData()
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
            t.Start();
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
LIMIT 10;
", conn))
                {
                    using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                    {
                        using (DataTable table = new DataTable())
                        {
                            da.Fill(table);

                            List<RaceData> data = new List<RaceData>();

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

                                    using (MySqlCommand cmd2 = new MySqlCommand("select * from ct_raw_wp_tote where rc_id = ?rc_id and cd_id = ?cd_id", conn))
                                    {
                                        cmd2.Parameters.AddWithValue("?rc_id", row["id"]);
                                        cmd2.Parameters.AddWithValue("?cd_id", row["card_id"]);

                                        using (MySqlDataReader dr = cmd2.ExecuteReader())
                                        {
                                            while (dr.Read())
                                            {
                                                if (!object.Equals(dr["raw_info"], DBNull.Value))
                                                {
                                                    long time = (long)dr["record_time"];
                                                    RaceDataItem item = new RaceDataItem();
                                                    race.Add(time, item);

                                                    JArray ja = (JArray)JsonConvert.DeserializeObject((string)dr["raw_info"]);
                                                    foreach (JObject jo in ja)
                                                    {
                                                        item.Odds.Add(new Hrs(jo["horseNo"].ToString(), (double)jo["win"], (double)jo["plc"]));
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    if (race.Count == 0) continue;
                                    using (MySqlCommand cmd2 = new MySqlCommand("select * from ct_raw_qn_tote where rc_id = ?rc_id and cd_id = ?cd_id and record_time between ?min_time and ?max_time", conn))
                                    {
                                        cmd2.Parameters.AddWithValue("?rc_id", row["id"]);
                                        cmd2.Parameters.AddWithValue("?cd_id", row["card_id"]);
                                        cmd2.Parameters.AddWithValue("?min_time", race.MinTime - 10000);
                                        cmd2.Parameters.AddWithValue("?max_time", race.MaxTime + 10000);

                                        using (MySqlDataReader dr = cmd2.ExecuteReader())
                                        {
                                            while (dr.Read())
                                            {
                                                if (!object.Equals(dr["raw_info"], DBNull.Value))
                                                {
                                                    long time = (long)dr["record_time"];
                                                    RaceDataItem item = race.GetNearestItem(time, 10000);
                                                    if (item != null && !item.Odds.HasSpQ && !item.Odds.HasSpQp)
                                                    {
                                                        JObject jo = (JObject)JsonConvert.DeserializeObject((string)dr["raw_info"]);
                                                        this.parseQnTote((string)jo["text_q"], item.Odds.SpQ);
                                                        this.parseQnTote((string)jo["text_qp"], item.Odds.SpQp);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                
                            }

                            foreach (ulong cardId in data.Select(x => x.CardID).Distinct().ToArray())
                            {
                                using (MySqlCommand cmd2 = new MySqlCommand("select id, record_time from ct_raw_wp_discount where raw_info is not null and cd_id = ?cd_id and record_time between ?min_time and ?max_time", conn))
                                {
                                    cmd2.Parameters.AddWithValue("?cd_id", cardId);
                                    cmd2.Parameters.AddWithValue("?min_time", data.Where(x => x.Count > 0).Select(x => x.MinTime).Min() - 10000);
                                    cmd2.Parameters.AddWithValue("?max_time", data.Where(x => x.Count > 0).Select(x => x.MaxTime).Max() + 10000);

                                    using (DataTable table_tmp = new DataTable())
                                    {
                                        using (MySqlDataAdapter da2 = new MySqlDataAdapter(cmd2))
                                        {
                                            da2.Fill(table_tmp);

                                            foreach (DataRow row_tmp in table_tmp.Rows)
                                            {
                                                long time = (long)row_tmp["record_time"];
                                                IEnumerable<RaceData> list = data.Where(x => x.CardID == cardId);
                                                Dictionary<int, RaceDataItem> items = new Dictionary<int, RaceDataItem>();
                                                foreach (RaceData race in list)
                                                {
                                                    RaceDataItem item = race.GetNearestItem(time, 10000);
                                                    if (item != null)
                                                    {
                                                        items.Add(race.RaceNo, item);
                                                    }
                                                }
                                                if (items.Count == 0) continue;

                                                using (MySqlCommand cmd3 = new MySqlCommand("select * from ct_raw_wp_discount where id = ?id", conn))
                                                {
                                                    cmd3.Parameters.AddWithValue("?id", row_tmp["id"]);

                                                    using (MySqlDataReader dr = cmd3.ExecuteReader())
                                                    {
                                                        while (dr.Read())
                                                        {
                                                            string text = (string)dr["raw_info"];
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
                                                                    if (dr["direction"].ToString() == "0")
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
                                                    }
                                                }
                                            }
                                        }
                                    }


                                }

                                using (MySqlCommand cmd2 = new MySqlCommand("select id, record_time from ct_raw_qn_discount where raw_info is not null and cd_id = ?cd_id and record_time between ?min_time and ?max_time", conn))
                                {
                                    cmd2.Parameters.AddWithValue("?cd_id", cardId);
                                    cmd2.Parameters.AddWithValue("?min_time", data.Where(x => x.Count > 0).Select(x => x.MinTime).Min() - 10000);
                                    cmd2.Parameters.AddWithValue("?max_time", data.Where(x => x.Count > 0).Select(x => x.MaxTime).Max() + 10000);

                                    using (DataTable table_tmp = new DataTable())
                                    {
                                        using (MySqlDataAdapter da2 = new MySqlDataAdapter(cmd2))
                                        {
                                            da2.Fill(table_tmp);

                                            foreach (DataRow row_tmp in table_tmp.Rows)
                                            {
                                                long time = (long)row_tmp["record_time"];
                                                IEnumerable<RaceData> list = data.Where(x => x.CardID == cardId);
                                                Dictionary<int, RaceDataItem> items = new Dictionary<int, RaceDataItem>();
                                                foreach (RaceData race in list)
                                                {
                                                    RaceDataItem item = race.GetNearestItem(time, 10000);
                                                    if (item != null)
                                                    {
                                                        items.Add(race.RaceNo, item);
                                                    }
                                                }
                                                if (items.Count == 0) continue;

                                                using (MySqlCommand cmd3 = new MySqlCommand("select * from ct_raw_qn_discount where id = ?id", conn))
                                                {
                                                    cmd3.Parameters.AddWithValue("?id", row_tmp["id"]);

                                                    using (MySqlDataReader dr = cmd3.ExecuteReader())
                                                    {
                                                        while (dr.Read())
                                                        {
                                                            string text = (string)dr["raw_info"];
                                                            foreach (string line in text.Split('\n'))
                                                            {
                                                                if (line.Length > 0)
                                                                {
                                                                    string[] elements = line.Split('\t');
                                                                    int raceNo = int.Parse(elements[0]);
                                                                    if (!items.ContainsKey(raceNo)) continue;

                                                                    string horseNo = Regex.Replace(elements[1], @"^\((\d+\-\d+)\)$", "$1");
                                                                    WaterQn water;
                                                                    if (dr["type"].ToString() == "Q")
                                                                    {
                                                                        if (dr["direction"].ToString() == "0")
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
                                                                        if (dr["direction"].ToString() == "0")
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
                                            }
                                        }
                                    }
                                }
                            }


                            foreach (RaceData race in data.Where(x => x.Count > 0))
                            {
                                race.Save(string.Format("{0:yyyy-MM-dd}-{1}-{2}.dat", race.StartTime, race.CardID, race.RaceNo));
                            }
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
                                Match m = Regex.Match(segments[i], @"^(?:\[[123]\])?(\d+(?:\.\d+)?)$");
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
                        Match m = Regex.Match(segments[i], @"^(?:\[[123]\])?(\d+(?:\.\d+)?)$");
                        if (m.Success)
                        {
                            dict.Add(hrs_key, double.Parse(m.Groups[1].Value));
                        }
                    }
                }
            }
        }
    }
}

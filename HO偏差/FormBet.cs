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

        class RaceHandler
        {
            public RaceHandler()
            {

            }

            private long _race_id;
            private long _card_id;
            private int _race_no;
            private DateTime _start_time;
            
            private HrsTable _latest_odds;
            private HrsTable _fitted_odds;
            private WaterTable _latest_waters;

            private int _stage;

            private Thread _tDaemon;
            private Thread _tFix;
            private Thread _tBet;

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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;

namespace HO偏差
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            rows = new Dictionary<string, double[]>();
            rand = new Random();

            lves = new Label[] { lve1, lve2, lve3, lve4, lve5, lve6, lve7, lve8, lve9, lve10, lve11, lve12, lve13, lve14 };
            lvds = new Label[] { lvd1, lvd2, lvd3, lvd4, lvd5, lvd6, lvd7, lvd8, lvd9, lvd10, lvd11, lvd12, lvd13, lvd14 };
            lres = new Label[] { lre1, lre2, lre3, lre4, lre5, lre6, lre7, lre8, lre9, lre10, lre11, lre12, lre13, lre14 };
            lrds = new Label[] { lrd1, lrd2, lrd3, lrd4, lrd5, lrd6, lrd7, lrd8, lrd9, lrd10, lrd11, lrd12, lrd13, lrd14 };
            lpws = new Label[] { lpw1, lpw2, lpw3, lpw4, lpw5, lpw6, lpw7, lpw8, lpw9, lpw10, lpw11, lpw12, lpw13, lpw14 };
            lpps = new Label[] { lpp1, lpp2, lpp3, lpp4, lpp5, lpp6, lpp7, lpp8, lpp9, lpp10, lpp11, lpp12, lpp13, lpp14 };
            lEs = new Label[] { lE1, lE2, lE3, lE4 };
            lrcws = new Label[] { lrcw1, lrcw2, lrcw3, lrcw4, lrcw5, lrcw6, lrcw7, lrcw8, lrcw9, lrcw10, lrcw11, lrcw12, lrcw13, lrcw14 };
            lrcps = new Label[] { lrcp1, lrcp2, lrcp3, lrcp4, lrcp5, lrcp6, lrcp7, lrcp8, lrcp9, lrcp10, lrcp11, lrcp12, lrcp13, lrcp14 };
            lrfws = new Label[] { label46, label48, label50, label52, label54, label56, label58, label76, label78, label80, label82, label84, label86, label88 };
            lrfps = new Label[] { label47, label49, label51, label53, label55, label57, label75, label77, label79, label81, label83, label85, label87, label89 };
        }

        private Dictionary<string, double[]> rows;
        private Random rand;
        private const int CNT = 14;
        private const double EE_D_INC = 0.001;     // 期望求导增量
        private const double DD_D_INC = 0.001;      // 方差求导增量
        private const double EE_STEP = 0.01;
        private const double DD_STEP = 0.01;
        private const int PRECISION = 7;

        private Thread _t_d, _t_o;

        private Label[] lves, lvds, lres, lrds, lpws, lpps, lEs, lrcws, lrcps, lrfws, lrfps;

        private Dictionary<double, double> _cached_gauss_result = new Dictionary<double, double>();
        private double gauss(double e, double d, double x)   // 计算P(v>x)
        {
            if (e != 0 || d != 1)
            {
                return this.gauss(0, 1, (x - e) / d);
            }
            else if (x == 0)
            {
                return 0.5;
            }
            else if (x < 0)
            {
                return 1 - gauss(e, d, -x);
            }
            else
            {
                double r = Math.Round(x, PRECISION);
                if (!_cached_gauss_result.ContainsKey(r))
                {
                    _cached_gauss_result[r] = Math.Round(0.5 - common.Math.Calculus.integrate(new common.Math.Calculus.Func(delegate(double y)
                    {
                        return Math.Exp(-y * y / 2) / Math.Sqrt(2 * Math.PI);
                    }), 0, r, Math.Pow(10, -PRECISION)), PRECISION);
                }
                return _cached_gauss_result[r];
            }
        }

        private void calc(double[] ee, double[] dd, out double[] p1, out double[] p3)
        {
            p1 = new double[CNT];
            p3 = new double[CNT];
            for (int i = 0; i < CNT; i++)
            {
                common.Math.Calculus.Func r = new common.Math.Calculus.Func(delegate(double x)
                {
                    double ret = 1;
                    for (int j = 0; j < CNT; j++)
                    {
                        if (j == i) continue;
                        ret += this.gauss(ee[j], dd[j], x);
                    }
                    return ret;
                });

                common.Math.Calculus.Func f_top1 = new common.Math.Calculus.Func(delegate(double x)
                {
                    double gx = Math.Exp(-(x - ee[i]) * (x - ee[i]) / (2 * dd[i] * dd[i])) / (Math.Sqrt(2 * Math.PI) * dd[i]);
                    double rx = 1;
                    for (int j = 0; j < CNT; j++)
                    {
                        if (j == i) continue;
                        rx *= 1 - this.gauss(ee[j], dd[j], x);
                    }
                    return gx * rx;

                    // 这里应该是将我跑x的成绩时，赢每个人的概率连乘，转换为其中0个人赢我的泊松分布概率  2017-3-7
                    // 这样子不行的，比如假设A赢我概率0.5，B赢我概率0.5，没有人能赢我的概率是0.5*0.5 = 0.25
                    // 转换后，A赢我的期望0.5+B赢我的期望0.5=1，泊松分布概率P(0)=0.368，差蛮远
                    //double rv = Math.Exp(-r(x));

                    //return gx * rv;
                });
                common.Math.Calculus.Func f_top3 = new common.Math.Calculus.Func(delegate(double x)
                {
                    double rv = r(x);
                    return Math.Exp(-(x - ee[i]) * (x - ee[i]) / (2 * dd[i] * dd[i])) / (Math.Sqrt(2 * Math.PI) * dd[i]) * (Math.Exp(-rv) + Math.Exp(-rv) * rv + Math.Exp(-rv) * rv * rv / 2);
                });

                p1[i] = common.Math.Calculus.integrate(f_top1, ee[i] - dd[i] * 8, ee[i] + dd[i] * 8, Math.Pow(10, -PRECISION));
                p3[i] = common.Math.Calculus.integrate(f_top3, ee[i] - dd[i] * 8, ee[i] + dd[i] * 8, Math.Pow(10, -PRECISION));
            }
        }

        private void btn正向过程_Click(object sender, EventArgs e)
        {
            this.btn正向过程.Enabled = false;

            if (_t_d == null)
            {
                _t_d = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        this.Invoke(new MethodInvoker(delegate
                        {
                            this.btn正向过程.Enabled = true;
                            this.btn正向过程.Text = "停止";
                        }));

                        // 生成期望
                        double[] ee = new double[CNT];
                        for (int i = 0; i < CNT; i++) ee[i] = rand.NextDouble();
                        double thd_e = ee.OrderByDescending(x => x).Skip(2).First();
                        for (int i = 0; i < CNT; i++) ee[i] -= thd_e;

                        // 生成方差
                        double[] dd = new double[CNT];
                        for (int i = 0; i < CNT; i++) dd[i] = rand.NextDouble() + 0.5;
                        double thd_d = dd[0];
                        for (int i = 0; i < CNT; i++)
                        {
                            if (ee[i] == 0)
                            {
                                thd_d = dd[i];
                                break;
                            }
                        }
                        for (int i = 0; i < CNT; i++) dd[i] /= thd_d;

                        //double[] ee = new double[] { 0.5290, 0.3828, -0.3311, 0.4858, 0.5788, 0.0668, 0.4494, 0.4878, 0.0000, 0.5260, 0.5960, 0.4944, 0.4302, 0.6743 };
                        //double[] dd = new double[] { 0.1984, 0.4951, 0.5684, 0.1490, 0.1336, 0.6521, 0.1595, 0.2534, 1.0000, 0.1970, 0.1868, 0.2022, 0.4422, 0.0973 };
                        rows["事实期望"] = ee;
                        rows["事实方差"] = dd;

                        // 计算事实概率
                        double[] p1, p3;
                        this.calc(ee, dd, out p1, out p3);
                        rows["TOP1概率"] = this.normalize(p1, 1);
                        rows["TOP3概率"] = this.normalize(p3, 3);

                        // 生成随机SP
                        double[] b1 = new double[CNT];
                        double[] b3 = new double[CNT];
                        for (int i = 0; i < CNT; i++)
                        {
                            b1[i] = p1[i] *(rand.NextDouble() * 0.5 + 0.75);
                            b3[i] = p3[i] *(rand.NextDouble() * 0.5 + 0.75);
                        }
                        double t1 = b1.Sum(x => x) * 0.83;
                        double t3 = b3.Sum(x => x) * 0.83;
                        double[] s1 = new double[CNT];
                        double[] s3 = new double[CNT];
                        // 假设TOP3 SP计算方式为 1 + (S - Si - (其余最大两项)) / 3 / Si
                        for (int i = 0; i < CNT; i++)
                        {
                            s1[i] = t1 / b1[i];
                            if (s1[i] < 1.01) s1[i] = 1.01;
                            s3[i] = 1 + (t3 - b3[i] - b3.Take(i).Union(b3.Skip(i + 1)).OrderByDescending(x => x).Take(2).Sum(x => x)) / 3 / b3[i];
                            if (s3[i] < 1.01) s3[i] = 1.01;
                        }
                        rows["TOP1 SP"] = s1;
                        rows["TOP3 SP"] = s3;

                        this.Invoke(new MethodInvoker(delegate
                        {
                            this.listView1.Items.Clear();
                            {
                                for (int i = 0; i < CNT; i++)
                                {
                                    ListViewItem lvi = this.listView1.Items.Add(string.Format("{0:00}", i + 1));
                                    lvi.SubItems.Add(string.Format("{0:0.0000}", rows["事实期望"][i]));
                                    lvi.SubItems.Add(string.Format("{0:0.0000}", rows["事实方差"][i]));
                                    lvi.SubItems.Add(string.Format("{0:0.0000}", rows["TOP1概率"][i] * 100));
                                    lvi.SubItems.Add(string.Format("{0:0.0000}", rows["TOP3概率"][i] * 100));
                                    lvi.SubItems.Add(string.Format("{0:0.00}", rows["TOP1 SP"][i]));
                                    lvi.SubItems.Add(string.Format("{0:0.00}", rows["TOP3 SP"][i]));
                                }
                            }
                            {
                                ListViewItem lvi = this.listView1.Items.Add("Total");
                                lvi.SubItems.Add("");
                                lvi.SubItems.Add("");
                                lvi.SubItems.Add(string.Format("{0:0.0000}", rows["TOP1概率"].Sum(x => x) * 100));
                                lvi.SubItems.Add(string.Format("{0:0.0000}", rows["TOP3概率"].Sum(x => x) * 100));
                            }
                        }));
                    }
                    finally
                    {
                        _t_d = null;

                        try
                        {
                            this.Invoke(new MethodInvoker(delegate
                            {
                                this.btn正向过程.Enabled = true;
                                this.btn正向过程.Text = "正向过程";
                            }));
                        }
                        catch
                        {
                        }
                    }
                }));
                _t_d.IsBackground = true;
                _t_d.Start();
            }
            else
            {
                _t_d.Abort();
            }
        }

        private delegate double rr(double[] ee, double[] dd);

        private double[] normalize(double[] p, double rate)
        {
            double sum = p.Sum() / rate;
            for (int i = 0; i < CNT; i++)
            {
                p[i] /= sum;
            }
            return p;
        }

        private double calc_rr(double[] s1, double[] p1, double[] s3, double[] p3)
        {
            // 归一化
            this.normalize(p1, 1);
            this.normalize(p3, 3);

            double[] rr = new double[CNT * 2];
            for (int i = 0; i < CNT; i++)
            {
                rr[i * 2] = s1[i] * p1[i];
                rr[i * 2 + 1] = s3[i] * p3[i];
            }

            return rr.Max();

            double e = rr.Average();
            double d = 0;
            for (int i = 0; i < CNT; i++)
            {
                d += (rr[i * 2] - e) * (rr[i * 2] - e);
                d += (rr[i * 2 + 1] - e) * (rr[i * 2 + 1] - e);
            }
            return d;
        }

        private void btn逆向过程_Click(object sender, EventArgs e)
        {
            this.btn逆向过程.Enabled = false;

            if (_t_o == null)
            {
                _t_o = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        this.Invoke(new MethodInvoker(delegate
                        {
                            this.btn逆向过程.Enabled = true;
                            this.btn逆向过程.Text = "停止";
                        }));

                        double[] fp1 = rows["TOP1概率"];
                        double[] fp3 = rows["TOP3概率"];
                        double[] s1 = rows["TOP1 SP"];
                        double[] s3 = rows["TOP3 SP"];

                        double[] ee = new double[CNT];
                        double[] dd = new double[CNT];

                        // 设定top1第3的项期望=0，方差=1
                        double trd_s1 = s1.OrderBy(x => x).Skip(2).First();
                        int trd_inx = -1;
                        for (int i = 0; i < CNT; i++)
                        {
                            if (s1[i] == trd_s1)
                            {
                                trd_inx = i;
                                break;
                            }
                        }

                        if (trd_inx == -1)
                        {
                            MessageBox.Show("没想到找不到排行第3的项");
                            return;
                        }

                        ee[trd_inx] = 0;
                        dd[trd_inx] = 1;

                        // 初始化其他项
                        // 目前全部设为期望=0，方差=1
                        // 后面考虑根据s1设置
                        for (int i = 0; i < CNT; i++)
                        {
                            if (i == trd_inx) continue;
                            ee[i] = 0;
                            dd[i] = 1;
                        }

                        double lastE = double.MaxValue;

                        while (true)
                        {
                            double[] p1, p3;
                            this.calc(ee, dd, out p1, out p3);

                            double rr = this.calc_rr(s1, p1, s3, p3);
                            double maxr1 = double.MinValue, minr1 = double.MaxValue, maxr3 = double.MinValue, minr3 = double.MaxValue;
                            int maxr1_i = -1, minr1_i = -1, maxr3_i = -1, minr3_i = -1;
                            for (int i = 0; i < CNT; i++)
                            {
                                if (s1[i] * p1[i] > maxr1)
                                {
                                    maxr1 = s1[i] * p1[i];
                                    maxr1_i = i;
                                }
                                if (s1[i] * p1[i] < minr1)
                                {
                                    minr1 = s1[i] * p1[i];
                                    minr1_i = i;
                                }
                                if (s3[i] * p3[i] > maxr3)
                                {
                                    maxr3 = s3[i] * p3[i];
                                    maxr3_i = i;
                                }
                                if (s3[i] * p3[i] < minr3)
                                {
                                    minr3 = s3[i] * p3[i];
                                    minr3_i = i;
                                }
                            }

                            // 求对各个参数的梯度
                            double E = 0;
                            double[] d_rr_ee = new double[CNT];
                            double[] d_rr_dd = new double[CNT];
                            for (int i = 0; i < CNT; i++)
                            {
                                if (i == trd_inx) continue;
                                double[] tp1, tp3;
                                
                                ee[i] += EE_D_INC;
                                this.calc(ee, dd, out tp1, out tp3);
                                d_rr_ee[i] = (this.calc_rr(s1, tp1, s3, tp3) - rr) / EE_D_INC;
                                E += d_rr_ee[i] * d_rr_ee[i];
                                ee[i] -= EE_D_INC;

                                dd[i] += DD_D_INC;
                                this.calc(ee, dd, out tp1, out tp3);
                                d_rr_dd[i] = (this.calc_rr(s1, tp1, s3, tp3) - rr) / DD_D_INC;
                                E += d_rr_dd[i] * d_rr_dd[i];
                                dd[i] -= DD_D_INC;
                            }

                            // 调整各参数
                            for (int i = 0; i < CNT; i++)
                            {
                                if (i == trd_inx) continue;
                                ee[i] -= d_rr_ee[i] * EE_STEP;
                                dd[i] -= d_rr_dd[i] * DD_STEP;
                                if (dd[i] < 0.1) dd[i] = 0.1;
                            }

                            this.Invoke(new MethodInvoker(delegate
                            {
                                for (int i = 0; i < CNT; i++)
                                {
                                    lves[i].Text = string.Format("{0:0.0000}", ee[i]);
                                    lvds[i].Text = string.Format("{0:0.0000}", dd[i]);
                                    lres[i].Text = string.Format("{0:0.0000}", d_rr_ee[i]);
                                    lrds[i].Text = string.Format("{0:0.0000}", d_rr_dd[i]);
                                    lpws[i].Text = string.Format("{0:0.0000}", p1[i] * 100);
                                    lpps[i].Text = string.Format("{0:0.0000}", p3[i] * 100);
                                    lrcws[i].Text = string.Format("{0:0.0000}", p1[i] * s1[i]);
                                    lrcps[i].Text = string.Format("{0:0.0000}", p3[i] * s3[i]);
                                    lrfws[i].Text = string.Format("{0:0.0000}", fp1[i] * s1[i]);
                                    lrfps[i].Text = string.Format("{0:0.0000}", fp3[i] * s3[i]);
                                }

                                for (int i = 0; i < CNT; i++)
                                {
                                    if (i != maxr1_i && i != minr1_i) lpws[i].BackColor = Color.White;
                                    if (i != maxr3_i && i != minr3_i) lpps[i].BackColor = Color.White;
                                }
                                lpws[maxr1_i].BackColor = Color.Yellow;
                                lpws[minr1_i].BackColor = Color.Red;
                                lpps[maxr3_i].BackColor = Color.Yellow;
                                lpps[minr3_i].BackColor = Color.Red;

                                for (int i = lEs.Length - 1; i > 0; i--)
                                {
                                    lEs[i].Text = lEs[i - 1].Text;
                                }
                                lEs[0].Text = string.Format("{0:0.0000} | {1,10:0.0000}", E, rr);
                                lpwSum.Text = string.Format("{0:0.0000}", p1.Sum() * 100);
                                lppSum.Text = string.Format("{0:0.0000}", p3.Sum() * 100);
                            }));

                            if (E > lastE) break;
                        }
                    }
                    finally
                    {
                        _t_o = null;

                        try
                        {
                            this.Invoke(new MethodInvoker(delegate
                            {
                                this.btn逆向过程.Enabled = true;
                                this.btn逆向过程.Text = "逆向过程";
                            }));
                        }
                        catch
                        {
                        }
                    }
                }));
                _t_o.IsBackground = true;
                _t_o.Start();
            }
            else
            {
                _t_o.Abort();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Clipboard.SetData(DataFormats.Text, string.Format("double[] ee = new double[] {{ {0} }};\r\ndouble[] dd = new double[] {{ {1} }}",
                string.Join(",", lves.Select(x => x.Text).ToArray()),
                string.Join(",", lvds.Select(x => x.Text).ToArray())));
        }
    }
}

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
        private const int PLC_CNT = 3;
        private const double EE_D_INC = 0.001;     // 期望求导增量
        private const double DD_D_INC = 0.001;      // 方差求导增量
        private const double EE_STEP = 0.1;
        private const double DD_STEP = 0.1;
        private const double STEP_DECAY = 0.01;     // 步长衰减
        private const int PRECISION = 5;

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

                //return Math.Round(0.5 - common.Math.Calculus.integrate(new common.Math.Calculus.Func(delegate(double y)
                //{
                //    return Math.Exp(-y * y / 2) / Math.Sqrt(2 * Math.PI);
                //}), 0, x, Math.Pow(10, -PRECISION)), PRECISION);
            }
        }

        private void calc(double[] ee, double[] dd, out double[] p1, out double[] p3, out double[] p3detail)
        {
            p1 = new double[CNT];
            p3 = new double[CNT];
            common.Math.Combination comb3 = new common.Math.Combination(CNT, PLC_CNT);
            p3detail = new double[comb3.Length];
            
            common.Math.Combination comb2 = new common.Math.Combination(CNT - 1, PLC_CNT - 1);
                int[][] _2_combinations = comb2.GetCombinations();

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

                // @deprecated f_top1没用了，在f_top3中计算了
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

                common.Math.Calculus.MultiFunc f_top_3 = new common.Math.Calculus.MultiFunc(delegate(double x)
                {
                    double gx = Math.Exp(-(x - ee[i]) * (x - ee[i]) / (2 * dd[i] * dd[i])) / (Math.Sqrt(2 * Math.PI) * dd[i]);

                    double[] ret = new double[comb2.Length + 2];     // 前两个为WIN和PLC的概率

                    // 选2 Tree最后的结果就是有两个人赢我的概率
                    // 选1 Tree最后的结果是有一个人赢我的概率
                    // LS最后结果是没人赢我的概率
                    //              
                    //          不选A  - “选2 Tree”   V(A)*2T
                    //       /
                    //  root                              +                  不选B - “选1 Tree”
                    //       \                                            / 
                    //          选A  -  “选1 Tree”  (1-V(A))*1T    --- 
                    //                                                    \ 
                    //                                                       选B - 连乘
                    //2T(n-2) = (1-V(n-1)) * (1-V(n-2))
                    //2T(d) = V(d)*2T(d+1)+(1-V(d))*1T(d+1)
                    //1T(n-1) = (1-V(n-1))
                    //1T(d) = V(d)*1T(d+1)+(1-V(d))*LS(d+1)
                    //LS(n-1) = V(n-1)
                    //LS(d) = V(d)*LS(d+1)
                    // 目标计算2T(0)
                    // 
                    // 扩展3T、4T、jT
                    // jT(n-j) = (1-V(n-1))*...*(1-V(n-j))
                    // jT(d) = V(d)*jT(d+1)+(1-V(d))*(j-1)T(d+1)

                    double[] V = new double[CNT - 1];   // 我赢i的概率
                    for (int j = 0; j < CNT; j++)
                    {
                        if (j < i)
                            V[j] = 1 - this.gauss(ee[j], dd[j], x);
                        else if (j > i)
                            V[j - 1] = 1 - this.gauss(ee[j], dd[j], x);
                        else // if (j == i)
                            continue;
                    }

                    int n = CNT - 1;
                    double[] T = new double[PLC_CNT];
                    for (int j = 0; j < PLC_CNT; j++)
                    {
                        T[j] = 1;
                        for (int k = 1; k <= j; k++)
                        {
                            T[j] *= 1 - V[n - k];
                        }
                    }
                    for (int j = n - 1; j >= 0; j--)
                    {
                        for (int k = 0; k < PLC_CNT; k++)
                        {
                            if (k == 0)
                                T[k] *= V[j];
                            else if (j - k >= 0)
                                T[k] = V[j - k] * T[k] + (1 - V[j - k]) * T[k - 1];
                        }
                    }

                    ret[0] = T[0];
                    ret[1] = T.Sum();

                    // 计算我跑第三(PLC_CNT)时，前两(PLC_CNT-1)名各种组合的概率
                    double tmp = 1;
                    int[] v0indices = new int[CNT - 1];
                    int v0count = 0,  // 我赢概率为0的数量，即肯定超过我的数量
                        v1count = 0;  // 不可能超过我的数量
                    for (int j = 0; j < CNT - 1; j++)
                    {
                        if (V[j] == 0)
                        {
                            v0indices[v0count++] = j;
                        }
                        else if (V[j] == 1)
                        {
                            v1count++;
                        }
                        else
                        {
                            tmp *= V[j];
                        }
                    }
                    // 肯定超过我的数量v0count大于PLC_CNT-1，我的名次肯定低于PLC_CNT，我跑第PLC_CNT的概率为0
                    // 不可能超过的数量v1count大于CNT-PLC_CNT，我的名次肯定高于PLC_CNT
                    if (v0count < PLC_CNT && v1count <= CNT - PLC_CNT) 
                    {
                        for (int j = 0; j < comb2.Length; j++)
                        {
                            int[] c = _2_combinations[j];
                            int cv0count = 0;
                            ret[j + 2] = tmp;
                            for (int k = 0; k < PLC_CNT - 1; k++)
                            {
                                if (V[c[k]] == 0)
                                {
                                    cv0count++;
                                }
                                else if (V[c[k]] == 1)
                                {
                                    ret[j + 2] = 0;
                                    break;
                                }
                                else
                                {
                                    ret[j + 2] /= V[c[k]];
                                    ret[j + 2] *= (1 - V[c[k]]);
                                }
                            }

                            if (cv0count < v0count)
                            {
                                ret[j + 2] = 0;
                            }
                        }
                    }

                    return new common.Math.vector(ret) * gx;
                });

                //p1[i] = common.Math.Calculus.integrate(f_top1, ee[i] - dd[i] * 8, ee[i] + dd[i] * 8, Math.Pow(10, -PRECISION), 5);
                //p3[i] = common.Math.Calculus.integrate(f_top3, ee[i] - dd[i] * 8, ee[i] + dd[i] * 8, Math.Pow(10, -PRECISION), 5);

                common.Math.vector pv = common.Math.Calculus.integrate(f_top_3, ee[i] - dd[i] * 7, ee[i] + dd[i] * 7, Math.Pow(10, -PRECISION), 5);
                p1[i] = pv[0];
                p3[i] = pv[1];

                for (int j = 0; j < comb2.Length; j++)
                {
                    int[] c = new int[PLC_CNT];
                    c[0] = i;
                    for (int k = 0; k < PLC_CNT - 1; k++)
                    {
                        if (_2_combinations[j][k] < i)
                            c[k + 1] = _2_combinations[j][k];
                        else
                            c[k + 1] = _2_combinations[j][k] + 1;
                    }

                    p3detail[comb3.Index(c)] += pv[j + 2];
                }
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

                        //double[] ee = new double[] { 0.00801567780227197, 0, -0.621224622997094, 0.0313196089264562, -0.0943667865797723, -0.0915018185467934, -0.334689835242317, -0.426669478149465, -0.493784849761885, -0.840716855060643, -0.577426177718409, -0.258726100557822, -0.783315496883968, -0.315391631012499 };
                        //double[] dd = new double[] { 0.572739966948158, 1, 0.872813330886322, 0.359513914114434, 0.538356241925828, 0.337836391363014, 0.907819085464169, 0.952665051689518, 0.746272143819065, 0.697850654308121, 0.636612974592895, 0.435706391326388, 0.989832968601233, 0.364285053896771 };

                        rows["事实期望"] = ee;
                        rows["事实方差"] = dd;

                        // 计算事实概率
                        double[] p1, p3, p3detail;
                        this.calc(ee, dd, out p1, out p3, out p3detail);
                        rows["TOP1概率"] = p1; // this.normalize(p1, 1);
                        rows["TOP3概率"] = p3; // this.normalize(p3, 3);

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

        private double calc_plc_r(double r, int inx, double[] bp, double[] wp, int[][] combinations)  // 全部投注inx时的赔付率
        {
            double r_tmp = 0;

            for (int i = 0; i < combinations.Length; i++)
            {
                double tmp = 0;
                bool ignore = true;
                for (int j = 0; j < PLC_CNT; j++)
                {
                    tmp += bp[combinations[i][j]];
                    if (inx == combinations[i][j]) ignore = false;
                }

                if (!ignore)
                {
                    r_tmp += (r - tmp) / 3 / bp[inx] * wp[i];
                }
            }

            return r_tmp;
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

                        double rr1 = 1 / s1.Sum(x => 1 / x);

                        // 计算WIN投注比率
                        double[] ph1 = new double[CNT];
                        for (int i = 0; i < CNT; i++)
                        {
                            ph1[i] = rr1 / s1[i];
                        }

                        // 计算PLC赔付率
                        IOrderedEnumerable<double> sorted_s3 = s3.OrderBy(x => x);
                        double o3 = sorted_s3.Skip(2).First();
                        double a = 3 * (o3 - 1) / (3 * (o3 - 1) + 1) * sorted_s3.Take(2).Sum(x => 1 / (3 * (x - 1)));
                        double b = sorted_s3.Skip(2).Sum(x => 1 / (3 * (x - 1) + 1));
                        double rr3 = (a + 1) / (a + b);

                        // 计算投注比例
                        double po3 = (1 - rr3) / ((sorted_s3.Skip(2).Sum(x => 1 / (3 * x - 2)) - 1) * (3 * o3 - 2));
                        double[] ph3 = new double[CNT];
                        for (int i = 0; i < CNT; i++)
                        {
                            if (s3[i] <= o3)
                            {
                                ph3[i] = po3 * (o3 - 1) / (s3[i] - 1); 
                            }
                            else
                            {
                                ph3[i] = po3 * (3 * o3 - 2) / (3 * s3[i] - 2);
                            }
                        }

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
                        int[] dir_d_rr_ee = new int[CNT], dir_d_rr_dd = new int[CNT];
                        for (int t = 0; ;t++ )
                        {
                            double[] p1, p3, p3detail;
                            this.calc(ee, dd, out p1, out p3, out p3detail);
                            double E = 0;

                            // 交叉熵损失以及交叉熵损失对与各个概率的梯度
                            double[] grad_p1 = new double[CNT], grad_p3 = new double[CNT];
                            for (int i = 0; i < CNT; i++)
                            {
                                E += -(ph1[i] * Math.Log(p1[i]) + (1 - ph1[i]) * Math.Log(1 - p1[i]));
                                E += -(ph3[i] * Math.Log(p3[i]) + (1 - ph3[i]) * Math.Log(1 - p3[i]));
                                grad_p1[i] = -(ph1[i] / p1[i] - (1 - ph1[i]) / (1 - p1[i]));
                                grad_p3[i] = -(ph3[i] / p3[i] - (1 - ph3[i]) / (1 - p3[i]));
                            }

                            //double rr = this.calc_rr(s1, p1, s3, p3);
                            double maxr1 = double.MinValue, minr1 = double.MaxValue, maxr3 = double.MinValue, minr3 = double.MaxValue;
                            int maxr1_i = -1, minr1_i = -1, maxr3_i = -1, minr3_i = -1;
                            double[] r1 = new double[CNT], r3 = new double[CNT];  // 这个是每个马的回报率，非赔付率
                            common.Math.Combination comb = new common.Math.Combination(CNT, PLC_CNT);
                            int[][] combs = comb.GetCombinations();
                            for (int i = 0; i < CNT; i++)
                            {
                                r1[i] = s1[i] * p1[i];
                                r3[i] = this.calc_plc_r(rr3, i, ph3, p3detail, combs);
                                if (r1[i] > maxr1)
                                {
                                    maxr1 = r1[i];
                                    maxr1_i = i;
                                }
                                if (r1[i] < minr1)
                                {
                                    minr1 = r1[i];
                                    minr1_i = i;
                                }
                                if (r3[i] > maxr3)
                                {
                                    maxr3 = r3[i];
                                    maxr3_i = i;
                                }
                                if (r3[i] < minr3)
                                {
                                    minr3 = r3[i];
                                    minr3_i = i;
                                }
                            }
                            

                            // 求对各个参数的梯度
                            double[] d_rr_ee = new double[CNT];
                            double[] d_rr_dd = new double[CNT];
                            for (int i = 0; i < CNT; i++)
                            {
                                // if (i == trd_inx) continue; 
                                double[] tp1, tp3, tp3detail;

                                ee[i] += EE_D_INC;
                                this.calc(ee, dd, out tp1, out tp3, out tp3detail);
                                for (int j = 0; j < CNT; j++)
                                {
                                    // 第j匹马的WIN概率对第i匹马的均值的梯度 = (tp1[j] - p1[j]) / EE_D_INC
                                    d_rr_ee[i] += (tp1[j] - p1[j]) / EE_D_INC * grad_p1[j];
                                    // 第j匹马的PLC概率对第i匹马的均值的梯度 = (tp3[j] - p3[j]) / EE_D_INC
                                    d_rr_ee[i] += (tp3[j] - p3[j]) / EE_D_INC * grad_p3[j];
                                }
                                ee[i] -= EE_D_INC;

                                dd[i] += DD_D_INC;
                                this.calc(ee, dd, out tp1, out tp3, out tp3detail);
                                for (int j = 0; j < CNT; j++)
                                {
                                    // 第j匹马的WIN概率对第i匹马的方差的梯度 = (tp1[j] - p1[j]) / DD_D_INC
                                    d_rr_dd[i] += (tp1[j] - p1[j]) / DD_D_INC * grad_p1[j];
                                    // 第j匹马的PLC概率对第i匹马的方差的梯度 = (tp3[j] - p3[j]) / DD_D_INC
                                    d_rr_dd[i] += (tp3[j] - p3[j]) / DD_D_INC * grad_p3[j];
                                }
                                dd[i] -= DD_D_INC;
                            }

                            // 调整各参数
                            for (int i = 0; i < CNT; i++)
                            {
                                // if (i == trd_inx) continue;
                                if (d_rr_ee[i] > 0 && dir_d_rr_ee[i] >= 0)
                                    dir_d_rr_ee[i]++;
                                else if (d_rr_ee[i] < 0 && dir_d_rr_ee[i] <= 0)
                                    dir_d_rr_ee[i]--;
                                else
                                    dir_d_rr_ee[i] = 0;

                                if (d_rr_dd[i] > 0 && dir_d_rr_dd[i] >= 0)
                                    dir_d_rr_dd[i]++;
                                else if (d_rr_dd[i] < 0 && dir_d_rr_dd[i] <= 0)
                                    dir_d_rr_dd[i]--;
                                else
                                    dir_d_rr_dd[i] = 0;

                                // 向负梯度方向调整
                                ee[i] += -d_rr_ee[i] * EE_STEP / (1 + t * STEP_DECAY) * (1 + Math.Abs(dir_d_rr_ee[i]) * STEP_DECAY);
                                dd[i] += -d_rr_dd[i] * DD_STEP / (1 + t * STEP_DECAY) * (1 + Math.Abs(dir_d_rr_dd[i]) * STEP_DECAY);

                                if (dd[i] < 0.0001) dd[i] = 0.0001;
                            }

                            // 调整之后再进行归一化
                            for (int i = 0; i < CNT; i++)
                            {
                                if (i == trd_inx) continue;
                                ee[i] = (ee[i] - ee[trd_inx]) / dd[trd_inx];
                                dd[i] = dd[i] / dd[trd_inx];
                            }
                            ee[trd_inx] = 0;
                            dd[trd_inx] = 1;

                            //E = r1.Sum(x => (x - rr1) * (x - rr1)) + r3.Sum(x => (x - rr3) * (x - rr3));
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
                                lEs[0].Text = string.Format("{2}: {0:0.0000} | {1:0.0000000}", E, 1 / (1 + t * STEP_DECAY), t);
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

        private void btnCopyEEDD_Click(object sender, EventArgs e)
        {
            Clipboard.SetData(DataFormats.Text, string.Format("double[] ee = new double[] {{ {0} }};\r\ndouble[] dd = new double[] {{ {1} }}",
                string.Join(",", rows["事实期望"].Select(x => x.ToString()).ToArray()),
                string.Join(",", rows["事实方差"].Select(x => x.ToString()).ToArray())));
        }
    }
}

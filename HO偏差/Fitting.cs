using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HO偏差
{
    class Fitting
    {
        private const double EE_D_INC = 0.001;     // 期望求导增量
        private const double DD_D_INC = 0.001;      // 方差求导增量
        private const double EE_STEP = 0.8;
        private const double DD_STEP = 0.8;
        private const double STEP_DECAY = 0.01;     // 步长衰减
        private const int PRECISION = 5;

        private static double SQRT_2_PI = Math.Sqrt(2 * Math.PI);
        private static double MIN_EXP_VALUE = -PRECISION * Math.Log(10);

        private static double[,] _cached_exp_result = new double[10,30000];
        private static double[] _cached_exp_split = new double[] {
            Math.Log(0.9),
            Math.Log(0.8),
            Math.Log(0.7),
            Math.Log(0.6),
            Math.Log(0.5),
            Math.Log(0.4),
            Math.Log(0.3),
            Math.Log(0.2),
            Math.Log(0.1),
            MIN_EXP_VALUE
        };

        public static double exp(double x)
        {
            if (x > 0) return Math.Exp(x);
            double ls = 0;
            for (int i = 0; i < _cached_exp_split.Length; i++)
            {
                if (x > _cached_exp_split[i])
                {
                    double len = ls - _cached_exp_split[i];

                    int r = (int)((ls - x) * 30000 / len);
                    lock (_cached_exp_result)
                    {
                        if (_cached_exp_result[i, r] == 0)
                        {
                            _cached_exp_result[i, r] = Math.Exp(ls - len * r / 30000);
                        }
                    }
                    return _cached_exp_result[i, r];
                }
                else
                {
                    ls = _cached_exp_split[i];
                }
            }

            return 0;
        }

        private static double[,] _cached_gauss_result = new double[10, 30000];
        private static double[] _cached_gauss_split = new double[] {
            0.12565, //    0.45
            0.25334, //    0.4
            0.38531, //    0.35
            0.52439, //    0.3
            0.67448, //    0.25
            0.84161, //	   0.2
            1.03641, //    0.15
            1.28152, //    0.1
            1.6448, //     0.05
            4.25685, //    0

        };
        public static double gauss(double e, double d, double x)   // 计算P(v>x)
        {
            if (e != 0 || d != 1)
            {
                return gauss(0, 1, (x - e) / d);
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
                double ls = 0;
                for (int i = 0; i < _cached_gauss_split.Length; i++)
                {
                    if (x < _cached_gauss_split[i])
                    {
                        double len = _cached_gauss_split[i] - ls;

                        int r = (int)((x - ls) * 30000 / len);
                        lock (_cached_gauss_result)
                        {
                            if (_cached_gauss_result[i, r] == 0)
                            {
                                _cached_gauss_result[i, r] = Math.Round(0.5 - common.Math.Calculus.integrate(new common.Math.Calculus.Func(delegate(double y)
                                {
                                    return exp(-y * y / 2) / SQRT_2_PI;
                                }), 0, len * r / 30000 + ls, Math.Pow(10, -PRECISION)), PRECISION);
                            }
                        }
                        return _cached_gauss_result[i, r];
                    }
                    else
                    {
                        ls = _cached_gauss_split[i];
                    }
                }

                return 0;
            }
        }

        /// <summary>
        /// 计算我跑成绩确定时，其他人赢我概率
        /// </summary>
        /// <param name="CNT">参赛人数</param>
        /// <param name="PLC_CNT">有效名次</param>
        /// <param name="i">我的位置</param>
        /// <param name="x">我的成绩</param>
        /// <param name="ee">每个人的实力期望</param>
        /// <param name="dd">每个人的发挥均值</param>
        /// <param name="V">我赢其他人的概率</param>
        /// <param name="T">我最终取得名次的概率</param>
        private static void calcFx(int CNT, int PLC_CNT, int i, double x, double[] ee, double[] dd, out double[] V, out double[] T)
        {
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

            V = new double[CNT - 1];   // 我赢i的概率
            for (int j = 0; j < CNT; j++)
            {
                if (j < i)
                    V[j] = 1 - gauss(ee[j], dd[j], x);
                else if (j > i)
                    V[j - 1] = 1 - gauss(ee[j], dd[j], x);
                else // if (j == i)
                    continue;
            }

            int n = CNT - 1;
            T = new double[PLC_CNT];
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
        }

        private static void calcProbilityForFitting(double[] ee, double[] dd, out double[] p1)
        {
            int CNT = ee.Length;

            p1 = new double[CNT];

            for (int i = 0; i < CNT; i++)
            {
                common.Math.Calculus.Func f_top_3 = new common.Math.Calculus.Func(delegate(double x)
                {
                    double gx = exp(-(x - ee[i]) * (x - ee[i]) / (2 * dd[i] * dd[i])) / (SQRT_2_PI * dd[i]);

                    double[] V, T;

                    calcFx(CNT, 1, i, x, ee, dd, out V, out T);     

                    return T[0] * gx;
                });

                p1[i] = common.Math.Calculus.integrate(f_top_3, ee[i] - dd[i] * 7, ee[i] + dd[i] * 7, Math.Pow(10, -PRECISION), 5);
            }
        }

        private static void calcProbilityForFitting(double[] ee, double[] dd, out double[] p1, out double[] pq)
        {
            int CNT = ee.Length;

            p1 = new double[CNT];

            common.Math.Combination combq = new common.Math.Combination(CNT, 2);
            pq = new double[combq.Length];

            for (int i = 0; i < CNT; i++)
            {
                common.Math.Calculus.MultiFunc f_top_3 = new common.Math.Calculus.MultiFunc(delegate(double x)
                {
                    double gx = exp(-(x - ee[i]) * (x - ee[i]) / (2 * dd[i] * dd[i])) / (SQRT_2_PI * dd[i]);

                    double[] ret = new double[CNT];     // 第一个为WIN的概率，后面CNT-1个是我第二名时第一名的是各匹马的概率

                    double[] V, T;

                    calcFx(CNT, 2, i, x, ee, dd, out V, out T);

                    ret[0] = T[0];

                    // 计算我跑第二时，第一名各马的概率
                    double tmp = 1;  // 去掉不可能赢之后的概率连乘
                    int v0count = 0,  // 我赢概率为0的数量，即肯定超过我的数量
                        v1count = 0;  // 不可能超过我的数量
                    for (int j = 0; j < CNT - 1; j++)
                    {
                        if (V[j] == 0)
                        {
                            v0count++;
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
                    // 肯定超过我的数量v0count大于1，我的名次肯定低于2，我跑第2的概率为0
                    // 不可能超过的数量v1count大于CNT-2，我的名次肯定高于2
                    if (v0count <= 1 && v1count <= CNT - 2)
                    {
                        for (int j = 0, j2 = 1; j < CNT - 1; j++, j2++)
                        {
                            if (v0count == 1)
                            {
                                if (V[j] == 0)
                                    ret[j2] = tmp;
                                else
                                    ret[j2] = 0;
                            }
                            else
                            {
                                if (V[j] == 1)
                                {
                                    ret[j2] = 0;
                                }
                                else
                                {
                                    ret[j2] = tmp / V[j] * (1 - V[j]);
                                }
                            }
                        }
                    }

                    return new common.Math.vector(ret) * gx;
                });

                common.Math.vector pv = common.Math.Calculus.integrate(f_top_3, ee[i] - dd[i] * 7, ee[i] + dd[i] * 7, Math.Pow(10, -PRECISION), 5);
                p1[i] = pv[0];

                int[] c = new int[2];
                c[0] = i;

                for (int j = 0, j2 = 1; j < CNT - 1; j++, j2++)
                {
                    // 只有和我的组合
                    if (j < i)
                        c[1] = j;
                    else
                        c[1] = j + 1;

                    pq[combq.Index(c)] += pv[j2];
                }
            }
        }

        public static void calcProbility(double[] ee, double[] dd, int PLC_CNT, out double[] p1, out double[] p3)
        {
            int CNT = ee.Length;

            p1 = new double[CNT];
            p3 = new double[CNT];

            for (int i = 0; i < CNT; i++)
            {
                common.Math.Calculus.MultiFunc f_top_3 = new common.Math.Calculus.MultiFunc(delegate(double x)
                {
                    double gx = exp(-(x - ee[i]) * (x - ee[i]) / (2 * dd[i] * dd[i])) / (SQRT_2_PI * dd[i]);

                    double[] ret = new double[2];     // 前两个为WIN和PLC的概率

                    double[] V, T;

                    calcFx(CNT, PLC_CNT, i, x, ee, dd, out V, out T);

                    ret[0] = T[0];
                    ret[1] = T.Sum();

                    return new common.Math.vector(ret) * gx;
                });

                common.Math.vector pv = common.Math.Calculus.integrate(f_top_3, ee[i] - dd[i] * 7, ee[i] + dd[i] * 7, Math.Pow(10, -PRECISION), 5);
                p1[i] = pv[0];
                p3[i] = pv[1];
            }
        }

        public static void calcProbility(double[] ee, double[] dd, int PLC_CNT, out double[] p1, out double[] p3, out double[] pq_win, out double[] pq_plc)
        {
            int CNT = ee.Length;

            p1 = new double[CNT];
            p3 = new double[CNT];

            // 需要计算各种组合细节
            common.Math.Combination combq = new common.Math.Combination(CNT, 2);
            pq_win = new double[combq.Length];
            pq_plc = new double[combq.Length];

            common.Math.Combination comb_plc = new common.Math.Combination(CNT - 1, PLC_CNT - 1);
            int[][] _plc_combinations = comb_plc.GetCombinations();
            common.Math.Combination comb2 = null;
            int[][] _2_combinations = null;
            if (PLC_CNT > 3)
            {
                comb2 = new common.Math.Combination(PLC_CNT - 1, 2);
                _2_combinations = comb2.GetCombinations();
            }

            for (int i = 0; i < CNT; i++)
            {
                common.Math.Calculus.MultiFunc f_top_3 = new common.Math.Calculus.MultiFunc(delegate(double x)
                {
                    double gx = exp(-(x - ee[i]) * (x - ee[i]) / (2 * dd[i] * dd[i])) / (SQRT_2_PI * dd[i]);

                    double[] ret;     // 前两个为WIN和PLC的概率

                    if (PLC_CNT <= 3)
                    {
                        ret = new double[comb_plc.Length + 2];
                    }
                    else
                    {
                        // 如果PLC_CNT超过3，为了计算前两名的概率，还必须计算我第二名时，其他马第一名的概率
                        ret = new double[comb_plc.Length + CNT - 1 + 2];
                    }

                    double[] V, T;

                    calcFx(CNT, PLC_CNT, i, x, ee, dd, out V, out T);

                    ret[0] = T[0];
                    ret[1] = T.Sum();

                    // 计算我跑第三(PLC_CNT)时，前两(PLC_CNT-1)名各种组合的概率
                    double tmp = 1;  // 去掉不可能赢之后的概率连乘
                    int v0count = 0,  // 我赢概率为0的数量，即肯定超过我的数量
                        v1count = 0;  // 不可能超过我的数量
                    for (int j = 0; j < CNT - 1; j++)
                    {
                        if (V[j] == 0)
                        {
                            v0count++;
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
                        for (int j = 0, j2 = 2; j < comb_plc.Length; j++, j2++)
                        {
                            int[] c = _plc_combinations[j];
                            int cv0count = 0;
                            ret[j2] = tmp;
                            for (int k = 0; k < PLC_CNT - 1; k++)
                            {
                                if (V[c[k]] == 0)
                                {
                                    cv0count++;
                                }
                                else if (V[c[k]] == 1)  // 我肯定赢的人在我前面，这个组合不可能存在
                                {
                                    ret[j2] = 0;
                                    break;
                                }
                                else
                                {
                                    ret[j2] /= V[c[k]];
                                    ret[j2] *= (1 - V[c[k]]);
                                }
                            }

                            if (cv0count < v0count)  // 如果组合中没有包含全部肯定赢我的人，这个组合不可能存在
                            {
                                ret[j2] = 0;
                            }
                        }
                    }
                    // 计算我第二名时各个人第一名的概率
                    if (PLC_CNT > 3 && v0count < 2 && v1count <= CNT - 2)
                    {
                        for (int j = 0, j2 = 2 + (int)comb_plc.Length; j < CNT - 1; j++, j2++)
                        {
                            if (v0count == 1)
                            {
                                if (V[j] == 0)
                                    ret[j2] = tmp;
                                else
                                    ret[j2] = 0;
                            }
                            else
                            {
                                if (V[j] == 1)
                                {
                                    ret[j2] = 0;
                                }
                                else
                                {
                                    ret[j2] = tmp / V[j] * (1 - V[j]);
                                }
                            }
                        }
                    }

                    return new common.Math.vector(ret) * gx;
                });

                common.Math.vector pv = common.Math.Calculus.integrate(f_top_3, ee[i] - dd[i] * 7, ee[i] + dd[i] * 7, Math.Pow(10, -PRECISION), 5);
                p1[i] = pv[0];
                p3[i] = pv[1];

                if (PLC_CNT > 3)
                {
                    // PLC_CNT大于3，Q的概率需要通过我第二名时，其他各马第一名的概率计算

                    int[] c = new int[2];
                    c[0] = i;

                    // Q的概率
                    for (int j = 0, j2 = 2 + (int)comb_plc.Length; j < PLC_CNT - 1; j++, j2++)
                    {
                        for (int k = 0; k < PLC_CNT - 1; k++)
                        {
                            if (k < i)
                                c[1] = k;
                            else
                                c[1] = k + 1;

                            pq_win[combq.Index(c)] += pv[j2];
                        }
                    }

                    // QP的概率
                    for (int j = 0, j2 = 2; j < comb_plc.Length; j++, j2++)
                    {
                        // 和我的组合
                        c[0] = i;
                        for (int k = 0; k < PLC_CNT - 1; k++)
                        {
                            if (_plc_combinations[j][k] < i)
                                c[1] = _plc_combinations[j][k];
                            else
                                c[1] = _plc_combinations[j][k] + 1;

                            pq_plc[combq.Index(c)] += pv[j2];
                        }

                        // 没有我的组合
                        for (int k = 0; k < _2_combinations.Length; k++)
                        {
                            if (_plc_combinations[j][_2_combinations[k][0]] < i)
                                c[0] = _plc_combinations[j][_2_combinations[k][0]];
                            else
                                c[0] = _plc_combinations[j][_2_combinations[k][0]] + 1;

                            if (_plc_combinations[j][_2_combinations[k][1]] < i)
                                c[1] = _plc_combinations[j][_2_combinations[k][1]];
                            else
                                c[1] = _plc_combinations[j][_2_combinations[k][1]] + 1;

                            pq_plc[combq.Index(c)] += pv[j2];
                        }
                    }
                }
                else if (PLC_CNT == 3)
                {
                    // PLC_CNT等于3，Q的概率根据我第三名时，前两名的概率计算

                    int[] c = new int[2];

                    for (int j = 0, j2 = 2; j < comb_plc.Length; j++, j2++)
                    {
                        // 和我的组合
                        c[0] = i;
                        for (int k = 0; k < PLC_CNT - 1; k++)
                        {
                            if (_plc_combinations[j][k] < i)
                                c[1] = _plc_combinations[j][k];
                            else
                                c[1] = _plc_combinations[j][k] + 1;

                            pq_plc[combq.Index(c)] += pv[j2];
                        }

                        // 没有我的组合
                        if (_plc_combinations[j][0] < i)
                            c[0] = _plc_combinations[j][0];
                        else
                            c[0] = _plc_combinations[j][0] + 1;

                        if (_plc_combinations[j][1] < i)
                            c[1] = _plc_combinations[j][1];
                        else
                            c[1] = _plc_combinations[j][1] + 1;

                        pq_win[combq.Index(c)] += pv[j2];
                        pq_plc[combq.Index(c)] += pv[j2];
                    }
                }
                else if (PLC_CNT == 2)
                {
                    // PLC_CNT等于2，Q/QP是一样的

                    int[] c = new int[2];
                    c[0] = i;

                    for (int j = 0, j2 = 2; j < comb_plc.Length; j++, j2++)
                    {
                        // 只有和我的组合
                        if (_plc_combinations[j][0] < i)
                            c[1] = _plc_combinations[j][0];
                        else
                            c[1] = _plc_combinations[j][0] + 1;

                        pq_win[combq.Index(c)] += pv[j2];
                    }
                }
            }
        }

        public static void calcProbility(HrsTable table, out double[] p1, out double[] p3, out double[] pq_win, out double[] pq_plc)
        {
            int CNT = table.Count;
            int PLC_CNT = 3;
            if (CNT <= table.PLC_SPLIT_POS) PLC_CNT = 2;

            double[] ee = table.Select(x => x.Mean).ToArray();
            double[] dd = table.Select(x => x.Var).ToArray();

            if ((table.HasSpQ && table.SpQ.Count > 0) || (table.HasSpQp && table.SpQp.Count > 0))
            {
                calcProbility(ee, dd, PLC_CNT, out p1, out p3, out pq_win, out pq_plc);

                if (!table.HasSpQ || table.SpQ.Count == 0)
                {
                    pq_win = null;
                }
                if (!table.HasSpQp || table.SpQp.Count == 0)
                {
                    pq_plc = null;
                }
            }
            else
            {
                calcProbility(ee, dd, PLC_CNT, out p1, out p3);
                pq_win = null;
                pq_plc = null;
            }
        }

        public static double[] calcBetRateForWin(HrsTable table, out double r)
        {
            int CNT = table.Count;
            double[] s1 = table.SpWin;
            double rr1 = 1 / s1.Sum(x => 1 / x);

            // 计算WIN投注比率
            double[] ph1 = new double[CNT];
            for (int i = 0; i < CNT; i++)
            {
                ph1[i] = rr1 / s1[i];
            }

            r = rr1;
            return ph1;
        }

        public static double[] calcBetRateForPlc(HrsTable table, out double r)
        {
            int CNT = table.Count;
            int PLC_CNT = 3;
            if (CNT <= table.PLC_SPLIT_POS) PLC_CNT = 2;
            
            double[] s3 = table.SpPlc;

            // 计算PLC赔付率
            IOrderedEnumerable<double> sorted_s3 = s3.OrderBy(x => x);
            double o3 = sorted_s3.Skip(PLC_CNT - 1).First();
            double a = PLC_CNT * (o3 - 1) / (PLC_CNT * (o3 - 1) + 1) * sorted_s3.Take(PLC_CNT - 1).Sum(x => 1 / (PLC_CNT * (x - 1)));
            double b = sorted_s3.Skip(PLC_CNT - 1).Sum(x => 1 / (PLC_CNT * (x - 1) + 1));
            double rr3 = (a + 1) / (a + b);

            // 计算投注比例
            double po3 = (1 - rr3) / ((sorted_s3.Skip(PLC_CNT - 1).Sum(x => 1 / (PLC_CNT * (x - 1) + 1)) - 1) * (PLC_CNT * (o3 - 1) + 1));
            double[] ph3 = new double[CNT];
            for (int i = 0; i < CNT; i++)
            {
                if (s3[i] <= o3)
                {
                    ph3[i] = po3 * (o3 - 1) / (s3[i] - 1);
                }
                else
                {
                    ph3[i] = po3 * (PLC_CNT * (o3 - 1) + 1) / (PLC_CNT * (s3[i] - 1) + 1);
                }
            }

            r = rr3;
            return ph3;
        }


        public static double[] calcBetRateForPlcWithStyle2(HrsTable table, ref double r)
        {
            int CNT = table.Count;
            int PLC_CNT = 3;
            if (CNT <= table.PLC_SPLIT_POS) PLC_CNT = 2;

            double[] s3 = table.SpPlc;

            double[] ph3 = new double[CNT];
            if (s3.Where(x => x == 1).Count() == 0)
            {
                // 没有有强制设为1的赔率
                r = PLC_CNT / s3.Sum(x => 1 / x);  // 赔付率

                for (int i = 0; i < CNT; i++)
                {
                    ph3[i] = r / s3[i] / PLC_CNT;
                }                
            }
            else
            {
                // 否则用预计赔付率
                List<int> od_1_indices = new List<int>();
                double other_p_sum = 0;
                for (int i = 0; i < CNT; i++)
                {
                    if (s3[i] > 1)
                    {
                        ph3[i] = r / s3[i] / PLC_CNT;
                        other_p_sum += ph3[i];
                    }
                    else
                    {
                        od_1_indices.Add(i);
                    }
                }
                foreach (int i in od_1_indices)
                {
                    ph3[i] = (PLC_CNT - other_p_sum) / od_1_indices.Count;
                    if (r / ph3[i] / PLC_CNT > 1.1)
                    {
                        // 验证
                        r = 0;      // 计算偏差过大
                        break;  
                    }
                }
            }

            return ph3;
        }

        public static double[] calcBetRateForQn(HrsTable table, out double r)
        {
            r = 0;

            // 计算Q的投注比例
            double[] pqw = null;
            if (table.HasSpQ)
            {
                if (table.SpQ.Count > 0)
                {
                    double[] sqw = table.SpQ.Sp;
                    pqw = new double[sqw.Length];

                    double rqw = 1 / sqw.Sum(x => 1 / x);
                    for (int i = 0; i < pqw.Length; i++)
                    {
                        pqw[i] = rqw / sqw[i];
                    }

                    r = rqw;
                }
            }

            return pqw;
        }

        public static double[] calcBetRateForQp(HrsTable table, out double r)
        {
            r = 0;

            int CNT = table.Count;
            int PLC_CNT = 3;
            if (CNT <= table.PLC_SPLIT_POS) PLC_CNT = 2;

            // 计算QP的投注比例
            // 修正发现几个错误引用了其他计算的变量 2017-09-17
            double[] pqp = null;
            if (table.HasSpQp)
            {
                if (table.SpQp.Count > 0)
                {
                    double[] sqp = table.SpQp.Sp;
                    pqp = new double[sqp.Length];

                    int QP_CNT = (int)(new common.Math.Combination(PLC_CNT, 2)).Length;

                    // 计算QP赔付率
                    IOrderedEnumerable<double> sorted_sqp = sqp.OrderBy(x => x);
                    double oqp3 = sorted_sqp.Skip(QP_CNT - 1).First();
                    double aqp = QP_CNT == 1 ? 0 : QP_CNT * (oqp3 - 1) / (QP_CNT * (oqp3 - 1) + 1) * sorted_sqp.Take(QP_CNT - 1).Sum(x => 1 / (QP_CNT * (x - 1)));
                    double bqp = sorted_sqp.Skip(QP_CNT - 1).Sum(x => 1 / (QP_CNT * (x - 1) + 1));
                    double rqp = (aqp + 1) / (aqp + bqp);

                    // 计算投注比例
                    double pqp3 = (1 - rqp) / ((sorted_sqp.Skip(QP_CNT - 1).Sum(x => 1 / (QP_CNT * (x - 1) + 1)) - 1) * (QP_CNT * (oqp3 - 1) + 1));
                    for (int i = 0; i < pqp.Length; i++)
                    {
                        if (sqp[i] <= oqp3)
                        {
                            pqp[i] = pqp3 * (oqp3 - 1) / (sqp[i] - 1);
                        }
                        else
                        {
                            pqp[i] = pqp3 * (QP_CNT * (oqp3 - 1) + 1) / (QP_CNT * (sqp[i] - 1) + 1);
                        }
                    }

                    r = rqp;
                }
            }

            return pqp;
        }

        public static double[] calcBetRateForQpWithStyle2(HrsTable table, ref double r)
        {
            int CNT = table.Count;
            int PLC_CNT = 3;
            if (CNT <= table.PLC_SPLIT_POS) PLC_CNT = 2;

            double[] pqp = null;
            if (table.HasSpQp)
            {
                if (table.SpQp.Count > 0)
                {
                    double[] sqp = table.SpQp.Sp;
                    pqp = new double[sqp.Length];

                    int QP_CNT = (int)(new common.Math.Combination(PLC_CNT, 2)).Length;

                    if (sqp.Where(x => x == 1).Count() == 0)
                    {
                        // 没有有强制设为1的赔率
                        r = QP_CNT / sqp.Sum(x => 1 / x);  // 赔付率

                        for (int i = 0; i < sqp.Length; i++)
                        {
                            pqp[i] = r / sqp[i] / QP_CNT;
                        }
                    }
                    else
                    {
                        // 否则用预计赔付率
                        List<int> od_1_indices = new List<int>();
                        double other_p_sum = 0;
                        for (int i = 0; i < sqp.Length; i++)
                        {
                            if (sqp[i] > 1)
                            {
                                pqp[i] = r / sqp[i] / QP_CNT;
                                other_p_sum += pqp[i];
                            }
                            else
                            {
                                od_1_indices.Add(i);
                            }
                        }
                        foreach (int i in od_1_indices)
                        {
                            pqp[i] = (QP_CNT - other_p_sum) / od_1_indices.Count;
                            if (pqp[i] > 1 || r / pqp[i] / QP_CNT > 1.1)
                            {
                                // 验证
                                r = 0;      // 计算偏差过大
                                break;
                            }
                        }
                    }
                }
            }

            return pqp;
        }

        public static double[] calcMinOddsForPlc(HrsTable table, double[] betRate, double r)
        {
            if (betRate == null) return null;
            int CNT = table.Count;
            int PLC_CNT = 3;
            if (CNT <= table.PLC_SPLIT_POS) PLC_CNT = 2;
            if (betRate.Length != CNT) return null;

            IOrderedEnumerable<double> sorted_rate = betRate.OrderBy(x => x);
            double split_rate = sorted_rate.Skip(PLC_CNT - 1).First();
            double sum_top = sorted_rate.Take(PLC_CNT - 1).Sum();
            return betRate.Select(x => 1 + (r - sum_top - (x < split_rate ? split_rate : x)) / PLC_CNT / x).ToArray();
        }

        public static double[] calcMaxOddsForPlc(HrsTable table, double[] betRate, double r)
        {
            if (betRate == null) return null;
            int CNT = table.Count;
            int PLC_CNT = 3;
            if (CNT <= table.PLC_SPLIT_POS) PLC_CNT = 2;
            if (betRate.Length != CNT) return null;

            IOrderedEnumerable<double> sorted_rate = betRate.OrderByDescending(x => x);
            double split_rate = sorted_rate.Skip(PLC_CNT - 1).First();
            double sum_top = sorted_rate.Take(PLC_CNT - 1).Sum();
            return betRate.Select(x => 1 + (r - sum_top - (x > split_rate ? split_rate : x)) / PLC_CNT / x).ToArray();
        }

        /// <summary>
        /// 获得Qp相关的组合，只针对PLC_CNT=3的情况
        /// </summary>
        /// <param name="comb"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private static int[] getQpRelationIndice(common.Math.Combination comb, int index)
        {
            if (comb.M != 2) return null;
            int[] c = comb.Combine(index);
            List<int> ret = new List<int>();
            ret.Add(index);

            for (int i = 0; i < comb.N; i++)
            {
                if (Array.IndexOf(c, i) == -1)
                {
                    ret.Add(comb.Index(new int[] { i, c[0] }));
                    ret.Add(comb.Index(new int[] { i, c[1] }));
                }
            }
            return ret.ToArray();
        }

        public static double[][] calcMinMaxOddsForQp(HrsTable table, double[] betRate, double r)
        {
            if (betRate == null) return null;
            int CNT = table.Count;
            int PLC_CNT = 3;
            if (CNT <= table.PLC_SPLIT_POS) PLC_CNT = 2;

            common.Math.Combination comb = new common.Math.Combination(CNT, 2);
            if (betRate.Length != comb.Length) return null;

            
            if (PLC_CNT == 2)
            {
                double[] odds = betRate.Select(x => r / x).ToArray();
                return new double[][] { odds, odds };
            }
            else
            {
                double[] odds_min = new double[betRate.Length];
                double[] odds_max = new double[betRate.Length];
                for (int i = 0; i < betRate.Length; i++)
                {
                    //int[] rel_indices = getQpRelationIndice(comb, i);
                    //double[] rel_rates = new double[rel_indices.Length];
                    //for (int j = 0; j < rel_indices.Length; j++) rel_rates[j] = betRate[rel_indices[j]];
                    //IOrderedEnumerable<double> sorted_rate = rel_rates.OrderByDescending(x => x);

                    double br0 = betRate[i];
                    int[] c = comb.Combine(i);
                    double min = double.MaxValue, max = double.MinValue;
                    for (int j = 0; j < CNT; j++)
                    {
                        if (Array.IndexOf(c, j) == -1)
                        {
                            double br1 = betRate[comb.Index(new int[] { j, c[0] })];
                            double br2 = betRate[comb.Index(new int[] { j, c[1] })];

                            double od = (r - br1 - br2 - br0) / 3 / br0;
                            if (od < min) min = od;
                            if (od > max) max = od;
                        }
                    }
                    odds_min[i] = min;
                    odds_max[i] = max;
                }
                return new double[][] { odds_min, odds_max };
            }
        }

        public static double fit(HrsTable table, double epsilon)
        {
            int CNT = table.Count;

            double[] s1 = table.SpWin;
            double[] s3 = table.SpPlc;

            double[] ph1, ph3, pqw, pqp;
            double rr1, rr3, rqw, rqp;

            ph1 = calcBetRateForWin(table, out rr1);
            ph3 = calcBetRateForPlc(table, out rr3);
            pqw = calcBetRateForQn(table, out rqw);
            pqp = calcBetRateForQp(table, out rqp);

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
                throw new Exception("没想到找不到排行第3的项");
            }

            double[] ee, dd;
            if (table.E == 0)
            {
                ee = new double[CNT];
                dd = new double[CNT];

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
            }
            else
            {
                // 从已经训练的数据继续训练

                ee = table.Select(x => x.Mean).ToArray();
                dd = table.Select(x => x.Var).ToArray();
            }

            double[] ee2 = new double[CNT], dd2 = new double[CNT];
            double minE = table.E == 0 ? double.MaxValue : table.E;
            int minE_t = 0;

            Array.Copy(ee, ee2, CNT);
            Array.Copy(dd, dd2, CNT);

            double lastE = double.MaxValue;
            int reach_count = 0;
            for (int t = 0; ; t++)
            {
                double[] p1, pq1 = null;
                if (pqw != null)
                    calcProbilityForFitting(ee, dd, out p1, out pq1);
                else
                    calcProbilityForFitting(ee, dd, out p1);
                double E = 0;

                // 交叉熵损失以及交叉熵损失对与各个概率的梯度
                double[] grad_p1 = new double[CNT];
                for (int i = 0; i < CNT; i++)
                {
                    E += -(ph1[i] * Math.Log(p1[i]));
                    grad_p1[i] = -(ph1[i] / p1[i]);
                }
                double[] grad_pq1 = null;
                if (pqw != null)
                {
                    grad_pq1 = new double[pqw.Length];
                    for (int i = 0; i < pqw.Length; i++)
                    {
                        E += -(pqw[i] * Math.Log(pq1[i]));
                        grad_pq1[i] = -(pqw[i] / pq1[i]);
                    }
                }
                if (E < minE)
                {
                    minE_t = t;
                    Array.Copy(ee, ee2, CNT);
                    Array.Copy(dd, dd2, CNT);
                    minE = E;
                }

                // 求对各个参数的梯度
                double[] d_rr_ee = new double[CNT];
                double[] d_rr_dd = new double[CNT];
                for (int i = 0; i < CNT; i++)
                {
                    double[] tp1, tpq1 = null;

                    ee[i] += EE_D_INC;
                    if (pqw != null)
                        calcProbilityForFitting(ee, dd, out tp1, out tpq1);
                    else
                        calcProbilityForFitting(ee, dd, out tp1);

                    for (int j = 0; j < CNT; j++)
                    {
                        // 第j匹马的WIN概率对第i匹马的均值的梯度 = (tp1[j] - p1[j]) / EE_D_INC
                        d_rr_ee[i] += (tp1[j] - p1[j]) / EE_D_INC * grad_p1[j];
                    }
                    if (pqw != null)
                    {
                        for (int j = 0; j < pqw.Length; j++)
                        {
                            d_rr_ee[i] += (tpq1[j] - pq1[j]) / EE_D_INC * grad_pq1[j];
                        }
                    }
                    ee[i] -= EE_D_INC;

                    dd[i] += DD_D_INC;
                    if (pqw != null)
                        calcProbilityForFitting(ee, dd, out tp1, out tpq1);
                    else
                        calcProbilityForFitting(ee, dd, out tp1);
                    for (int j = 0; j < CNT; j++)
                    {
                        // 第j匹马的WIN概率对第i匹马的方差的梯度 = (tp1[j] - p1[j]) / DD_D_INC
                        d_rr_dd[i] += (tp1[j] - p1[j]) / DD_D_INC * grad_p1[j];
                    }
                    if (pqw != null)
                    {
                        for (int j = 0; j < pqw.Length; j++)
                        {
                            d_rr_dd[i] += (tpq1[j] - pq1[j]) / DD_D_INC * grad_pq1[j];
                        }
                    }
                    dd[i] -= DD_D_INC;
                }

                // 调整各参数
                for (int i = 0; i < CNT; i++)
                {
                    // 向负梯度方向调整
                    ee[i] += -d_rr_ee[i] * EE_STEP / (1 + t * STEP_DECAY);
                    dd[i] += -d_rr_dd[i] * DD_STEP / (1 + t * STEP_DECAY);

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
                
                if (Math.Abs(lastE - E) < epsilon)
                {
                    reach_count++;
                    if (reach_count > 3)
                    {
                        if (pqw != null)
                        {
                            double[] dpq = new double[pqw.Length];
                            for (int i = 0; i < dpq.Length; i++) dpq[i] = Math.Log(pqw[i] / pq1[i]);
                        }
                        double[] dp1 = new double[ph1.Length];
                        for (int i = 0; i < dp1.Length; i++) dp1[i] = Math.Log(ph1[i] / p1[i]);
                        break;
                    }
                }
                else if (t - minE_t > 20)
                {
                    Array.Copy(ee2, ee, CNT);
                    Array.Copy(dd2, dd, CNT);
                    lastE = minE;
                    break;
                }
                else
                {
                    reach_count = 0;
                }
                lastE = E;
            }

            // 拟合完成，将结果写回Hrs中
            for (int i = 0; i < CNT; i++)
            {
                table[i].Mean = ee[i];
                table[i].Var = dd[i];
            }

            return lastE;
        }
    }
}

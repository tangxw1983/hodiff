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

        private static Dictionary<double, double> _cached_exp_result = new Dictionary<double, double>();
        private static double exp(double x)
        {
            double r = Math.Round(x, PRECISION);
            if (!_cached_exp_result.ContainsKey(r))
            {
                _cached_exp_result[r] = Math.Exp(r);
            }
            return _cached_exp_result[r];
        }

        private static Dictionary<double, double> _cached_gauss_result = new Dictionary<double, double>();
        private static double gauss(double e, double d, double x)   // 计算P(v>x)
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
                double r = Math.Round(x, PRECISION);
                if (!_cached_gauss_result.ContainsKey(r))
                {
                    _cached_gauss_result[r] = Math.Round(0.5 - common.Math.Calculus.integrate(new common.Math.Calculus.Func(delegate(double y)
                    {
                        return exp(-y * y / 2) / SQRT_2_PI;
                    }), 0, r, Math.Pow(10, -PRECISION)), PRECISION);
                }
                return _cached_gauss_result[r];
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

        public static void calcProbility(double[] ee, double[] dd, out double[] p1, out double[] p3, ref double[] p3detail)
        {
            int CNT = ee.Length;
            int PLC_CNT = 3;
            if (CNT <= 6) PLC_CNT = 2;

            p1 = new double[CNT];
            p3 = new double[CNT];

            if (p3detail == null)
            {
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
            else
            {
                // 需要计算各种组合细节
                common.Math.Combination comb3 = new common.Math.Combination(CNT, PLC_CNT);
                p3detail = new double[comb3.Length];

                common.Math.Combination comb2 = new common.Math.Combination(CNT - 1, PLC_CNT - 1);
                int[][] _2_combinations = comb2.GetCombinations();

                for (int i = 0; i < CNT; i++)
                {
                    common.Math.Calculus.MultiFunc f_top_3 = new common.Math.Calculus.MultiFunc(delegate(double x)
                    {
                        double gx = exp(-(x - ee[i]) * (x - ee[i]) / (2 * dd[i] * dd[i])) / (SQRT_2_PI * dd[i]);

                        double[] ret = new double[comb2.Length + 2];     // 前两个为WIN和PLC的概率

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

                    common.Math.vector pv = common.Math.Calculus.integrate(f_top_3, ee[i] - dd[i] * 7, ee[i] + dd[i] * 7, Math.Pow(10, -PRECISION), 5);
                    p1[i] = pv[0];
                    p3[i] = pv[1];
                }
            }
            
        }

        public static double fit(HrsTable table, double epsilon)
        {
            int CNT = table.Count;

            double[] s1 = table.SpWin;
            double[] s3 = table.SpPlc;

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
                throw new Exception("没想到找不到排行第3的项");
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
            for (int t = 0; ; t++)
            {
                double[] p1, p3;
                calcProbility(ee, dd, out p1, out p3);
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

                // 求对各个参数的梯度
                double[] d_rr_ee = new double[CNT];
                double[] d_rr_dd = new double[CNT];
                for (int i = 0; i < CNT; i++)
                {
                    double[] tp1, tp3;

                    ee[i] += EE_D_INC;
                    calcProbility(ee, dd, out tp1, out tp3);
                    for (int j = 0; j < CNT; j++)
                    {
                        // 第j匹马的WIN概率对第i匹马的均值的梯度 = (tp1[j] - p1[j]) / EE_D_INC
                        d_rr_ee[i] += (tp1[j] - p1[j]) / EE_D_INC * grad_p1[j];
                        // 第j匹马的PLC概率对第i匹马的均值的梯度 = (tp3[j] - p3[j]) / EE_D_INC
                        d_rr_ee[i] += (tp3[j] - p3[j]) / EE_D_INC * grad_p3[j];
                    }
                    ee[i] -= EE_D_INC;

                    dd[i] += DD_D_INC;
                    calcProbility(ee, dd, out tp1, out tp3);
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

                if (lastE - E < epsilon) break;
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

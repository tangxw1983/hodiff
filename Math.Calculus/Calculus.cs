using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace common.Math
{
    public static class Calculus
    {
        public delegate double Func(double x);

        private class t
        {
            public t()
            {
                _t = null;
                _scale = 2;
                this.ResetScale();
            }

            private double[] _t;
            private int _scale;

            private void ResetScale()
            {
                double[] tmp = new double[_scale * _scale];
                if (_t != null && _t.Length > 0 && tmp.Length > 0) Array.Copy(_t, tmp, _t.Length > tmp.Length ? tmp.Length : _t.Length);
                if (_t != null) GC.SuppressFinalize(_t);
                _t = tmp;
            }

            /*
             *   j 0  1  2  3  4
             *  i
             *  0  0  1  4  9 16
             *  1  2  3  5 10 17
             *  2  6  7  8 11 18
             *  3 12 13 14 15 19
             *  4 20 21 22 23 24
             *  
             *   max(i,j)^2 + (i<j?i:j+i)
             * */
            private int GetIndex(int i, int j)
            {
                if (i < j)
                    return j * j + i;
                else
                    return i * i + i + j;
            }

            public double this[int i, int j]
            {
                get
                {
                    if (i >= _scale || j >= _scale)
                        return 0;
                    else
                        return _t[this.GetIndex(i, j)];
                }
                set
                {
                    if (i >= _scale || j >= _scale)
                    {
                        _scale = (i > j ? i : j) + 1;
                        this.ResetScale();
                    }

                    _t[this.GetIndex(i, j)] = value;
                }
            }
        }

        public static double integrate(Func f, double a, double b, double e)
        {
            t t = new t();
            int n, k, i, m;
            double h, g, p;
            h = (b - a) / 2;
            t[0, 0] = h * (f(a) + f(b));
            k = 1;
            n = 1;
            do
            {
                g = 0;
                for (i = 1; i <= n; i++)
                    g += f(a + (2 * i - 1) * h);
                t[k, 0] = t[k - 1, 0] / 2 + h * g;
                for (m = 1; m <= k; m++)
                {
                    p = System.Math.Pow(4, m);
                    t[k - m, m] = (p * t[k - m + 1, m - 1] - t[k - m, m - 1]) / (p - 1);
                }
                m -= 1;
                h /= 2;
                n *= 2;
                k += 1;
            } while (System.Math.Abs(t[0, m] - t[0, m - 1]) > e);

            return t[0, m];
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace common.Math
{
    public class vector
    {
        public vector(double[] data)
        {
            _data = data;
        }

        private double[] _data;

        public double this[int i]
        {
            get { return _data[i]; }
            set { _data[i] = value; }
        }

        public int Dimension
        {
            get
            {
                return _data.Length;
            }
        }

        public vector abs()
        {
            vector r = new vector(new double[_data.Length]);
            for (var i = 0; i < _data.Length; i++)
            {
                r[i] = System.Math.Abs(_data[i]);
            }
            return r;
        }

        public double Max()
        {
            return _data.Max();
        }

        public double[] toArray()
        {
            return _data;
        }

        public static vector operator +(vector x, vector y)
        {
            if (x == null) return y;
            if (y == null) return x;
            if (x._data == null) return y;
            if (y._data == null) return x;
            if (x.Dimension != y.Dimension) throw new Exception("维度不同，无法计算");
            vector r = new vector(new double[x.Dimension]);
            for (int i = 0; i < x.Dimension; i++)
            {
                r[i] = x[i] + y[i];
            }
            return r;
        }

        public static vector operator -(vector x)
        {
            vector r = new vector(new double[x.Dimension]);
            for (int i = 0; i < x.Dimension; i++)
            {
                r[i] = -x[i];
            }
            return r;
        }

        public static vector operator -(vector x, vector y)
        {
            if (x == null) return -y;
            if (y == null) return x;
            if (x._data == null) return -y;
            if (y._data == null) return x;
            if (x.Dimension != y.Dimension) throw new Exception("维度不同，无法计算");
            vector r = new vector(new double[x.Dimension]);
            for (int i = 0; i < x.Dimension; i++)
            {
                r[i] = x[i] - y[i];
            }
            return r;
        }

        public static vector operator *(vector x, double y)
        {
            vector r = new vector(new double[x.Dimension]);
            for (int i = 0; i < x.Dimension; i++)
            {
                r[i] = y * x[i];
            }
            return r;
        }

        public static vector operator *(double x, vector y)
        {
            return y * x;
        }

        public static vector operator /(vector x, double y)
        {
            return x * (1 / y);
        }

        public static vector operator /(double x, vector y)
        {
            vector r = new vector(new double[y.Dimension]);
            for (int i = 0; i < y.Dimension; i++)
            {
                r[i] = x / y[i];
            }
            return r;
        }
    }

    public static class Calculus
    {
        public delegate double Func(double x);
        public delegate vector MultiFunc(double x);

        private class t<T>
        {
            public t()
            {
                _t = null;
                _scale = 2;
                this.ResetScale();
            }

            private T[] _t;
            private int _scale;

            private void ResetScale()
            {
                T[] tmp = new T[_scale * _scale];
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

            public T this[int i, int j]
            {
                get
                {
                    if (i >= _scale || j >= _scale)
                        return default(T);
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

            public override string ToString()
            {
                string[] lines = new string[_scale];
                for (int i = 0; i < _scale; i++)
                {
                    lines[i] = "";
                    for (int j = 0; j < _scale; j++)
                    {
                        lines[i] += string.Format("{0:0.0000000000} ", this[i, j]);
                    }
                }
                return "\n" + string.Join("\n", lines);
            }
        }

        public static double integrate(Func f, double a, double b, double e)
        {
            return integrate(f, a, b, e, 2);
        }

        public static double integrate(Func f, double a, double b, double e, int min_step)
        {
            t<double> t = new t<double>();
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
            } while (k < min_step || System.Math.Abs(t[0, m] - t[0, m - 1]) > e);

            return t[0, m];
        }
        
        public static vector integrate(MultiFunc f, double a, double b, double e, int min_step)
        {
            t<vector> t = new t<vector>();
            int n, k, i, m;
            double h, p;
            vector g;
            h = (b - a) / 2;
            t[0, 0] = h * (f(a) + f(b));
            k = 1;
            n = 1;
            do
            {
                g = null;
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
            } while (k < min_step || (t[0, m] - t[0, m - 1]).abs().Max() > e);

            return t[0, m];
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace common.Math
{
    public class Combination
    {
        public Combination(int n, int m)
        {
            _n = n;
            _m = m;
            _length = Count(n, m);
        }

        private int _n;
        private int _m;
        private long _length;
        public long Length { get { return _length; } }

        public static long Count(int n, int m)
        {
            if (m > n / 2)
                return Count(n, n - m);
            else
            {
                long ret = 1;
                for (int i = 0; i < m; i++)
                {
                    ret *= n - i;
                    ret /= i + 1;
                }
                return ret;
            }
        }

        private long getOffset(int inx, int lvl, int pinx)
        {
            long ret = 0;
            if (lvl == _m - 1)
                ret = inx - pinx - 1;
            else
            {
                for (int i = pinx + 1; i < inx; i++)
                {
                    ret += Count(_n - i - 1, _m - lvl - 1);
                }
            }
            return ret;
        }

        public int Index(int[] c)
        {
            if (c == null) throw new ArgumentNullException("c");
            if (c.Length != _m) throw new ArgumentException("c length is not equal to m");

            long offset = 0;
            int pinx = -1;
            for (int i = 0; i < _m; i++)
            {
                offset += getOffset(c[i], i, pinx);
                pinx = c[i];
            }

            if (offset > int.MaxValue) throw new Exception("result out of range");
            return (int)offset;
        }

        public int[] Combine(int index)
        {
            if (index >= _length) throw new IndexOutOfRangeException();
            int[] ret = new int[_m];
            long offset = 0;
            int pinx = -1;
            for (int i = 0; i < _m - 1; i++)
            {
                long inc = 0;
                for (int j = pinx + 1; j < _n - _m + i + 1; j++)
                {
                    long dim_offset = getOffset(j + 1, i, pinx);
                    if (offset + dim_offset > index)
                    {
                        ret[i] = j;
                        pinx = j;
                        offset += inc;
                        break;
                    }
                    else
                    {
                        inc = dim_offset;
                    }
                }
            }
            ret[_m - 1] = index - (int)offset + pinx + 1;
            return ret;
        }

        public static int[] Random(int n, int m)
        {
            Random rand = new System.Random();
            int[] ret = new int[m];
            for (int k = 0; k < m; k++)
            {
                bool re_gen = true;
                do
                {
                    ret[k] = rand.Next(n);
                    re_gen = false;
                    for (int q = k - 1; q >= 0; q--)
                    {
                        if (ret[q] == ret[k])
                        {
                            re_gen = true;
                            break;
                        }
                    }
                } while (re_gen);
            }
            return ret;
        }

        public int[] Random()
        {
            return Random(_n, _m);
        }
    }
}

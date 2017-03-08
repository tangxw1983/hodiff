using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace test
{
    class Program
    {
        static void Main(string[] args)
        {
            test1();
        }

        private static void test1()
        {
            Console.WriteLine("标准正态分布函数表");
            common.Math.Calculus.Func f = new common.Math.Calculus.Func(delegate(double x)
            {
                return Math.Exp(-x * x / 2) / Math.Sqrt(2 * Math.PI);
            });
            for (int i = 1; i <= 30; i++)
            {
                Console.WriteLine("f({0}) = {1}", i * 0.0001, 0.5 + common.Math.Calculus.integrate(f, 0, i * 0.0001, 0.000000001));
            }
            Console.ReadLine();
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace HO偏差
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            //RaceData test = RaceData.Load("sp-2017-03-19-1-1.dat");

            Application.Run(new FormBuildTestDataSpecifiedPoint());
        }
    }
}

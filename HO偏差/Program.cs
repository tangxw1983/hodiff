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

            //bool flag;
            //string url, rawInfo;
            //url = "http://120.24.210.35:3000/data/market/get_tote_wp_raw_info?record_id=131238173&_=131230217";
            //rawInfo = FormBet.RaceHandler.getRawInfoByAPI(url);
            //HrsTable table = new HrsTable();
            //flag = FormBet.RaceHandler.parseWpOddsRaw(rawInfo, table);
            //url = "http://120.24.210.35:3000/data/market/get_tote_qn_raw_info?record_id=189820772&_=189813870";
            //rawInfo = FormBet.RaceHandler.getRawInfoByAPI(url);
            //flag = FormBet.RaceHandler.parseQnOddsRaw(rawInfo, table);
            //double E = Fitting.fit(table, 0.0001);
            //Console.WriteLine("{0}", E);


            //FormBet.RaceHandler.parseQnOddsRaw

            Application.Run(new FormBet());
        }
    }
}

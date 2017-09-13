using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Text.RegularExpressions;

namespace HO偏差
{
    public partial class FormTestFitting : Form
    {
        public FormTestFitting()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            HrsTable table = new HrsTable();
            table.Add(new Hrs("1", 5.5, 2.3));
            table.Add(new Hrs("2", 9.1, 1.6));
            table.Add(new Hrs("3", 44.9, 8.2));
            table.Add(new Hrs("4", 4.9, 2.7));
            table.Add(new Hrs("5", 16.3, 2.4));
            table.Add(new Hrs("6", 3.4, 1.5));
            table.Add(new Hrs("7", 15.6, 2.3));
            table.Add(new Hrs("8", 33.3, 4.4));
            table.Add(new Hrs("9", 17.3, 2.7));
            table.Add(new Hrs("10", 16.1, 3.4));
            table.Add(new Hrs("11", 40.1, 6.4));
            table.Add(new Hrs("12", 36, 5.7));
            table.Add(new Hrs("13", 42.7, 6.7));

            double E = Fitting.fit(table, 0.00001);
            MessageBox.Show(E.ToString());
        }

        private void btnFit_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.InitialDirectory = Application.StartupPath;
                dlg.Multiselect = true;

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    this.btnFit.Enabled = false;

                    Thread t = new Thread(delegate(object args)
                    {
                        string[] filenames = (string[])args;
                        foreach (string filename in dlg.FileNames)
                        {
                            RaceData race = RaceData.Load(filename);

                            int i = 0;
                            foreach (KeyValuePair<long, RaceDataItem> item in race)
                            {
                                if (i++ == 0) continue;
                                this.Invoke(new MethodInvoker(delegate
                                {
                                    this.txtFitLog.AppendText(string.Format("{0:HH:mm:ss} > {1} of {2} fitting...\r\n", DateTime.Now, item.Key, filename));
                                }));

                                item.Value.Odds.ClearSCR();
                                double E = Fitting.fit(item.Value.Odds, 0.0001);
                                item.Value.Odds.E = E;

                                this.Invoke(new MethodInvoker(delegate
                                {
                                    this.txtFitLog.AppendText(string.Format("{0:HH:mm:ss} > {1} of {2} E = {3}\r\n", DateTime.Now, item.Key, filename, E));
                                }));

                                break;
                            }

                            Match m = Regex.Match(filename, @"^(.+?)\.fit(\d*)$");
                            if (m.Success)
                            {
                                if (m.Groups[2].Value == "")
                                {
                                    race.Save(m.Groups[1].Value + ".fit2");
                                }
                                else
                                {
                                    race.Save(m.Groups[1].Value + ".fit" + (int.Parse(m.Groups[2].Value) + 1).ToString());
                                }
                            }
                            else
                            {
                                race.Save(filename + ".fit");
                            }

                            
                            this.Invoke(new MethodInvoker(delegate
                            {
                                this.txtFitLog.AppendText(string.Format("{0:HH:mm:ss} > file {1} finished\r\n", DateTime.Now, filename));
                            }));

                        }

                        this.Invoke(new MethodInvoker(delegate
                        {
                            this.txtFitLog.AppendText(string.Format("{0:HH:mm:ss} > all finished\r\n", DateTime.Now));
                            this.btnFit.Enabled = true;
                        }));
                    });
                    t.Start(dlg.FileNames);
                }
            }
        }
    }
}

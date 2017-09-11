using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HO偏差
{
    public partial class FormFixWaterType : Form
    {
        public FormFixWaterType()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.InitialDirectory = Application.StartupPath;
                dlg.Multiselect = true;

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    foreach (string filename in dlg.FileNames)
                    {
                        RaceData race = RaceData.Load(filename);
                        foreach (KeyValuePair<long, RaceDataItem> kvp in race)
                        {
                            kvp.Value.Waters.FixWaterType();
                        }
                        race.Save(filename);
                    }
                }
            }
        }
    }
}

namespace HO偏差
{
    partial class FormBet
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnStart = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.iptRaceDate = new System.Windows.Forms.DateTimePicker();
            this.iptRaceLoc = new System.Windows.Forms.TextBox();
            this.iptToteType = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // btnStart
            // 
            this.btnStart.Location = new System.Drawing.Point(468, 326);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(169, 71);
            this.btnStart.TabIndex = 0;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(46, 48);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(89, 18);
            this.label1.TabIndex = 1;
            this.label1.Text = "Race Date";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(46, 85);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(80, 18);
            this.label2.TabIndex = 1;
            this.label2.Text = "Race Loc";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(46, 124);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(89, 18);
            this.label3.TabIndex = 1;
            this.label3.Text = "Tote Type";
            // 
            // iptRaceDate
            // 
            this.iptRaceDate.Location = new System.Drawing.Point(168, 41);
            this.iptRaceDate.Name = "iptRaceDate";
            this.iptRaceDate.Size = new System.Drawing.Size(200, 28);
            this.iptRaceDate.TabIndex = 2;
            // 
            // iptRaceLoc
            // 
            this.iptRaceLoc.Location = new System.Drawing.Point(168, 82);
            this.iptRaceLoc.Name = "iptRaceLoc";
            this.iptRaceLoc.Size = new System.Drawing.Size(100, 28);
            this.iptRaceLoc.TabIndex = 3;
            // 
            // iptToteType
            // 
            this.iptToteType.Location = new System.Drawing.Point(168, 121);
            this.iptToteType.Name = "iptToteType";
            this.iptToteType.Size = new System.Drawing.Size(100, 28);
            this.iptToteType.TabIndex = 3;
            // 
            // FormBet
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(780, 539);
            this.Controls.Add(this.iptToteType);
            this.Controls.Add(this.iptRaceLoc);
            this.Controls.Add(this.iptRaceDate);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnStart);
            this.Name = "FormBet";
            this.Text = "FormBet";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.DateTimePicker iptRaceDate;
        private System.Windows.Forms.TextBox iptRaceLoc;
        private System.Windows.Forms.TextBox iptToteType;
    }
}
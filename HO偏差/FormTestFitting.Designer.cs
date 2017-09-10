namespace HO偏差
{
    partial class FormTestFitting
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
            this.button1 = new System.Windows.Forms.Button();
            this.btnFit = new System.Windows.Forms.Button();
            this.txtFitLog = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(525, 56);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(97, 49);
            this.button1.TabIndex = 0;
            this.button1.Text = "Test";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // btnFit
            // 
            this.btnFit.Location = new System.Drawing.Point(279, 224);
            this.btnFit.Name = "btnFit";
            this.btnFit.Size = new System.Drawing.Size(305, 86);
            this.btnFit.TabIndex = 1;
            this.btnFit.Text = "Fit";
            this.btnFit.UseVisualStyleBackColor = true;
            this.btnFit.Click += new System.EventHandler(this.btnFit_Click);
            // 
            // txtFitLog
            // 
            this.txtFitLog.Location = new System.Drawing.Point(12, 337);
            this.txtFitLog.Multiline = true;
            this.txtFitLog.Name = "txtFitLog";
            this.txtFitLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtFitLog.Size = new System.Drawing.Size(828, 265);
            this.txtFitLog.TabIndex = 2;
            // 
            // FormTestFitting
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(852, 614);
            this.Controls.Add(this.txtFitLog);
            this.Controls.Add(this.btnFit);
            this.Controls.Add(this.button1);
            this.Name = "FormTestFitting";
            this.Text = "FormTestFitting";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button btnFit;
        private System.Windows.Forms.TextBox txtFitLog;
    }
}
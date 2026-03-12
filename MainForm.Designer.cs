namespace SwitchFileSync
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.TextBox txtPcPath;
        private System.Windows.Forms.TextBox txtSwitchPath;
        private System.Windows.Forms.Button btnSendToSwitch;
        private System.Windows.Forms.Button btnSendToPc;
        private System.Windows.Forms.Label lblPcPath;
        private System.Windows.Forms.Label lblSwitchPath;
        private System.Windows.Forms.Button btnBrowsePc;
        private System.Windows.Forms.Button btnBrowseSwitch;
        private System.Windows.Forms.TreeView treeSwitchExplorer;
        private System.Windows.Forms.Label lblPlaytimePc;
        private System.Windows.Forms.Label lblPlaytimeSwitch;

        private System.Windows.Forms.ProgressBar progressBar;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            txtPcPath = new TextBox();
            txtSwitchPath = new TextBox();
            btnSendToSwitch = new Button();
            btnSendToPc = new Button();
            lblPcPath = new Label();
            lblSwitchPath = new Label();
            btnBrowsePc = new Button();
            btnBrowseSwitch = new Button();
            treeSwitchExplorer = new TreeView();
            lblPlaytimePc = new Label();
            lblPlaytimeSwitch = new Label();
            progressBar = new ProgressBar();
            SuspendLayout();
            // 
            // txtPcPath
            // 
            txtPcPath.Location = new Point(20, 40);
            txtPcPath.Name = "txtPcPath";
            txtPcPath.Size = new Size(400, 23);
            txtPcPath.TabIndex = 1;
            // 
            // txtSwitchPath
            // 
            txtSwitchPath.Location = new Point(20, 100);
            txtSwitchPath.Name = "txtSwitchPath";
            txtSwitchPath.Size = new Size(400, 23);
            txtSwitchPath.TabIndex = 4;
            // 
            // btnSendToSwitch
            // 
            btnSendToSwitch.Location = new Point(20, 140);
            btnSendToSwitch.Name = "btnSendToSwitch";
            btnSendToSwitch.Size = new Size(235, 35);
            btnSendToSwitch.TabIndex = 5;
            btnSendToSwitch.Text = "Upload to Switch";
            btnSendToSwitch.UseVisualStyleBackColor = true;
            btnSendToSwitch.Click += btnSendToSwitch_Click;
            // 
            // btnSendToPc
            // 
            btnSendToPc.Location = new Point(270, 140);
            btnSendToPc.Name = "btnSendToPc";
            btnSendToPc.Size = new Size(235, 35);
            btnSendToPc.TabIndex = 6;
            btnSendToPc.Text = "Download to PC";
            btnSendToPc.UseVisualStyleBackColor = true;
            btnSendToPc.Click += btnSendToPc_Click;
            // 
            // lblPcPath
            // 
            lblPcPath.AutoSize = true;
            lblPcPath.Location = new Point(20, 20);
            lblPcPath.Name = "lblPcPath";
            lblPcPath.Size = new Size(88, 15);
            lblPcPath.TabIndex = 0;
            lblPcPath.Text = "PC Save Folder:";
            // 
            // lblSwitchPath
            // 
            lblSwitchPath.AutoSize = true;
            lblSwitchPath.Location = new Point(20, 80);
            lblSwitchPath.Name = "lblSwitchPath";
            lblSwitchPath.Size = new Size(128, 15);
            lblSwitchPath.TabIndex = 3;
            lblSwitchPath.Text = "Selected Switch Folder:";
            // 
            // btnBrowsePc
            // 
            btnBrowsePc.Location = new Point(430, 40);
            btnBrowsePc.Name = "btnBrowsePc";
            btnBrowsePc.Size = new Size(75, 23);
            btnBrowsePc.TabIndex = 2;
            btnBrowsePc.Text = "Browse";
            btnBrowsePc.UseVisualStyleBackColor = true;
            btnBrowsePc.Click += btnBrowsePc_Click;
            // 
            // btnBrowseSwitch
            // 
            btnBrowseSwitch.Location = new Point(430, 100);
            btnBrowseSwitch.Name = "btnBrowseSwitch";
            btnBrowseSwitch.Size = new Size(75, 23);
            btnBrowseSwitch.TabIndex = 2;
            btnBrowseSwitch.Text = "Browse";
            btnBrowseSwitch.UseVisualStyleBackColor = true;
            btnBrowseSwitch.Click += btnBrowseSwitch_Click;
            // 
            // treeSwitchExplorer
            // 
            treeSwitchExplorer.Location = new Point(20, 220);
            treeSwitchExplorer.Name = "treeSwitchExplorer";
            treeSwitchExplorer.Size = new Size(485, 200);
            treeSwitchExplorer.TabIndex = 7;
            treeSwitchExplorer.BeforeExpand += treeSwitchExplorer_BeforeExpand;
            treeSwitchExplorer.AfterSelect += treeSwitchExplorer_AfterSelect;
            // 
            // lblPlaytimePc
            // 
            lblPlaytimePc.Location = new Point(20, 423);
            lblPlaytimePc.Margin = new Padding(0, 0, 3, 0);
            lblPlaytimePc.Name = "lblPlaytimePc";
            lblPlaytimePc.Size = new Size(250, 23);
            lblPlaytimePc.TabIndex = 0;
            lblPlaytimePc.Text = "Playtime PC: N/A";
            // 
            // lblPlaytimeSwitch
            // 
            lblPlaytimeSwitch.Location = new Point(270, 423);
            lblPlaytimeSwitch.Name = "lblPlaytimeSwitch";
            lblPlaytimeSwitch.Size = new Size(246, 23);
            lblPlaytimeSwitch.TabIndex = 1;
            lblPlaytimeSwitch.Text = "Playtime Switch: N/A";
            // 
            // progressBar
            // 
            progressBar.Location = new Point(20, 185);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(485, 20);
            progressBar.TabIndex = 8;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(524, 451);
            Controls.Add(progressBar);
            Controls.Add(treeSwitchExplorer);
            Controls.Add(btnSendToPc);
            Controls.Add(btnSendToSwitch);
            Controls.Add(txtSwitchPath);
            Controls.Add(lblSwitchPath);
            Controls.Add(lblPlaytimePc);
            Controls.Add(lblPlaytimeSwitch);
            Controls.Add(btnBrowsePc);
            Controls.Add(btnBrowseSwitch);
            Controls.Add(txtPcPath);
            Controls.Add(lblPcPath);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "MainForm";
            Text = "Hollow Knight Switch/PC Save Sync";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
    }
}

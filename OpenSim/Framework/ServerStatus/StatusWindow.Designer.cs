namespace OpenSim.Framework.ServerStatus
{
    partial class StatusWindow
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
            this.panel1 = new System.Windows.Forms.Panel();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabThreads = new System.Windows.Forms.TabPage();
            this.tabNetwork = new System.Windows.Forms.TabPage();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.m_outTotalTraffic = new System.Windows.Forms.Label();
            this.m_inTotalTraffic = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.m_outTcpTraffic = new System.Windows.Forms.Label();
            this.m_inTcpTraffic = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.m_outUdpTraffic = new System.Windows.Forms.Label();
            this.m_inUdpTraffic = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.m_networkDrawing = new System.Windows.Forms.PictureBox();
            this.tabMemory = new System.Windows.Forms.TabPage();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.m_memStats = new System.Windows.Forms.PictureBox();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.m_availableMem = new System.Windows.Forms.Label();
            this.m_commitSize = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.tabCpu = new System.Windows.Forms.TabPage();
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this.m_cpuDrawing = new System.Windows.Forms.PictureBox();
            this.tabPackets = new System.Windows.Forms.TabPage();
            this.panel1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabNetwork.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.m_networkDrawing)).BeginInit();
            this.tabMemory.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.m_memStats)).BeginInit();
            this.groupBox5.SuspendLayout();
            this.tabCpu.SuspendLayout();
            this.tableLayoutPanel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.m_cpuDrawing)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.tabControl1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(604, 550);
            this.panel1.TabIndex = 0;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabThreads);
            this.tabControl1.Controls.Add(this.tabNetwork);
            this.tabControl1.Controls.Add(this.tabMemory);
            this.tabControl1.Controls.Add(this.tabCpu);
            this.tabControl1.Controls.Add(this.tabPackets);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(604, 550);
            this.tabControl1.TabIndex = 0;
            // 
            // tabThreads
            // 
            this.tabThreads.Location = new System.Drawing.Point(4, 22);
            this.tabThreads.Name = "tabThreads";
            this.tabThreads.Padding = new System.Windows.Forms.Padding(3);
            this.tabThreads.Size = new System.Drawing.Size(596, 524);
            this.tabThreads.TabIndex = 0;
            this.tabThreads.Text = "Threads";
            this.tabThreads.UseVisualStyleBackColor = true;
            // 
            // tabNetwork
            // 
            this.tabNetwork.Controls.Add(this.tableLayoutPanel1);
            this.tabNetwork.Location = new System.Drawing.Point(4, 22);
            this.tabNetwork.Name = "tabNetwork";
            this.tabNetwork.Padding = new System.Windows.Forms.Padding(3);
            this.tabNetwork.Size = new System.Drawing.Size(596, 524);
            this.tabNetwork.TabIndex = 1;
            this.tabNetwork.Text = "Network";
            this.tabNetwork.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.groupBox1, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.m_networkDrawing, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(3, 3);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 94F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(590, 518);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.groupBox4);
            this.groupBox1.Controls.Add(this.groupBox3);
            this.groupBox1.Controls.Add(this.groupBox2);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox1.Location = new System.Drawing.Point(3, 427);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(584, 88);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Traffic";
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.m_outTotalTraffic);
            this.groupBox4.Controls.Add(this.m_inTotalTraffic);
            this.groupBox4.Controls.Add(this.label11);
            this.groupBox4.Controls.Add(this.label12);
            this.groupBox4.Location = new System.Drawing.Point(319, 20);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(148, 55);
            this.groupBox4.TabIndex = 0;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Total";
            // 
            // m_outTotalTraffic
            // 
            this.m_outTotalTraffic.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.m_outTotalTraffic.Location = new System.Drawing.Point(43, 29);
            this.m_outTotalTraffic.Name = "m_outTotalTraffic";
            this.m_outTotalTraffic.Size = new System.Drawing.Size(90, 13);
            this.m_outTotalTraffic.TabIndex = 1;
            this.m_outTotalTraffic.Text = "0,00 kB/s";
            // 
            // m_inTotalTraffic
            // 
            this.m_inTotalTraffic.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.m_inTotalTraffic.Location = new System.Drawing.Point(43, 16);
            this.m_inTotalTraffic.Name = "m_inTotalTraffic";
            this.m_inTotalTraffic.Size = new System.Drawing.Size(90, 13);
            this.m_inTotalTraffic.TabIndex = 1;
            this.m_inTotalTraffic.Text = "0,00 kB/s";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(10, 29);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(27, 13);
            this.label11.TabIndex = 0;
            this.label11.Text = "Out:";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(10, 16);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(19, 13);
            this.label12.TabIndex = 0;
            this.label12.Text = "In:";
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.m_outTcpTraffic);
            this.groupBox3.Controls.Add(this.m_inTcpTraffic);
            this.groupBox3.Controls.Add(this.label7);
            this.groupBox3.Controls.Add(this.label8);
            this.groupBox3.Location = new System.Drawing.Point(165, 20);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(148, 55);
            this.groupBox3.TabIndex = 0;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Tcp";
            // 
            // m_outTcpTraffic
            // 
            this.m_outTcpTraffic.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.m_outTcpTraffic.Location = new System.Drawing.Point(43, 29);
            this.m_outTcpTraffic.Name = "m_outTcpTraffic";
            this.m_outTcpTraffic.Size = new System.Drawing.Size(90, 13);
            this.m_outTcpTraffic.TabIndex = 1;
            this.m_outTcpTraffic.Text = "0,00 kB/s";
            // 
            // m_inTcpTraffic
            // 
            this.m_inTcpTraffic.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.m_inTcpTraffic.Location = new System.Drawing.Point(43, 16);
            this.m_inTcpTraffic.Name = "m_inTcpTraffic";
            this.m_inTcpTraffic.Size = new System.Drawing.Size(90, 13);
            this.m_inTcpTraffic.TabIndex = 1;
            this.m_inTcpTraffic.Text = "0,00 kB/s";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(10, 29);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(27, 13);
            this.label7.TabIndex = 0;
            this.label7.Text = "Out:";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(10, 16);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(19, 13);
            this.label8.TabIndex = 0;
            this.label8.Text = "In:";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.m_outUdpTraffic);
            this.groupBox2.Controls.Add(this.m_inUdpTraffic);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Location = new System.Drawing.Point(11, 20);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(148, 55);
            this.groupBox2.TabIndex = 0;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Udp";
            // 
            // m_outUdpTraffic
            // 
            this.m_outUdpTraffic.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.m_outUdpTraffic.Location = new System.Drawing.Point(43, 29);
            this.m_outUdpTraffic.Name = "m_outUdpTraffic";
            this.m_outUdpTraffic.Size = new System.Drawing.Size(90, 13);
            this.m_outUdpTraffic.TabIndex = 1;
            this.m_outUdpTraffic.Text = "0,00 kB/s";
            // 
            // m_inUdpTraffic
            // 
            this.m_inUdpTraffic.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.m_inUdpTraffic.Location = new System.Drawing.Point(43, 16);
            this.m_inUdpTraffic.Name = "m_inUdpTraffic";
            this.m_inUdpTraffic.Size = new System.Drawing.Size(90, 13);
            this.m_inUdpTraffic.TabIndex = 1;
            this.m_inUdpTraffic.Text = "0,00 kB/s";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(10, 29);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(27, 13);
            this.label2.TabIndex = 0;
            this.label2.Text = "Out:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(10, 16);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(19, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "In:";
            // 
            // m_networkDrawing
            // 
            this.m_networkDrawing.Dock = System.Windows.Forms.DockStyle.Fill;
            this.m_networkDrawing.Location = new System.Drawing.Point(3, 3);
            this.m_networkDrawing.Name = "m_networkDrawing";
            this.m_networkDrawing.Size = new System.Drawing.Size(584, 418);
            this.m_networkDrawing.TabIndex = 1;
            this.m_networkDrawing.TabStop = false;
            // 
            // tabMemory
            // 
            this.tabMemory.Controls.Add(this.tableLayoutPanel2);
            this.tabMemory.Location = new System.Drawing.Point(4, 22);
            this.tabMemory.Name = "tabMemory";
            this.tabMemory.Padding = new System.Windows.Forms.Padding(3);
            this.tabMemory.Size = new System.Drawing.Size(596, 524);
            this.tabMemory.TabIndex = 2;
            this.tabMemory.Text = "Memory";
            this.tabMemory.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.ColumnCount = 1;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Controls.Add(this.m_memStats, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.groupBox5, 0, 1);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(3, 3);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 2;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(590, 518);
            this.tableLayoutPanel2.TabIndex = 0;
            // 
            // m_memStats
            // 
            this.m_memStats.Dock = System.Windows.Forms.DockStyle.Fill;
            this.m_memStats.Location = new System.Drawing.Point(3, 3);
            this.m_memStats.Name = "m_memStats";
            this.m_memStats.Size = new System.Drawing.Size(584, 440);
            this.m_memStats.TabIndex = 2;
            this.m_memStats.TabStop = false;
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.m_availableMem);
            this.groupBox5.Controls.Add(this.m_commitSize);
            this.groupBox5.Controls.Add(this.label4);
            this.groupBox5.Controls.Add(this.label3);
            this.groupBox5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox5.Location = new System.Drawing.Point(3, 449);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(584, 66);
            this.groupBox5.TabIndex = 3;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Current memory usage";
            // 
            // m_availableMem
            // 
            this.m_availableMem.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.m_availableMem.Location = new System.Drawing.Point(90, 38);
            this.m_availableMem.Name = "m_availableMem";
            this.m_availableMem.Size = new System.Drawing.Size(100, 15);
            this.m_availableMem.TabIndex = 1;
            this.m_availableMem.Text = "0,00 mb";
            // 
            // m_commitSize
            // 
            this.m_commitSize.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.m_commitSize.Location = new System.Drawing.Point(90, 20);
            this.m_commitSize.Name = "m_commitSize";
            this.m_commitSize.Size = new System.Drawing.Size(100, 15);
            this.m_commitSize.TabIndex = 1;
            this.m_commitSize.Text = "0,00 mb";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(31, 39);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(53, 13);
            this.label4.TabIndex = 0;
            this.label4.Text = "Available:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(19, 21);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(65, 13);
            this.label3.TabIndex = 0;
            this.label3.Text = "Commit size:";
            // 
            // tabCpu
            // 
            this.tabCpu.Controls.Add(this.tableLayoutPanel3);
            this.tabCpu.Location = new System.Drawing.Point(4, 22);
            this.tabCpu.Name = "tabCpu";
            this.tabCpu.Padding = new System.Windows.Forms.Padding(3);
            this.tabCpu.Size = new System.Drawing.Size(596, 524);
            this.tabCpu.TabIndex = 3;
            this.tabCpu.Text = "Cpu";
            this.tabCpu.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanel3
            // 
            this.tableLayoutPanel3.ColumnCount = 1;
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel3.Controls.Add(this.m_cpuDrawing, 0, 0);
            this.tableLayoutPanel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel3.Location = new System.Drawing.Point(3, 3);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            this.tableLayoutPanel3.RowCount = 2;
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel3.Size = new System.Drawing.Size(590, 518);
            this.tableLayoutPanel3.TabIndex = 0;
            // 
            // m_cpuDrawing
            // 
            this.m_cpuDrawing.Dock = System.Windows.Forms.DockStyle.Fill;
            this.m_cpuDrawing.Location = new System.Drawing.Point(3, 3);
            this.m_cpuDrawing.Name = "m_cpuDrawing";
            this.m_cpuDrawing.Size = new System.Drawing.Size(584, 492);
            this.m_cpuDrawing.TabIndex = 0;
            this.m_cpuDrawing.TabStop = false;
            // 
            // tabPackets
            // 
            this.tabPackets.Location = new System.Drawing.Point(4, 22);
            this.tabPackets.Name = "tabPackets";
            this.tabPackets.Padding = new System.Windows.Forms.Padding(3);
            this.tabPackets.Size = new System.Drawing.Size(596, 524);
            this.tabPackets.TabIndex = 4;
            this.tabPackets.Text = "Packets";
            this.tabPackets.UseVisualStyleBackColor = true;
            // 
            // StatusWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            //AutoSizeMode didn't work with mono
            //this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(604, 550);
            this.Controls.Add(this.panel1);
            this.Name = "StatusWindow";
            this.Text = "realXtend server status";
            this.panel1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabNetwork.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.m_networkDrawing)).EndInit();
            this.tabMemory.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.m_memStats)).EndInit();
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.tabCpu.ResumeLayout(false);
            this.tableLayoutPanel3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.m_cpuDrawing)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabThreads;
        private System.Windows.Forms.TabPage tabNetwork;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Label m_outTotalTraffic;
        private System.Windows.Forms.Label m_inTotalTraffic;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label m_outTcpTraffic;
        private System.Windows.Forms.Label m_inTcpTraffic;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label m_outUdpTraffic;
        private System.Windows.Forms.Label m_inUdpTraffic;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.PictureBox m_networkDrawing;
        private System.Windows.Forms.TabPage tabMemory;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.PictureBox m_memStats;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label m_availableMem;
        private System.Windows.Forms.Label m_commitSize;
        private System.Windows.Forms.TabPage tabCpu;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
        private System.Windows.Forms.PictureBox m_cpuDrawing;
        private System.Windows.Forms.TabPage tabPackets;



    }
}
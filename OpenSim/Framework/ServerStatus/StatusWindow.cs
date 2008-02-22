using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Timers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenSim.Framework.ServerStatus
{
    public partial class StatusWindow : Form
    {
        System.Timers.Timer m_updateTimer;
        static Dictionary<int, ThreadItem> m_threadItems = new Dictionary<int, ThreadItem>();
        static Dictionary<int, string> m_idToName = new Dictionary<int, string>();
        static int m_nCoreCount = System.Environment.ProcessorCount;

        PerformanceCounter m_pcAvailRam = null;
        
        class TrafficHistory {
            public float outUdpBytes = 0;
            public float inUdpBytes = 0;
            public float outTcpBytes = 0;
            public float inTcpBytes = 0;
            public float outTotalBytes = 0;
            public float inTotalBytes = 0;
            public float resentBytes = 0;
        }

        class MemoryHistory {
            public float nonpagedSystemMemory;
            public float pagedMemory;
            public float pagedSystemMemory;
            public float gcReportedMem;
            public float workingSet;
        }

        class CpuHistory {
            public float[] cpuUsage = new float[m_nCoreCount];
            public float totalUsage = 0;
        }

        class PacketItem
        {
            public long m_bytesOut = 0;
            public long m_packetsOut = 0;
            public long m_bytesIn = 0;
            public long m_packetsIn = 0;
            public long m_resent = 0;

            public bool m_addedToList = false;
            public ListViewItem.ListViewSubItem m_listBytesOut = null;
            public ListViewItem.ListViewSubItem m_listPacketsOut = null;
            public ListViewItem.ListViewSubItem m_listBytesIn = null;
            public ListViewItem.ListViewSubItem m_listPacketsIn = null;
            public ListViewItem.ListViewSubItem m_listResent = null;
        }

        static float m_fNetworkHistoryScale;
        static float m_fMemoryHistoryScale;

        static Dictionary<string, PacketItem> m_packets = new Dictionary<string, PacketItem>();
        static LinkedList<TrafficHistory> m_trafficHistory = new LinkedList<TrafficHistory>();
        static LinkedList<MemoryHistory> m_memoryHistory = new LinkedList<MemoryHistory>();
        static LinkedList<CpuHistory> m_cpuHistory = new LinkedList<CpuHistory>();

        PerformanceCounter[] m_pcCpu = new PerformanceCounter[System.Environment.ProcessorCount];
        PerformanceCounter m_pcCpuTotal = null;

        static volatile int outUdpBytes = 0;
        static volatile int inUdpBytes = 0;
        static volatile int outTcpBytes = 0;
        static volatile int inTcpBytes = 0;
        static volatile int outResent = 0;

        BufferedListView m_threads;
        BufferedListView m_listPackets;

        #region BufferedListView
        /**
         * Flicker minimized listview
         **/
        public class BufferedListView : ListView
        {
            #region WM - Window Messages
            public enum WM
            {
                WM_NULL = 0x0000,
                WM_CREATE = 0x0001,
                WM_DESTROY = 0x0002,
                WM_MOVE = 0x0003,
                WM_SIZE = 0x0005,
                WM_ACTIVATE = 0x0006,
                WM_SETFOCUS = 0x0007,
                WM_KILLFOCUS = 0x0008,
                WM_ENABLE = 0x000A,
                WM_SETREDRAW = 0x000B,
                WM_SETTEXT = 0x000C,
                WM_GETTEXT = 0x000D,
                WM_GETTEXTLENGTH = 0x000E,
                WM_PAINT = 0x000F,
                WM_CLOSE = 0x0010,
                WM_QUERYENDSESSION = 0x0011,
                WM_QUIT = 0x0012,
                WM_QUERYOPEN = 0x0013,
                WM_ERASEBKGND = 0x0014,

            }
            #endregion

            #region RECT
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            public struct RECT
            {
                public int left;
                public int top;
                public int right;
                public int bottom;
            }
            #endregion

            #region Imported User32.DLL functions
            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            static public extern bool ValidateRect(IntPtr handle, ref RECT rect);
            #endregion

            #region GetWindowRECT
            // Get the listview's rectangle and return it as a RECT structure
            private RECT GetWindowRECT()
            {
                RECT rect = new RECT();
                rect.left = this.Left;
                rect.right = this.Right;
                rect.top = this.Top;
                rect.bottom = this.Bottom;
                return rect;
            }
            #endregion

            volatile public bool updating = false;

            public BufferedListView()
            {
            }


            protected override void OnPaintBackground(PaintEventArgs pea)
            {
                // do nothing here since this event is now handled by OnPaint
            }


            protected override void OnPaint(PaintEventArgs pea)
            {
                base.OnPaint(pea);
            }

            protected override void WndProc(ref Message messg)
            {
                if (updating)
                {
                    if ((int)WM.WM_ERASEBKGND == messg.Msg)
                    {
                        return;
                    } 
                    else if ((int)WM.WM_PAINT == messg.Msg)
                    {
                        RECT vrect = this.GetWindowRECT();
                        // validate the entire window				
                        ValidateRect(this.Handle, ref vrect);
                    }

                }
                base.WndProc(ref messg);
            }
        }
        #endregion

        #region ThreadItem
        /**
         * Represents a single thread item in the listview control
         **/
        class ThreadItem
        {
            public ListViewItem listItem;
            public ListViewItem.ListViewSubItem name;
            public ListViewItem.ListViewSubItem cpu;
        }
        #endregion

        
        public static void ReportOutPacketUdp(int size, bool resent)
        {
            if (resent)
            {
                outResent += size += 8;
            }
            outUdpBytes += size + 8;
        }

        public static void ReportInPacketUdp(int size) { inUdpBytes += size + 8; }

        public static void ReportOutPacketTcp(int size) { outTcpBytes += size + 20; }
        public static void ReportInPacketTcp(int size) { inTcpBytes += size + 20; }

        public static void ReportProcessedOutPacket(string name, int size, bool resent)
        {
            PacketItem item = null;
            if (m_packets.ContainsKey(name))
            {
                item = m_packets[name];
            }
            else
            {
                item = new PacketItem();
                m_packets[name] = item;
            }

            if (resent)
            {
                item.m_resent += size;
            }
            item.m_bytesOut += size;
            item.m_packetsOut++;
        }

        public static void ReportProcessedInPacket(string name, int size)
        {
            PacketItem item = null;
            if (m_packets.ContainsKey(name))
            {
                item = m_packets[name];
            }
            else
            {
                item = new PacketItem();
                m_packets[name] = item;
            }

            item.m_bytesIn += size;
            item.m_packetsIn++;
        }


        public StatusWindow()
        {
            m_pcAvailRam = new PerformanceCounter("Memory", "Available MBytes");
            m_packets = new Dictionary<string, PacketItem>();

            InitializeComponent();

            m_listPackets = new BufferedListView();
            m_listPackets.Dock = System.Windows.Forms.DockStyle.Fill;
            m_listPackets.GridLines = true;
            m_listPackets.Location = new System.Drawing.Point(3, 3);
            m_listPackets.MultiSelect = false;
            m_listPackets.Name = "m_listPackets";
            m_listPackets.Size = new System.Drawing.Size(500, 400);
            m_listPackets.TabIndex = 0;
            m_listPackets.View = System.Windows.Forms.View.Details;

            tabPackets.Controls.Add(m_listPackets);

            m_listPackets.Columns.Add("Packet").Width = 260;
            m_listPackets.Columns.Add("In count").Width = 80;
            m_listPackets.Columns.Add("In bytes").Width = 80;
            m_listPackets.Columns.Add("Out count").Width = 80;
            m_listPackets.Columns.Add("Out bytes").Width = 80;
            m_listPackets.Columns.Add("Resent").Width = 80;


            m_threads = new BufferedListView();
            m_threads.Dock = System.Windows.Forms.DockStyle.Fill;
            m_threads.GridLines = true;
            m_threads.Location = new System.Drawing.Point(3, 3);
            m_threads.MultiSelect = false;
            m_threads.Name = "m_threads";
            m_threads.Size = new System.Drawing.Size(500, 400);
            m_threads.TabIndex = 0;
            m_threads.View = System.Windows.Forms.View.Details;
            
            tabThreads.Controls.Add(m_threads);

            m_threads.Columns.Add("ID");
            m_threads.Columns.Add("Name").Width = 260;
            m_threads.Columns.Add("CPU Time").Width=100;

            m_updateTimer = new System.Timers.Timer(2000);
            m_updateTimer.Elapsed += new ElapsedEventHandler(UpdateTimer);
            outUdpBytes = 0;
            inUdpBytes = 0;
            outTcpBytes = 0;
            inTcpBytes = 0;
            outResent = 0;
            m_updateTimer.Start();

            for(int i = 0; i < m_nCoreCount; i++)
            {
                m_pcCpu[i] = new PerformanceCounter("Processor", "% Processor Time", i.ToString(), true);
                m_pcCpu[i].MachineName = ".";
            }
            m_pcCpuTotal = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
        }

        public void CloseStatusWindow() {
            Close();
            m_updateTimer.Stop();
            m_threadItems.Clear();

            m_pcAvailRam.Close();
            m_pcAvailRam = null;

            for (int i = 0; i < m_nCoreCount; i++)
            {
                m_pcCpu[i].Close();
                m_pcCpu[i] = null;
            }

            foreach(PacketItem item in m_packets.Values)
            {
                item.m_addedToList = false;
            }

            m_packets = null;

            m_pcCpuTotal.Close();
            m_pcCpuTotal = null;
        }

        public static void ReportThreadName(int id, string name)
        {
            lock (m_threadItems)
            {
                m_idToName[id] = name;
            }
        }

        delegate void UpdateControlsDelegate();

        TrafficHistory UpdateNetworkHistory()
        {
            TrafficHistory item = new TrafficHistory();

            item.inUdpBytes = ((float)inUdpBytes) / 1024.0f / 2.0f;
            item.outUdpBytes = ((float)outUdpBytes) / 1024.0f / 2.0f;
            item.inTcpBytes = ((float)inTcpBytes) / 1024.0f / 2.0f;
            item.outTcpBytes = ((float)outTcpBytes) / 1024.0f / 2.0f;
            item.resentBytes = ((float)outResent) / 1024.0f / 2.0f;
            item.inTotalBytes = item.inUdpBytes + item.inTcpBytes;
            item.outTotalBytes = item.outUdpBytes + item.outTcpBytes;
            inUdpBytes = 0;
            outUdpBytes = 0;
            inTcpBytes = 0;
            outTcpBytes = 0;
            outResent = 0;

            m_trafficHistory.AddFirst(item);
            if (m_trafficHistory.Count > 500)
            {
                m_trafficHistory.RemoveLast();
            }

            return item;
        }

        MemoryHistory UpdateMemoryHistory(Process proc)
        {
            MemoryHistory item = new MemoryHistory();

            item.gcReportedMem = ((float)System.GC.GetTotalMemory(false)) / 1024.0f / 1024.0f;
            item.workingSet = ((float)proc.WorkingSet64) / 1024.0f / 1024.0f;
            item.nonpagedSystemMemory = ((float)proc.NonpagedSystemMemorySize64) / 1024.0f / 1024.0f;
            item.pagedSystemMemory = ((float)proc.PagedMemorySize64) / 1024.0f / 1024.0f;
            item.pagedSystemMemory = ((float)proc.PagedSystemMemorySize64) / 1024.0f / 1024.0f;

            m_memoryHistory.AddFirst(item);
            if (m_memoryHistory.Count > 500)
            {
                m_memoryHistory.RemoveLast();
            }

            return item;
        }

        void UpdateCpuHistory() {
            CpuHistory item = new CpuHistory();

            for( int i = 0; i < m_nCoreCount; i++)
            {
                item.cpuUsage[i] = m_pcCpu[i].NextValue();
            }

            item.totalUsage = m_pcCpuTotal.NextValue();

            m_cpuHistory.AddFirst(item);
            if (m_cpuHistory.Count > 500)
            {
                m_cpuHistory.RemoveLast();
            }
        }

        string FormatDataSize(long byteCount)
        {
            double fCount = (double)byteCount;
            if (byteCount > 1024 * 1024 * 1024)
            {
                fCount/=(1024.0*1024.0*1024.0);
                return fCount.ToString("##0.00") + "GB";
            }
            else if (byteCount > 1024*1024*10)
            {
                fCount/=(1024.0*1024.0);
                return fCount.ToString("##0.00") + "MB";
            }
            else if (byteCount > 1024*10)
            {
                fCount/=1024.0;
                return fCount.ToString("##0.00") + "KB";
            }
            else
            {
                return byteCount.ToString() +"B";
            }
        }

        void UpdatePacketView()
        {
            foreach(KeyValuePair<string, PacketItem> item in m_packets)
            {
                if (item.Value.m_addedToList) 
                {
                    item.Value.m_listPacketsIn.Text = item.Value.m_packetsIn.ToString();
                    item.Value.m_listBytesIn.Text = FormatDataSize(item.Value.m_bytesIn);
                    item.Value.m_listPacketsOut.Text = item.Value.m_packetsOut.ToString();
                    item.Value.m_listBytesOut.Text = FormatDataSize(item.Value.m_bytesOut);
                    item.Value.m_listResent.Text = FormatDataSize(item.Value.m_resent);
                }
                else
                {
                    ListViewItem listItem = m_listPackets.Items.Add(item.Key);
                    item.Value.m_listPacketsIn = listItem.SubItems.Add(item.Value.m_packetsIn.ToString());
                    item.Value.m_listBytesIn = listItem.SubItems.Add(FormatDataSize(item.Value.m_bytesIn));
                    item.Value.m_listPacketsOut = listItem.SubItems.Add(item.Value.m_packetsOut.ToString());
                    item.Value.m_listBytesOut = listItem.SubItems.Add(FormatDataSize(item.Value.m_bytesOut));
                    item.Value.m_listResent = listItem.SubItems.Add(FormatDataSize(item.Value.m_resent));
                    item.Value.m_addedToList = true;
                }
                
            }
        }

        void UpdateControls()
        {
            Process proc = Process.GetCurrentProcess();

            TrafficHistory netItem = UpdateNetworkHistory();
            MemoryHistory memItem = UpdateMemoryHistory(proc);

            UpdateCpuHistory();

            if (tabControl1.SelectedIndex == 0)
            {
                m_threads.updating = true;
                UpdateThreadList();
                m_threads.updating = false;
                m_threads.Invalidate();
            }
            else if (tabControl1.SelectedIndex == 1)
            {
                RefreshNetworkStats(netItem);
                PaintNetworkHistory();
            }
            else if (tabControl1.SelectedIndex == 2)
            {
                m_availableMem.Text = m_pcAvailRam.NextValue().ToString("##0.00") + " MB";
                m_commitSize.Text = memItem.workingSet.ToString("##0.00") + " MB";
                PaintMemoryHistory();
            }
            else if (tabControl1.SelectedIndex == 3)
            {
                PaintCpuHistory();
            }
            else if (tabControl1.SelectedIndex == 4)
            {
                m_listPackets.updating = true;
                UpdatePacketView();
                m_listPackets.updating = false;
                m_listPackets.Invalidate();
            }
        }

        protected void PaintCpuHistory()
        {
            Pen[] pens = { Pens.Yellow, Pens.Blue, Pens.Green, Pens.Red,
                           Pens.White, Pens.Turquoise, Pens.Linen, Pens.Gray,
                           Pens.Purple, Pens.Pink, Pens.LightBlue, Pens.LightSalmon};

            Pen penLine = Pens.DarkSlateGray;

            Graphics screenGfx = m_cpuDrawing.CreateGraphics();
            Bitmap backBuffer = new Bitmap(m_cpuDrawing.Width, m_cpuDrawing.Height);
            Graphics gfx = Graphics.FromImage(backBuffer);

            gfx.Clear(Color.Black);

            float fMax = 105.0f;
            for (int i = 0; i < m_cpuDrawing.Height - 10; i += 30)
            {
                float yPos = m_cpuDrawing.Height - i - 15;
                float fPos = ((float)(i)) / ((float)m_cpuDrawing.Height);
                gfx.DrawLine(penLine, 0, yPos, m_cpuDrawing.Width, yPos);
            }

            //Size of second in pixels
            float fSecondStep = 2.5f; //120 seconds
            float fTotalSeconds = (1 / fSecondStep) * (m_cpuDrawing.Width - 90);
            for (int i = 0; i < m_cpuDrawing.Width - 90; i += 50)
            {
                float xPos = 90 + i;
                float fTime = fTotalSeconds - (((float)(i)) / fSecondStep);

                gfx.DrawLine(penLine, xPos, 0, xPos, m_cpuDrawing.Height - 15);
                string strText = fTime.ToString("##0");
                gfx.DrawString(strText, SystemFonts.DialogFont, Brushes.CadetBlue,
                               xPos - 4 * strText.Length, m_cpuDrawing.Height - 15);
            }


            float nXPos = m_cpuDrawing.Width;
            float fHeightMul = m_cpuDrawing.Height - 15;
            float fYStart = m_cpuDrawing.Height - 15;
            CpuHistory lastItem = null;

            foreach (CpuHistory item in m_cpuHistory)
            {
                if (lastItem != null)
                {
                    nXPos -= fSecondStep * 2;

                    for (int i = 0; i < m_nCoreCount; i++)
                    {
                        gfx.DrawLine(pens[i+1], nXPos, fYStart - (item.cpuUsage[i] / fMax) * fHeightMul,
                                     nXPos + fSecondStep * 2, fYStart - (lastItem.cpuUsage[i] / fMax) * fHeightMul);
                    }

                    gfx.DrawLine(pens[0], nXPos, fYStart - (item.totalUsage / fMax) * fHeightMul,
                                 nXPos + fSecondStep * 2, fYStart - (lastItem.totalUsage / fMax) * fHeightMul);

                    if (nXPos < 0)
                        break;
                }

                lastItem = item;
            }

            for (int i = 0; i < m_cpuDrawing.Height - 10; i += 30)
            {
                float yPos = m_cpuDrawing.Height - i - 15;
                float fPos = ((float)(i)) / ((float)m_cpuDrawing.Height);
                gfx.DrawString((fPos * fMax).ToString("##0.00") + "%",
                               SystemFonts.DialogFont, Brushes.CadetBlue, 3, yPos);
            }

            int nPosX = m_cpuDrawing.Width - 50;


            gfx.DrawString("Total", SystemFonts.DialogFont, pens[0].Brush, nPosX, 10);
            for (int i = 0; i < m_nCoreCount; i++ )
            {
                gfx.DrawString("Core " + i, SystemFonts.DialogFont, pens[i+1].Brush, nPosX, 22 + i * 12);
            }

            screenGfx.DrawImageUnscaled(backBuffer, 0, 0);
        }

        protected void PaintNetworkHistory()
        {
            Pen penUdpOut = Pens.SkyBlue;
            Pen penUdpIn = Pens.Blue;
            Pen penTcpOut = Pens.Red;
            Pen penTcpIn = Pens.Pink;
            Pen penTotalOut = Pens.Green;
            Pen penTotalIn = Pens.LimeGreen;
            Pen penResent = Pens.Orange;

            Pen penLine = Pens.DarkSlateGray;

            
            Graphics screenGfx = m_networkDrawing.CreateGraphics();
            Bitmap backBuffer = new Bitmap(m_networkDrawing.Width, m_networkDrawing.Height);
            Graphics gfx = Graphics.FromImage(backBuffer);
            
            gfx.Clear(Color.Black);

            float fMax = m_fNetworkHistoryScale;
            if (fMax < 12.0f)
                fMax = 12.0f;
            for( int i = 0; i < m_networkDrawing.Height-10; i+= 30)
            {
                float yPos = m_networkDrawing.Height - i-15;
                float fPos = ((float)(i)) / ((float)m_networkDrawing.Height);
                gfx.DrawLine(penLine, 0, yPos, m_networkDrawing.Width, yPos);
            }

            //Size of second in pixels
            float fSecondStep = 1.5f; //120 seconds
            float fTotalSeconds = (1/fSecondStep) * (m_networkDrawing.Width - 90);
            for( int i = 0; i < m_networkDrawing.Width-90; i+= 50)
            {
                float xPos = 90 + i;
                float fTime = fTotalSeconds - (((float)(i)) / fSecondStep);

                gfx.DrawLine(penLine, xPos, 0, xPos, m_networkDrawing.Height - 15);
                string strText = fTime.ToString("##0");
                gfx.DrawString(strText, SystemFonts.DialogFont, Brushes.CadetBlue,
                               xPos - 4 * strText.Length, m_networkDrawing.Height - 15);
            }


            float nXPos = m_networkDrawing.Width;
            float fHeightMul = m_networkDrawing.Height - 15;
            float fYStart = m_networkDrawing.Height - 15;
            TrafficHistory lastItem = null;

            float fHighestRate = 0;
            foreach(TrafficHistory item in m_trafficHistory) {
                if (lastItem != null)
                {
                    nXPos -= fSecondStep * 2;

                    gfx.DrawLine(penUdpIn, nXPos, fYStart - (item.inUdpBytes / fMax) * fHeightMul,
                                 nXPos + fSecondStep * 2, fYStart - (lastItem.inUdpBytes / fMax) * fHeightMul);
                    gfx.DrawLine(penUdpOut, nXPos, fYStart - (item.outUdpBytes / fMax) * fHeightMul,
                                 nXPos + fSecondStep * 2, fYStart - (lastItem.outUdpBytes / fMax) * fHeightMul);

                    gfx.DrawLine(penTcpIn, nXPos, fYStart - (item.inTcpBytes / fMax) * fHeightMul,
                                 nXPos + fSecondStep * 2, fYStart - (lastItem.inTcpBytes / fMax) * fHeightMul);
                    gfx.DrawLine(penTcpOut, nXPos, fYStart - (item.outTcpBytes / fMax) * fHeightMul,
                                 nXPos + fSecondStep * 2, fYStart - (lastItem.outTcpBytes / fMax) * fHeightMul);

                    gfx.DrawLine(penTotalIn, nXPos, fYStart - (item.inTotalBytes / fMax) * fHeightMul,
                                 nXPos + fSecondStep * 2, fYStart - (lastItem.inTotalBytes / fMax) * fHeightMul);
                    gfx.DrawLine(penTotalOut, nXPos, fYStart - (item.outTotalBytes / fMax) * fHeightMul,
                                 nXPos + fSecondStep * 2, fYStart - (lastItem.outTotalBytes / fMax) * fHeightMul);

                    gfx.DrawLine(penResent, nXPos, fYStart - (item.resentBytes / fMax) * fHeightMul,
                                 nXPos + fSecondStep * 2, fYStart - (lastItem.resentBytes / fMax) * fHeightMul);

                    if (nXPos < 0)
                        break;
                }
                lastItem = item;
                if (item.inTotalBytes > fHighestRate)
                    fHighestRate = item.inTotalBytes;
                if (item.outTotalBytes > fHighestRate)
                    fHighestRate = item.outTotalBytes;
            }

            for (int i = 0; i < m_networkDrawing.Height - 10; i += 30)
            {
                float yPos = m_networkDrawing.Height - i - 15;
                float fPos = ((float)(i)) / ((float)m_networkDrawing.Height);
                gfx.DrawString((fPos * fMax).ToString("##0.00") + " KB/s",
                               SystemFonts.DialogFont, Brushes.CadetBlue, 3, yPos);
            }

            int nPosX = m_networkDrawing.Width-60;
            gfx.DrawString("Udp in", SystemFonts.DialogFont, penUdpIn.Brush, nPosX, 10);
            gfx.DrawString("Udp out", SystemFonts.DialogFont, penUdpOut.Brush, nPosX, 22);
            gfx.DrawString("Tcp in", SystemFonts.DialogFont, penTcpIn.Brush, nPosX, 34);
            gfx.DrawString("Tcp out", SystemFonts.DialogFont, penTcpOut.Brush, nPosX, 46);
            gfx.DrawString("Total in", SystemFonts.DialogFont, penTotalIn.Brush, nPosX, 58);
            gfx.DrawString("Total out", SystemFonts.DialogFont, penTotalOut.Brush, nPosX, 70);
            gfx.DrawString("Resent", SystemFonts.DialogFont, penResent.Brush, nPosX, 82);

            screenGfx.DrawImageUnscaled(backBuffer, 0, 0);

            m_fNetworkHistoryScale = fHighestRate + 10;
        }

        protected void PaintMemoryHistory()
        {
            Pen penPagedMemory = Pens.Gray;
            Pen penPagedSystemMemory = Pens.Orange;
            Pen penNonPagedMemory = Pens.LawnGreen;
            Pen penWorkingSet = Pens.HotPink;
            Pen penGcReported = Pens.Red;
                    

            Pen penLine = Pens.DarkSlateGray;

            Graphics screenGfx = m_memStats.CreateGraphics();
            Bitmap backBuffer = new Bitmap(m_memStats.Width, m_memStats.Height);
            Graphics gfx = Graphics.FromImage(backBuffer);

            gfx.Clear(Color.Black);

            float fMax = m_fMemoryHistoryScale;
            if (fMax < 12.0f)
                fMax = 12.0f;
            for (int i = 0; i < m_memStats.Height - 10; i += 30)
            {
                float yPos = m_memStats.Height - i - 15;
                float fPos = ((float)(i)) / ((float)m_memStats.Height);
                gfx.DrawLine(penLine, 0, yPos, m_memStats.Width, yPos);
            }

            //Size of second in pixels
            float fSecondStep = 1.5f; //120 seconds
            float fTotalSeconds = (1 / fSecondStep) * (m_memStats.Width - 90);
            for (int i = 0; i < m_memStats.Width - 90; i += 50)
            {
                float xPos = 90 + i;
                float fTime = fTotalSeconds - (((float)(i)) / fSecondStep);

                gfx.DrawLine(penLine, xPos, 0, xPos, m_memStats.Height - 15);
                string strText = fTime.ToString("##0");
                gfx.DrawString(strText, SystemFonts.DialogFont, Brushes.CadetBlue,
                               xPos - 4 * strText.Length, m_memStats.Height - 15);
            }


            float nXPos = m_memStats.Width;
            float fHeightMul = m_memStats.Height - 15;
            float fYStart = m_memStats.Height - 15;
            MemoryHistory lastItem = null;

            float fHighestRate = 0;
            foreach (MemoryHistory item in m_memoryHistory)
            {
                if (lastItem != null)
                {

                    nXPos -= fSecondStep * 2;

                    gfx.DrawLine(penPagedMemory, nXPos, fYStart - (item.pagedMemory / fMax) * fHeightMul,
                                 nXPos + fSecondStep * 2, fYStart - (lastItem.pagedMemory / fMax) * fHeightMul);

                    gfx.DrawLine(penPagedSystemMemory, nXPos, fYStart - (item.pagedSystemMemory / fMax) * fHeightMul,
                                 nXPos + fSecondStep * 2, fYStart - (lastItem.pagedSystemMemory / fMax) * fHeightMul);

                    gfx.DrawLine(penNonPagedMemory, nXPos, fYStart - (item.nonpagedSystemMemory / fMax) * fHeightMul,
                                 nXPos + fSecondStep * 2, fYStart - (lastItem.nonpagedSystemMemory / fMax) * fHeightMul);

                    gfx.DrawLine(penWorkingSet, nXPos, fYStart - (item.workingSet / fMax) * fHeightMul,
                                 nXPos + fSecondStep * 2, fYStart - (lastItem.workingSet / fMax) * fHeightMul);

                    gfx.DrawLine(penGcReported, nXPos, fYStart - (item.gcReportedMem / fMax) * fHeightMul,
                                 nXPos + fSecondStep * 2, fYStart - (lastItem.gcReportedMem / fMax) * fHeightMul);
                    
                    if (nXPos < 0)
                        break;
                }

                lastItem = item;
                if (item.nonpagedSystemMemory > fHighestRate)
                    fHighestRate = item.nonpagedSystemMemory;
                if (item.pagedMemory > fHighestRate)
                    fHighestRate = item.pagedMemory;
                if (item.pagedSystemMemory > fHighestRate)
                    fHighestRate = item.pagedSystemMemory;
                if (item.workingSet > fHighestRate)
                    fHighestRate = item.workingSet;
                if (item.gcReportedMem > fHighestRate)
                    fHighestRate = item.gcReportedMem;
            }

            for (int i = 0; i < m_memStats.Height - 10; i += 30)
            {
                float yPos = m_memStats.Height - i - 15;
                float fPos = ((float)(i)) / ((float)m_memStats.Height);
                gfx.DrawString((fPos * fMax).ToString("##0.00") + " MB",
                               SystemFonts.DialogFont, Brushes.CadetBlue, 3, yPos);
            }

            int nPosX = m_memStats.Width - 120;

            gfx.DrawString("Working set", SystemFonts.DialogFont, penWorkingSet.Brush, nPosX, 10);
            gfx.DrawString("GC Reported mem.", SystemFonts.DialogFont, penGcReported.Brush, nPosX, 22);
            gfx.DrawString("Paged mem.", SystemFonts.DialogFont, penPagedMemory.Brush, nPosX, 34);
            gfx.DrawString("Paged system mem.", SystemFonts.DialogFont, penPagedSystemMemory.Brush, nPosX, 46);
            gfx.DrawString("Nonpaged mem.", SystemFonts.DialogFont, penNonPagedMemory.Brush, nPosX, 58);

            screenGfx.DrawImageUnscaled(backBuffer, 0, 0);

            m_fMemoryHistoryScale = fHighestRate + 10;
        }

        void RefreshNetworkStats(TrafficHistory item)
        {
            m_inUdpTraffic.Text = item.inUdpBytes.ToString("##0.00") + " KB/s";
            m_outUdpTraffic.Text = item.outUdpBytes.ToString("##0.00") + " KB/s";
            m_inTcpTraffic.Text = item.inTcpBytes.ToString("##0.00") + " KB/s";
            m_outTcpTraffic.Text = item.outTcpBytes.ToString("##0.00") + " KB/s";
            m_inTotalTraffic.Text = item.inTotalBytes.ToString("##0.00") + " KB/s";
            m_outTotalTraffic.Text = item.outTotalBytes.ToString("##0.00") + " KB/s";
        }

        protected void UpdateTimer(object sender, ElapsedEventArgs ea)
        {
            if (m_threads.InvokeRequired)
            {
                m_threads.Invoke(new UpdateControlsDelegate(UpdateControls));
            }
            else
            {
                UpdateControls();
            }
        }
        

        #region Thread items access
        void UpdateThreadItem(ProcessThread pt)
        {
            if (m_threadItems.ContainsKey(pt.Id))
            {
                ThreadItem item = m_threadItems[pt.Id];

                item.cpu.Text = pt.TotalProcessorTime.ToString();
                if (m_idToName.ContainsKey(pt.Id))
                {
                    item.name.Text = m_idToName[pt.Id];
                }
            }
            else
            {
                ThreadItem item = new ThreadItem();
                item.listItem = m_threads.Items.Add(pt.Id.ToString());
                string name = "[n/a]";
                if (m_idToName.ContainsKey(pt.Id))
                {
                    name = m_idToName[pt.Id];
                }
                item.name = item.listItem.SubItems.Add(name);
                item.cpu = item.listItem.SubItems.Add(pt.TotalProcessorTime.ToString());

                m_threadItems[pt.Id] = item;
            }
        }

        void RemoveThreadItems(List<int> removed)
        {
            foreach (int id in removed)
            {
                m_threads.Items.Remove(m_threadItems[id].listItem);
                m_threadItems.Remove(id);
                m_idToName.Remove(id);
            }
        }
        
        private void UpdateThreadList()
        {
            ProcessThreadCollection threads = Process.GetCurrentProcess().Threads;

            lock (m_threadItems)
            {
                Dictionary<int, int> runningThreads = new Dictionary<int, int>();

                foreach (ProcessThread pt in threads)
                {
                    runningThreads[pt.Id] = 0;

                    UpdateThreadItem(pt);
                }

                List<int> removed = new List<int>();
                foreach (int id in m_threadItems.Keys)
                {
                    if (!runningThreads.ContainsKey(id))
                    {
                        removed.Add(id);
                    }
                }

                RemoveThreadItems(removed);
            }
        }

        #endregion
    }
}
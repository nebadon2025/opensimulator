using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Web;

namespace OpenSim.Framework.ServerStatus
{
    public class ServerStatus
    {
        static StatusWindow m_window = null;
        static Thread m_thread = null;

        static public void ReportOutPacketUdp(int size, bool resent)
        {
            StatusWindow.ReportOutPacketUdp(size, resent);
        }

        static public void ReportInPacketUdp(int size)
        {
            StatusWindow.ReportInPacketUdp(size);
        }
        
        static public void ReportOutPacketTcp(int size)
        {
            StatusWindow.ReportOutPacketTcp(size);
        }

        static public void ReportInPacketTcp(int size)
        {
            StatusWindow.ReportInPacketTcp(size);
        }

        static public void ReportProcessedInPacket(string name, int size)
        {
            if (m_window != null)
                StatusWindow.ReportProcessedInPacket(name, size);
        }
        
        static public void ReportProcessedOutPacket(string name, int size, bool resent)
        {
            if (m_window != null)
                StatusWindow.ReportProcessedOutPacket(name, size, resent);
        }

        static public void ReportThreadName(string name)
        {
            StatusWindow.ReportThreadName(AppDomain.GetCurrentThreadId(), name);
        }

        static public void ShowWindow()
        {
            if (m_window == null)
            {
                m_window = new StatusWindow();

                m_thread = new Thread(new ThreadStart(run));
                m_thread.IsBackground = true;
                m_thread.Start();
            }
            else
            {
                m_window.Show();
            }
        }

        static void run()
        {
            ReportThreadName("ServerStatus");
            m_window.ShowDialog();
            m_window.CloseStatusWindow();
            m_window = null;
            m_thread = null;
        }
    }
}

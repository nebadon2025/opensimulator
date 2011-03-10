/* Copyright 2011 (c) Intel Corporation
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * The name of the copyright holder may not be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Timers;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    public interface ISyncStatistics
    {
        // return an identifier for this statistics source
        string StatisticIdentifier();
        // a line of comma separated values
        string StatisticLine(bool clearFlag);
        // a line of comma separated field descriptions (describes what StatisticLine returns)
        string StatisticTitle();
    }

    public class SyncStatisticCollector
    {
        public static bool LogEnabled = false;
        public static string LogDirectory = ".";
        public static int LogInterval = 5000;
        public static int LogMaxFileTimeMin = 5;    // 5 minutes
        public static string LogFileHeader = "stats-";

        private static List<ISyncStatistics> s_staters = new List<ISyncStatistics>();
        private static object s_statersLock = new object();
        private static Timer s_timer = null;

        static SyncStatisticCollector()
        {
        }

        public SyncStatisticCollector()
        {
        }

        /// <summary>
        /// Any implementor of ISyncStatistics will call Register to put themselves in
        /// the list of routines to collect statistics from. This will run periodically
        /// and suck statistics from the registered routines.
        /// </summary>
        /// <param name="stat"></param>
        public static void Register(ISyncStatistics stat)
        {
            if (!LogEnabled) return;
            lock (SyncStatisticCollector.s_statersLock)
            {
                // set up logging timer
                if (SyncStatisticCollector.s_timer == null)
                {
                    SyncStatisticCollector.s_timer = new Timer();
                    SyncStatisticCollector.s_timer.Interval = LogInterval;
                    SyncStatisticCollector.s_timer.Enabled = true;
                    SyncStatisticCollector.s_timer.Elapsed += Tick;

                }
                SyncStatisticCollector.s_staters.Add(stat);
            }
            return;
        }

        public static void Close()
        {
            lock (SyncStatisticCollector.s_statersLock)
            {
                SyncStatisticCollector.LogEnabled = false;
                if (SyncStatisticCollector.s_timer != null)
                {
                    SyncStatisticCollector.s_timer.Enabled = false;
                    SyncStatisticCollector.s_timer.Dispose();
                    SyncStatisticCollector.s_timer = null;
                }
                if (SyncStatisticCollector.LogFile != null)
                {
                    SyncStatisticCollector.LogFile.Close();
                    SyncStatisticCollector.LogFile.Dispose();
                    SyncStatisticCollector.LogFile = null;
                }
            }
        }


        private static void Tick(object sender, EventArgs e)
        {
            if (!LogEnabled) return;
            lock (SyncStatisticCollector.s_statersLock)
            {
                foreach (ISyncStatistics iss in s_staters)
                {
                    LogWriter(iss.StatisticIdentifier() + "," + iss.StatisticLine(true));
                }
            }
            return;
        }

        private static DateTime LogStartTime;
        private static System.IO.TextWriter LogFile = null;
        private static void LogWriter(string line)
        {
            try
            {
                DateTime now = DateTime.Now;
                if (LogFile == null || (now > (LogStartTime + new TimeSpan(0, LogMaxFileTimeMin, 0))))
                {
                    if (LogFile != null)
                    {
                        LogFile.Close();
                        LogFile.Dispose();
                        LogFile = null;
                    }

                    // First log file or time has expired, start writing to a new log file
                    LogStartTime = now;
                    string path = (LogDirectory.Length > 0 ? LogDirectory 
                                + System.IO.Path.DirectorySeparatorChar.ToString() : "")
                            + String.Format("{0}{1}.log", LogFileHeader, now.ToString("yyyyMMddHHmmss"));
                    LogFile = new StreamWriter(File.Open(path, FileMode.Append, FileAccess.Write));
                }
                if (LogFile != null)
                {
                    StringBuilder buff = new StringBuilder();
                    // buff.Append(now.ToString("yyyyMMddHHmmssfff"));
                    buff.Append(now.ToString("yyyyMMddHHmmss"));
                    buff.Append(",");
                    buff.Append(line);
                    buff.Append("\r\n");
                    LogFile.Write(buff.ToString());
                }
            }
            catch (Exception e)
            {
                // m_log.ErrorFormat("{0}: FAILURE WRITING TO LOGFILE: {1}", LogHeader, e);
                LogEnabled = false;
            }
            return;
        }
    }
}

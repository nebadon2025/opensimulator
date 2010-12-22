/*
 * Copyright (c) Contributors: TO BE FILLED
 */

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using log4net;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    public class SyncConnector
    {
        private TcpClient m_tcpConnection = null;
        private RegionSyncListenerInfo m_remoteListenerInfo = null;
        private Thread m_rcvLoop;
        private string LogHeader = "[SYNC CONNECTOR]";
        // The logfile
        private ILog m_log;

        private int m_connectorNum;
        public int ConnectorNum
        {
            get { return m_connectorNum; }
        }

        public SyncConnector(int connectorNum, TcpClient tcpclient)
        {
            m_tcpConnection = tcpclient;
            m_connectorNum = connectorNum;
            m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        }

        public SyncConnector(int connectorNum, RegionSyncListenerInfo listenerInfo)
        {
            m_remoteListenerInfo = listenerInfo;
            m_connectorNum = connectorNum;
            m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        }

        //Start the connection
        public bool Start()
        {
            m_tcpConnection = new TcpClient();
            try
            {
                m_tcpConnection.Connect(m_remoteListenerInfo.Addr, m_remoteListenerInfo.Port);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("{0} [Start] Could not connect to RegionSyncServer at {1}:{2}", LogHeader, m_remoteListenerInfo.Addr, m_remoteListenerInfo.Port);
                m_log.Warn(e.Message);
                return false;
            }

            m_rcvLoop = new Thread(new ThreadStart(ReceiveLoop));
            m_rcvLoop.Name = "SyncConnector ReceiveLoop";
            m_log.WarnFormat("{0} Starting {1} thread", LogHeader, m_rcvLoop.Name);
            m_rcvLoop.Start();

            return true;
        }

        public void Stop()
        {
            // The remote scene will remove our avatars automatically when we disconnect
            //m_rcvLoop.Abort();

            // Close the connection
            m_tcpConnection.Client.Close();
            m_tcpConnection.Close();
        }

        // *** This is the main thread loop for each sync connection
        private void ReceiveLoop()
        {
            m_log.WarnFormat("{0} Thread running: {1}", LogHeader, m_rcvLoop.Name);
            while (true && m_tcpConnection.Connected)
            {
                RegionSyncMessage msg;
                // Try to get the message from the network stream
                try
                {
                    msg = new RegionSyncMessage(m_tcpConnection.GetStream());
                    //m_log.WarnFormat("{0} Received: {1}", LogHeader, msg.ToString());
                }
                // If there is a problem reading from the client, shut 'er down. 
                catch
                {
                    //ShutdownClient();
                    Stop();
                    return;
                }
                // Try handling the message
                try
                {
                    HandleMessage(msg);
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("{0} Encountered an exception: {1} (MSGTYPE = {2})", LogHeader, e.Message, msg.ToString());
                }
            }
        }

        private void HandleMessage(RegionSyncMessage msg)
        {

        }
    }
}
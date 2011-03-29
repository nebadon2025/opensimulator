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
using System.IO;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using log4net;
using OpenMetaverse;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    // For implementations, a lot was copied from RegionSyncClientView, especially the SendLoop/ReceiveLoop.
    public class SyncConnector : ISyncStatistics
    {
        private TcpClient m_tcpConnection = null;
        private RegionSyncListenerInfo m_remoteListenerInfo = null;
        private Thread m_rcvLoop;
        private Thread m_send_loop;

        private string LogHeader = "[SYNC CONNECTOR]";
        // The logfile
        private ILog m_log;

        //members for in/out messages queueing
        object stats = new object();
        private long queuedUpdates=0;
        private long dequeuedUpdates=0;
        private long msgsIn=0;
        private long msgsOut=0;
        private long bytesIn=0;
        private long bytesOut=0;
        private DateTime lastStatTime;
        // A queue for outgoing traffic. 
        private BlockingUpdateQueue m_outQ = new BlockingUpdateQueue();

        private RegionSyncModule m_regionSyncModule = null;

        // unique connector number across all regions
        private static int m_connectorNum = 0;
        public int ConnectorNum
        {
            get { return m_connectorNum; }
        }

        //the actorID of the other end of the connection
        private string m_syncOtherSideActorID;
        public string OtherSideActorID
        {
            get { return m_syncOtherSideActorID; }
            set { m_syncOtherSideActorID = value; }
        }

        //The region name of the other side of the connection
        private string m_syncOtherSideRegionName="";
        public string OtherSideRegionName
        {
            get { return m_syncOtherSideRegionName; }
        }

        // Check if the client is connected
        public bool Connected
        { get { return (m_tcpConnection !=null && m_tcpConnection.Connected); } }

        public string Description
        {
            get
            {
                if (m_syncOtherSideRegionName == null)
                    return String.Format("SyncConnector{0}", m_connectorNum);
                return String.Format("SyncConnector{0}({2}/{1:10})",
                            m_connectorNum, m_syncOtherSideRegionName, m_syncOtherSideActorID);
            }
        }

        /// <summary>
        /// The constructor that will be called when a SyncConnector is created passively: a remote SyncConnector has initiated the connection.
        /// </summary>
        /// <param name="connectorNum"></param>
        /// <param name="tcpclient"></param>
        public SyncConnector(int connectorNum, TcpClient tcpclient, RegionSyncModule syncModule)
        {
            m_tcpConnection = tcpclient;
            m_connectorNum = connectorNum;
            m_regionSyncModule = syncModule;
            lastStatTime = DateTime.Now;
            SyncStatisticCollector.Register(this);
            m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        }

        /// <summary>
        /// The constructor that will be called when a SyncConnector is created actively: it is created to send connection request to a remote listener
        /// </summary>
        /// <param name="connectorNum"></param>
        /// <param name="listenerInfo"></param>
        public SyncConnector(int connectorNum, RegionSyncListenerInfo listenerInfo, RegionSyncModule syncModule)
        {
            m_remoteListenerInfo = listenerInfo;
            m_connectorNum = connectorNum;
            m_regionSyncModule = syncModule;
            lastStatTime = DateTime.Now;
            SyncStatisticCollector.Register(this);
            m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        }

        //Connect to the remote listener
        public bool Connect()
        {
            m_tcpConnection = new TcpClient();
            try
            {
                m_tcpConnection.Connect(m_remoteListenerInfo.Addr, m_remoteListenerInfo.Port);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("{0} [Start] Could not connect to RegionSyncModule at {1}:{2}", LogHeader, m_remoteListenerInfo.Addr, m_remoteListenerInfo.Port);
                m_log.Warn(e.Message);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Start both the send and receive threads
        /// </summary>
        public void StartCommThreads()
        {
            // Create a thread for the receive loop
            m_rcvLoop = new Thread(new ThreadStart(ReceiveLoop));
            m_rcvLoop.Name = Description + " (ReceiveLoop)";
            m_log.WarnFormat("{0} Starting {1} thread", Description, m_rcvLoop.Name);
            m_rcvLoop.Start();

            // Create a thread for the send loop
            m_send_loop = new Thread(new ThreadStart(delegate() { SendLoop(); }));
            m_send_loop.Name = Description + " (SendLoop)";
            m_log.WarnFormat("{0} Starting {1} thread", Description, m_send_loop.Name);
            m_send_loop.Start();
        }

        public void Shutdown()
        {
            m_log.Warn(LogHeader + " shutdown connection");
            // Abort receive and send loop
            m_rcvLoop.Abort();
            m_send_loop.Abort();

            // Close the connection
            m_tcpConnection.Client.Close();
            m_tcpConnection.Close();
        }

        ///////////////////////////////////////////////////////////
        // Sending messages out to the other side of the connection
        ///////////////////////////////////////////////////////////
        // Send messages from the update Q as fast as we can DeQueue them
        // *** This is the main send loop thread for each connected client
        private void SendLoop()
        {
            try
            {
                while (true)
                {
                    // Dequeue is thread safe
                    byte[] update = m_outQ.Dequeue();
                    lock (stats)
                        dequeuedUpdates++;
                    Send(update);
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("{0} has disconnected: {1} (SendLoop)", Description, e.Message);
            }
            Shutdown();
        }

        /// <summary>
        /// Enqueue update of an object/avatar into the outgoing queue, and return right away
        /// </summary>
        /// <param name="id">UUID of the object/avatar</param>
        /// <param name="update">the update infomation in byte format</param>
        public void EnqueueOutgoingUpdate(UUID id, byte[] update)
        {
            lock (stats)
                queuedUpdates++;
            // Enqueue is thread safe
            m_outQ.Enqueue(id, update);
        }

        //Send out a messge directly. This should only by called for short messages that are not sent frequently.
        //Don't call this function for sending out updates. Call EnqueueOutgoingUpdate instead
        public void Send(SymmetricSyncMessage msg)
        {
            Send(msg.ToBytes());
        }

        private void Send(byte[] data)
        {
            if (m_tcpConnection.Connected)
            {
                try
                {
                    lock (stats)
                    {
                        msgsOut++;
                        bytesOut += data.Length;
                    }
                    m_tcpConnection.GetStream().BeginWrite(data, 0, data.Length, ar =>
                    {
                        if (m_tcpConnection.Connected)
                        {
                            try
                            {
                                m_tcpConnection.GetStream().EndWrite(ar);
                            }
                            catch (Exception)
                            { }
                        }
                    }, null);
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("{0}:Error in Send() {1} has disconnected -- error message: {2}.", Description, m_connectorNum, e.Message);
                }
            }
        }

        ///////////////////////////////////////////////////////////
        // Receiving messages from the other side ofthe connection
        ///////////////////////////////////////////////////////////
        private void ReceiveLoop()
        {
            m_log.WarnFormat("{0} Thread running: {1}", LogHeader, m_rcvLoop.Name);
            while (true && m_tcpConnection.Connected)
            {
                SymmetricSyncMessage msg;
                // Try to get the message from the network stream
                try
                {
                    msg = new SymmetricSyncMessage(m_tcpConnection.GetStream());
                    //m_log.WarnFormat("{0} Received: {1}", LogHeader, msg.ToString());
                }
                // If there is a problem reading from the client, shut 'er down. 
                catch (Exception e)
                {
                    //ShutdownClient();
                    m_log.WarnFormat("{0}: ReceiveLoop error {1} has disconnected -- error message {2}.", Description, m_connectorNum, e.Message);
                    Shutdown();
                    return;
                }
                // Try handling the message
                try
                {
                    HandleMessage(msg);
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("{0} Encountered an exception: {1} (MSGTYPE = {2})", Description, e.Message, msg.ToString());
                }
            }
            }

        private void HandleMessage(SymmetricSyncMessage msg)
        {

            msgsIn++;
            bytesIn += msg.Data.Length;
            switch (msg.Type)
            {
                case SymmetricSyncMessage.MsgType.RegionName:
                    {
                        m_syncOtherSideRegionName = Encoding.ASCII.GetString(msg.Data, 0, msg.Length);
                        if (m_regionSyncModule.IsSyncRelay)
                        {
                            SymmetricSyncMessage outMsg = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.RegionName, m_regionSyncModule.LocalScene.RegionInfo.RegionName);
                            Send(outMsg);
                        }
                        m_log.DebugFormat("Syncing to region \"{0}\"", m_syncOtherSideRegionName); 
                        return;
                    }
                case SymmetricSyncMessage.MsgType.ActorID:
                    {
                        m_syncOtherSideActorID = Encoding.ASCII.GetString(msg.Data, 0, msg.Length);
                        if (m_regionSyncModule.IsSyncRelay)
                        {
                            SymmetricSyncMessage outMsg = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.ActorID, m_regionSyncModule.ActorID);
                            Send(outMsg);
                        }
                        m_log.DebugFormat("Syncing to actor \"{0}\"", m_syncOtherSideActorID);
                        return;
                    }
                default:
                    break;
            }

            //For any other messages, we simply deliver the message to RegionSyncModule for now.
            //Later on, we may deliver messages to different modules, say sync message to RegionSyncModule and event message to ActorSyncModule.
            m_regionSyncModule.HandleIncomingMessage(msg, m_syncOtherSideActorID, this);
        }

        public string StatisticIdentifier()
        {
            return this.Description;
        }

        public string StatisticLine(bool clearFlag)
        {
            string statLine = "";
            lock (stats)
            {
                double secondsSinceLastStats = DateTime.Now.Subtract(lastStatTime).TotalSeconds;
                lastStatTime = DateTime.Now;
                statLine = String.Format("{0},{1},{2},{3},{4},{5},{6}",
                        msgsIn, msgsOut, bytesIn, bytesOut, m_outQ.Count,
                        8 * (bytesIn / secondsSinceLastStats / 1000000),
                        8 * (bytesOut / secondsSinceLastStats / 1000000) );
                if (clearFlag)
                    msgsIn = msgsOut = bytesIn = bytesOut = 0;
            }
            return statLine;
        }

        public string StatisticTitle()
        {
            return "msgsIn,msgsOut,bytesIn,bytesOut,queueSize,Mbps In,Mbps Out";
        }
    }
}
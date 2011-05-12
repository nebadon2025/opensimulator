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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenMetaverse;
using log4net;
using OpenSim.Region.Framework.Scenes.Serialization;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    // The RegionSyncServer has a listener thread which accepts connections from RegionSyncClients
    // and an additional thread to process updates to/from each RegionSyncClient.
    public class RegionSyncServer
    {
        #region RegionSyncServer members
        // Set the addr and port for TcpListener
        private IPAddress m_addr;
        private Int32 m_port;

        private int clientCounter;
 
        // The local scene.
        private Scene m_scene;
        private ILog m_log;

        // The listener and the thread which listens for connections from client managers
        private TcpListener m_listener;
        private Thread m_listenerThread;

        private DSGClientManagerLoadBalancer m_ClientBalancer;

        // Check if any of the client views are in a connected state
        public bool Synced
        {
            get
            {
                return (m_ClientBalancer.Count > 0);
            }
        }

        private string LogHeader()
        {
            return String.Format("[REGION SYNC SERVER ({0})]", m_scene.RegionInfo.RegionName);
        }

        public void ReportStats(System.IO.TextWriter tw)
        {
            tw.WriteLine("{0}: {1}                      TOTAL        LOCAL        REMOTE               TO_SCENE                              FROM_SCENE", DateTime.Now.ToLongTimeString(), LogHeader());
            tw.WriteLine("{0}: {1}                                                          MSGS  ( /s )    BYTES    (  Mbps  )   MSGS  ( /s )    BYTES    (  Mbps  )     QUEUE", DateTime.Now.ToLongTimeString(), LogHeader());
            m_ClientBalancer.ForEachClientManager(delegate(RegionSyncClientView rscv) {
            {
                tw.WriteLine("{0}: [{1}] {2}", DateTime.Now.ToLongTimeString(), rscv.Description, rscv.GetStats());
            }
            });
            tw.Flush();
        }

        public void ReportStatus()
        {
            int cvcount = m_ClientBalancer.Count;
            m_log.ErrorFormat("{0} Connected to {1} remote client managers", LogHeader(), cvcount);
            m_log.ErrorFormat("{0} Local scene contains {1} presences", LogHeader(), m_scene.SceneGraph.GetRootAgentCount());
            m_ClientBalancer.ForEachClientManager(delegate(RegionSyncClientView rscv){rscv.ReportStatus();});
        }


        #endregion

        // Constructor
        public RegionSyncServer(Scene scene, string addr, int port, int maxClientsPerManager)
        {
            m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            m_scene = scene;
            m_addr = IPAddress.Parse(addr);
            m_port = port;
            m_ClientBalancer = new DSGClientManagerLoadBalancer(maxClientsPerManager, scene);
        }

        // Start the server
        public void Start()
        {
            m_listenerThread = new Thread(new ThreadStart(Listen));
            m_listenerThread.Name = "RegionSyncServer Listener";
            m_log.WarnFormat("{0} Starting {1} thread", LogHeader(), m_listenerThread.Name);
            m_listenerThread.Start();
        }



        // Stop the server and disconnect all RegionSyncClients
        public void Shutdown()
        {
            // Stop the listener and listening thread so no new clients are accepted
            m_listener.Stop();
            m_listenerThread.Abort();
            m_listenerThread = null;

            m_ClientBalancer.ForEachClientManager(delegate(RegionSyncClientView rscv)
            {
                // Each client view will clean up after itself
                rscv.Shutdown();
                m_ClientBalancer.RemoveSyncedClient(rscv);
            });
        }

        // Listen for connections from a new RegionSyncClient
        // When connected, start the ReceiveLoop for the new client
        private void Listen()
        {
            m_listener = new TcpListener(m_addr, m_port);

            try
            {
                // Start listening for clients
                m_listener.Start();
                while (true)
                {
                    // *** Move/Add TRY/CATCH to here, but we don't want to spin loop on the same error
                    m_log.WarnFormat("{0} Listening for new connections on {1}:{2}...", LogHeader(), m_addr.ToString(), m_port.ToString());
                    TcpClient tcpclient = m_listener.AcceptTcpClient();
                    IPAddress addr = ((IPEndPoint)tcpclient.Client.RemoteEndPoint).Address;
                    int port = ((IPEndPoint)tcpclient.Client.RemoteEndPoint).Port;
                    // Add the RegionSyncClientView to the list of clients
                    // *** Need to work on the timing order of starting the client view and adding to the server list
                    // so that messages coming from the scene do not get lost before the client view is added but
                    // not sent before it is ready to process them.
                    RegionSyncClientView rscv = new RegionSyncClientView(++clientCounter, m_scene, tcpclient);
                    m_log.WarnFormat("{0} New connection from {1}", LogHeader(), rscv.Description);
                    m_ClientBalancer.AddSyncedClient(rscv);
                }
            }
            catch (SocketException e)
            {
                m_log.WarnFormat("{0} [Listen] SocketException: {1}", LogHeader(), e);
            }
        }

        // Broadcast a message to all connected RegionSyncClients
        public void Broadcast(RegionSyncMessage msg)
        {
            List<RegionSyncClientView> closed = null;
            m_ClientBalancer.ForEachClientManager(delegate(RegionSyncClientView rscv)
            {
                // If connected, send the message.
                if (rscv.Connected)
                {
                    rscv.Send(msg);
                }
                // Else, remove the client view from the list
                else
                {
                    if (closed == null)
                        closed = new List<RegionSyncClientView>();
                    closed.Add(rscv);
                }
            });
            if (closed != null)
            {
                foreach (RegionSyncClientView rscv in closed)
                    m_ClientBalancer.RemoveSyncedClient(rscv);
            }
        }

        // Broadcast a message to all connected RegionSyncClients
        public void EnqueuePresenceUpdate(UUID id, byte[] update)
        {
            List<RegionSyncClientView> closed = null;
            m_ClientBalancer.ForEachClientManager(delegate(RegionSyncClientView rscv)
            {
                // If connected, send the message.
                if (rscv.Connected)
                {
                    rscv.EnqueuePresenceUpdate(id, update);
                }
                // Else, remove the client view from the list
                else
                {
                    if (closed == null)
                        closed = new List<RegionSyncClientView>();
                    closed.Add(rscv);
                }
            });
            if (closed != null)
            {
                foreach (RegionSyncClientView rscv in closed)
                    m_ClientBalancer.RemoveSyncedClient(rscv);
            }
        }

        public void BalanceClients()
        {
            m_ClientBalancer.BalanceLoad();
        }
        //KittyL: 
        public void BroadcastToCM(RegionSyncMessage.MsgType msgType, SceneObjectGroup sog)
        {
            string sogxml = SceneObjectSerializer.ToXml2Format(sog);

            //m_log.Debug("SOG " + sog.UUID);

            RegionSyncMessage rsm = new RegionSyncMessage(msgType, sogxml);
            Broadcast(rsm);
        }

    }
}

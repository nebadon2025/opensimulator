using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using log4net;

namespace OpenSim.Region.Examples.RegionSyncModule
{
    // The RegionSyncServer has a listener thread which accepts connections from RegionSyncClients
    // and an additional thread to process updates to/from each RegionSyncClient.
    public class RegionSyncServer
    {
        #region RegionSyncServer members
        // Set the addr and port for TcpListener
        private IPAddress m_addr;
        private Int32 m_port;
 
        // The local scene.
        private Scene m_scene;

        // A queue for incoming and outgoing traffic
        // Incoming stuff can be from any client
        // Outgoing stuff will be multicast to all clients
        private Queue<string> m_inQ = new Queue<string>();
        private Queue<string> m_outQ = new Queue<string>();

        private ILog m_log;

        // The listener and the thread which listens for connections from client managers
        private TcpListener m_listener;
        private Thread m_listenerThread;

        // The list of clients and the threads handling IO for each client
        // Lock should be used when client managers connect or disconnect
        // while modifying the client list.
        private Object clientLock = new Object();
        private List<RegionSyncClientView> m_client_views = new List<RegionSyncClientView>();

        // Check if any of the client views are in a connected state
        public bool Synced
        {
            get
            {
                lock (clientLock)
                {
                    foreach (RegionSyncClientView cv in m_client_views)
                    {
                        if (cv.Connected)
                            return true;
                    }
                    return false;
                }
            }
        }
        #endregion

        // Constructor
        public RegionSyncServer(Scene scene, string addr, int port)
        {
            m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            m_log.Warn("[REGION SYNC SERVER] Constructed");
            m_scene = scene;
            m_addr = IPAddress.Parse(addr);
            m_port = port;
        }

        // Start the server
        public void Start()
        {
            m_listenerThread = new Thread(new ThreadStart(Listen));
            m_listenerThread.Name = "RegionSyncServer Listener";
            m_log.WarnFormat("[REGION SYNC SERVER] Starting {0} thread", m_listenerThread.Name);
            m_listenerThread.Start();
            m_log.Warn("[REGION SYNC SERVER] Started");
        }

        // Stop the server and disconnect all RegionSyncClients
        public void Shutdown()
        {
            lock (clientLock)
            {
                // Stop the listener and listening thread so no new clients are accepted
                m_listener.Stop();
                m_listenerThread.Abort();
                m_listenerThread = null;
                // Stop all existing client views and clear client list
                foreach (RegionSyncClientView cv in m_client_views)
                {
                    // Each client view will clean up after itself
                    cv.Shutdown();
                }
                m_client_views.Clear();
            }
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
                m_log.WarnFormat("[REGION SYNC SERVER] Listening on port {0}", m_port.ToString());

                while (true)
                {
                    // *** Move/Add TRY/CATCH to here, but we don't want to spin loop on the same error
                    m_log.Warn("[REGION SYNC SERVER] Waiting for a connection...");
                    TcpClient tcpclient = m_listener.AcceptTcpClient();
                    IPAddress addr = ((IPEndPoint)tcpclient.Client.RemoteEndPoint).Address;
                    int port = ((IPEndPoint)tcpclient.Client.RemoteEndPoint).Port;
                    lock (clientLock)
                    {
                        // Add the RegionSyncClientView to the list of clients
                        // *** Need to work on the timing order of starting the client view and adding to the server list
                        // so that messages coming from the scene do not get lost before the client view is added but
                        // not sent before it is ready to process them.
                        RegionSyncClientView rscv = new RegionSyncClientView(m_client_views.Count, m_scene, tcpclient);
                        m_log.WarnFormat("[REGION SYNC SERVER] New connection from {0}", rscv.Description);
                        m_client_views.Add(rscv);
                    }
                }
            }
            catch (SocketException e)
            {
                m_log.WarnFormat("[REGION SYNC SERVER] [Listen] SocketException: {0}", e);
            }
        }

        // Broadcast a message to all connected RegionSyncClients
        public void Broadcast(RegionSyncMessage msg)
        {
            List<RegionSyncClientView> clients = new List<RegionSyncClientView>();
            lock (clientLock)
            {
                foreach (RegionSyncClientView client in m_client_views)
                {
                    if (client.Connected)
                        clients.Add(client);
                }
            }

            if(clients.Count > 0 )
            {
                //m_log.WarnFormat("[REGION SYNC SERVER] Broadcasting {0} to all connected RegionSyncClients", msg.ToString());
                foreach( RegionSyncClientView client in clients)
                {
                    client.Send(msg);
                }
            }
        }
    }
}

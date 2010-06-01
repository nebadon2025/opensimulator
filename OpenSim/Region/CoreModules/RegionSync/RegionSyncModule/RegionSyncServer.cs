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

        private int clientCounter;
 
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
        // The list is read most of the time and only updated when a new client manager
        // connects, so we just replace the list when it changes. Iterators on this
        // list need to be able to handle if an element is shutting down.
        private object m_clientview_lock = new object();
        private HashSet<RegionSyncClientView> m_client_views = new HashSet<RegionSyncClientView>();

        // Check if any of the client views are in a connected state
        public bool Synced
        {
            get
            {
                return (m_client_views.Count > 0);
            }
        }

        // Add the client view to the list and increment synced client counter
        public void AddSyncedClient(RegionSyncClientView rscv)
        {
            lock (m_clientview_lock)
            {
                HashSet<RegionSyncClientView> currentlist = m_client_views;
                HashSet<RegionSyncClientView> newlist = new HashSet<RegionSyncClientView>(currentlist);
                newlist.Add(rscv);
                // Threads holding the previous version of the list can keep using it since
                // they will not hold it for long and get a new copy next time they need to iterate
                m_client_views = newlist;
            }
        }

        // Remove the client view from the list and decrement synced client counter
        public void RemoveSyncedClient(RegionSyncClientView rscv)
        {
            lock (m_clientview_lock)
            {
                HashSet<RegionSyncClientView> currentlist = m_client_views;
                HashSet<RegionSyncClientView> newlist = new HashSet<RegionSyncClientView>(currentlist);
                newlist.Remove(rscv);
                // Threads holding the previous version of the list can keep using it since
                // they will not hold it for long and get a new copy next time they need to iterate
                m_client_views = newlist;
            }
        }

        public void ReportStats()
        {
            // We should be able to safely iterate over our reference to the list since
            // the only places which change it will replace it with an updated version
            m_log.Error("SERVER, MSGIN, MSGOUT, BYTESIN, BYTESOUT");
            foreach (RegionSyncClientView rscv in m_client_views)
            {
                m_log.ErrorFormat("{0}: {1}", rscv.Description, rscv.GetStats());
            }
        }

        #endregion

        // Constructor
        public RegionSyncServer(Scene scene, string addr, int port)
        {
            m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            //m_log.Warn("[REGION SYNC SERVER] Constructed");
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
            //m_log.Warn("[REGION SYNC SERVER] Started");
        }

        // Stop the server and disconnect all RegionSyncClients
        public void Shutdown()
        {
            // Stop the listener and listening thread so no new clients are accepted
            m_listener.Stop();
            m_listenerThread.Abort();
            m_listenerThread = null;

            // Stop all existing client views and clear client list
            HashSet<RegionSyncClientView> list = new HashSet<RegionSyncClientView>(m_client_views);
            foreach (RegionSyncClientView rscv in list)
            {
                // Each client view will clean up after itself
                rscv.Shutdown();
                RemoveSyncedClient(rscv);
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
                while (true)
                {
                    // *** Move/Add TRY/CATCH to here, but we don't want to spin loop on the same error
                    m_log.WarnFormat("[REGION SYNC SERVER] Listening for new connections on port {0}...", m_port.ToString());
                    TcpClient tcpclient = m_listener.AcceptTcpClient();
                    IPAddress addr = ((IPEndPoint)tcpclient.Client.RemoteEndPoint).Address;
                    int port = ((IPEndPoint)tcpclient.Client.RemoteEndPoint).Port;
                    // Add the RegionSyncClientView to the list of clients
                    // *** Need to work on the timing order of starting the client view and adding to the server list
                    // so that messages coming from the scene do not get lost before the client view is added but
                    // not sent before it is ready to process them.
                    RegionSyncClientView rscv = new RegionSyncClientView(++clientCounter, m_scene, tcpclient);
                    m_log.WarnFormat("[REGION SYNC SERVER] New connection from {0}", rscv.Description);
                    AddSyncedClient(rscv);
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
            List<RegionSyncClientView> closed = null;
            foreach (RegionSyncClientView client in m_client_views)
            {
                // If connected, send the message.
                if (client.Connected)
                {
                    client.Send(msg);
                }
                // Else, remove the client view from the list
                else
                {
                    if (closed == null)
                        closed = new List<RegionSyncClientView>();
                    closed.Add(client);
                }
            }
            if (closed != null)
            {
                foreach (RegionSyncClientView rscv in closed)
                    RemoveSyncedClient(rscv);
                    //m_client_views.Remove(rscv);
            }
        }
    }
}

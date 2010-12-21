/*
 * Copyright (c) Contributors: TO BE FILLED
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Region.CoreModules.Framework.InterfaceCommander;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using log4net;
using System.Net;
using System.Net.Sockets;
using System.Threading;

/////////////////////////////////////////////////////////////////////////////////////////////
//KittyL: created 12/17/2010, to start DSG Symmetric Synch implementation
/////////////////////////////////////////////////////////////////////////////////////////////
namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{

    //The connector that connects the local Scene (cache) and remote authoratative Scene
    public class RegionSyncModule : INonSharedRegionModule, IRegionSyncModule, ICommandableModule
    {
        #region INonSharedRegionModule

        public void Initialise(IConfigSource config)
        {
            m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            IConfig syncConfig = config.Configs["RegionSyncModule"];
            m_active = false;
            if (syncConfig == null)
            {
                m_log.Warn("[REGION SYNC MODULE] No RegionSyncModule config section found. Shutting down.");
                return;
            }
            else if (!syncConfig.GetBoolean("Enabled", false))
            {
                m_log.Warn("[REGION SYNC MODULE] RegionSyncModule is not enabled. Shutting down.");
                return;
            }

            m_actorID = syncConfig.GetString("ActorID", "");
            if (m_actorID == "")
            {
                m_log.Error("ActorID not defined in [RegionSyncModule] section in config file");
                return;
            }

            m_isSyncRelay = syncConfig.GetBoolean("IsSyncRelay", false);

            m_isSyncListenerLocal = syncConfig.GetBoolean("IsSyncListenerLocal", false);
            string listenerAddrDefault = syncConfig.GetString("ServerIPAddress", "127.0.0.1");
            m_syncListenerAddr = syncConfig.GetString("SyncListenerIPAddress", listenerAddrDefault);
            m_syncListenerPort = syncConfig.GetInt("SyncListenerPort", 13000);

            m_active = true;

            m_log.Warn("[REGION SYNC MODULE] Initialised for actor "+ m_actorID);
        }

        //Called after Initialise()
        public void AddRegion(Scene scene)
        {
            if (!m_active)
                return;

            //connect with scene
            m_scene = scene;

            //register the module
            m_scene.RegisterModuleInterface<IRegionSyncModule>(this);

            // Setup the command line interface
            m_scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
            InstallInterfaces();
        }

        //Called after AddRegion() has been called for all region modules of the scene
        public void RegionLoaded(Scene scene)
        {
            //If this one is configured to start a listener so that other actors can connect to form a overlay, start the listener.
            //For now, we use start topology, and ScenePersistence actor is always the one to start the listener.
            if (m_isSyncListenerLocal)
            {
                StartSyncListener();
            }
            
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void Close()
        {
            m_scene = null;
        }

        public string Name
        {
            get { return "RegionSyncModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion //INonSharedRegionModule

        #region IRegionSyncModule

        ///////////////////////////////////////////////////////////////////////////////////////////////////
        // Synchronization related members and functions, exposed through IRegionSyncModule interface
        ///////////////////////////////////////////////////////////////////////////////////////////////////

        private DSGActorTypes m_actorType;
        public DSGActorTypes DSGActorType
        {
            get { return m_actorType; }
        }

        private string m_actorID;
        public string ActorID
        {
            get { return m_actorID; }
        }

        private bool m_active = false;
        public bool Active
        {
            get { return m_active; }
        }

        private bool m_isSyncRelay = false;
        public bool IsSyncRelay
        {
            get { return m_isSyncRelay; }
        }

        private RegionSyncListener m_regionSyncListener = null;

        public void SendObjectUpdates(List<SceneObjectGroup> sog)
        {

        }

        #endregion //IRegionSyncModule  

        #region ICommandableModule Members
        private readonly Commander m_commander = new Commander("sync");
        public ICommander CommandInterface
        {
            get { return m_commander; }
        }
        #endregion

        #region Console Command Interface
        private void InstallInterfaces()
        {
            Command cmdSyncStart = new Command("start", CommandIntentions.COMMAND_HAZARDOUS, SyncStart, "Begins synchronization with RegionSyncServer.");
            //cmdSyncStart.AddArgument("server_address", "The IP address of the server to synchronize with", "String");
            //cmdSyncStart.AddArgument("server_port", "The port of the server to synchronize with", "Integer");

            Command cmdSyncStop = new Command("stop", CommandIntentions.COMMAND_HAZARDOUS, SyncStop, "Stops synchronization with RegionSyncServer.");
            //cmdSyncStop.AddArgument("server_address", "The IP address of the server to synchronize with", "String");
            //cmdSyncStop.AddArgument("server_port", "The port of the server to synchronize with", "Integer");

            Command cmdSyncStatus = new Command("status", CommandIntentions.COMMAND_HAZARDOUS, SyncStatus, "Displays synchronization status.");

            m_commander.RegisterCommand("start", cmdSyncStart);
            m_commander.RegisterCommand("stop", cmdSyncStop);
            m_commander.RegisterCommand("status", cmdSyncStatus);

            lock (m_scene)
            {
                // Add this to our scene so scripts can call these functions
                m_scene.RegisterModuleCommander(m_commander);
            }
        }


        /// <summary>
        /// Processes commandline input. Do not call directly.
        /// </summary>
        /// <param name="args">Commandline arguments</param>
        private void EventManager_OnPluginConsole(string[] args)
        {
            if (args[0] == "sync")
            {
                if (args.Length == 1)
                {
                    m_commander.ProcessConsoleCommand("help", new string[0]);
                    return;
                }

                string[] tmpArgs = new string[args.Length - 2];
                int i;
                for (i = 2; i < args.Length; i++)
                    tmpArgs[i - 2] = args[i];

                m_commander.ProcessConsoleCommand(args[1], tmpArgs);
            }
        }


        #endregion Console Command Interface

        #region RegionSyncModule members and functions

        /////////////////////////////////////////////////////////////////////////////////////////
        // Synchronization related functions, NOT exposed through IRegionSyncModule interface
        /////////////////////////////////////////////////////////////////////////////////////////

        private ILog m_log;
        //private bool m_active = true;

        private bool m_isSyncListenerLocal = false;
        private string m_syncListenerAddr;
        public string SyncListenerAddr
        {
            get { return m_syncListenerAddr; }
        }

        private int m_syncListenerPort;
        public int SyncListenerPort
        {
            get { return m_syncListenerPort; }
        }


        private Scene m_scene;
        public Scene LocalScene
        {
            get { return m_scene; }
        }

        private HashSet<SyncConnector> m_syncConnectors=null;
        private object m_syncConnectorsLock = new object();
        private System.Timers.Timer m_statsTimer = new System.Timers.Timer(1000);


        private void StatsTimerElapsed(object source, System.Timers.ElapsedEventArgs e)
        {
            //TO BE IMPLEMENTED
            m_log.Warn("[REGION SYNC MODULE]: StatsTimerElapsed -- NOT yet implemented.");
        }

        private void StartSyncListener()
        {
            m_regionSyncListener = new RegionSyncListener(m_syncListenerAddr, m_syncListenerPort, this);
            m_regionSyncListener.Start();
            m_statsTimer.Elapsed += new System.Timers.ElapsedEventHandler(StatsTimerElapsed);
            m_statsTimer.Start();
        }

        private void SyncStart(Object[] args)
        {

            if (m_isSyncListenerLocal)
            {
                if (m_regionSyncListener.IsListening)
                {
                    m_log.Warn("[REGION SYNC MODULE]: RegionSyncListener is local, already started");
                }
                else
                {
                    StartSyncListener();
                }
            }
            else
            {

            }
        }

        private void SyncStop(Object[] args)
        {
            if (m_isSyncListenerLocal)
            {
                if (m_regionSyncListener.IsListening)
                {
                    m_regionSyncListener.Shutdown();
                }
            }
            else
            {

            }
        }

        private void SyncStatus(Object[] args)
        {
            //TO BE IMPLEMENTED
            m_log.Warn("[REGION SYNC MODULE]: SyncStatus() TO BE IMPLEMENTED !!!");
        }

        public void AddSyncConnector(TcpClient tcpclient)
        {
            //IPAddress addr = ((IPEndPoint)tcpclient.Client.RemoteEndPoint).Address;
            //int port = ((IPEndPoint)tcpclient.Client.RemoteEndPoint).Port;

            SyncConnector syncConnector = new SyncConnector(tcpclient);
            AddSyncConnector(syncConnector);
        }

        public void AddSyncConnector(SyncConnector syncConnector)
        {
            lock (m_syncConnectorsLock)
            {
                // Create a new list while modifying the list: An optimization for frequent reads and occasional writes.
                // Anyone holding the previous version of the list can keep using it since
                // they will not hold it for long and get a new copy next time they need to iterate

                HashSet<SyncConnector> currentlist = m_syncConnectors;
                HashSet<SyncConnector> newlist = new HashSet<SyncConnector>(currentlist);
                newlist.Add(syncConnector);

                m_syncConnectors = newlist;
            }
        }

        public void RemoveSyncConnector(SyncConnector syncConnector)
        {
            lock (m_syncConnectorsLock)
            {
                // Create a new list while modifying the list: An optimization for frequent reads and occasional writes.
                // Anyone holding the previous version of the list can keep using it since
                // they will not hold it for long and get a new copy next time they need to iterate

                HashSet<SyncConnector> currentlist = m_syncConnectors;
                HashSet<SyncConnector> newlist = new HashSet<SyncConnector>(currentlist);
                newlist.Remove(syncConnector);

                m_syncConnectors = newlist;
            }
        }

        #endregion //RegionSyncModule members and functions

    }


    public class RegionSyncListener
    {
        private IPAddress m_addr;
        private Int32 m_port;
        private RegionSyncModule m_regionSyncModule;
        private ILog m_log;

        // The listener and the thread which listens for sync connection requests
        private TcpListener m_listener;
        private Thread m_listenerThread;

        private bool m_isListening = false;
        public bool IsListening
        {
            get { return m_isListening; }
        }

        public RegionSyncListener(string addr, int port, RegionSyncModule regionSyncModule)
        {
            m_addr = IPAddress.Parse(addr);
            m_port = port;
            m_regionSyncModule = regionSyncModule;

            m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        }

        // Start the listener
        public void Start()
        {
            m_listenerThread = new Thread(new ThreadStart(Listen));
            m_listenerThread.Name = "RegionSyncListener";
            m_log.WarnFormat("[REGION SYNC LISTENER] Starting {0} thread", m_listenerThread.Name);
            m_listenerThread.Start();
            m_isListening = true;
            //m_log.Warn("[REGION SYNC SERVER] Started");
        }

        // Stop the server and disconnect all RegionSyncClients
        public void Shutdown()
        {
            // Stop the listener and listening thread so no new clients are accepted
            m_listener.Stop();

            //Aborting the listener thread probably is not the best way to shutdown, but let's worry about that later.
            m_listenerThread.Abort();
            m_listenerThread = null;
            m_isListening = false;
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
                    m_log.WarnFormat("[REGION SYNC SERVER] Listening for new connections on {0}:{1}...", m_addr.ToString(), m_port.ToString());
                    TcpClient tcpclient = m_listener.AcceptTcpClient();

                    //pass the tcpclient information to RegionSyncModule, who will then create a SyncConnector
                    m_regionSyncModule.AddSyncConnector(tcpclient);
                }
            }
            catch (SocketException e)
            {
                m_log.WarnFormat("[REGION SYNC SERVER] [Listen] SocketException: {0}", e);
            }
        }

    }
 
}
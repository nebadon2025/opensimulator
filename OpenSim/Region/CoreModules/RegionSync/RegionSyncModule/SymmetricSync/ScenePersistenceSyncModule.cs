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

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    /*
    public class DSGActorBase
    {
        protected RegionSyncModule m_regionSyncModule;

        public DSGActorBase(RegionSyncModule regionSyncModule)
        {
            m_regionSyncModule = regionSyncModule;
        }

        public virtual void init()
        {

        }


    }
     * */

    public class ScenePersistenceSyncModule : INonSharedRegionModule, IScenePersistenceSyncModule
    {
        #region INonSharedRegionModule

        public void Initialise(IConfigSource config)
        {
            m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            IConfig syncConfig = config.Configs["RegionSyncModule"];
            m_active = false;
            if (syncConfig == null)
            {
                m_log.Warn("[Scene Persistence Sync MODULE] No RegionSyncModule config section found. Shutting down.");
                return;
            }
            else if (!syncConfig.GetBoolean("Enabled", false))
            {
                m_log.Warn("[Scene Persistence Sync MODULE] RegionSyncModule is not enabled. Shutting down.");
                return;
            }

            string actorType = syncConfig.GetString("DSGActorType", "").ToLower();
            if (!actorType.Equals("scene_persistence"))
            {
                m_log.Warn("[Scene Persistence Sync MODULE]: not configured as Scene Persistence Actor. Shut down.");
                return;
            }

        }

        //Called after Initialise()
        public void AddRegion(Scene scene)
        {
            if (!m_active)
                return;

            //connect with scene
            m_scene = scene;

            //register the module
            m_scene.RegisterModuleInterface<IScenePersistenceSyncModule>(this);

            // Setup the command line interface
            //m_scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
            //InstallInterfaces();
        }

        //Called after AddRegion() has been called for all region modules of the scene
        public void RegionLoaded(Scene scene)
        {

        }

        public void RemoveRegion(Scene scene)
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close()
        {
            m_scene = null;
        }

        public string Name
        {
            get { return "ScenePersistenceSyncModule"; }
        }



        #endregion //IRegionModule

        #region RegionSyncModule members and functions

        private ILog m_log;
        private bool m_active = false;
        public bool Active
        {
            get { return m_active; }
        }

        private Scene m_scene;
        public Scene LocalScene
        {
            get { return m_scene; }
        }

        #endregion //INonSharedRegionModule members and functions
    }

    /*
    public class RegionSyncListener
    {
        // Start the listener
        public void Start()
        {
            m_listenerThread = new Thread(new ThreadStart(Listen));
            m_listenerThread.Name = "RegionSyncServer Listener";
            m_log.WarnFormat("[REGION SYNC SERVER] Starting {0} thread", m_listenerThread.Name);
            m_listenerThread.Start();
            //m_log.Warn("[REGION SYNC SERVER] Started");
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
                    IPAddress addr = ((IPEndPoint)tcpclient.Client.RemoteEndPoint).Address;
                    int port = ((IPEndPoint)tcpclient.Client.RemoteEndPoint).Port;

                    //pass the tcpclient information to RegionSyncModule, who will then create a SyncConnector
                }
            }
            catch (SocketException e)
            {
                m_log.WarnFormat("[REGION SYNC SERVER] [Listen] SocketException: {0}", e);
            }
        }
     
    }
     * */ 

}
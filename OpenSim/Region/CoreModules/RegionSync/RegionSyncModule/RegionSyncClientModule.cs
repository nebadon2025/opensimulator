/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
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

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    public class RegionSyncClientModule : IRegionModule, IRegionSyncClientModule, ICommandableModule
    {
        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
            
            IConfig syncConfig = config.Configs["RegionSyncModule"];
            if (syncConfig == null || syncConfig.GetString("Mode", "client").ToLower() != "client")
            {
                m_active = false;
                m_log.Warn("[REGION SYNC CLIENT MODULE] Not in client mode. Shutting down.");
                return;
            }
            m_serveraddr = syncConfig.GetString("ServerIPAddress", "127.0.0.1");
            m_serverport = syncConfig.GetInt("ServerPort", 13000);
            m_scene = scene;
            m_scene.RegisterModuleInterface<IRegionSyncClientModule>(this);

            // Setup the command line interface
            m_scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
            InstallInterfaces();

            m_log.Warn("[REGION SYNC CLIENT MODULE] Initialised");
        }

        public void PostInitialise()
        {
            if (!m_active)
                return;
            // Go ahead and try to sync right away
            Start();
        }

        public void Close()
        {
            m_scene = null;
        }

        public string Name
        {
            get { return "RegionSyncModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }
        #endregion

        #region ICommandableModule Members
        private readonly Commander m_commander = new Commander("sync");
        public ICommander CommandInterface
        {
            get { return m_commander; }
        }
        #endregion

        #region IRegionSyncClientModule members
        public void SendCoarseLocations()
        {
            m_client.SendCoarseLocations();
        }

        public bool Active
        {
            get { return m_active; }
        }

        public bool Synced
        {
            get 
            { 
                lock(m_client_lock)
                {
                    return (m_client != null); 
                }
            }
        }
        #endregion

        #region RegionSyncClientModule members
        private bool m_active = true;
        private string m_serveraddr;
        private int m_serverport;
        private Scene m_scene;
        private ILog m_log;
        private Object m_client_lock = new Object();
        private RegionSyncClient m_client = null;


        #endregion

        #region Event Handlers
        #endregion

        private void DebugSceneStats()
        {
            return;
            /*
            List<ScenePresence> avatars = m_scene.GetAvatars(); 
            List<EntityBase> entities = m_scene.GetEntities();
            m_log.WarnFormat("There are {0} avatars and {1} entities in the scene", avatars.Count, entities.Count);
             */
        }

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

        private void SyncStart(Object[] args)
        {
            Start();
        }

        private void Start()
        {
            
            lock (m_client_lock)
            {
                if (m_client != null)
                {
                    m_log.WarnFormat("[REGION SYNC CLIENT MODULE] Already synchronizing to {0}", m_client.GetServerAddressAndPort());
                    return;
                }
                //m_log.Warn("[REGION SYNC CLIENT MODULE] Starting synchronization");
                m_log.Warn("[REGION SYNC CLIENT MODULE] Starting RegionSyncClient");

                m_client = new RegionSyncClient(m_scene, m_serveraddr, m_serverport);
                m_client.Start();
            }
        }

        private void SyncStop(Object[] args)
        {
            lock(m_client_lock)
            {
                if (m_client == null)
                {
                    m_log.WarnFormat("[REGION SYNC CLIENT MODULE] Already stopped");
                    return;
                }
                m_client.Stop();
                m_client = null;
                m_log.Warn("[REGION SYNC CLIENT MODULE] Stopping synchronization");
            }
        }

        private void SyncStatus(Object[] args)
        {
            lock (m_client_lock)
            {
                if (m_client == null)
                {
                    m_log.WarnFormat("[REGION SYNC CLIENT MODULE] Not currently synchronized");
                    return;
                }
                m_log.WarnFormat("[REGION SYNC CLIENT MODULE] Synchronized");
                m_client.ReportStatus();
            }
        }
        #endregion
    }
}

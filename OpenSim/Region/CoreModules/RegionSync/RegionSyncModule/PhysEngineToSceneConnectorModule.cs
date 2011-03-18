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
using System.Reflection;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Region.CoreModules.Framework.InterfaceCommander;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Physics.Manager;
using OpenSim.Services.Interfaces;
using log4net;
using System.Net;
using System.Net.Sockets;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    //The connector that connects the local Scene (cache) and remote authoratative Scene
    public class PhysEngineToSceneConnectorModule : IRegionModule, IPhysEngineToSceneConnectorModule, ICommandableModule
    {
        #region PhysEngineToSceneConnectorModule members and functions

        private static int m_activeActors = 0;
        private bool m_active = false;
        private string m_serveraddr;
        private int m_serverport;
        private Scene m_scene;
        private static List<Scene> m_allScenes = new List<Scene>();
        private ILog m_log;
        private Object m_client_lock = new Object();
        //private PhysEngineToSceneConnector m_scriptEngineToSceneConnector = null;
        private IConfig m_syncConfig = null;
        public IConfig SyncConfig { get { return m_syncConfig; } }
        private bool m_debugWithViewer = false;
        public bool DebugWithViewer { get { return m_debugWithViewer; } }
        private string m_regionSyncMode = "";

        //Variables relavant for multi-scene subscription. 
        private Dictionary<string, PhysEngineToSceneConnector> m_PEToSceneConnectors = new Dictionary<string, PhysEngineToSceneConnector>(); //connector for each auth. scene
        private string LogHeader = "[PhysEngineToSceneConnectorModule]";
        private PhysEngineToSceneConnector m_idlePEToSceneConnector = null;
        private PhysEngineToSceneConnector m_physEngineToSceneConnector = null;

        //quark information
        //private int QuarkInfo.SizeX;
        //private int QuarkInfo.SizeY;
        //private string m_quarkListString;
        private string m_subscriptionSpaceString;

        #endregion PhysEngineToSceneConnectorModule members and functions

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
            m_active = false; //set to false unless this is the valid local scene

            //Read in configuration
            IConfig syncConfig = config.Configs["RegionSyncModule"];
            if (syncConfig != null 
                    && syncConfig.GetBoolean("Enabled", false)
                    // && syncConfig.GetString("Mode", "").ToLower() == "client"
                    && syncConfig.GetBoolean("PhysEngineClient", false)
                )
            {
                //scene.RegionSyncEnabled = true;
            }
            else
            {
                //scene.RegionSyncEnabled = false;
                m_log.Warn(LogHeader + ": Not in physics engine client mode. Shutting down.");
                return;
            }

            m_active = true;
            m_activeActors++;

            m_log.Debug(LogHeader + " Init PEToSceneConnectorModule, for local scene " + scene.RegionInfo.RegionName);

            m_scene = scene;
            m_scene.RegisterModuleInterface<IPhysEngineToSceneConnectorModule>(this);
            m_syncConfig = syncConfig;
            m_debugWithViewer = syncConfig.GetBoolean("PhysEngineDebugWithViewer", false);

            //read in the quark size information
            //QuarkInfo.SizeX = syncConfig.GetInt("QuarkSizeX", (int)Constants.RegionSize);
            //QuarkInfo.SizeY = syncConfig.GetInt("QuarkSizeY", (int)Constants.RegionSize);
            QuarkInfo.SizeX = syncConfig.GetInt("QuarkSizeX", (int)Constants.RegionSize);
            QuarkInfo.SizeY = syncConfig.GetInt("QuarkSizeY", (int)Constants.RegionSize);

            //m_quarkListString = syncConfig.GetString("InitQuarkSet", ""); //if not specified, dost not subscribe to any quark
            //if (m_quarkListString.Equals("all"))
            //{
            //    m_quarkListString = RegionSyncUtil.QuarkStringListToString(RegionSyncUtil.GetAllQuarkStringInScene(QuarkInfo.SizeX, QuarkInfo.SizeY));
            //}
            m_subscriptionSpaceString = syncConfig.GetString("InitSubscriptionSpace", "0_0,256_256");
            
             
            // Setup the command line interface
            m_scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
            InstallInterfaces();

            m_log.Warn(LogHeader + " Initialised");

            // collect all the scenes for later routing
            if (!m_allScenes.Contains(scene))
            {
                m_allScenes.Add(scene);
            }
        }

        public void PostInitialise()
        {
            if (!m_active)
                return;

            Start();    // fake a 'phys start' to get things going

            //m_log.Warn(LogHeader + " Post-Initialised");
        }

        public void Close()
        {
            if (m_active)
            {
            }
            m_scene = null;
            m_active = false;
            m_activeActors--;
        }

        public string Name
        {
            get { return "RegionSyncPhysEngineModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }
        #endregion

        #region ICommandableModule Members
        private readonly Commander m_commander = new Commander("phys");
        public ICommander CommandInterface
        {
            get { return m_commander; }
        }
        #endregion

        #region IPhysEngineToSceneConnectorModule members


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
                    return (m_PEToSceneConnectors.Count > 0);
                }
            }
        }

        bool IPhysEngineToSceneConnectorModule.IsPhysEngineActor()
        {
            return PhysEngineToSceneConnectorModule.IsPhysEngineActorS;
        }
        bool IPhysEngineToSceneConnectorModule.IsPhysEngineScene()
        {
            return PhysEngineToSceneConnectorModule.IsPhysEngineSceneS;
        }
        bool IPhysEngineToSceneConnectorModule.IsActivePhysEngineScene()
        {
            return PhysEngineToSceneConnectorModule.IsActivePhysEngineSceneS;
        }

        public static bool IsPhysEngineSceneS
        {
            get { return SceneToPhysEngineSyncServer.IsPhysEngineScene2S(); }
        }
        public static bool IsActivePhysEngineSceneS
        {
            get { return SceneToPhysEngineSyncServer.IsActivePhysEngineScene2S(); }
        }
        public static bool IsPhysEngineActorS
        {
            get { return (m_activeActors != 0); }
        }

        /// <summary>
        /// The scene is unknown by ODE so we have to look through the scenes to
        /// find the one with this PhysicsActor so we can send the update.
        /// </summary>
        /// <param name="pa"></param>
        public static void RouteUpdate(PhysicsActor pa)
        {
            SceneObjectPart sop;
            ScenePresence sp;
            Scene s = null;
            foreach (Scene ss in m_allScenes)
            {
                try
                {
                    sop = ss.GetSceneObjectPart(pa.UUID);
                }
                catch
                {
                    sop = null;
                }
                if (sop != null)
                {
                    s = ss;
                    break;
                }
                try
                {
                    sp = ss.GetScenePresence(pa.UUID);
                }
                catch
                {
                    sp = null;
                }
                if (sp != null)
                {
                    s = ss;
                    break;
                }
            }
            if (s != null)
            {
                if (s.PhysEngineToSceneConnectorModule != null)
                {
                    s.PhysEngineToSceneConnectorModule.SendUpdate(pa);
                }
                else
                {
                    Console.WriteLine("RouteUpdate: PhysEngineToSceneConnectorModule is null");
                }
            }
            else
            {
                Console.WriteLine("RouteUpdate: no SOP found for {0}", pa.UUID);
            }
            return;
        }

        #endregion


        #region Event Handlers
        #endregion

        private void DebugSceneStats()
        {
            return;
            /*
            List<ScenePresence> avatars = m_scene.GetAvatars(); 
            List<EntityBase> entities = m_scene.GetEntities();
            m_log.WarnFormat("{0} There are {1} avatars and {2} entities in the scene", LogHeader, avatars.Count, entities.Count);
             */
        }

        public void SendUpdate(PhysicsActor pa)
        {
            if (this.m_physEngineToSceneConnector != null)
            {
                this.m_physEngineToSceneConnector.SendPhysUpdateAttributes(pa);
            }
        }

        #region Console Command Interface
        //IMPORTANT: these functions should only be actived for the PhysEngineToSceneConnectorModule that is associated with the valid local scene

        private void InstallInterfaces()
        {
            Command cmdSyncStart = new Command("start", CommandIntentions.COMMAND_HAZARDOUS, SyncStart, "Begins synchronization with RegionSyncServer.");
            //cmdSyncStart.AddArgument("server_port", "The port of the server to synchronize with", "Integer");
            
            Command cmdSyncStop = new Command("stop", CommandIntentions.COMMAND_HAZARDOUS, SyncStop, "Stops synchronization with RegionSyncServer.");
            //cmdSyncStop.AddArgument("server_address", "The IP address of the server to synchronize with", "String");
            //cmdSyncStop.AddArgument("server_port", "The port of the server to synchronize with", "Integer");

            Command cmdSyncStatus = new Command("status", CommandIntentions.COMMAND_HAZARDOUS, SyncStatus, "Displays synchronization status.");

            //The following two commands are more for easier debugging purpose
            Command cmdSyncSetQuarks = new Command("quarkSpace", CommandIntentions.COMMAND_HAZARDOUS, SetQuarkList, "Set the set of quarks to subscribe to. For debugging purpose. Should be issued before \"sync start\"");
            cmdSyncSetQuarks.AddArgument("quarkSpace", "The (rectangle) space of quarks to subscribe, represented by x0_y0,x1_y1, the left-bottom and top-right corners of the rectangel space", "String");

            Command cmdSyncSetQuarkSize = new Command("quarksize", CommandIntentions.COMMAND_HAZARDOUS, SetQuarkSize, "Set the size of each quark. For debugging purpose. Should be issued before \"sync quarks\"");
            cmdSyncSetQuarkSize.AddArgument("quarksizeX", "The size on x axis of each quark", "Integer");
            cmdSyncSetQuarkSize.AddArgument("quarksizeY", "The size on y axis of each quark", "Integer");

            m_commander.RegisterCommand("start", cmdSyncStart);
            m_commander.RegisterCommand("stop", cmdSyncStop);
            m_commander.RegisterCommand("status", cmdSyncStatus);
            m_commander.RegisterCommand("quarkSpace", cmdSyncSetQuarks);

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
            if (args[0] == "phys")
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
            m_serveraddr = m_scene.RegionInfo.PhysicsSyncServerAddress;
            m_serverport = m_scene.RegionInfo.PhysicsSyncServerPort;

            lock (m_client_lock)
            {
                //m_log.Warn(LogHeader + " Starting synchronization");
                m_log.Warn(LogHeader + ": Starting RegionSyncPhysEngine");

                //Only one remote scene to connect to. Subscribe to whatever specified in the config file.
                //List<string> quarkStringList = RegionSyncUtil.QuarkStringToStringList(m_quarkListString);
                //InitPhysEngineToSceneConnector(quarkStringList);
                InitPhysEngineToSceneConnector(m_subscriptionSpaceString);
            }
        }

        private void SetQuarkList(Object[] args)
        {
            m_subscriptionSpaceString = (string)args[0];

            InitPhysEngineToSceneConnector(m_subscriptionSpaceString);
        }

        private void SetQuarkSize(Object[] args)
        {
            QuarkInfo.SizeX = (int)args[0];
            QuarkInfo.SizeY = (int)args[1];

        }

        private void InitPhysEngineToSceneConnector(string space)
        {
            
            m_physEngineToSceneConnector = new PhysEngineToSceneConnector(m_scene, 
                    m_serveraddr, m_serverport, m_debugWithViewer, /* space,*/ m_syncConfig);
            if (m_physEngineToSceneConnector.Start())
            {
                m_PEToSceneConnectors.Add(m_scene.RegionInfo.RegionName, m_physEngineToSceneConnector);
            }
        }

        private void SyncStop(Object[] args)
        {
            lock (m_client_lock)
            {
                //if (m_scriptEngineToSceneConnector == null)
                if(m_PEToSceneConnectors.Count==0 && m_idlePEToSceneConnector==null)
                {
                    m_log.Warn(LogHeader + " Already stopped");
                    return;
                }

                if (m_PEToSceneConnectors.Count > 0)
                {
                    foreach (KeyValuePair<string, PhysEngineToSceneConnector> valPair in m_PEToSceneConnectors)
                    {
                        PhysEngineToSceneConnector connector = valPair.Value;
                        if (connector == null)
                        {
                            continue;
                        }
                        connector.Stop();
                    }
                    m_PEToSceneConnectors.Clear();
                }
                else if (m_idlePEToSceneConnector != null)
                {
                    m_idlePEToSceneConnector.Stop();
                    m_idlePEToSceneConnector = null;
                }

                //m_scriptEngineToSceneConnector.Stop();
                //m_scriptEngineToSceneConnector = null;
                m_log.Warn(LogHeader+": Stopping synchronization");
            }

            //save script state and stop script instances
            // TODO: Load balancing. next line commented out to compile
            // m_scene.EventManager.TriggerPhysEngineSyncStop();
            //remove all objects
            m_scene.DeleteAllSceneObjects();
            
        }

        private void SyncStatus(Object[] args)
        {
            lock (m_client_lock)
            {
                if (m_PEToSceneConnectors.Count == 0)
                {
                    m_log.Warn(LogHeader + " Not currently synchronized");
                    return;
                }
                foreach (KeyValuePair<string, PhysEngineToSceneConnector> pair in m_PEToSceneConnectors)
                {
                    PhysEngineToSceneConnector sceneConnector = pair.Value;
                    sceneConnector.ReportStatus();
                }
            }
        }
        #endregion
    }
}

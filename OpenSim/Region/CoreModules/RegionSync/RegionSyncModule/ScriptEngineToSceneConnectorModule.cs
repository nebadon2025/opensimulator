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
    //The information of the authoratative copy of a scene
    public class AuthSceneInfo
    {
        public string Name;
        public string Addr;
        public int Port;
        public int LocX=-1;
        public int LocY=-1;

        public AuthSceneInfo(string name, string addr, int port)
        {
            Name = name;
            Addr = addr;
            Port = port;
        }

        public AuthSceneInfo(string name, string addr, int port, int locX, int locY)
        {
            Name = name;
            Addr = addr;
            Port = port;
            LocX = locX;
            LocY = locY;
        }
    }

    //The connector that connects the local Scene (cache) and remote authoratative Scene
    public class ScriptEngineToSceneConnectorModule : IRegionModule, IScriptEngineToSceneConnectorModule, ICommandableModule
    {
        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
            m_active = false; //set to false unless this is the valid local scene

            //Read in configuration
            IConfig syncConfig = config.Configs["RegionSyncModule"];
            if (syncConfig != null && syncConfig.GetString("Enabled", "").ToLower() == "true")
            {
                scene.RegionSyncEnabled = true;
            }
            else
            {
                scene.RegionSyncEnabled = false;
            }

            m_regionSyncMode = syncConfig.GetString("Mode", "").ToLower();
            if (syncConfig == null || m_regionSyncMode != "script_engine")
            {
                m_log.Warn("[REGION SYNC SCRIPT ENGINE MODULE] Not in script_engine mode. Shutting down.");
                return;
            }

            //get the name of the valid region for script engine, i.e., that region that will holds all objects and scripts
            //if not matching m_scene's name, simply return
            string validLocalScene = syncConfig.GetString("ValidScriptEngineScene", "");
            if (!validLocalScene.Equals(scene.RegionInfo.RegionName))
            {
                m_log.Warn("Not the valid local scene, shutting down");
                return;
            }
            m_active = true; 
            m_validLocalScene = validLocalScene;

            m_log.Debug("Init SEToSceneConnectorModule, for local scene " + scene.RegionInfo.RegionName);

            //get the number of regions this script engine subscribes
            m_sceneNum = syncConfig.GetInt("SceneNumber", 1);

            //get the mapping of local scenes to auth. scenes
            List<string> authScenes = new List<string>();
            for (int i = 0; i < m_sceneNum; i++)
            {
                string localScene = "LocalScene" + i;
                string localSceneName = syncConfig.GetString(localScene, "");
                string masterScene = localScene + "Master";
                string masterSceneName = syncConfig.GetString(masterScene, "");

                if (localSceneName.Equals("") || masterSceneName.Equals(""))
                {
                    m_log.Warn(localScene + " or " + masterScene+ " has not been assigned a value in configuration. Shutting down.");
                    return;
                }

                //m_localToAuthSceneMapping.Add(localSceneName, masterSceneName);
                RecordLocalAuthSceneMappings(localSceneName, masterSceneName);
                authScenes.Add(masterSceneName);
                m_localScenesByName.Add(localSceneName, null);
            }

            int defaultPort = 13000;
            //get the addr:port info of the authoritative scenes
            for (int i = 0; i < m_sceneNum; i++)
            {
                string authSceneName = authScenes[i];
                //string serverAddr = authSceneName + "_ServerIPAddress";
                //string serverPort = authSceneName + "_ServerPort";
                string serverAddr = authSceneName + "_SceneToSESyncServerIP";
                string addr = syncConfig.GetString(serverAddr, "127.0.0.1");
                string serverPort = authSceneName + "_SceneToSESyncServerPort";
                int port = syncConfig.GetInt(serverPort, defaultPort);
                defaultPort++;

                AuthSceneInfo authSceneInfo = new AuthSceneInfo(authSceneName, addr, port);
                m_authScenesInfoByName.Add(authSceneName, authSceneInfo);
            }

            m_scene = scene;
            m_scene.RegisterModuleInterface<IScriptEngineToSceneConnectorModule>(this);
            m_syncConfig = syncConfig;
            m_debugWithViewer = syncConfig.GetBoolean("ScriptEngineDebugWithViewer", false);

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

            m_log.Warn("[REGION SYNC SCRIPT ENGINE MODULE] Initialised");
        }

        public void PostInitialise()
        {
            if (!m_active)
                return;

            //m_log.Warn("[REGION SYNC CLIENT MODULE] Post-Initialised");
            m_scene.EventManager.OnPopulateLocalSceneList += OnPopulateLocalSceneList;
        }

        public void Close()
        {
            if (m_active)
            {
                m_scene.EventManager.OnPopulateLocalSceneList -= OnPopulateLocalSceneList;
            }
            m_scene = null;
            m_active = false;
        }

        public string Name
        {
            get { return "RegionSyncScriptEngineModule"; }
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

        #region IScriptEngineToSceneConnectorModule members


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
                    //return (m_scriptEngineToSceneConnector != null); 
                    return (m_SEToSceneConnectors.Count > 0);
                }
            }
        }


        #endregion

        #region ScriptEngineToSceneConnectorModule members and functions

        private bool m_active = false;
        private string m_serveraddr;
        private int m_serverport;
        private Scene m_scene;
        private ILog m_log;
        private Object m_client_lock = new Object();
        //private ScriptEngineToSceneConnector m_scriptEngineToSceneConnector = null;
        private IConfig m_syncConfig = null;
        private bool m_debugWithViewer = false;
        private string m_regionSyncMode = "";

        //Variables relavant for multi-scene subscription. 
        private int m_sceneNum = 0;
        private string m_validLocalScene = "";
        private Dictionary<string, string> m_localToAuthSceneMapping = new Dictionary<string,string>(); //1-1 mapping from local shadow scene to authoratative scene
        private Dictionary<string, string> m_authToLocalSceneMapping = new Dictionary<string, string>(); //1-1 mapping from authoratative scene to local shadow scene
        private Dictionary<string, Scene> m_localScenesByName = new Dictionary<string,Scene>(); //name and references to local scenes
        private Dictionary<string, AuthSceneInfo> m_authScenesInfoByName = new Dictionary<string,AuthSceneInfo>(); //info of each auth. scene's connector port, stored by each scene's name
        private Dictionary<string, ScriptEngineToSceneConnector> m_SEToSceneConnectors = new Dictionary<string, ScriptEngineToSceneConnector>(); //connector for each auth. scene
        private Dictionary<string, AuthSceneInfo> m_authScenesInfoByLoc = new Dictionary<string,AuthSceneInfo>(); //IP and port number of each auth. scene's connector port
        private string LogHeader = "[ScriptEngineToSceneConnectorModule]";
        private ScriptEngineToSceneConnector m_idleSEToSceneConnector = null;

        //quark information
        //private int QuarkInfo.SizeX;
        //private int QuarkInfo.SizeY;
        //private string m_quarkListString;
        private string m_subscriptionSpaceString;

        public IConfig SyncConfig
        {
            get { return m_syncConfig; }
        }

        public bool DebugWithViewer
        {
            get { return m_debugWithViewer; }
        }

        //Record the locX and locY of one auth. scene (identified by addr:port) this ScriptEngine connects to
        public void RecordSceneLocation(string addr, int port, uint locX, uint locY)
        {
            string loc = SceneLocToString(locX, locY);
            if (m_authScenesInfoByLoc.ContainsKey(loc))
            {
                m_log.Warn(": have already registered info for Scene at " + loc);
                m_authScenesInfoByLoc.Remove(loc);
            }

            foreach (KeyValuePair<string, AuthSceneInfo> valPair in m_authScenesInfoByName)
            {
                AuthSceneInfo authSceneInfo = valPair.Value;
                if (authSceneInfo.Addr == addr && authSceneInfo.Port == port)
                {
                    authSceneInfo.LocX = (int)locX;
                    authSceneInfo.LocY = (int)locY;
                    m_authScenesInfoByLoc.Add(loc, authSceneInfo);   
                    break;
                }
            }
        }

        /// <summary>
        /// Set the property of a prim located in the given scene (identified by locX, locY)
        /// </summary>
        /// <param name="locX"></param>
        /// <param name="locY"></param>
        /// <param name="primID"></param>
        /// <param name="pName"></param>
        /// <param name="pValue"></param>
        public void SendSetPrimProperties(uint locX, uint locY, UUID primID, string pName, object pValue)
        {
            if (!Active || !Synced)
                return;

            ScriptEngineToSceneConnector connector = GetSEToSceneConnector(locX, locY);
            connector.SendSetPrimProperties(primID, pName, pValue);
        }

        public Scene GetLocalScene(string authSceneName)
        {
            if (!m_authToLocalSceneMapping.ContainsKey(authSceneName))
            {
                m_log.Warn(LogHeader + ": no authoritative scene with name "+authSceneName+" recorded");
                return null;
            }
            string localSceneName = m_authToLocalSceneMapping[authSceneName];
            if (m_localScenesByName.ContainsKey(localSceneName))
            {
                return m_localScenesByName[localSceneName];
            }
            else
                return null;
        }

        private string SceneLocToString(uint locX, uint locY)
        {
            string loc = locX + "-" + locY;
            return loc;
        }

        //Get the right instance of ScriptEngineToSceneConnector, given the location of the authoritative scene
        private ScriptEngineToSceneConnector GetSEToSceneConnector(uint locX, uint locY)
        {
            string loc = SceneLocToString(locX, locY);
            if (!m_authScenesInfoByLoc.ContainsKey(loc))
                return null;
            string authSceneName = m_authScenesInfoByLoc[loc].Name;
            if (!m_SEToSceneConnectors.ContainsKey(authSceneName))
            {
                return null;
            }
            return m_SEToSceneConnectors[authSceneName];
        }


        private void RecordLocalAuthSceneMappings(string localSceneName, string authSceneName)
        {
            if (m_localToAuthSceneMapping.ContainsKey(localSceneName))
            {
                m_log.Warn(LogHeader + ": already registered " + localSceneName+", authScene was recorded as "+ m_localToAuthSceneMapping[localSceneName]);
            }
            else
            {
                m_localToAuthSceneMapping.Add(localSceneName, authSceneName);
            }
            if (m_authToLocalSceneMapping.ContainsKey(authSceneName))
            {
                m_log.Warn(LogHeader + ": already registered " + authSceneName + ", authScene was recorded as " + m_authToLocalSceneMapping[authSceneName]);
            }
            else
            {
                m_authToLocalSceneMapping.Add(authSceneName, localSceneName);
            }
        }

        //Get the name of the authoritative scene the given local scene maps to. Return null if not found.
        private string GetAuthSceneName(string localSceneName)
        {
            if (m_localToAuthSceneMapping.ContainsKey(localSceneName))
            {
                m_log.Warn(LogHeader + ": " + localSceneName + " not registered in m_localToAuthSceneMapping");
                return null;
            }
            return m_localToAuthSceneMapping[localSceneName];
        }

        //get the name of the local scene the given authoritative scene maps to. Return null if not found.
        private string GetLocalSceneName(string authSceneName)
        {
            if (!m_authToLocalSceneMapping.ContainsKey(authSceneName))
            {
                m_log.Warn(LogHeader + ": " + authSceneName + " not registered in m_authToLocalSceneMapping");
                return null;
            }
            return m_authToLocalSceneMapping[authSceneName];
        }

        #endregion

        #region Event Handlers

        public void OnPopulateLocalSceneList(List<Scene> localScenes)
        //public void OnPopulateLocalSceneList(List<Scene> localScenes, string[] cmdparams)
        {
            if (!Active)
                return;

            //populate the dictionary m_localScenes
            foreach (Scene lScene in localScenes)
            {
                string name = lScene.RegionInfo.RegionName;
                if(!m_localScenesByName.ContainsKey(name)){
                    m_log.Warn(LogHeader+": has not reigstered a local scene named "+name);
                    continue;
                }
                m_localScenesByName[name] = lScene;

                //lScene.RegionSyncMode = m_regionSyncMode;
                lScene.IsOutsideScenes = IsOutSideSceneSubscriptions;
            }

            //test position conversion
            /*
            //Vector3 pos = new Vector3(290, 100, 10);
            uint preLocX = Convert.ToUInt32(cmdparams[2]);
            uint preLocY = Convert.ToUInt32(cmdparams[3]);
            float posX = (float)Convert.ToDouble(cmdparams[4]);
            float posY = (float)Convert.ToDouble(cmdparams[5]);
            float posZ = (float)Convert.ToDouble(cmdparams[6]);
            Vector3 pos = new Vector3(posX, posY, posZ);
            uint locX, locY;
            Vector3 newPos;
            ConvertPosition(1000, 1000, pos, out locX, out locY, out newPos);
             * */
        }

        #endregion

        private string GetAllSceneNames()
        {
            string scenes = "";
            foreach (KeyValuePair<string, Scene> valPair in m_localScenesByName)
            {
                Scene lScene = valPair.Value;
                string authScene = m_localToAuthSceneMapping[lScene.RegionInfo.RegionName];
                scenes += authScene + ",";

            }
            return scenes;
        }

        //public bool IsOutSideSceneSubscriptions(Scene currentScene, Vector3 pos)
        public bool IsOutSideSceneSubscriptions(uint locX, uint locY, Vector3 pos)
        {
            string sceneNames = GetAllSceneNames();
            m_log.Debug(LogHeader + ": IsOutSideSceneSubscriptions called. Conceptually, we are checking inside scene-subscriptions: " + sceneNames);

            //First, convert the position to a scene s.t. the attempting position is contained withing that scene
            uint curLocX, curLocY;
            Vector3 curPos;
            bool converted = ConvertPosition(locX, locY, pos, out curLocX, out curLocY, out curPos);

            if (!converted)
            {
                m_log.Warn("("+locX+","+locY+","+pos+")"+" converts to scenes with negative coordinates.");
                return false;
            }
            //See of the quark identified by (curLocX,curLocY) is one we subscribed to
            string sceneLoc = SceneLocToString(curLocX, curLocY);
            if (m_authScenesInfoByLoc.ContainsKey(sceneLoc))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        //When the offset position is outside the range of current scene, convert it to the offset position in the right quark.
        //Return null if the new scene's left-bottom corner X or Y value is negative.
        //Assumption: A position is uniquely identified by (locX, locY, offsetPos).
        private bool ConvertPosition(uint preLocX, uint preLocY, Vector3 prePos, out uint curLocX, out uint curLocY, out Vector3 curPos)
        {
            Vector3 newPos;
            int newLocX;
            int newLocY;
            //code copied from EntityTransferModule.Cross()

            newPos = prePos; 
            newLocX = (int)preLocX;
            newLocY = (int)preLocY;

            int changeX = 1;
            int changeY = 1;

            //Adjust the X values, if going east, changeX is positive, otherwise, it is negative
            if (prePos.X >= 0)
            {
                changeX = (int)(prePos.X / (int)Constants.RegionSize);
            }
            else
            {
                changeX = (int)(prePos.X / (int)Constants.RegionSize) - 1 ;
            }
            newLocX = (int)preLocX + changeX;
            newPos.X = prePos.X - (changeX * Constants.RegionSize);

            if (prePos.Y >= 0)
            {
                changeY = (int)(prePos.Y / (int)Constants.RegionSize);
            }
            else
            {
                changeY = (int)(prePos.Y / (int)Constants.RegionSize) - 1;
            }
            changeY = (int)(prePos.Y / (int)Constants.RegionSize);
            newLocY = (int)preLocY + changeY;
            newPos.Y = prePos.Y - (changeY * Constants.RegionSize);

            curLocX = (uint)newLocX;
            curLocY = (uint)newLocY;
            curPos = newPos;

            if (newLocX < 0 || newLocY < 0)
            {
                //reset the position
                curLocX = preLocX;
                curLocY = preLocY;
                if (newLocX < 0)
                {
                    curPos.X = 2;
                }
                if(newLocY<0)
                {
                    curPos.Y = 2;
                }
                return false;
            }
            else
                return true;
        }
           

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
        //IMPORTANT: these functions should only be actived for the ScriptEngineToSceneConnectorModule that is associated with the valid local scene

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

            Command cmdSyncRegister = new Command("register", CommandIntentions.COMMAND_HAZARDOUS, SyncRegister, "Register as an idle script engine. Sync'ing with Scene won't start until \"sync start\". ");

            //For debugging load balancing and migration process
            Command cmdSyncStartLB = new Command("startLB", CommandIntentions.COMMAND_HAZARDOUS, SyncStartLB, "Register as an idle script engine. Sync'ing with Scene won't start until \"sync start\". ");

            m_commander.RegisterCommand("start", cmdSyncStart);
            m_commander.RegisterCommand("stop", cmdSyncStop);
            m_commander.RegisterCommand("status", cmdSyncStatus);
            m_commander.RegisterCommand("quarkSpace", cmdSyncSetQuarks);
            m_commander.RegisterCommand("register", cmdSyncRegister);
            m_commander.RegisterCommand("startLB", cmdSyncStartLB);

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
            lock (m_client_lock)
            {
                //if (m_scriptEngineToSceneConnector != null)
                if(m_SEToSceneConnectors.Count>0)
                {
                    string authScenes = "";
                    foreach (KeyValuePair<string, ScriptEngineToSceneConnector> valPair in m_SEToSceneConnectors)
                    {
                        authScenes += valPair.Key + ", ";
                    }
                    m_log.WarnFormat(LogHeader+": Already synchronized to "+authScenes);
                    return;
                }
                //m_log.Warn("[REGION SYNC CLIENT MODULE] Starting synchronization");
                m_log.Warn(LogHeader + ": Starting RegionSyncScriptEngine");

                if (m_sceneNum > 1)
                {
                    //If there is no arguments following "sync start", then be default we will connect to one or more scenes.
                    //we need to create a connector to each authoritative scene
                    foreach (KeyValuePair<string, AuthSceneInfo> valPair in m_authScenesInfoByName)
                    {
                        string authSceneName = valPair.Key;
                        AuthSceneInfo authSceneInfo = valPair.Value;

                        //create a new connector, the local end of each connector, however, is linked to the ValidScene only, 
                        //since all objects will be contained in this scene only
                        ScriptEngineToSceneConnector scriptEngineToSceneConnector = new ScriptEngineToSceneConnector(m_scene, authSceneInfo.Addr, authSceneInfo.Port, m_debugWithViewer, authSceneName, m_syncConfig);
                        if (scriptEngineToSceneConnector.Start())
                        {
                            m_SEToSceneConnectors.Add(authSceneName, scriptEngineToSceneConnector);
                        }
                    }
                }
                else
                {
                    //Only one remote scene to connect to. Subscribe to whatever specified in the config file.
                    //List<string> quarkStringList = RegionSyncUtil.QuarkStringToStringList(m_quarkListString);
                    //InitScriptEngineToSceneConnector(quarkStringList);
                    InitScriptEngineToSceneConnector(m_subscriptionSpaceString);
                }
            }
        }

        private void SyncRegister(Object[] args)
        {
            //This should not happen. No-validLocalScene should not have register handlers for the command
            //if (m_scene.RegionInfo.RegionName != m_validLocalScene)
            //    return;

            //Registration only, no state sync'ing yet. So only start the connector for the validLocalScene. (For now, we only test this with one scene, and 
            //quarks are smaller than a 256x256 scene.
            string authSceneName = m_localToAuthSceneMapping[m_validLocalScene];
            AuthSceneInfo authSceneInfo = m_authScenesInfoByName[authSceneName];
            m_idleSEToSceneConnector = new ScriptEngineToSceneConnector(m_scene, authSceneInfo.Addr, authSceneInfo.Port, m_debugWithViewer, authSceneName, m_syncConfig);
            m_idleSEToSceneConnector.RegisterIdle();
        }

        /// <summary>
        /// The given ScriptEngineToSceneConnector, after having connected to the Scene (called its Start()), will
        /// call this function to remove it self as an idle connector, and to be recorded as one working connector.
        /// </summary>
        /// <param name="seToSceneConnector"></param>
        public void RecordSyncStartAfterLoadMigration(ScriptEngineToSceneConnector seToSceneConnector)
        {
            foreach (KeyValuePair<string, AuthSceneInfo> valPair in m_authScenesInfoByName)
            {
                string authSceneName = valPair.Key;
                AuthSceneInfo authSceneInfo = valPair.Value;

                string localScene = m_authToLocalSceneMapping[authSceneName];

                if (localScene != m_scene.RegionInfo.RegionName)
                    continue;

                if (m_SEToSceneConnectors.ContainsKey(authSceneName))
                {
                    m_log.Warn(LogHeader + ": Connector to " + authSceneName + " is already considered connected");
                    return;
                }

                m_SEToSceneConnectors.Add(authSceneName, seToSceneConnector); 
                //there should only be one element in the dictionary if we reach this loop, anyway, we break from it.
                break;
            }
            m_idleSEToSceneConnector = null;
        }

        private void SyncStartLB(Object[] args)
        {
            string authSceneName = m_localToAuthSceneMapping[m_validLocalScene];
            ScriptEngineToSceneConnector sceneConnector = m_SEToSceneConnectors[authSceneName];
            sceneConnector.SendLoadBalanceRequest();
        }

        private void SetQuarkList(Object[] args)
        {
            m_subscriptionSpaceString = (string)args[0];

            InitScriptEngineToSceneConnector(m_subscriptionSpaceString);
        }

        private void SetQuarkSize(Object[] args)
        {
            QuarkInfo.SizeX = (int)args[0];
            QuarkInfo.SizeY = (int)args[1];

        }

        private void InitScriptEngineToSceneConnector(string space)
        {
            
            foreach (KeyValuePair<string, AuthSceneInfo> valPair in m_authScenesInfoByName)
            {
                string authSceneName = valPair.Key;
                AuthSceneInfo authSceneInfo = valPair.Value;

                string localScene = m_authToLocalSceneMapping[authSceneName];

                if (localScene != m_scene.RegionInfo.RegionName)
                    continue;

                //create a new connector, the local end of each connector, however, is set of the ValidScene only, 
                //since all objects will be contained in this scene only
                ScriptEngineToSceneConnector scriptEngineToSceneConnector = new ScriptEngineToSceneConnector(m_scene, authSceneInfo.Addr, authSceneInfo.Port,
                    m_debugWithViewer, authSceneName, space, m_syncConfig);
                if (scriptEngineToSceneConnector.Start())
                {
                    m_SEToSceneConnectors.Add(authSceneName, scriptEngineToSceneConnector);
                }

                break; //there should only be one element in the dictionary if we reach this loop, anyway, we break from it.
            }
        }

        private void SyncStop(Object[] args)
        {
            lock (m_client_lock)
            {
                //if (m_scriptEngineToSceneConnector == null)
                if(m_SEToSceneConnectors.Count==0 && m_idleSEToSceneConnector==null)
                {
                    m_log.WarnFormat("[REGION SYNC SCRIPT ENGINE MODULE] Already stopped");
                    return;
                }

                if (m_SEToSceneConnectors.Count > 0)
                {
                    foreach (KeyValuePair<string, ScriptEngineToSceneConnector> valPair in m_SEToSceneConnectors)
                    {
                        ScriptEngineToSceneConnector connector = valPair.Value;
                        if (connector == null)
                        {
                            continue;
                        }
                        connector.Stop();
                    }
                    m_SEToSceneConnectors.Clear();
                }
                else if (m_idleSEToSceneConnector != null)
                {
                    m_idleSEToSceneConnector.Stop();
                    m_idleSEToSceneConnector = null;
                }

                //m_scriptEngineToSceneConnector.Stop();
                //m_scriptEngineToSceneConnector = null;
                m_log.Warn(LogHeader+": Stopping synchronization");
            }

            m_authScenesInfoByLoc.Clear();

            //save script state and stop script instances
            m_scene.EventManager.TriggerScriptEngineSyncStop();
            //remove all objects
            m_scene.DeleteAllSceneObjects();
            
        }

        private void SyncStatus(Object[] args)
        {
            lock (m_client_lock)
            {
                if (m_SEToSceneConnectors.Count == 0)
                {
                    m_log.WarnFormat("[REGION SYNC SCRIPT ENGINE MODULE] Not currently synchronized");
                    return;
                }
                m_log.WarnFormat("[REGION SYNC SCRIPT ENGINE MODULE] Synchronized");
                foreach (KeyValuePair<string, ScriptEngineToSceneConnector> pair in m_SEToSceneConnectors)
                {
                    ScriptEngineToSceneConnector sceneConnector = pair.Value;
                    sceneConnector.ReportStatus();
                }
            }
        }
        #endregion
    }
}

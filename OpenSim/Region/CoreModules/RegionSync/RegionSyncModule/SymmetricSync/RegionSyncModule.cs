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
using OpenSim.Region.Framework.Scenes.Serialization;
using log4net;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;

using Mono.Addins;
using OpenMetaverse.StructuredData;

/////////////////////////////////////////////////////////////////////////////////////////////
//KittyL: created 12/17/2010, to start DSG Symmetric Synch implementation
/////////////////////////////////////////////////////////////////////////////////////////////
namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AttachmentsModule")]
    public class RegionSyncModule : INonSharedRegionModule, IRegionSyncModule, ICommandableModule
    //public class RegionSyncModule : IRegionModule, IRegionSyncModule, ICommandableModule
    {
        #region INonSharedRegionModule

        public void Initialise(IConfigSource config)
        //public void Initialise(Scene scene, IConfigSource config)
        {
            m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            m_sysConfig = config.Configs["RegionSyncModule"];
            m_active = false;
            if (m_sysConfig == null)
            {
                m_log.Warn("[REGION SYNC MODULE] No RegionSyncModule config section found. Shutting down.");
                return;
            }
            else if (!m_sysConfig.GetBoolean("Enabled", false))
            {
                m_log.Warn("[REGION SYNC MODULE] RegionSyncModule is not enabled. Shutting down.");
                return;
            }

            m_actorID = m_sysConfig.GetString("ActorID", "");
            if (m_actorID == "")
            {
                m_log.Error("ActorID not defined in [RegionSyncModule] section in config file. Shutting down.");
                return;
            }

            m_isSyncRelay = m_sysConfig.GetBoolean("IsSyncRelay", false);
            m_isSyncListenerLocal = m_sysConfig.GetBoolean("IsSyncListenerLocal", false);

            m_active = true;

            m_log.Warn("[REGION SYNC MODULE] Initialised for actor "+ m_actorID);

            //The ActorType configuration will be read in and process by an ActorSyncModule, not here.
        }

        //Called after Initialise()
        public void AddRegion(Scene scene)
        {
            m_log.Warn(LogHeader + " AddRegion() called");

            if (!m_active)
                return;

            //connect with scene
            m_scene = scene;

            //register the module 
            m_scene.RegisterModuleInterface<IRegionSyncModule>(this);

            // Setup the command line interface
            m_scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
            InstallInterfaces();

            //Register for the OnPostSceneCreation event
            m_scene.EventManager.OnPostSceneCreation += OnPostSceneCreation;
            m_scene.EventManager.OnObjectBeingRemovedFromScene += new EventManager.ObjectBeingRemovedFromScene(RegionSyncModule_OnObjectBeingRemovedFromScene);
        }

        //Called after AddRegion() has been called for all region modules of the scene
        public void RegionLoaded(Scene scene)
        {
            m_log.Warn(LogHeader + " RegionLoaded() called");

            //If this one is configured to start a listener so that other actors can connect to form a overlay, start the listener.
            //For now, we use start topology, and ScenePersistence actor is always the one to start the listener.
            if (m_isSyncListenerLocal)
            {
                StartLocalSyncListener();
            }
            else
            {
                //Start connecting to the remote listener. TO BE IMPLEMENTED. 
                //For now, the connection will be started by manually typing in "sync start".
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

        private DSGActorTypes m_actorType = DSGActorTypes.Unknown;
        /// <summary>
        /// The type of the actor running locally. This value will be set by an ActorSyncModule, so that 
        /// no hard code needed in RegionSyncModule to recoganize the actor's type, thus make it easier
        /// to add new ActorSyncModules w/o chaning the code in RegionSyncModule.
        /// </summary>
        public DSGActorTypes DSGActorType
        {
            get { return m_actorType; }
            set { m_actorType = value; }
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

        private RegionSyncListener m_localSyncListener = null;

        // Lock is used to synchronize access to the update status and update queues
        private object m_updateSceneObjectPartLock = new object();
        private Dictionary<UUID, SceneObjectGroup> m_primUpdates = new Dictionary<UUID, SceneObjectGroup>();
        private object m_updateScenePresenceLock = new object();
        private Dictionary<UUID, ScenePresence> m_presenceUpdates = new Dictionary<UUID, ScenePresence>();
        private int m_sendingUpdates;

        public void QueueSceneObjectPartForUpdate(SceneObjectPart part)
        {
            //if the last update of the prim is caused by this actor itself, or if the actor is a relay node, then enqueue the update
            if (part.LastUpdateActorID.Equals(m_actorID) || m_isSyncRelay)
            {
                lock (m_updateSceneObjectPartLock)
                {
                    m_primUpdates[part.UUID] = part.ParentGroup;
                }
            }
        }

        public void QueueScenePresenceForTerseUpdate(ScenePresence presence)
        {
            lock (m_updateScenePresenceLock)
            {
                m_presenceUpdates[presence.UUID] = presence;
            }
        }

        public void SendSceneUpdates()
        {
            // Existing value of 1 indicates that updates are currently being sent so skip updates this pass
            if (Interlocked.Exchange(ref m_sendingUpdates, 1) == 1)
            {
                m_log.WarnFormat("[REGION SYNC SERVER MODULE] SendUpdates(): An update thread is already running.");
                return;
            }

            List<SceneObjectGroup> primUpdates;
            List<ScenePresence> presenceUpdates;

            lock (m_updateSceneObjectPartLock)
            {
                primUpdates = new List<SceneObjectGroup>(m_primUpdates.Values);
                //presenceUpdates = new List<ScenePresence>(m_presenceUpdates.Values);
                m_primUpdates.Clear();
                //m_presenceUpdates.Clear();
            }

            lock (m_updateScenePresenceLock)
            {
                presenceUpdates = new List<ScenePresence>(m_presenceUpdates.Values);
                m_presenceUpdates.Clear();
            }

            // This could be another thread for sending outgoing messages or just have the Queue functions
            // create and queue the messages directly into the outgoing server thread.
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                // Dan's note: Sending the message when it's first queued would yield lower latency but much higher load on the simulator
                // as parts may be updated many many times very quickly. Need to implement a higher resolution send in heartbeat
                foreach (SceneObjectGroup sog in primUpdates)
                {
                    //If this is a relay node, or at least one part of the object has the last update caused by this actor, then send the update
                    if (m_isSyncRelay || (!sog.IsDeleted && CheckObjectForSendingUpdate(sog)))
                    {
                        //send 
                        string sogxml = SceneObjectSerializer.ToXml2Format(sog);
                        SymmetricSyncMessage syncMsg = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.UpdatedObject, sogxml);
                        SendObjectUpdateToRelevantSyncConnectors(sog, syncMsg);
                    }
                }
                /*
                foreach (ScenePresence presence in presenceUpdates)
                {
                    try
                    {
                        if (!presence.IsDeleted)
                        {
                            
                            OSDMap data = new OSDMap(10);
                            data["id"] = OSD.FromUUID(presence.UUID);
                            // Do not include offset for appearance height. That will be handled by RegionSyncClient before sending to viewers
                            if(presence.AbsolutePosition.IsFinite())
                                data["pos"] = OSD.FromVector3(presence.AbsolutePosition);
                            else
                                data["pos"] = OSD.FromVector3(Vector3.Zero);
                            if(presence.Velocity.IsFinite())
                                data["vel"] = OSD.FromVector3(presence.Velocity);
                            else
                                data["vel"] = OSD.FromVector3(Vector3.Zero);
                            data["rot"] = OSD.FromQuaternion(presence.Rotation);
                            data["fly"] = OSD.FromBoolean(presence.Flying);
                            data["flags"] = OSD.FromUInteger((uint)presence.AgentControlFlags);
                            data["anim"] = OSD.FromString(presence.Animator.CurrentMovementAnimation);
                            // needed for a full update
                            if (presence.ParentID != presence.lastSentParentID)
                            {
                                data["coll"] = OSD.FromVector4(presence.CollisionPlane);
                                data["off"] = OSD.FromVector3(presence.OffsetPosition);
                                data["pID"] = OSD.FromUInteger(presence.ParentID);
                                presence.lastSentParentID = presence.ParentID;
                            }

                            RegionSyncMessage rsm = new RegionSyncMessage(RegionSyncMessage.MsgType.UpdatedAvatar, OSDParser.SerializeJsonString(data));
                            m_server.EnqueuePresenceUpdate(presence.UUID, rsm.ToBytes());
                           

                        }
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[REGION SYNC SERVER MODULE] Caught exception sending presence updates for {0}: {1}", presence.Name, e.Message);
                    }
                }
                 * */

                // Indicate that the current batch of updates has been completed
                Interlocked.Exchange(ref m_sendingUpdates, 0);
            });
        }

        public void SendTerrainUpdates(string lastUpdateActorID)
        {
            if(m_isSyncRelay || m_actorID.Equals(lastUpdateActorID))
            {
                //m_scene.Heightmap should have been updated already by the caller, send it out
                //SendSyncMessage(SymmetricSyncMessage.MsgType.Terrain, m_scene.Heightmap.SaveToXmlString());
                SendTerrainUpdateMessage();
            }
        }

        #endregion //IRegionSyncModule  

        #region ICommandableModule Members
        private readonly Commander m_commander = new Commander("ssync");
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
            if (args[0] == "ssync")
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
        //private RegionSyncListenerInfo m_localSyncListenerInfo 

        private HashSet<RegionSyncListenerInfo> m_remoteSyncListeners;

        private int m_syncConnectorNum = 0;

        private Scene m_scene;
        public Scene LocalScene
        {
            get { return m_scene; }
        }

        private IConfig m_sysConfig = null;
        private string LogHeader = "[REGION SYNC MODULE]";

        //The list of SyncConnectors. ScenePersistence could have multiple SyncConnectors, each connecting to a differerent actor.
        //An actor could have several SyncConnectors as well, each connecting to a ScenePersistence that hosts a portion of the objects/avatars
        //the actor operates on.
        private HashSet<SyncConnector> m_syncConnectors= new HashSet<SyncConnector>();
        private object m_syncConnectorsLock = new object();
        
        //Timers for periodically status report has not been implemented yet.
        private System.Timers.Timer m_statsTimer = new System.Timers.Timer(1000);
        private void StatsTimerElapsed(object source, System.Timers.ElapsedEventArgs e)
        {
            //TO BE IMPLEMENTED
            m_log.Warn("[REGION SYNC MODULE]: StatsTimerElapsed -- NOT yet implemented.");
        }

        private void SendObjectUpdateToRelevantSyncConnectors(SceneObjectGroup sog, SymmetricSyncMessage syncMsg)
        {
            List<SyncConnector> syncConnectors = GetSyncConnectorsForObjectUpdates(sog);

            foreach (SyncConnector connector in syncConnectors)
            {
                //string sogxml = SceneObjectSerializer.ToXml2Format(sog);
                //SymmetricSyncMessage syncMsg = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.UpdatedObject, sogxml);
                connector.EnqueueOutgoingUpdate(sog.UUID, syncMsg.ToBytes());
            }
        }

        /// <summary>
        /// Check if we need to send out an update message for the given object.
        /// </summary>
        /// <param name="sog"></param>
        /// <returns></returns>
        private bool CheckObjectForSendingUpdate(SceneObjectGroup sog)
        {
            //If any part in the object has the last update caused by this actor itself, then send the update
            foreach (SceneObjectPart part in sog.Parts)
            {
                if (part.LastUpdateActorID.Equals(m_actorID))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get the set of SyncConnectors to send updates of the given object. 
        /// </summary>
        /// <param name="sog"></param>
        /// <returns></returns>
        private List<SyncConnector> GetSyncConnectorsForObjectUpdates(SceneObjectGroup sog)
        {
            List<SyncConnector> syncConnectors = new List<SyncConnector>();
            if (m_isSyncRelay)
            {
                //This is a relay node in the synchronization overlay, forward it to all connectors. 
                //Note LastUpdateTimeStamp and LastUpdateActorID is one per SceneObjectPart, not one per SceneObjectGroup, 
                //hence an actor sending in an update on one SceneObjectPart of a SceneObjectGroup may need to know updates
                //in other parts as well, so we are sending to all connectors.
                ForEachSyncConnector(delegate(SyncConnector connector)
                {
                    syncConnectors.Add(connector);
                });
            }
            else
            {
                //This is a end node in the synchronization overlay (e.g. a non ScenePersistence actor). Get the right set of synconnectors.
                //This may go more complex when an actor connects to several ScenePersistence actors.
                ForEachSyncConnector(delegate(SyncConnector connector)
                {
                    syncConnectors.Add(connector);
                });
            }

            return syncConnectors;
        }

        //NOTE: We proably don't need to do this, and there might not be a need for OnPostSceneCreation event to let RegionSyncModule
        //      and ActorSyncModules to gain some access to each other. We'll keep it here for a while, until we are sure it's not 
        //      needed.
        //      Now the communication between RegionSyncModule and ActorSyncModules are through SceneGraph or Scene.EventManager events.
        public void OnPostSceneCreation(Scene createdScene)
        {
            //If this is the local scene the actor is working on, find out the actor type.
            if (createdScene.RegionInfo.RegionName == m_scene.RegionInfo.RegionName)
            {
                if(m_scene.ActorSyncModule == null){
                    m_log.Error(LogHeader + "interface Scene.ActorSyncModule has not been set yet");
                    return;
                }
                m_actorType = m_scene.ActorSyncModule.ActorType;
            }
        }

        private void StartLocalSyncListener()
        {
            RegionSyncListenerInfo localSyncListenerInfo = GetLocalSyncListenerInfo();
            m_localSyncListener = new RegionSyncListener(localSyncListenerInfo, this);
            m_localSyncListener.Start();
            
            //STATS TIMER: TO BE IMPLEMENTED
            //m_statsTimer.Elapsed += new System.Timers.ElapsedEventHandler(StatsTimerElapsed);
            //m_statsTimer.Start();
        }

        //Get the information for local IP:Port for listening incoming connection requests.
        //For now, we use configuration to access the information. Might be replaced by some Grid Service later on.
        private RegionSyncListenerInfo GetLocalSyncListenerInfo()
        {
            string addr = m_sysConfig.GetString("SyncListenerIPAddress", "127.0.0.1");
            int port = m_sysConfig.GetInt("SyncListenerPort", 13000);
            RegionSyncListenerInfo info = new RegionSyncListenerInfo(addr, port);

            return info;
        }

        //Get the information for remote [IP:Port] to connect to for synchronization purpose.
        //For example, an actor may need to connect to several ScenePersistence's if the objects it operates are hosted collectively
        //by these ScenePersistence.
        //For now, we use configuration to access the information. Might be replaced by some Grid Service later on.
        //And for now, we assume there is only 1 remote listener to connect to.
        private void GetRemoteSyncListenerInfo()
        {
            //For now, we assume there is only one remote listener to connect to. Later on, 
            //we may need to modify the code to read in multiple listeners.
            string addr = m_sysConfig.GetString("SyncListenerIPAddress", "127.0.0.1");
            int port = m_sysConfig.GetInt("SyncListenerPort", 13000);
            RegionSyncListenerInfo info = new RegionSyncListenerInfo(addr, port);

            m_remoteSyncListeners = new HashSet<RegionSyncListenerInfo>();
            
            m_remoteSyncListeners.Add(info);
        }

        //Start SyncListener if a listener is supposed to run on this actor; Otherwise, initiate connections to remote listeners.
        private void SyncStart(Object[] args)
        {
            if (m_actorType == DSGActorTypes.Unknown)
            {
                m_log.Error(LogHeader + ": SyncStart -- ActorType not set yet. Either it's not defined in config file (DSGActorType), or the ActorSyncModule (ScenePersistenceSyncModule, ScriptEngineSyncModule etc) has not defined it.");
                return;
            }

            if (m_isSyncListenerLocal)
            {
                if (m_localSyncListener.IsListening)
                {
                    m_log.Warn("[REGION SYNC MODULE]: RegionSyncListener is local, already started");
                }
                else
                {
                    StartLocalSyncListener();
                }
            }
            else
            {
                if (m_remoteSyncListeners == null)
                {
                    GetRemoteSyncListenerInfo();
                }
                StartSyncConnections();
                DoInitialSync();
            }
        }

        private void SyncStop(Object[] args)
        {
            if (m_isSyncListenerLocal)
            {
                if (m_localSyncListener.IsListening)
                {
                    m_localSyncListener.Shutdown();
                }
            }
            else
            {
                //Shutdown all sync connectors
                StopAllSyncConnectors();
            }

            //Trigger SyncStop event, ActorSyncModules can then take actor specific action if needed.
            //For instance, script engine will save script states
            //save script state and stop script instances
            m_scene.EventManager.TriggerOnSymmetricSyncStop();
            
        }

        private void SyncStatus(Object[] args)
        {
            //TO BE IMPLEMENTED
            m_log.Warn("[REGION SYNC MODULE]: SyncStatus() TO BE IMPLEMENTED !!!");
        }

        //Start connections to each remote listener. 
        //For now, there is only one remote listener.
        private void StartSyncConnections()
        {
            foreach (RegionSyncListenerInfo remoteListener in m_remoteSyncListeners)
            {
                SyncConnector syncConnector = new SyncConnector(m_syncConnectorNum++, remoteListener, this);
                if (syncConnector.Connect())
                {
                    syncConnector.StartCommThreads();
                    AddSyncConnector(syncConnector);
                }
            }
        }

        //To be called when a SyncConnector needs to be created by that the local listener receives a connection request
        public void AddNewSyncConnector(TcpClient tcpclient)
        {
            //Create a SynConnector due to an incoming request, and starts its communication threads
            SyncConnector syncConnector = new SyncConnector(m_syncConnectorNum++, tcpclient, this);
            syncConnector.StartCommThreads();
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

            m_log.Debug("[REGION SYNC MODULE]: new connector " + syncConnector.ConnectorNum);
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

        public void StopAllSyncConnectors()
        {
            lock (m_syncConnectorsLock)
            {
                foreach (SyncConnector syncConnector in m_syncConnectors)
                {
                    syncConnector.Shutdown();
                }

                m_syncConnectors.Clear();
            }
        }

        private void DoInitialSync()
        {
            m_scene.DeleteAllSceneObjects();
            
            SendSyncMessage(SymmetricSyncMessage.MsgType.RegionName, m_scene.RegionInfo.RegionName);
            m_log.WarnFormat("Sending region name: \"{0}\"", m_scene.RegionInfo.RegionName);

            SendSyncMessage(SymmetricSyncMessage.MsgType.GetTerrain);
            SendSyncMessage(SymmetricSyncMessage.MsgType.GetObjects);
            //Send(new RegionSyncMessage(RegionSyncMessage.MsgType.GetAvatars));

            //We'll deal with Event a bit later

            // Register for events which will be forwarded to authoritative scene
            // m_scene.EventManager.OnNewClient += EventManager_OnNewClient;
            //m_scene.EventManager.OnMakeRootAgent += EventManager_OnMakeRootAgent;
            //m_scene.EventManager.OnMakeChildAgent += EventManager_OnMakeChildAgent;
            //m_scene.EventManager.OnClientClosed += new EventManager.ClientClosed(RemoveLocalClient);
        }

        /// <summary>
        /// This function will enqueue a message for each SyncConnector in the connector's outgoing queue.
        /// Each SyncConnector has a SendLoop thread to send the messages in its outgoing queue.
        /// </summary>
        /// <param name="msgType"></param>
        /// <param name="data"></param>
        private void SendSyncMessage(SymmetricSyncMessage.MsgType msgType, string data)
        {
            //See RegionSyncClientView for initial implementation by Dan Lake

            SymmetricSyncMessage msg = new SymmetricSyncMessage(msgType, data);
            ForEachSyncConnector(delegate(SyncConnector syncConnector)
            {
                syncConnector.Send(msg);
            });
        }

        private void SendSyncMessage(SymmetricSyncMessage.MsgType msgType)
        {
            //See RegionSyncClientView for initial implementation by Dan Lake

            SendSyncMessage(msgType, "");
        }

        public void ForEachSyncConnector(Action<SyncConnector> action)
        {
            List<SyncConnector> closed = null;
            foreach (SyncConnector syncConnector in m_syncConnectors)
            {
                // If connected, apply the action
                if (syncConnector.Connected)
                {
                    action(syncConnector);
                }                
                    // Else, remove the SyncConnector from the list
                else
                {
                    if (closed == null)
                        closed = new List<SyncConnector>();
                    closed.Add(syncConnector);
                }
            }

            if (closed != null)
            {
                foreach (SyncConnector connector in closed)
                {
                    RemoveSyncConnector(connector);
                }
            }
        }



        /// <summary>
        /// The handler for processing incoming sync messages.
        /// </summary>
        /// <param name="msg"></param>
        public void HandleIncomingMessage(SymmetricSyncMessage msg)
        {
            switch (msg.Type)
            {
                case SymmetricSyncMessage.MsgType.GetTerrain:
                    {
                        //SendSyncMessage(SymmetricSyncMessage.MsgType.Terrain, m_scene.Heightmap.SaveToXmlString());
                        SendTerrainUpdateMessage();
                        return;
                    }
                case SymmetricSyncMessage.MsgType.Terrain:
                    {
                        /*
                        m_scene.Heightmap.LoadFromXmlString(Encoding.ASCII.GetString(msg.Data, 0, msg.Length));
                        //Inform the terrain module that terrain has been updated
                        m_scene.RequestModuleInterface<ITerrainModule>().TaintTerrain();
                        m_log.Debug(LogHeader+": Synchronized terrain");
                         * */
                        HandleTerrainUpdateMessage(msg);
                        return;
                    }
                case SymmetricSyncMessage.MsgType.GetObjects:
                    {
                        EntityBase[] entities = m_scene.GetEntities(); 
                        foreach (EntityBase e in entities)
                        {
                            if (e is SceneObjectGroup)
                            {
                                string sogxml = SceneObjectSerializer.ToXml2Format((SceneObjectGroup)e);
                                SendSyncMessage(SymmetricSyncMessage.MsgType.NewObject, sogxml);

                                //m_log.Debug(LogHeader + ": " + sogxml);
                            }
                        }
                        return;
                    }
                case SymmetricSyncMessage.MsgType.NewObject:
                case SymmetricSyncMessage.MsgType.UpdatedObject:
                    {
                        HandleAddOrUpdateObjectBySynchronization(msg);
                        //HandleAddNewObject(sog);
                        return;
                    }
                case SymmetricSyncMessage.MsgType.RemovedObject:
                    {
                        HandleRemovedObject(msg);
                        return;
                    }
                default:
                    return;
            }
        }

        private void HandleTerrainUpdateMessage(SymmetricSyncMessage msg)
        {
            // Get the data from message and error check
            OSDMap data = DeserializeMessage(msg);

            if (data == null)
            {
                SymmetricSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                return;
            }

            string msgData = data["terrain"].AsString();
            long lastUpdateTimeStamp = data["actorID"].AsLong();
            string lastUpdateActorID = data["timeStamp"].AsString();

            //set new terrain
            m_scene.Heightmap.LoadFromXmlString(msgData);
            m_scene.RequestModuleInterface<ITerrainModule>().TaintTerrianBySynchronization(lastUpdateTimeStamp, lastUpdateActorID); ;
            m_log.Debug(LogHeader + ": Synchronized terrain");
        }

        private void HandleAddOrUpdateObjectBySynchronization(SymmetricSyncMessage msg)
        {
            string sogxml = Encoding.ASCII.GetString(msg.Data, 0, msg.Length);
            SceneObjectGroup sog = SceneObjectSerializer.FromXml2Format(sogxml);

            if (sog.IsDeleted)
            {
                SymmetricSyncMessage.HandleTrivial(LogHeader, msg, String.Format("Ignoring update on deleted object, UUID: {0}.", sog.UUID));
                return;
            }
            else
            {
                Scene.ObjectUpdateResult updateResult = m_scene.AddOrUpdateObjectBySynchronization(sog);

                //if (added)
                switch (updateResult)
                {
                    case Scene.ObjectUpdateResult.New:
                        m_log.DebugFormat("[{0} Object \"{1}\" ({1}) ({2}) added.", LogHeader, sog.Name, sog.UUID.ToString(), sog.LocalId.ToString());
                        break;
                    case Scene.ObjectUpdateResult.Updated:
                        m_log.DebugFormat("[{0} Object \"{1}\" ({1}) ({2}) updated.", LogHeader, sog.Name, sog.UUID.ToString(), sog.LocalId.ToString());
                        break;
                    case Scene.ObjectUpdateResult.Error:
                        m_log.WarnFormat("[{0} Object \"{1}\" ({1}) ({2}) -- add or update ERROR.", LogHeader, sog.Name, sog.UUID.ToString(), sog.LocalId.ToString());
                        break;
                    case Scene.ObjectUpdateResult.Unchanged:
                        m_log.DebugFormat("[{0} Object \"{1}\" ({1}) ({2}) unchanged after receiving an update.", LogHeader, sog.Name, sog.UUID.ToString(), sog.LocalId.ToString());
                        break;
                }
            }
        }

        private void SendTerrainUpdateMessage()
        {
            string msgData = m_scene.Heightmap.SaveToXmlString();
            long lastUpdateTimeStamp;
            string lastUpdateActorID;
            m_scene.RequestModuleInterface<ITerrainModule>().GetSyncInfo(out lastUpdateTimeStamp, out lastUpdateActorID);

            OSDMap data = new OSDMap(3);
            data["terrain"] = OSD.FromString(msgData);
            data["actorID"] = OSD.FromString(lastUpdateActorID);
            data["timeStamp"] = OSD.FromLong(lastUpdateTimeStamp);

            SendSyncMessage(SymmetricSyncMessage.MsgType.Terrain, OSDParser.SerializeJsonString(data));
        }

        private void HandleRemovedObject(SymmetricSyncMessage msg)
        {
            // Get the data from message and error check
            OSDMap data = DeserializeMessage(msg);

            if (data == null)
            {

                SymmetricSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                return;
            }

            // Get the parameters from data
            //ulong regionHandle = data["regionHandle"].AsULong();
            //uint localID = data["UUID"].AsUInteger();
            UUID sogUUID = data["UUID"].AsUUID();

            SceneObjectGroup sog = m_scene.SceneGraph.GetGroupByPrim(sogUUID);
            if (sog != null)
            {
                m_scene.DeleteSceneObjectBySynchronization(sog);
            }
        }

        HashSet<string> exceptions = new HashSet<string>();
        private OSDMap DeserializeMessage(SymmetricSyncMessage msg)
        {
            OSDMap data = null;
            try
            {
                data = OSDParser.DeserializeJson(Encoding.ASCII.GetString(msg.Data, 0, msg.Length)) as OSDMap;
            }
            catch (Exception e)
            {
                lock (exceptions)
                    // If this is a new message, then print the underlying data that caused it
                    if (!exceptions.Contains(e.Message))
                        m_log.Error(LogHeader + " " + Encoding.ASCII.GetString(msg.Data, 0, msg.Length));
                data = null;
            }
            return data;
        }

        private void HandleAddNewObject(SceneObjectGroup sog)
        {
            //RegionSyncModule only add object to SceneGraph. Any actor specific actions will be implemented 
            //by each ActorSyncModule, which would be triggered its subcription to event SceneGraph.OnObjectCreate.
            bool attachToBackup = false;

            if (m_scene.AddNewSceneObject(sog, attachToBackup))
            {
                m_log.Debug(LogHeader + ": added obj " + sog.UUID);
            }
        }


        /// <summary>
        /// Send a sync message to remove the given objects in all connected actors, if this is a relay node. 
        /// UUID is used for identified a removed object.
        /// </summary>
        /// <param name="sog"></param>
        private void RegionSyncModule_OnObjectBeingRemovedFromScene(SceneObjectGroup sog)
        {
            //m_log.DebugFormat("RegionSyncModule_OnObjectBeingRemovedFromScene called at time {0}:{1}:{2}", DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);

            //Only send the message out if this is a relay node for sync messages, or this actor caused deleting the object
            if (m_isSyncRelay || CheckObjectForSendingUpdate(sog))
            {
                OSDMap data = new OSDMap(1);
                //data["regionHandle"] = OSD.FromULong(regionHandle);
                //data["localID"] = OSD.FromUInteger(sog.LocalId);
                data["UUID"] = OSD.FromUUID(sog.UUID);

                SymmetricSyncMessage rsm = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.RemovedObject, OSDParser.SerializeJsonString(data));
                SendObjectUpdateToRelevantSyncConnectors(sog, rsm);
            }


        }

        #endregion //RegionSyncModule members and functions

    }

    public class RegionSyncListenerInfo
    {
        public IPAddress Addr;
        public int Port;

        //TO ADD: reference to RegionInfo that describes the shape/size of the space that the listener is associated with

        public RegionSyncListenerInfo(string addr, int port)
        {
            Addr = IPAddress.Parse(addr);
            Port = port;
        }
    }

    public class RegionSyncListener
    {
        private RegionSyncListenerInfo m_listenerInfo;
        private RegionSyncModule m_regionSyncModule;
        private ILog m_log;
        private string LogHeader = "[RegionSyncListener]";

        // The listener and the thread which listens for sync connection requests
        private TcpListener m_listener;
        private Thread m_listenerThread;
        
        private bool m_isListening = false;
        public bool IsListening
        {
            get { return m_isListening; }
        }

        public RegionSyncListener(RegionSyncListenerInfo listenerInfo, RegionSyncModule regionSyncModule)
        {
            m_listenerInfo = listenerInfo;
            m_regionSyncModule = regionSyncModule;

            m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        }

        // Start the listener
        public void Start()
        {
            m_listenerThread = new Thread(new ThreadStart(Listen));
            m_listenerThread.Name = "RegionSyncListener";
            m_log.WarnFormat(LogHeader+" Starting {0} thread", m_listenerThread.Name);
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

        // Listen for connections from a new SyncConnector
        // When connected, start the ReceiveLoop for the new client
        private void Listen()
        {
            m_listener = new TcpListener(m_listenerInfo.Addr, m_listenerInfo.Port);

            try
            {
                // Start listening for clients
                m_listener.Start();
                while (true)
                {
                    // *** Move/Add TRY/CATCH to here, but we don't want to spin loop on the same error
                    m_log.WarnFormat("[REGION SYNC SERVER] Listening for new connections on {0}:{1}...", m_listenerInfo.Addr.ToString(), m_listenerInfo.Port.ToString());
                    TcpClient tcpclient = m_listener.AcceptTcpClient();

                    //Create a SynConnector and starts it communication threads
                    m_regionSyncModule.AddNewSyncConnector(tcpclient);
                }
            }
            catch (SocketException e)
            {
                m_log.WarnFormat("[REGION SYNC SERVER] [Listen] SocketException: {0}", e);
            }
        }

    }
 
}
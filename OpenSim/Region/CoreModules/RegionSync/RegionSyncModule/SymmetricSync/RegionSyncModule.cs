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
using OpenSim.Services.Interfaces;
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

            LogHeader += "-Actor " + m_actorID;
            m_log.Warn("[REGION SYNC MODULE] Initialised for actor "+ m_actorID);

            //The ActorType configuration will be read in and process by an ActorSyncModule, not here.
        }

        //Called after Initialise()
        public void AddRegion(Scene scene)
        {
            //m_log.Warn(LogHeader + " AddRegion() called");

            if (!m_active)
                return;

            //connect with scene
            m_scene = scene;

            //register the module 
            m_scene.RegisterModuleInterface<IRegionSyncModule>(this);

            // Setup the command line interface
            m_scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
            InstallInterfaces();

            //Register for local Scene events
            m_scene.EventManager.OnPostSceneCreation += OnPostSceneCreation;
            //m_scene.EventManager.OnObjectBeingRemovedFromScene += new EventManager.ObjectBeingRemovedFromScene(RegionSyncModule_OnObjectBeingRemovedFromScene);

            LogHeader += "-LocalRegion " + scene.RegionInfo.RegionName;
        }

        //Called after AddRegion() has been called for all region modules of the scene
        public void RegionLoaded(Scene scene)
        {
            //m_log.Warn(LogHeader + " RegionLoaded() called");

            /*
            //If this one is configured to start a listener so that other actors can connect to form a overlay, start the listener.
            //For now, we use the star topology, and ScenePersistence actor is always the one to start the listener.
            if (m_isSyncListenerLocal)
            {
                StartLocalSyncListener();
            }
            else
            {
                //Start connecting to the remote listener. TO BE IMPLEMENTED. 
                //For now, the connection will be started by manually typing in "sync start".

            }
             * */

            //Start symmetric synchronization initialization automatically
            //SyncStart(null);
            
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
        private bool m_synced = false;

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

        //SendSceneUpdates put each update into an outgoing queue of each SyncConnector
        public void SendSceneUpdates()
        {
            // Existing value of 1 indicates that updates are currently being sent so skip updates this pass
            if (Interlocked.Exchange(ref m_sendingUpdates, 1) == 1)
            {
                m_log.WarnFormat("[REGION SYNC MODULE] SendUpdates(): An update thread is already running.");
                return;
            }

            List<SceneObjectGroup> primUpdates=null;
            List<ScenePresence> presenceUpdates=null;

            if (m_primUpdates.Count > 0)
            {
                lock (m_updateSceneObjectPartLock)
                {
                    primUpdates = new List<SceneObjectGroup>(m_primUpdates.Values);
                    //presenceUpdates = new List<ScenePresence>(m_presenceUpdates.Values);
                    m_primUpdates.Clear();
                    //m_presenceUpdates.Clear();
                }
            }

            if (m_presenceUpdates.Count > 0)
            {
                lock (m_updateScenePresenceLock)
                {
                    presenceUpdates = new List<ScenePresence>(m_presenceUpdates.Values);
                    m_presenceUpdates.Clear();
                }
            }

            if (primUpdates != null || presenceUpdates != null)
            {
                // This could be another thread for sending outgoing messages or just have the Queue functions
                // create and queue the messages directly into the outgoing server thread.
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    // Dan's note: Sending the message when it's first queued would yield lower latency but much higher load on the simulator
                    // as parts may be updated many many times very quickly. Need to implement a higher resolution send in heartbeat

                    if (primUpdates != null)
                    {
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
                    }
                    /*
                    if(presenceUpdates!=null){
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
                            m_log.ErrorFormat("[REGION SYNC MODULE] Caught exception sending presence updates for {0}: {1}", presence.Name, e.Message);
                        }
                    }}
                     * */

                    // Indicate that the current batch of updates has been completed
                    Interlocked.Exchange(ref m_sendingUpdates, 0);
                });
            }
            else
            {
                Interlocked.Exchange(ref m_sendingUpdates, 0);
            }
        }

        //The following Sendxxx calls,send out a message immediately, w/o putting it in the SyncConnector's outgoing queue.
        //May need some optimization there on the priorities.

        public void SendTerrainUpdates(string lastUpdateActorID)
        {
            if(m_isSyncRelay || m_actorID.Equals(lastUpdateActorID))
            {
                //m_scene.Heightmap should have been updated already by the caller, send it out
                //SendSyncMessage(SymmetricSyncMessage.MsgType.Terrain, m_scene.Heightmap.SaveToXmlString());
                SendTerrainUpdateMessage();
            }
        }

        /// <summary>
        /// Send a sync message to remove the given objects in all connected actors. 
        /// UUID is used for identified a removed object. This function now should
        /// only be triggered by an object removal that is initiated locally.
        /// </summary>
        /// <param name="sog"></param>
        //private void RegionSyncModule_OnObjectBeingRemovedFromScene(SceneObjectGroup sog)
        public void SendDeleteObject(SceneObjectGroup sog, bool softDelete)
        {
            m_log.DebugFormat("SendDeleteObject called for object {0}", sog.UUID);

            //Only send the message out if this is a relay node for sync messages, or this actor caused deleting the object
            //if (m_isSyncRelay || CheckObjectForSendingUpdate(sog))


            OSDMap data = new OSDMap();
            //data["regionHandle"] = OSD.FromULong(regionHandle);
            //data["localID"] = OSD.FromUInteger(sog.LocalId);
            data["UUID"] = OSD.FromUUID(sog.UUID);
            data["actorID"] = OSD.FromString(m_actorID);
            data["softDelete"] = OSD.FromBoolean(softDelete);

            SymmetricSyncMessage rsm = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.RemovedObject, OSDParser.SerializeJsonString(data));
            SendObjectUpdateToRelevantSyncConnectors(sog, rsm);
        }


        public void SendLinkObject(SceneObjectGroup linkedGroup, SceneObjectPart root, List<SceneObjectPart> children)
        {
            if(children.Count==0) return;

            OSDMap data = new OSDMap();
            string sogxml = SceneObjectSerializer.ToXml2Format(linkedGroup);
            data["linkedGroup"]=OSD.FromString(sogxml);
            data["rootID"] = OSD.FromUUID(root.UUID);
            data["partCount"] = OSD.FromInteger(children.Count);
            data["actorID"] = OSD.FromString(m_actorID);
            int partNum = 0;
            foreach(SceneObjectPart part in children){
                string partTempID = "part"+partNum;
                data[partTempID] = OSD.FromUUID(part.UUID);
                partNum++;
            }

            SymmetricSyncMessage rsm = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.LinkObject, OSDParser.SerializeJsonString(data));
            SendObjectUpdateToRelevantSyncConnectors(linkedGroup, rsm);
        }

        public void SendDeLinkObject(List<SceneObjectPart> prims, List<SceneObjectGroup> beforeDelinkGroups, List<SceneObjectGroup> afterDelinkGroups)
        {
            if (prims.Count==0 || beforeDelinkGroups.Count==0) return;

            OSDMap data = new OSDMap();
            data["partCount"] = OSD.FromInteger(prims.Count);
            int partNum = 0;
            foreach (SceneObjectPart part in prims)
            {
                string partTempID = "part" + partNum;
                data[partTempID] = OSD.FromUUID(part.UUID);
                partNum++;
            }
            //We also include the IDs of beforeDelinkGroups, for now it is more for sanity checking at the receiving end, so that the receiver 
            //could make sure its delink starts with the same linking state of the groups/prims.
            data["beforeGroupsCount"] = OSD.FromInteger(beforeDelinkGroups.Count);
            int groupNum = 0;
            foreach (SceneObjectGroup affectedGroup in beforeDelinkGroups)
            {
                string groupTempID = "beforeGroup" + groupNum;
                data[groupTempID] = OSD.FromUUID(affectedGroup.UUID);
                groupNum++;
            }

            //include the property values of each object after delinking, for synchronizing the values
            data["afterGroupsCount"] = OSD.FromInteger(afterDelinkGroups.Count);
            groupNum = 0;
            foreach (SceneObjectGroup afterGroup in afterDelinkGroups)
            {
                string groupTempID = "afterGroup" + groupNum;
                string sogxml = SceneObjectSerializer.ToXml2Format(afterGroup);
                data[groupTempID] = OSD.FromString(sogxml);
                groupNum++;
            }

            SymmetricSyncMessage rsm = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.DelinkObject, OSDParser.SerializeJsonString(data));
            SendDelinkObjectToRelevantSyncConnectors(beforeDelinkGroups, rsm);
        }

        public void PublishSceneEvent(EventManager.EventNames ev, Object[] evArgs)
        {
            switch (ev)
            {
                case EventManager.EventNames.NewScript:
                    if (evArgs.Length < 3)
                    {
                        m_log.Error(LogHeader + " not enough event args for NewScript");
                        return;
                    }
                    OnLocalNewScript((UUID)evArgs[0], (SceneObjectPart)evArgs[1], (UUID)evArgs[2]);
                    return;
                case EventManager.EventNames.UpdateScript:
                    if (evArgs.Length < 5)
                    {
                        m_log.Error(LogHeader + " not enough event args for UpdateScript");
                        return;
                    }
                    OnLocalUpdateScript((UUID)evArgs[0], (UUID)evArgs[1], (UUID)evArgs[2], (bool)evArgs[3], (UUID)evArgs[4]);
                    return;
                case EventManager.EventNames.ScriptReset:
                    if (evArgs.Length < 2)
                    {
                        m_log.Error(LogHeader + " not enough event args for ScriptReset");
                        return;
                    }
                    OnLocalScriptReset((uint)evArgs[0], (UUID)evArgs[1]);
                    return;
                case EventManager.EventNames.ChatFromClient:
                    if (evArgs.Length < 2)
                    {
                        m_log.Error(LogHeader + " not enough event args for ChatFromClient");
                        return;
                    }
                    OnLocalChatFromClient(evArgs[0], (OSChatMessage)evArgs[1]);
                    return;
                case EventManager.EventNames.ChatFromWorld:
                    if (evArgs.Length < 2)
                    {
                        m_log.Error(LogHeader + " not enough event args for ChatFromWorld");
                        return;
                    }
                    OnLocalChatFromWorld(evArgs[0], (OSChatMessage)evArgs[1]);
                    return;
                case EventManager.EventNames.ObjectGrab:
                    OnLocalGrabObject((uint)evArgs[0], (uint)evArgs[1], (Vector3)evArgs[2], (IClientAPI)evArgs[3], (SurfaceTouchEventArgs)evArgs[4]);
                    return;
                case EventManager.EventNames.ObjectGrabbing:
                    OnLocalObjectGrabbing((uint)evArgs[0], (uint)evArgs[1], (Vector3)evArgs[2], (IClientAPI)evArgs[3], (SurfaceTouchEventArgs)evArgs[4]);
                    return;
                case EventManager.EventNames.ObjectDeGrab:
                    OnLocalDeGrabObject((uint)evArgs[0], (uint)evArgs[1], (IClientAPI)evArgs[2], (SurfaceTouchEventArgs)evArgs[3]);
                    return;
                default:
                    return;
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

        private static int PortUnknown = -1;
        private static string IPAddrUnknown = String.Empty;

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

        //seq number for scene events that are sent out to other actors
        private ulong m_eventSeq = 0;

        //Timers for periodically status report has not been implemented yet.
        private System.Timers.Timer m_statsTimer = new System.Timers.Timer(1000);
        private void StatsTimerElapsed(object source, System.Timers.ElapsedEventArgs e)
        {
            //TO BE IMPLEMENTED
            m_log.Warn("[REGION SYNC MODULE]: StatsTimerElapsed -- NOT yet implemented.");
        }

        //Object updates are sent by enqueuing into each connector's outQueue.
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

        private void SendDelinkObjectToRelevantSyncConnectors(List<SceneObjectGroup> beforeDelinkGroups, SymmetricSyncMessage syncMsg)
        {
            HashSet<int> syncConnectorsSent = new HashSet<int>();

            foreach (SceneObjectGroup sog in beforeDelinkGroups)
            {
                List<SyncConnector> syncConnectors = GetSyncConnectorsForObjectUpdates(sog);
                foreach (SyncConnector connector in syncConnectors)
                {
                    if (!syncConnectorsSent.Contains(connector.ConnectorNum))
                    {
                        m_log.Debug(LogHeader + " send DeLinkObject to " + connector.Description);
                        connector.EnqueueOutgoingUpdate(sog.UUID, syncMsg.ToBytes());
                        syncConnectorsSent.Add(connector.ConnectorNum);
                    }
                }
            }
        }

        //Events are send out right away, without being put into the connector's outQueue first. 
        //May need a better method for managing the outgoing messages (i.e. prioritizing object updates and events)
        private void SendSceneEventToRelevantSyncConnectors(string init_actorID, SymmetricSyncMessage rsm)
        {
            List<SyncConnector> syncConnectors = GetSyncConnectorsForSceneEvents(init_actorID, rsm);

            foreach (SyncConnector connector in syncConnectors)
            {
                connector.Send(rsm);
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

        /// <summary>
        /// Get the set of SyncConnectors to send certain scene events. 
        /// </summary>
        /// <param name="sog"></param>
        /// <returns></returns>
        private List<SyncConnector> GetSyncConnectorsForSceneEvents(string init_actorID, SymmetricSyncMessage rsm)
        {
            List<SyncConnector> syncConnectors = new List<SyncConnector>();
            if (m_isSyncRelay)
            {
                //This is a relay node in the synchronization overlay, forward it to all connectors, except the one that sends in the event
                ForEachSyncConnector(delegate(SyncConnector connector)
                {
                    if (connector.OtherSideActorID != init_actorID)
                    {
                        syncConnectors.Add(connector);
                    }
                });
            }
            else
            {
                //This is a end node in the synchronization overlay (e.g. a non ScenePersistence actor). Get the right set of synconnectors.
                //For now, there is only one syncconnector that connects to ScenePersistence, due to the star topology.
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

            //Start symmetric synchronization initialization automatically
            SyncStart(null);
        }

        private void StartLocalSyncListener()
        {
            RegionSyncListenerInfo localSyncListenerInfo = GetLocalSyncListenerInfo();

            if (localSyncListenerInfo!=null)
            {
                m_log.Warn(LogHeader + " Starting SyncListener");
                m_localSyncListener = new RegionSyncListener(localSyncListenerInfo, this);
                m_localSyncListener.Start();
            }
            
            //STATS TIMER: TO BE IMPLEMENTED
            //m_statsTimer.Elapsed += new System.Timers.ElapsedEventHandler(StatsTimerElapsed);
            //m_statsTimer.Start();
        }

        //Get the information for local IP:Port for listening incoming connection requests.
        //For now, we use configuration to access the information. Might be replaced by some Grid Service later on.
        private RegionSyncListenerInfo GetLocalSyncListenerInfo()
        {
            m_log.Debug(LogHeader + ": Reading in " + m_scene.RegionInfo.RegionName + "_SyncListenerIPAddress" + " and " + m_scene.RegionInfo.RegionName + "_SyncListenerPort");

            //string addr = m_sysConfig.GetString(m_scene.RegionInfo.RegionName+"_SyncListenerIPAddress", IPAddrUnknown);
            //int port = m_sysConfig.GetInt(m_scene.RegionInfo.RegionName+"_SyncListenerPort", PortUnknown);

            string addr = m_scene.RegionInfo.SyncServerAddress;
            int port = m_scene.RegionInfo.SyncServerPort;

            m_log.Warn(LogHeader + ": listener addr: " + addr + ", port: " + port);

            if (!addr.Equals(IPAddrUnknown) && port != PortUnknown)
            {
                RegionSyncListenerInfo info = new RegionSyncListenerInfo(addr, port);

                // remove any cruft from previous runs
                m_scene.GridService.CleanUpEndpoint(m_scene.RegionInfo.RegionID.ToString());
                // Register the endpoint and quark and persistence actor for this simulator instance
                GridEndpointInfo gei = new GridEndpointInfo();
                gei.syncServerID = m_scene.RegionInfo.RegionID.ToString();
                gei.address = m_scene.RegionInfo.SyncServerAddress;
                gei.port = (uint)m_scene.RegionInfo.SyncServerPort;
                if (!m_scene.GridService.RegisterEndpoint(gei))
                {
                    m_log.ErrorFormat("{0}: Failure registering endpoint", LogHeader);
                }
                if (!m_scene.GridService.RegisterActor(m_scene.RegionInfo.RegionID.ToString(),
                                "scene_persistence", m_scene.RegionInfo.RegionID.ToString()))
                {
                    m_log.ErrorFormat("{0}: Failure registering actor", LogHeader);
                }
                if (!m_scene.GridService.RegisterQuark(m_scene.RegionInfo.RegionID.ToString(),
                            m_scene.RegionInfo.SyncQuarkLocationX, m_scene.RegionInfo.SyncQuarkLocationY))
                {
                    m_log.ErrorFormat("{0}: Failure registering quark", LogHeader);
                }

                return info;
            }

            return null;
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
            //string addr = m_sysConfig.GetString(m_scene.RegionInfo.RegionName + "_SyncListenerIPAddress", IPAddrUnknown);
            //int port = m_sysConfig.GetInt(m_scene.RegionInfo.RegionName + "_SyncListenerPort", PortUnknown);

            string addr = m_scene.RegionInfo.SyncServerAddress;
            int port = m_scene.RegionInfo.SyncServerPort;

            // if the address is not specified in the region configuration file, get it from the grid service
            if (addr.Equals(IPAddrUnknown))
            {
                List<GridEndpointInfo> lgei = m_scene.GridService.LookupQuark(
                        m_scene.RegionInfo.SyncQuarkLocationX, m_scene.RegionInfo.SyncQuarkLocationY, "scene_persistence");
                if (lgei == null || lgei.Count != 1)
                {
                    m_log.ErrorFormat("{0}: Failed to find quark persistence actor", LogHeader);
                    addr = IPAddrUnknown;
                    port = PortUnknown;
                }
                else
                {
                    GridEndpointInfo gei = lgei[0];
                    addr = gei.address;
                    port = (int)gei.port;
                    m_log.WarnFormat("{0}: Found quark ({1}/{2}) persistence actor at {3}:{4}", LogHeader,
                            m_scene.RegionInfo.SyncQuarkLocationX, m_scene.RegionInfo.SyncQuarkLocationY,
                            addr, port.ToString());
                }
            }

            if (!addr.Equals(IPAddrUnknown) && port != PortUnknown)
            {
                RegionSyncListenerInfo info = new RegionSyncListenerInfo(addr, port);
                m_remoteSyncListeners = new HashSet<RegionSyncListenerInfo>();
                m_remoteSyncListeners.Add(info);
            }
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
                if (m_localSyncListener!=null && m_localSyncListener.IsListening)
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
                if (StartSyncConnections())
                {
                    DoInitialSync();
                }
            }
        }

        private void SyncStop(Object[] args)
        {
            if (m_isSyncListenerLocal)
            {
                if (m_localSyncListener!=null && m_localSyncListener.IsListening)
                {
                    m_localSyncListener.Shutdown();
                    //Trigger SyncStop event, ActorSyncModules can then take actor specific action if needed.
                    //For instance, script engine will save script states
                    //save script state and stop script instances
                    m_scene.EventManager.TriggerOnSymmetricSyncStop();
                }
            }
            else
            {
                //Shutdown all sync connectors
                if (m_synced)
                {
                    StopAllSyncConnectors();
                    m_synced = false;

                    //Trigger SyncStop event, ActorSyncModules can then take actor specific action if needed.
                    //For instance, script engine will save script states
                    //save script state and stop script instances
                    m_scene.EventManager.TriggerOnSymmetricSyncStop();
                }
            }


            
        }

        private void SyncStatus(Object[] args)
        {
            //TO BE IMPLEMENTED
            m_log.Warn("[REGION SYNC MODULE]: SyncStatus() TO BE IMPLEMENTED !!!");
        }

        //Start connections to each remote listener. 
        //For now, there is only one remote listener.
        private bool StartSyncConnections()
        {
            if (m_remoteSyncListeners == null)
            {
                m_log.Error(LogHeader + " SyncListener's address or port has not been configured.");
                return false;
            }

            if (m_synced)
            {
                m_log.Warn(LogHeader + ": Already synced.");
                return false;
            }

            foreach (RegionSyncListenerInfo remoteListener in m_remoteSyncListeners)
            {
                SyncConnector syncConnector = new SyncConnector(m_syncConnectorNum++, remoteListener, this);
                if (syncConnector.Connect())
                {
                    syncConnector.StartCommThreads();
                    AddSyncConnector(syncConnector);
                }
            }

            m_synced = true;

            return true;
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

            SendSyncMessage(SymmetricSyncMessage.MsgType.ActorID, m_actorID);

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
        /// This function will send out the sync message right away, without putting it into the SyncConnector's queue.
        /// Should only be called for infrequent or high prority messages.
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
        /// <param name="senderActorID">ActorID of the sender</param>
        public void HandleIncomingMessage(SymmetricSyncMessage msg, string senderActorID)
        {
            //Added senderActorID, so that we don't have to include actorID in sync messages -- TODO
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
                        HandleTerrainUpdateMessage(msg, senderActorID);
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
                        HandleAddOrUpdateObjectBySynchronization(msg, senderActorID);
                        //HandleAddNewObject(sog);
                        return;
                    }
                case SymmetricSyncMessage.MsgType.RemovedObject:
                    {
                        HandleRemovedObject(msg, senderActorID);
                        return;
                    }
                case SymmetricSyncMessage.MsgType.LinkObject:
                    {
                        HandleLinkObject(msg, senderActorID);
                        return;
                    }
                case SymmetricSyncMessage.MsgType.DelinkObject:
                    {
                        HandleDelinkObject(msg, senderActorID);
                        return;
                    }
                    //EVENTS PROCESSING
                case SymmetricSyncMessage.MsgType.NewScript:
                case SymmetricSyncMessage.MsgType.UpdateScript:
                case SymmetricSyncMessage.MsgType.ScriptReset:
                case SymmetricSyncMessage.MsgType.ChatFromClient:
                case SymmetricSyncMessage.MsgType.ChatFromWorld:
                case SymmetricSyncMessage.MsgType.ObjectGrab:
                case SymmetricSyncMessage.MsgType.ObjectGrabbing:
                case SymmetricSyncMessage.MsgType.ObjectDeGrab:
                    {
                        HandleRemoteEvent(msg, senderActorID);
                        return;
                    }
                default:
                    return;
            }
        }

        private void HandleTerrainUpdateMessage(SymmetricSyncMessage msg, string senderActorID)
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

        private void HandleAddOrUpdateObjectBySynchronization(SymmetricSyncMessage msg, string senderActorID)
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
                        //m_log.DebugFormat("[{0} Object \"{1}\" ({1}) ({2}) unchanged after receiving an update.", LogHeader, sog.Name, sog.UUID.ToString(), sog.LocalId.ToString());
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

        private void HandleRemovedObject(SymmetricSyncMessage msg, string senderActorID)
        {
            // Get the data from message and error check
            OSDMap data = DeserializeMessage(msg);

            if (data == null)
            {

                SymmetricSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                return;
            }

            UUID sogUUID = data["UUID"].AsUUID();
            string init_actorID = data["actorID"].AsString();
            bool softDelete = data["softDelete"].AsBoolean();

            SceneObjectGroup sog = m_scene.SceneGraph.GetGroupByPrim(sogUUID);

            if (sog != null)
            {
                if (!softDelete)
                {
                    m_log.Debug(LogHeader + " hard delete object " + sog.UUID);
                    m_scene.DeleteSceneObjectBySynchronization(sog);
                }
                else
                {
                    m_log.Debug(LogHeader + " soft delete object " + sog.UUID);
                    m_scene.UnlinkSceneObject(sog, true);
                }
            }

            //if this is a relay node, forwards the event
            if (m_isSyncRelay)
            {
                //SendSceneEventToRelevantSyncConnectors(init_actorID, msg);
                SendSceneEventToRelevantSyncConnectors(senderActorID, msg);
            }
        }

        private void HandleLinkObject(SymmetricSyncMessage msg, string senderActorID)
        {
            // Get the data from message and error check
            OSDMap data = DeserializeMessage(msg);
            if (data == null)
            {
                SymmetricSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                return;
            }

            //string init_actorID = data["actorID"].AsString();
            string sogxml = data["linkedGroup"].AsString();
            SceneObjectGroup linkedGroup = SceneObjectSerializer.FromXml2Format(sogxml);
            UUID rootID = data["rootID"].AsUUID();
            int partCount = data["partCount"].AsInteger();
            List<UUID> childrenIDs = new List<UUID>();

            for (int i = 0; i < partCount; i++)
            {
                string partTempID = "part" + i;
                childrenIDs.Add(data[partTempID].AsUUID());
            }

            m_scene.LinkObjectBySync(linkedGroup, rootID, childrenIDs);

            //if this is a relay node, forwards the event
            if (m_isSyncRelay)
            {
                //SendSceneEventToRelevantSyncConnectors(init_actorID, msg);
                SendSceneEventToRelevantSyncConnectors(senderActorID, msg);
            }
        }

        private void HandleDelinkObject(SymmetricSyncMessage msg, string senderActorID)
        {
            OSDMap data = DeserializeMessage(msg);
            if (data == null)
            {
                SymmetricSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                return;
            }

            //List<SceneObjectPart> localPrims = new List<SceneObjectPart>();
            List<UUID> delinkPrimIDs = new List<UUID>();
            List<UUID> beforeDelinkGroupIDs = new List<UUID>();
            List<SceneObjectGroup> incomingAfterDelinkGroups = new List<SceneObjectGroup>();

            int partCount = data["partCount"].AsInteger();
            for (int i = 0; i < partCount; i++)
            {
                string partTempID = "part" + i;
                UUID primID = data[partTempID].AsUUID();
                //SceneObjectPart localPart = m_scene.GetSceneObjectPart(primID);
                //localPrims.Add(localPart);
                delinkPrimIDs.Add(primID);
            }

            int beforeGroupCount = data["beforeGroupsCount"].AsInteger();
            for (int i = 0; i < beforeGroupCount; i++)
            {
                string groupTempID = "beforeGroup" + i;
                UUID beforeGroupID = data[groupTempID].AsUUID();
                beforeDelinkGroupIDs.Add(beforeGroupID);
            }

            int afterGroupsCount = data["afterGroupsCount"].AsInteger();
            for (int i = 0; i < afterGroupsCount; i++)
            {
                string groupTempID = "afterGroup" + i;
                string sogxml = data[groupTempID].AsString();
                SceneObjectGroup afterGroup = SceneObjectSerializer.FromXml2Format(sogxml);
                incomingAfterDelinkGroups.Add(afterGroup);
            }

            m_scene.DelinkObjectsBySync(delinkPrimIDs, beforeDelinkGroupIDs, incomingAfterDelinkGroups);

            //if this is a relay node, forwards the event
            if (m_isSyncRelay)
            {
                //SendSceneEventToRelevantSyncConnectors(init_actorID, msg);
                SendSceneEventToRelevantSyncConnectors(senderActorID, msg);
            }
        }

        /// <summary>
        /// The common actions for handling remote events (event initiated at other actors and propogated here)
        /// </summary>
        /// <param name="msg"></param>
        private void HandleRemoteEvent(SymmetricSyncMessage msg, string senderActorID)
        {
            OSDMap data = DeserializeMessage(msg);
            if (data == null)
            {
                SymmetricSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                return;
            }

            string init_actorID = data["actorID"].AsString();
            ulong evSeqNum = data["seqNum"].AsULong();

            switch (msg.Type)
            {
                case SymmetricSyncMessage.MsgType.NewScript:
                    HandleRemoteEvent_OnNewScript(init_actorID, evSeqNum, data);
                    break;
                case SymmetricSyncMessage.MsgType.UpdateScript:
                    HandleRemoteEvent_OnUpdateScript(init_actorID, evSeqNum, data);
                    break; 
                case SymmetricSyncMessage.MsgType.ScriptReset:
                    HandleRemoteEvent_OnScriptReset(init_actorID, evSeqNum, data);
                    break;
                case SymmetricSyncMessage.MsgType.ChatFromClient:
                    HandleRemoteEvent_OnChatFromClient(init_actorID, evSeqNum, data);
                    break;
                case SymmetricSyncMessage.MsgType.ChatFromWorld:
                    HandleRemoteEvent_OnChatFromWorld(init_actorID, evSeqNum, data);
                    break;
                case SymmetricSyncMessage.MsgType.ObjectGrab:
                    HandleRemoteEvent_OnObjectGrab(init_actorID, evSeqNum, data);
                    break;
                case SymmetricSyncMessage.MsgType.ObjectGrabbing:
                    HandleRemoteEvent_OnObjectGrabbing(init_actorID, evSeqNum, data);
                    break;
                case SymmetricSyncMessage.MsgType.ObjectDeGrab:
                    HandleRemoteEvent_OnObjectDeGrab(init_actorID, evSeqNum, data);
                    break;
            }

            //if this is a relay node, forwards the event
            if (m_isSyncRelay)
            {
                //SendSceneEventToRelevantSyncConnectors(init_actorID, msg);
                SendSceneEventToRelevantSyncConnectors(senderActorID, msg);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="actorID">the ID of the actor that initiates the event</param>
        /// <param name="evSeqNum">sequence num of the event from the actor</param>
        /// <param name="data">OSDMap data of event args</param>
        private void HandleRemoteEvent_OnNewScript(string actorID, ulong evSeqNum, OSDMap data)
        {
            m_log.Debug(LogHeader + ", " + m_actorID + ": received NewScript");

            UUID agentID = data["agentID"].AsUUID();
            UUID primID = data["primID"].AsUUID();
            UUID itemID = data["itemID"].AsUUID();

            string sogXml = data["sog"].AsString();
            SceneObjectGroup sog = SceneObjectSerializer.FromXml2Format(sogXml);
            SceneObjectPart part = null;
            
            foreach (SceneObjectPart prim in sog.Parts)
            {
                if(prim.UUID.Equals(primID)){
                    part = prim;
                    break;
                }
            }
            if(part == null)
            {
                m_log.Warn(LogHeader+": part "+primID+" not exist in the serialized object, do nothing");
                return;
            }
            //Update the object first
            Scene.ObjectUpdateResult updateResult = m_scene.AddOrUpdateObjectBySynchronization(sog);

            if (updateResult == Scene.ObjectUpdateResult.Updated || updateResult == Scene.ObjectUpdateResult.New)
            {
                m_log.Debug(LogHeader + ": TriggerNewScriptLocally");
                //Next, trigger creating the new script
                SceneObjectPart localPart = m_scene.GetSceneObjectPart(primID);
                m_scene.EventManager.TriggerNewScriptLocally(agentID, localPart, itemID);
            }
        }
       

        /// <summary>
        /// Special actions for remote event UpdateScript
        /// </summary>
        /// <param name="actorID">the ID of the actor that initiates the event</param>
        /// <param name="evSeqNum">sequence num of the event from the actor</param>
        /// <param name="data">OSDMap data of event args</param>
        private void HandleRemoteEvent_OnUpdateScript(string actorID, ulong evSeqNum, OSDMap data)
        {
            m_log.Debug(LogHeader + ", " + m_actorID + ": received UpdateScript");

            UUID agentID = data["agentID"].AsUUID();
            UUID itemID = data["itemID"].AsUUID();
            UUID primID = data["primID"].AsUUID();
            bool isRunning = data["running"].AsBoolean();
            UUID assetID = data["assetID"].AsUUID();

            //trigger the event in the local scene
            m_scene.EventManager.TriggerUpdateScriptLocally(agentID, itemID, primID, isRunning, assetID);           
        }

        /// <summary>
        /// Special actions for remote event ScriptReset
        /// </summary>
        /// <param name="data">OSDMap data of event args</param>
        private void HandleRemoteEvent_OnScriptReset(string actorID, ulong evSeqNum, OSDMap data)
        {
            m_log.Debug(LogHeader + ", " + m_actorID + ": received ScriptReset");

            UUID agentID = data["agentID"].AsUUID();
            UUID itemID = data["itemID"].AsUUID();
            UUID primID = data["primID"].AsUUID();

            SceneObjectPart part = m_scene.GetSceneObjectPart(primID);
            if (part == null || part.ParentGroup.IsDeleted)
            {
                m_log.Warn(LogHeader + " part " + primID + " not exist, all is deleted");
                return;
            }
            m_scene.EventManager.TriggerScriptResetLocally(part.LocalId, itemID);
        }

        /// <summary>
        /// Special actions for remote event ChatFromClient
        /// </summary>
        /// <param name="data">OSDMap data of event args</param>
        private void HandleRemoteEvent_OnChatFromClient(string actorID, ulong evSeqNum, OSDMap data)
        {
            //m_log.Debug(LogHeader + ": received ChatFromClient from "+actorID+", seq "+evSeqNum);

            OSChatMessage args = new OSChatMessage();
            args.Channel = data["channel"].AsInteger();
            args.Message = data["msg"].AsString();
            args.Position = data["pos"].AsVector3();
            args.From = data["name"].AsString();
            UUID id = data["id"].AsUUID();
            args.Scene = m_scene;
            //args.Type = ChatTypeEnum.Say;
            args.Type = (ChatTypeEnum) data["type"].AsInteger();
            ScenePresence sp;
            m_scene.TryGetScenePresence(id, out sp);

            m_scene.EventManager.TriggerOnChatFromClientLocally(sp, args); //Let WorldCommModule and other modules to catch the event
            m_scene.EventManager.TriggerOnChatFromWorldLocally(sp, args); //This is to let ChatModule to get the event and deliver it to avatars
        }

        private void HandleRemoteEvent_OnChatFromWorld(string actorID, ulong evSeqNum, OSDMap data)
        {
            //m_log.Debug(LogHeader + ", " + m_actorID + ": received ChatFromWorld from " + actorID + ", seq " + evSeqNum);

            OSChatMessage args = new OSChatMessage();
            args.Channel = data["channel"].AsInteger();
            args.Message = data["msg"].AsString();
            args.Position = data["pos"].AsVector3();
            args.From = data["name"].AsString();
            UUID id = data["id"].AsUUID();
            args.Scene = m_scene;
            //args.Type = ChatTypeEnum.Say;
            args.Type = (ChatTypeEnum)data["type"].AsInteger();
            //ScenePresence sp;
            //m_scene.TryGetScenePresence(id, out sp);

            m_scene.EventManager.TriggerOnChatFromWorldLocally(m_scene, args);
        }

        /// <summary>
        /// Special actions for remote event ChatFromClient
        /// </summary>
        /// <param name="data">OSDMap data of event args</param>
        private void HandleRemoteEvent_OnObjectGrab(string actorID, ulong evSeqNum, OSDMap data)
        {
            m_log.Debug(LogHeader + ", " + m_actorID + ": received GrabObject from " + actorID + ", seq " + evSeqNum);


            UUID agentID = data["agentID"].AsUUID();
            UUID primID = data["primID"].AsUUID();
            UUID originalPrimID = data["originalPrimID"].AsUUID();
            Vector3 offsetPos = data["offsetPos"].AsVector3();
            SurfaceTouchEventArgs surfaceArgs = new SurfaceTouchEventArgs();
            surfaceArgs.Binormal = data["binormal"].AsVector3();
            surfaceArgs.FaceIndex = data["faceIndex"].AsInteger();
            surfaceArgs.Normal = data["normal"].AsVector3();
            surfaceArgs.Position = data["position"].AsVector3();
            surfaceArgs.STCoord = data["stCoord"].AsVector3();
            surfaceArgs.UVCoord = data["uvCoord"].AsVector3();

            //Create an instance of IClientAPI to pass along agentID, see SOPObject.EventManager_OnObjectGrab()
            //We don't really need RegionSyncAvatar's implementation here, just borrow it's IClientAPI interface. 
            //If we decide to remove RegionSyncAvatar later, we can simple just define a very simple class that implements
            //ICleintAPI to be used here. 
            IClientAPI remoteClinet = new RegionSyncAvatar(m_scene, agentID, "", "", Vector3.Zero);
            SceneObjectPart part = m_scene.GetSceneObjectPart(primID);
            if (part == null)
            {
                m_log.Error(LogHeader + ": no prim with ID " + primID);
                return;
            }
            uint originalID = 0;
            if (originalPrimID != UUID.Zero)
            {
                SceneObjectPart originalPart = m_scene.GetSceneObjectPart(originalPrimID);
                originalID = originalPart.LocalId;
            }
            m_scene.EventManager.TriggerObjectGrabLocally(part.LocalId, originalID, offsetPos, remoteClinet, surfaceArgs);
        }

        private void HandleRemoteEvent_OnObjectGrabbing(string actorID, ulong evSeqNum, OSDMap data)
        {
            m_log.Debug(LogHeader + ", " + m_actorID + ": received GrabObject from " + actorID + ", seq " + evSeqNum);

            UUID agentID = data["agentID"].AsUUID();
            UUID primID = data["primID"].AsUUID();
            UUID originalPrimID = data["originalPrimID"].AsUUID();
            Vector3 offsetPos = data["offsetPos"].AsVector3();
            SurfaceTouchEventArgs surfaceArgs = new SurfaceTouchEventArgs();
            surfaceArgs.Binormal = data["binormal"].AsVector3();
            surfaceArgs.FaceIndex = data["faceIndex"].AsInteger();
            surfaceArgs.Normal = data["normal"].AsVector3();
            surfaceArgs.Position = data["position"].AsVector3();
            surfaceArgs.STCoord = data["stCoord"].AsVector3();
            surfaceArgs.UVCoord = data["uvCoord"].AsVector3();

            //Create an instance of IClientAPI to pass along agentID, see SOPObject.EventManager_OnObjectGrab()
            //We don't really need RegionSyncAvatar's implementation here, just borrow it's IClientAPI interface. 
            //If we decide to remove RegionSyncAvatar later, we can simple just define a very simple class that implements
            //ICleintAPI to be used here. 
            IClientAPI remoteClinet = new RegionSyncAvatar(m_scene, agentID, "", "", Vector3.Zero);
            SceneObjectPart part = m_scene.GetSceneObjectPart(primID);
            if (part == null)
            {
                m_log.Error(LogHeader + ": no prim with ID " + primID);
                return;
            }
            uint originalID = 0;
            if (originalPrimID != UUID.Zero)
            {
                SceneObjectPart originalPart = m_scene.GetSceneObjectPart(originalPrimID);
                originalID = originalPart.LocalId;
            }

            m_scene.EventManager.TriggerObjectGrabbingLocally(part.LocalId, originalID, offsetPos, remoteClinet, surfaceArgs);
        }

        private void HandleRemoteEvent_OnObjectDeGrab(string actorID, ulong evSeqNum, OSDMap data)
        {
            m_log.Debug(LogHeader + ", " + m_actorID + ": received GrabObject from " + actorID + ", seq " + evSeqNum);

            UUID agentID = data["agentID"].AsUUID();
            UUID primID = data["primID"].AsUUID();
            UUID originalPrimID = data["originalPrimID"].AsUUID();
            
            SurfaceTouchEventArgs surfaceArgs = new SurfaceTouchEventArgs();
            surfaceArgs.Binormal = data["binormal"].AsVector3();
            surfaceArgs.FaceIndex = data["faceIndex"].AsInteger();
            surfaceArgs.Normal = data["normal"].AsVector3();
            surfaceArgs.Position = data["position"].AsVector3();
            surfaceArgs.STCoord = data["stCoord"].AsVector3();
            surfaceArgs.UVCoord = data["uvCoord"].AsVector3();

            //Create an instance of IClientAPI to pass along agentID, see SOPObject.EventManager_OnObjectGrab()
            //We don't really need RegionSyncAvatar's implementation here, just borrow it's IClientAPI interface. 
            //If we decide to remove RegionSyncAvatar later, we can simple just define a very simple class that implements
            //ICleintAPI to be used here. 
            IClientAPI remoteClinet = new RegionSyncAvatar(m_scene, agentID, "", "", Vector3.Zero);
            SceneObjectPart part = m_scene.GetSceneObjectPart(primID);
            if (part == null)
            {
                m_log.Error(LogHeader + ": no prim with ID " + primID);
                return;
            }
            uint originalID = 0;
            if (originalPrimID != UUID.Zero)
            {
                SceneObjectPart originalPart = m_scene.GetSceneObjectPart(originalPrimID);
                originalID = originalPart.LocalId;
            }

            m_scene.EventManager.TriggerObjectDeGrabLocally(part.LocalId, originalID, remoteClinet, surfaceArgs);
        }


        /// <summary>
        /// The handler for (locally initiated) event OnNewScript: triggered by client's RezSript packet, publish it to other actors.
        /// </summary>
        /// <param name="clientID">ID of the client who creates the new script</param>
        /// <param name="part">the prim that contains the new script</param>
        private void OnLocalNewScript(UUID clientID, SceneObjectPart part, UUID itemID)
        {
            m_log.Debug(LogHeader + " RegionSyncModule_OnLocalNewScript");

            SceneObjectGroup sog = part.ParentGroup;
            if(sog==null){
                m_log.Warn(LogHeader + ": part " + part.UUID + " not in an SceneObjectGroup yet. Will not propagating new script event");
                //sog = new SceneObjectGroup(part);
                return;
            }
            //For simplicity, we just leverage a SOP's serialization method to transmit the information of new inventory item for the script).
            //This can certainly be optimized later (e.g. only sending serialization of the inventory item)
            OSDMap data = new OSDMap();
            data["agentID"] = OSD.FromUUID(clientID);
            data["primID"] = OSD.FromUUID(part.UUID);
            data["itemID"] = OSD.FromUUID(itemID); //id of the new inventory item of the part
            data["sog"] = OSD.FromString(SceneObjectSerializer.ToXml2Format(sog));

            SendSceneEvent(SymmetricSyncMessage.MsgType.NewScript, data);
        }

        /// <summary>
        /// The handler for (locally initiated) event OnUpdateScript: publish it to other actors.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="itemId"></param>
        /// <param name="primId"></param>
        /// <param name="isScriptRunning"></param>
        /// <param name="newAssetID"></param>
        private void OnLocalUpdateScript(UUID agentID, UUID itemId, UUID primId, bool isScriptRunning, UUID newAssetID)
        {
            m_log.Debug(LogHeader + " RegionSyncModule_OnUpdateScript");

            OSDMap data = new OSDMap();
            data["agentID"] = OSD.FromUUID(agentID);
            data["itemID"] = OSD.FromUUID(itemId);
            data["primID"] = OSD.FromUUID(primId);
            data["running"] = OSD.FromBoolean(isScriptRunning);
            data["assetID"] = OSD.FromUUID(newAssetID);

            /*
            data["actorID"] = OSD.FromString(m_actorID);
            data["seqNum"] = OSD.FromULong(GetNextEventSeq());

            SymmetricSyncMessage rsm = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.UpdateScript, OSDParser.SerializeJsonString(data));
            //send to actors who are interested in the event
            SendSceneEventToRelevantSyncConnectors(m_actorID, rsm);
             * */
            SendSceneEvent(SymmetricSyncMessage.MsgType.UpdateScript, data);
        }

        private void OnLocalScriptReset(uint localID, UUID itemID)
        {
            //we will use the prim's UUID as the identifier, not the localID, to publish the event for the prim                
            SceneObjectPart part = m_scene.GetSceneObjectPart(localID);

            if (part == null)
            {
                m_log.Warn(LogHeader + ": part with localID " + localID + " not exist");
                return;
            }

            OSDMap data = new OSDMap();
            data["primID"] = OSD.FromUUID(part.UUID);
            data["itemID"] = OSD.FromUUID(itemID);

            SendSceneEvent(SymmetricSyncMessage.MsgType.ScriptReset, data);
        }


        private void OnLocalChatFromClient(Object sender, OSChatMessage chat)
        {
            ScenePresence avatar = m_scene.GetScenePresence(chat.SenderUUID);

            if (avatar == null)
            {
                m_log.Warn(LogHeader + "avatar " + chat.SenderUUID + " not exist locally, NOT sending out ChatFromClient");
                return;
            }

            OSDMap data = new OSDMap();
            data["channel"] = OSD.FromInteger(chat.Channel);
            data["msg"] = OSD.FromString(chat.Message);
            data["pos"] = OSD.FromVector3(chat.Position);
            data["name"] = OSD.FromString(avatar.Name); //note this is different from OnLocalChatFromWorld
            data["id"] = OSD.FromUUID(chat.SenderUUID);
            data["type"] = OSD.FromInteger((int)chat.Type);
            SendSceneEvent(SymmetricSyncMessage.MsgType.ChatFromClient, data);
        }


        private void OnLocalChatFromWorld(Object sender, OSChatMessage chat)
        {

            OSDMap data = new OSDMap();
            data["channel"] = OSD.FromInteger(chat.Channel);
            data["msg"] = OSD.FromString(chat.Message);
            data["pos"] = OSD.FromVector3(chat.Position);
            data["name"] = OSD.FromString(chat.From); //note this is different from OnLocalChatFromClient
            data["id"] = OSD.FromUUID(chat.SenderUUID);
            data["type"] = OSD.FromInteger((int)chat.Type);
            SendSceneEvent(SymmetricSyncMessage.MsgType.ChatFromWorld, data);
        }
        
        private void OnLocalGrabObject(uint localID, uint originalID, Vector3 offsetPos, IClientAPI remoteClient, SurfaceTouchEventArgs surfaceArgs)
        {
            /*
            //we will use the prim's UUID as the identifier, not the localID, to publish the event for the prim                
            SceneObjectPart part = m_scene.GetSceneObjectPart(localID);
            if (part == null)
            {
                m_log.Warn(LogHeader + ": part with localID " + localID + " not exist");
                return;
            }

            //this seems to be useful if the prim touched and the prim handling the touch event are different:
            //i.e. a child part is touched, pass the event to root, and root handles the event. then root is the "part",
            //and the child part is the "originalPart"
            SceneObjectPart originalPart = null;
            if (originalID != 0)
            {
                originalPart = m_scene.GetSceneObjectPart(originalID);
                if (originalPart == null)
                {
                    m_log.Warn(LogHeader + ": part with localID " + localID + " not exist");
                    return;
                }
            }

            OSDMap data = new OSDMap();
            data["agentID"] = OSD.FromUUID(remoteClient.AgentId);
            data["primID"] = OSD.FromUUID(part.UUID);
            if (originalID != 0)
            {
                data["originalPrimID"] = OSD.FromUUID(originalPart.UUID);
            }
            else
            {
                data["originalPrimID"] = OSD.FromUUID(UUID.Zero);
            }
            data["offsetPos"] = OSD.FromVector3(offsetPos);
            
            data["binormal"] = OSD.FromVector3(surfaceArgs.Binormal);
            data["faceIndex"] = OSD.FromInteger(surfaceArgs.FaceIndex);
            data["normal"] = OSD.FromVector3(surfaceArgs.Normal);
            data["position"] = OSD.FromVector3(surfaceArgs.Position);
            data["stCoord"] = OSD.FromVector3(surfaceArgs.STCoord);
            data["uvCoord"] = OSD.FromVector3(surfaceArgs.UVCoord);
             * */
            OSDMap data = PrepareObjectGrabArgs(localID, originalID, offsetPos, remoteClient, surfaceArgs);
            SendSceneEvent(SymmetricSyncMessage.MsgType.ObjectGrab, data);
        }

        private void OnLocalObjectGrabbing(uint localID, uint originalID, Vector3 offsetPos, IClientAPI remoteClient, SurfaceTouchEventArgs surfaceArgs)
        {
            OSDMap data = PrepareObjectGrabArgs(localID, originalID, offsetPos, remoteClient, surfaceArgs);
            if (data != null)
            {
                SendSceneEvent(SymmetricSyncMessage.MsgType.ObjectGrabbing, data);
            }
        }

        private OSDMap PrepareObjectGrabArgs(uint localID, uint originalID, Vector3 offsetPos, IClientAPI remoteClient, SurfaceTouchEventArgs surfaceArgs)
        {
            //we will use the prim's UUID as the identifier, not the localID, to publish the event for the prim                
            SceneObjectPart part = m_scene.GetSceneObjectPart(localID);
            if (part == null)
            {
                m_log.Warn(LogHeader + ": PrepareObjectGrabArgs - part with localID " + localID + " not exist");
                return null;
            }

            //this seems to be useful if the prim touched and the prim handling the touch event are different:
            //i.e. a child part is touched, pass the event to root, and root handles the event. then root is the "part",
            //and the child part is the "originalPart"
            SceneObjectPart originalPart = null;
            if (originalID != 0)
            {
                originalPart = m_scene.GetSceneObjectPart(originalID);
                if (originalPart == null)
                {
                    m_log.Warn(LogHeader + ": PrepareObjectGrabArgs - part with localID " + localID + " not exist");
                    return null;
                }
            }

            OSDMap data = new OSDMap();
            data["agentID"] = OSD.FromUUID(remoteClient.AgentId);
            data["primID"] = OSD.FromUUID(part.UUID);
            if (originalID != 0)
            {
                data["originalPrimID"] = OSD.FromUUID(originalPart.UUID);
            }
            else
            {
                data["originalPrimID"] = OSD.FromUUID(UUID.Zero);
            }
            data["offsetPos"] = OSD.FromVector3(offsetPos);

            data["binormal"] = OSD.FromVector3(surfaceArgs.Binormal);
            data["faceIndex"] = OSD.FromInteger(surfaceArgs.FaceIndex);
            data["normal"] = OSD.FromVector3(surfaceArgs.Normal);
            data["position"] = OSD.FromVector3(surfaceArgs.Position);
            data["stCoord"] = OSD.FromVector3(surfaceArgs.STCoord);
            data["uvCoord"] = OSD.FromVector3(surfaceArgs.UVCoord);

            return data;
        }


        private void OnLocalDeGrabObject(uint localID, uint originalID, IClientAPI remoteClient, SurfaceTouchEventArgs surfaceArgs)
        {

        }

        private void SendSceneEvent(SymmetricSyncMessage.MsgType msgType, OSDMap data)
        {
            data["actorID"] = OSD.FromString(m_actorID);
            data["seqNum"] = OSD.FromULong(GetNextEventSeq());
            SymmetricSyncMessage rsm = new SymmetricSyncMessage(msgType, OSDParser.SerializeJsonString(data));

            //send to actors who are interested in the event
            SendSceneEventToRelevantSyncConnectors(m_actorID, rsm);
        }

        /*
        private void PublishSceneEvent(OSDMap data)
        {
            data["actorID"] = OSD.FromString(m_actorID);
            data["seqNum"] = OSD.FromULong(GetNextEventSeq());

            SymmetricSyncMessage rsm = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.OnUpdateScript, OSDParser.SerializeJsonString(data));
            SendSceneEventToRelevantSyncConnectors(m_actorID, rsm);
        }
         * */ 

        private ulong GetNextEventSeq()
        {
            return m_eventSeq++;
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
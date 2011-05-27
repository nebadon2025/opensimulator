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
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Region.Physics.Manager;
using OpenSim.Services.Interfaces;
using log4net;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;

using System.IO;
using System.Xml;
using Mono.Addins;
using OpenMetaverse.StructuredData;

/////////////////////////////////////////////////////////////////////////////////////////////
//KittyL: created 12/17/2010, to start DSG Symmetric Synch implementation
/////////////////////////////////////////////////////////////////////////////////////////////
namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "RegionSyncModule")]
    public class RegionSyncModule : INonSharedRegionModule, IRegionSyncModule, ICommandableModule, ISyncStatistics
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

            // parameters for statistic logging
            SyncStatisticCollector.LogEnabled = m_sysConfig.GetBoolean("SyncLogEnabled", false);
            SyncStatisticCollector.LogDirectory = m_sysConfig.GetString("SyncLogDirectory", ".");
            SyncStatisticCollector.LogInterval = m_sysConfig.GetInt("SyncLogInterval", 5000);
            SyncStatisticCollector.LogMaxFileTimeMin = m_sysConfig.GetInt("SyncLogMaxFileTimeMin", 5);
            SyncStatisticCollector.LogFileHeader = m_sysConfig.GetString("SyncLogFileHeader", "sync-");

            //initialize PrimSyncInfoManager
            PrimSyncInfoManager.DebugLog = m_log;
            PrimSyncInfo.DebugLog = m_log;
            PropertySyncInfo.DebugLog = m_log;
            int syncInfoAgeOutSeconds = m_sysConfig.GetInt("PrimSyncInfoAgeOutSeconds", 300); //unit of seconds
            TimeSpan tSpan = new TimeSpan(0, 0, syncInfoAgeOutSeconds);
            m_primSyncInfoManager = new PrimSyncInfoManager(this, tSpan.Ticks);
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

            m_scene.EventManager.OnNewPresence += OnNewPresence; 

            LogHeader += "-LocalRegion " + scene.RegionInfo.RegionName;

            m_syncID = GetSyncID();
        }

        //Called after AddRegion() has been called for all region modules of the scene
        public void RegionLoaded(Scene scene)
        {
            SyncStatisticCollector.Register(this);
            
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

        //ActorID might not be in use anymore. Rather, SyncID should be used. 
        //(Synchronization is sync node centric, not actor centric.)
        private string m_actorID;
        public string ActorID
        {
            get { return m_actorID; }
        }

        private string m_syncID;
        public string SyncID
        {
            get { return m_syncID; }
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

        //The following Sendxxx calls,send out a message immediately, w/o putting it in the SyncConnector's outgoing queue.
        //May need some optimization there on the priorities.

        public void SendTerrainUpdates(long updateTimeStamp, string lastUpdateActorID)
        {
            if (!IsSyncingWithOtherSyncNodes())
            {
                //no SyncConnector connected. Do nothing.
                return;
            }
            if (m_isSyncRelay || m_actorID.Equals(lastUpdateActorID))
            {
                //m_scene.Heightmap should have been updated already by the caller, send it out
                //SendSyncMessage(SymmetricSyncMessage.MsgType.Terrain, m_scene.Heightmap.SaveToXmlString());
                SendTerrainUpdateMessage(updateTimeStamp, lastUpdateActorID);
            }
        }

        public void PublishSceneEvent(EventManager.EventNames ev, Object[] evArgs)
        {
            if (!IsSyncingWithOtherSyncNodes())
            {
                //no SyncConnector connected. Do nothing.
                return;
            }

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
                case EventManager.EventNames.ChatFromWorld: 
                case EventManager.EventNames.ChatBroadcast:
                    if (evArgs.Length < 2)
                    {
                        m_log.Error(LogHeader + " not enough event args for ChatFromWorld");
                        return;
                    }
                    //OnLocalChatBroadcast(evArgs[0], (OSChatMessage)evArgs[1]);
                    OnLocalChatEvents(ev, evArgs[0], (OSChatMessage)evArgs[1]); 
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
                case EventManager.EventNames.Attach:
                    OnLocalAttach((uint)evArgs[0], (UUID)evArgs[1], (UUID)evArgs[2]);
                    return;
                case EventManager.EventNames.PhysicsCollision:
                    OnLocalPhysicsCollision((UUID)evArgs[0], (OSDArray)evArgs[1]);
                    return;
                default:
                    return;
            }
        }

        /////////////////////////////////////////////////////////////
        //New IRegionSyncModule functions for per property sync'ing
        /////////////////////////////////////////////////////////////

        /// <summary>
        /// Called when new object is created in local SceneGraph. (Add new object
        /// by receiving sync message should not trigger calling this function.)
        /// </summary>
        /// <param name="sog"></param>
        public void SyncNewObject(SceneObjectGroup sog)
        {
            //First, add PrimSyncInfoManager's record.
            foreach (SceneObjectPart part in sog.Parts)
            {
                m_primSyncInfoManager.InsertPrimSyncInfo(part, DateTime.Now.Ticks, m_syncID);
            }

            if (!IsSyncingWithOtherSyncNodes())
            {
                //no SyncConnector connected. No need to send out sync messages.
                return;
            }

            OSDMap encodedSOG = SceneObjectEncoder(sog);
            SymmetricSyncMessage syncMsg = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.NewObject, OSDParser.SerializeJsonString(encodedSOG));

            m_log.DebugFormat("{0}: Send NewObject out for {1},{2}", LogHeader, sog.Name, sog.UUID);

            SendSpecialObjectUpdateToRelevantSyncConnectors(m_actorID, sog, syncMsg);
        }

        public void SyncDeleteObject(SceneObjectGroup sog, bool softDelete)
        {
            //First, remove from PrimSyncInfoManager's record.
            foreach (SceneObjectPart part in sog.Parts)
            {
                m_primSyncInfoManager.RemovePrimSyncInfo(part);
            }

            if (!IsSyncingWithOtherSyncNodes())
            {
                //no SyncConnector connected. Do nothing.
                return;
            }

            OSDMap data = new OSDMap();
            data["UUID"] = OSD.FromUUID(sog.UUID);
            //TODO: need to put in SyncID instead of ActorID here. 
            //For now, keep it the same for simple debugging
            data["actorID"] = OSD.FromString(m_actorID); 
            data["softDelete"] = OSD.FromBoolean(softDelete);

            //m_log.DebugFormat("{0}: Send DeleteObject out for {1},{2}", m_scene.RegionInfo.RegionName, sog.Name, sog.UUID);

            SymmetricSyncMessage rsm = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.RemovedObject, OSDParser.SerializeJsonString(data));
            SendSpecialObjectUpdateToRelevantSyncConnectors(m_actorID, sog, rsm);
        }

        public void SyncLinkObject(SceneObjectGroup linkedGroup, SceneObjectPart root, List<SceneObjectPart> children)
        {
            if (children.Count == 0) return;

            //the group is just linked, each part has quite some properties changed 
            //(OffsetPosition, etc). Need to sync the property values in PrimSyncInfoManager
            //first
            foreach (SceneObjectPart part in linkedGroup.Parts)
            {
                m_primSyncInfoManager.UpdatePrimSyncInfoByLocal(part, new List<SceneObjectPartSyncProperties> { SceneObjectPartSyncProperties.FullUpdate });
            }


            if (!IsSyncingWithOtherSyncNodes())
            {
                //no SyncConnector connected. Do nothing.
                return;
            }

            //Now encode the linkedGroup for sync
            OSDMap data = new OSDMap();
            OSDMap encodedSOG = SceneObjectEncoder(linkedGroup);
            data["linkedGroup"] = encodedSOG;
            data["rootID"] = OSD.FromUUID(root.UUID);
            data["partCount"] = OSD.FromInteger(children.Count);
            data["actorID"] = OSD.FromString(m_actorID);
            int partNum = 0;
            foreach (SceneObjectPart part in children)
            {
                string partTempID = "part" + partNum;
                data[partTempID] = OSD.FromUUID(part.UUID);
                partNum++;

                //m_log.DebugFormat("{0}: SendLinkObject to link {1},{2} with {3}, {4}", part.Name, part.UUID, root.Name, root.UUID);
            }

            SymmetricSyncMessage rsm = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.LinkObject, OSDParser.SerializeJsonString(data));
            SendSpecialObjectUpdateToRelevantSyncConnectors(m_actorID, linkedGroup, rsm);
            //SendSceneEventToRelevantSyncConnectors(m_actorID, rsm, linkedGroup);
        }

        public void SyncDeLinkObject(List<SceneObjectPart> prims, List<SceneObjectGroup> beforeDelinkGroups, List<SceneObjectGroup> afterDelinkGroups)
        {
            if (prims.Count == 0 || beforeDelinkGroups.Count == 0) return;

            //the prims are just delinked, each part has quite some properties changed 
            //(OffsetPosition, etc). Need to sync the property values in PrimSyncInfoManager
            //first
            foreach (SceneObjectPart part in prims)
            {
                m_primSyncInfoManager.UpdatePrimSyncInfoByLocal(part, new List<SceneObjectPartSyncProperties> { SceneObjectPartSyncProperties.FullUpdate });
            }


            if (!IsSyncingWithOtherSyncNodes())
            {
                //no SyncConnector connected. Do nothing.
                return;
            }

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
                //string sogxml = SceneObjectSerializer.ToXml2Format(afterGroup);
                //data[groupTempID] = OSD.FromString(sogxml);
                OSDMap encodedSOG = SceneObjectEncoder(afterGroup);
                data[groupTempID] = encodedSOG;
                groupNum++;
            }

            SymmetricSyncMessage rsm = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.DelinkObject, OSDParser.SerializeJsonString(data));
            SendDelinkObjectToRelevantSyncConnectors(m_actorID, beforeDelinkGroups, rsm);
        }

        public void Debug(String debugMsg)
        {
            m_log.DebugFormat("{0}", debugMsg);
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

            //for test and debugging purpose
            Command cmdSyncDebug = new Command("debug", CommandIntentions.COMMAND_HAZARDOUS, SyncDebug, "Trigger some debugging functions");

            //for sync state comparison, 
            Command cmdSyncStateReport = new Command("state", CommandIntentions.COMMAND_HAZARDOUS, SyncStateReport, "Trigger synchronization state comparision functions");


            m_commander.RegisterCommand("start", cmdSyncStart);
            m_commander.RegisterCommand("stop", cmdSyncStop);
            m_commander.RegisterCommand("status", cmdSyncStatus);
            m_commander.RegisterCommand("debug", cmdSyncDebug);
            m_commander.RegisterCommand("state", cmdSyncStateReport);

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

        ///////////////////////////////////////////////////////////////////////
        // Memeber variables
        ///////////////////////////////////////////////////////////////////////

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

        private RegionSyncListener m_localSyncListener = null;
        private bool m_synced = false;

        ///////////////////////////////////////////////////////////////////////
        // Memeber variables for per-property timestamp
        ///////////////////////////////////////////////////////////////////////

        private Object m_primPropertyUpdateLock = new Object();
        private Dictionary<UUID, HashSet<SceneObjectPartSyncProperties>> m_primPropertyUpdates = new Dictionary<UUID, HashSet<SceneObjectPartSyncProperties>>();
        private int m_sendingPrimPropertyUpdates = 0;

        ///////////////////////////////////////////////////////////////////////
        // Legacy members for bucket-based sync, 
        ///////////////////////////////////////////////////////////////////////

        private Dictionary<string, Object> m_primUpdateLocks = new Dictionary<string, object>();
        private Dictionary<string, Dictionary<UUID, SceneObjectPart>> m_primUpdates = new Dictionary<string, Dictionary<UUID, SceneObjectPart>>();

        private delegate void PrimUpdatePerBucketSender(string bucketName, List<SceneObjectPart> primUpdates);
        private Dictionary<string, PrimUpdatePerBucketSender> m_primUpdatesPerBucketSender = new Dictionary<string, PrimUpdatePerBucketSender>();

        private delegate void PrimUpdatePerBucketReceiver(string bucketName, OSDMap data);
        private Dictionary<string, PrimUpdatePerBucketReceiver> m_primUpdatesPerBucketReceiver = new Dictionary<string, PrimUpdatePerBucketReceiver>();

        //The functions that encode properties in each bucket. For now, 
        //general bucket works on SOG, physics bucket works on SOP, so we define
        //the arg to be of type Object to be general in the interface. 
        //TO DO: redesign the interface once the bucket encoders working on more 
        //consistent/specific arguments.
        private delegate OSDMap UpdatePerBucketEncoder(string bucketName, Object arg);
        private Dictionary<string, UpdatePerBucketEncoder> m_updatePerBucketEncoder = new Dictionary<string, UpdatePerBucketEncoder>();

        //Decoders of properties in each bucket
        private delegate void UpdatePerBucketDecoder(string bucketName, OSDMap data, out Object outData);
        private Dictionary<string, UpdatePerBucketDecoder> m_updatePerBucketDecoder = new Dictionary<string, UpdatePerBucketDecoder>();


        private object m_updateScenePresenceLock = new object();
        private Dictionary<UUID, ScenePresence> m_presenceUpdates = new Dictionary<UUID, ScenePresence>();
        private int m_sendingUpdates = 0;

        private int m_maxNumOfPropertyBuckets;

        /////////////////////////////////////////////////////////////////////////////////////////
        // Synchronization related functions, NOT exposed through IRegionSyncModule interface
        /////////////////////////////////////////////////////////////////////////////////////////

        private string GetSyncID()
        {
            if (m_scene != null)
            {
                return m_scene.RegionInfo.RegionID.ToString();
            }
            else
            {
                return String.Empty;
            }
        }

        private void StatsTimerElapsed(object source, System.Timers.ElapsedEventArgs e)
        {
            //TO BE IMPLEMENTED
            m_log.Warn("[REGION SYNC MODULE]: StatsTimerElapsed -- NOT yet implemented.");
        }

        private void HandleGetTerrainRequest(SyncConnector connector)
        {
            string msgData = m_scene.Heightmap.SaveToXmlString();
            long lastUpdateTimeStamp;
            string lastUpdateActorID;
            m_scene.RequestModuleInterface<ITerrainModule>().GetSyncInfo(out lastUpdateTimeStamp, out lastUpdateActorID);

            OSDMap data = new OSDMap(3);
            data["terrain"] = OSD.FromString(msgData);
            data["actorID"] = OSD.FromString(lastUpdateActorID);
            data["timeStamp"] = OSD.FromLong(lastUpdateTimeStamp);

            //m_log.DebugFormat("{0}: Send out terrain with TS {1}, actorID {2}", LogHeader, lastUpdateTimeStamp, lastUpdateActorID);

            SymmetricSyncMessage syncMsg = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.Terrain, OSDParser.SerializeJsonString(data));
            connector.Send(syncMsg);
        }

        private void HandleGetObjectRequest(SyncConnector connector)
        {
            EntityBase[] entities = m_scene.GetEntities();
            foreach (EntityBase e in entities)
            {
                if (e is SceneObjectGroup)
                {
                    SceneObjectGroup sog = (SceneObjectGroup)e;

                    //SymmetricSyncMessage syncMsg = NewObjectMessageEncoder(sog);
                    OSDMap encodedSOG = SceneObjectEncoder(sog);
                    SymmetricSyncMessage syncMsg = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.NewObject, OSDParser.SerializeJsonString(encodedSOG));

                    //SendToSyncConnector(connector, sog, syncMsg);
                    connector.EnqueueOutgoingUpdate(sog.UUID, syncMsg.ToBytes());
                    //}
                }
            }
        } 

        private bool IsSyncingWithOtherSyncNodes()
        {
            return (m_syncConnectors.Count > 0);
        }

        private void SendTerrainUpdateToRelevantSyncConnectors(SymmetricSyncMessage syncMsg, string lastUpdateActorID)
        {
            List<SyncConnector> syncConnectors = GetSyncConnectorsForSceneEvents(lastUpdateActorID, syncMsg, null);

            foreach (SyncConnector connector in syncConnectors)
            {
                m_log.DebugFormat("{0}: Send terrain update to {1}", LogHeader, connector.OtherSideActorID);
                connector.Send(syncMsg);
            }
        }


        //Object updates are sent by enqueuing into each connector's outQueue.
        private void SendObjectUpdateToRelevantSyncConnectors(SceneObjectGroup sog, SymmetricSyncMessage syncMsg)
        {
            HashSet<SyncConnector> syncConnectors = GetSyncConnectorsForObjectUpdates(sog);

            foreach (SyncConnector connector in syncConnectors)
            {
                //m_log.Debug("Send " + syncMsg.Type.ToString() + " about sog "+sog.Name+","+sog.UUID+ " at pos "+sog.AbsolutePosition.ToString()+" to " + connector.OtherSideActorID);

                connector.EnqueueOutgoingUpdate(sog.UUID, syncMsg.ToBytes());
            }
        }

        //Object updates are sent by enqueuing into each connector's outQueue.
        private void SendPrimUpdateToRelevantSyncConnectors(SceneObjectPart updatedPart, SymmetricSyncMessage syncMsg, string lastUpdateActorID)
        {
            HashSet<SyncConnector> syncConnectors = GetSyncConnectorsForPrimUpdates(updatedPart);

            foreach (SyncConnector connector in syncConnectors)
            {
                //m_log.Debug("Send " + syncMsg.Type.ToString() + " about sop " + updatedPart.Name + "," + updatedPart.UUID + " at pos "+updatedPart.GroupPosition.ToString()
                //+" to " + connector.OtherSideActorID);

                if (!connector.OtherSideActorID.Equals(lastUpdateActorID))
                {
                    connector.EnqueueOutgoingUpdate(updatedPart.UUID, syncMsg.ToBytes());
                }
            }
        }

        private void SendDelinkObjectToRelevantSyncConnectors(string senderActorID, List<SceneObjectGroup> beforeDelinkGroups, SymmetricSyncMessage syncMsg)
        {
            HashSet<int> syncConnectorsSent = new HashSet<int>();

            foreach (SceneObjectGroup sog in beforeDelinkGroups)
            {
                HashSet<SyncConnector> syncConnectors = GetSyncConnectorsForObjectUpdates(sog);
                foreach (SyncConnector connector in syncConnectors)
                {
                    if (!syncConnectorsSent.Contains(connector.ConnectorNum) && !connector.OtherSideActorID.Equals(senderActorID))
                    {
                        m_log.DebugFormat("{0}: send DeLinkObject to {1}", LogHeader, connector.Description);
                        connector.EnqueueOutgoingUpdate(sog.UUID, syncMsg.ToBytes());
                        syncConnectorsSent.Add(connector.ConnectorNum);
                    }
                }
            }
        }

        /// <summary>
        /// Send some special object updatess to other sync nodes, including: 
        /// NewObject, RemoveObject, LinkObject. The sync messages are sent out right
        /// away, without being enqueue'ed as object update messages.
        /// </summary>
        /// <param name="sog"></param>
        /// <param name="syncMsg"></param>
        private void SendSpecialObjectUpdateToRelevantSyncConnectors(string init_actorID, SceneObjectGroup sog, SymmetricSyncMessage syncMsg)
        {
            HashSet<SyncConnector> syncConnectors = GetSyncConnectorsForObjectUpdates(sog);

            foreach (SyncConnector connector in syncConnectors)
            {
                if (!connector.OtherSideActorID.Equals(init_actorID))
                    connector.Send(syncMsg);
            }
        }

        private void SendSpecialObjectUpdateToRelevantSyncConnectors(string init_actorID, Vector3 globalPos, SymmetricSyncMessage syncMsg)
        {
            HashSet<SyncConnector> syncConnectors = GetSyncConnectorsForObjectUpdates(globalPos);

            foreach (SyncConnector connector in syncConnectors)
            {
                if (!connector.OtherSideActorID.Equals(init_actorID))
                    connector.Send(syncMsg);
            }
        }

        //Events are send out right away, without being put into the connector's outQueue first. 
        //May need a better method for managing the outgoing messages (i.e. prioritizing object updates and events)
        private void SendSceneEventToRelevantSyncConnectors(string init_actorID, SymmetricSyncMessage rsm, SceneObjectGroup sog)
        {
            //TODO: need to pick connectors based on sog position (quark it resides in)
            List<SyncConnector> syncConnectors = GetSyncConnectorsForSceneEvents(init_actorID, rsm, sog);

            foreach (SyncConnector connector in syncConnectors)
            {
                lock (m_stats) m_statEventOut++;
                connector.Send(rsm);
            }
        }

        /// <summary>
        /// Get the set of SyncConnectors to send updates of the given object. 
        /// </summary>
        /// <param name="sog"></param>
        /// <returns></returns>
        private HashSet<SyncConnector> GetSyncConnectorsForObjectUpdates(SceneObjectGroup sog)
        {
            HashSet<SyncConnector> syncConnectors = new HashSet<SyncConnector>();
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
        /// Get the set of sync connectors that connect to syncnodes whose SyncQuarks
        /// cover the given global position (no need to convert the position).
        /// </summary>
        /// <param name="globalPos"></param>
        /// <returns></returns>
        private HashSet<SyncConnector> GetSyncConnectorsForObjectUpdates(Vector3 globalPos)
        {
            //HashSet<SyncConnector> syncConnectors = m_syncConnectorManager.GetSyncConnectorsByPosition(globalPos);
            //return syncConnectors;

            //fake an sog to pass to GetSyncConnectorsForObjectUpdates, which 
            //is not used at all; a temp solution before we migrate to quarks
            SceneObjectGroup sog = new SceneObjectGroup(); 
            return GetSyncConnectorsForObjectUpdates(sog);
        }

        private HashSet<SyncConnector> GetSyncConnectorsForObjectUpdates(SceneObjectPart updatedPart)
        {
            return GetSyncConnectorsForObjectUpdates(updatedPart.ParentGroup);
        }

        private HashSet<SyncConnector> GetSyncConnectorsForPrimUpdates(SceneObjectPart updatedPart)
        {
            HashSet<SyncConnector> syncConnectors = new HashSet<SyncConnector>(GetSyncConnectorsForObjectUpdates(updatedPart.ParentGroup));
            return syncConnectors;
        }

        /// <summary>
        /// Get the set of SyncConnectors to send certain scene events. 
        /// </summary>
        /// <param name="sog"></param>
        /// <returns></returns>
        private List<SyncConnector> GetSyncConnectorsForSceneEvents(string init_actorID, SymmetricSyncMessage rsm, SceneObjectGroup sog)
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
                //m_actorType = m_scene.ActorSyncModule.ActorType;
            }

            //Start symmetric synchronization initialization automatically
            SyncStart(null);
        }

        private void OnNewPresence(ScenePresence avatar)
        {
            //Go through the objects, if any of them are attachments of the
            //new avatar, link them.
            EntityBase[] entities = m_scene.GetEntities();
            foreach (EntityBase e in entities)
            {
                if (e is SceneObjectGroup)
                {
                    SceneObjectGroup sog = (SceneObjectGroup)e;
                    if (sog.RootPart.AttachedAvatar == avatar.UUID)
                    {
                        m_scene.AttachObjectBySync(avatar, sog);
                    }
                }
            }

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
                m_synced = true;
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
            int connectorCount = 0;
            m_log.Warn(LogHeader + ": " + this.StatisticTitle());
            m_log.Warn(LogHeader + ": " + this.StatisticLine(true));
            ForEachSyncConnector(delegate(SyncConnector connector)
            {
                if (connectorCount++ == 0)
                    m_log.WarnFormat("[REGION SYNC MODULE]: Description: {0}", connector.StatisticTitle());
                m_log.WarnFormat("{0}: {1}: {2}", "[REGION SYNC MODULE}", connector.Description, connector.StatisticLine(true));
            });
        }

        private void SyncStateReport(Object[] args)
        {
            //Preliminary implementation
            EntityBase[] entities = m_scene.GetEntities();
            List<SceneObjectGroup> sogList = new List<SceneObjectGroup>();
            foreach (EntityBase entity in entities)
            {
                if (entity is SceneObjectGroup)
                {
                    sogList.Add((SceneObjectGroup)entity);
                }
            }

            int primCount = 0;
            foreach (SceneObjectGroup sog in sogList)
            {
                primCount += sog.Parts.Length;
            }

            m_log.WarnFormat("SyncStateReport {0} -- Object count: {1}, Prim Count {2} ", m_scene.RegionInfo.RegionName, sogList.Count, primCount);
            foreach (SceneObjectGroup sog in sogList)
            {
                m_log.WarnFormat("SyncStateReport -- SOG: name {0}, UUID {1}, position {2}", sog.Name, sog.UUID, sog.AbsolutePosition);
                foreach (SceneObjectPart part in sog.Parts)
                {
                    Vector3 pos = Vector3.Zero;
                    if (part.PhysActor != null)
                    {
                        pos = part.PhysActor.Position;
                    }
                    string debugMsg = "Part " + part.Name + "," + part.UUID+", LocalID "+part.LocalId;
                    if (part.ParentGroup.RootPart.UUID == part.UUID)
                    {
                        debugMsg += ", RootPart, ";
                        //else
                        //    debugMsg += ", ChildPart, ";
                        debugMsg += "ParentId = " + part.ParentID;
                        debugMsg += ", GroupPos " + part.GroupPosition + ", offset-position " + part.OffsetPosition;
                        if (part.IsAttachment)
                        {
                            debugMsg += ", AttachedAvatar=" + part.AttachedAvatar + ", AttachmentPoint = " + part.AttachmentPoint;
                            debugMsg += ", AttachedPos = " + part.AttachedPos;
                        }
                        debugMsg += ", Flags = " + part.Flags.ToString();
                        debugMsg += ", LocalFlags = " + part.LocalFlags.ToString();
                        if (part.Text != String.Empty)
                        {
                            debugMsg += ", Text = " + part.Text+", Color = "+part.Color.ToString();
                        }
                        debugMsg += ", AggregateScriptEvents = " + part.AggregateScriptEvents;
                        debugMsg += ", VolumeDetectActive" + part.VolumeDetectActive; 

                        ScenePresence sp = m_scene.GetScenePresence(part.AttachedAvatar);
                        if (sp != null)
                        {
                            debugMsg += ", attached avatar's localID = "+sp.LocalId;
                        }
                        m_log.WarnFormat(debugMsg);
                    }
                }
            }

            if (m_isSyncRelay)
            {
                SymmetricSyncMessage msg = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.SyncStateReport);
                ForEachSyncConnector(delegate(SyncConnector connector)
                {
                    connector.Send(msg);
                });

            }
        }

        private void SyncDebug(Object[] args)
        {
            if (m_scene != null)
            {
                EntityBase[] entities = m_scene.GetEntities();
                foreach (EntityBase entity in entities)
                {
                    if (entity is SceneObjectGroup)
                    {

                        SceneObjectGroup sog = (SceneObjectGroup)entity;

                        string sogXml = sog.ToXml2();

                        SceneObjectGroup sogCopy = SceneXmlLoader.DeserializeGroupFromXml2(sogXml);
                    }
                }
            }
        }

        private void PrimSyncSerializationDebug()
        {
            if (m_scene != null)
            {
                EntityBase[] entities = m_scene.GetEntities();
                foreach (EntityBase entity in entities)
                {
                    if (entity is SceneObjectGroup)
                    {

                        SceneObjectGroup sog = (SceneObjectGroup)entity;

                        //First, create PrimSyncInfo for each part in SOG and insert 
                        //into the local record
                        foreach (SceneObjectPart part in sog.Parts)
                        {
                            m_primSyncInfoManager.InsertPrimSyncInfo(part, DateTime.Now.Ticks, m_syncID);
                        }

                        //Next test serialization
                        OSDMap sogData = SceneObjectEncoder(sog);

                        //Next, test de-serialization
                        SceneObjectGroup group;
                        Dictionary<UUID, PrimSyncInfo> primsSyncInfo;
                        SceneObjectDecoder(sogData, out group, out primsSyncInfo);

                        //Add the list of PrimSyncInfo to PrimSyncInfoManager's record.
                        m_primSyncInfoManager.InsertMultiPrimSyncInfo(primsSyncInfo);

                        //Change each part's UUID so that we can add them to Scene and test the steps in AddNewSceneObjectByDecoding 
                        foreach (SceneObjectPart part in group.Parts)
                        {
                            UUID oldUUID = part.UUID;
                            part.UUID = UUID.Random();

                            PrimSyncInfo syncInfo = primsSyncInfo[oldUUID];
                            primsSyncInfo.Add(part.UUID, syncInfo);
                        }

                        //Add the decoded object to Scene
                        Scene.ObjectUpdateResult updateResult = m_scene.AddNewSceneObjectBySync(group);

                        //Now the PhysActor of each part in sog have been created, let's 
                        //set the PhysActor's properties
                        foreach (SceneObjectPart part in group.Parts)
                        {
                            primsSyncInfo[part.UUID].SetSOPPhyscActorProperties(part);
                        }

                        break;
                    }
                }
            }

        }

        //end of debug functions


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
                    m_synced = true;
                }
            }

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

            m_log.DebugFormat("{0}: new connector {1}", LogHeader, syncConnector.ConnectorNum);
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

                if (newlist.Count == 0)
                {
                    m_synced = false;
                }

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
            //m_scene.DeleteAllSceneObjects();
            m_scene.DeleteAllSceneObjectsBySync();
            
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

        #region Sync message handlers


        /// <summary>
        /// The handler for processing incoming sync messages.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="senderActorID">ActorID of the sender</param>
        public void HandleIncomingMessage(SymmetricSyncMessage msg, string senderActorID, SyncConnector syncConnector)
        {
            lock (m_stats) m_statMsgsIn++;
            //Added senderActorID, so that we don't have to include actorID in sync messages -- TODO
            switch (msg.Type)
            {
                case SymmetricSyncMessage.MsgType.GetTerrain:
                    {
                        //SendSyncMessage(SymmetricSyncMessage.MsgType.Terrain, m_scene.Heightmap.SaveToXmlString());
                        //SendTerrainUpdateMessage();
                        HandleGetTerrainRequest(syncConnector);
                        return;
                    }
                case SymmetricSyncMessage.MsgType.Terrain:
                    {
                        HandleTerrainUpdateMessage(msg, senderActorID);
                        return;
                    }
                case SymmetricSyncMessage.MsgType.GetObjects:
                    {
                        HandleGetObjectRequest(syncConnector);
                        return;
                    }
                case SymmetricSyncMessage.MsgType.NewObject:
                    HandleSyncNewObject(msg, senderActorID);
                    break;
                case SymmetricSyncMessage.MsgType.UpdatedPrimProperties:
                    HandleUpdatedPrimProperties(msg, senderActorID);
                    break;
                case SymmetricSyncMessage.MsgType.RemovedObject:
                    {
                        HandleRemovedObject(msg, senderActorID);
                        return;
                    }
                case SymmetricSyncMessage.MsgType.LinkObject:
                    {
                        HandleSyncLinkObject(msg, senderActorID);
                        return;
                    }
                case SymmetricSyncMessage.MsgType.DelinkObject:
                    {
                        HandleSyncDelinkObject(msg, senderActorID);
                        return;
                    }
                    //EVENTS PROCESSING
                case SymmetricSyncMessage.MsgType.NewScript:
                case SymmetricSyncMessage.MsgType.UpdateScript:
                case SymmetricSyncMessage.MsgType.ScriptReset:
                case SymmetricSyncMessage.MsgType.ChatFromClient:
                case SymmetricSyncMessage.MsgType.ChatFromWorld:
                case SymmetricSyncMessage.MsgType.ChatBroadcast:
                case SymmetricSyncMessage.MsgType.ObjectGrab:
                case SymmetricSyncMessage.MsgType.ObjectGrabbing:
                case SymmetricSyncMessage.MsgType.ObjectDeGrab:
                case SymmetricSyncMessage.MsgType.Attach:
                case SymmetricSyncMessage.MsgType.PhysicsCollision:
                    {
                        HandleRemoteEvent(msg, senderActorID);
                        return;
                    }
                case SymmetricSyncMessage.MsgType.SyncStateReport:
                    {
                        SyncStateReport(null);
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
            long lastUpdateTimeStamp = data["timeStamp"].AsLong();
            string lastUpdateActorID = data["actorID"].AsString();

            //m_log.DebugFormat("{0}: received Terrain update msg, with TS {1}, actorID {2}",LogHeader, lastUpdateTimeStamp, lastUpdateActorID);

            //update the terrain if the incoming terrain data has an more recent timestamp
            m_scene.RequestModuleInterface<ITerrainModule>().SetSyncInfo(0, m_scene.GetSyncActorID());
            if (m_scene.RequestModuleInterface<ITerrainModule>().UpdateTerrianBySync(lastUpdateTimeStamp, lastUpdateActorID, msgData))
            {
                //m_scene.Heightmap.LoadFromXmlString(msgData);
                //CheckForTerrainUpdates(false, timeStamp, actorID);
                m_log.DebugFormat("{0} : Synchronized terrain", LogHeader);
            }
        }

        ///////////////////////////////////////////////////////////////////////
        // Per property sync handlers
        ///////////////////////////////////////////////////////////////////////

        private void HandleSyncNewObject(SymmetricSyncMessage msg, string senderActorID)
        {

            OSDMap data = DeserializeMessage(msg);

            //if this is a relay node, forward the event
            Vector3 globalPos = data["GroupPosition"].AsVector3();
            if (m_isSyncRelay)
            {
                SendSpecialObjectUpdateToRelevantSyncConnectors(senderActorID, globalPos, msg);
            }
            
            AddNewSceneObjectByDecoding(data);

        }

        private void HandleUpdatedPrimProperties(SymmetricSyncMessage msg, string senderActorID)
        {
            OSDMap data = DeserializeMessage(msg);
            UUID primUUID;
            HashSet<PropertySyncInfo> propertiesSyncInfo;

            if (!data.ContainsKey("primUUID"))
            {
                m_log.WarnFormat("{0}: HandleUpdatedPrimProperties -- primUUID is missing in {1}", LogHeader, msg.Type.ToString());
                return;
            }
            primUUID = data["primUUID"].AsUUID();
            SceneObjectPart sop = m_scene.GetSceneObjectPart(primUUID);

            if (sop == null || sop.ParentGroup.IsDeleted)
            {
                m_log.WarnFormat("{0}: HandleUpdatedPrimProperties -- prim {1} no longer in local SceneGraph", LogHeader, primUUID);
                return;
            }

            propertiesSyncInfo = m_primSyncInfoManager.DecodePrimProperties(data);

            if (propertiesSyncInfo.Count>0)
            {
                //SYNC DEBUG
                /*
                string pString = "";
                foreach (PropertySyncInfo p in propertiesSyncInfo)
                {
                    pString += p.Property.ToString() + " ";
                    if (p.Property == SceneObjectPartSyncProperties.Shape)
                    {
                        PrimitiveBaseShape shape = PropertySerializer.DeSerializeShape((String)p.LastUpdateValue);
                        m_log.DebugFormat("Shape to be changed on SOP {0}, {1} to ProfileShape {2}", sop.Name, sop.UUID, shape.ProfileShape);
                    }
                }
                 * */ 
                 
                 
                //m_log.DebugFormat("ms {0}: HandleUpdatedPrimProperties, for prim {1},{2} with updated properties -- {3}", DateTime.Now.Millisecond, sop.Name, sop.UUID, pString);


                List<SceneObjectPartSyncProperties> propertiesUpdated = m_primSyncInfoManager.UpdatePrimSyncInfoBySync(sop, propertiesSyncInfo);

                //SYNC DEBUG
                
                if (propertiesUpdated.Contains(SceneObjectPartSyncProperties.AggregateScriptEvents))
                {
                    m_log.DebugFormat("AggregateScriptEvents updated: " + sop.AggregateScriptEvents); 
                }
                /*
                if (propertiesUpdated.Contains(SceneObjectPartSyncProperties.Shape))
                {
                    String hashedShape = Util.Md5Hash((PropertySerializer.SerializeShape(sop)));
                    m_log.DebugFormat("HandleUpdatedPrimProperties -- SOP {0},{1}, Shape updated, ProfileShape {2}, hashed value in SOP:{3}, in PrinSyncInfoManager: {4}",
                        sop.Name, sop.UUID, sop.Shape.ProfileShape, hashedShape, m_primSyncInfoManager.GetPrimSyncInfo(sop.UUID).PropertiesSyncInfo[SceneObjectPartSyncProperties.Shape].LastUpdateValueHash);
                }
                 * */  

                if (propertiesUpdated.Count > 0)
                {
                    //Enqueue the updated SOP and its properties for sync 
                    ProcessAndEnqueuePrimUpdatesBySync(sop, propertiesUpdated);

                    //Calling SOP.ScheduleFullUpdate(), so that viewers, if any,
                    //will receive updates as well.
                    sop.ScheduleFullUpdate(null);
                }
            }
        }

        private void HandleSyncLinkObject(SymmetricSyncMessage msg, string senderActorID)
        {

            // Get the data from message and error check
            OSDMap data = DeserializeMessage(msg);
            if (data == null)
            {
                SymmetricSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                return;
            }

            OSDMap encodedSOG = (OSDMap)data["linkedGroup"];
            SceneObjectGroup linkedGroup;
            Dictionary<UUID, PrimSyncInfo> primsSyncInfo;
            SceneObjectDecoder(encodedSOG, out linkedGroup, out primsSyncInfo);

            //TEMP DEBUG
            // m_log.DebugFormat(" received linkedGroup: {0}", linkedGroup.DebugObjectUpdateResult());
            //m_log.DebugFormat(linkedGroup.DebugObjectUpdateResult());

            if (linkedGroup == null)
            {
                m_log.WarnFormat("{0}: HandleSyncLinkObject, no valid Linked-Group has been deserialized", LogHeader);
                return;
            }

            UUID rootID = data["rootID"].AsUUID();
            int partCount = data["partCount"].AsInteger();
            List<UUID> childrenIDs = new List<UUID>();

            for (int i = 0; i < partCount; i++)
            {
                string partTempID = "part" + i;
                childrenIDs.Add(data[partTempID].AsUUID());
            }

            //if this is a relay node, forwards the event
            if (m_isSyncRelay)
            {
                //SendSceneEventToRelevantSyncConnectors(senderActorID, msg, linkedGroup);
                SendSpecialObjectUpdateToRelevantSyncConnectors(senderActorID, linkedGroup, msg);
            }

            //TEMP SYNC DEBUG
            //m_log.DebugFormat("{0}: received LinkObject from {1}", LogHeader, senderActorID);

            m_scene.LinkObjectBySync(linkedGroup, rootID, childrenIDs);

            //Update properties, if any has changed
            foreach (KeyValuePair<UUID, PrimSyncInfo> incomingPrimSyncInfo in primsSyncInfo)
            {
                UUID primUUID = incomingPrimSyncInfo.Key;
                PrimSyncInfo updatedPrimSyncInfo = incomingPrimSyncInfo.Value;

                SceneObjectPart part = m_scene.GetSceneObjectPart(primUUID);
                if (part == null)
                {
                    m_log.WarnFormat("{0}: HandleSyncLinkObject, prim {1} not in local Scene Graph after LinkObjectBySync is called", LogHeader, primUUID);
                }
                else
                {
                    m_primSyncInfoManager.UpdatePrimSyncInfoBySync(part, updatedPrimSyncInfo);
                }
            }
        }

        private void HandleSyncDelinkObject(SymmetricSyncMessage msg, string senderActorID)
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
            List<Dictionary<UUID, PrimSyncInfo>> incomingPrimSyncInfo = new List<Dictionary<UUID, PrimSyncInfo>>();

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
                //string sogxml = data[groupTempID].AsString();
                SceneObjectGroup afterGroup;
                OSDMap encodedSOG = (OSDMap)data[groupTempID];
                Dictionary<UUID, PrimSyncInfo> primsSyncInfo;
                SceneObjectDecoder(encodedSOG, out afterGroup, out primsSyncInfo);
                incomingAfterDelinkGroups.Add(afterGroup);
                incomingPrimSyncInfo.Add(primsSyncInfo);
            }

            //if this is a relay node, forwards the event
            if (m_isSyncRelay)
            {
                List<SceneObjectGroup> beforeDelinkGroups = new List<SceneObjectGroup>();
                foreach (UUID sogID in beforeDelinkGroupIDs)
                {
                    SceneObjectGroup sog = m_scene.SceneGraph.GetGroupByPrim(sogID);
                    beforeDelinkGroups.Add(sog);
                }
                SendDelinkObjectToRelevantSyncConnectors(senderActorID, beforeDelinkGroups, msg);
            }

            m_scene.DelinkObjectsBySync(delinkPrimIDs, beforeDelinkGroupIDs, incomingAfterDelinkGroups);

            //Sync properties 
            //Update properties, for each prim in each deLinked-Object
            foreach (Dictionary<UUID, PrimSyncInfo> primsSyncInfo in incomingPrimSyncInfo)
            {
                foreach (KeyValuePair<UUID, PrimSyncInfo> inPrimSyncInfo in primsSyncInfo)
                {
                    UUID primUUID = inPrimSyncInfo.Key;
                    PrimSyncInfo updatedPrimSyncInfo = inPrimSyncInfo.Value;

                    SceneObjectPart part = m_scene.GetSceneObjectPart(primUUID);
                    if (part == null)
                    {
                        m_log.WarnFormat("{0}: HandleSyncDelinkObject, prim {1} not in local Scene Graph after DelinkObjectsBySync is called", LogHeader, primUUID);
                    }
                    else
                    {
                        m_primSyncInfoManager.UpdatePrimSyncInfoBySync(part, updatedPrimSyncInfo);
                    }
                }
            }
        }


        /// <summary>
        /// Send out a sync message about the updated Terrain. If this is a relay node,
        /// forward the sync message to all connectors except the one which initiated
        /// the update.
        /// </summary>
        /// <param name="lastUpdateTimeStamp"></param>
        /// <param name="lastUpdateActorID"></param>
        private void SendTerrainUpdateMessage(long lastUpdateTimeStamp, string lastUpdateActorID)
        {
            string msgData = m_scene.Heightmap.SaveToXmlString();
            //long lastUpdateTimeStamp;
            //string lastUpdateActorID;
            //m_scene.RequestModuleInterface<ITerrainModule>().GetSyncInfo(out lastUpdateTimeStamp, out lastUpdateActorID);

            OSDMap data = new OSDMap(3);
            data["terrain"] = OSD.FromString(msgData);
            data["actorID"] = OSD.FromString(lastUpdateActorID);
            data["timeStamp"] = OSD.FromLong(lastUpdateTimeStamp);

            //m_log.DebugFormat("{0}: Ready to send terrain update with lastUpdateTimeStamp {1} and lastUpdateActorID {2}", LogHeader, lastUpdateTimeStamp, lastUpdateActorID);

            SymmetricSyncMessage syncMsg = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.Terrain, OSDParser.SerializeJsonString(data));
            SendTerrainUpdateToRelevantSyncConnectors(syncMsg, lastUpdateActorID);
            //SendSyncMessage(SymmetricSyncMessage.MsgType.Terrain, OSDParser.SerializeJsonString(data));
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

            //if this is a relay node, forwards the event
            if (m_isSyncRelay)
            {
                //SendSceneEventToRelevantSyncConnectors(senderActorID, msg, sog);
                SendSpecialObjectUpdateToRelevantSyncConnectors(senderActorID, sog, msg);
            }


            if (sog != null)
            {
                if (!softDelete)
                {
                    //m_log.DebugFormat("{0}: hard delete object {1}", LogHeader, sog.UUID);
                    foreach (SceneObjectPart part in sog.Parts)
                    {
                        m_primSyncInfoManager.RemovePrimSyncInfo(part);
                    }
                    m_scene.DeleteSceneObjectBySynchronization(sog);
                }
                else
                {
                    //m_log.DebugFormat("{0}: soft delete object {1}", LogHeader, sog.UUID);
                    m_scene.UnlinkSceneObject(sog, true);
                }
            }


        }

        #endregion //Sync message handlers

        #region Remote Event handlers

        /// <summary>
        /// The common actions for handling remote events (event initiated at other actors and propogated here)
        /// </summary>
        /// <param name="msg"></param>
        private void HandleRemoteEvent(SymmetricSyncMessage msg, string senderActorID)
        {

            //if this is a relay node, forwards the event
            if (m_isSyncRelay)
            {
                SendSceneEventToRelevantSyncConnectors(senderActorID, msg, null);
            }

            OSDMap data = DeserializeMessage(msg);
            if (data == null)
            {
                SymmetricSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                return;
            }

            lock (m_stats) m_statEventIn++;
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
                    //HandleRemoteEvent_OnChatFromClient(init_actorID, evSeqNum, data);
                    //break;
                case SymmetricSyncMessage.MsgType.ChatFromWorld:
                    //HandleRemoteEvent_OnChatFromWorld(init_actorID, evSeqNum, data);
                    //break;
                case SymmetricSyncMessage.MsgType.ChatBroadcast:
                    //HandleRemoteEvent_OnChatBroadcast(init_actorID, evSeqNum, data);
                    HandleRemoveEvent_OnChatEvents(msg.Type, init_actorID, evSeqNum, data);
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
                case SymmetricSyncMessage.MsgType.Attach:
                    HandleRemoteEvent_OnAttach(init_actorID, evSeqNum, data);
                    break;
                case SymmetricSyncMessage.MsgType.PhysicsCollision:
                    HandleRemoteEvent_PhysicsCollision(init_actorID, evSeqNum, data);
                    break;
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

            UUID agentID = data["agentID"].AsUUID();
            UUID primUUID = data["primUUID"].AsUUID();
            UUID itemID = data["itemID"].AsUUID();

            SceneObjectPart localPart = m_scene.GetSceneObjectPart(primUUID);

            if (localPart == null || localPart.ParentGroup.IsDeleted)
            {
                m_log.WarnFormat("{0}: HandleRemoteEvent_OnNewScript -- prim {1} no longer in local SceneGraph", LogHeader, primUUID);
                return;
            }

            HashSet<PropertySyncInfo> propertiesSyncInfo = m_primSyncInfoManager.DecodePrimProperties(data);
            if (propertiesSyncInfo.Count > 0)
            {
                List<SceneObjectPartSyncProperties> propertiesUpdated = m_primSyncInfoManager.UpdatePrimSyncInfoBySync(localPart, propertiesSyncInfo);
            }

            //The TaskInventory value might have already been sync'ed by UpdatedPrimProperties, 
            //but we still need to create the script instance by reading out the inventory.
            m_scene.EventManager.TriggerNewScriptLocally(agentID, localPart, itemID);
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
            //m_log.Debug(LogHeader + ", " + m_actorID + ": received ScriptReset");

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
        /// Handlers for remote chat events: ChatFromClient, ChatFromWorld, ChatBroadcast
        /// </summary>
        /// <param name="msgType"></param>
        /// <param name="actorID"></param>
        /// <param name="evSeqNum"></param>
        /// <param name="data">The args of the event</param>
        private void HandleRemoveEvent_OnChatEvents(SymmetricSyncMessage.MsgType msgType, string actorID, ulong evSeqNum, OSDMap data)
        {
            OSChatMessage args = new OSChatMessage();
            args.Channel = data["channel"].AsInteger();
            args.Message = data["msg"].AsString();
            args.Position = data["pos"].AsVector3();
            args.From = data["name"].AsString();
            UUID id = data["id"].AsUUID();
            args.Scene = m_scene;
            args.Type = (ChatTypeEnum)data["type"].AsInteger();

            switch (msgType)
            {
                case SymmetricSyncMessage.MsgType.ChatFromClient:
                    ScenePresence sp;
                    m_scene.TryGetScenePresence(id, out sp);
                    m_scene.EventManager.TriggerOnChatFromClientLocally(sp, args); //Let WorldCommModule and other modules to catch the event
                    m_scene.EventManager.TriggerOnChatFromWorldLocally(sp, args); //This is to let ChatModule to get the event and deliver it to avatars
                    break;
                case SymmetricSyncMessage.MsgType.ChatFromWorld:
                    m_scene.EventManager.TriggerOnChatFromWorldLocally(m_scene, args);
                    break;
                case SymmetricSyncMessage.MsgType.ChatBroadcast:
                    m_scene.EventManager.TriggerOnChatBroadcastLocally(m_scene, args);
                    break;
            }
        }

        /// <summary>
        /// Special actions for remote event ChatFromClient
        /// </summary>
        /// <param name="data">OSDMap data of event args</param>
        private void HandleRemoteEvent_OnObjectGrab(string actorID, ulong evSeqNum, OSDMap data)
        {
           // m_log.Debug(LogHeader + ", " + m_actorID + ": received GrabObject from " + actorID + ", seq " + evSeqNum);


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
           // m_log.Debug(LogHeader + ", " + m_actorID + ": received GrabObject from " + actorID + ", seq " + evSeqNum);

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
           // m_log.Debug(LogHeader + ", " + m_actorID + ": received GrabObject from " + actorID + ", seq " + evSeqNum);

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

        private void HandleRemoteEvent_OnAttach(string actorID, ulong evSeqNum, OSDMap data)
        {
            
            UUID primID = data["primID"].AsUUID();
            UUID itemID = data["itemID"].AsUUID();
            UUID avatarID = data["avatarID"].AsUUID();

            SceneObjectPart part = m_scene.GetSceneObjectPart(primID);
            if (part == null)
            {
                m_log.Warn(LogHeader + ", HandleRemoteEvent_OnAttach: no part with UUID " + primID + " found");
                return;
            }

            uint localID = part.LocalId;
            m_scene.EventManager.TriggerOnAttachLocally(localID, itemID, avatarID);
        }

        private void HandleRemoteEvent_PhysicsCollision(string actorID, ulong evSeqNum, OSDMap data)
        {
            UUID primUUID = data["primUUID"].AsUUID();
            //OSDArray collisionLocalIDs = (OSDArray)data["collisionLocalIDs"];
            OSDArray collisionUUIDs = (OSDArray)data["collisionUUIDs"];

            SceneObjectPart part = m_scene.GetSceneObjectPart(primUUID);
            if (part == null)
            {
                m_log.WarnFormat("{0}: HandleRemoteEvent_PhysicsCollision: no part with UUID {1} found", LogHeader, primUUID);
                return;
            }
            if (collisionUUIDs == null)
            {
                m_log.WarnFormat("{0}: HandleRemoteEvent_PhysicsCollision: no collisionLocalIDs", LogHeader);
                return;
            }

            // Build up the collision list. The contact point is ignored so we generate some default.
            CollisionEventUpdate e = new CollisionEventUpdate();
            /*
            foreach (uint collisionID in collisionLocalIDs)
            {
                // e.addCollider(collisionID, new ContactPoint());
                e.addCollider(collisionID, new ContactPoint(Vector3.Zero, Vector3.UnitX, 0.03f));
            }
             * */
            for(int i=0; i<collisionUUIDs.Count; i++)
            {
                OSD arg = collisionUUIDs[i];
                UUID collidingUUID = arg.AsUUID();

                SceneObjectPart collidingPart = m_scene.GetSceneObjectPart(collidingUUID);
                if (collidingPart == null)
                {
                    //collision object is not a prim, check if it's an avatar
                    ScenePresence sp = m_scene.GetScenePresence(collidingUUID);
                    if (sp == null)
                    {
                        m_log.WarnFormat("Received collision event for SOP {0},{1} with another SOP {2}, but the latter is not found in local Scene",
                            part.Name, part.UUID, collidingUUID);
                    }
                    else
                    {
                        e.addCollider(sp.LocalId, new ContactPoint(Vector3.Zero, Vector3.UnitX, 0.03f));
                    }
                }
                else
                {
                    e.addCollider(collidingPart.LocalId, new ContactPoint(Vector3.Zero, Vector3.UnitX, 0.03f));
                }
            }
            part.PhysicsCollisionLocally(e);
        }

        /// <summary>
        /// The handler for (locally initiated) event OnNewScript: triggered by client's RezSript packet, publish it to other actors.
        /// </summary>
        /// <param name="clientID">ID of the client who creates the new script</param>
        /// <param name="part">the prim that contains the new script</param>
        private void OnLocalNewScript(UUID clientID, SceneObjectPart part, UUID itemID)
        {
            //m_log.Debug(LogHeader + " RegionSyncModule_OnLocalNewScript");

            SceneObjectGroup sog = part.ParentGroup;
            if(sog==null){
                m_log.Warn(LogHeader + ": part " + part.UUID + " not in an SceneObjectGroup yet. Will not propagating new script event");
                //sog = new SceneObjectGroup(part);
                return;
            }
            HashSet<SceneObjectPartSyncProperties> updatedProperties = m_primSyncInfoManager.UpdatePrimSyncInfoByLocal(part, 
                new List<SceneObjectPartSyncProperties>(){SceneObjectPartSyncProperties.TaskInventory});

            //It is very likely that the TaskInventory cache data in PrimSyncInfoManager
            //has been updated by local RezScript(), which will only update
            //inventory but not creating script instance unless this is a 
            //script engine. We just make sure that if that does not happen 
            //ealier than this, we are sync'ing the new TaskInventory.
            updatedProperties.Add(SceneObjectPartSyncProperties.TaskInventory);

            OSDMap syncData = m_primSyncInfoManager.EncodePrimProperties(part, updatedProperties);
            //syncData already includes primUUID, add agentID and itemID next
            syncData["agentID"] = OSD.FromUUID(clientID);
            syncData["itemID"] = OSD.FromUUID(itemID); //id of the new inventory item of the part

            SendSceneEvent(SymmetricSyncMessage.MsgType.NewScript, syncData);
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
            //m_log.Debug(LogHeader + " RegionSyncModule_OnUpdateScript");

            OSDMap data = new OSDMap();
            data["agentID"] = OSD.FromUUID(agentID);
            data["itemID"] = OSD.FromUUID(itemId);
            data["primID"] = OSD.FromUUID(primId);
            data["running"] = OSD.FromBoolean(isScriptRunning);
            data["assetID"] = OSD.FromUUID(newAssetID);

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

        private void OnLocalChatBroadcast(Object sender, OSChatMessage chat)
        {

        }

        private void OnLocalChatEvents(EventManager.EventNames evType, Object sender, OSChatMessage chat)
        {
            OSDMap data = new OSDMap();
            data["channel"] = OSD.FromInteger(chat.Channel);
            data["msg"] = OSD.FromString(chat.Message);
            data["pos"] = OSD.FromVector3(chat.Position);
            data["name"] = OSD.FromString(chat.From); //note this is different from OnLocalChatFromClient
            data["id"] = OSD.FromUUID(chat.SenderUUID);
            data["type"] = OSD.FromInteger((int)chat.Type);

            switch (evType)
            {
                case EventManager.EventNames.ChatFromClient:
                    SendSceneEvent(SymmetricSyncMessage.MsgType.ChatFromClient, data);
                    break;
                case EventManager.EventNames.ChatFromWorld:
                    SendSceneEvent(SymmetricSyncMessage.MsgType.ChatFromWorld, data);
                    break;
                case EventManager.EventNames.ChatBroadcast:
                    SendSceneEvent(SymmetricSyncMessage.MsgType.ChatBroadcast, data);
                    break; 
            }

        }

        private void OnLocalAttach(uint localID, UUID itemID, UUID avatarID)
        {

            OSDMap data = new OSDMap();
            SceneObjectPart part = m_scene.GetSceneObjectPart(localID);
            if (part == null)
            {
                m_log.Warn(LogHeader + ", OnLocalAttach: no part with localID: " + localID);
                return;
            }
            data["primID"] = OSD.FromUUID(part.UUID);
            data["itemID"] = OSD.FromUUID(itemID);
            data["avatarID"] = OSD.FromUUID(avatarID);
            SendSceneEvent(SymmetricSyncMessage.MsgType.Attach, data);
        }

        /*
        private void OnLocalPhysicsCollision(UUID partUUID, OSDArray collisionLocalIDs)
        {
            OSDMap data = new OSDMap();
            data["primUUID"] = OSD.FromUUID(partUUID);
            data["collisionLocalIDs"] = collisionLocalIDs;
            SendSceneEvent(SymmetricSyncMessage.MsgType.PhysicsCollision, data);
        }
         * */
        private void OnLocalPhysicsCollision(UUID partUUID, OSDArray collisionUUIDs)
        {
            OSDMap data = new OSDMap();
            data["primUUID"] = OSD.FromUUID(partUUID);
            data["collisionUUIDs"] = collisionUUIDs;
            SendSceneEvent(SymmetricSyncMessage.MsgType.PhysicsCollision, data);
        }

        private void OnLocalGrabObject(uint localID, uint originalID, Vector3 offsetPos, IClientAPI remoteClient, SurfaceTouchEventArgs surfaceArgs)
        {
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
            SendSceneEventToRelevantSyncConnectors(m_actorID, rsm, null);
        }

        private ulong GetNextEventSeq()
        {
            return m_eventSeq++;
        }
        #endregion //Remote Event handlers

        #region Prim Property Sync management


        private PrimSyncInfoManager m_primSyncInfoManager;
        //private 
        /// <summary>
        /// Triggered when some properties in an SOP have been updated locally. 
        /// Sync the properties' values in this sync module with those in
        /// SOP, and update timestamp accordingly. 
        /// </summary>
        /// <param name="part"></param>
        /// <param name="updatedProperties"></param>
        public void ProcessAndEnqueuePrimUpdatesByLocal(SceneObjectPart part, List<SceneObjectPartSyncProperties> updatedProperties)
        {
            if (part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            //Do some sanity checking of the properties
            List<SceneObjectPartSyncProperties> checkedUpdatedProperties = new List<SceneObjectPartSyncProperties>();
            foreach (SceneObjectPartSyncProperties property in updatedProperties)
            {
                if (PrimSyncInfo.PrimPhysActorProperties.Contains(property) && part.PhysActor == null)
                {
                    m_log.WarnFormat("ProcessAndEnqueuePrimUpdatesByLocal: informed that property {0} changed in {1},{2}, which does not have PhysActor yet. Wrong property passed in!!!",
                        property, part.Name, part.UUID);
                }
                else
                {
                    checkedUpdatedProperties.Add(property);
                }
            }

            //Sync values with SOP's data and update timestamp according, to 
            //obtain the list of properties that really have been updated
            //and should be propogated to other sync nodes.
            HashSet<SceneObjectPartSyncProperties> propertiesWithSyncInfoUpdated = m_primSyncInfoManager.UpdatePrimSyncInfoByLocal(part, checkedUpdatedProperties);

            //Enqueue the prim with the set of updated properties, excluding the group properties
            if (propertiesWithSyncInfoUpdated.Count > 0)
            {
                lock (m_primPropertyUpdateLock)
                {
                    if (m_primPropertyUpdates.ContainsKey(part.UUID))
                    {
                        foreach (SceneObjectPartSyncProperties property in propertiesWithSyncInfoUpdated)
                        {
                            //Include the "property" into the list of updated properties.
                            //HashSet's Add function should handle it correctly whether the property
                            //is or is not in the set.
                            m_primPropertyUpdates[part.UUID].Add(property);
                        }
                    }
                    else
                    {
                        m_primPropertyUpdates.Add(part.UUID, propertiesWithSyncInfoUpdated);
                    }
                }
            }
        }

        /// <summary>
        /// Triggered when some properties of the given SOP have been updated by
        /// receiving a sync message UpdatedPrimProperties.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="updatedProperties"></param>
        public void ProcessAndEnqueuePrimUpdatesBySync(SceneObjectPart part, List<SceneObjectPartSyncProperties> updatedProperties)
        {
            if (part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            HashSet<SceneObjectPartSyncProperties> propertiesToSync = new HashSet<SceneObjectPartSyncProperties>();

            if (m_isSyncRelay)
            {
                //If this is a sync relay node, it needs to forward out the updates
                propertiesToSync = new HashSet<SceneObjectPartSyncProperties>(updatedProperties);
            }

            //Enqueue the prim with the set of updated properties
            if (propertiesToSync.Count > 0)
            {
                lock (m_primPropertyUpdateLock)
                {
                    if (m_primPropertyUpdates.ContainsKey(part.UUID))
                    {
                        foreach (SceneObjectPartSyncProperties property in propertiesToSync)
                        {
                            //Include the "property" into the list of updated properties.
                            //HashSet's Add function should handle it correctly whether the property
                            //is or is not in the set.
                            m_primPropertyUpdates[part.UUID].Add(property);
                        }
                    }
                    else
                    {
                        m_primPropertyUpdates.Add(part.UUID, propertiesToSync);
                    }
                }
            }
        }


        /// <summary>
        /// Triggered periodically to send out sync messages that include 
        /// prim-properties that have been updated since last SyncOut.
        /// </summary>
        public void SyncOutPrimUpdates()
        {
            if (!IsSyncingWithOtherSyncNodes())
            {
                //no SyncConnector connected. clear update queues and return.
                m_primPropertyUpdates.Clear();
                return;
            }

            //If no prim has been updated since last SyncOut, simply return.
            if (m_primPropertyUpdates.Count == 0)
                return;

            // Existing value of 1 indicates that updates are currently being sent so skip updates this pass
            if (Interlocked.Exchange(ref m_sendingPrimPropertyUpdates, 1) == 1)
            {
                m_log.DebugFormat("{0} SyncOutPrimUpdates(): An update thread is already running.", LogHeader);
                return;
            }

            //copy the updated prim property list, and clear m_primPropertyUpdates immediately for future use
            Dictionary<UUID, HashSet<SceneObjectPartSyncProperties>> primPropertyUpdates = new Dictionary<UUID, HashSet<SceneObjectPartSyncProperties>>();
            lock (m_primPropertyUpdateLock)
            {
                foreach (KeyValuePair<UUID, HashSet<SceneObjectPartSyncProperties>> updatedPrimProperties in m_primPropertyUpdates)
                {
                    UUID primUUID = updatedPrimProperties.Key;
                    SceneObjectPart prim = m_scene.GetSceneObjectPart(primUUID);
                    //Skip if the prim is on longer in the local Scene Graph
                    if (prim == null)
                    {
                        //m_log.WarnFormat("{0}: in SyncOutPrimUpdates, prim {1} no longer in local SceneGraph", LogHeader, primUUID);
                        continue;
                    }
                    //Skip if the object group is being deleted
                    if (prim.ParentGroup.IsDeleted)
                        continue;

                    primPropertyUpdates.Add(primUUID, updatedPrimProperties.Value);
                }
                m_primPropertyUpdates.Clear();
            }

            if (primPropertyUpdates.Count > 0)
            {
                //Starting a new thread to prepare sync message and enqueue it to SyncConnectors
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    foreach (KeyValuePair<UUID, HashSet<SceneObjectPartSyncProperties>> updatedPrimProperties in primPropertyUpdates)
                    {
                        UUID primUUID = updatedPrimProperties.Key;
                        SceneObjectPart sop = m_scene.GetSceneObjectPart(primUUID);

                        if (sop == null || sop.ParentGroup.IsDeleted)
                            continue;
                        else
                        {
                            HashSet<SceneObjectPartSyncProperties> updatedProperties = updatedPrimProperties.Value;

                            //Sync the SOP data and cached property values in PrimSyncInfoManager again
                            HashSet<SceneObjectPartSyncProperties> propertiesWithSyncInfoUpdated = m_primSyncInfoManager.UpdatePrimSyncInfoByLocal(sop, new List<SceneObjectPartSyncProperties>(updatedProperties));
                            updatedProperties.UnionWith(propertiesWithSyncInfoUpdated);
                            SendPrimPropertyUpdates(sop, updatedProperties);
                        }
                    }
                    
                    // Indicate that the current batch of updates has been completed
                    Interlocked.Exchange(ref m_sendingPrimPropertyUpdates, 0);
                });
            }
            else
            {
                Interlocked.Exchange(ref m_sendingPrimPropertyUpdates, 0);
            }
        }

        private void SendPrimPropertyUpdates(SceneObjectPart sop, HashSet<SceneObjectPartSyncProperties> updatedProperties)
        {
            OSDMap syncData = m_primSyncInfoManager.EncodePrimProperties(sop, updatedProperties);

            if (syncData.Count > 0)
            {
                //DSG DEBUG
                
                string pString = "";
                foreach (SceneObjectPartSyncProperties property in updatedProperties)
                {
                    pString += property.ToString() + " ";
                }
                //m_log.DebugFormat("{0}: SendPrimPropertyUpdates for {1}, {2}, with updated properties -- {3}", LogHeader, sop.Name, sop.UUID, pString);

                //DSG DEBUG

                if (updatedProperties.Contains(SceneObjectPartSyncProperties.AggregateScriptEvents))
                {
                    m_log.DebugFormat("SendPrimPropertyUpdates -- prim {0}: AggregateScriptEvents: {1} ", sop.Name, sop.AggregateScriptEvents);
                }
                /*
                if (updatedProperties.Contains(SceneObjectPartSyncProperties.Position))
                {
                    m_log.DebugFormat("SendPrimPropertyUpdates -- prim {0}: Position: {1} ", sop.Name, sop.PhysActor.Position);
                }
                 * */ 

                SymmetricSyncMessage syncMsg = new SymmetricSyncMessage(SymmetricSyncMessage.MsgType.UpdatedPrimProperties, OSDParser.SerializeJsonString(syncData));
                SendPrimUpdateToRelevantSyncConnectors(sop.UUID, syncMsg);
            }
        }

        //Object updates are sent by enqueuing into each connector's outQueue.
        private void SendPrimUpdateToRelevantSyncConnectors(UUID primUUID, SymmetricSyncMessage syncMsg)
        {
            SceneObjectPart updatedPart = m_scene.GetSceneObjectPart(primUUID);
            if (updatedPart == null)
                return;

            HashSet<SyncConnector> syncConnectors = GetSyncConnectorsForPrimUpdates(updatedPart);
            foreach (SyncConnector connector in syncConnectors)
            {
                connector.EnqueueOutgoingUpdate(updatedPart.UUID, syncMsg.ToBytes());
            }
        }

        /// <summary>
        /// Encode a SOG. Values of each part's properties are copied from 
        /// PrimSyncInfo, instead of from SOP's data. If a part's PrimSyncInfo
        /// is not maintained by PrimSyncInfoManager yet, add it first.
        /// </summary>
        /// <param name="sog"></param>
        /// <returns></returns>
        private OSDMap SceneObjectEncoder(SceneObjectGroup sog)
        {
            OSDMap data = new OSDMap();
            data["UUID"] = OSD.FromUUID(sog.UUID);

            Vector3 globalPos = sog.AbsolutePosition;
            data["GroupPosition"] = OSDMap.FromVector3(globalPos);

            HashSet<SceneObjectPartSyncProperties> fullPropertyList = new HashSet<SceneObjectPartSyncProperties>() { SceneObjectPartSyncProperties.FullUpdate };
            if (!m_primSyncInfoManager.IsPrimSyncInfoExist(sog.RootPart.UUID))
            {
                m_log.WarnFormat("{0}: SceneObjectEncoder -- SOP {1},{2} not in PrimSyncInfoManager's record yet", LogHeader, sog.RootPart.Name, sog.RootPart.UUID);
                //This should not happen, but we deal with it by inserting a newly created PrimSynInfo
                m_primSyncInfoManager.InsertPrimSyncInfo(sog.RootPart, DateTime.Now.Ticks, m_syncID);
            }
            data["RootPart"] = m_primSyncInfoManager.EncodePrimProperties(sog.RootPart, fullPropertyList);

            OSDArray otherPartsArray = new OSDArray();
            foreach (SceneObjectPart part in sog.Parts)
            {
                if (!part.UUID.Equals(sog.RootPart.UUID))
                {
                    if (!m_primSyncInfoManager.IsPrimSyncInfoExist(part.UUID))
                    {
                        m_log.WarnFormat("{0}: SceneObjectEncoder -- SOP {1},{2} not in PrimSyncInfoManager's record yet", LogHeader, part.Name, part.UUID);
                        //This should not happen, but we deal with it by inserting a newly created PrimSynInfo
                        m_primSyncInfoManager.InsertPrimSyncInfo(part, DateTime.Now.Ticks, m_syncID);
                    }
                    OSDMap partData = m_primSyncInfoManager.EncodePrimProperties(part, fullPropertyList);
                    otherPartsArray.Add(partData);
                }
            }
            data["OtherParts"] = otherPartsArray;

            return data;
        }

        private void AddNewSceneObjectByDecoding(OSDMap data)
        {
            SceneObjectGroup group;
            Dictionary<UUID, PrimSyncInfo> primsSyncInfo;

            SceneObjectDecoder(data, out group, out primsSyncInfo);

            if (group == null)
            {
                m_log.WarnFormat("{0}: AddNewSceneObjectByDecoding, no valid SceneObjectGroup has been created", LogHeader);
                return;
            }

            //DSG DEBUG
            m_log.DebugFormat("calling AddNewSceneObjectByDecoding for SOG {0}, {1}", group.Name, group.UUID);

            //Add the list of PrimSyncInfo to PrimSyncInfoManager's record.
            m_primSyncInfoManager.InsertMultiPrimSyncInfo(primsSyncInfo);

            //Add the decoded object to Scene
            Scene.ObjectUpdateResult updateResult = m_scene.AddNewSceneObjectBySync(group);

            //Now the PhysActor of each part in sog have been created, let's 
            //set the PhysActor's properties. Also trigger aggregateScriptEventSubscriptions
            //since it may access PhysActor to link collision events
            foreach (SceneObjectPart part in group.Parts)
            {
                m_primSyncInfoManager.SetSOPPhyscActorProperties(part);
                part.aggregateScriptEventSubscriptions();
            }

            group.ScheduleGroupForFullUpdate(null);
        }

        /// <summary>
        /// Decode & create a SOG data structure. Due to the fact that PhysActor
        /// is only created when SOG.AttachToScene() is called, the returned SOG
        /// here only have non PhysActor properties decoded and values set. The
        /// PhysActor properties should be set later by the caller.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="sog"></param>
        /// <param name="partsPrimSyncInfo"></param>
        private void SceneObjectDecoder(OSDMap data, out SceneObjectGroup sog, out Dictionary<UUID, PrimSyncInfo> partsPrimSyncInfo)
        {
            sog = new SceneObjectGroup();
            partsPrimSyncInfo = new Dictionary<UUID, PrimSyncInfo>();

            //if(data.ContainsKey("UUID")){
            //    sog.UUID = data["UUID"].AsUUID();
            //}

            if(!data.ContainsKey("RootPart")){
                m_log.WarnFormat("{0}: SceneObjectDecoder, no RootPart found in the OSDMap");
                return;
            }

            OSDMap rootData = (OSDMap)data["RootPart"];
            //Decode and copy to the list of PrimSyncInfo
            PrimSyncInfo primSyncInfo = m_primSyncInfoManager.DecodeFullSetPrimProperties(rootData);
            SceneObjectPart root= primSyncInfo.PrimSyncInfoToSOP();

            if (root != null)
            {
                sog.SetRootPart(root);
                partsPrimSyncInfo.Add(root.UUID, primSyncInfo);
            }
            else
                return;

            if (sog.UUID == UUID.Zero)
                sog.UUID = sog.RootPart.UUID;

            //Decode the remaining parts and add them to the object group
            if(data.ContainsKey("OtherParts")){
                //int otherPartsCount = data["OtherPartsCount"].AsInteger();
                OSDArray otherPartsArray = (OSDArray) data["OtherParts"]; 
                for(int i=0; i<otherPartsArray.Count; i++){
                    OSDMap partData = (OSDMap)otherPartsArray[i];
                    primSyncInfo = m_primSyncInfoManager.DecodeFullSetPrimProperties(partData);
                    SceneObjectPart part = primSyncInfo.PrimSyncInfoToSOP();

                    if(part!=null){
                        sog.AddPart(part);
                        partsPrimSyncInfo.Add(part.UUID, primSyncInfo);
                    }
                }
            }

            foreach (SceneObjectPart part in sog.Parts)
            {
                partsPrimSyncInfo[part.UUID].SetGroupProperties(part);
            }

        }

        #endregion //Prim Property Sync management


        #region ISyncStatistics
        private object m_stats = new object();
        private int m_statMsgsIn = 0;
        private int m_statMsgsOut = 0;
        private int m_statSOGBucketIn = 0;
        private int m_statSOGBucketOut = 0;
        private int m_statPhysBucketIn = 0;
        private int m_statPhysBucketOut = 0;
        private int m_statEventIn = 0;
        private int m_statEventOut = 0;
        public string StatisticIdentifier()
        {
            // RegionSyncModule(actor/region)
            return "RegionSyncModule" + "(" + ActorID + "/" + m_scene.RegionInfo.RegionName + ")";
        }

        public string StatisticLine(bool clearFlag)
        {
            string statLine = "";
            lock (m_stats)
            {
                statLine = String.Format("{0},{1},{2},{3},{4},{5},{6},{7}",
                    m_statMsgsIn, m_statMsgsOut,
                    m_statSOGBucketIn, m_statSOGBucketOut,
                    m_statPhysBucketIn, m_statPhysBucketOut,
                    m_statEventIn, m_statEventOut
                );
                if (clearFlag)
                {
                    m_statMsgsIn = m_statMsgsOut = 0;
                    m_statSOGBucketIn = m_statSOGBucketOut = 0;
                    m_statPhysBucketIn = m_statPhysBucketOut = 0;
                    m_statEventIn = m_statEventOut = 0;
                }
            }
            return statLine;
        }

        public string StatisticTitle()
        {
            return "MsgsIn,MsgsOut,SOGIn,SOGOut,PhysIn,PhysOut,EventIn,EventOut";
        }
        #endregion ISyncStatistics

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

    ///////////////////////////////////////////////////////////////////////////
    // 
    ///////////////////////////////////////////////////////////////////////////

    public enum PropertyUpdateSource
    {
        Local,
        BySync
    }

    public class PropertySyncInfo
    {
        //private ILog m_log;
        public static ILog DebugLog;

        private long m_lastUpdateTimeStamp;
        public long LastUpdateTimeStamp
        {
            get { return m_lastUpdateTimeStamp; }
        }

        private string m_lastUpdateSyncID;
        public string LastUpdateSyncID
        {
            get { return m_lastUpdateSyncID; }
        }

        /// <summary>
        /// The value of the most recent value sent/received by Sync Module.
        /// For property with simple types, the value is copied directly. 
        /// For property with complex data structures, the value (values of
        /// subproperties) is serialized and stored.
        /// </summary>
        private Object m_lastUpdateValue;
        public Object LastUpdateValue
        {
            get { return m_lastUpdateValue; }
            //set { m_lastUpdateValue = value; }
        }

        private string m_lastUpdateValueHash = String.Empty;
        public String LastUpdateValueHash
        {
            get {return m_lastUpdateValueHash;}
        }

        /// <summary>
        /// Record the time the last sync message about this property is received.
        /// This value is only meaninful when m_lastUpdateSource==BySync
        /// </summary>
        private long m_lastSyncUpdateRecvTime;
        public long LastSyncUpdateRecvTime
        {
            get { return m_lastSyncUpdateRecvTime; }
            set { m_lastSyncUpdateRecvTime = value; }
        }

        private PropertyUpdateSource m_lastUpdateSource;
        public PropertyUpdateSource LastUpdateSource
        {
            get { return m_lastUpdateSource; }
        }

        private SceneObjectPartSyncProperties m_property;
        public SceneObjectPartSyncProperties Property
        {
            get { return m_property; }
        }

        private Object m_syncInfoLock = new Object();

        public PropertySyncInfo(SceneObjectPartSyncProperties property)
        {
            m_property = property;
        }

        public PropertySyncInfo(PropertySyncInfo pSyncInfo)
        {
            m_property = pSyncInfo.Property;
            m_lastUpdateValue = pSyncInfo.LastUpdateValue;
            m_lastUpdateTimeStamp = pSyncInfo.LastUpdateTimeStamp;
            m_lastUpdateSyncID = pSyncInfo.LastUpdateSyncID;
            //m_lastSyncUpdateRecvTime == ??

            switch (m_property)
            {
                case SceneObjectPartSyncProperties.Shape:
                case SceneObjectPartSyncProperties.TaskInventory:
                    m_lastUpdateValueHash = GetPropertyHashValue((string)pSyncInfo.LastUpdateValue);
                    break;
            }
        }

        public PropertySyncInfo(SceneObjectPartSyncProperties property, Object initValue, long initTS, string syncID)
        {
            m_property = property;
            m_lastUpdateValue = initValue;
            m_lastUpdateTimeStamp = initTS;
            m_lastUpdateSyncID = syncID;
            //m_lastSyncUpdateRecvTime == ??

            switch (property)
            {
                case SceneObjectPartSyncProperties.Shape:
                case SceneObjectPartSyncProperties.TaskInventory:
                    m_lastUpdateValueHash = GetPropertyHashValue((string)initValue);
                    break;
            }
        }

        /// <summary>
        /// Initialize from data in given OSDMap.
        /// </summary>
        /// <param name="property"></param>
        /// <param name="syncData"></param>
        public PropertySyncInfo(SceneObjectPartSyncProperties property, OSDMap syncData)
        {
            m_property = property;
            FromOSDMap(syncData);
        }

        /// <summary>
        /// Update SyncInfo when the property is updated locally. This interface
        /// is for complex properties that need hashValue for fast comparison,
        /// such as Shape and TaskInventory.
        /// </summary>
        /// <param name="ts"></param>
        /// <param name="syncID"></param>
        /// <param name="pValue"></param>
        /// <param name="pHashedValue">This is only meaningful for complex properties:
        /// Shape & TaskInventory. For other properties, it is ignore.</param>
        public void UpdateSyncInfoByLocal(long ts, string syncID, Object pValue, string pHashedValue)
        {
            lock (m_syncInfoLock)
            {
                m_lastUpdateValue = pValue;
                m_lastUpdateTimeStamp = ts;
                m_lastUpdateSyncID = syncID;
                m_lastUpdateSource = PropertyUpdateSource.Local;
                m_lastUpdateValueHash = pHashedValue;
            }
        }

        /// <summary>
        /// Update SyncInfo when the property is updated locally. This interface
        /// is for properties of simple types.
        /// </summary>
        /// <param name="ts"></param>
        /// <param name="syncID"></param>
        /// <param name="pValue"></param>
        public void UpdateSyncInfoByLocal(long ts, string syncID, Object pValue)
        {
            lock (m_syncInfoLock)
            {
                m_lastUpdateValue = pValue;
                m_lastUpdateTimeStamp = ts;
                m_lastUpdateSyncID = syncID;
                m_lastUpdateSource = PropertyUpdateSource.Local;
            }
        }

        /// <summary>
        /// Compare the local timestamp with that in pSyncInfo. If the one in
        /// pSyncInfo is newer, copy its members to the local record.
        /// </summary>
        /// <param name="pSyncInfo"></param>
        /// <returns></returns>
        public bool CompareAndUpdateSyncInfoBySync(PropertySyncInfo pSyncInfo, long recvTS)
        {
            if (pSyncInfo.LastUpdateTimeStamp > m_lastUpdateTimeStamp)
            {
                //update
                UpdateSyncInfoBySync(pSyncInfo.m_lastUpdateTimeStamp, pSyncInfo.m_lastUpdateSyncID, pSyncInfo.m_lastUpdateValue, recvTS);
                return true;
            }
            else
            {
                if (pSyncInfo.LastUpdateTimeStamp == m_lastUpdateTimeStamp)
                {
                    if (!m_lastUpdateSyncID.Equals(pSyncInfo.LastUpdateSyncID))
                    {
                        DebugLog.WarnFormat("CompareAndUpdateSyncInfoBySync: property {0} being updated by {1} and {2} at the same time. Do nothing as of current implementation. TO RESOLVE.",
                            m_property, m_lastUpdateSyncID, pSyncInfo.LastUpdateSyncID);
                    }
                }
                return false;
            }

        }

        /// <summary>
        /// Update SyncInfo when the property is updated by receiving a sync 
        /// message.
        /// </summary>
        /// <param name="ts"></param>
        /// <param name="syncID"></param>
        public void UpdateSyncInfoBySync(long ts, string syncID, Object pValue, long recvTS)
        {
            lock (m_syncInfoLock)
            {
                m_lastUpdateValue = pValue;
                m_lastUpdateTimeStamp = ts;
                m_lastUpdateSyncID = syncID;
                m_lastSyncUpdateRecvTime = recvTS;
                m_lastUpdateSource = PropertyUpdateSource.BySync;

                switch (m_property)
                {
                    case SceneObjectPartSyncProperties.Shape:
                    case SceneObjectPartSyncProperties.TaskInventory:
                        m_lastUpdateValueHash = GetPropertyHashValue((string)m_lastUpdateValue);
                        break;
                }
            }
        }

        public bool IsHashValueEqual(string hashValue)
        {
            return m_lastUpdateValueHash.Equals(hashValue);
        }

        public bool IsValueEqual(Object pValue)
        {
            return m_lastUpdateValue.Equals(pValue);
        }

        /// <summary>
        /// Convert the value of the given property to OSD type.
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        public OSDMap ToOSDMap()
        {
            OSDMap propertyData = new OSDMap();
            propertyData["LastUpdateTimeStamp"] = LastUpdateTimeStamp;
            propertyData["LastUpdateSyncID"] = LastUpdateSyncID;

            switch (m_property)
            {
                ///////////////////////////////////////
                //SOP properties with complex structure
                ///////////////////////////////////////
                case SceneObjectPartSyncProperties.Shape:
                case SceneObjectPartSyncProperties.TaskInventory:
                    propertyData["Value"] = OSD.FromString((string)LastUpdateValue);
                    break;

                ////////////////////////////
                //SOP properties, enum types
                ////////////////////////////
                case SceneObjectPartSyncProperties.AggregateScriptEvents:
                    propertyData["Value"] = OSD.FromInteger((int)((scriptEvents)LastUpdateValue));
                    break;
                case SceneObjectPartSyncProperties.Flags:
                case SceneObjectPartSyncProperties.LocalFlags:
                    propertyData["Value"] = OSD.FromInteger((int)((PrimFlags)LastUpdateValue));
                    break;
                ////////////////////////////
                //SOP properties, bool types
                ////////////////////////////
                case SceneObjectPartSyncProperties.AllowedDrop:
                case SceneObjectPartSyncProperties.IsAttachment:
                case SceneObjectPartSyncProperties.PassTouches:
                case SceneObjectPartSyncProperties.VolumeDetectActive:
                    propertyData["Value"] = OSD.FromBoolean((bool)LastUpdateValue);
                    break;

                ////////////////////////////
                //SOP properties, Vector3 types
                ////////////////////////////
                case SceneObjectPartSyncProperties.AngularVelocity:
                case SceneObjectPartSyncProperties.AttachedPos:
                case SceneObjectPartSyncProperties.GroupPosition:
                case SceneObjectPartSyncProperties.OffsetPosition:
                case SceneObjectPartSyncProperties.Scale:
                case SceneObjectPartSyncProperties.SitTargetPosition:
                case SceneObjectPartSyncProperties.SitTargetPositionLL:
                case SceneObjectPartSyncProperties.SOP_Acceleration:
                case SceneObjectPartSyncProperties.Velocity:
                    propertyData["Value"] = OSD.FromVector3((Vector3)LastUpdateValue);
                    break;

                ////////////////////////////
                //SOP properties, UUID types
                ////////////////////////////
                case SceneObjectPartSyncProperties.AttachedAvatar:
                case SceneObjectPartSyncProperties.CollisionSound:
                case SceneObjectPartSyncProperties.CreatorID:
                case SceneObjectPartSyncProperties.FolderID:
                case SceneObjectPartSyncProperties.GroupID:
                case SceneObjectPartSyncProperties.LastOwnerID:
                case SceneObjectPartSyncProperties.OwnerID:
                case SceneObjectPartSyncProperties.Sound:
                    propertyData["Value"] = OSD.FromUUID((UUID)LastUpdateValue);
                    break;

                //case SceneObjectPartProperties.AttachedPos:
                ////////////////////////////
                //SOP properties, uint types
                ////////////////////////////
                case SceneObjectPartSyncProperties.AttachmentPoint:
                case SceneObjectPartSyncProperties.BaseMask:
                case SceneObjectPartSyncProperties.Category:
                case SceneObjectPartSyncProperties.EveryoneMask:
                case SceneObjectPartSyncProperties.GroupMask:
                case SceneObjectPartSyncProperties.InventorySerial:
                case SceneObjectPartSyncProperties.NextOwnerMask:
                case SceneObjectPartSyncProperties.OwnerMask:
                    propertyData["Value"] = OSD.FromUInteger((uint)LastUpdateValue);
                    break;

                //case SceneObjectPartProperties.BaseMask:
                //case SceneObjectPartProperties.Category:

                ////////////////////////////
                //SOP properties, byte types
                ////////////////////////////                    
                case SceneObjectPartSyncProperties.ClickAction:
                case SceneObjectPartSyncProperties.Material:
                case SceneObjectPartSyncProperties.ObjectSaleType:
                case SceneObjectPartSyncProperties.UpdateFlag:
                    propertyData["Value"] = OSD.FromInteger((byte)LastUpdateValue);
                    break;
                //case SceneObjectPartProperties.CollisionSound:

                ////////////////////////////
                //SOP properties, float types
                ////////////////////////////
                case SceneObjectPartSyncProperties.CollisionSoundVolume:
                    propertyData["Value"] = OSD.FromReal((float)LastUpdateValue);
                    break;

                ////////////////////////////
                //SOP properties, Color(struct type)
                ////////////////////////////
                case SceneObjectPartSyncProperties.Color:
                    propertyData["Value"] = OSD.FromString(PropertySerializer.SerializeColor((System.Drawing.Color)LastUpdateValue));
                    break;

                ////////////////////////////
                //SOP properties, int types
                ////////////////////////////
                case SceneObjectPartSyncProperties.CreationDate:
                case SceneObjectPartSyncProperties.LinkNum:
                case SceneObjectPartSyncProperties.OwnershipCost:
                case SceneObjectPartSyncProperties.SalePrice:
                case SceneObjectPartSyncProperties.ScriptAccessPin:
                    propertyData["Value"] = OSD.FromInteger((int)LastUpdateValue);
                    break;

                ////////////////////////////
                //SOP properties, string types
                ////////////////////////////
                case SceneObjectPartSyncProperties.CreatorData:
                case SceneObjectPartSyncProperties.Description:
                case SceneObjectPartSyncProperties.MediaUrl:
                case SceneObjectPartSyncProperties.Name:
                case SceneObjectPartSyncProperties.SitName:
                case SceneObjectPartSyncProperties.Text:
                case SceneObjectPartSyncProperties.TouchName:
                    propertyData["Value"] = OSD.FromString((string)LastUpdateValue);
                    break;
                ////////////////////////////
                //SOP properties, byte[]  types
                ////////////////////////////
                case SceneObjectPartSyncProperties.ParticleSystem:
                case SceneObjectPartSyncProperties.TextureAnimation:
                    propertyData["Value"] = OSD.FromBinary((byte[])LastUpdateValue);
                    break;

                ////////////////////////////
                //SOP properties, Quaternion  types
                ////////////////////////////
                case SceneObjectPartSyncProperties.RotationOffset:
                case SceneObjectPartSyncProperties.SitTargetOrientation:
                case SceneObjectPartSyncProperties.SitTargetOrientationLL:
                    propertyData["Value"] = OSD.FromQuaternion((Quaternion)LastUpdateValue);
                    break;

                ////////////////////////////////////
                //PhysActor properties, float type
                ////////////////////////////////////
                case SceneObjectPartSyncProperties.Buoyancy:
                    propertyData["Value"] = OSD.FromReal((float)LastUpdateValue);
                    break;

                ////////////////////////////////////
                //PhysActor properties, bool type
                ////////////////////////////////////
                case SceneObjectPartSyncProperties.Flying:
                case SceneObjectPartSyncProperties.IsColliding:
                case SceneObjectPartSyncProperties.CollidingGround:
                case SceneObjectPartSyncProperties.IsPhysical:
                case SceneObjectPartSyncProperties.Kinematic:
                    propertyData["Value"] = OSD.FromBoolean((bool)LastUpdateValue);
                    break;

                ////////////////////////////////////
                //PhysActor properties, Vector3 type
                ////////////////////////////////////
                case SceneObjectPartSyncProperties.Force:
                case SceneObjectPartSyncProperties.PA_Acceleration:
                case SceneObjectPartSyncProperties.Position:
                case SceneObjectPartSyncProperties.RotationalVelocity:
                case SceneObjectPartSyncProperties.Size:
                case SceneObjectPartSyncProperties.Torque:
                    propertyData["Value"] = OSD.FromVector3((Vector3)LastUpdateValue);
                    break;

                ////////////////////////////////////
                //PhysActor properties, Quaternion type
                ////////////////////////////////////
                case SceneObjectPartSyncProperties.Orientation:
                    propertyData["Value"] = OSD.FromQuaternion((Quaternion)LastUpdateValue);
                    break;

                ///////////////////////
                //SOG properties
                ///////////////////////
                case SceneObjectPartSyncProperties.IsSelected:
                    propertyData["Value"] = OSD.FromBoolean((bool)LastUpdateValue);
                    break;

                default:
                    DebugLog.WarnFormat("PrimSynInfo.PropertyToOSD -- no handler for property {0} ", m_property);
                    break;
            }
            return propertyData;
        }

        /// <summary>
        /// Set member values by decoding out of propertyData. Should only
        /// be called in initialization time (e.g. from constructor).
        /// </summary>
        /// <param name="propertyData"></param>
        private void FromOSDMap(OSDMap propertyData)
        {
            if (!propertyData.ContainsKey("LastUpdateTimeStamp"))
            {
                DebugLog.WarnFormat("PrimSynInfo.FromOSDMap -- OSDMap missing LastUpdateTimeStamp");
            }
            else
            {
                m_lastUpdateTimeStamp = propertyData["LastUpdateTimeStamp"].AsLong();
            }

            if (!propertyData.ContainsKey("LastUpdateSyncID"))
            {
                DebugLog.WarnFormat("PrimSynInfo.FromOSDMap -- OSDMap missing LastUpdateSyncID");
            }
            else
            {
                m_lastUpdateSyncID = propertyData["LastUpdateSyncID"].AsString();
            }

            //We do not test if propertyData.ContainsKey("Value"), since Jason
            //serialization seems does not include a value if it's equals to
            //the default value. So just let Jason decoding to set the value 
            //either by reading out of the OSDMap, or set to default value.

            switch (m_property)
            {
                ///////////////////////////////////////
                //SOP properties with complex structure
                ///////////////////////////////////////
                case SceneObjectPartSyncProperties.Shape:
                case SceneObjectPartSyncProperties.TaskInventory:
                    m_lastUpdateValue = (Object)propertyData["Value"].AsString();
                    m_lastUpdateValueHash = Util.Md5Hash((string)m_lastUpdateValue);
                    break;

                ////////////////////////////
                //SOP properties, enum types
                ////////////////////////////
                case SceneObjectPartSyncProperties.AggregateScriptEvents:
                    scriptEvents sEvents = (scriptEvents)(propertyData["Value"].AsInteger());
                    m_lastUpdateValue = (Object)sEvents;
                    //propertyData["Value"] = OSD.FromInteger((int)((scriptEvents)LastUpdateValue));
                    break;
                case SceneObjectPartSyncProperties.Flags:
                case SceneObjectPartSyncProperties.LocalFlags:
                    PrimFlags flags = (PrimFlags)(propertyData["Value"].AsInteger());
                    m_lastUpdateValue = (Object)flags;
                    //propertyData["Value"] = OSD.FromInteger((int)((PrimFlags)LastUpdateValue));
                    break;
                ////////////////////////////
                //SOP properties, bool types
                ////////////////////////////
                case SceneObjectPartSyncProperties.AllowedDrop:
                case SceneObjectPartSyncProperties.IsAttachment:
                case SceneObjectPartSyncProperties.PassTouches:
                case SceneObjectPartSyncProperties.VolumeDetectActive:
                    m_lastUpdateValue = (Object)(propertyData["Value"].AsBoolean());
                    //propertyData["Value"] = OSD.FromBoolean((bool)LastUpdateValue);
                    break;

                ////////////////////////////
                //SOP properties, Vector3 types
                ////////////////////////////
                case SceneObjectPartSyncProperties.AngularVelocity:
                case SceneObjectPartSyncProperties.AttachedPos:
                case SceneObjectPartSyncProperties.GroupPosition:
                case SceneObjectPartSyncProperties.OffsetPosition:
                case SceneObjectPartSyncProperties.Scale:
                case SceneObjectPartSyncProperties.SitTargetPosition:
                case SceneObjectPartSyncProperties.SitTargetPositionLL:
                case SceneObjectPartSyncProperties.SOP_Acceleration:
                case SceneObjectPartSyncProperties.Velocity:
                    //propertyData["Value"] = OSD.FromVector3((Vector3)LastUpdateValue);
                    m_lastUpdateValue = (Object)(propertyData["Value"].AsVector3());
                    break;

                ////////////////////////////
                //SOP properties, UUID types
                ////////////////////////////
                case SceneObjectPartSyncProperties.AttachedAvatar:
                case SceneObjectPartSyncProperties.CollisionSound:
                case SceneObjectPartSyncProperties.CreatorID:
                case SceneObjectPartSyncProperties.FolderID:
                case SceneObjectPartSyncProperties.GroupID:
                case SceneObjectPartSyncProperties.LastOwnerID:
                case SceneObjectPartSyncProperties.OwnerID:
                case SceneObjectPartSyncProperties.Sound:
                    //propertyData["Value"] = OSD.FromUUID((UUID)LastUpdateValue);
                    m_lastUpdateValue = (Object)(propertyData["Value"].AsUUID());
                    break;

                //case SceneObjectPartProperties.AttachedPos:
                ////////////////////////////
                //SOP properties, uint types
                ////////////////////////////
                case SceneObjectPartSyncProperties.AttachmentPoint:
                case SceneObjectPartSyncProperties.BaseMask:
                case SceneObjectPartSyncProperties.Category:
                case SceneObjectPartSyncProperties.EveryoneMask:
                case SceneObjectPartSyncProperties.GroupMask:
                case SceneObjectPartSyncProperties.InventorySerial:
                case SceneObjectPartSyncProperties.NextOwnerMask:
                case SceneObjectPartSyncProperties.OwnerMask:
                    //propertyData["Value"] = OSD.FromUInteger((uint)LastUpdateValue);
                    m_lastUpdateValue = (Object)(propertyData["Value"].AsUInteger());
                    break;

                //case SceneObjectPartProperties.BaseMask:
                //case SceneObjectPartProperties.Category:

                ////////////////////////////
                //SOP properties, byte types
                ////////////////////////////                    
                case SceneObjectPartSyncProperties.ClickAction:
                case SceneObjectPartSyncProperties.Material:
                case SceneObjectPartSyncProperties.ObjectSaleType:
                case SceneObjectPartSyncProperties.UpdateFlag:
                    //propertyData["Value"] = OSD.FromInteger((byte)LastUpdateValue);
                    byte pValue = (byte)(propertyData["Value"].AsInteger());
                    m_lastUpdateValue = (Object)pValue;
                    break;
                //case SceneObjectPartProperties.CollisionSound:

                ////////////////////////////
                //SOP properties, float types
                ////////////////////////////
                case SceneObjectPartSyncProperties.CollisionSoundVolume:
                    //propertyData["Value"] = OSD.FromReal((float)LastUpdateValue);
                    float csValue = (float)propertyData["Value"].AsReal();
                    m_lastUpdateValue = (Object)(csValue);
                    break;

                ////////////////////////////
                //SOP properties, Color(struct type)
                ////////////////////////////
                case SceneObjectPartSyncProperties.Color:
                    //propertyData["Value"] = OSD.FromString(PropertySerializer.SerializeColor((System.Drawing.Color)LastUpdateValue));
                    System.Drawing.Color cValue = PropertySerializer.DeSerializeColor(propertyData["Value"].AsString());
                    m_lastUpdateValue = (Object)cValue;
                    break;

                ////////////////////////////
                //SOP properties, int types
                ////////////////////////////
                case SceneObjectPartSyncProperties.CreationDate:
                case SceneObjectPartSyncProperties.LinkNum:
                case SceneObjectPartSyncProperties.OwnershipCost:
                case SceneObjectPartSyncProperties.SalePrice:
                case SceneObjectPartSyncProperties.ScriptAccessPin:
                    //propertyData["Value"] = OSD.FromInteger((int)LastUpdateValue);
                    m_lastUpdateValue = (Object)(propertyData["Value"].AsInteger());
                    break;

                ////////////////////////////
                //SOP properties, string types
                ////////////////////////////
                case SceneObjectPartSyncProperties.CreatorData:
                case SceneObjectPartSyncProperties.Description:
                case SceneObjectPartSyncProperties.MediaUrl:
                case SceneObjectPartSyncProperties.Name:
                case SceneObjectPartSyncProperties.SitName:
                case SceneObjectPartSyncProperties.Text:
                case SceneObjectPartSyncProperties.TouchName:
                    //propertyData["Value"] = OSD.FromString((string)LastUpdateValue);
                    m_lastUpdateValue = (Object)(propertyData["Value"].AsString());
                    break;
                ////////////////////////////
                //SOP properties, byte[]  types
                ////////////////////////////
                case SceneObjectPartSyncProperties.ParticleSystem:
                case SceneObjectPartSyncProperties.TextureAnimation:
                    //propertyData["Value"] = OSD.FromBinary((byte[])LastUpdateValue);
                    m_lastUpdateValue = (Object)(propertyData["Value"].AsBinary());
                    break;

                ////////////////////////////
                //SOP properties, Quaternion  types
                ////////////////////////////
                case SceneObjectPartSyncProperties.RotationOffset:
                case SceneObjectPartSyncProperties.SitTargetOrientation:
                case SceneObjectPartSyncProperties.SitTargetOrientationLL:
                    //propertyData["Value"] = OSD.FromQuaternion((Quaternion)LastUpdateValue);
                    m_lastUpdateValue = (Object)(propertyData["Value"].AsQuaternion());
                    break;

                ////////////////////////////////////
                //PhysActor properties, float type
                ////////////////////////////////////
                case SceneObjectPartSyncProperties.Buoyancy:
                    //propertyData["Value"] = OSD.FromReal((float)LastUpdateValue);
                    float bValue = (float)propertyData["Value"].AsReal();
                    m_lastUpdateValue = (Object)(bValue);
                    break;

                ////////////////////////////////////
                //PhysActor properties, bool type
                ////////////////////////////////////
                case SceneObjectPartSyncProperties.Flying:
                case SceneObjectPartSyncProperties.IsColliding:
                case SceneObjectPartSyncProperties.CollidingGround:
                case SceneObjectPartSyncProperties.IsPhysical:
                case SceneObjectPartSyncProperties.Kinematic:
                    //propertyData["Value"] = OSD.FromBoolean((bool)LastUpdateValue);
                    m_lastUpdateValue = (Object)(propertyData["Value"].AsBoolean());
                    break;

                ////////////////////////////////////
                //PhysActor properties, Vector3 type
                ////////////////////////////////////
                case SceneObjectPartSyncProperties.Force:
                case SceneObjectPartSyncProperties.PA_Acceleration:
                case SceneObjectPartSyncProperties.Position:
                case SceneObjectPartSyncProperties.RotationalVelocity:
                case SceneObjectPartSyncProperties.Size:
                case SceneObjectPartSyncProperties.Torque:
                    //propertyData["Value"] = OSD.FromVector3((Vector3)LastUpdateValue);
                    m_lastUpdateValue = (Object)(propertyData["Value"].AsVector3());
                    break;

                ////////////////////////////////////
                //PhysActor properties, Quaternion type
                ////////////////////////////////////
                case SceneObjectPartSyncProperties.Orientation:
                    //propertyData["Value"] = OSD.FromQuaternion((Quaternion)LastUpdateValue);
                    m_lastUpdateValue = (Object)(propertyData["Value"].AsQuaternion());
                    break;

                ///////////////////////
                //SOG properties
                ///////////////////////
                case SceneObjectPartSyncProperties.IsSelected:
                    //propertyData["Value"] = OSD.FromBoolean((bool)LastUpdateValue);
                    m_lastUpdateValue = (Object)(propertyData["Value"].AsBoolean());
                    break;

                default:
                    DebugLog.WarnFormat("PrimSynInfo.PropertyToOSD -- no handler for property {0} ", m_property);
                    break;

            }
        }

        private string GetPropertyHashValue(string initValue)
        {
            return Util.Md5Hash(initValue);
        }

    }

    public class PropertySerializer
    {
        //TO BE TESTED


        public static string SerializeShape(SceneObjectPart part)
        {
            string serializedShape;
            using (StringWriter sw = new StringWriter())
            {
                using (XmlTextWriter writer = new XmlTextWriter(sw))
                {
                    SceneObjectSerializer.WriteShape(writer, part.Shape, new Dictionary<string, object>());
                }
                serializedShape = sw.ToString();
            }
            return serializedShape;
        }

        public static PrimitiveBaseShape DeSerializeShape(string shapeString)
        {
            if (shapeString == null || shapeString == String.Empty || shapeString == "")
            {
                return null;
            }
            StringReader sr = new StringReader(shapeString);
            XmlTextReader reader = new XmlTextReader(sr);
            PrimitiveBaseShape shapeValue;
            try
            {
                shapeValue = SceneObjectSerializer.ReadShape(reader, "Shape");
            }
            catch (Exception e)
            {
                Console.WriteLine("DeSerializeShape: Error " + e.Message);
                return null;
            }
            return shapeValue;
        }

        public static string SerializeTaskInventory(SceneObjectPart part)
        {
            string serializedTaskInventory;
            using (StringWriter sw = new StringWriter())
            {
                using (XmlTextWriter writer = new XmlTextWriter(sw))
                {
                    SceneObjectSerializer.WriteTaskInventory(writer, part.TaskInventory, new Dictionary<string, object>(), part.ParentGroup.Scene);
                }
                serializedTaskInventory = sw.ToString();
            }
            return serializedTaskInventory;
        }

        public static TaskInventoryDictionary DeSerializeTaskInventory(string taskInvString)
        {
            if (taskInvString == null || taskInvString == String.Empty || taskInvString == "")
            {
                return null;
            }
            StringReader sr = new StringReader(taskInvString);
            XmlTextReader reader = new XmlTextReader(sr);
            TaskInventoryDictionary taskVal;
            try
            {
                taskVal = SceneObjectSerializer.ReadTaskInventory(reader, "TaskInventory");
            }catch(Exception e)
            {
                Console.WriteLine("DeSerializeTaskInventory: Error " + e.Message);
                return null;
            }
            return taskVal;
        }

        //Copy code from SceneObjectSerializer.SOPToXml2
        public static string SerializeColor(System.Drawing.Color color)
        {
            string serializedColor;
            using (StringWriter sw = new StringWriter())
            {
                using (XmlTextWriter writer = new XmlTextWriter(sw))
                {
                    writer.WriteStartElement("Color");
                    writer.WriteElementString("R", color.R.ToString(Utils.EnUsCulture));
                    writer.WriteElementString("G", color.G.ToString(Utils.EnUsCulture));
                    writer.WriteElementString("B", color.B.ToString(Utils.EnUsCulture));
                    writer.WriteElementString("A", color.G.ToString(Utils.EnUsCulture));
                    writer.WriteEndElement();
                }
                serializedColor = sw.ToString();
            }
            return serializedColor;
        }

        //Copy code from SceneObjectSerializer.ProcessColor
        public static System.Drawing.Color DeSerializeColor(string colorString)
        {
            StringReader sr = new StringReader(colorString);
            XmlTextReader reader = new XmlTextReader(sr);

            System.Drawing.Color color = new System.Drawing.Color();

            reader.ReadStartElement("Color");
            if (reader.Name == "R")
            {
                float r = reader.ReadElementContentAsFloat("R", String.Empty);
                float g = reader.ReadElementContentAsFloat("G", String.Empty);
                float b = reader.ReadElementContentAsFloat("B", String.Empty);
                float a = reader.ReadElementContentAsFloat("A", String.Empty);
                color = System.Drawing.Color.FromArgb((int)a, (int)r, (int)g, (int)b);
                reader.ReadEndElement();
            }
            return color;
        }

    }

    public class PrimSyncInfo
    {
        #region Members
        public static long TimeOutThreshold;
        public static ILog DebugLog;

        private Dictionary<SceneObjectPartSyncProperties, PropertySyncInfo> m_propertiesSyncInfo = new Dictionary<SceneObjectPartSyncProperties,PropertySyncInfo>();
        public Dictionary<SceneObjectPartSyncProperties, PropertySyncInfo> PropertiesSyncInfo
        {
            get { return m_propertiesSyncInfo; }
        }

        private long m_PrimLastUpdateTime;
        public long PrimLastUpdateTime
        {
            get { return m_PrimLastUpdateTime; }
        }

        private UUID m_UUID;
        public UUID UUID
        {
            get { return m_UUID; }
        }

        private Object m_primSyncInfoLock = new Object();
        public static HashSet<SceneObjectPartSyncProperties> FullSetPrimProperties = SceneObjectPart.GetAllPrimProperties();
        public static HashSet<SceneObjectPartSyncProperties> PrimPhysActorProperties = SceneObjectPart.GetAllPhysActorProperties();
        public static HashSet<SceneObjectPartSyncProperties> PrimNonPhysActorProperties = SceneObjectPart.GetAllPrimNonPhysActorProperties();
        public static HashSet<SceneObjectPartSyncProperties> GroupProperties = SceneObjectPart.GetGroupProperties();

        private PrimSyncInfoManager m_syncInfoManager;

        #endregion //Members

        #region Constructors

        public PrimSyncInfo(SceneObjectPart part, long initUpdateTimestamp, string syncID, PrimSyncInfoManager manager)
        {
            m_UUID = part.UUID;
            m_syncInfoManager = manager;
            InitPropertiesSyncInfo(part, initUpdateTimestamp, syncID);
        }

        public PrimSyncInfo(PrimSyncInfoManager manager)
        {
            m_syncInfoManager = manager;
        }

        public PrimSyncInfo(UUID id, OSDMap primSyncInfoData, PrimSyncInfoManager manager)
        {
            m_UUID = id;
            m_syncInfoManager = manager;
            InitPropertiesSyncInfoFromOSDMap(primSyncInfoData);
        }

        #endregion //Constructors

        public void UpdatePropertyWithHashByLocal(SceneObjectPartSyncProperties property, long lastUpdateTS, string syncID, Object pValue, string pHashedValue)
        {
            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateTS, syncID, pValue, pHashedValue);
        }

        //Triggered when a set of local writes just happened, and ScheduleFullUpdate 
        //or ScheduleTerseUpdate has been called.
        /// <summary>
        /// Update copies of the given list of properties in the prim's SyncInfo.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="updatedProperties"></param>
        /// <param name="lastUpdateTS"></param>
        /// <param name="syncID"></param>
        public HashSet<SceneObjectPartSyncProperties> UpdatePropertiesByLocal(SceneObjectPart part, List<SceneObjectPartSyncProperties> updatedProperties, long lastUpdateTS, string syncID)
        {
            //DSG DEBUG
            /*
            if (updatedProperties.Contains(SceneObjectPartSyncProperties.Shape))
            {
                DebugLog.DebugFormat("UpdatePropertiesByLocal: To update SOP {0},{1} Shape to be {2}", part.Name, part.UUID, part.Shape.ProfileShape);
            }*/ 
            

            HashSet<SceneObjectPartSyncProperties> propertiesToBeSynced = new HashSet<SceneObjectPartSyncProperties>(updatedProperties);
            if (part == null) 
            {
                propertiesToBeSynced.Clear();
                return propertiesToBeSynced;
            }

            //pre-process properties in updatedProperties, for sanity checking
            //or expanding/shrinking
            foreach (SceneObjectPartSyncProperties property in updatedProperties)
            {
                //first, see if there are physics properties updated but PhysActor
                //does not exist
                if (SceneObjectPart.GetAllPhysActorProperties().Contains(property))
                {
                    if (part.PhysActor == null)
                    {
                        DebugLog.WarnFormat("PrimSyncInfo: Informed some physics property in SOP updated, yet SOP's PhysActor no longer exsits.");
                        return propertiesToBeSynced;
                    }
                }
                else
                {
                    switch (property)
                    {
                        case SceneObjectPartSyncProperties.FullUpdate:
                            //Caller indicated many properties have changed. We need to 
                            //compare and update all properties
                            if (part.PhysActor == null)
                                propertiesToBeSynced = PrimNonPhysActorProperties;
                            else
                                propertiesToBeSynced = FullSetPrimProperties;
                            break;
                            //return propertiesToBeSynced;
                        case SceneObjectPartSyncProperties.None:
                            propertiesToBeSynced.Clear();
                            break;
                    }
                }
            }

            if(propertiesToBeSynced.Count==0){
                return propertiesToBeSynced;
            }

            //Second, for each updated property in the list, find out the ones 
            //that really have recently been updated by local operations
            HashSet<SceneObjectPartSyncProperties> propertiesUpdatedByLocal = new HashSet<SceneObjectPartSyncProperties>();

            //DSG DEBUG
            /*
            if (propertiesToBeSynced.Contains(SceneObjectPartSyncProperties.Shape))
            {
                DebugLog.DebugFormat("UpdatePropertiesByLocal: to update cache of SOP {0}, {1} Shape to {2}", part.Name, part.UUID, part.Shape.ProfileShape);
            }
             * */ 

            lock (m_primSyncInfoLock)
            {
                foreach (SceneObjectPartSyncProperties property in propertiesToBeSynced)
                {
                    bool updated = false;
                    //Compare if the value of the property in this SyncModule is 
                    //different than the value in SOP
                    switch (property)
                    {
                        case SceneObjectPartSyncProperties.Shape:
                        case SceneObjectPartSyncProperties.TaskInventory:
                            //Convert the value of complex properties to string and hash
                            updated = CompareHashedValue_UpdateByLocal(part, property, lastUpdateTS, syncID);
                            break;
                        default:
                            updated = CompareValue_UpdateByLocal(part, property, lastUpdateTS, syncID);
                            break;
                    }

                    if (updated)
                    {
                        propertiesUpdatedByLocal.Add(property);
                    }
                }
            }
            return propertiesUpdatedByLocal;
        }

        //TODO: might return status such as Updated, Unchanged, etc to caller
        public List<SceneObjectPartSyncProperties> UpdatePropertiesBySync(SceneObjectPart part, HashSet<PropertySyncInfo> propertiesSyncInfo)
        {
            long recvTS = DateTime.Now.Ticks;
            List<SceneObjectPartSyncProperties> propertiesUpdated = new List<SceneObjectPartSyncProperties>();

            lock (m_primSyncInfoLock)
            {
                foreach (PropertySyncInfo pSyncInfo in propertiesSyncInfo)
                {
                    bool updated = false;
                    SceneObjectPartSyncProperties property = pSyncInfo.Property;
                    //Compare if the value of the property in this SyncModule is 
                    //different than the value in SOP
                    if (!m_propertiesSyncInfo.ContainsKey(property))
                    {
                        //could happen if PhysActor is just created (object stops being phantom)
                        if (PrimSyncInfo.PrimPhysActorProperties.Contains(property))
                        {
                            PropertySyncInfo syncInfo = new PropertySyncInfo(pSyncInfo);
                            m_propertiesSyncInfo.Add(property, syncInfo);   
                        }
                        else
                        {
                            DebugLog.WarnFormat("PrimSyncInfo.UpdatePropertiesBySync -- no record of property {0} for SOP {1},{2}", property, part.Name, part.UUID);
                        }
                    }
                    else
                    {
                        try
                        {
                            //Compare timestamp and update SyncInfo if necessary
                            updated = m_propertiesSyncInfo[property].CompareAndUpdateSyncInfoBySync(pSyncInfo, recvTS);
                            //If updated, update the property value in SOP
                            if (updated)
                            {
                                //DSG DEBUG
                                /*
                                if (property == SceneObjectPartSyncProperties.Shape)
                                {
                                    DebugLog.DebugFormat("UpdatePropertiesBySync: updating Shape of {0}, {1}", part.Name, part.UUID);
                                }
                                 * */

                                //UpdateSOPProperty(part, m_propertiesSyncInfo[property]);
                                SetSOPPropertyValue(part, property);
                                propertiesUpdated.Add(property);
                            }
                        }
                        catch (Exception e)
                        {
                            DebugLog.ErrorFormat("Error in updating property {0}: {1}", property, e.Message);
                        }
                    }
                }
            }
            return propertiesUpdated;
        }

        public List<SceneObjectPartSyncProperties> UpdatePropertiesBySync(SceneObjectPart part, PrimSyncInfo updatedPrimSyncInfo)
        {
            HashSet<PropertySyncInfo> propertiesSyncInfo = new HashSet<PropertySyncInfo>(updatedPrimSyncInfo.PropertiesSyncInfo.Values);
            return UpdatePropertiesBySync(part, propertiesSyncInfo);
        }


        /// <summary>
        /// Encode the SyncInfo of each property, including its current value 
        /// maintained in this SyncModule, its timestamp and syncID.
        /// </summary>
        /// <param name="propertiesToSync">The list of properties to be encoded. 
        /// If FullUpdate is included, then encode all properties.</param>
        /// <returns></returns>
        public OSDMap EncodePropertiesSyncInfo(HashSet<SceneObjectPartSyncProperties> propertiesToSync)
        {
            OSDMap propertyData = new OSDMap();

            //Lock first, so that we effectively freeze the record and take a snapshot
            lock (m_primSyncInfoLock)
            {
                if (propertiesToSync.Contains(SceneObjectPartSyncProperties.FullUpdate))
                {
                    foreach (SceneObjectPartSyncProperties property in FullSetPrimProperties)
                    {
                        if (m_propertiesSyncInfo.ContainsKey(property))
                            propertyData.Add(property.ToString(), m_propertiesSyncInfo[property].ToOSDMap());
                        else
                        {
                            DebugLog.WarnFormat("PrimSyncInfo: property {0} not in sync cache", property);
                        }
                    }
                }
                else
                {
                    foreach (SceneObjectPartSyncProperties property in propertiesToSync)
                    {
                        if (m_propertiesSyncInfo.ContainsKey(property))
                            propertyData.Add(property.ToString(), m_propertiesSyncInfo[property].ToOSDMap());
                        else
                        {
                            DebugLog.WarnFormat("PrimSyncInfo: property {0} not in sync cache", property);
                        }
                    }
                }
            }
            return propertyData;
        }

        /// <summary>
        /// Decode a set of PropertySyncInfo from OSDMap.
        /// </summary>
        /// <param name="primSyncInfoData"></param>
        public static HashSet<PropertySyncInfo> DecodeProperiesSyncInfo(OSDMap primSyncInfoData)
        {
            HashSet<PropertySyncInfo> propertiesSyncInfo = new HashSet<PropertySyncInfo>();

            foreach (string propertyString in primSyncInfoData.Keys)
            {
                //convert string to enum
                SceneObjectPartSyncProperties property = (SceneObjectPartSyncProperties)Enum.Parse(typeof(SceneObjectPartSyncProperties), propertyString, true);
                if (FullSetPrimProperties.Contains(property))
                {
                    //PropertySyncInfo propertySyncInfo = OSDMapToPropertySyncInfo((OSDMap)primSyncInfoData[property.ToString()]);
                    PropertySyncInfo propertySyncInfo = new PropertySyncInfo(property, (OSDMap)primSyncInfoData[propertyString]);
                    propertiesSyncInfo.Add(propertySyncInfo);
                }else{
                    //DebugLog.WarnFormat("DecodeAndSetProperiesSyncInfo: Property {0} not a valid SceneObjectPartSyncProperties", propertyString);
                }
            }
            return propertiesSyncInfo;
        }

        //Should only be called after receiving a message with encoded prim properties,
        //
        /// <summary>
        /// Create a SOP instance based on the properties in PrimSyncInfo. Should 
        /// only be called after receiving a message with encoded prim properties,
        /// and the SOP with the given UUID does not exsit locally yet.
        /// </summary>
        /// <returns></returns>
        public SceneObjectPart PrimSyncInfoToSOP()
        {
            SceneObjectPart sop = new SceneObjectPart();
            sop.UUID = m_UUID;

            foreach (SceneObjectPartSyncProperties property in FullSetPrimProperties)
            {
                if (m_propertiesSyncInfo.ContainsKey(property))
                {
                    try
                    {
                        SetSOPPropertyValue(sop, property);
                    }
                    catch (Exception e)
                    {
                        DebugLog.ErrorFormat("Error in setting SOP property {0}: {1}", property, e.Message);
                    }
                }
                else
                {
                    //This might just be fine. For phantom objects, they don't have 
                    //PhysActor properties, and those properties would end up here.

                    //DebugLog.WarnFormat("PrimSyncInfoToSOP -- property {0} not in record.", property);
                }
            }
            return sop;
        }

        public void SetSOPPhyscActorProperties(SceneObjectPart sop)
        {
            foreach (SceneObjectPartSyncProperties property in PrimPhysActorProperties)
            {
                SetSOPPropertyValue(sop, property);
            }
        }

        public void SetGroupProperties(SceneObjectPart sop)
        {
            foreach (SceneObjectPartSyncProperties property in GroupProperties)
            {
                SetSOPPropertyValue(sop, property);
            }
        }

        private Scene GetLocalScene()
        {
            return m_syncInfoManager.GetLocalScene();
        }

        /// <summary>
        /// Initialize the properties with the values in the given SOP. 
        /// </summary>
        /// <param name="part"></param>
        /// <param name="initUpdateTimestamp"></param>
        /// <param name="syncID"></param>
        private void InitPropertiesSyncInfo(SceneObjectPart part, long initUpdateTimestamp, string syncID)
        {
            m_propertiesSyncInfo.Clear();
            HashSet<SceneObjectPartSyncProperties> initPrimProperties;

            if (part.PhysActor == null)
                initPrimProperties = PrimNonPhysActorProperties;
            else
                initPrimProperties = FullSetPrimProperties;

            lock (m_primSyncInfoLock)
            {
                foreach (SceneObjectPartSyncProperties property in initPrimProperties)
                {
                    Object initValue = GetSOPPropertyValue(part, property);
                    PropertySyncInfo syncInfo = new PropertySyncInfo(property, initValue, initUpdateTimestamp, syncID);
                    m_propertiesSyncInfo.Add(property, syncInfo);
                }
            }
        }

        /// <summary>
        /// Decode PropertySyncInfo for each property and insert into m_propertiesSyncInfo.
        /// This is called to initialize this PrimSyncInfo by decoding from OSDMap.
        /// </summary>
        /// <param name="primSyncInfoData"></param>
        private void InitPropertiesSyncInfoFromOSDMap(OSDMap primSyncInfoData)
        {
            lock (m_primSyncInfoLock)
            {
                m_propertiesSyncInfo.Clear();
                foreach (SceneObjectPartSyncProperties property in FullSetPrimProperties)
                {
                    if (primSyncInfoData.ContainsKey(property.ToString()))
                    {
                        PropertySyncInfo propertySyncInfo = new PropertySyncInfo(property, (OSDMap)primSyncInfoData[property.ToString()]);
                        m_propertiesSyncInfo.Add(property, propertySyncInfo);
                    }
                    else
                    {
                        //For Phantom prims, they don't have PhysActor properties. So this branch could happen.
                        //DebugLog.WarnFormat("InitPropertiesSyncInfoFromOSDMap: Property {0} not included in the given OSDMap", property);
                    }
                }
            }
        }

        //Assumption: the caller already locks the access lock, and no need to lock here
        private bool CompareHashedValue_UpdateByLocal(SceneObjectPart part, SceneObjectPartSyncProperties property, long lastUpdateTS, string syncID)
        {
            bool updated = false;
            if (!m_propertiesSyncInfo.ContainsKey(property))
            {
                Object initValue = GetSOPPropertyValue(part, property);
                PropertySyncInfo syncInfo = new PropertySyncInfo(property, initValue, lastUpdateTS, syncID);
                m_propertiesSyncInfo.Add(property, syncInfo);
                return true;
            }
            switch (property)
            {
                case SceneObjectPartSyncProperties.Shape:
                    string primShapeString = PropertySerializer.SerializeShape(part);
                    string primShapeStringHash = Util.Md5Hash(primShapeString);
                    
                    if (!m_propertiesSyncInfo[property].IsHashValueEqual(primShapeStringHash))
                    {
                        //The SOP's property value has a newer timestamp, update the data in
                        //PrimSyncInfoManager to be consistent; otherwise, overwrite SOP's property
                        //value by copying that from PrimSyncInfoManager
                        if (lastUpdateTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            UpdatePropertyWithHashByLocal(property, lastUpdateTS, syncID, (Object)primShapeString, primShapeStringHash);

                            //DSG DEBUG
                            //DebugLog.DebugFormat("CompareHashedValue_UpdateByLocal - Shape of {0}, {1} updated to ProfileShape {2}: SOP hashed shape: {3}, cached hash {4}",
                            //   part.Name, part.UUID, part.Shape.ProfileShape, primShapeStringHash, m_propertiesSyncInfo[property].LastUpdateValueHash);

                            updated = true;
                        }
                        else if (lastUpdateTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            PrimitiveBaseShape shape = PropertySerializer.DeSerializeShape((string)m_propertiesSyncInfo[property].LastUpdateValue);
                            part.Shape = shape;
                        }

                    }
                    break;
                case SceneObjectPartSyncProperties.TaskInventory:
                    string primTaskInventoryString = PropertySerializer.SerializeTaskInventory(part);
                    string primTaskInventoryStringHash = Util.Md5Hash(primTaskInventoryString);
                    if (!m_propertiesSyncInfo[property].IsHashValueEqual(primTaskInventoryStringHash))
                    {
                        if (lastUpdateTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            UpdatePropertyWithHashByLocal(property, lastUpdateTS, syncID, (Object)primTaskInventoryString, primTaskInventoryStringHash);
                            updated = true;
                        }
                        else if (lastUpdateTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            TaskInventoryDictionary taskInv = PropertySerializer.DeSerializeTaskInventory((string)m_propertiesSyncInfo[property].LastUpdateValue);
                            part.TaskInventory = taskInv;
                        }
                    }
                    break;
                default:
                    break;
            }
            return updated;
        }


        /// <summary>
        /// Compare the value (not "reference") of the given property. 
        /// Assumption: the caller has already checked if PhysActor exists
        /// if there are physics properties updated.
        /// If the value maintained here is different from that in SOP data,
        /// synchronize the two: 
        /// (1) if the value here has a timestamp newer than lastUpdateByLocalTS 
        /// (e.g. due to clock drifts among different sync nodes, a remote
        /// write might have a newer timestamp than the local write), 
        /// overwrite the SOP's property with the value here (effectively 
        /// disvalidate the local write operation that just happened). 
        /// (2) otherwise, copy SOP's data and update timestamp and syncID 
        /// as indicated by "lastUpdateByLocalTS" and "syncID".
        /// </summary>
        /// <param name="part"></param>
        /// <param name="property"></param>
        /// <param name="lastUpdateByLocalTS"></param>
        /// <param name="syncID"></param>
        /// <returns>Return true if the property's value maintained in this 
        /// RegionSyncModule is replaced by SOP's data.</returns>
        private bool CompareValue_UpdateByLocal(SceneObjectPart part, SceneObjectPartSyncProperties property, long lastUpdateByLocalTS, string syncID)
        {
            bool propertyUpdatedByLocal = false;

            if (!m_propertiesSyncInfo.ContainsKey(property))
            {
                Object initValue = GetSOPPropertyValue(part, property);
                PropertySyncInfo syncInfo = new PropertySyncInfo(property, initValue, lastUpdateByLocalTS, syncID);
                m_propertiesSyncInfo.Add(property, syncInfo);
                return true;
            }

            //First, check if the value maintained here is different from that 
            //in SOP's. If different, next check if the timestamp in SyncInfo is
            //bigger (newer) than lastUpdateByLocalTS; if so (although ideally 
            //should not happen, but due to things likc clock not so perfectly 
            //sync'ed, it might happen), overwrite SOP's value with what's maintained
            //in SyncInfo; otherwise, copy SOP's data to SyncInfo.
            
            //Note: for properties handled in this 
            //function, they are mainly value types (int, bool, struct, etc).
            //So they are copied by value, not by reference. 
            //For a few properties, we copy by clone.
            switch (property)
            {
                ///////////////////////
                //SOP properties
                ///////////////////////
                case SceneObjectPartSyncProperties.AggregateScriptEvents:
                    if (!part.AggregateScriptEvents.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //copy from SOP's data
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.AggregateScriptEvents);
                            propertyUpdatedByLocal = true;

                            //TEMP DEBUG
                            DebugLog.DebugFormat("CompareValue_UpdateByLocal -- copy SOP's AggregateScriptEvents {0}", part.AggregateScriptEvents);
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.AggregateScriptEvents = (scriptEvents) m_propertiesSyncInfo[property].LastUpdateValue; 
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.AllowedDrop:
                    if (!part.AllowedDrop.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.AllowedDrop);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.AllowedDrop = (bool)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.AngularVelocity:
                    if (!part.AngularVelocity.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.AngularVelocity);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.AngularVelocity = (Vector3)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.AttachedAvatar:
                    if (!part.AttachedAvatar.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.AttachedAvatar);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.AttachedAvatar = (UUID)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.AttachedPos:
                    if (!part.AttachedPos.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.AttachedPos);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.AttachedPos = (Vector3)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.AttachmentPoint:
                    if (!part.AttachmentPoint.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.AttachmentPoint);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.AttachmentPoint = (uint)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.BaseMask:
                    if (!part.BaseMask.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.BaseMask);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.BaseMask = (uint)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.Category:
                    if (!part.Category.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.Category);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.Category = (uint)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.ClickAction:
                    if (!part.ClickAction.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.ClickAction);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            SetSOPCollisionSound(part, (UUID)m_propertiesSyncInfo[property].LastUpdateValue);
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.CollisionSound:
                    if (!part.CollisionSound.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.CollisionSound);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.CollisionSound = (UUID)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.CollisionSoundVolume:
                    if (!part.CollisionSoundVolume.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.CollisionSoundVolume);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.CollisionSoundVolume = (float)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.Color:
                    if (!part.Color.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.Color);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.Color = (System.Drawing.Color)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.CreationDate:
                    if (!part.CreationDate.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.CreationDate);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.CreationDate = (int)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.CreatorData:
                    if (!part.CreatorData.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.CreatorData.Clone());
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.CreatorData = (string)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.CreatorID:
                    if (!part.CreatorID.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.CreatorID);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.CreatorID = (UUID)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.Description:
                    if (!part.Description.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.Description.Clone());
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.Description = (string)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }       
                    break;
                case SceneObjectPartSyncProperties.EveryoneMask:
                    if (!part.EveryoneMask.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.EveryoneMask);
                             propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.EveryoneMask = (uint)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }       
                    break;
                case SceneObjectPartSyncProperties.Flags:
                    if (!part.Flags.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                        m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.Flags);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.Flags = (PrimFlags)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }       
                    break;
                case SceneObjectPartSyncProperties.FolderID:
                    if (!part.FolderID.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.FolderID);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.FolderID = (UUID)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }    
                    break;
                //Skip SceneObjectPartProperties.FullUpdate, which should be handled seperatedly
                case SceneObjectPartSyncProperties.GroupID:
                    if (!part.GroupID.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.GroupID);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.GroupID = (UUID)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.GroupMask:
                    if (!part.GroupMask.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.GroupMask);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.GroupMask = (uint)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.GroupPosition:
                    propertyUpdatedByLocal = CompareAndUpdateSOPGroupPosition(part, lastUpdateByLocalTS, syncID);
                    break;
                case SceneObjectPartSyncProperties.InventorySerial:
                    if (!part.InventorySerial.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.InventorySerial);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.InventorySerial = (uint)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.IsAttachment:
                    if (!part.IsAttachment.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.IsAttachment);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.IsAttachment = (bool)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.LastOwnerID:
                    if (!part.LastOwnerID.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.LastOwnerID);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.LastOwnerID = (UUID)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.LinkNum:
                    if (!part.LinkNum.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.LinkNum);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.LinkNum = (int)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.LocalFlags:
                    if (!part.LocalFlags.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.LocalFlags);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.LocalFlags = (PrimFlags)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.Material:
                    if (!part.Material.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.Material);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.Material = (byte)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.MediaUrl:
                    //Use "!=" to compare, instead of "Equals", because part.MediaUrl 
                    //might be null.
                    //String assignment in C# create a new copy of the string, hence we do
                    //not call clone() explicitly, so that the case of "part.MediaUrl==null"
                    //can be handled properly as well
                    if (part.MediaUrl != (string) m_propertiesSyncInfo[property].LastUpdateValue)
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.MediaUrl);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.MediaUrl = (string)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.Name:
                    if (!part.Name.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.Name.Clone());
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.Name = (string)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.NextOwnerMask:
                    if (!part.NextOwnerMask.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.NextOwnerMask);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.NextOwnerMask = (uint)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.ObjectSaleType:
                    if (!part.ObjectSaleType.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                        {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                        m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.ObjectSaleType);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.ObjectSaleType = (byte)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.OffsetPosition:
                    if (!part.OffsetPosition.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                        {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                        m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.OffsetPosition);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.OffsetPosition = (Vector3)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.OwnerID:
                    if (!part.OwnerID.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.OwnerID);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.OwnerID = (UUID)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.OwnerMask:
                    if (!part.OwnerMask.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.OwnerMask);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.OwnerMask = (uint)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.OwnershipCost:
                    if (!part.OwnershipCost.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.OwnershipCost);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.OwnershipCost = (int)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.ParticleSystem:
                    if (!ByteArrayEquals(part.ParticleSystem, (Byte[])m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, part.ParticleSystem.Clone());
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            byte[] pValue = (byte[])m_propertiesSyncInfo[property].LastUpdateValue;
                            part.ParticleSystem = (byte[])pValue.Clone();
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.PassTouches:
                    if (!part.PassTouches.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.PassTouches);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.PassTouches = (bool)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.RotationOffset:
                    if (!part.RotationOffset.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.RotationOffset);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.RotationOffset = (Quaternion)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.SalePrice:
                    if (!part.SalePrice.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                        {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                        m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.SalePrice);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.SalePrice = (int)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.Scale:
                    if (!part.Scale.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                        {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                        m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.Scale);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.Scale = (Vector3)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.ScriptAccessPin:
                    if (!part.ScriptAccessPin.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                        {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                        m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.ScriptAccessPin);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.ScriptAccessPin = (int)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                //case SceneObjectPartProperties.Shape: -- For "Shape", we need to call CompareHashValues
                case SceneObjectPartSyncProperties.SitName:
                    if (!part.SitName.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.SitName.Clone());
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.SitName = (string)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.SitTargetOrientation:
                    if (!part.SitTargetOrientation.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.SitTargetOrientation);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.SitTargetOrientation = (Quaternion)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.SitTargetOrientationLL:
                    if (!part.SitTargetOrientationLL.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                        {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                        m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.SitTargetOrientationLL);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.SitTargetOrientationLL = (Quaternion)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.SitTargetPosition:
                    if (!part.SitTargetPosition.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                        {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                        m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.SitTargetPosition);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.SitTargetOrientation = (Quaternion)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.SitTargetPositionLL:
                    if (!part.SitTargetPositionLL.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                        {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                        m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.SitTargetPositionLL);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.SitTargetPosition = (Vector3)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.SOP_Acceleration:
                    if (!part.Acceleration.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.Acceleration);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.Acceleration = (Vector3)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.Sound:
                    if (!part.Sound.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.Sound);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.Sound = (UUID)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                //case SceneObjectPartProperties.TaskInventory:-- For "TaskInventory", we need to call CompareHashValues
                case SceneObjectPartSyncProperties.Text:
                    if (!part.Text.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.Text.Clone());
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.Text = (string)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.TextureAnimation:
                    if (!ByteArrayEquals(part.TextureAnimation, (Byte[])m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, part.TextureAnimation.Clone());
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            byte[] pValue = (byte[])m_propertiesSyncInfo[property].LastUpdateValue;
                            part.TextureAnimation = (byte[])pValue.Clone();
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.TouchName:
                    if (!part.TouchName.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.TouchName.Clone());
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.TouchName = (string)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.UpdateFlag:
                    if (!part.UpdateFlag.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.UpdateFlag);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.UpdateFlag = (byte)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.Velocity:
                    if (!part.Velocity.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.Velocity);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.Velocity = (Vector3)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.VolumeDetectActive:
                    if (!part.VolumeDetectActive.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.VolumeDetectActive);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite SOP's data
                            part.VolumeDetectActive = (bool)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;

                ///////////////////////
                //PhysActor properties
                ///////////////////////
                case SceneObjectPartSyncProperties.Buoyancy:
                    if (!part.PhysActor.Buoyancy.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.PhysActor.Buoyancy);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite PhysActor's data
                            part.PhysActor.Buoyancy = (float)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.Flying:
                    if (!part.PhysActor.Flying.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.PhysActor.Flying);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite PhysActor's data
                            part.PhysActor.Flying = (bool)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.Force:
                    if (!part.PhysActor.Force.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                        {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                        m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.PhysActor.Force);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite PhysActor's data
                            part.PhysActor.Force = (Vector3)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.IsColliding:
                    if (!part.PhysActor.IsColliding.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.PhysActor.IsColliding);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite PhysActor's data
                            part.PhysActor.IsColliding = (bool)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                 case SceneObjectPartSyncProperties.CollidingGround:
                    if (!part.PhysActor.CollidingGround.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.PhysActor.CollidingGround);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite PhysActor's data
                            part.PhysActor.CollidingGround = (bool)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.IsPhysical:
                    if (!part.PhysActor.IsPhysical.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.PhysActor.IsPhysical);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite PhysActor's data
                            part.PhysActor.IsPhysical = (bool)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.Kinematic:
                    if (!part.PhysActor.Kinematic.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                        {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                        m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.PhysActor.Kinematic);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite PhysActor's data
                            part.PhysActor.Kinematic = (bool)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.Orientation:
                    if (!part.PhysActor.Orientation.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.PhysActor.Orientation);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite PhysActor's data
                            part.PhysActor.Orientation = (Quaternion)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.PA_Acceleration:
                    if (!part.PhysActor.Acceleration.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.PhysActor.Acceleration);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite PhysActor's data
                            part.PhysActor.Acceleration = (Vector3)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.Position:
                    propertyUpdatedByLocal = CompareAndUpdateSOPPosition(part, lastUpdateByLocalTS, syncID);
                    break;
                case SceneObjectPartSyncProperties.RotationalVelocity:
                    if (!part.PhysActor.RotationalVelocity.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.PhysActor.RotationalVelocity);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite PhysActor's data
                            part.PhysActor.RotationalVelocity = (Vector3)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.Size:
                    if (!part.PhysActor.Size.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                        {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                        m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.PhysActor.Size);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite PhysActor's data
                            part.PhysActor.Size = (Vector3)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
                case SceneObjectPartSyncProperties.Torque:
                    if (!part.PhysActor.Torque.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.PhysActor.Torque);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite PhysActor's data
                            part.PhysActor.Torque = (Vector3)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;

                ///////////////////////
                //SOG properties
                ///////////////////////
                case SceneObjectPartSyncProperties.IsSelected:
                    if (!part.ParentGroup.IsSelected.Equals(m_propertiesSyncInfo[property].LastUpdateValue))
                    {
                        if (lastUpdateByLocalTS > m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            m_propertiesSyncInfo[property].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.ParentGroup.IsSelected);
                            propertyUpdatedByLocal = true;
                        }
                        else if (lastUpdateByLocalTS < m_propertiesSyncInfo[property].LastUpdateTimeStamp)
                        {
                            //overwrite PhysActor's data
                            part.ParentGroup.IsSelected = (bool)m_propertiesSyncInfo[property].LastUpdateValue;
                        }
                    }
                    break;
            }

            return propertyUpdatedByLocal;
        }


        private bool ByteArrayEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }
       
        private Object GetSOPPropertyValue(SceneObjectPart part, SceneObjectPartSyncProperties property)
        {
            if (part == null) return null;

            Object pValue = null;
            switch (property)
            {
                case SceneObjectPartSyncProperties.Shape:
                    return (Object)PropertySerializer.SerializeShape(part);
                case SceneObjectPartSyncProperties.TaskInventory:
                    return (Object)PropertySerializer.SerializeTaskInventory(part);

                ///////////////////////
                //SOP properties
                ///////////////////////
                case SceneObjectPartSyncProperties.AggregateScriptEvents:
                    return (Object)part.AggregateScriptEvents;
                case SceneObjectPartSyncProperties.AllowedDrop:
                    return (Object)part.AllowedDrop;
                case SceneObjectPartSyncProperties.AngularVelocity:
                    return (Object)part.AngularVelocity;
                case SceneObjectPartSyncProperties.AttachedAvatar:
                    return (Object)part.AttachedAvatar;
                case SceneObjectPartSyncProperties.AttachedPos:
                    return (Object)part.AttachedPos;
                case SceneObjectPartSyncProperties.AttachmentPoint:
                    return (Object)part.AttachmentPoint;
                case SceneObjectPartSyncProperties.BaseMask:
                    return (Object)part.BaseMask;
                case SceneObjectPartSyncProperties.Category:
                    return (Object)part.Category;
                case SceneObjectPartSyncProperties.ClickAction:
                    return (Object)part.ClickAction;
                case SceneObjectPartSyncProperties.CollisionSound:
                    return (Object)part.CollisionSound;
                case SceneObjectPartSyncProperties.CollisionSoundVolume:
                    return (Object)part.CollisionSoundVolume;
                case SceneObjectPartSyncProperties.Color:
                    return (Object)part.Color;
                case SceneObjectPartSyncProperties.CreationDate:
                    return (Object)part.CreationDate;
                case SceneObjectPartSyncProperties.CreatorData:
                    return (Object)part.CreatorData;
                case SceneObjectPartSyncProperties.CreatorID:
                    return (Object)part.CreatorID;
                case SceneObjectPartSyncProperties.Description:
                    return (Object)part.Description;
                case SceneObjectPartSyncProperties.EveryoneMask:
                    return (Object)part.EveryoneMask;
                case SceneObjectPartSyncProperties.Flags:
                    return (Object)part.Flags;
                case SceneObjectPartSyncProperties.FolderID:
                    return (Object)part.FolderID;
                //Skip SceneObjectPartProperties.FullUpdate, which should be handled seperatedly
                case SceneObjectPartSyncProperties.GroupID:
                    return (Object)part.GroupID;
                case SceneObjectPartSyncProperties.GroupMask:
                    return (Object)part.GroupMask;
                case SceneObjectPartSyncProperties.GroupPosition:
                    return (Object)part.GroupPosition;
                case SceneObjectPartSyncProperties.InventorySerial:
                    return (Object)part.InventorySerial;
                case SceneObjectPartSyncProperties.IsAttachment:
                    return (Object)part.IsAttachment;
                case SceneObjectPartSyncProperties.LastOwnerID:
                    return (Object)part.LastOwnerID;
                case SceneObjectPartSyncProperties.LinkNum:
                    return (Object)part.LinkNum;
                case SceneObjectPartSyncProperties.LocalFlags:
                    return (Object)part.LocalFlags;
                case SceneObjectPartSyncProperties.Material:
                    return (Object)part.Material;
                case SceneObjectPartSyncProperties.MediaUrl:
                    return (Object)part.MediaUrl;
                case SceneObjectPartSyncProperties.Name:
                    return (Object)part.Name;
                case SceneObjectPartSyncProperties.NextOwnerMask:
                    return (Object)part.NextOwnerMask;
                case SceneObjectPartSyncProperties.ObjectSaleType:
                    return (Object)part.ObjectSaleType;
                case SceneObjectPartSyncProperties.OffsetPosition:
                    return (Object)part.OffsetPosition;
                case SceneObjectPartSyncProperties.OwnerID:
                    return (Object)part.OwnerID;
                case SceneObjectPartSyncProperties.OwnerMask:
                    return (Object)part.OwnerMask;
                case SceneObjectPartSyncProperties.OwnershipCost:
                    return (Object)part.OwnershipCost;
                case SceneObjectPartSyncProperties.ParticleSystem:
                    //byte[], return a cloned copy
                    return (Object)part.ParticleSystem.Clone();
                case SceneObjectPartSyncProperties.PassTouches:
                    return (Object)part.PassTouches;
                case SceneObjectPartSyncProperties.RotationOffset:
                    return (Object)part.RotationOffset;
                case SceneObjectPartSyncProperties.SalePrice:
                    return (Object)part.SalePrice;
                case SceneObjectPartSyncProperties.Scale:
                    return (Object)part.Scale;
                case SceneObjectPartSyncProperties.ScriptAccessPin:
                    return (Object)part.ScriptAccessPin;
                //case SceneObjectPartProperties.Shape: -- For "Shape", we need to call CompareHashValues
                case SceneObjectPartSyncProperties.SitName:
                    return (Object)part.SitName;
                case SceneObjectPartSyncProperties.SitTargetOrientation:
                    return (Object)part.SitTargetOrientation;
                case SceneObjectPartSyncProperties.SitTargetOrientationLL:
                    return (Object)part.SitTargetOrientationLL;
                case SceneObjectPartSyncProperties.SitTargetPosition:
                    return (Object)part.SitTargetPosition;
                case SceneObjectPartSyncProperties.SitTargetPositionLL:
                    return (Object)part.SitTargetPositionLL;
                case SceneObjectPartSyncProperties.SOP_Acceleration:
                    return (Object)part.Acceleration;
                case SceneObjectPartSyncProperties.Sound:
                    return (Object)part.Sound;
                //case SceneObjectPartProperties.TaskInventory:-- For "TaskInventory", we need to call CompareHashValues
                case SceneObjectPartSyncProperties.Text:
                    return (Object)part.Text;
                case SceneObjectPartSyncProperties.TextureAnimation:
                    //byte[], return a cloned copy
                    return part.TextureAnimation.Clone();
                case SceneObjectPartSyncProperties.TouchName:
                    return (Object)part.TouchName;
                case SceneObjectPartSyncProperties.UpdateFlag:
                    return (Object)part.UpdateFlag;
                case SceneObjectPartSyncProperties.Velocity:
                    return (Object)part.Velocity;
                case SceneObjectPartSyncProperties.VolumeDetectActive:
                    return (Object)part.VolumeDetectActive;

                ///////////////////////
                //PhysActor properties
                ///////////////////////
                case SceneObjectPartSyncProperties.Buoyancy:
                    if (part.PhysActor == null)
                        return null;
                    return (Object)part.PhysActor.Buoyancy;
                case SceneObjectPartSyncProperties.Flying:
                    if (part.PhysActor == null)
                        return null;
                    return (Object)part.PhysActor.Flying;
                case SceneObjectPartSyncProperties.Force:
                    if (part.PhysActor == null)
                        return null;
                    return (Object)part.PhysActor.Force;
                case SceneObjectPartSyncProperties.IsColliding:
                    if (part.PhysActor == null)
                        return null;
                    return (Object)part.PhysActor.IsColliding;
                case SceneObjectPartSyncProperties.CollidingGround:
                    if (part.PhysActor == null)
                        return null;
                    return (Object)part.PhysActor.CollidingGround;
                case SceneObjectPartSyncProperties.IsPhysical:
                    if (part.PhysActor == null)
                        return null;
                    return (Object)part.PhysActor.IsPhysical;
                case SceneObjectPartSyncProperties.Kinematic:
                    if (part.PhysActor == null)
                        return null;
                    return (Object)part.PhysActor.Kinematic;
                case SceneObjectPartSyncProperties.Orientation:
                    if (part.PhysActor == null)
                        return null;
                    return (Object)part.PhysActor.Orientation;
                case SceneObjectPartSyncProperties.PA_Acceleration:
                    if (part.PhysActor == null)
                        return null;
                    return (Object)part.PhysActor.Acceleration;
                case SceneObjectPartSyncProperties.Position:
                    if (part.PhysActor == null)
                        return null;
                    return (Object)part.PhysActor.Position;
                case SceneObjectPartSyncProperties.RotationalVelocity:
                    if (part.PhysActor == null)
                        return null;
                    return (Object)part.PhysActor.RotationalVelocity;
                case SceneObjectPartSyncProperties.Size:
                    if (part.PhysActor == null)
                        return null;
                    return (Object)part.PhysActor.Size;
                case SceneObjectPartSyncProperties.Torque:
                    if (part.PhysActor == null)
                        return null;
                    return (Object)part.PhysActor.Torque;

                ///////////////////////
                //SOG properties
                ///////////////////////
                case SceneObjectPartSyncProperties.IsSelected:
                    return (Object)part.ParentGroup.IsSelected;
            }

            return pValue;
        }

        /// <summary>
        /// Set the property's value based on the value maintained in PrimSyncInfoManager.
        /// Assumption: caller will call ScheduleFullUpdate to enqueue updates properly.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="property"></param>
        private void SetSOPPropertyValue(SceneObjectPart part, SceneObjectPartSyncProperties property)
        {
            if (part == null) return;
            if (!m_propertiesSyncInfo.ContainsKey(property)){
                //DebugLog.WarnFormat("SetSOPPropertyValue: property {0} not in record.", property.ToString());
                //For phantom prims, they don't have physActor properties, 
                //so for those properties, simply return
                return;
            }

            if (!m_propertiesSyncInfo.ContainsKey(property))
            {
                DebugLog.WarnFormat("PrimSyncInfo: property {0} not in sync cache", property);
                return;
            }
            PropertySyncInfo pSyncInfo = m_propertiesSyncInfo[property];

            switch (property)
            {
                case SceneObjectPartSyncProperties.Shape:
                    PrimitiveBaseShape shapeVal = PropertySerializer.DeSerializeShape((string)pSyncInfo.LastUpdateValue);
                    if (shapeVal != null)
                    {
                        part.Shape = shapeVal;
                        /*
                        String hashedShape = Util.Md5Hash((PropertySerializer.SerializeShape(part)));
                        DebugLog.DebugFormat("Shape of SOP {0}, {1}, changed, hashed shape = {2} in SOP, = {3} in PrimSyncInfoManager",
                            part.Name, part.UUID, hashedShape, pSyncInfo.LastUpdateValueHash);
                         * */
                         
                    }
                    else
                    {

                    }
                    break;
                case SceneObjectPartSyncProperties.TaskInventory:
                    TaskInventoryDictionary taskVal = PropertySerializer.DeSerializeTaskInventory((string)pSyncInfo.LastUpdateValue);
                    if (taskVal != null)
                        part.TaskInventory = taskVal;
                    break;

                ///////////////////////
                //SOP properties
                ///////////////////////
                case SceneObjectPartSyncProperties.AggregateScriptEvents:
                    part.AggregateScriptEvents = (scriptEvents)pSyncInfo.LastUpdateValue;
                    DebugLog.DebugFormat("set {0} value to be {1}", property.ToString(), part.AggregateScriptEvents);
                    part.aggregateScriptEventSubscriptions();

                    break;
                case SceneObjectPartSyncProperties.AllowedDrop:
                    part.AllowedDrop = (bool)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.AngularVelocity:
                    part.AngularVelocity = (Vector3)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.AttachedAvatar:
                    //part.AttachedAvatar = (UUID)pSyncInfo.LastUpdateValue;
                    UUID attachedAvatar = (UUID)pSyncInfo.LastUpdateValue;
                    if (!part.AttachedAvatar.Equals(attachedAvatar))
                    {
                        part.AttachedAvatar = attachedAvatar;
                        if (attachedAvatar != UUID.Zero)
                        {
                            Scene localScene = GetLocalScene();
                            ScenePresence avatar = localScene.GetScenePresence(attachedAvatar);
                            //It is possible that the avatar has not been fully 
                            //created locally when attachment objects are sync'ed.
                            //So we need to check if the avatar already exists.
                            //If not, handling of NewAvatar will evetually trigger
                            //calling of SetParentLocalId.
                            if (avatar != null)
                            {
                                if (part.ParentGroup != null)
                                {
                                    part.ParentGroup.RootPart.SetParentLocalId(avatar.LocalId);
                                }
                                else
                                {
                                    //If this SOP is not a part of group yet, record the 
                                    //avatar's localID for now. If this SOP is rootpart of
                                    //the group, then the localID is the right setting; 
                                    //otherwise, this SOP will be linked to the SOG it belongs
                                    //to later, and that will rewrite the parent localID.
                                    part.SetParentLocalId(avatar.LocalId);
                                }
                            }
                        }
                        else
                        {
                            part.SetParentLocalId(0);
                        }

                    }
                    break;
                case SceneObjectPartSyncProperties.AttachedPos:
                    part.AttachedPos = (Vector3)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.AttachmentPoint:
                    //part.AttachmentPoint = (uint)pSyncInfo.LastUpdateValue;
                    part.SetAttachmentPoint((uint)pSyncInfo.LastUpdateValue);
                    break;
                case SceneObjectPartSyncProperties.BaseMask:
                    part.BaseMask = (uint)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.Category:
                    part.Category = (uint)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.ClickAction:
                    part.ClickAction = (byte)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.CollisionSound:
                    //part.CollisionSound = (UUID)pSyncInfo.LastUpdateValue;
                    SetSOPCollisionSound(part, (UUID)pSyncInfo.LastUpdateValue);
                    break;
                case SceneObjectPartSyncProperties.CollisionSoundVolume:
                    part.CollisionSoundVolume = (float)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.Color:
                    part.Color = (System.Drawing.Color)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.CreationDate:
                    part.CreationDate = (int)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.CreatorData:
                    part.CreatorData = (string)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.CreatorID:
                    part.CreatorID = (UUID)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.Description:
                    part.Description = (string)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.EveryoneMask:
                    part.EveryoneMask = (uint)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.Flags:
                    //part.Flags = (PrimFlags)pSyncInfo.LastUpdateValue;
                    SetSOPFlags(part, (PrimFlags)pSyncInfo.LastUpdateValue);
                    break;
                case SceneObjectPartSyncProperties.FolderID:
                    part.FolderID = (UUID)pSyncInfo.LastUpdateValue;
                    break;
                //Skip SceneObjectPartProperties.FullUpdate, which should be handled seperatedly
                case SceneObjectPartSyncProperties.GroupID:
                    part.GroupID = (UUID)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.GroupMask:
                    part.GroupMask = (uint)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.GroupPosition:
                    part.GroupPosition = (Vector3)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.InventorySerial:
                    part.InventorySerial = (uint)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.IsAttachment:
                    part.IsAttachment = (bool)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.LastOwnerID:
                    part.LastOwnerID = (UUID)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.LinkNum:
                    part.LinkNum = (int)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.LocalFlags:
                    part.LocalFlags = (PrimFlags)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.Material:
                    part.Material = (byte)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.MediaUrl:
                    part.MediaUrl = (string)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.Name:
                    part.Name = (string)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.NextOwnerMask:
                    part.NextOwnerMask = (uint)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.ObjectSaleType:
                    part.ObjectSaleType = (byte)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.OffsetPosition:
                    part.OffsetPosition = (Vector3)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.OwnerID:
                    part.OwnerID = (UUID)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.OwnerMask:
                    part.OwnerMask = (uint)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.OwnershipCost:
                    part.OwnershipCost = (int)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.ParticleSystem:
                    //byte[], return a cloned copy
                    byte[] pValue = (byte[])pSyncInfo.LastUpdateValue;
                    part.ParticleSystem = (byte[])pValue.Clone();
                    break;
                case SceneObjectPartSyncProperties.PassTouches:
                    part.PassTouches = (bool)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.RotationOffset:
                    part.RotationOffset = (Quaternion)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.SalePrice:
                    part.SalePrice = (int)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.Scale:
                    part.Scale = (Vector3)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.ScriptAccessPin:
                    part.ScriptAccessPin = (int)pSyncInfo.LastUpdateValue;
                    break;
                //case SceneObjectPartProperties.Shape: -- For "Shape", we need to call CompareHashValues
                case SceneObjectPartSyncProperties.SitName:
                    part.SitName = (string)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.SitTargetOrientation:
                    part.SitTargetOrientation = (Quaternion)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.SitTargetOrientationLL:
                    part.SitTargetOrientationLL = (Quaternion)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.SitTargetPosition:
                    part.SitTargetPosition = (Vector3)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.SitTargetPositionLL:
                    part.SitTargetPositionLL = (Vector3)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.SOP_Acceleration:
                    part.Acceleration = (Vector3)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.Sound:
                    part.Sound = (UUID)pSyncInfo.LastUpdateValue;
                    break;
                //case SceneObjectPartProperties.TaskInventory:-- For "TaskInventory", we need to call CompareHashValues
                case SceneObjectPartSyncProperties.Text:
                    part.Text = (string)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.TextureAnimation:
                    //byte[], return a cloned copy
                    byte[] tValue = (byte[])pSyncInfo.LastUpdateValue;
                    part.TextureAnimation = (byte[])tValue.Clone();
                    break;
                case SceneObjectPartSyncProperties.TouchName:
                    part.TouchName = (string)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.UpdateFlag:
                    part.UpdateFlag = (byte)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.Velocity:
                    part.Velocity = (Vector3)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.VolumeDetectActive:
                    //part.ParentGroup.UpdatePrimFlagsBySync(part.LocalId, part., IsTemporary, IsPhantom, part.VolumeDetectActive);
                    bool isVD = (bool)pSyncInfo.LastUpdateValue;
                    DebugLog.DebugFormat("VolumeDetectActive updated on SOP {0}, to {1}", part.Name, isVD);
                    if (part.ParentGroup != null)
                    {
                        DebugLog.DebugFormat("calling ScriptSetVolumeDetectBySync");
                        part.ParentGroup.ScriptSetVolumeDetectBySync(isVD);
                    }
                    part.VolumeDetectActive = isVD;
                    part.aggregateScriptEventSubscriptions();
                    break;

                ///////////////////////
                //PhysActor properties
                ///////////////////////
                case SceneObjectPartSyncProperties.Buoyancy:
                    if (part.PhysActor != null)
                        part.PhysActor.Buoyancy = (float)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.Flying:
                    if (part.PhysActor != null)
                        part.PhysActor.Flying = (bool)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.Force:
                    if (part.PhysActor != null)
                        part.PhysActor.Force = (Vector3)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.IsColliding:
                    if (part.PhysActor != null)
                        part.PhysActor.IsColliding = (bool)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.CollidingGround:
                    if (part.PhysActor != null)
                        part.PhysActor.CollidingGround = (bool)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.IsPhysical:
                    if (part.PhysActor != null)
                        part.PhysActor.IsPhysical = (bool)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.Kinematic:
                    if (part.PhysActor != null)
                        part.PhysActor.Kinematic = (bool)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.Orientation:
                    if (part.PhysActor != null)
                        part.PhysActor.Orientation = (Quaternion)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.PA_Acceleration:
                    if (part.PhysActor != null)
                        part.PhysActor.Acceleration = (Vector3)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.Position:
                    if (part.PhysActor != null)
                        part.PhysActor.Position = (Vector3)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.RotationalVelocity:
                    if (part.PhysActor != null)
                        part.PhysActor.RotationalVelocity = (Vector3)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.Size:
                    if (part.PhysActor != null)
                        part.PhysActor.Size = (Vector3)pSyncInfo.LastUpdateValue;
                    break;
                case SceneObjectPartSyncProperties.Torque:
                    if (part.PhysActor != null)
                        part.PhysActor.Torque = (Vector3)pSyncInfo.LastUpdateValue;
                    break;

                ///////////////////////
                //SOG properties
                ///////////////////////
                case SceneObjectPartSyncProperties.IsSelected:
                    if (part.ParentGroup != null)
                        part.ParentGroup.IsSelected = (bool)pSyncInfo.LastUpdateValue;
                    break;
            }

            //Calling ScheduleFullUpdate to trigger enqueuing updates for sync'ing (relay sync nodes need to do so)
            //part.ScheduleFullUpdate(new List<SceneObjectPartSyncProperties>() { property }); 
        }

        //Do not call "part.CollisionSound =" to go through its set function.
        //We don't want the side effect of calling aggregateScriptEvents.
        private void SetSOPCollisionSound(SceneObjectPart part, UUID cSound)
        {
            if (part.UpdateCollisionSound(cSound))
            {
                part.ParentGroup.Scene.EventManager.TriggerAggregateScriptEvents(part);
            }
        }

        private void SetSOPFlags(SceneObjectPart part, PrimFlags flags)
        {
            //Do not set part.Flags yet, 
            //part.Flags = flags;

            bool UsePhysics = (flags & PrimFlags.Physics) != 0;
            bool IsTemporary = (flags & PrimFlags.TemporaryOnRez) != 0;
            //bool IsVolumeDetect = part.VolumeDetectActive;
            bool IsPhantom = (flags & PrimFlags.Phantom) != 0;

            if (part.ParentGroup != null)
            {
                part.ParentGroup.UpdatePrimFlagsBySync(part.LocalId, UsePhysics, IsTemporary, IsPhantom, part.VolumeDetectActive);
                //part.UpdatePrimFlagsBySync(UsePhysics, IsTemporary, IsPhantom, part.VolumeDetectActive);
            }
            part.Flags = flags;
            part.aggregateScriptEventSubscriptions();
            part.ScheduleFullUpdate(null);
        }

        //In SOP's implementation, GroupPosition and SOP.PhysActor.Position are 
        //correlated. We need to make sure that they are both properly synced.
        private bool CompareAndUpdateSOPGroupPosition(SceneObjectPart part, long lastUpdateByLocalTS, string syncID)
        {
            if (!part.GroupPosition.Equals(m_propertiesSyncInfo[SceneObjectPartSyncProperties.GroupPosition].LastUpdateValue))
            {
                if (lastUpdateByLocalTS > m_propertiesSyncInfo[SceneObjectPartSyncProperties.GroupPosition].LastUpdateTimeStamp)
                {
                    //Update cached value with SOP.GroupPosition
                    m_propertiesSyncInfo[SceneObjectPartSyncProperties.GroupPosition].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.GroupPosition);

                    //Also may need to cached PhysActor.Position
                    if (part.PhysActor != null)
                    {
                        if (!part.PhysActor.Position.Equals(m_propertiesSyncInfo[SceneObjectPartSyncProperties.Position].LastUpdateValue))
                        {
                            m_propertiesSyncInfo[SceneObjectPartSyncProperties.Position].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.PhysActor.Position);
                        }
            
                    }
                    return true;
                }
                else if (lastUpdateByLocalTS < m_propertiesSyncInfo[SceneObjectPartSyncProperties.GroupPosition].LastUpdateTimeStamp)
                {
                    //overwrite SOP's data, set function of GroupPosition updates PhysActor.Position as well
                    part.GroupPosition = (Vector3)m_propertiesSyncInfo[SceneObjectPartSyncProperties.GroupPosition].LastUpdateValue;

                    //PhysActor.Position is just updated by setting GroupPosition 
                    //above, so need to update the cached value of Position here.
                    if (part.PhysActor != null)
                    {
                        if (!part.PhysActor.Position.Equals(m_propertiesSyncInfo[SceneObjectPartSyncProperties.Position].LastUpdateValue))
                        {
                            //Set the timestamp and syncID to be the same with GroupPosition
                            long lastUpdateTimestamp = m_propertiesSyncInfo[SceneObjectPartSyncProperties.GroupPosition].LastUpdateTimeStamp;
                            string lastUpdateSyncID = m_propertiesSyncInfo[SceneObjectPartSyncProperties.GroupPosition].LastUpdateSyncID;
                            m_propertiesSyncInfo[SceneObjectPartSyncProperties.Position].UpdateSyncInfoByLocal(lastUpdateTimestamp,
                                lastUpdateSyncID, (Object)part.PhysActor.Position);
                        }
                    }
                }
            }
            return false;
        }

        private bool CompareAndUpdateSOPPosition(SceneObjectPart part, long lastUpdateByLocalTS, string syncID)
        {
            if (part.PhysActor == null)
                return false;

            if (!part.PhysActor.Position.Equals(m_propertiesSyncInfo[SceneObjectPartSyncProperties.Position].LastUpdateValue))
            {
                if (lastUpdateByLocalTS > m_propertiesSyncInfo[SceneObjectPartSyncProperties.Position].LastUpdateTimeStamp)
                {
                    //Update SOP.PhysActor.Position 
                    m_propertiesSyncInfo[SceneObjectPartSyncProperties.Position].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.PhysActor.Position);

                    //Also may need to update SOP.GroupPosition (especially for root parts)
                    if (!part.GroupPosition.Equals(m_propertiesSyncInfo[SceneObjectPartSyncProperties.GroupPosition].LastUpdateValue))
                    {
                        //Update SOP.GroupPosition
                        m_propertiesSyncInfo[SceneObjectPartSyncProperties.GroupPosition].UpdateSyncInfoByLocal(lastUpdateByLocalTS, syncID, (Object)part.GroupPosition);
                    }
                    return true;
                }
                else if (lastUpdateByLocalTS < m_propertiesSyncInfo[SceneObjectPartSyncProperties.Position].LastUpdateTimeStamp)
                {
                    //overwrite PhysActor's data with the cached value
                    part.PhysActor.Position = (Vector3)m_propertiesSyncInfo[SceneObjectPartSyncProperties.Position].LastUpdateValue;

                    //GroupPosition may change due to PhysActor.Position changes,
                    //especially for root parts. Sync the value of GroupPosition.
                    if (!part.GroupPosition.Equals(m_propertiesSyncInfo[SceneObjectPartSyncProperties.GroupPosition].LastUpdateValue))
                    {
                        //Need to reset SOP.GroupPosition to the cached value here
                        //Set the timestamp and syncID to be the same with Position
                        long lastUpdateTimestamp = m_propertiesSyncInfo[SceneObjectPartSyncProperties.Position].LastUpdateTimeStamp;
                        string lastUpdateSyncID = m_propertiesSyncInfo[SceneObjectPartSyncProperties.Position].LastUpdateSyncID;
                        m_propertiesSyncInfo[SceneObjectPartSyncProperties.GroupPosition].UpdateSyncInfoByLocal(lastUpdateTimestamp,
                            lastUpdateSyncID, (Object)part.GroupPosition);
                    }
                }
            }
            return false;
        }
    }

    public class PrimSyncInfoManager
    {
        //private ILog DebugLog;
        public static ILog DebugLog;

        /// <summary>
        /// Lock for write-accessing m_primsInSync. We assume accesses to m_primsInSync
        /// are in the "many reads, a few writes" pattern. Writers needs to lock on it.
        /// Readers who interate through m_primsInSync need to copy a reference to 
        /// m_primsInSync and operate on the reference, but no need to lock. (Readers who
        /// just grabs reference to one item in m_primsInSync for further operation
        /// might not even need to copy a reference to m_primsInSync initially???)
        /// </summary>
        private Object m_primsInSyncLock = new Object();
        private Dictionary<UUID, PrimSyncInfo> m_primsInSync;
        private RegionSyncModule m_regionSyncModule;
        
        
        /// <summary>
        /// The max time for a SOP's SyncInfo to sit in record 
        /// w/o being updated either locally or bySync.
        /// </summary>
        private long m_ageOutThreshold; 

        public Dictionary<UUID, PrimSyncInfo> PrimsInSync
        {
            get { return m_primsInSync; }
        }

        public PrimSyncInfoManager(RegionSyncModule syncModule, long ageOutTh)
        {
            m_primsInSync = new Dictionary<UUID, PrimSyncInfo>();
            m_regionSyncModule = syncModule;
            m_ageOutThreshold = ageOutTh;
        }

        public bool IsPrimSyncInfoExist(UUID primUUID)
        {
            return m_primsInSync.ContainsKey(primUUID);
        }

        public Scene GetLocalScene()
        {
            return m_regionSyncModule.LocalScene;
        }

        /// <summary>
        /// For each property in updatedProperties, (1) if the value in SOP's
        /// data is different than that in PrimSyncInfo, and what's in PrimSyncInfo
        /// has an older timestamp, then update that property's value and syncInfo
        /// in PrimSyncInfo; (2) otherwise, skip the property and do nothing.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="updatedProperties"></param>
        /// <returns>The list properties among updatedProperties whose value
        /// have been copied over to PrimSyncInfo.</returns>
        public HashSet<SceneObjectPartSyncProperties> UpdatePrimSyncInfoByLocal(SceneObjectPart part, List<SceneObjectPartSyncProperties> updatedProperties)
        {
            long currentTime = DateTime.Now.Ticks;
            if (m_primsInSync.ContainsKey(part.UUID))
            {
                PrimSyncInfo primSyncInfo = m_primsInSync[part.UUID];
                return primSyncInfo.UpdatePropertiesByLocal(part, updatedProperties, currentTime, m_regionSyncModule.SyncID);
            }
            else
            {
                //The SOP's SyncInfo is no longer in my record, may due to 
                //TimeOut or something. Add it back. Assume the properties
                //were "AgeOut" seconds old.
                PrimSyncInfo primSyncInfo = new PrimSyncInfo(part, currentTime, m_regionSyncModule.SyncID, this);
                InsertPrimSyncInfo(part.UUID, primSyncInfo);

                return new HashSet<SceneObjectPartSyncProperties>(updatedProperties);
            }
        }

        public List<SceneObjectPartSyncProperties> UpdatePrimSyncInfoBySync(SceneObjectPart part, HashSet<PropertySyncInfo> propertiesSyncInfo)
        {
            if (m_primsInSync.ContainsKey(part.UUID))
            {
                PrimSyncInfo primSyncInfo = m_primsInSync[part.UUID];
                return primSyncInfo.UpdatePropertiesBySync(part, propertiesSyncInfo);
            }
            else
            {
                //This should not happen, as we should only receive UpdatedPrimProperties after receiving a NewObject message
                DebugLog.WarnFormat("PrimSyncInfoManager.UpdatePrimSyncInfoBySync: SOP {0},{1} not in local record of PrimSyncInfo.", part.Name, part.UUID);
                return new List<SceneObjectPartSyncProperties>();
            }
        }

        public List<SceneObjectPartSyncProperties> UpdatePrimSyncInfoBySync(SceneObjectPart part, PrimSyncInfo updatedPrimSyncInfo)
        {
            if (m_primsInSync.ContainsKey(part.UUID))
            {
                PrimSyncInfo primSyncInfo = m_primsInSync[part.UUID];
                return primSyncInfo.UpdatePropertiesBySync(part, updatedPrimSyncInfo);
            }
            else
            {
                //This should not happen, as we should only receive UpdatedPrimProperties after receiving a NewObject message
                DebugLog.WarnFormat("PrimSyncInfoManager.UpdatePrimSyncInfoBySync: SOP {0},{1} not in local record of PrimSyncInfo.", part.Name, part.UUID);
                return new List<SceneObjectPartSyncProperties>();
            }
        }

        public OSDMap EncodePrimProperties(SceneObjectPart sop, HashSet<SceneObjectPartSyncProperties> updatedProperties)
        {
            OSDMap data = new OSDMap();
            UUID primUUID = sop.UUID;
            if (!m_primsInSync.ContainsKey(primUUID))
            {
                DebugLog.WarnFormat("EncodePrimProperties: {0} not in RegionSyncModule's PrimSyncInfo list yet", primUUID);
                return data;
            }

            data["primUUID"] = OSDMap.FromUUID(primUUID);

            //If SceneObjectPartProperties.FullUpdate is in updatedProperties,
            //convert it to the full list of all properties
            HashSet<SceneObjectPartSyncProperties> propertiesToEncoded = updatedProperties;
            if (updatedProperties.Contains(SceneObjectPartSyncProperties.FullUpdate))
            {
                if (sop.PhysActor != null)
                    propertiesToEncoded = SceneObjectPart.GetAllPrimProperties();
                else
                    propertiesToEncoded = SceneObjectPart.GetAllPrimNonPhysActorProperties();
            }

            OSDMap propertyData = m_primsInSync[primUUID].EncodePropertiesSyncInfo(propertiesToEncoded);
            data["propertyData"] = propertyData;

            return data;
        }

        public HashSet<PropertySyncInfo> DecodePrimProperties(OSDMap primPropertiesData)
        {
            HashSet<PropertySyncInfo> propertiesSyncInfo = new HashSet<PropertySyncInfo>();
            if (!primPropertiesData.ContainsKey("propertyData"))
            {
                DebugLog.WarnFormat("DecodePrimProperties: propertyData is missing");
                return propertiesSyncInfo;
            }
            
            //if(!m_primsInSync.ContainsKey(primUUID)){
            //    DebugLog.WarnFormat("prim {0} not in PrimSyncInfoManager's record");
            //    return;
            //}
            propertiesSyncInfo = PrimSyncInfo.DecodeProperiesSyncInfo((OSDMap)primPropertiesData["propertyData"]);

            return propertiesSyncInfo;
        }

        public PrimSyncInfo DecodeFullSetPrimProperties(OSDMap primData)
        {
            if (!primData.ContainsKey("primUUID") || !primData.ContainsKey("propertyData"))
            {
                DebugLog.WarnFormat("DecodeFullSetPrimProperties: either primUUID or propertyData is missing");
                return null;
            }
            UUID primUUID = primData["primUUID"];
            OSDMap propertyData = (OSDMap)primData["propertyData"];

            PrimSyncInfo primSynInfo = new PrimSyncInfo(primUUID, propertyData, this);

            return primSynInfo;
        }

        /// <summary>
        /// Insert a new PrimSyncInfo into local record. Implemented for access 
        /// pattern of many readers/a few writers. 
        /// </summary>
        /// <param name="primUUID"></param>
        /// <param name="primSyncInfo"></param>
        /// <returns>True if primSyncInfo is inserted; false if it is already in
        /// but updated.</returns>
        public bool InsertPrimSyncInfo(UUID primUUID, PrimSyncInfo primSyncInfo)
        {
            lock (m_primsInSyncLock)
            {
                if (m_primsInSync.ContainsKey(primUUID))
                {
                    m_primsInSync[primUUID] = primSyncInfo;
                    return false;
                }

                //copy the items from the old list and insert the new record
                Dictionary<UUID, PrimSyncInfo> newPrimsInSync = new Dictionary<UUID, PrimSyncInfo>(m_primsInSync);
                newPrimsInSync.Add(primUUID, primSyncInfo);

                //replace the old list
                m_primsInSync = newPrimsInSync; 
            }
            return true;
        }

        /// <summary>
        /// For a new SOP, create a PrimSyncInfo for it. Assume the timestamp for
        /// each property is at least T ticks old, T=m_ageOutThreshold. 
        /// </summary>
        /// <param name="part"></param>
        /// <param name="syncInfoInitTime"></param>
        /// <param name="syncID"></param>
        /// <returns></returns>
        public bool InsertPrimSyncInfo(SceneObjectPart part, long syncInfoInitTime, string syncID)
        {
            long lastUpdateTimeStamp = syncInfoInitTime - m_ageOutThreshold;

            PrimSyncInfo primSyncInfo = new PrimSyncInfo(part, lastUpdateTimeStamp, syncID, this);
            lock (m_primsInSyncLock)
            {
                if (m_primsInSync.ContainsKey(part.UUID))
                {
                    m_primsInSync[part.UUID] = primSyncInfo;
                    return false;
                }
                Dictionary<UUID, PrimSyncInfo> newPrimsInSync = new Dictionary<UUID, PrimSyncInfo>(m_primsInSync);
                newPrimsInSync.Add(part.UUID, primSyncInfo);

                m_primsInSync = newPrimsInSync;
            }
            return true;
        }
        
        public void InsertMultiPrimSyncInfo(Dictionary<UUID, PrimSyncInfo> multiPrimsSyncInfo)
        {
            lock (m_primsInSyncLock)
            {
                //copy the old list, update the copied list
                Dictionary<UUID, PrimSyncInfo> newPrimsInSync = new Dictionary<UUID, PrimSyncInfo>(m_primsInSync);
                foreach (KeyValuePair<UUID, PrimSyncInfo> valPair in multiPrimsSyncInfo)
                {
                    UUID primUUID = valPair.Key;
                    PrimSyncInfo primSyncInfo = valPair.Value;
                    if (newPrimsInSync.ContainsKey(primUUID))
                    {
                        newPrimsInSync[primUUID] = primSyncInfo;
                        continue;
                    }
                    newPrimsInSync.Add(primUUID, primSyncInfo);
                }

                //replace the old list
                m_primsInSync = newPrimsInSync;
            }
        }

        public bool RemovePrimSyncInfo(SceneObjectPart part)
        {
            if (!m_primsInSync.ContainsKey(part.UUID))
            {
                return false;
            }
            lock (m_primsInSyncLock)
            {
                Dictionary<UUID, PrimSyncInfo> newPrimsInSync = new Dictionary<UUID, PrimSyncInfo>(m_primsInSync);
                newPrimsInSync.Remove(part.UUID);

                m_primsInSync = newPrimsInSync;
            }
            return true;
        }

        public PrimSyncInfo GetPrimSyncInfo(UUID primUUID)
        {
            if (m_primsInSync.ContainsKey(primUUID))
            {
                return m_primsInSync[primUUID];
            }
            else
                return null;
        }

        public bool SetSOPPhyscActorProperties(SceneObjectPart part)
        {
            if(m_primsInSync.ContainsKey(part.UUID)){
                m_primsInSync[part.UUID].SetSOPPhyscActorProperties(part);
                return true;
            }
            return false;
        }
    }
 
}

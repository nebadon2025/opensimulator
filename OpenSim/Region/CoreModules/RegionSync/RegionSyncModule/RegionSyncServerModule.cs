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
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Region.CoreModules.Framework.InterfaceCommander;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using log4net;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    public class RegionSyncServerModule : IRegionModule, IRegionSyncServerModule, ICommandableModule
    {
        private static int DefaultPort = 13000;

        #region IRegionModule Members
        public void Initialise(Scene scene, IConfigSource config)
        {
            m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            // If no syncConfig, do not start up server mode
            IConfig syncConfig = config.Configs["RegionSyncModule"];
            if (syncConfig == null)
            {
                scene.RegionSyncEnabled = false;
                m_active = false;
                m_log.Warn("[REGION SYNC SERVER MODULE] No RegionSyncModule config section found. Shutting down.");
                return;
            }

            // If syncConfig does not indicate "enabled", do not start up server mode
            bool enabled = syncConfig.GetBoolean("Enabled", true);
            if(!enabled)
            {
                scene.RegionSyncEnabled = false;
                m_active = false;
                m_log.Warn("[REGION SYNC SERVER MODULE] RegionSyncModule is not enabled. Shutting down.");
                return;
            }

            // If syncConfig does not indicate "server", do not start up server mode
            string mode = syncConfig.GetString("Mode", "server").ToLower();
            if(mode != "server")
            {
                scene.RegionSyncEnabled = false;
                m_active = false;
                m_log.WarnFormat("[REGION SYNC SERVER MODULE] RegionSyncModule is in {0} mode. Shutting down.", mode);
                return;
            }

            // Enable region sync in server mode on the scene and module
            scene.RegionSyncEnabled = true;
            scene.RegionSyncMode = mode;
            m_active = true;

            // Init the sync statistics log file
            string syncstats = "syncstats" + "_" + scene.RegionInfo.RegionName + ".txt";
            m_statsWriter = File.AppendText(syncstats);

            //Get sync server info for Client Manager actors 
            string serverAddr = scene.RegionInfo.RegionName + "_ServerIPAddress";
            m_serveraddr = syncConfig.GetString(serverAddr, "127.0.0.1");
            string serverPort = scene.RegionInfo.RegionName + "_ServerPort";
            m_serverport = syncConfig.GetInt(serverPort, DefaultPort);
            // Client manager load balancing
            m_maxClientsPerManager = syncConfig.GetInt("MaxClientsPerManager", 100);
            DefaultPort++;

            //Get sync server info for Script Engine actors 
            string seServerAddr = scene.RegionInfo.RegionName + "_SceneToSESyncServerIP";
            m_seSyncServeraddr = syncConfig.GetString(seServerAddr, "127.0.0.1");
            string seServerPort = scene.RegionInfo.RegionName + "_SceneToSESyncServerPort";
            m_seSyncServerport = syncConfig.GetInt(seServerPort, DefaultPort);
            DefaultPort++;

            //Get quark information
            QuarkInfo.SizeX = syncConfig.GetInt("QuarkSizeX", (int)Constants.RegionSize);
            QuarkInfo.SizeY = syncConfig.GetInt("QuarkSizeY", (int)Constants.RegionSize);

            m_scene = scene;
            m_scene.RegisterModuleInterface<IRegionSyncServerModule>(this);

            // Setup the command line interface
            m_scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
            InstallInterfaces();

            //m_log.Warn("[REGION SYNC SERVER MODULE] Initialised");
        }

        public void PostInitialise()
        {
            if (!m_active)
                return;

            //m_scene.EventManager.OnObjectBeingRemovedFromScene += new EventManager.ObjectBeingRemovedFromScene(EventManager_OnObjectBeingRemovedFromScene);

            //m_scene.EventManager.OnAvatarEnteringNewParcel += new EventManager.AvatarEnteringNewParcel(EventManager_OnAvatarEnteringNewParcel);
            //m_scene.EventManager.OnClientMovement += new EventManager.ClientMovement(EventManager_OnClientMovement);
            //m_scene.EventManager.OnLandObjectAdded += new EventManager.LandObjectAdded(EventManager_OnLandObjectAdded);
            //m_scene.EventManager.OnLandObjectRemoved += new EventManager.LandObjectRemoved(EventManager_OnLandObjectRemoved);
            m_scene.EventManager.OnNewClient += new EventManager.OnNewClientDelegate(EventManager_OnNewClient);
            //m_scene.EventManager.OnNewPresence += new EventManager.OnNewPresenceDelegate(EventManager_OnNewPresence);
            m_scene.EventManager.OnRemovePresence += new EventManager.OnRemovePresenceDelegate(EventManager_OnRemovePresence);
            m_scene.SceneGraph.OnObjectCreate += new ObjectCreateDelegate(SceneGraph_OnObjectCreate);
            m_scene.SceneGraph.OnObjectDuplicate += new ObjectDuplicateDelegate(SceneGraph_OnObjectDuplicate);
            //m_scene.SceneGraph.OnObjectRemove += new ObjectDeleteDelegate(SceneGraph_OnObjectRemove);
            //m_scene.StatsReporter.OnSendStatsResult += new SimStatsReporter.SendStatResult(StatsReporter_OnSendStatsResult);
            m_scene.EventManager.OnOarFileLoaded += new EventManager.OarFileLoaded(EventManager_OnOarFileLoaded);

            m_log.Warn("[REGION SYNC SERVER MODULE] Starting RegionSyncServer");
            // Start the server and listen for RegionSyncClients
            m_server = new RegionSyncServer(m_scene, m_serveraddr, m_serverport, m_maxClientsPerManager);
            m_server.Start();
            m_statsTimer.Elapsed += new System.Timers.ElapsedEventHandler(StatsTimerElapsed);
            m_statsTimer.Start();

            m_log.Warn("[REGION SYNC SERVER MODULE] Starting SceneToScriptEngineSyncServer");
            //Start the sync server for script engines
            m_sceneToSESyncServer = new SceneToScriptEngineSyncServer(m_scene, m_seSyncServeraddr, m_seSyncServerport);
            m_sceneToSESyncServer.Start();
            //m_log.Warn("[REGION SYNC SERVER MODULE] Post-Initialised");
        }

        private void StatsTimerElapsed(object source, System.Timers.ElapsedEventArgs e)
        {
            if (Synced)
                m_server.ReportStats(m_statsWriter);
        }

        void IRegionModule.Close()
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


        //KittyL added
        //Later, should make quarkIDs the argument to the function call
        //public void SendResetScene()
        //{
        //    m_server.Broadcast(new RegionSyncMessage(RegionSyncMessage.MsgType.ResetScene, "reset"));
        //}

        #endregion

        #region ICommandableModule Members
        private readonly Commander m_commander = new Commander("sync");
        public ICommander CommandInterface
        {
            get { return m_commander; }
        }
        #endregion

        #region IRegionSyncServerModule members
        // Lock is used to synchronize access to the update status and both update queues
        private object m_updateLock = new object();
        private int m_sendingUpdates;
        private Dictionary<UUID, SceneObjectGroup> m_primUpdates = new Dictionary<UUID, SceneObjectGroup>();
        private Dictionary<UUID, ScenePresence> m_presenceUpdates = new Dictionary<UUID, ScenePresence>();

        private System.Timers.Timer m_statsTimer = new System.Timers.Timer(1000);
        //private TextWriter m_statsWriter = File.AppendText("syncstats.txt");
        private TextWriter m_statsWriter;

        public void QueuePartForUpdate(SceneObjectPart part)
        {
            if (!Active || !Synced)
                return;
            lock (m_updateLock)
            {
                m_primUpdates[part.ParentGroup.UUID] = part.ParentGroup;
            }
            //m_log.WarnFormat("[REGION SYNC SERVER MODULE] QueuePartForUpdate: {0}", part.UUID.ToString());
        }

        public void QueuePresenceForTerseUpdate(ScenePresence presence)
        {
            if (!Active || !Synced)
                return;
            lock (m_updateLock)
            {
                m_presenceUpdates[presence.UUID] = presence;
            }
            //m_log.WarnFormat("[REGION SYNC SERVER MODULE] QueuePresenceForUpdate: {0}", presence.UUID.ToString());
        }

        public void SendUpdates()
        {
            if (!Active || !Synced)
                return;

            // Existing value of 1 indicates that updates are currently being sent so skip updates this pass
            if (Interlocked.Exchange(ref m_sendingUpdates, 1) == 1)
            {
                m_log.WarnFormat("[REGION SYNC SERVER MODULE] SendUpdates(): An update thread is already running.");
                return;
            }

            List<SceneObjectGroup> primUpdates;
            List<ScenePresence> presenceUpdates;

            lock (m_updateLock)
            {
                primUpdates = new List<SceneObjectGroup>(m_primUpdates.Values);
                presenceUpdates = new List<ScenePresence>(m_presenceUpdates.Values);
                m_primUpdates.Clear();
                m_presenceUpdates.Clear();
            }

            // This could be another thread for sending outgoing messages or just have the Queue functions
            // create and queue the messages directly into the outgoing server thread.
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                // Sending the message when it's first queued would yield lower latency but much higher load on the simulator
                // as parts may be updated many many times very quickly. Need to implement a higher resolution send in heartbeat
                foreach (SceneObjectGroup sog in primUpdates)
                {
                    if (!sog.IsDeleted)
                    {
                        /*
                        string sogxml = SceneObjectSerializer.ToXml2Format(sog);

                        m_log.Debug("[REGION SYNC SERVER MODULE]: to update object " + sog.UUID + ", localID: "+sog.LocalId
                            + ", with color " + sog.RootPart.Shape.Textures.DefaultTexture.RGBA.A 
                            + "," + sog.RootPart.Shape.Textures.DefaultTexture.RGBA.B + "," + sog.RootPart.Shape.Textures.DefaultTexture.RGBA.G 
                            + "," + sog.RootPart.Shape.Textures.DefaultTexture.RGBA.R);

                        m_server.Broadcast(new RegionSyncMessage(RegionSyncMessage.MsgType.UpdatedObject, sogxml));
                         * */
                        //KittyL: modified to broadcast to different types of actors
                        m_server.BroadcastToCM(RegionSyncMessage.MsgType.UpdatedObject, sog);
                        m_sceneToSESyncServer.SendToSE(RegionSyncMessage.MsgType.UpdatedObject, sog);
                    }
                }
                foreach (ScenePresence presence in presenceUpdates)
                {
                    try
                    {
                        if (!presence.IsDeleted)
                        {
                            OSDMap data = new OSDMap(7);
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
                            RegionSyncMessage rsm = new RegionSyncMessage(RegionSyncMessage.MsgType.UpdatedAvatar, OSDParser.SerializeJsonString(data));
                            m_server.EnqueuePresenceUpdate(presence.UUID, rsm.ToBytes());

                        }
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[REGION SYNC SERVER MODULE] Caught exception sending presence updates for {0}: {1}", presence.Name, e.Message);
                    }
                }
                // Indicate that the current batch of updates has been completed
                Interlocked.Exchange(ref m_sendingUpdates, 0);
            });
        }

        private Dictionary<UUID, System.Threading.Timer> m_appearanceTimers = new Dictionary<UUID, Timer>();

        public void SendAppearance(UUID agentID, byte[] vp, Primitive.TextureEntry te)
        {
            ScenePresence sp;
            if (!m_scene.TryGetScenePresence(agentID, out sp))
            {
                m_log.WarnFormat("[REGION SYNC SERVER MODULE] <{0}> {1} SendAppearance could not locate presence!", "           ", agentID);
                return;
            }
            //m_log.WarnFormat("[REGION SYNC SERVER MODULE] <{0}> {1} ScenePresence called SendAppearance ({2})", sp.Name, agentID, te == null ? "  " : "te");
            if(te == null)
                return;
            int delay = 1000;
            //m_log.WarnFormat("[REGION SYNC SERVER MODULE] <{0}> {1} Waiting {2}ms before sending appearance to all client managers", sp.Name, agentID, delay);
            OSDMap data = new OSDMap(3);
            data["id"] = OSDUUID.FromUUID(agentID);
            data["vp"] = new OSDBinary(vp);
            data["te"] = te.GetOSD();
            Timer appearanceSetter = new Timer(delegate(object obj)
                            {
                                //m_log.WarnFormat("[REGION SYNC SERVER MODULE]  <{0}> {1} Broadcasting appearance to all client managers", sp.Name, agentID);
                                m_server.Broadcast(new RegionSyncMessage(RegionSyncMessage.MsgType.AvatarAppearance, OSDParser.SerializeJsonString(data)));
                                lock (m_appearanceTimers)
                                    m_appearanceTimers.Remove(agentID);
                            }, null, delay, Timeout.Infinite);
            // Just keeps a reference to this timer
            lock (m_appearanceTimers)
                m_appearanceTimers[agentID] = appearanceSetter;
        }

        public void DeleteObject(ulong regionHandle, uint localID, SceneObjectPart part)
        {
            if (!Active || !Synced)
                return;

            //First, tell client managers to remove the SceneObjectPart 
            OSDMap data = new OSDMap(2);
            data["regionHandle"] = OSD.FromULong(regionHandle);
            data["localID"] = OSD.FromUInteger(localID);
            RegionSyncMessage rsm = new RegionSyncMessage(RegionSyncMessage.MsgType.RemovedObject, OSDParser.SerializeJsonString(data));
            //m_server.BroadcastToCM(rsm);
            m_server.Broadcast(rsm);

            //KittyL: Second, tell script engine to remove the object, identified by UUID
            //UUID objID = m_scene.GetSceneObjectPart(localID).ParentGroup.UUID;
            //SceneObjectPart part = m_scene.GetSceneObjectPart(localID);
            if (part != null)
            {
                data = new OSDMap(1);
                
                data["UUID"] = OSD.FromUUID(part.UUID);
                rsm = new RegionSyncMessage(RegionSyncMessage.MsgType.RemovedObject, OSDParser.SerializeJsonString(data));
                
                //when an object is deleted, this function (DeleteObject) could be triggered more than once. So we check 
                //if the object part is already removed is the scene (part==null)
                m_log.Debug("Inform script engine about the deleted object");
                m_sceneToSESyncServer.SendToSE(rsm, part.ParentGroup);
            }
            
        }

        public bool Active
        {
            get { return m_active; }
        }

        // Check if the sync server module is connected to any clients (KittyL: edited for testing if connected to any actors)
        public bool Synced
        {
            get
            {
                if (m_server == null || !m_server.Synced)
                //if((m_server == null || !m_server.Synced) && (m_sceneToSESyncServer==null || !m_sceneToSESyncServer.Synced))
                    return false;
                return true;
            }
        }

        public void SendLoadWorldMap(ITerrainChannel heightMap)
        {
            RegionSyncMessage msg = new RegionSyncMessage(RegionSyncMessage.MsgType.Terrain, m_scene.Heightmap.SaveToXmlString());
            m_server.Broadcast(msg);
            //KittyL: added for SE
            m_sceneToSESyncServer.SendToAllConnectedSE(msg);
        }

        #region cruft
#if false
        
        public void QueuePartForUpdate(SceneObjectPart part)
        { 
            
            m_server.Broadcast(string.Format("QueuePartForUpdate - Name:{0}, LocalID:{1}, UUID:{2}", part.Name, part.LocalId.ToString(), part.UUID.ToString()));
            m_log.Warn(string.Format("QueuePartForUpdate - Name:{0}, LocalID:{1}, UUID:{2}", part.Name, part.LocalId.ToString(), part.UUID.ToString()));
            //m_log.Warn(System.Environment.StackTrace);
            
        }
        
        public void SendPartFullUpdate(SceneObjectPart part)
        { 
            /*
            m_server.Broadcast(string.Format("SendPartFullUpdate - Name:{0}, LocalID:{1}, UUID:{2}", part.Name, part.LocalId.ToString(), part.UUID.ToString()));
            m_log.Warn(string.Format("SendPartFullUpdate - Name:{0}, LocalID:{1}, UUID:{2}", part.Name, part.LocalId.ToString(), part.UUID.ToString())); 
             * */
        }
        public void SendPartTerseUpdate(SceneObjectPart part)
        {
            /*
            m_server.Broadcast(string.Format("SendPartTerseUpdate - Name:{0}, LocalID:{1}, UUID:{2}", part.Name, part.LocalId.ToString(), part.UUID.ToString()));
            m_log.Warn(string.Format("SendPartTerseUpdate - Name:{0}, LocalID:{1}, UUID:{2}", part.Name, part.LocalId.ToString(), part.UUID.ToString())); 
             * */
        } 
        public void SendShutdownConnectionNotice(Scene scene)
        { 
            /*
            m_server.Broadcast("SendShutdownConnectionNotice");
            m_log.Warn("SendShutdownConnectionNotice"); 
             * */
        }
        public void SendKillObject(ulong regionHandle, uint localID)
        { 
            /*
            m_server.Broadcast(string.Format("SendKillObject - regionHandle:{0}, localID:{1}", regionHandle.ToString(), localID.ToString()));
            m_log.Warn(string.Format("SendKillObject - regionHandle:{0}, localID:{1}", regionHandle.ToString(), localID.ToString()));
            m_log.Warn(System.Environment.StackTrace);
             * */
        }
#endif
        #endregion
        #endregion

        #region RegionSyncServerModule members
        private bool m_active = true;
        private string m_serveraddr;
        private int m_serverport;
        private int m_maxClientsPerManager;
        private Scene m_scene;
        //private IClientAPI m_clientAggregator;
        private ILog m_log;
        //private int m_moveCounter = 0;
        private RegionSyncServer m_server = null;

        //Sync-server for script engines
        private string m_seSyncServeraddr;
        private int m_seSyncServerport;
        private SceneToScriptEngineSyncServer m_sceneToSESyncServer = null;
        
        //quark related information
        //private int QuarkInfo.SizeX;
        //private int QuarkInfo.SizeY;

        #endregion

        #region Event Handlers
        private void SceneGraph_OnObjectCreate(EntityBase entity)
        {
            if (!Synced)
                return;

//            m_log.Debug("[RegionSyncServerModule]: SceneGraph_OnObjectCreate() called");

            if (entity is SceneObjectGroup)
            {
                /*
                string sogxml = SceneObjectSerializer.ToXml2Format((SceneObjectGroup)entity);

                SceneObjectGroup sog = (SceneObjectGroup)entity;
                m_log.Debug("SOG " + sog.UUID); 

                RegionSyncMessage rsm = new RegionSyncMessage(RegionSyncMessage.MsgType.NewObject, sogxml);
                //KittyL: edited to support both Client Manager and Script Engine actors
                //m_server.Broadcast(rsm);
                m_server.BroadcastToCM(rsm);
                 * */
                SceneObjectGroup sog = (SceneObjectGroup)entity;
                m_server.BroadcastToCM(RegionSyncMessage.MsgType.NewObject, sog);

                m_sceneToSESyncServer.SendToSE(RegionSyncMessage.MsgType.NewObject, sog);

            }
            else
            {
                m_log.Warn("SceneGraph_OnObjectCreate called with non-SceneObjectGroup");
            }
        }

        private void SceneGraph_OnObjectDuplicate(EntityBase original, EntityBase copy)
        {
            if (!Synced)
                return;
            if (original is SceneObjectGroup && copy is SceneObjectGroup)
            {
             
                //string sogxml = SceneObjectSerializer.ToXml2Format((SceneObjectGroup)copy);
                //RegionSyncMessage rsm = new RegionSyncMessage(RegionSyncMessage.MsgType.NewObject, sogxml);
                //m_server.Broadcast(rsm);
                SceneObjectGroup sog = (SceneObjectGroup)copy;
                m_server.BroadcastToCM(RegionSyncMessage.MsgType.NewObject, sog);
                m_sceneToSESyncServer.SendToSE(RegionSyncMessage.MsgType.NewObject, sog);
            }
            else
            {
                m_log.Warn("SceneGraph_OnObjectDuplicate called with non-SceneObjectGroup");
            }
        }

        private void SceneGraph_OnObjectRemove(EntityBase entity)
        {
            if (!Synced)
                return;
            if (entity is SceneObjectGroup)
            {
                // No reason to send the entire object, just send the UUID to be deleted
                RegionSyncMessage rsm = new RegionSyncMessage(RegionSyncMessage.MsgType.RemovedObject, entity.UUID.ToString());
                m_server.Broadcast(rsm);

                SceneObjectPart part = m_scene.GetSceneObjectPart(entity.UUID);
                if (part != null)
                {
                    OSDMap data = new OSDMap(1);

                    data["UUID"] = OSD.FromUUID(part.UUID);
                    rsm = new RegionSyncMessage(RegionSyncMessage.MsgType.RemovedObject, OSDParser.SerializeJsonString(data));

                    //when an object is deleted, this function (DeleteObject) could be triggered more than once. So we check 
                    //if the object part is already removed is the scene (part==null)
                    m_log.Debug("Inform script engine about the deleted object");
                    m_sceneToSESyncServer.SendToSE(rsm, part.ParentGroup);
                }
            }
            else
            {
                m_log.Warn("SceneGraph_OnObjectDelete called with non-SceneObjectGroup");
            }
        }

        // A ficticious event
        public void Scene_AddNewPrim(SceneObjectGroup sog)
        {
            if (!Synced)
                return;
        }

        /*
        public void StatsReporter_OnSendStatsResult(SimStats stats)
        {
            //m_log.Warn("SendSimStats"); 
        }

        void EventManager_OnObjectBeingRemovedFromScene(SceneObjectGroup sog)
        {
            string msg = (string.Format("EventManager_OnObjectBeingRemovedFromScene" + System.Environment.NewLine +
            "REMOVE: ownerID {0}, groupID {1}, pos {2}, rot {3}, shape {4}, id {5}, localID {6}", sog.OwnerID.ToString(), sog.GroupID.ToString(), sog.RootPart.GroupPosition.ToString(), sog.Rotation.ToString(), sog.RootPart.Shape.ToString(), sog.UUID.ToString(), sog.LocalId.ToString()));
            m_server.Broadcast(msg);
            m_log.Warn("[REGION SYNC SERVER MODULE] " + msg);
            DebugSceneStats();
        }

        void SceneGraph_OnObjectRemove(EntityBase obj)
        {
            SceneObjectGroup sog = (SceneObjectGroup)obj;
            string msg = (string.Format("SceneGraph_OnObjectRemove" + System.Environment.NewLine + 
            "REMOVE: ownerID {0}, groupID {1}, pos {2}, rot {3}, shape {4}, id {5}, localID {6}", sog.OwnerID.ToString(), sog.GroupID.ToString(), sog.RootPart.GroupPosition.ToString(), sog.Rotation.ToString(), sog.RootPart.Shape.ToString(), sog.UUID.ToString(), sog.LocalId.ToString()));
            m_server.Broadcast(msg);
            m_log.Warn("[REGION SYNC SERVER MODULE] " + msg);
            DebugSceneStats();
        }

        void SceneGraph_OnObjectDuplicate(EntityBase original, EntityBase clone)
        {
            SceneObjectGroup sog1 = (SceneObjectGroup)original;
            SceneObjectGroup sog2 = (SceneObjectGroup)clone;
            string msg = (string.Format("SceneGraph_OnObjectDuplicate" +
                System.Environment.NewLine +
                "ORIGINAL: ownerID {0}, groupID {1}, pos {2}, rot {3}, shape {4}, id {5}, localID {6}" + 
                System.Environment.NewLine + 
                "CLONE: ownerID {7}, groupID {8}, pos {9}, rot {10}, shape {11}, id {12}, localID {13}", 
                sog1.OwnerID.ToString(), sog1.GroupID.ToString(), sog1.RootPart.GroupPosition.ToString(), sog1.Rotation.ToString(), sog1.RootPart.Shape.ToString(), sog1.UUID.ToString(), sog1.LocalId.ToString(),
                sog2.OwnerID.ToString(), sog2.GroupID.ToString(), sog2.RootPart.GroupPosition.ToString(), sog2.Rotation.ToString(), sog2.RootPart.Shape.ToString(), sog2.UUID.ToString(), sog2.LocalId.ToString()));
            m_server.Broadcast(msg);
            m_log.Warn("[REGION SYNC SERVER MODULE] " + msg);

            m_log.WarnFormat("[REGION SYNC SERVER MODULE] SceneGraph_OnObjectDuplicate");
            DebugSceneStats();
        }

        void SceneGraph_OnObjectCreate(EntityBase obj)
        {
            SceneObjectGroup sog = (SceneObjectGroup)obj;
            string msg = (string.Format("SceneGraph_OnObjectCreate" + System.Environment.NewLine +
                "CREATE: ownerID {0}, groupID {1}, pos {2}, rot {3}, shape {4}, id {5}, localID {6}", sog.OwnerID.ToString(), sog.GroupID.ToString(), sog.RootPart.GroupPosition.ToString(), sog.Rotation.ToString(), sog.RootPart.Shape.ToString(), sog.UUID.ToString(), sog.LocalId.ToString()));
            m_server.Broadcast(msg);
            m_log.Warn("[REGION SYNC SERVER MODULE] " + msg);
            //DebugSceneStats();
            
        }

        void EventManager_OnLandObjectRemoved(UUID globalID)
        {
            m_log.WarnFormat("[REGION SYNC SERVER MODULE] EventManager_OnLandObjectRemoved");
            DebugSceneStats();
        }

        void EventManager_OnLandObjectAdded(ILandObject newParcel)
        {
            m_log.WarnFormat("[REGION SYNC SERVER MODULE] EventManager_OnLandObjectAdded");
            DebugSceneStats();
        }

        void EventManager_OnClientMovement(ScenePresence client)
        {  
            m_moveCounter++;
            if (m_moveCounter % 100 == 0)
            {
                m_server.Broadcast(msg);
                m_log.Warn("REGION SYNC SERVER MODULE] " + msg);
            }
        }

        void EventManager_OnAvatarEnteringNewParcel(ScenePresence avatar, int localLandID, UUID regionID)
        {
            m_log.WarnFormat("[REGION SYNC SERVER MODULE] (OnAvatarEnteringNewParcel) Avatar \"{0}\" has joined the scene {1} {2} {3} {4}", avatar.Name, avatar.ControllingClient.AgentId.ToString(), avatar.UUID.ToString(), localLandID, regionID.ToString());
            DebugSceneStats();
        }

        */
        private void EventManager_OnNewPresence(ScenePresence presence)
        {
            if (!Synced)
                return;
            m_log.WarnFormat("[REGION SYNC SERVER MODULE] (OneNewPresence) \"{0}\"", presence.Name);
        }

        private void EventManager_OnNewClient(IClientAPI client)
        {
            if (!Synced)
                return;
            m_log.WarnFormat("[REGION SYNC SERVER MODULE] Agent \"{0}\" {1} has joined the scene", client.FirstName + " " + client.LastName, client.AgentId.ToString());
            // Let the client managers know that a new agent has connected
            OSDMap data = new OSDMap(1);
            data["agentID"] = OSD.FromUUID(client.AgentId);
            data["first"] = OSD.FromString(client.FirstName);
            data["last"] = OSD.FromString(client.LastName);
            data["startPos"] = OSD.FromVector3(client.StartPos);
            m_server.Broadcast(new RegionSyncMessage(RegionSyncMessage.MsgType.NewAvatar, OSDParser.SerializeJsonString(data)));
        }

        private void EventManager_OnRemovePresence(UUID agentID)
        {
            if (!Synced)
                return;
            /*
            ScenePresence avatar;
            if (m_scene.TryGetScenePresence(agentID, out avatar))
            {
                m_log.WarnFormat("[REGION SYNC SERVER MODULE] Avatar \"{0}\" {1} {2} has left the scene", avatar.Firstname + " " + avatar.Lastname, agentID.ToString(), avatar.UUID.ToString());
            }
            else
            {
                m_log.WarnFormat("[REGION SYNC SERVER MODULE] Avatar \"unknown\" has left the scene");
            }
             * */
            OSDMap data = new OSDMap();
            data["agentID"] = OSD.FromUUID(agentID);
            m_server.Broadcast(new RegionSyncMessage(RegionSyncMessage.MsgType.RemovedAvatar, OSDParser.SerializeJsonString(data)));
        }

        private void EventManager_OnOarFileLoaded(Guid requestID, string errorMsg)
        {
            //we ignore the requestID and the errorMsg
            SendLoadWorldMap(m_scene.Heightmap);
        }

        #endregion

        #region Console Command Interface
        private void InstallInterfaces()
        {
            Command cmdSyncStatus = new Command("status", CommandIntentions.COMMAND_HAZARDOUS, SyncStatus, "Reports current status of the RegionSyncServer.");
            Command cmdBalanceClients = new Command("balance", CommandIntentions.COMMAND_HAZARDOUS, BalanceClients, "Balance client load across available client managers.");
            m_commander.RegisterCommand("status", cmdSyncStatus);
            m_commander.RegisterCommand("balance", cmdBalanceClients);

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

        private void SyncStatus(Object[] args)
        {
            if (Synced)
                m_server.ReportStatus();
            else
                m_log.Error("No RegionSyncClients connected");
        }

        private void BalanceClients(Object[] args)
        {
            if (Synced)
                m_server.BalanceClients();
            else
                m_log.Error("No RegionSyncClients connected");
        }
        #endregion


        private static readonly Vector3 CenterOfRegion = new Vector3(((int)Constants.RegionSize * 0.5f), ((int)Constants.RegionSize * 0.5f),20);
        // -----------------------------------------------------------------   
        // -----------------------------------------------------------------   
        internal void LocalChat(string msg, int channel)
        {
            OSChatMessage osm = new OSChatMessage();
            osm.From = "RegionSyncServerModule";
            osm.Message = msg;
            osm.Type = ChatTypeEnum.Region;
            osm.Position = CenterOfRegion;
            osm.Sender = null;
            osm.SenderUUID = OpenMetaverse.UUID.Zero; // Hmph! Still?

            osm.Channel = channel;

            m_log.DebugFormat("[REGION SYNC SERVER MODULE]  LocalChat({0},{1})", msg, channel);
            m_scene.EventManager.TriggerOnChatBroadcast(this, osm);
        }

    }

}

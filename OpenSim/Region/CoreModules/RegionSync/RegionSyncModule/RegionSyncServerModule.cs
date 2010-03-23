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
    
using System.Collections.Generic;
using System.Reflection;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using log4net;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace OpenSim.Region.Examples.RegionSyncModule
{
    public class RegionSyncServerModule : IRegionModule, IRegionSyncServerModule
    {
        #region IRegionModule Members
        public void Initialise(Scene scene, IConfigSource config)
        {
            m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            IConfig syncConfig = config.Configs["RegionSyncModule"];
            if (syncConfig == null || syncConfig.GetString("Mode", "server").ToLower() != "server")
            {
                m_active = false;
                m_log.Warn("[REGION SYNC SERVER MODULE] Not in server mode. Shutting down.");
                return;
            }
            m_serveraddr = syncConfig.GetString("ServerIPAddress", "127.0.0.1");
            m_serverport = syncConfig.GetInt("ServerPort", 13000);
            m_scene = scene;
            m_scene.RegisterModuleInterface<IRegionSyncServerModule>(this);
            m_log.Warn("[REGION SYNC SERVER MODULE] Initialised");
        }

        public void PostInitialise()
        {
            if (!m_active)
                return;
            
            //m_scene.EventManager.OnObjectBeingRemovedFromScene += new EventManager.ObjectBeingRemovedFromScene(EventManager_OnObjectBeingRemovedFromScene);
            //m_scene.EventManager.OnNewClient +=new EventManager.OnNewClientDelegate(EventManager_OnNewClient);
            //m_scene.EventManager.OnNewPresence +=new EventManager.OnNewPresenceDelegate(EventManager_OnNewPresence);
            //m_scene.EventManager.OnAvatarEnteringNewParcel += new EventManager.AvatarEnteringNewParcel(EventManager_OnAvatarEnteringNewParcel);
            //m_scene.EventManager.OnClientMovement += new EventManager.ClientMovement(EventManager_OnClientMovement);
            //m_scene.EventManager.OnLandObjectAdded += new EventManager.LandObjectAdded(EventManager_OnLandObjectAdded);
            //m_scene.EventManager.OnLandObjectRemoved += new EventManager.LandObjectRemoved(EventManager_OnLandObjectRemoved);
            m_scene.EventManager.OnRemovePresence += new EventManager.OnRemovePresenceDelegate(EventManager_OnRemovePresence);
            m_scene.SceneGraph.OnObjectCreate += new ObjectCreateDelegate(SceneGraph_OnObjectCreate);
            m_scene.SceneGraph.OnObjectDuplicate += new ObjectDuplicateDelegate(SceneGraph_OnObjectDuplicate);
            //m_scene.SceneGraph.OnObjectRemove += new ObjectDeleteDelegate(SceneGraph_OnObjectRemove);
            //m_scene.StatsReporter.OnSendStatsResult += new SimStatsReporter.SendStatResult(StatsReporter_OnSendStatsResult);
            
            m_log.Warn("[REGION SYNC SERVER MODULE] Starting RegionSyncServer");
            // Start the server and listen for RegionSyncClients
            m_server = new RegionSyncServer(m_scene, m_serveraddr, m_serverport);
            m_server.Start();
            m_log.Warn("[REGION SYNC SERVER MODULE] Post-Initialised");
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
#endregion 

        #region IRegionSyncServerModule members
        // Lock is used to synchronize access to the update status and both update queues
        private object m_updateLock = new object();
        private int m_sendingUpdates;
        private Dictionary<UUID, SceneObjectGroup> m_primUpdates = new Dictionary<UUID, SceneObjectGroup>();
        private Dictionary<UUID, ScenePresence> m_presenceUpdates = new Dictionary<UUID, ScenePresence>();

        public void QueuePartForUpdate(SceneObjectPart part)
        {
            if (!Active || !Synced)
                return;
            lock (m_primUpdates)
            {
                m_primUpdates[part.ParentGroup.UUID] = part.ParentGroup;
            }
            //m_log.WarnFormat("[REGION SYNC SERVER MODULE] QueuePartForUpdate: {0}", part.UUID.ToString());
        }

        public void QueuePresenceForTerseUpdate(ScenePresence presence)
        {
            if (!Active || !Synced)
                return;
            lock (m_presenceUpdates)
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
            primUpdates = new List<SceneObjectGroup>(m_primUpdates.Values);
            presenceUpdates = new List<ScenePresence>(m_presenceUpdates.Values);
            m_primUpdates.Clear();
            m_presenceUpdates.Clear();

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
                        string sogxml = SceneObjectSerializer.ToXml2Format(sog);
                        m_server.Broadcast(new RegionSyncMessage(RegionSyncMessage.MsgType.UpdateObject, sogxml));
                    }
                }
                foreach (ScenePresence presence in presenceUpdates)
                {
                    if (!presence.IsDeleted)
                    {
                        OSDMap data = new OSDMap(4);
                        data["id"] = OSD.FromUUID(presence.UUID);
                        // Do not include offset for appearance height. That will be handled by RegionSyncClient before sending to viewers
                        data["pos"] = OSD.FromVector3(presence.AbsolutePosition); 
                        data["vel"] = OSD.FromVector3(presence.Velocity);
                        data["rot"] = OSD.FromQuaternion(presence.Rotation);
                        RegionSyncMessage rsm = new RegionSyncMessage(RegionSyncMessage.MsgType.UpdateAvatarTerse, OSDParser.SerializeJsonString(data));
                        m_server.Broadcast(rsm);
                    }
                }
                // Indicate that the current batch of updates has been completed
                Interlocked.Exchange(ref m_sendingUpdates, 0);
            });
        }

        public void DeleteObject(ulong regionHandle, uint localID)
        {
            if (!Active || !Synced)
                return;
            OSDMap data = new OSDMap(2);
            data["regionHandle"] = OSD.FromULong(regionHandle);
            data["localID"] = OSD.FromUInteger(localID);
            RegionSyncMessage rsm = new RegionSyncMessage(RegionSyncMessage.MsgType.RemoveObject, OSDParser.SerializeJsonString(data));
            m_server.Broadcast(rsm);
        }

        public bool Active
        {
            get { return m_active; }
        }

        // Check if the sync server module is connected to any clients
        public bool Synced
        {
            get
            {
                if (m_server == null || !m_server.Synced)
                    return false;
                return true;
            }
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
        private Scene m_scene;
        //private IClientAPI m_clientAggregator;
        private ILog m_log;
        //private int m_moveCounter = 0;
        private RegionSyncServer m_server = null;
        #endregion

        #region Event Handlers
        private void SceneGraph_OnObjectCreate(EntityBase entity)
        {
            if (entity is SceneObjectGroup)
            {
                string sogxml = SceneObjectSerializer.ToXml2Format((SceneObjectGroup)entity);
                RegionSyncMessage rsm = new RegionSyncMessage(RegionSyncMessage.MsgType.AddObject, sogxml);
                m_server.Broadcast(rsm);
            }
            else
            {
                m_log.Warn("SceneGraph_OnObjectCreate called with non-SceneObjectGroup");
            }
        }

        private void SceneGraph_OnObjectDuplicate(EntityBase original, EntityBase copy)
        {
            if (original is SceneObjectGroup && copy is SceneObjectGroup)
            {
                string sogxml = SceneObjectSerializer.ToXml2Format((SceneObjectGroup)copy);
                RegionSyncMessage rsm = new RegionSyncMessage(RegionSyncMessage.MsgType.AddObject, sogxml);
                m_server.Broadcast(rsm);
            }
            else
            {
                m_log.Warn("SceneGraph_OnObjectDuplicate called with non-SceneObjectGroup");
            }
        }

        private void SceneGraph_OnObjectRemove(EntityBase entity)
        {
            if (entity is SceneObjectGroup)
            {
                // No reason to send the entire object, just send the UUID to be deleted
                RegionSyncMessage rsm = new RegionSyncMessage(RegionSyncMessage.MsgType.RemoveObject, entity.UUID.ToString());
                m_server.Broadcast(rsm);
            }
            else
            {
                m_log.Warn("SceneGraph_OnObjectDelete called with non-SceneObjectGroup");
            }
        }

        // A ficticious event
        public void Scene_AddNewPrim(SceneObjectGroup sog)
        {
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
                string msg = (string.Format("EventManager_OnClientMovement - Event has been triggered 100 times"));
                m_server.Broadcast(msg);
                m_log.Warn("REGION SYNC SERVER MODULE] " + msg);
            }
        }


        void EventManager_OnAvatarEnteringNewParcel(ScenePresence avatar, int localLandID, UUID regionID)
        {
            m_log.WarnFormat("[REGION SYNC SERVER MODULE] (OnAvatarEnteringNewParcel) Avatar \"{0}\" has joined the scene (1) {2} {3} {4}", avatar.Name, avatar.ControllingClient.AgentId.ToString(), avatar.UUID.ToString(), localLandID, regionID.ToString());
            DebugSceneStats();
        }


        private void EventManager_OnNewClient(IClientAPI client)
        {
            ScenePresence presence = m_scene.GetScenePresence(client.AgentId);
            if (presence != null)
            {
                m_log.WarnFormat("[REGION SYNC SERVER MODULE] (OnNewClient) \"{0}\"", presence.Name);
                DebugSceneStats();
            }
            else
            {
                m_log.WarnFormat("[REGION SYNC SERVER MODULE] (OnNewClient) A new client connected but module could not get ScenePresence");
            }
        }

        private void EventManager_OnNewPresence(ScenePresence presence)
        {
            m_log.WarnFormat("[REGION SYNC SERVER MODULE] Avatar \"{0}\" (1) {2} has joined the scene", presence.Firstname + " " + presence.Lastname, presence.ControllingClient.AgentId.ToString(), presence.UUID.ToString());
            DebugSceneStats();
        }
         * */

        private void EventManager_OnRemovePresence(UUID agentID)
        {
            ScenePresence avatar;
            if (m_scene.TryGetAvatar(agentID, out avatar))
            {
                m_log.WarnFormat("[REGION SYNC SERVER MODULE] Avatar \"{0}\" (1) {2} has left the scene", avatar.Firstname + " " + avatar.Lastname, agentID.ToString(), avatar.UUID.ToString());
            }
            else
            {
                m_log.WarnFormat("[REGION SYNC SERVER MODULE] Avatar \"unknown\" has left the scene");
            }
            OSDArray data = new OSDArray();
            data.Add(OSD.FromULong(avatar.RegionHandle));
            data.Add(OSD.FromUInteger(avatar.LocalId));
            RegionSyncMessage rsm = new RegionSyncMessage(RegionSyncMessage.MsgType.RemoveAvatar, data.ToString());
            m_server.Broadcast(rsm);
        }

        #endregion


        /*
        private void DebugSceneStats()
        {
            List<ScenePresence> avatars = m_scene.GetAvatars(); 
            List<EntityBase> entities = m_scene.GetEntities();
            m_log.WarnFormat("There are {0} avatars and {1} entities in the scene", avatars.Count, entities.Count);
        }

        public IClientAPI ClientAggregator
        {
            get {return m_clientAggregator;}
        }

        private void AddAvatars()
        {
            for (int i = 0; i < 1; i++)
            {
                MyNpcCharacter m_character = new MyNpcCharacter(m_scene, this);
                m_scene.AddNewClient(m_character);
                
                m_scene.AgentCrossing(m_character.AgentId, m_character.StartPos, false);
            }
        }
         */
    }
}

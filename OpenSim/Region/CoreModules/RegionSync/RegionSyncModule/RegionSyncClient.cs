using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes.Types;
using log4net;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    // The RegionSyncClient has a receive thread to process messages from the RegionSyncServer.
    public class RegionSyncClient
    {
        #region MsgHandlerStatus Enum
        public enum MsgHandlerStatus
        {
            Success, // Everything went as expected
            Trivial, // Minor issue, nothing to worry about
            Warning, // Something went wrong, we can continue
            Error    // This should certainly not have happened! (A bug)
        }
        #endregion

        #region RegionSyncClient members

        // Set the addr and port of RegionSyncServer
        private IPAddress m_addr;
        private Int32 m_port;

        // A reference to the local scene
        private Scene m_scene;

        // The avatars added to this client manager for clients on other client managers
        object m_syncRoot = new object();
        Dictionary<UUID, RegionSyncAvatar> m_remoteAvatars = new Dictionary<UUID, RegionSyncAvatar>();
        Dictionary<UUID, IClientAPI> m_localAvatars = new Dictionary<UUID, IClientAPI>();

        private Dictionary<UUID, RegionSyncAvatar> RemoteAvatars
        {
            get { return m_remoteAvatars; }
        }

        private Dictionary<UUID, IClientAPI> LocalAvatars
        {
            get { return m_localAvatars; }
        }

        // The logfile
        private ILog m_log;

        private string LogHeader = "[REGION SYNC CLIENT]";

        // The listener and the thread which listens for connections from client managers
        private Thread m_rcvLoop;

        // The client connection to the RegionSyncServer
        private TcpClient m_client = new TcpClient();

        private string m_regionName;

        private System.Timers.Timer m_statsTimer = new System.Timers.Timer(30000);
        #endregion

        // Constructor
        public RegionSyncClient(Scene scene, string addr, int port)
        {
            m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            m_scene = scene;
            m_addr = IPAddress.Parse(addr);
            m_port = port;
            
            m_statsTimer.Elapsed += new System.Timers.ElapsedEventHandler(StatsTimerElapsed);
        }

        // Start the RegionSyncClient
        public void Start()
        {
            try
            {
                m_client.Connect(m_addr, m_port);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("{0} [Start] Could not connect to RegionSyncServer at {1}:{2}", LogHeader, m_addr, m_port);
                m_log.Warn(e.Message);
            }

            m_log.WarnFormat("{0} Connected to RegionSyncServer at {1}:{2}", LogHeader, m_addr, m_port);

            m_rcvLoop = new Thread(new ThreadStart(ReceiveLoop));
            m_rcvLoop.Name = "RegionSyncClient ReceiveLoop";
            m_log.WarnFormat("{0} Starting {1} thread", LogHeader, m_rcvLoop.Name);
            m_rcvLoop.Start();
            //m_log.WarnFormat("{0} Started", LogHeader);
            DoInitialSync();
            m_statsTimer.Start();
        }

        // Disconnect from the RegionSyncServer and close the RegionSyncClient
        public void Stop()
        {
            // The remote scene will remove our avatars automatically when we disconnect
            m_rcvLoop.Abort();
            ShutdownClient();
        }

        public void ReportStatus()
        {
            Dictionary<UUID, IClientAPI> locals = LocalAvatars;
            Dictionary<UUID, RegionSyncAvatar> remotes = RemoteAvatars;

            m_log.WarnFormat("{0}: {1,4} Local avatars", LogHeader, locals.Count);
            foreach (KeyValuePair<UUID, IClientAPI> kvp in locals)
            {
                ScenePresence sp;
                bool inScene = m_scene.TryGetScenePresence(kvp.Value.AgentId, out sp);
                m_log.WarnFormat("{0}:     {1} {2} {3}", LogHeader, kvp.Value.AgentId, kvp.Value.Name, inScene ? "" : "(NOT IN SCENE)");
            }
            m_log.WarnFormat("{0}: {1,4} Remote avatars", LogHeader, remotes.Count);
            foreach (KeyValuePair<UUID, RegionSyncAvatar> kvp in remotes)
            {
                ScenePresence sp;
                bool inScene = m_scene.TryGetScenePresence(kvp.Value.AgentId, out sp);
                m_log.WarnFormat("{0}:     {1} {2} {3}", LogHeader, kvp.Value.AgentId, kvp.Value.Name, inScene ? "" : "(NOT IN SCENE)");
            }
            m_log.WarnFormat("{0}: ===================================================", LogHeader);
            m_log.WarnFormat("{0} Synchronized to RegionSyncServer at {1}:{2}", LogHeader, m_addr, m_port);
            m_log.WarnFormat("{0}: {1,4} Local avatars", LogHeader, locals.Count);
            m_log.WarnFormat("{0}: {1,4} Remote avatars", LogHeader, remotes.Count);
        }

        private void ShutdownClient()
        {
            m_log.WarnFormat("{0} Disconnected from RegionSyncServer. Shutting down.", LogHeader);

            // Remove remote avatars from local scene
            try
            {
                foreach (KeyValuePair<UUID, RegionSyncAvatar> kvp in RemoteAvatars)
                {
                    // This will cause a callback into RemoveLocalClient
                    m_scene.RemoveClient(kvp.Key);
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("Caught exception while removing remote avatars on Shutdown: {0}", e.Message);
            }

            try
            {
                // Remove local avatars from remote scene
                foreach (KeyValuePair<UUID, IClientAPI> kvp in LocalAvatars)
                {
                    // Tell the remote scene to remove this client
                    RemoveLocalClient(kvp.Key, m_scene);
                    // Remove the agent update handler from the client
                    kvp.Value.OnAgentUpdateRaw -= HandleAgentUpdateRaw;
                    kvp.Value.OnSetAppearanceRaw -= HandleSetAppearanceRaw;
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("Caught exception while removing local avatars on Shutdown: {0}", e.Message);
            }

            // Close the connection
            m_client.Client.Close();
            m_client.Close();

        }

        // Listen for messages from a RegionSyncClient
        // *** This is the main thread loop for each connected client
        private void ReceiveLoop()
        {
            m_log.WarnFormat("{0} Thread running: {1}", LogHeader, m_rcvLoop.Name);
            while (true && m_client.Connected)
            {
                RegionSyncMessage msg;
                // Try to get the message from the network stream
                try
                {
                    msg = new RegionSyncMessage(m_client.GetStream());
                    //m_log.WarnFormat("{0} Received: {1}", LogHeader, msg.ToString());
                }
                // If there is a problem reading from the client, shut 'er down. 
                catch
                {
                    ShutdownClient();
                    return;
                }
                // Try handling the message
                try
                {
                    HandleMessage(msg);
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("{0} Encountered an exception: {1} (MSGTYPE = {2})", LogHeader, e.Message, msg.ToString());
                }
            }
        }

        #region SEND
        // Send a message to a single connected RegionSyncClient
        private void Send(string msg)
        {
            byte[] bmsg = System.Text.Encoding.ASCII.GetBytes(msg + System.Environment.NewLine);
            Send(bmsg);
        }

        private void Send(RegionSyncMessage msg)
        {
            Send(msg.ToBytes());
            //m_log.WarnFormat("{0} Sent {1}", LogHeader, msg.ToString());
        }

        private void Send(byte[] data)
        {
            if (m_client.Connected)
            {
                try
                {
                    m_client.GetStream().Write(data, 0, data.Length);
                }
                // If there is a problem reading from the client, shut 'er down. 
                // *** Still need to alert the module that it's no longer connected!
                catch
                {
                    ShutdownClient();
                }
            }
        }
        #endregion

        // Send coarse locations to locally connected clients
        // This is a highly optimized version for lots of local and remote clients
        // 99.9% faster for 1000 clients!
        public void SendCoarseLocations()
        {
            List<UUID> ids = new List<UUID>();
            List<Vector3> locations = new List<Vector3>();
            m_scene.GetCoarseLocations(out ids, out locations);
            foreach (KeyValuePair<UUID, IClientAPI> kvp in LocalAvatars)
            {
                kvp.Value.SendCoarseLocationUpdate(ids, locations);
            }
        }

        // Handle an incoming message
        // TODO: This should not be synchronous with the receive!
        // Instead, handle messages from an incoming Queue so server doesn't block sending
        private void HandleMessage(RegionSyncMessage msg)
        {
            switch (msg.Type)
            {
                case RegionSyncMessage.MsgType.RegionName:
                    {
                        m_regionName = Encoding.ASCII.GetString(msg.Data, 0, msg.Length);
                        RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Syncing to region \"{0}\"", m_regionName));
                        return;
                    }
                case RegionSyncMessage.MsgType.Terrain:
                    {
                        m_scene.Heightmap.LoadFromXmlString(Encoding.ASCII.GetString(msg.Data, 0, msg.Length));
                        RegionSyncMessage.HandleSuccess(LogHeader, msg, "Synchronized terrain");
                        return;
                    }
                case RegionSyncMessage.MsgType.NewObject:
                case RegionSyncMessage.MsgType.UpdatedObject:
                    {
                        SceneObjectGroup sog = SceneObjectSerializer.FromXml2Format(Encoding.ASCII.GetString(msg.Data, 0, msg.Length));
                        if (sog.IsDeleted)
                        {
                            RegionSyncMessage.HandleTrivial(LogHeader, msg, String.Format("Ignoring update on deleted LocalId {0}.", sog.LocalId.ToString()));
                            return;
                        }

                        if (m_scene.AddNewSceneObject(sog, true));
                            //RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Object \"{0}\" ({1}) ({1}) updated.", sog.Name, sog.UUID.ToString(), sog.LocalId.ToString()));
                        //else
                            //RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Object \"{0}\" ({1}) ({1}) added.", sog.Name, sog.UUID.ToString(), sog.LocalId.ToString()));
                        sog.ScheduleGroupForFullUpdate();
                        return;
                    }
                case RegionSyncMessage.MsgType.RemovedObject:
                    {
                        // Get the data from message and error check
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }

                        // Get the parameters from data
                        //ulong regionHandle = data["regionHandle"].AsULong();
                        uint localID = data["localID"].AsUInteger();

                        // Find the object in the scene
                        SceneObjectGroup sog = m_scene.SceneGraph.GetGroupByPrim(localID);
                        if (sog == null)
                        {
                            //RegionSyncMessage.HandleWarning(LogHeader, msg, String.Format("localID {0} not found.", localID.ToString()));
                            return;
                        }

                        // Delete the object from the scene
                        m_scene.DeleteSceneObject(sog, false);
                        RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("localID {0} deleted.", localID.ToString()));
                        return;
                    }
                case RegionSyncMessage.MsgType.NewAvatar:
                    {
                        // Get the data from message and error check
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }

                        // Get the parameters from data and error check
                        UUID agentID = data["agentID"].AsUUID();
                        string first = data["first"].AsString();
                        string last = data["last"].AsString();
                        Vector3 startPos = data["startPos"].AsVector3();
                        if (agentID == null || agentID == UUID.Zero || first == null || last == null || startPos == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Missing or invalid JSON data.");
                            return;
                        }

                        if (m_remoteAvatars.ContainsKey(agentID))
                        {
                            RegionSyncMessage.HandleWarning(LogHeader, msg, String.Format("Attempted to add duplicate avatar \"{0} {1}\" ({2})", first, last, agentID.ToString()));
                            return;
                        }

                        ScenePresence sp;
                        if (m_scene.TryGetScenePresence(agentID, out sp))
                        {
                            //RegionSyncMessage.HandlerDebug(LogHeader, msg, String.Format("Confirmation of new avatar \"{0}\" ({1})", sp.Name, sp.UUID.ToString()));
                            RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Confirmation of new avatar \"{0}\" ({1})", sp.Name, sp.UUID.ToString()));
                            return;
                        }

                        RegionSyncAvatar av = new RegionSyncAvatar(m_scene, agentID, first, last, startPos);
                        m_remoteAvatars.Add(agentID, av);
                        m_scene.AddNewClient(av);
                        //RegionSyncMessage.HandlerDebug(LogHeader, msg, String.Format("Added new remote avatar \"{0}\" ({1})", first + " " + last, agentID));
                        RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Added new remote avatar \"{0}\" ({1})", first + " " + last, agentID));
                        return;
                        
                    }
                case RegionSyncMessage.MsgType.UpdatedAvatar:
                    {
                        // Get the data from message and error check
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }

                        // Get the parameters from data and error check
                        UUID agentID = UUID.Zero;
                        Vector3 pos = Vector3.Zero;
                        Vector3 vel = Vector3.Zero;
                        Quaternion rot = Quaternion.Identity;
                        bool flying = false;
                        string anim = "";
                        uint flags = 0;

                        try
                        {
                            agentID = data["id"].AsUUID();
                            pos = data["pos"].AsVector3();
                            vel = data["vel"].AsVector3();
                            rot = data["rot"].AsQuaternion();
                            flying = data["fly"].AsBoolean();
                            anim = data["anim"].AsString();
                            flags = data["flags"].AsUInteger();
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat("{0} Caught exception in UpdatedAvatar handler (Decoding JSON): {1}", LogHeader, e.Message);
                        }
                        if (agentID == null || agentID == UUID.Zero )
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Missing or invalid JSON data.");
                            return;
                        }

                        // Find the presence in the scene and error check
                        ScenePresence presence;
                        m_scene.TryGetScenePresence(agentID, out presence);
                        if (presence == null)
                        {
                            //RegionSyncMessage.HandleWarning(LogHeader, msg, String.Format("agentID {0} not found.", agentID.ToString()));
                            return;
                        }

                        /**
                        // Update the scene presence from parameters
                        bool updateAnimations = ((presence.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_FLY        ) != (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_FLY        ) ||
                                                 (presence.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_AT_POS     ) != (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_AT_POS     ) ||
                                                 (presence.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG     ) != (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG     ) ||
                                                 (presence.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_UP_POS     ) != (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_UP_POS     ) ||
                                                 (presence.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG     ) != (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG     ) ||
                                                 (presence.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_TURN_LEFT  ) != (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_TURN_LEFT  ) ||
                                                 (presence.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_TURN_RIGHT ) != (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_TURN_RIGHT ));
                        */
                        try
                        {
                            presence.AgentControlFlags = flags;
                            presence.AbsolutePosition = pos;
                            presence.Velocity = vel;
                            presence.Rotation = rot;
                            // It seems the physics scene can drop an avatar if the avatar makes it angry
                            if (presence.PhysicsActor != null)
                            {
                                presence.PhysicsActor.Flying = flying;
                                presence.PhysicsActor.CollidingGround = !flying;
                            }
                        }
                        catch(Exception e)
                        {
                            m_log.ErrorFormat("{0} Caught exception in UpdatedAvatar handler (setting presence values) for {1}: {2}", LogHeader, presence.Name, e.Message);
                        }
                        try
                        {
                            presence.SendTerseUpdateToAllClients();
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat("{0} Caught exception in UpdatedAvatar handler (SendTerseUpdateToAllClients): {1}", LogHeader, e.Message);
                        }
                        /*
                        foreach (KeyValuePair<UUID, IClientAPI> kvp in LocalAvatars)
                        {
                            presence.SendTerseUpdateToClient(kvp.Value);
                        }*/

                        try
                        {
                            // If the animation has changed, set the new animation
                            if (!presence.Animator.CurrentMovementAnimation.Equals(anim))
                                presence.Animator.TrySetMovementAnimation(anim);
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat("{0} Caught exception in UpdatedAvatar handler (TrySetMovementAnimation): {1}", LogHeader, e.Message);
                        }
                        /*
                         * result = String.Format("Avatar \"{0}\" ({1}) ({2}) updated (pos:{3}, vel:{4}, rot:{5}, fly:{6})",
                                presence.Name, presence.UUID.ToString(), presence.LocalId.ToString(),
                                presence.AbsolutePosition.ToString(), presence.Velocity.ToString(), 
                                presence.Rotation.ToString(), presence.PhysicsActor.Flying ? "Y" : "N");
                           HandleSuccess(msg, result);
                         * */
                        return;
                    }
                case RegionSyncMessage.MsgType.RemovedAvatar:
                    {
                        // Get the data from message and error check
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }

                        // Get the agentID from data and error check
                        UUID agentID = data["agentID"].AsUUID();
                        if (agentID == UUID.Zero)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Missing or invalid JSON data.");
                            return;
                        }

                        // Try to get the presence from the scene
                        
                        
                        ScenePresence presence;
                        m_scene.TryGetScenePresence(agentID, out presence);

                        // Local and Remote avatar dictionaries will only be modified under this lock
                        // They can be read without locking as we are just going to swap refs atomically
                        lock (m_syncRoot)
                        {

                            if (RemoteAvatars.ContainsKey(agentID))
                            {
                                Dictionary<UUID, RegionSyncAvatar> newremotes = new Dictionary<UUID, RegionSyncAvatar>(RemoteAvatars);
                                string name = newremotes[agentID].Name;
                                // Remove from list of remote avatars
                                newremotes.Remove(agentID);
                                m_remoteAvatars = newremotes;

                                // If this presence is in out remote list, then this message tells us to remove it from the 
                                // local scene so we should find it there.
                                if (presence == null)
                                {
                                    RegionSyncMessage.HandleError(LogHeader, msg, String.Format("Couldn't remove remote avatar \"{0}\" because it's not in local scene", name));
                                    return;
                                }
                                m_scene.RemoveClient(agentID);
                                RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Remote avatar \"{0}\" removed from scene", name));
                                return;
                            }
                            else if (LocalAvatars.ContainsKey(agentID))
                            {
                                Dictionary<UUID, IClientAPI> newlocals = new Dictionary<UUID, IClientAPI>(LocalAvatars);
                                string name = newlocals[agentID].Name;
                                // Remove from list of local avatars
                                newlocals.Remove(agentID);
                                m_localAvatars = newlocals;

                                // If this presence is in our local list, then this message is a confirmation from the server
                                // and the presence should not be in our scene at this time.
                                if (presence != null)
                                {
                                    RegionSyncMessage.HandleError(LogHeader, msg, String.Format("Remove avatar confirmation received for \"{0}\" still in the local scene.", name));
                                    return;
                                }
                                RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Received confirmation of removed avatar \"{0}\"", name));
                                return;
                            }
                        }
                        // Getting here is always an error because we have been asked to remove and avatar 
                        // so it should have been in either the local or remote collections
                        if (presence == null)
                            RegionSyncMessage.HandleError(LogHeader, msg, String.Format("Avatar is not local OR remote and was not found in scene: \"{0}\" {1}", presence.Name, agentID.ToString()));
                        else
                            RegionSyncMessage.HandleError(LogHeader, msg, String.Format("Avatar is not local OR remote but was found in scene: {1}", agentID.ToString()));
                        return;
                    }
                case RegionSyncMessage.MsgType.ChatFromClient:
                    {
                        // Get the data from message and error check
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }
                        OSChatMessage args = new OSChatMessage();
                        args.Channel = data["channel"].AsInteger();
                        args.Message = data["msg"].AsString();
                        args.Position = data["pos"].AsVector3();
                        args.From = data["name"].AsString();
                        UUID id = data["id"].AsUUID();
                        args.Scene = m_scene;
                        args.Type = ChatTypeEnum.Say;
                        ScenePresence sp;
                        m_scene.TryGetScenePresence(id, out sp);
                        if (sp != null)
                        {
                            args.Sender = sp.ControllingClient;
                            args.SenderUUID = id;
                            m_scene.EventManager.TriggerOnChatBroadcast(sp.ControllingClient, args);
                        }

                        //RegionSyncMessage.HandlerDebug(LogHeader, msg, String.Format("Received chat from \"{0}\"", args.From));
                        RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Received chat from \"{0}\"", args.From));
                        return;
                    }
                case RegionSyncMessage.MsgType.AvatarAppearance:
                    {
                        //m_log.WarnFormat("{0} START of AvatarAppearance handler", LogHeader); 
                        // Get the data from message and error check
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }

                        // Get the parameters from data and error check
                        UUID agentID = data["id"].AsUUID();
                        if (agentID == null || agentID == UUID.Zero)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Missing or invalid JSON data.");
                            return;
                        }

                        // Find the presence in the scene
                        ScenePresence presence;
                        if (m_scene.TryGetScenePresence(agentID, out presence))
                        {
                            string name = presence.Name;
                            Primitive.TextureEntry te = Primitive.TextureEntry.FromOSD(data["te"]);
                            byte[] vp = data["vp"].AsBinary();

                            bool missingBakes = false;
                            byte[] BAKE_INDICES = new byte[] { 8, 9, 10, 11, 19, 20 };
                            for (int i = 0; i < BAKE_INDICES.Length; i++)
                            {
                                int j = BAKE_INDICES[i];
                                Primitive.TextureEntryFace face = te.FaceTextures[j];
                                if (face != null && face.TextureID != AppearanceManager.DEFAULT_AVATAR_TEXTURE)
                                {
                                    if (m_scene.AssetService.Get(face.TextureID.ToString()) == null)
                                    {
                                        RegionSyncMessage.HandlerDebug(LogHeader, msg, "Missing baked texture " + face.TextureID + " (" + j + ") for avatar " + name);
                                        missingBakes = true;
                                    }
                                }
                            }

                            //m_log.WarnFormat("{0} Calling presence.SetAppearance for {1} (\"{2}\") {3}", LogHeader, agentID, presence.Name, missingBakes ? "MISSING BAKES" : "GOT BAKES");
                            presence.SetAppearance(te, vp);
                            RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Set appearance for {0}", name));
                        }
                        else
                        {
                            RegionSyncMessage.HandleWarning(LogHeader, msg, String.Format("Agent {0} not found in the scene.", agentID));
                        }
                        //m_log.WarnFormat("{0} END of AvatarAppearance handler", LogHeader); 
                        return;
                    }
                default:
                    {
                        RegionSyncMessage.HandleError(LogHeader, msg, String.Format("{0} Unsupported message type: {1}", LogHeader, ((int)msg.Type).ToString()));
                        return;
                    }
            }
        }


        HashSet<string> exceptions = new HashSet<string>();
        private OSDMap DeserializeMessage(RegionSyncMessage msg)
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

        private void DoInitialSync()
        {
            m_scene.DeleteAllSceneObjects();
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.RegionName, m_scene.RegionInfo.RegionName));
            m_log.WarnFormat("Sending region name: \"{0}\"", m_scene.RegionInfo.RegionName);
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.GetTerrain));
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.GetObjects));
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.GetAvatars));

            // Register for events which will be forwarded to authoritative scene
            m_scene.EventManager.OnNewClient += EventManager_OnNewClient;
            m_scene.EventManager.OnClientClosed += new EventManager.ClientClosed(RemoveLocalClient);
        }

        public string GetServerAddressAndPort()
        {
            return m_addr.ToString() + ":" + m_port.ToString();
        }

        #region MESSAGES SENT FROM CLIENT MANAGER TO SIM
        public void EventManager_OnNewClient(IClientAPI client)
        {
            // If this client was added in response to NewAvatar message from a synced server, 
            // don't subscribe to events or send back to server
            if (RemoteAvatars.ContainsKey(client.AgentId))
                    return;
            // Otherwise, it's a real local client connecting so track it locally
            lock (m_syncRoot)
            {
                Dictionary<UUID, IClientAPI> newlocals = new Dictionary<UUID, IClientAPI>(LocalAvatars);
                newlocals.Add(client.AgentId, client);
                m_localAvatars = newlocals;
            }
            m_log.WarnFormat("{0} New local client \"{1}\" ({2}) being added to remote scene.", LogHeader, client.Name, client.AgentId.ToString());
            // Let the auth sim know that a new agent has connected
            OSDMap data = new OSDMap(4);
            data["agentID"] = OSD.FromUUID(client.AgentId);
            data["first"] = OSD.FromString(client.FirstName);
            data["last"] = OSD.FromString(client.LastName);
            data["startPos"] = OSD.FromVector3(client.StartPos);
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.AgentAdd, OSDParser.SerializeJsonString(data)));

            // Register for interesting client events which will be forwarded to auth sim
            // These are the raw packet data blocks from the client, intercepted and sent up to the sim
            client.OnAgentUpdateRaw += HandleAgentUpdateRaw;
            client.OnSetAppearanceRaw += HandleSetAppearanceRaw;
            client.OnChatFromClientRaw += HandleChatFromClientRaw;
        }

        void RemoveLocalClient(UUID clientID, Scene scene)
        {
            lock (m_syncRoot)
            {
                // If the client to be removed is a local client, then send a message to the remote scene
                if (LocalAvatars.ContainsKey(clientID))
                {
                    // If we are still connected, let the auth sim know that an agent has disconnected
                    if (m_client.Connected)
                    {
                        m_log.WarnFormat("{0} Local client ({1}) has left the scene. Notifying remote scene.", LogHeader, clientID.ToString());
                        OSDMap data = new OSDMap(1);
                        data["agentID"] = OSD.FromUUID(clientID);
                        Send(new RegionSyncMessage(RegionSyncMessage.MsgType.AgentRemove, OSDParser.SerializeJsonString(data)));
                    }
                    else
                    {
                        m_log.WarnFormat("{0} Local client ({1}) has left the scene.", LogHeader, clientID.ToString());
                        // Remove it from our list of local avatars (we will never receive a confirmation since we are disconnected)
                        Dictionary<UUID, IClientAPI> newlocals = new Dictionary<UUID, IClientAPI>(LocalAvatars);
                        newlocals.Remove(clientID);
                        m_localAvatars = newlocals;
                    }
                }
            }
        }

        /// <summary>
        /// This is the event handler for client movement. If a client is moving, this event is triggering.
        /// </summary>
        public void HandleAgentUpdateRaw(object sender, byte[] agentData)
        {
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.AgentUpdate, agentData));
        }

        public void HandleSetAppearanceRaw(object sender, UUID agentID, byte[] vp, Primitive.TextureEntry te)
        {
            // Try to find the scene presence we want to set the appearance for
            ScenePresence sp;
            string name = "NOT FOUND";
            if (m_scene.TryGetScenePresence(agentID, out sp))
                name = sp.Name;
            m_log.WarnFormat("{0} Received LLClientView.SetAppearance ({1,3},{2,2}) for {3} (\"{4}\")", LogHeader, vp.Length.ToString(), (te == null) ? "" : "te", agentID.ToString(), sp.Name);
            if (sp == null)
                return;

            // Set the appearance on the presence. This will generate the needed exchange with the client if rebakes need to take place.
            m_log.WarnFormat("{0} Setting appearance on ScenePresence {1} \"{2}\"", LogHeader, sp.UUID, sp.Name);
            sp.SetAppearance(te, vp);

            if (te != null)
            {
                //m_log.WarnFormat("{0} Sending appearance to server for {1} \"{2}\"", LogHeader, sp.UUID, sp.Name);
                OSDMap data = new OSDMap(3);
                data["id"] = OSDUUID.FromUUID(sp.UUID);
                data["vp"] = new OSDBinary(sp.Appearance.VisualParams);
                data["te"] = te.GetOSD();
                Send(new RegionSyncMessage(RegionSyncMessage.MsgType.AvatarAppearance, OSDParser.SerializeJsonString(data)));
            }
        }

        public void HandleChatFromClientRaw(object sender, byte[] chatData)
        {
            if (chatData != null && sender is IClientAPI)
            {
                IClientAPI client = (IClientAPI)sender;
                ScenePresence presence;
                m_scene.TryGetScenePresence(client.AgentId, out presence);
                if(presence != null)
                {
                    int len = 0;
                    ChatFromViewerPacket.ChatDataBlock cdb = new ChatFromViewerPacket.ChatDataBlock();
                    cdb.FromBytes(chatData, ref len);
                    if (cdb.Message.Length == 0)
                        return;
                    OSDMap data = new OSDMap(5);
                    data["channel"] = OSD.FromInteger(cdb.Channel);
                    data["msg"] = OSD.FromString(Utils.BytesToString(cdb.Message));
                    data["pos"] = OSD.FromVector3(presence.AbsolutePosition);
                    data["name"] = OSD.FromString(presence.Name);
                    data["id"] = OSD.FromUUID(presence.UUID);

                    Send(new RegionSyncMessage(RegionSyncMessage.MsgType.ChatFromClient, OSDParser.SerializeJsonString(data)));
                    //m_log.WarnFormat("Forwarding chat message from local client \"{0}\" to remote scene", presence.Name);
                }
            }
        }
        #endregion


        private void StatsTimerElapsed(object source, System.Timers.ElapsedEventArgs e)
        {
            // Let the auth sim know about this client's POV Re: Client counts
            int t, l, r;
            lock (m_syncRoot)
            {
                t = m_scene.SceneGraph.GetRootAgentCount();
                l = LocalAvatars.Count;
                r = RemoteAvatars.Count;
            }
            OSDMap data = new OSDMap(3);
            data["total"] = OSD.FromInteger(t);
            data["local"] = OSD.FromInteger(l);
            data["remote"] = OSD.FromInteger(r);
            //m_log.WarnFormat("{0}: Sent stats: {1},{2},{3}", LogHeader, t, l, r);
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.RegionStatus, OSDParser.SerializeJsonString(data)));
        }
    }
}

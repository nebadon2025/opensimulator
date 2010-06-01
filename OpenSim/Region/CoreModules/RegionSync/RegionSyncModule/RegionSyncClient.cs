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

namespace OpenSim.Region.Examples.RegionSyncModule
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

        // The logfile
        private ILog m_log;

        private string LogHeader = "[REGION SYNC CLIENT]";

        // The listener and the thread which listens for connections from client managers
        private Thread m_rcvLoop;

        // The client connection to the RegionSyncServer
        private TcpClient m_client = new TcpClient();

        // The queue of incoming messages which need handling
        //private Queue<string> m_inQ = new Queue<string>();
        #endregion

        // Constructor
        public RegionSyncClient(Scene scene, string addr, int port)
        {
            m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            //m_log.WarnFormat("{0} Constructed", LogHeader);
            m_scene = scene;
            m_addr = IPAddress.Parse(addr);
            m_port = port;
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
        }

        // Disconnect from the RegionSyncServer and close the RegionSyncClient
        public void Stop()
        {
            // The remote scene will remove our avatars automatically when we disconnect
            m_rcvLoop.Abort();
            ShutdownClient();
        }

        private void ShutdownClient()
        {
            m_log.WarnFormat("{0} Disconnected from RegionSyncServer. Shutting down.", LogHeader);

            lock (m_syncRoot)
            {
                // Remove remote avatars from local scene
                try
                {
                    List<UUID> remoteAvatars = new List<UUID>(m_remoteAvatars.Keys);
                    foreach (UUID id in remoteAvatars)
                    {
                        m_scene.RemoveClient(id);
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("Caught exception while removing remote avatars on Shutdown: {0}", e.Message);
                }
                try
                {
                    // Remove local avatars from remote scene
                    List<KeyValuePair<UUID, IClientAPI>> localAvatars = new List<KeyValuePair<UUID, IClientAPI>>(m_localAvatars);
                    foreach (KeyValuePair<UUID, IClientAPI> kvp in localAvatars)
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
                    string message;
                    MsgHandlerStatus status;
                    lock(m_syncRoot)
                        status = HandleMessage(msg, out message);
                    switch (status)
                    {
                        case MsgHandlerStatus.Success:
                                //m_log.WarnFormat("{0} Handled {1}: {2}", LogHeader, msg.ToString(), message);
                                break;
                        case MsgHandlerStatus.Trivial:
                                m_log.WarnFormat("{0} Issue handling {1}: {2}", LogHeader, msg.ToString(), message);
                                break;
                        case MsgHandlerStatus.Warning:
                                m_log.WarnFormat("{0} Warning handling {1}: {2}", LogHeader, msg.ToString(), message);
                                break;
                        case MsgHandlerStatus.Error:
                                m_log.WarnFormat("{0} Error handling {1}: {2}", LogHeader, msg.ToString(), message);
                                break;
                    }
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("{0} Encountered an exception: {1}", LogHeader, e.Message);
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
            lock (m_syncRoot)
            {
                foreach (IClientAPI client in m_localAvatars.Values)
                {
                    client.SendCoarseLocationUpdate(ids, locations);
                }
            }
        }

        HashSet<string> exceptions = new HashSet<string>();
        private OSDMap DeserializeMessage(RegionSyncMessage msg)
        {
            OSDMap data = null;
            try
            {
                data = OSDParser.DeserializeJson(Encoding.ASCII.GetString(msg.Data)) as OSDMap;
            }
            catch (Exception e)
            {
                lock (exceptions)
                    // If this is a new message, then print the underlying data that caused it
                    if (!exceptions.Contains(e.Message))
                        m_log.Error(LogHeader + " " + Encoding.ASCII.GetString(msg.Data));
                data = null;
            }
            return data;
        }

        // Handle an incoming message
        // Returns true if the message was processed
        // TODO: This should not be synchronous with the receive!
        // Instead, handle messages from the incoming Queue
        private MsgHandlerStatus HandleMessage(RegionSyncMessage msg, out string result)
        {
            switch (msg.Type)
            {
                case RegionSyncMessage.MsgType.Terrain:
                    {
                        m_scene.Heightmap.LoadFromXmlString(Encoding.ASCII.GetString(msg.Data));
                        result = "Synchronized terrain";
                        return MsgHandlerStatus.Success;
                    }
                case RegionSyncMessage.MsgType.NewObject:
                case RegionSyncMessage.MsgType.UpdatedObject:
                    {
                        SceneObjectGroup sog = SceneObjectSerializer.FromXml2Format(Encoding.ASCII.GetString(msg.Data));
                        if (sog.IsDeleted)
                        {
                            result = String.Format("Ignoring update on deleted LocalId {0}.", sog.LocalId.ToString());
                            return MsgHandlerStatus.Trivial;
                        }
                        if (m_scene.AddNewSceneObject(sog, true))
                            result = String.Format("Object \"{0}\" ({1}) ({1}) updated.", sog.Name, sog.UUID.ToString(), sog.LocalId.ToString());
                        else
                            result = String.Format("Object \"{0}\" ({1}) ({1}) added.", sog.Name, sog.UUID.ToString(), sog.LocalId.ToString());
                        sog.ScheduleGroupForFullUpdate();
                        return MsgHandlerStatus.Success;
                    }
                      //  return HandlerSuccess(msg, "Updated avatar");
                    /*
                case RegionSyncMessage.MsgType.SetObjectPosition: Attributes!
                    {
                        if (part.ParentGroup == null)
                        {
                            part.UpdateOffSet(new Vector3((float)real_vec.x, (float)real_vec.y, (float)real_vec.z));
                        }
                        else if (part.ParentGroup.RootPart == part)
                        {
                            part.parent.UpdateGroupPosition(new Vector3((float)real_vec.x, (float)real_vec.y, (float)real_vec.z));
                        }
                        else
                        {
                            part.OffsetPosition = new Vector3((float)targetPos.x, (float)targetPos.y, (float)targetPos.z);
                            SceneObjectGroup parent = part.ParentGroup;
                            parent.HasGroupChanged = true;
                            parent.ScheduleGroupForTerseUpdate();
                        }
                    }
                     * */
                case RegionSyncMessage.MsgType.RemovedObject:
                    {
                        OSDMap data = DeserializeMessage(msg);
                        if( data != null )
                        {
                            ulong regionHandle = data["regionHandle"].AsULong();
                            uint localID = data["localID"].AsUInteger();

                            // We get the UUID of the object to be deleted, find it in the scene
                            if (regionHandle != 0 && localID != 0 )
                            {
                                if (regionHandle == m_scene.RegionInfo.RegionHandle)
                                {
                                    SceneObjectGroup sog = m_scene.SceneGraph.GetGroupByPrim(localID);
                                    if (sog != null)
                                    {
                                        m_scene.DeleteSceneObject(sog, false);
                                        result = String.Format("localID {0} deleted.", localID.ToString());
                                        return MsgHandlerStatus.Success;
                                    }
                                }
                                result = String.Format("ignoring delete request for non-local regionHandle {0}.", regionHandle.ToString());
                                return MsgHandlerStatus.Trivial;
                            }
                            result = String.Format("localID {0} not found.", localID.ToString());
                            return MsgHandlerStatus.Warning;
                        }
                        result = "Could not deserialize JSON data.";
                        return MsgHandlerStatus.Error;
                    }
                case RegionSyncMessage.MsgType.NewAvatar:
                    {
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            result = "Could not deserialize JSON data.";
                            return MsgHandlerStatus.Error;
                        }

                        UUID agentID = data["agentID"].AsUUID();
                        string first = data["first"].AsString();
                        string last = data["last"].AsString();
                        Vector3 startPos = data["startPos"].AsVector3();

                        if (agentID == null || agentID == UUID.Zero || first == null || last == null || startPos == null)
                        {
                            result = "Missing or invalid JSON data.";
                            return MsgHandlerStatus.Error;
                        }

                        if (m_remoteAvatars.ContainsKey(agentID))
                        {
                            result = String.Format("Attempted to add duplicate avatar \"{0} {1}\" ({2})", first, last, agentID.ToString());
                            return MsgHandlerStatus.Warning;
                        }

                        ScenePresence sp;
                        if (m_scene.TryGetScenePresence(agentID, out sp))
                        {
                            result = String.Format("Confirmation of new avatar \"{0}\" ({1})", sp.Name, sp.UUID.ToString());
                            HandlerDebug(msg, result);
                            return MsgHandlerStatus.Success;
                        }

                        RegionSyncAvatar av = new RegionSyncAvatar(m_scene, agentID, first, last, startPos);
                        m_remoteAvatars.Add(agentID, av);
                        m_scene.AddNewClient(av);
                        result = String.Format("Added new remote avatar \"{0}\" ({1})", first + " " + last, agentID);
                        HandlerDebug(msg, result);
                        return MsgHandlerStatus.Success;
                        
                    }
                case RegionSyncMessage.MsgType.UpdatedAvatar:
                    {
                        // Get the data from message and error check
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            result = "Could not deserialize JSON data.";
                            return MsgHandlerStatus.Error;
                        }

                        // Get the parameters from data and error check
                        UUID agentID = data["id"].AsUUID();
                        Vector3 pos = data["pos"].AsVector3();
                        Vector3 vel = data["vel"].AsVector3();
                        Quaternion rot = data["rot"].AsQuaternion();
                        bool flying = data["fly"].AsBoolean();
                        uint flags = data["flags"].AsUInteger();
                        if (agentID == null || agentID == UUID.Zero )
                        {
                            result = "Missing or invalid JSON data.";
                            return MsgHandlerStatus.Error;
                        }

                        // Find the presence in the scene and error check
                        ScenePresence presence;
                        m_scene.TryGetScenePresence(agentID, out presence);
                        if (presence == null)
                        {
                            result = String.Format("agentID {0} not found.", agentID.ToString());
                            return MsgHandlerStatus.Warning;
                        }

                        // Update the scene presence from parameters
                        /*
                        bool updateAnimations = ((presence.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_FLY        ) != (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_FLY        ) ||
                                                 (presence.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_AT_POS     ) != (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_AT_POS     ) ||
                                                 (presence.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG     ) != (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG     ) ||
                                                 (presence.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_UP_POS     ) != (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_UP_POS     ) ||
                                                 (presence.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG     ) != (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG     ) ||
                                                 (presence.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_TURN_LEFT  ) != (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_TURN_LEFT  ) ||
                                                 (presence.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_TURN_RIGHT ) != (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_TURN_RIGHT ));
                         * */
                        presence.AgentControlFlags = flags;
                        presence.AbsolutePosition = pos;
                        presence.Velocity = vel;
                        presence.Rotation = rot;
                        bool updateAnimations = 
                        presence.PhysicsActor.Flying = flying;
                        List<IClientAPI> locals;
                        lock (m_syncRoot)
                            locals = new List<IClientAPI>(m_localAvatars.Values);
                        presence.SendTerseUpdateToClientList(locals);
                        if(updateAnimations)
                            presence.Animator.UpdateMovementAnimations();
                        /*
                         * result = String.Format("Avatar \"{0}\" ({1}) ({2}) updated (pos:{3}, vel:{4}, rot:{5}, fly:{6})",
                                presence.Name, presence.UUID.ToString(), presence.LocalId.ToString(),
                                presence.AbsolutePosition.ToString(), presence.Velocity.ToString(), 
                                presence.Rotation.ToString(), presence.PhysicsActor.Flying ? "Y" : "N");
                         * */
                        result = ""; // For performance, skip creating that long string unless debugging
                        return MsgHandlerStatus.Success;
                    }
                case RegionSyncMessage.MsgType.RemovedAvatar:
                    {
                        // Get the data from message and error check
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            result = "Could not deserialize JSON data.";
                            return MsgHandlerStatus.Error;
                        }

                        // Get the agentID from data and error check
                        UUID agentID = data["agentID"].AsUUID();
                        if (agentID == UUID.Zero)
                        {
                            result = "Missing or invalid JSON data.";
                            return MsgHandlerStatus.Error;
                        }

                        // Find the presence in the scene and error check
                        ScenePresence presence;
                        m_scene.TryGetScenePresence(agentID, out presence);
                        if(presence == null)
                        {
                            result = String.Format("agentID {0} not found.", agentID.ToString());
                            return MsgHandlerStatus.Success;
                        }

                        // Remove the presence from the scene and if it's remote, then from the remote list too
                        m_scene.RemoveClient(agentID);
                        if (m_remoteAvatars.ContainsKey(agentID))
                        {
                            result = String.Format("Remote avatar \"{0}\" removed from scene", presence.Name);
                            HandlerDebug(msg, result);
                            return MsgHandlerStatus.Success;
                        }
                        else if(m_localAvatars.ContainsKey(agentID))
                        {
                            result = String.Format("Received confirmation of removed avatar \"{0}\" ({1})", presence.Name, presence.UUID.ToString());
                            HandlerDebug(msg, result);
                            return MsgHandlerStatus.Success;
                        }
                        result = String.Format("Avatar is not local OR remote but was found in scene: \"{0}\" ({1})", presence.Name, presence.UUID.ToString());
                        HandlerDebug(msg, result);
                        return MsgHandlerStatus.Error;
                    }
                case RegionSyncMessage.MsgType.ChatFromClient:
                    {
                        // Get the data from message and error check
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            result = "Could not deserialize JSON data.";
                            return MsgHandlerStatus.Error;
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

                        result = String.Format("Received chat from \"{0}\"", args.From);
                        HandlerDebug(msg, result);
                        return MsgHandlerStatus.Success;
                    }
                case RegionSyncMessage.MsgType.AvatarAppearance:
                    {
                        // Get the data from message and error check
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            result = "Could not deserialize JSON data.";
                            return MsgHandlerStatus.Error;
                        }

                        // Get the parameters from data and error check
                        UUID agentID = data["id"].AsUUID();
                        if (agentID == null || agentID == UUID.Zero)
                        {
                            result = "Missing or invalid JSON data.";
                            return MsgHandlerStatus.Error;
                        }

                        // Find the presence in the scene
                        ScenePresence presence;
                        if (m_scene.TryGetScenePresence(agentID, out presence))
                        {
                            string name = presence.Name;
                            Primitive.TextureEntry te = Primitive.TextureEntry.FromOSD(data["te"]);
                            byte[] vp = data["vp"].AsBinary();

                            byte[] BAKE_INDICES = new byte[] { 8, 9, 10, 11, 19, 20 };
                            for (int i = 0; i < BAKE_INDICES.Length; i++)
                            {
                                int j = BAKE_INDICES[i];
                                Primitive.TextureEntryFace face = te.FaceTextures[j];
                                if (face != null && face.TextureID != AppearanceManager.DEFAULT_AVATAR_TEXTURE)
                                    if (m_scene.AssetService.Get(face.TextureID.ToString()) == null)
                                        HandlerDebug(msg, "Missing baked texture " + face.TextureID + " (" + j + ") for avatar " + name);
                            }

                            presence.SetAppearance(te, vp);
                            result = String.Format("Agent \"{0}\" ({1}) updated their appearance.", name, agentID);
                            return MsgHandlerStatus.Success;
                        }
                        result = String.Format("Agent {0} not found in the scene.", agentID);
                        return MsgHandlerStatus.Warning;
                    }

                    /*
                case RegionSyncMessage.MsgType.UpdateObject:
                    {
                        UUID uuid;
                        if (UUID.TryParse(Encoding.ASCII.GetString(msg.Data), out uuid))
                        {
                            // We get the UUID of the object to be updated, find it in the scene
                            SceneObjectGroup sog = m_scene.SceneGraph.GetGroupByPrim(uuid);
                            Vector3 v = new Vector3();
                            v.ToString();
                            if (sog != null)
                            {
                                //m_scene.DeleteSceneObject(sog, false);
                                return HandlerSuccess(msg, String.Format("UUID {0} updated.", uuid.ToString()));
                            }
                            else
                            {
                                return HandlerFailure(msg, String.Format("UUID {0} not found.", uuid.ToString()));
                            }
                        }
                        return HandlerFailure(msg, "Could not parse UUID");
                    }
                     * */
                default:
                    {
                        result = String.Format("{0} Unsupported message type: {1}", LogHeader, ((int)msg.Type).ToString());
                        return MsgHandlerStatus.Error;
                    }
            }
        }

        private bool HandlerDebug(RegionSyncMessage msg, string handlerMessage)
        {
            m_log.WarnFormat("{0} DBG ({1}): {2}", LogHeader, msg.ToString(), handlerMessage);
            return true;
        }

        private void DoInitialSync()
        {
            m_scene.DeleteAllSceneObjects();
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
            lock (m_remoteAvatars)
            {
                if (m_remoteAvatars.ContainsKey(client.AgentId))
                    return;
            }
            // Otherwise, it's a real local client connecting so track it locally
            lock(m_syncRoot)
                m_localAvatars.Add(client.AgentId, client);
            m_log.WarnFormat("{0} New local client \"{1}\" ({2}) being added to remote scene.", LogHeader, client.Name, client.AgentId.ToString());
            // Let the auth sim know that a new agent has connected
            OSDMap data = new OSDMap(1);
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
                if (m_localAvatars.ContainsKey(clientID))
                {
                    m_log.WarnFormat("{0} Local client ({1}) being removed from remote scene.", LogHeader, clientID.ToString());
                    m_localAvatars.Remove(clientID);
                    // Let the auth sim know that an agent has disconnected
                    OSDMap data = new OSDMap(1);
                    data["agentID"] = OSD.FromUUID(clientID);
                    Send(new RegionSyncMessage(RegionSyncMessage.MsgType.AgentRemove, OSDParser.SerializeJsonString(data)));
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
            if (te != null)
            {
                OSDMap data = new OSDMap(2);
                data["id"] = OSDUUID.FromUUID(agentID);
                data["vp"] = new OSDBinary(vp);
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

        /*
        // Should be part of the RegionSyncClient
        public string ReceiveMsg()
        {
            lock (m_outQ)
            {
                if (m_outQ.Count > 0)
                {
                    return m_outQ.Dequeue();
                }
            }
            return null;
        }
         * */
    }
}

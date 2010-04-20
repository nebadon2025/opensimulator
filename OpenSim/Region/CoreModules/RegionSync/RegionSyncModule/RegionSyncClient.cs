using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using OpenMetaverse;
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
        #region RegionSyncClient members

        // Set the addr and port of RegionSyncServer
        private IPAddress m_addr;
        private Int32 m_port;

        // A reference to the local scene
        private Scene m_scene;

        // The logfile
        private ILog m_log;

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
            m_log.Warn("[REGION SYNC CLIENT] Constructed");
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
                m_log.WarnFormat("[REGION SYNC CLIENT] [Start] Could not connect to RegionSyncServer at {0}:{1}", m_addr, m_port);
                m_log.Warn(e.Message);
            }

            m_log.WarnFormat("[REGION SYNC CLIENT] Connected to RegionSyncServer at {0}:{1}", m_addr, m_port);

            m_rcvLoop = new Thread(new ThreadStart(ReceiveLoop));
            m_rcvLoop.Name = "RegionSyncClient ReceiveLoop";
            m_log.WarnFormat("[REGION SYNC CLIENT] Starting {0} thread", m_rcvLoop.Name);
            m_rcvLoop.Start();
            m_log.Warn("[REGION SYNC CLIENT] Started");
            DoInitialSync();
        }

        // Disconnect from the RegionSyncServer and close the RegionSyncClient
        public void Stop()
        {
            m_rcvLoop.Abort();
            ShutdownClient();
        }

        private void ShutdownClient()
        {
            m_log.WarnFormat("[REGION SYNC CLIENT] Disconnected from RegionSyncServer. Shutting down.");
            m_scene.ForEachClient(delegate(IClientAPI client) { client.OnAgentUpdateRaw -= HandleAgentUpdateRaw; });
            m_client.Client.Close();
            m_client.Close();
        }

        // Listen for messages from a RegionSyncClient
        // *** This is the main thread loop for each connected client
        private void ReceiveLoop()
        {
            m_log.WarnFormat("[REGION SYNC CLIENT] Thread running: {0}", m_rcvLoop.Name);
            while (true && m_client.Connected)
            {
                RegionSyncMessage msg;
                // Try to get the message from the network stream
                try
                {
                    msg = new RegionSyncMessage(m_client.GetStream());
                    //m_log.WarnFormat("[REGION SYNC CLIENT] Received: {0}", msg.ToString());
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
                    m_log.WarnFormat("[REGION SYNC CLIENT] Encountered an exception: {0}", e.Message);
                }
            }
        }

        #region Send
        // Send a message to a single connected RegionSyncClient
        private void Send(string msg)
        {
            byte[] bmsg = System.Text.Encoding.ASCII.GetBytes(msg + System.Environment.NewLine);
            Send(bmsg);
        }

        private void Send(RegionSyncMessage msg)
        {
            Send(msg.ToBytes());
            //m_log.WarnFormat("[REGION SYNC CLIENT] Sent {0}", msg.ToString());
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
        
        // Handle an incoming message
        // TODO: This should not be synchronous with the receive!
        // Instead, handle messages from the incoming Queue
        private bool HandleMessage(RegionSyncMessage msg)
        {
            switch (msg.Type)
            {
                case RegionSyncMessage.MsgType.Terrain:
                    {
                        m_scene.Heightmap.LoadFromXmlString(Encoding.ASCII.GetString(msg.Data));
                        return HandlerSuccess(msg, "Syncrhonized terrain");
                    }
                case RegionSyncMessage.MsgType.AddObject:
                case RegionSyncMessage.MsgType.UpdateObject:
                    {
                        SceneObjectGroup sog = SceneObjectSerializer.FromXml2Format(Encoding.ASCII.GetString(msg.Data));
                        if(sog.IsDeleted)
                            return HandlerFailure(msg, String.Format("Ignoring update on deleted LocalId {0}.", sog.LocalId.ToString()));
                        if (m_scene.AddNewSceneObject(sog, true))
                        {
                            sog.ScheduleGroupForFullUpdate();
                            return HandlerSuccess(msg, String.Format("LocalId {0} added or updated.", sog.LocalId.ToString()));
                        }
                        return HandlerFailure(msg, String.Format("Could not add or update LocalId {0}.", sog.LocalId.ToString()));
                    }
                case RegionSyncMessage.MsgType.UpdateAvatarTerse:
                    {

                        OSDMap data = OSDParser.DeserializeJson(Encoding.ASCII.GetString(msg.Data)) as OSDMap;
                        if( data != null )
                        {
                            UUID agentID = data["id"].AsUUID();
                            Vector3 pos = data["pos"].AsVector3();
                            Vector3 vel = data["vel"].AsVector3();
                            Quaternion rot = data["rot"].AsQuaternion();

                            // We get the UUID of the avatar to be updated, find it in the scene
                            if (agentID != UUID.Zero)
                            {
                                ScenePresence presence = m_scene.GetScenePresence(agentID);
                                if (presence != null)
                                {
                                    presence.AbsolutePosition = pos;
                                    presence.Velocity = vel;
                                    presence.Rotation = rot;
                                    presence.SendTerseUpdateToAllClients();
                                    return HandlerSuccess(msg, String.Format("agentID {0} updated", agentID.ToString()));
                                }
                            }
                            return HandlerFailure(msg, String.Format("agentID {0} not found.", agentID.ToString()));
                        }
                        return HandlerFailure(msg, "Could not parse AgentUpdate parameters");
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
                case RegionSyncMessage.MsgType.RemoveObject:
                    {
                        OSDMap data = OSDParser.DeserializeJson(Encoding.ASCII.GetString(msg.Data)) as OSDMap;
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
                                        return HandlerSuccess(msg, String.Format("localID {0} deleted.", localID.ToString()));
                                    }
                                }
                                return HandlerFailure(msg, String.Format("ignoring delete request for non-local regionHandle {0}.", regionHandle.ToString()));
                            }
                            return HandlerFailure(msg, String.Format("localID {0} not found.", localID.ToString()));
                        }
                        return HandlerFailure(msg, "Could not parse DeleteObject parameters");
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
                        m_log.WarnFormat("[REGION SYNC CLIENT] Unsupported message type");
                        return false;
                    }
            }
        }

        private bool HandlerSuccess(RegionSyncMessage msg, string handlerMessage)
        {
            //m_log.WarnFormat("[REGION SYNC CLIENT] Handled {0}: {1}", msg.ToString(), handlerMessage);
            return true;
        }

        private bool HandlerFailure(RegionSyncMessage msg, string handlerMessage)
        {
            //m_log.WarnFormat("[REGION SYNC SERVER] Unable to handle {0}: {1}", msg.ToString(), handlerMessage);
            return false;
        }

        private void DoInitialSync()
        {
            m_scene.DeleteAllSceneObjects();
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.GetTerrain));
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.GetObjects));
            // Register for events which will be forwarded to authoritative scene
            m_scene.EventManager.OnNewClient += EventManager_OnNewClient;
        }

        public string GetServerAddressAndPort()
        {
            return m_addr.ToString() + ":" + m_port.ToString();
        }


        #region Event Handlers

        public void EventManager_OnNewClient(IClientAPI client)
        {
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
            client.OnChatFromClientRaw += HandleChatFromClientRaw;
        }

        /// <summary>
        /// This is the event handler for client movement. If a client is moving, this event is triggering.
        /// </summary>
        public void HandleAgentUpdateRaw(object sender, byte[] agentData)
        {
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.AgentUpdate, agentData));
        }

        public void HandleChatFromClientRaw(object sender, byte[] chatData)
        {
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.ChatFromClient, chatData));
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

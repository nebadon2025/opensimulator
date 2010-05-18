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
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Region.Framework.Interfaces;
using log4net;

namespace OpenSim.Region.Examples.RegionSyncModule
{
    // The RegionSyncClientView acts as a thread on the RegionSyncServer to handle incoming
    // messages from RegionSyncClients. 
    public class RegionSyncClientView
    {
        #region RegionSyncClientView members

        object stats = new object();
        private long msgsIn;
        private long msgsOut;
        private long bytesIn;
        private long bytesOut;

        // The TcpClient this view uses to communicate with its RegionSyncClient
        private TcpClient m_tcpclient;
        // Set the addr and port for TcpListener
        private IPAddress m_addr;
        private Int32 m_port;
        private int m_connection_number;
        private Scene m_scene;

        Dictionary<UUID, RegionSyncAvatar> m_syncedAvatars = new Dictionary<UUID, RegionSyncAvatar>();

        // A queue for incoming and outgoing traffic
        private OpenMetaverse.BlockingQueue<RegionSyncMessage> inbox = new OpenMetaverse.BlockingQueue<RegionSyncMessage>();

        private ILog m_log;

        private Thread m_receive_loop;
        private Thread m_handler;

        // A string of the format [REGION SYNC CLIENT VIEW #X] for use in log headers
        private string LogHeader
        {
            get { return String.Format("[REGION SYNC CLIENT VIEW #{0}]", m_connection_number); }
        }

        // A string of the format "RegionSyncClientView #X" for use in describing the object itself
        public string Description
        {
            get { return String.Format("RegionSyncClientView #{0}", m_connection_number); }
        }

        public string GetStats()
        {
            lock(stats)
                return String.Format("{0},{1},{2},{3}", msgsIn, msgsOut, bytesIn, bytesOut);
        }

        // Check if the client is connected
        public bool Connected
        { get { return m_tcpclient.Connected; } }
        #endregion

        // Constructor
        public RegionSyncClientView(int num, Scene scene, TcpClient client)
        {
            m_connection_number = num;
            m_scene = scene;
            m_tcpclient = client;
            m_addr = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
            m_port = ((IPEndPoint)client.Client.RemoteEndPoint).Port;

            m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            m_log.WarnFormat("{0} Constructed", LogHeader);

            // Create a thread for the receive loop
            m_receive_loop = new Thread(new ThreadStart(delegate() { ReceiveLoop(); }));
            m_receive_loop.Name = Description;
            m_log.WarnFormat("{0} Started thread: {1}", LogHeader, m_receive_loop.Name);
            m_receive_loop.Start();
        }

        // Stop the RegionSyncClientView, disconnecting the RegionSyncClient
        public void Shutdown()
        {
            // Abort ReceiveLoop Thread, close Socket and TcpClient
            m_receive_loop.Abort();
            m_tcpclient.Client.Close();
            m_tcpclient.Close();
        }

        // Listen for messages from a RegionSyncClient
        // *** This is the main thread loop for each connected client
        private void ReceiveLoop()
        {
            try
            {
                while (true)
                {
                    RegionSyncMessage msg = GetMessage();
                    lock (stats)
                    {
                        msgsIn++;
                        bytesIn += msg.Length;
                    }
                    HandleMessage(msg);
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat("{0} RegionSyncClient has disconnected: {1}", LogHeader, e.Message);
            }
        }

        // Get a message from the RegionSyncClient
        private RegionSyncMessage GetMessage()
        {
            // Get a RegionSyncMessager from the incoming stream
            RegionSyncMessage msg = new RegionSyncMessage(m_tcpclient.GetStream());
            //m_log.WarnFormat("{0} Received {1}", LogHeader, msg.ToString());
            return msg;
        }

        HashSet<string> exceptions = new HashSet<string>();
        private OSDMap DeserializeMessage(RegionSyncMessage msg)
        {
            OSDMap data = null;
            try
            {
                data = OSDParser.DeserializeJson(Encoding.ASCII.GetString(msg.Data)) as OSDMap;
            }
            catch(Exception e)
            {
                lock(exceptions)
                    // If this is a new message, then print the underlying data that caused it
                    if(!exceptions.Contains(e.Message))
                        m_log.Error(LogHeader + " " + Encoding.ASCII.GetString(msg.Data));
                data = null;
            }
            return data;
        }


        // Handle an incoming message
        // *** Perhaps this should not be synchronous with the receive
        // We could handle messages from an incoming Queue
        private bool HandleMessage(RegionSyncMessage msg)
        {
            //string handlerMessage = "";
            switch (msg.Type)
            {
                case RegionSyncMessage.MsgType.GetTerrain:
                    {
                        Send(new RegionSyncMessage(RegionSyncMessage.MsgType.Terrain, m_scene.Heightmap.SaveToXmlString()));
                        return HandlerSuccess(msg, "Terrain sent");
                    }
                case RegionSyncMessage.MsgType.GetObjects:
                    {
                        List<EntityBase> entities = m_scene.GetEntities();
                        foreach(EntityBase e in entities)
                        {
                            if (e is SceneObjectGroup)
                            {
                                string sogxml = SceneObjectSerializer.ToXml2Format((SceneObjectGroup)e);
                                Send(new RegionSyncMessage(RegionSyncMessage.MsgType.NewObject, sogxml));
                            }
                        }
                        return HandlerSuccess(msg, "Sent all scene objects");
                    }
                case RegionSyncMessage.MsgType.GetAvatars:
                    {
                        m_scene.ForEachScenePresence(delegate(ScenePresence presence)
                        {
                            // Let the client managers know about this avatar
                            OSDMap data = new OSDMap(1);
                            data["agentID"] = OSD.FromUUID(presence.ControllingClient.AgentId);
                            data["first"] = OSD.FromString(presence.ControllingClient.FirstName);
                            data["last"] = OSD.FromString(presence.ControllingClient.LastName);
                            data["startPos"] = OSD.FromVector3(presence.ControllingClient.StartPos);
                            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.NewAvatar, OSDParser.SerializeJsonString(data)));
                        });
                        return HandlerSuccess(msg, "Sent all scene avatars");
                    }
                case RegionSyncMessage.MsgType.AgentAdd:
                    {
                        OSDMap data = DeserializeMessage(msg);
                        if (data != null)
                        {
                            UUID agentID = data["agentID"].AsUUID();
                            string first = data["first"].AsString();
                            string last = data["last"].AsString();
                            Vector3 startPos = data["startPos"].AsVector3();

                            if (agentID != null && first != null && last != null && startPos != null)
                            {
                                RegionSyncAvatar av = new RegionSyncAvatar(m_scene, agentID, first, last, startPos);
                                lock (m_syncedAvatars)
                                {
                                    if (m_syncedAvatars.ContainsKey(agentID))
                                    {
                                        return HandlerFailure(msg, String.Format( "Attempted to add duplicate avatar with agentID {0}", agentID));
                                    }
                                    m_syncedAvatars.Add(agentID, av);
                                }
                                m_scene.AddNewClient(av);
                                return HandlerSuccess(msg, String.Format("Handled AddAgent for UUID {0}", agentID));
                            }
                        }
                        return HandlerFailure(msg, "Could not deserialize JSON data.");
                        
                    }
                case RegionSyncMessage.MsgType.AgentUpdate:
                    {
                        int len = 0;
                        AgentUpdatePacket.AgentDataBlock agentData = new AgentUpdatePacket.AgentDataBlock();
                        agentData.FromBytes(msg.Data, ref len);

                        UUID agentID = agentData.AgentID;

                        RegionSyncAvatar av;
                        bool found;
                        lock (m_syncedAvatars)
                        {
                            found = m_syncedAvatars.TryGetValue(agentData.AgentID, out av);
                        }
                        if(!found)
                            return HandlerFailure(msg, String.Format("Received agent update for non-existent avatar with UUID {0}", agentData.AgentID));

                        AgentUpdateArgs arg = new AgentUpdateArgs();
                        arg.AgentID = agentData.AgentID;
                        arg.BodyRotation = agentData.BodyRotation;
                        arg.CameraAtAxis = agentData.CameraAtAxis;
                        arg.CameraCenter = agentData.CameraCenter;
                        arg.CameraLeftAxis = agentData.CameraLeftAxis;
                        arg.CameraUpAxis = agentData.CameraUpAxis;
                        arg.ControlFlags = agentData.ControlFlags;
                        arg.Far = agentData.Far;
                        arg.Flags = agentData.Flags;
                        arg.HeadRotation = agentData.HeadRotation;
                        arg.SessionID = agentData.SessionID;
                        arg.State = agentData.State;

                        if( av.AgentUpdate(arg) )
                            return HandlerSuccess(msg, String.Format("Handled AgentUpdate for UUID {0}", agentID));
                        else
                            return HandlerFailure(msg, String.Format("Could not handle AgentUpdate UUID {0}", agentID));
                    }
                case RegionSyncMessage.MsgType.AgentRemove:
                    {
                        OSDMap data = DeserializeMessage(msg);
                        if (data != null)
                        {
                            UUID agentID = data["agentID"].AsUUID();

                            if (agentID != null && agentID != UUID.Zero)
                            {
                                lock (m_syncedAvatars)
                                {
                                    if (m_syncedAvatars.ContainsKey(agentID))
                                    {
                                        m_syncedAvatars.Remove(agentID);
                                        ScenePresence presence;
                                        if (m_scene.TryGetScenePresence(agentID, out presence))
                                        {
                                            string name = presence.Name;
                                            m_scene.RemoveClient(agentID);
                                            return HandlerSuccess(msg, String.Format("Agent \"{0}\" ({1}) was removed from scene.", name, agentID));
                                        }
                                        else
                                        {
                                            return HandlerFailure(msg, String.Format("Agent {0} not found in the scene.", agentID));
                                        }
                                    }
                                    else
                                    {
                                        return HandlerFailure(msg, String.Format("Agent {0} not in the list of synced avatars.", agentID));
                                    }
                                }
                            }
                        }
                        return HandlerFailure(msg, "Could not deserialize JSON data.");

                    }
                case RegionSyncMessage.MsgType.AvatarAppearance:
                    {
                        OSDMap data = DeserializeMessage(msg);
                        if (data != null)
                        {
                            UUID agentID = data["id"].AsUUID();

                            if (agentID != null && agentID != UUID.Zero)
                            {
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
                                    return HandlerDebug(msg, String.Format("Agent \"{0}\" ({1}) updated their appearance.", name, agentID));
                                }
                                else
                                {
                                    return HandlerFailure(msg, String.Format("Agent {0} not found in the scene.", agentID));
                                }
                            }
                        }
                        return HandlerFailure(msg, "Could not deserialize JSON data.");

                    }
                default:
                    {
                        m_log.WarnFormat("{0} Unable to handle unsupported message type", LogHeader);
                        return false;
                    }
            }
        }

        private bool HandlerDebug(RegionSyncMessage msg, string handlerMessage)
        {
            m_log.WarnFormat("{0} DBG ({1}): {2}", LogHeader, msg.ToString(), handlerMessage);
            return true;
        }

        private bool HandlerSuccess(RegionSyncMessage msg, string handlerMessage)
        {
            //m_log.WarnFormat("{0} Handled {1}: {2}", LogHeader, msg.ToString(), handlerMessage);
            return true;
        }

        private bool HandlerFailure(RegionSyncMessage msg, string handlerMessage)
        {
            m_log.WarnFormat("{0} Unable to handle {1}: {2}", LogHeader, msg.ToString(), handlerMessage);
            return false;
        }

        public void Send(RegionSyncMessage msg)
        {
            Send(msg.ToBytes());
            //m_log.WarnFormat("{0} Sent {1}", LogHeader, msg.ToString());
        }

        private void Send(byte[] data)
        {
            if (m_tcpclient.Connected)
            {
                try
                {
                    lock (stats)
                    {
                        msgsOut++;
                        bytesOut += data.Length;
                    }
                    m_tcpclient.GetStream().Write(data, 0, data.Length);
                }
                catch (IOException e)
                {
                    m_log.WarnFormat("{0} RegionSyncClient has disconnected.", LogHeader);
                }
            }
        }

        #region crud
        // Should be part of the RegionSyncClient
        /*
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
        #endregion

    }
}

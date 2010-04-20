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
        // The TcpClient this view uses to communicate with its RegionSyncClient
        private TcpClient m_tcpclient;
        // Set the addr and port for TcpListener
        private IPAddress m_addr;
        private Int32 m_port;
        private int m_connection_number;
        private Scene m_scene;

        Dictionary<UUID, RegionSyncAvatar> m_syncedAvatars = new Dictionary<UUID, RegionSyncAvatar>();

        // A queue for incoming and outgoing traffic
        private Queue<string> m_inQ = new Queue<string>();
        private Queue<string> m_outQ = new Queue<string>();

        private ILog m_log;

        private Thread m_receive_loop;
        
        // The last time the entire region was sent to this RegionSyncClient
        private long m_archive_time;

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
                    HandleMessage(msg);
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat("{0} RegionSyncClient has disconnected.", LogHeader);
            }
        }

        // Get a message from the RegionSyncClient
        private RegionSyncMessage GetMessage()
        {
            // Get a RegionSyncMessager from the incoming stream
            RegionSyncMessage msg = new RegionSyncMessage(m_tcpclient.GetStream());
            m_log.WarnFormat("{0} Received {1}", LogHeader, msg.ToString());
            return msg;
        }

        // Handle an incoming message
        // *** Perhaps this should not be synchronous with the receive
        // We could handle messages from an incoming Queue
        private bool HandleMessage(RegionSyncMessage msg)
        {
            //string handlerMessage = "";
            switch (msg.Type)
            {
                case RegionSyncMessage.MsgType.GetObjects:
                    {
                        List<EntityBase> entities = m_scene.GetEntities();
                        foreach(EntityBase e in entities)
                        {
                            if (e is SceneObjectGroup)
                            {
                                string sogxml = SceneObjectSerializer.ToXml2Format((SceneObjectGroup)e);
                                Send(new RegionSyncMessage(RegionSyncMessage.MsgType.AddObject, sogxml));
                            }
                        }
                        return HandlerSuccess(msg, "Sent all scene objects");
                    }
                case RegionSyncMessage.MsgType.GetTerrain:
                    {
                        Send(new RegionSyncMessage(RegionSyncMessage.MsgType.Terrain, m_scene.Heightmap.SaveToXmlString()));
                        return HandlerSuccess(msg, "Terrain sent");
                    }
                case RegionSyncMessage.MsgType.AgentAdd:
                    {
                        OSDMap data = OSDParser.DeserializeJson(Encoding.ASCII.GetString(msg.Data)) as OSDMap;
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
                        return HandlerFailure(msg, "Could not parse AddAgent parameters");
                        
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
                default:
                    {
                        m_log.WarnFormat("{0} Unable to handle unsupported message type", LogHeader);
                        return false;
                    }
            }
        }

        private bool HandlerSuccess(RegionSyncMessage msg, string handlerMessage)
        {
            m_log.WarnFormat("{0} Handled {1}: {2}", LogHeader, msg.ToString(), handlerMessage);
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

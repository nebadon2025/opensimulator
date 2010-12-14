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
using OpenSim.Region.Physics.Manager;
using log4net;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{



    //KittyL: NOTE -- We need to define an interface for all actors to connect into the Scene,
    //        e.g. IActorConnector, that runs on the Scene side, processes messages from actors,
    //        and apply Scene/Object operations.

    // The SceneToPhysEngineConnector acts as a thread on the RegionSyncServer to handle incoming
    // messages from PhysEngineToSceneConnectors that run on Physics Engines. It connects the 
    // authoratative Scene with remote script engines.
    public class SceneToPhysEngineConnector
    {
        #region SceneToPhysEngineConnector members

        object stats = new object();
        private DateTime lastStatTime;
        private long msgsIn;
        private long msgsOut;
        private long bytesIn;
        private long bytesOut;
        private long pollBlocks;
        private int lastTotalCount;
        private int lastLocalCount;
        private int lastRemoteCount;

        private int msgCount = 0;

        // The TcpClient this view uses to communicate with its RegionSyncClient
        private TcpClient m_tcpclient;
        // Set the addr and port for TcpListener
        private IPAddress m_addr;
        private Int32 m_port;
        private int m_connection_number;
        private Scene m_scene;

        object m_syncRoot = new object();
        Dictionary<UUID, RegionSyncAvatar> m_syncedAvatars = new Dictionary<UUID, RegionSyncAvatar>();

        // A queue for incoming and outgoing traffic
        private OpenMetaverse.BlockingQueue<RegionSyncMessage> inbox = new OpenMetaverse.BlockingQueue<RegionSyncMessage>();
        private OpenMetaverse.BlockingQueue<RegionSyncMessage> outbox = new OpenMetaverse.BlockingQueue<RegionSyncMessage>();

        private ILog m_log;

        private Thread m_receive_loop;
        private string m_regionName;

        private SceneToPhysEngineSyncServer m_syncServer = null;

        // A string of the format [REGION SYNC SCRIPT API (regionname)] for use in log headers
        private string LogHeader
        {
            get
            {
                if (m_regionName == null)
                    return String.Format("[SceneToPhysEngineConnector #{0}]", m_connection_number);
                return String.Format("[SceneToPhysEngineConnector #{0} ({1:10})]", m_connection_number, m_regionName);
            }
        }

        // A string of the format "RegionSyncClientView #X" for use in describing the object itself
        public string Description
        {
            get
            {
                if (m_regionName == null)
                    return String.Format("RegionSyncPhysAPI #{0}", m_connection_number);
                return String.Format("RegionSyncPhysAPI #{0} ({1:10})", m_connection_number, m_regionName);
            }
        }

        public int ConnectionNum
        {
            get { return m_connection_number; }
        }

        public string GetStats()
        {
            string ret;
            //lock (m_syncRoot)
            //    syncedAvCount = m_syncedAvatars.Count;
            lock (stats)
            {
                double secondsSinceLastStats = DateTime.Now.Subtract(lastStatTime).TotalSeconds;
                lastStatTime = DateTime.Now;

                // ret = String.Format("[{0,4}/{1,4}], [{2,4}/{3,4}], [{4,4}/{5,4}], [{6,4} ({7,4})], [{8,8} ({9,8:00.00})], [{10,4} ({11,4})], [{12,8} ({13,8:00.00})], [{14,8} ({15,4}]",
                ret = String.Format("[{0,4}/{1,4}], [{2,6}/{3,6}], [{4,4}/{5,4}], [{6,6} ({7,6})], [{8,4} ({9,4})]",
                    //lastTotalCount, totalAvCount, // TOTAL AVATARS
                    //lastLocalCount, syncedAvCount, // LOCAL TO THIS CLIENT VIEW
                    //lastRemoteCount, totalAvCount - syncedAvCount, // REMOTE (SHOULD = TOTAL - LOCAL)
                    msgsIn, (int)(msgsIn / secondsSinceLastStats),
                    bytesIn, 8 * (bytesIn / secondsSinceLastStats / 1000000), // IN
                    msgsOut, (int)(msgsOut / secondsSinceLastStats),
                    bytesOut, 8 * (bytesOut / secondsSinceLastStats / 1000000), // OUT
                    pollBlocks, (int)(pollBlocks / secondsSinceLastStats)); // NUMBER OF TIMES WE BLOCKED WRITING TO SOCKET

                msgsIn = msgsOut = bytesIn = bytesOut = pollBlocks = 0;
            }
            return ret;
        }

        // Check if the client is connected
        public bool Connected
        { get { return m_tcpclient.Connected; } }

        //private int QuarkInfo.SizeX;
        //private int QuarkInfo.SizeY;
        //private List<QuarkInfo> m_quarkSubscriptions;
        Dictionary<string, QuarkInfo> m_quarkSubscriptions;
        public Dictionary<string, QuarkInfo> QuarkSubscriptionList
        {
            get { return m_quarkSubscriptions; }
        }



        #endregion

        // Constructor
        public SceneToPhysEngineConnector(int num, Scene scene, TcpClient client, SceneToPhysEngineSyncServer syncServer)
        {
            m_connection_number = num;
            m_scene = scene;
            m_tcpclient = client;
            m_addr = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
            m_port = ((IPEndPoint)client.Client.RemoteEndPoint).Port;
            m_syncServer = syncServer;

            //QuarkInfo.SizeX = quarkSizeX;
            //QuarkInfo.SizeY = quarkSizeY;

            m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            //m_log.WarnFormat("{0} Constructed", LogHeader);

            //Register for events from Scene.EventManager
            //m_scene.EventManager.OnRezScript += SEConnectorOnRezScript;
            //m_scene.EventManager.OnScriptReset += SEConnectorOnScriptReset;
            //m_scene.EventManager.OnUpdateScript += SEConnectorOnUpdateScript;
            // Create a thread for the receive loop
            m_receive_loop = new Thread(new ThreadStart(delegate() { ReceiveLoop(); }));
            m_receive_loop.Name = Description;
            //m_log.WarnFormat("{0} Started thread: {1}", LogHeader, m_receive_loop.Name);
            m_receive_loop.Start();

            //tell the remote script engine about the locX, locY of this authoritative scene
            // SendSceneLoc();
            m_log.DebugFormat("{0}: SceneToPhysEngineConnector initialized", LogHeader);
        }

        // Stop the listening thread, disconnecting the RegionSyncPhysEngine
        public void Shutdown()
        {
            m_syncServer.RemoveSyncedPhysEngine(this);
            // m_scene.EventManager.OnChatFromClient -= EventManager_OnChatFromClient;
            // Abort ReceiveLoop Thread, close Socket and TcpClient
            m_receive_loop.Abort();
            m_tcpclient.Client.Close();
            m_tcpclient.Close();

            //m_scene.EventManager.OnRezScript -= SEConnectorOnRezScript;
            //m_scene.EventManager.OnScriptReset -= SEConnectorOnScriptReset;
            //m_scene.EventManager.OnUpdateScript -= SEConnectorOnUpdateScript;
        }

        #region Send/Receive messages to/from the remote Physics Engine

        // Listen for messages from a RegionSyncClient
        // *** This is the main thread loop for each connected client
        private void ReceiveLoop()
        {
            //m_scene.EventManager.OnChatFromClient += new EventManager.ChatFromClientEvent(EventManager_OnChatFromClient);

            // Reset stats and time
            lastStatTime = DateTime.Now;
            msgsIn = msgsOut = bytesIn = bytesOut = 0;

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
                    lock (m_syncRoot)
                        HandleMessage(msg);
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat("{0}: has disconnected: {1}", LogHeader, e.Message);
            }
            Shutdown();
        }

        // Get a message from the RegionSyncClient
        private RegionSyncMessage GetMessage()
        {
            // Get a RegionSyncMessager from the incoming stream
            RegionSyncMessage msg = new RegionSyncMessage(m_tcpclient.GetStream());
            //m_log.WarnFormat("{0} Received {1}", LogHeader, msg.ToString());
            return msg;
        }

        // Handle an incoming message
        // *** Perhaps this should not be synchronous with the receive
        // We could handle messages from an incoming Queue
        private void HandleMessage(RegionSyncMessage msg)
        {
            msgCount++;
            //string handlerMessage = "";
            switch (msg.Type)
            {
                case RegionSyncMessage.MsgType.ActorStop:
                    {
                        Shutdown();
                    }
                    return;
                case RegionSyncMessage.MsgType.LoadBalanceRequest:
                    {
                        m_syncServer.HandleLoadBalanceRequest(this);
                        return;
                    }
                    
                case RegionSyncMessage.MsgType.ActorStatus:
                    {
                        string status = Encoding.ASCII.GetString(msg.Data, 0, msg.Length);
                        ActorStatus actorStatus = (ActorStatus)Convert.ToInt32(status);
                        if (actorStatus == ActorStatus.Sync)
                        {
                            m_log.Debug(LogHeader + ": received ActorStatus " + actorStatus.ToString());
                            m_syncServer.AddSyncedPhysEngine(this);
                        }
                        else
                        {
                            m_log.Warn(LogHeader + ": not supposed to received RegionSyncMessage.MsgType.ActorStatus==" + status.ToString());
                        }
                        return;
                    }

                case RegionSyncMessage.MsgType.PhysTerseUpdate:
                    {
                        HandlePhysTerseUpdate(msg);
                        return;
                    }
                case RegionSyncMessage.MsgType.PhysOutOfBounds:
                    {
                        HandlePhysOutOfBounds(msg);
                        return;
                    }
                case RegionSyncMessage.MsgType.PhysCollisionUpdate:
                    {
                        HandlePhysCollisionUpdate(msg);
                        return;
                    }
                case RegionSyncMessage.MsgType.PhysUpdateAttributes:
                    {
                        HandlePhysUpdateAttributes(msg);
                        return;
                    }
                default:
                    {
                        m_log.WarnFormat("{0} Unable to handle unsupported message type", LogHeader);
                        return;
                    }
            }
        }

        private void HandlePhysTerseUpdate(RegionSyncMessage msg)
        {
            // TODO: 
            return;
        }

        private void HandlePhysOutOfBounds(RegionSyncMessage msg)
        {
            // TODO: 
            return;
        }

        private void HandlePhysCollisionUpdate(RegionSyncMessage msg)
        {
            // TODO: 
            return;
        }

        /// <summary>
        /// The physics engine has some updates to the attributes. Unpack the parameters, find the
        /// correct PhysicsActor and plug in the new values;
        /// </summary>
        /// <param name="msg"></param>
        private void HandlePhysUpdateAttributes(RegionSyncMessage msg)
        {
            // TODO: 
            OSDMap data = RegionSyncUtil.DeserializeMessage(msg, LogHeader);
            try
            {
                uint localID = data["localID"].AsUInteger();
                // m_log.DebugFormat("{0}: received PhysUpdateAttributes for {1}", LogHeader, localID);
                PhysicsActor pa = FindPhysicsActor(localID);
                if (pa != null)
                {
                    pa.Size = data["size"].AsVector3();
                    pa.Position = data["position"].AsVector3();
                    pa.Force = data["force"].AsVector3();
                    pa.Velocity = data["velocity"].AsVector3();
                    pa.Torque = data["torque"].AsVector3();
                    pa.Orientation = data["orientantion"].AsQuaternion();
                    pa.IsPhysical = data["isPhysical"].AsBoolean();  // receive??
                    pa.Flying = data["flying"].AsBoolean();      // receive??
                    pa.Kinematic = data["kinematic"].AsBoolean();    // receive??
                    pa.Buoyancy = (float)(data["buoyancy"].AsReal());
                }
                else
                {
                    m_log.WarnFormat("{0}: attribute update for unknown localID {1}", LogHeader, localID);
                    return;
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat("{0}: EXCEPTION processing UpdateAttributes: {1}", LogHeader, e);
                return;
            }
            return;
        }

        // Find the physics actor whether it is an object or a scene presence
        private PhysicsActor FindPhysicsActor(uint localID)
        {
            SceneObjectPart sop = m_scene.GetSceneObjectPart(localID);
            if (sop != null)
            {
                return sop.PhysActor;
            }
            ScenePresence sp = m_scene.GetScenePresence(localID);
            if (sp != null)
            {
                return sp.PhysicsActor;
            }
            return null;
        }

        public void SendPhysUpdateAttributes(PhysicsActor pa)
        {
            // m_log.DebugFormat("{0}: sending PhysUpdateAttributes for {1}", LogHeader, pa.LocalID);
            OSDMap data = new OSDMap(9);
            data["localID"] = OSD.FromUInteger(pa.LocalID);
            data["size"] = OSD.FromVector3(pa.Size);
            data["position"] = OSD.FromVector3(pa.Position);
            data["force"] = OSD.FromVector3(pa.Force);
            data["velocity"] = OSD.FromVector3(pa.Velocity);
            data["torque"] = OSD.FromVector3(pa.Torque);
            data["orientation"] = OSD.FromQuaternion(pa.Orientation);
            data["isPhysical"] = OSD.FromBoolean(pa.IsPhysical);
            data["flying"] = OSD.FromBoolean(pa.Flying);
            data["buoyancy"] = OSD.FromReal(pa.Buoyancy);

            RegionSyncMessage rsm = new RegionSyncMessage(RegionSyncMessage.MsgType.PhysUpdateAttributes, 
                                                                OSDParser.SerializeJsonString(data));
            Send(rsm);
            return;
        }

        //For simplicity, we assume the subscription sent by PhysEngine is legistimate (no overlapping with other script engines, etc)
        private void HandleQuarkSubscription(RegionSyncMessage msg)
        {
            string quarkString = Encoding.ASCII.GetString(msg.Data, 0, msg.Length);
            m_log.Debug(LogHeader + ": received quark-string: " + quarkString);

            List<string> quarkStringList = RegionSyncUtil.QuarkStringToStringList(quarkString);
            //m_quarkSubscriptions = RegionSyncUtil.GetQuarkInfoList(quarkStringList, QuarkInfo.SizeX, QuarkInfo.SizeY);
            List<QuarkInfo> quarkList = RegionSyncUtil.GetQuarkInfoList(quarkStringList);
            m_syncServer.RegisterQuarkSubscription(quarkList, this);

            m_quarkSubscriptions = new Dictionary<string, QuarkInfo>();
            foreach (QuarkInfo quark in quarkList)
            {
                m_quarkSubscriptions.Add(quark.QuarkStringRepresentation, quark);
            }
        }

        private RegionSyncMessage PrepareObjectUpdateMessage(RegionSyncMessage.MsgType msgType, SceneObjectGroup sog)
        {
            OSDMap data = new OSDMap(3);
            data["locX"] = OSD.FromUInteger(m_scene.RegionInfo.RegionLocX);
            data["locY"] = OSD.FromUInteger(m_scene.RegionInfo.RegionLocY);
            string sogxml = SceneObjectSerializer.ToXml2Format(sog);
            data["sogXml"] = OSD.FromString(sogxml);

            RegionSyncMessage rsm = new RegionSyncMessage(msgType, OSDParser.SerializeJsonString(data));
            return rsm;
        }

        private void SendSceneLoc()
        {
            uint locX = m_scene.RegionInfo.RegionLocX;
            uint locY = m_scene.RegionInfo.RegionLocY;

            OSDMap data = new OSDMap(2);
            data["locX"] = OSD.FromUInteger(locX);
            data["locY"] = OSD.FromUInteger(locY);
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.SceneLocation, OSDParser.SerializeJsonString(data)));
        }

        public void Send(RegionSyncMessage msg)
        {
            if (msg.Type == RegionSyncMessage.MsgType.AvatarAppearance)
                m_log.WarnFormat("{0} Sending AvatarAppearance to client manager", LogHeader);

            Send(msg.ToBytes());
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
                    m_tcpclient.GetStream().BeginWrite(data, 0, data.Length, ar =>
                    {
                        if (m_tcpclient.Connected)
                        {
                            try
                            {
                                m_tcpclient.GetStream().EndWrite(ar);
                            }
                            catch (Exception)
                            {
                                m_log.WarnFormat("{0} Write to output stream failed", LogHeader);
                            }
                        }
                    }, null);
                }
                catch (IOException)
                {
                    m_log.WarnFormat("{0} Physics Engine has disconnected.", LogHeader);
                }
            }
            else
            {
                m_log.DebugFormat("{0} Attempt to send with no connection", LogHeader);
            }

        }

        public void SendObjectUpdate(RegionSyncMessage.MsgType msgType, SceneObjectGroup sog)
        {
            Send(PrepareObjectUpdateMessage(msgType, sog));
        }

        #endregion Send/Receive messages to/from the remote Physics Engine

        #region Spacial query functions (should be eventually implemented within Scene)

        //This should be a function of Scene, but since we don't have the quark concept in Scene yet, 
        //for now we implement it here.
        //Ideally, for quark based space representation, the Scene has a list of quarks, and each quark points
        //to a list of objects within that quark. Then it's much easier to return the right set of objects within
        //a certain space. (Or use DB that supports spatial queries.)
        List<SceneObjectGroup> GetObjectsInGivenSpace(Scene scene, Dictionary<string, QuarkInfo> quarkSubscriptions)
        {
            List<EntityBase> entities = m_scene.GetEntities();
            List<SceneObjectGroup> sogList = new List<SceneObjectGroup>();
            foreach (EntityBase e in entities)
            {
                if (e is SceneObjectGroup)
                {
                    SceneObjectGroup sog = (SceneObjectGroup)e;
                    string quarkID = RegionSyncUtil.GetQuarkIDByPosition(sog.AbsolutePosition);
                    if (m_quarkSubscriptions.ContainsKey(quarkID))
                    {
                        sogList.Add(sog);
                    }
                }
            }

            return sogList;
        }

        #endregion 

        #region Load balancing functions
        public void SendLoadBalanceRejection(string response)
        {
            RegionSyncMessage msg = new RegionSyncMessage(RegionSyncMessage.MsgType.LoadBalanceRejection, response);
            Send(msg);
        }
        #endregion  
    }
}

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
    public class SceneToPhysEngineConnector : ISyncStatistics
    {
        #region SceneToPhysEngineConnector members

        object stats = new object();
        private DateTime lastStatTime;
        private long msgsIn;
        private long msgsOut;
        private long bytesIn;
        private long bytesOut;
        private long pollBlocks;

        private int msgCount = 0;

        // The TcpClient this view uses to communicate with its RegionSyncClient
        private TcpClient m_tcpclient;
        // Set the addr and port for TcpListener
        private IPAddress m_addr;
        private Int32 m_port;
        private static int m_connection_number = 0;
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

        public string StatisticIdentifier()
        {
            return "SceneToPhysEngineConnector" + ConnectionNum.ToString();
        }

        public string StatisticLine(bool clearFlag)
        {
            string ret = "";
            lock (stats)
            {
                double secondsSinceLastStats = DateTime.Now.Subtract(lastStatTime).TotalSeconds;
                lastStatTime = DateTime.Now;

                ret = String.Format("{0},{1},{2},{3},{4}",
                    msgsIn, msgsOut, bytesIn, bytesOut, pollBlocks
                    );
                if (clearFlag)
                    msgsIn = msgsOut = bytesIn = bytesOut = pollBlocks = 0;
            }
            return ret;
        }

        public string StatisticTitle()
        {
            return "msgsIn,msgsOut,bytesIn,bytesOut,pollBlocks";
        }

        // Check if the client is connected
        public bool Connected
        { get { return m_tcpclient.Connected; } }


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

            SyncStatisticCollector.Register(this);

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
            SceneToPhysEngineSyncServer.PhysLogMessage(true, msg);
            msgCount++;
            //string handlerMessage = "";
            switch (msg.Type)
            {
                case RegionSyncMessage.MsgType.ActorStop:
                    {
                        Shutdown();
                    }
                    return;
                    /*
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
                    */

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
            OSDMap data = RegionSyncUtil.DeserializeMessage(msg, LogHeader);
            try
            {
                UUID uuid = data["uuid"].AsUUID();
                // m_log.DebugFormat("{0}: received PhysUpdateAttributes for {1}", LogHeader, uuid);
                PhysicsActor pa = FindPhysicsActor(uuid);
                if (pa != null)
                {
                    pa.RequestPhysicsterseUpdate();
                }
                else
                {
                    m_log.WarnFormat("{0}: terse update for unknown uuid {1}", LogHeader, uuid);
                    return;
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat("{0}: EXCEPTION processing PhysTerseUpdate: {1}", LogHeader, e);
                return;
            }
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
            OSDMap data = RegionSyncUtil.DeserializeMessage(msg, LogHeader);
            try
            {
                UUID uuid = data["uuid"].AsUUID();
                string actorID = data["actorID"].AsString();
                // m_log.DebugFormat("{0}: received PhysUpdateAttributes for {1}", LogHeader, uuid);
                PhysicsActor pa = FindPhysicsActor(uuid);
                if (pa != null)
                {
                    if (pa.PhysicsActorType == (int)ActorTypes.Prim)
                    {
                        m_log.WarnFormat("{0}: HandlePhysUpdateAttributes for an prim: {1}", LogHeader, pa.UUID);
                    }
                    pa.Size = data["size"].AsVector3();
                    pa.Position = data["position"].AsVector3();
                    pa.Force = data["force"].AsVector3();
                    pa.Velocity = data["velocity"].AsVector3();
                    pa.RotationalVelocity = data["rotationalVelocity"].AsVector3();
                    pa.Acceleration = data["acceleration"].AsVector3();
                    pa.Torque = data["torque"].AsVector3();
                    pa.Orientation = data["orientation"].AsQuaternion();
                    pa.IsPhysical = data["isPhysical"].AsBoolean();  // receive??
                    pa.Flying = data["flying"].AsBoolean();      // receive??
                    pa.Kinematic = data["kinematic"].AsBoolean();    // receive??
                    pa.Buoyancy = (float)(data["buoyancy"].AsReal());
                    pa.CollidingGround = data["isCollidingGround"].AsBoolean();
                    pa.IsColliding = data["isCollidingGround"].AsBoolean();
                    pa.ChangingActorID = actorID;

                    pa.RequestPhysicsterseUpdate(); // tell the system the values have changed
                }
                else
                {
                    m_log.WarnFormat("{0}: attribute update for unknown uuid {1}", LogHeader, uuid);
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
        private PhysicsActor FindPhysicsActor(UUID uuid)
        {
            SceneObjectPart sop = m_scene.GetSceneObjectPart(uuid);
            if (sop != null)
            {
                return sop.PhysActor;
            }
            ScenePresence sp = m_scene.GetScenePresence(uuid);
            if (sp != null)
            {
                return sp.PhysicsActor;
            }
            return null;
        }

        public void SendPhysUpdateAttributes(PhysicsActor pa)
        {
            // m_log.DebugFormat("{0}: sending PhysUpdateAttributes for {1}", LogHeader, pa.UUID);
            if (pa.PhysicsActorType == (int)ActorTypes.Prim)
            {
                m_log.WarnFormat("{0}: SendPhysUpdateAttributes for an prim: {1}", LogHeader, pa.UUID);
            }
            OSDMap data = new OSDMap(15);
            data["time"] = OSD.FromString(DateTime.Now.ToString("yyyyMMddHHmmssfff"));
            data["localID"] = OSD.FromUInteger(pa.LocalID);
            data["uuid"] = OSD.FromUUID(pa.UUID);
            data["actorID"] = OSD.FromString(RegionSyncServerModule.ActorID);
            data["size"] = OSD.FromVector3(pa.Size);
            data["position"] = OSD.FromVector3(pa.Position);
            data["force"] = OSD.FromVector3(pa.Force);
            data["velocity"] = OSD.FromVector3(pa.Velocity);
            data["rotationalVelocity"] = OSD.FromVector3(pa.RotationalVelocity);
            data["acceleration"] = OSD.FromVector3(pa.Acceleration);
            data["torque"] = OSD.FromVector3(pa.Torque);
            data["orientation"] = OSD.FromQuaternion(pa.Orientation);
            data["isPhysical"] = OSD.FromBoolean(pa.IsPhysical);
            data["flying"] = OSD.FromBoolean(pa.Flying);
            data["buoyancy"] = OSD.FromReal(pa.Buoyancy);
            // data["isColliding"] = OSD.FromBoolean(pa.IsColliding);
            // data["isCollidingGround"] = OSD.FromBoolean(pa.CollidingGround);

            RegionSyncMessage rsm = new RegionSyncMessage(RegionSyncMessage.MsgType.PhysUpdateAttributes, 
                                                                OSDParser.SerializeJsonString(data));
            Send(rsm);
            return;
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

        #region Load balancing functions
        /*
        public void SendLoadBalanceRejection(string response)
        {
            RegionSyncMessage msg = new RegionSyncMessage(RegionSyncMessage.MsgType.LoadBalanceRejection, response);
            Send(msg);
        }
        */
        #endregion  
    }
}

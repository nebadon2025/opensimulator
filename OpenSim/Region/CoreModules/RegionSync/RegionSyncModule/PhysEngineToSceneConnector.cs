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
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Client;
using OpenSim.Region.Physics.Manager;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes.Types;
using log4net;

using Nini.Config;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{

    // The RegionSyncPhysEngine has a receive thread to process messages from the RegionSyncServer.
    // It is the client side of the synchronization channel, and send to and receive updates from the 
    // Auth. Scene. The server side thread handling the sync channel is implemented in RegionSyncScriptAPI.cs.
    // 
    // The current implementation is very similar to RegionSyncClient.
    // TODO: eventually the same RegionSyncSceneAPI should handle all traffic from different actors, e.g. 
    //       using a pub/sub model.
    public class PhysEngineToSceneConnector
    {
        #region PhysEngineToSceneConnector members

        // Set the addr and port of RegionSyncServer
        private IPAddress m_addr;
        private string m_addrString;
        private Int32 m_port;

        // A reference to the local scene
        private Scene m_validLocalScene;

        // The avatars added to this client manager for clients on other client managers
        object m_syncRoot = new object();

        // The logfile
        private ILog m_log;

        private string LogHeader = "[PHYSICS ENGINE TO SCENE CONNECTOR]";

        // The listener and the thread which listens for connections from client managers
        private Thread m_rcvLoop;

        // The client connection to the RegionSyncServer
        private TcpClient m_client = new TcpClient();

        // The queue of incoming messages which need handling
        //private Queue<string> m_inQ = new Queue<string>();

        //KittyL: added to identify different actors
        private ActorType m_actorType = ActorType.PhysicsEngine;

        private bool m_debugWithViewer = false;
        private long m_messagesSent = 0;
        private long m_messagesReceived = 0;

        private IConfig m_sysConfig;

        //members for load balancing purpose
        //private TcpClient m_loadMigrationSouceEnd = null;
        //private LoadMigrationEndPoint m_loadMigrationSouceEnd = null;
        private Thread m_loadMigrationSrcRcvLoop;
        //private LoadMigrationListener m_loadMigrationListener = null;

        //List of queued messages, when the space that the updated object is located is being migrated
        private List<RegionSyncMessage> m_updateMsgQueue = new List<RegionSyncMessage>();

        #endregion


        // Constructor
        public PhysEngineToSceneConnector(Scene validLocalScene, string addr, int port, bool debugWithViewer, 
                            IConfig sysConfig)
        {
            m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            m_validLocalScene = validLocalScene;
            m_addr = IPAddress.Parse(addr);
            m_addrString = addr;
            m_port = port;
            m_debugWithViewer = debugWithViewer;
            m_sysConfig = sysConfig;

            SceneToPhysEngineSyncServer.logEnabled = m_sysConfig.GetBoolean("PhysLogEnabled", false);
            SceneToPhysEngineSyncServer.logDir = m_sysConfig.GetString("PhysLogDir", ".");
        }
         
        // Start the RegionSyncPhysEngine client thread
        public bool Start()
        {
            if (EstablishConnection())
            {
                StartStateSync();
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool EstablishConnection()
        {
            if (m_client.Connected)
            {
                m_log.Warn(LogHeader + ": already connected");
                return false;
            }

            try
            {
                m_client.Connect(m_addr, m_port);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("{0} [Start] Could not connect to SceneToPhysEngineSyncServer at {1}:{2}", LogHeader, m_addr, m_port);
                m_log.Warn(e.Message);
                return false;
            }

            m_log.WarnFormat("{0} Connected to SceneToPhysEngineSyncServer at {1}:{2}", LogHeader, m_addr, m_port);

            m_rcvLoop = new Thread(new ThreadStart(ReceiveLoop));
            m_rcvLoop.Name = "PhysEngineToSceneConnector ReceiveLoop";
            m_log.WarnFormat("{0} Starting {1} thread", LogHeader, m_rcvLoop.Name);
            m_rcvLoop.Start();
            return true;
        }

        private void StartStateSync()
        {
            RegionSyncMessage msg = new RegionSyncMessage(RegionSyncMessage.MsgType.ActorStatus, Convert.ToString((int)ActorStatus.Sync));
            Send(msg);
            Thread.Sleep(100);
            DoInitialSync();
        }

        public void RegisterIdle()
        {
            EstablishConnection();
            RegionSyncMessage msg = new RegionSyncMessage(RegionSyncMessage.MsgType.ActorStatus, Convert.ToString((int)ActorStatus.Idle));
            Send(msg);
        }

        private void DoInitialSync()
        {
        }

        // Disconnect from the RegionSyncServer and close client thread
        public void Stop()
        {
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.ActorStop, "stop"));
            // The remote scene will remove the SceneToPhysEngineConnector when we disconnect
            m_rcvLoop.Abort();
            ShutdownClient();

            //stop the migration connections
            //ShutdownClient(m_loadMigrationSouceEnd);
            //if (m_loadMigrationListener != null)
            //    m_loadMigrationListener.Shutdown();
        }

        public void ReportStatus()
        {
            m_log.WarnFormat("{0} Synchronized to RegionSyncServer at {1}:{2}", LogHeader, m_addr, m_port);
            m_log.WarnFormat("{0} Received={1}, Sent={2}", LogHeader, m_messagesReceived, m_messagesSent);
            lock (m_syncRoot)
            {
                //TODO: should be reporting about the information of the objects/scripts
            }
        }

        private void ShutdownClient()
        {
            m_log.WarnFormat("{0} Disconnected from RegionSyncServer. Shutting down.", LogHeader);

            //TODO: remove the objects and scripts
            //lock (m_syncRoot)
            //{
                
            //}

            if (m_client != null)
            {
                // Close the connection
                m_client.Client.Close();
                m_client.Close();
            }
            SceneToPhysEngineSyncServer.PhysLogMessageClose();
        }

        // Listen for messages from a RegionSyncServer
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
                    //lock (m_syncRoot) -- KittyL: why do we need to lock here? We could lock inside HandleMessage if necessary, and lock on different objects for better performance
                    m_messagesReceived++;
                    HandleMessage(msg);
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("{0} Encountered an exception: {1} (MSGTYPE = {2})", LogHeader, e.Message, msg.ToString());
                }
            }
        }

        #region SEND
        //DSG-TODO: for Scene based DSG, Send() also needs to figure out which Scene to send to, e.g. needs a switching function based on object position

        // Send a message to a single connected RegionSyncServer
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
                    m_messagesSent++;
                }
                // If there is a problem reading from the client, shut 'er down. 
                // *** Still need to alert the module that it's no longer connected!
                catch
                {
                    ShutdownClient();
                }
            }
        }
        #endregion SEND

        //KittyL: Has to define SendCoarseLocations() here, since it's defined in IRegionSyncClientModule.
        //        But should not do much as being PhysEngine, not ClientManager
        public void SendCoarseLocations()
        {
        }

        // Handle an incoming message
        // Dan-TODO: This should not be synchronous with the receive!
        //           Instead, handle messages from an incoming Queue so server doesn't block sending
        //
        // KittyL: This is the function that PhysEngine and ClientManager have the most different implementations
        private void HandleMessage(RegionSyncMessage msg)
        {
            //TO FINISH: 

            SceneToPhysEngineSyncServer.PhysLogMessage(false, msg);
            switch (msg.Type)
            {
                case RegionSyncMessage.MsgType.RegionName:
                    {
                        return;
                    }
                case RegionSyncMessage.MsgType.PhysUpdateAttributes:
                    {
                        HandlePhysUpdateAttributes(msg);
                        return;
                    }
                default:
                    {
                        RegionSyncMessage.HandleError(LogHeader, msg, String.Format("{0} Unsupported message type: {1}", LogHeader, ((int)msg.Type).ToString()));
                        return;
                    }
            }

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
                UUID uuid = data["uuid"].AsUUID();
                string actorID = data["actorID"].AsString();
                m_log.DebugFormat("{0}: HandlPhysUpdateAttributes for {1}", LogHeader, uuid);
                PhysicsActor pa = FindPhysicsActor(uuid);
                if (pa != null)
                {
                    if (pa.PhysicsActorType == (int)ActorTypes.Prim)
                    {
                        m_log.WarnFormat("{0}: HandlePhysUpdateAttributes for an prim: {1}", LogHeader, pa.UUID);
                    }
                    // pa.Size = data["size"].AsVector3();
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
                    SceneObjectPart sop = m_validLocalScene.GetSceneObjectPart(uuid);
                    if (sop != null)
                    {
                        pa.Shape = sop.Shape;
                    }
                    pa.ChangingActorID = actorID;
                    m_validLocalScene.PhysicsScene.AddPhysicsActorTaint(pa);
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
            SceneObjectPart sop = m_validLocalScene.GetSceneObjectPart(uuid);
            if (sop != null)
            {
                return sop.PhysActor;
            }
            ScenePresence sp = m_validLocalScene.GetScenePresence(uuid);
            if (sp != null)
            {
                return sp.PhysicsActor;
            }
            return null;
        }

        public void SendPhysUpdateAttributes(PhysicsActor pa)
        {
            // m_log.DebugFormat("{0}: SendPhysUpdateAttributes for {1}", LogHeader, pa.UUID);
            if (pa.PhysicsActorType == (int)ActorTypes.Prim)
            {
                m_log.WarnFormat("{0}: SendPhysUpdateAttributes for an prim: {1}", LogHeader, pa.UUID);
            }
            OSDMap data = new OSDMap(17);
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
            data["isColliding"] = OSD.FromBoolean(pa.IsColliding);
            data["isCollidingGround"] = OSD.FromBoolean(pa.CollidingGround);

            RegionSyncMessage rsm = new RegionSyncMessage(RegionSyncMessage.MsgType.PhysUpdateAttributes, 
                                                                OSDParser.SerializeJsonString(data));
            Send(rsm);
            return;
        }

        #region Utility functions

        private OSDMap GetOSDMap(string strdata)
        {
            OSDMap args = null;
            OSD buffer = OSDParser.DeserializeJson(strdata);
            if (buffer.Type == OSDType.Map)
            {
                args = (OSDMap)buffer;
                return args;
            }
            return null;
        
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



        public string GetServerAddressAndPort()
        {
            return m_addr.ToString() + ":" + m_port.ToString();
        }

        #endregion Utility functions

        #region Handlers for Scene events

        private void HandleAddOrUpdateObjectInLocalScene(RegionSyncMessage msg)
        {
            // TODO: modify for physics
            OSDMap data = DeserializeMessage(msg);
            uint locX = data["locX"].AsUInteger();
            uint locY = data["locY"].AsUInteger();
            string sogxml = data["sogXml"].AsString();
            SceneObjectGroup sog = SceneObjectSerializer.FromXml2Format(sogxml);

        }

        #endregion Handlers for events/updates from Scene

        public string StatisticIdentifier()
        {
            return "PhysEngineToSceneConnector";
        }

        public string StatisticLine(bool clearFlag)
        {
            string ret = "";
            /*
            lock (stats)
            {
                ret = String.Format("{0},{1},{2},{3},{4},{5}",
                    msgsIn, msgsOut, bytesIn, bytesOut
                    );
                if (clearFlag)
                    msgsIn = msgsOut = bytesIn = bytesOut = 0;
            }
            */
            return ret;
        }
        public string StatisticTitle()
        {
            return "msgsIn,msgsOut,bytesIn,bytesOut";
        }
    }
}

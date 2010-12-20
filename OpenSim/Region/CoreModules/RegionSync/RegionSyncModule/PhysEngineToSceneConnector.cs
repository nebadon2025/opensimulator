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
    //The data structure that maintains the list of quarks a script engine subscribes to.
    //It might be better to organize the quarks in a k-d tree structure, for easier
    //partitioning of the quarks based on spatial information.
    //But for now, we just assume the quarks each script engine operates on form a rectangle shape.
    //So we just use xmin,ymin and xmax,ymax to identify the rectange; and use a List structure to
    //store the quarks.
    //Quark size is defined in QuarkInfo.SizeX and QuarkInfo.SizeY.

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


        //KittyL: Comment out m_statsTimer for now, will figure out whether we need it for PhysEngine later
        //private System.Timers.Timer m_statsTimer = new System.Timers.Timer(30000);

        // The queue of incoming messages which need handling
        //private Queue<string> m_inQ = new Queue<string>();

        //KittyL: added to identify different actors
        private ActorType m_actorType = ActorType.PhysicsEngine;

        private bool m_debugWithViewer = false;
        private long m_messagesSent = 0;
        private long m_messagesReceived = 0;

        private QuarkSubsriptionInfo m_subscribedQuarks; 
        

        private IConfig m_sysConfig;

        //members for load balancing purpose
        //private TcpClient m_loadMigrationSouceEnd = null;
        private LoadMigrationEndPoint m_loadMigrationSouceEnd = null;
        private Thread m_loadMigrationSrcRcvLoop;
        private LoadMigrationListener m_loadMigrationListener = null;

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
            //m_statsTimer.Elapsed += new System.Timers.ElapsedEventHandler(StatsTimerElapsed);
            m_sysConfig = sysConfig;

            logEnabled = m_sysConfig.GetBoolean("LogEnabled", false);
            logDir = m_sysConfig.GetString("LogDir", ".");

            //assume we are connecting to the whole scene as one big quark
            m_subscribedQuarks = new QuarkSubsriptionInfo(0, 0, (int)Constants.RegionSize, (int)Constants.RegionSize);
        }

        private List<string> GetQuarkStringList()
        {
            List<string> quarkList = new List<string>();
            foreach (QuarkInfo quark in m_subscribedQuarks.QuarkList)
            {
                quarkList.Add(quark.QuarkStringRepresentation);
            }
            return quarkList;
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
            // SendQuarkSubscription();
            Thread.Sleep(100);
            DoInitialSync();
        }


        private void SendQuarkSubscription()
        {
            List<string> quarkStringList = GetQuarkStringList();
            string quarkString = RegionSyncUtil.QuarkStringListToString(quarkStringList);

            m_log.Debug(LogHeader + ": subscribe to quarks: " + quarkString);
            //Send(quarkString);
            RegionSyncMessage msg = new RegionSyncMessage(RegionSyncMessage.MsgType.QuarkSubscription, quarkString);
            Send(msg);
        }

        public void SetQuarkSubscription(QuarkSubsriptionInfo quarks)
        {
            m_subscribedQuarks = quarks;
        }

        public void RegisterIdle()
        {
            EstablishConnection();
            RegionSyncMessage msg = new RegionSyncMessage(RegionSyncMessage.MsgType.ActorStatus, Convert.ToString((int)ActorStatus.Idle));
            Send(msg);
        }

        private void DoInitialSync()
        {
            // m_validLocalScene.DeleteAllSceneObjects();
            //m_log.Debug(LogHeader + ": send actor type " + m_actorType);
            //Send(new RegionSyncMessage(RegionSyncMessage.MsgType.ActorType, Convert.ToString((int)m_actorType)));
            //KittyL??? Do we need to send in RegionName?

            //Send(new RegionSyncMessage(RegionSyncMessage.MsgType.RegionName, m_scene.RegionInfo.RegionName));
            //m_log.WarnFormat("Sending region name: \"{0}\"", m_scene.RegionInfo.RegionName);

            // Send(new RegionSyncMessage(RegionSyncMessage.MsgType.GetTerrain));
            // Send(new RegionSyncMessage(RegionSyncMessage.MsgType.GetObjects));

            // Register for events which will be forwarded to authoritative scene
            // m_scene.EventManager.OnNewClient += EventManager_OnNewClient;
            //m_scene.EventManager.OnClientClosed += new EventManager.ClientClosed(RemoveLocalClient);
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
            if (m_loadMigrationListener != null)
                m_loadMigrationListener.Shutdown();
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
            LogMessageClose();
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
            LogMessage(logOutput, msg);
            byte[] bmsg = System.Text.Encoding.ASCII.GetBytes(msg + System.Environment.NewLine);
            Send(bmsg);
        }

        private void Send(RegionSyncMessage msg)
        {
            LogMessage(logOutput, msg);
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

            LogMessage(logInput, msg);
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
                uint localID = data["localID"].AsUInteger();
                // m_log.DebugFormat("{0}: HandlPhysUpdateAttributes for {1}", LogHeader, localID);
                PhysicsActor pa = FindPhysicsActor(localID);
                if (pa != null)
                {
                    Vector3 sizeTemp = data["size"].AsVector3();
                    if (sizeTemp.Z != 0)
                    {
                        // pa.Size = sizeTemp;
                    }
                    pa.Position = data["position"].AsVector3();
                    pa.Force = data["force"].AsVector3();
                    pa.Velocity = data["velocity"].AsVector3();
                    pa.Torque = data["torque"].AsVector3();
                    pa.Orientation = data["orientantion"].AsQuaternion();
                    pa.IsPhysical = data["isPhysical"].AsBoolean();  // receive??
                    pa.Flying = data["flying"].AsBoolean();      // receive??
                    pa.Kinematic = data["kinematic"].AsBoolean();    // receive??
                    pa.Buoyancy = (float)(data["buoyancy"].AsReal());
                    SceneObjectPart sop = m_validLocalScene.GetSceneObjectPart(localID);
                    if (sop != null)
                    {
                        pa.Shape = sop.Shape;
                    }
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
            SceneObjectPart sop = m_validLocalScene.GetSceneObjectPart(localID);
            if (sop != null)
            {
                return sop.PhysActor;
            }
            ScenePresence sp = m_validLocalScene.GetScenePresence(localID);
            if (sp != null)
            {
                return sp.PhysicsActor;
            }
            return null;
        }

        public void SendPhysUpdateAttributes(PhysicsActor pa)
        {
            // m_log.DebugFormat("{0}: SendPhysUpdateAttributes for {1}", LogHeader, pa.LocalID);
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
            /*
            if (data["locX"] == null || data["locY"] == null || data["sogXml"] == null)
            {
                m_log.Warn(LogHeader + ": parameters missing in NewObject/UpdatedObject message, need to have locX, locY, sogXml");
                return;
            }
             * */
            uint locX = data["locX"].AsUInteger();
            uint locY = data["locY"].AsUInteger();
            string sogxml = data["sogXml"].AsString();
            SceneObjectGroup sog = SceneObjectSerializer.FromXml2Format(sogxml);

        }

        #endregion Handlers for events/updates from Scene
        #region Message Logging
        private bool logInput = false;
        private bool logOutput = true;
        private bool logEnabled = true;
        private class MsgLogger
        {
            public DateTime startTime;
            public string path = null;
            public System.IO.TextWriter Log = null;
        }
        private MsgLogger logWriter = null;
        private TimeSpan logMaxFileTime = new TimeSpan(0, 5, 0);   // (h,m,s) => 5 minutes
        private string logDir = "/stats/stats";
        private object logLocker = new Object();

        private void LogMessage(bool direction, RegionSyncMessage rsm)
        {
            if (!logEnabled) return;    // save to work of the ToStringFull if not enabled
            LogMessage(direction, rsm.ToStringFull());
        }

        private void LogMessage(bool direction, string msg)
        {
            if (!logEnabled) return;

            lock (logLocker)
            {
                try
                {
                    DateTime now = DateTime.Now;
                    if (logWriter == null || (now > logWriter.startTime + logMaxFileTime))
                    {
                        if (logWriter != null && logWriter.Log != null)
                        {
                            logWriter.Log.Close();
                            logWriter.Log.Dispose();
                            logWriter.Log = null;
                        }

                        // First log file or time has expired, start writing to a new log file
                        logWriter = new MsgLogger();
                        logWriter.startTime = now;
                        logWriter.path = (logDir.Length > 0 ? logDir + System.IO.Path.DirectorySeparatorChar.ToString() : "")
                                + String.Format("physics-{0}.log", now.ToString("yyyyMMddHHmmss"));
                        logWriter.Log = new StreamWriter(File.Open(logWriter.path, FileMode.Append, FileAccess.Write));
                    }
                    if (logWriter != null && logWriter.Log != null)
                    {
                        StringBuilder buff = new StringBuilder();
                        buff.Append(now.ToString("yyyyMMddHHmmss"));
                        buff.Append(" ");
                        buff.Append(direction ? "A->S:" : "S->A:");
                        buff.Append(msg);
                        buff.Append("\r\n");
                        logWriter.Log.Write(buff.ToString());
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("{0}: FAILURE WRITING TO LOGFILE: {1}", LogHeader, e);
                    logEnabled = false;
                }
            }
            return;
        }

        private void LogMessageClose()
        {
            if (logWriter != null && logWriter.Log != null)
            {
                logWriter.Log.Close();
                logWriter.Log.Dispose();
                logWriter.Log = null;
                logWriter = null;
            }
            logEnabled = false;
        }
        #endregion Message Logging

    }
}

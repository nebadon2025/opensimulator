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

        private string m_authSceneName;

        //KittyL: Comment out m_statsTimer for now, will figure out whether we need it for PhysEngine later
        //private System.Timers.Timer m_statsTimer = new System.Timers.Timer(30000);

        // The queue of incoming messages which need handling
        //private Queue<string> m_inQ = new Queue<string>();

        //KittyL: added to identify different actors
        private ActorType m_actorType = ActorType.PhysicsEngine;

        private bool m_debugWithViewer = false;

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
        public PhysEngineToSceneConnector(Scene validLocalScene, string addr, int port, bool debugWithViewer, string authSceneName, IConfig sysConfig)
        {
            m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            m_validLocalScene = validLocalScene;
            m_addr = IPAddress.Parse(addr);
            m_addrString = addr;
            m_port = port;
            m_debugWithViewer = debugWithViewer;
            m_authSceneName = authSceneName;
            //m_statsTimer.Elapsed += new System.Timers.ElapsedEventHandler(StatsTimerElapsed);
            m_sysConfig = sysConfig;

            //assume we are connecting to the whole scene as one big quark
            m_subscribedQuarks = new QuarkSubsriptionInfo(0, 0, (int)Constants.RegionSize, (int)Constants.RegionSize);
        }

        /// <summary>
        /// Create a PhysEngineToSceneConnector based on the space it is supposed to subscribe (and operate) on.
        /// </summary>
        /// <param name="validLocalScene"></param>
        /// <param name="addr"></param>
        /// <param name="port"></param>
        /// <param name="debugWithViewer"></param>
        /// <param name="authSceneName"></param>
        /// <param name="subscriptionSpace"></param>
        /// <param name="quarkSizeX"></param>
        /// <param name="quarkSizeY"></param>
        /// <param name="sysConfig"></param>
        public PhysEngineToSceneConnector(Scene validLocalScene, string addr, int port, bool debugWithViewer, string authSceneName,
            string subscriptionSpace, IConfig sysConfig)
        {
            if (QuarkInfo.SizeX == -1 || QuarkInfo.SizeY == -1)
            {
                m_log.Error("QuarkInfo.SizeX or QuarkInfo.SizeY has not been configured.");
                Environment.Exit(0);
            }

            m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            m_validLocalScene = validLocalScene;
            m_addr = IPAddress.Parse(addr);
            m_addrString = addr;
            m_port = port;
            m_debugWithViewer = debugWithViewer;
            m_authSceneName = authSceneName;
            //m_statsTimer.Elapsed += new System.Timers.ElapsedEventHandler(StatsTimerElapsed);
            m_sysConfig = sysConfig;

            m_subscribedQuarks = new QuarkSubsriptionInfo(subscriptionSpace);
        }

        public PhysEngineToSceneConnectorModule GetPEToSceneConnectorMasterModule()
        {
            if (m_validLocalScene == null)
                return null;
            return (PhysEngineToSceneConnectorModule)m_validLocalScene.PhysEngineToSceneConnectorModule;
        }

        public Scene GetValidLocalScene()
        {
            return m_validLocalScene;
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
         


        /// <summary>
        /// Get the reference to the local scene that is supposed to be mapped to the remote auth. scene.
        /// </summary>
        /// <param name="authSceneName"></param>
        /// <returns></returns>
        private Scene GetLocalScene(string authSceneName)
        {
            PhysEngineToSceneConnectorModule connectorModule = GetPEToSceneConnectorMasterModule();
            return connectorModule.GetLocalScene(authSceneName);
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
            SendQuarkSubscription();
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
            m_validLocalScene.DeleteAllSceneObjects();
            //m_log.Debug(LogHeader + ": send actor type " + m_actorType);
            //Send(new RegionSyncMessage(RegionSyncMessage.MsgType.ActorType, Convert.ToString((int)m_actorType)));
            //KittyL??? Do we need to send in RegionName?

            //Send(new RegionSyncMessage(RegionSyncMessage.MsgType.RegionName, m_scene.RegionInfo.RegionName));
            //m_log.WarnFormat("Sending region name: \"{0}\"", m_scene.RegionInfo.RegionName);

            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.GetTerrain));
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.GetObjects));

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
                }
                // If there is a problem reading from the client, shut 'er down. 
                // *** Still need to alert the module that it's no longer connected!
                catch
                {
                    ShutdownClient();
                }
            }
        }

        /// <summary>
        /// Send requests to update object properties in the remote authoratative Scene.
        /// </summary>
        /// <param name="primID">UUID of the object</param>
        /// <param name="pName">name of the property to be updated</param>
        /// <param name="valParams">parameters of the value of the property</param>
        /// <returns></returns>
        public void SendSetPrimProperties(UUID primID, string pName, object val)
        {
            OSDMap data = new OSDMap();
            data["UUID"] = OSD.FromUUID(primID);
            data["name"] = OSD.FromString(pName);
            object[] valParams = (object[])val;
            //data["param"] = OSD.FromString(presence.ControllingClient.LastName);
            Vector3 pos, vel;
            switch (pName)
            {
                case "object_rez":
                    //this is to rez an object from the prim's inventory, rather than change the prim's property
                    if(valParams.Length<5){
                        m_log.Warn(LogHeader+": values for object's "+pName+" property should include: inventory, pos, velocity, rotation, param");
                        return;
                    }
                    string inventory = (string)valParams[0];
                    pos = (Vector3)valParams[1];
                    vel = (Vector3)valParams[2];
                    Quaternion rot = (Quaternion)valParams[3];
                    int param = (int)valParams[4];
                    data["inventory"]=OSD.FromString(inventory);
                    data["pos"]=OSD.FromVector3(pos);
                    data["vel"] = OSD.FromVector3(vel);
                    data["rot"] = OSD.FromQuaternion(rot);
                    data["param"] = OSD.FromInteger(param);
                    break;
                case "color":
                    if(valParams.Length<2){
                        m_log.Warn(LogHeader+": values for object's "+pName+" property should include: color-x, color-y, color-z, face");
                        return;
                    }
                    //double cx = (double)valParams[0];
                    //double cy = (double)valParams[1];
                    //double cz = (double)valParams[2];
                    //Vector3 color = new Vector3((float)cx, (float)cy, (float)cz);
                    Vector3 color = (Vector3)valParams[0];
                    data["color"] = OSD.FromVector3(color);
                    data["face"] = OSD.FromInteger((int)valParams[1]);

                    //m_log.DebugFormat("{0}: to set color {1} on face {2} of prim {3}", LogHeader, color.ToString(), (int)valParams[1], primID);

                    break;
                case "pos":
                    if (valParams.Length < 1)
                    {
                        m_log.Warn(LogHeader + ": values for object's " + pName + " property should include: pos(vector)");
                        return;
                    }
                    //double px = (double)valParams[0];
                    //double py = (double)valParams[1];
                    //double pz = (double)valParams[2];
                    //Vector3 pos = new Vector3((float)px, (float)py, (float)pz);
                    pos = (Vector3)valParams[0];
                    data["pos"] = OSD.FromVector3(pos);

                    m_log.DebugFormat("{0}: to set pos {1} for prim {2}", LogHeader, pos.ToString(), primID);
                    break;
                default:
                    //
                    break;
            }

            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.SetObjectProperty, OSDParser.SerializeJsonString(data)));
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

            switch (msg.Type)
            {
                case RegionSyncMessage.MsgType.RegionName:
                    {
                        string authSceneName = Encoding.ASCII.GetString(msg.Data, 0, msg.Length);
                        if (authSceneName != m_authSceneName)
                        {
                            //This should not happen. If happens, check the configuration files (OpenSim.ini) on other sides.
                            m_log.Warn(": !!! Mismatch between configurations of authoritative scene. Script Engine's config: "+m_authSceneName+", Scene's config: "+authSceneName);
                            return;
                        }
                        RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Syncing to region \"{0}\"", m_authSceneName));
                        return;
                    }
                case RegionSyncMessage.MsgType.Terrain:
                    {
                        //We need to handle terrain differently as we handle objects: we really will set the HeightMap
                        //of each local scene that is the shadow copy of its auth. scene.
                        Scene localScene = GetLocalScene(m_authSceneName);
                        if (localScene == null)
                        {
                            m_log.Warn("no local Scene mapped to "+m_authSceneName);
                            return;
                        }
                        localScene.Heightmap.LoadFromXmlString(Encoding.ASCII.GetString(msg.Data, 0, msg.Length));
                        RegionSyncMessage.HandleSuccess(LogHeader, msg, "Synchronized terrain");
                        return;
                    }
                case RegionSyncMessage.MsgType.NewObject:
                case RegionSyncMessage.MsgType.UpdatedObject:
                    {
                        HandleAddOrUpdateObjectInLocalScene(msg);
                        return;
                    }
                default:
                    {
                        RegionSyncMessage.HandleError(LogHeader, msg, String.Format("{0} Unsupported message type: {1}", LogHeader, ((int)msg.Type).ToString()));
                        return;
                    }
            }

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

    }
        
}

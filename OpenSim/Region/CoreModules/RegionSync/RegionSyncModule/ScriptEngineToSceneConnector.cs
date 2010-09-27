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

//SYNC_SE: Added by KittyL, 06/21/2010
namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    //The data structure that maintains the list of quarks a script engine subscribes to.
    //It might be better to organize the quarks in a k-d tree structure, for easier
    //partitioning of the quarks based on spatial information.
    //But for now, we just assume the quarks each script engine operates on form a rectangle shape.
    //So we just use xmin,ymin and xmax,ymax to identify the rectange; and use a List structure to
    //store the quarks.
    //Quark size is defined in QuarkInfo.SizeX and QuarkInfo.SizeY.
    public class QuarkSubsriptionInfo
    {
        private List<QuarkInfo> m_quarks;
        private int m_minX;
        private int m_minY;
        private int m_maxX;
        private int m_maxY;
        //public static int QuarkSizeX;
        //public static int QuarkSizeY;

        public int MinX
        {
            get { return m_minX; }
        }

        public int MinY
        {
            get { return m_minY; }
        }

        public int MaxX
        {
            get { return m_maxX; }
        }

        public int MaxY
        {
            get { return m_maxY; }
        }

        public QuarkSubsriptionInfo(int xmin, int ymin, int xmax, int ymax)
        {
            //QuarkInfo.SizeX = quarkSizeX;
            //QuarkInfo.SizeY = quarkSizeY;
            m_minX = xmin;
            m_minY = ymin;
            m_maxX = xmax;
            m_maxY = ymax;
            m_quarks = RegionSyncUtil.GetQuarkSubscriptions(xmin, ymin, xmax, ymax);
        }

        public QuarkSubsriptionInfo(string space)
        {
            //QuarkInfo.SizeX = quarkSizeX;
            //QuarkInfo.SizeY = quarkSizeY;
            int[] coordinates = RegionSyncUtil.GetCornerCoordinates(space);
            m_minX = coordinates[0];
            m_minY = coordinates[1];
            m_maxX = coordinates[2];
            m_maxY = coordinates[3];

            m_quarks = RegionSyncUtil.GetQuarkSubscriptions(m_minX, m_minY, m_maxX, m_maxY);
        }

        public List<QuarkInfo> QuarkList
        {
            get { return m_quarks; }
        }

        /// <summary>
        /// Test if a given position is in the space of the quarks.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public bool IsInSpace(Vector3 pos)
        {
            //For now, the test is simple: we just compare with the corners of the rectangle space.
            //Need to be modified when we later support irregular quark unions.
            if (pos.X < MinX || pos.X >= MaxX || pos.Y < MinY || pos.Y >= MaxY)
            {
                return false;
            }
            else
                return true;
        }

        public string StringRepresentation()
        {
            return RegionSyncUtil.QuarkInfoToString(m_quarks);
        }

        public void RemoveQuarks(QuarkSubsriptionInfo remQuarks)
        {
            //First, remove the given quarks.
            QuarkInfo toRemoveQuark;
            foreach (QuarkInfo rQuark in remQuarks.QuarkList)
            {
                toRemoveQuark = null;
                foreach (QuarkInfo originalQuark in m_quarks)
                {
                    if (rQuark.QuarkStringRepresentation.Equals(originalQuark.QuarkStringRepresentation))
                    {
                        toRemoveQuark = originalQuark;
                        break;
                    }
                }
                if (toRemoveQuark != null)
                {
                    m_quarks.Remove(toRemoveQuark);
                }
            }

            //Second, adjust the space bounding box
            //The removed space should share the same top-right corner with the original space (simple binary space partition)
            int[] remainingBoundingBox = RegionSyncUtil.RemoveSpace(m_minX, m_minY, m_maxX, m_maxY, remQuarks.MinX, remQuarks.MinY, remQuarks.MaxX, remQuarks.MaxY);
            m_minX = remainingBoundingBox[0];
            m_minY = remainingBoundingBox[1];
            m_maxX = remainingBoundingBox[2];
            m_maxY = remainingBoundingBox[3];
        }
    }

    // The RegionSyncScriptEngine has a receive thread to process messages from the RegionSyncServer.
    // It is the client side of the synchronization channel, and send to and receive updates from the 
    // Auth. Scene. The server side thread handling the sync channel is implemented in RegionSyncScriptAPI.cs.
    // 
    // The current implementation is very similar to RegionSyncClient.
    // TODO: eventually the same RegionSyncSceneAPI should handle all traffic from different actors, e.g. 
    //       using a pub/sub model.
    public class ScriptEngineToSceneConnector
    {
        #region ScriptEngineToSceneConnector members

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

        private string LogHeader = "[SCRIPT ENGINE TO SCENE CONNECTOR]";

        // The listener and the thread which listens for connections from client managers
        private Thread m_rcvLoop;

        // The client connection to the RegionSyncServer
        private TcpClient m_client = new TcpClient();

        private string m_authSceneName;

        //KittyL: Comment out m_statsTimer for now, will figure out whether we need it for ScriptEngine later
        //private System.Timers.Timer m_statsTimer = new System.Timers.Timer(30000);

        // The queue of incoming messages which need handling
        //private Queue<string> m_inQ = new Queue<string>();

        //KittyL: added to identify different actors
        private ActorType m_actorType = ActorType.ScriptEngine;

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
        public ScriptEngineToSceneConnector(Scene validLocalScene, string addr, int port, bool debugWithViewer, string authSceneName, IConfig sysConfig)
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
        /// Create a ScriptEngineToSceneConnector based on the space it is supposed to subscribe (and operate) on.
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
        public ScriptEngineToSceneConnector(Scene validLocalScene, string addr, int port, bool debugWithViewer, string authSceneName,
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

        public ScriptEngineToSceneConnectorModule GetSEToSceneConnectorMasterModule()
        {
            if (m_validLocalScene == null)
                return null;
            return (ScriptEngineToSceneConnectorModule)m_validLocalScene.ScriptEngineToSceneConnectorModule;
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
            ScriptEngineToSceneConnectorModule connectorModule = GetSEToSceneConnectorMasterModule();
            return connectorModule.GetLocalScene(authSceneName);
        }

        // Start the RegionSyncScriptEngine client thread
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
                m_log.WarnFormat("{0} [Start] Could not connect to SceneToScriptEngineSyncServer at {1}:{2}", LogHeader, m_addr, m_port);
                m_log.Warn(e.Message);
                return false;
            }

            m_log.WarnFormat("{0} Connected to SceneToScriptEngineSyncServer at {1}:{2}", LogHeader, m_addr, m_port);

            m_rcvLoop = new Thread(new ThreadStart(ReceiveLoop));
            m_rcvLoop.Name = "ScriptEngineToSceneConnector ReceiveLoop";
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
            // The remote scene will remove the SceneToScriptEngineConnector when we disconnect
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
        //        But should not do much as being ScriptEngine, not ClientManager
        public void SendCoarseLocations()
        {
        }

        // Handle an incoming message
        // Dan-TODO: This should not be synchronous with the receive!
        //           Instead, handle messages from an incoming Queue so server doesn't block sending
        //
        // KittyL: This is the function that ScriptEngine and ClientManager have the most different implementations
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
                case RegionSyncMessage.MsgType.RemovedObject:
                    {
                        HandleRemoveObject(msg);
                        return;
                    }
                case RegionSyncMessage.MsgType.ResetScene:
                    {
                        m_validLocalScene.DeleteAllSceneObjects();
                        return;
                    }
                case RegionSyncMessage.MsgType.OnRezScript:
                    {
                        HandleOnRezScript(msg);
                        return;
                    }
                case RegionSyncMessage.MsgType.OnScriptReset:
                    {
                        HandleOnScriptReset(msg);
                        return;
                    }
                case RegionSyncMessage.MsgType.OnUpdateScript:
                    {
                        HandleOnUpdateScript(msg);
                        return;
                    }
                case RegionSyncMessage.MsgType.SceneLocation:
                    {
                        HandleSceneLocation(msg);
                        return;
                    }
                case RegionSyncMessage.MsgType.LoadMigrationNotice:
                    {
                        HanldeLoadMigrationNotice(msg);
                        return;
                    }
                case RegionSyncMessage.MsgType.LoadBalanceRejection:
                    {
                        HandleLoadBalanceRejection(msg);
                        return;
                    }
                case RegionSyncMessage.MsgType.LoadBalanceResponse:
                    {
                        HandleLoadBalanceResponse(msg);
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

            if (sog.IsDeleted)
            {
                RegionSyncMessage.HandleTrivial(LogHeader, msg, String.Format("Ignoring update on deleted LocalId {0}.", sog.LocalId.ToString()));
                return;
            }
            else
            {
                //We record the object's location (locX, locY), the coordinates of the left-bottom corner of the scene
                sog.LocX = locX;
                sog.LocY = locY;
                bool added = m_validLocalScene.AddOrUpdateObjectInLocalScene(sog, m_debugWithViewer);

                if (added)
                {
                    //m_log.DebugFormat("[{0} Object \"{1}\" ({1}) ({2}) added.", LogHeader, sog.Name, sog.UUID.ToString(), sog.LocalId.ToString());
                    //rez script
                    sog.CreateScriptInstances(0, false, m_validLocalScene.DefaultScriptEngine, 0);
                    sog.ResumeScripts();
                }
                else
                {
                    //m_log.DebugFormat("[{0} Object \"{1}\" ({1}) ({2}) updated.", LogHeader, sog.Name, sog.UUID.ToString(), sog.LocalId.ToString());
                }
            }
        }

        private void HandleRemoveObject(RegionSyncMessage msg)
        {
            // Get the data from message and error check
            OSDMap data = DeserializeMessage(msg);
            if (data == null)
            {
                RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                return;
            }

            if (!data.ContainsKey("UUID"))
            {
                m_log.Warn(LogHeader + ": parameters missing in RemoveObject message from Scene, need to have object UUID");
                return;
            }

            // Get the parameters from data
            //ulong regionHandle = data["regionHandle"].AsULong();
           // UUID objID = data["sogUUID"].AsUUID();
            UUID primID = data["UUID"].AsUUID();
            
            // Find the object in the scene
            SceneObjectGroup sog = m_validLocalScene.SceneGraph.GetGroupByPrim(primID);
            if (sog == null)
            {
                //RegionSyncMessage.HandleWarning(LogHeader, msg, String.Format("localID {0} not found.", localID.ToString()));
                return;
            }

            // Delete the object from the scene
            m_validLocalScene.DeleteSceneObject(sog, false);
            RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("object {0} deleted.", primID.ToString()));
        }

        private void HandleOnRezScript(RegionSyncMessage msg)
        {

            OSDMap data = DeserializeMessage(msg);
            /*
            if (!data.ContainsKey("localID") || !data.ContainsKey("itemID") || !data.ContainsKey("script") || !data.ContainsKey("startParam") 
                || !data.ContainsKey("postOnRez") || !data.ContainsKey("engine") || !data.ContainsKey("stateSource"))
            {
                if (!data.ContainsKey("localID")) m_log.Debug("missing localID");
                if (!data.ContainsKey("itemID")) m_log.Debug("missing itemID");
                if (!data.ContainsKey("script")) m_log.Debug("missing script");
                if (!data.ContainsKey("startParam")) m_log.Debug("missing startParam");
                if (!data.ContainsKey("postOnRez")) m_log.Debug("missing postOnRez");
                if (!data.ContainsKey("engine")) m_log.Debug("missing engine");
                if (!data.ContainsKey("stateSource")) m_log.Debug("missing stateSource");
                
                m_log.Warn(LogHeader+": parameters missing in OnRezScript message from Scene, need to have localID, itemID, script,startParam, postOnRez, engine, and stateSource");
                return;
            }
             * */ 

            uint localID = data["localID"].AsUInteger();
            UUID itemID = data["itemID"].AsUUID();
            string script = data["script"].AsString();
            int startParam = data["startParam"].AsInteger();
            bool postOnRez =  data["postOnRez"].AsBoolean();
            string engine = data["engine"].AsString();
            int stateSource = data["stateSource"].AsInteger();

            m_log.Debug(LogHeader + ": TriggerRezScript");
            //Trigger OnRezScrip to start script engine's execution 
            m_validLocalScene.EventManager.TriggerRezScript(localID, itemID, script, startParam, postOnRez, engine, stateSource);
        }

        private void HandleOnScriptReset(RegionSyncMessage msg)
        {
            OSDMap data = DeserializeMessage(msg);
            /*
            if (data["localID"] == null || data["itemID"] == null)
            {
                m_log.Warn(LogHeader + ": parameters missing in OnScriptReset message from Scene, need to have localID, itemID");
                return;
            }
             * */
            
            uint localID = data["localID"].AsUInteger();
            UUID itemID = data["itemID"].AsUUID();
            m_log.Debug(LogHeader + ": TriggerScriptReset");
            //Trigger OnScriptReset to start script engine's execution 
            m_validLocalScene.EventManager.TriggerScriptReset(localID, itemID);
        }

        private void HandleOnUpdateScript(RegionSyncMessage msg)
        {
            OSDMap data = DeserializeMessage(msg);
            /*
            if (!data.ContainsKey("agentID") == null || !data.ContainsKey("itemID") == null || !data.ContainsKey("primID") == null || !data.ContainsKey("running") == null)
            {
                m_log.Warn(LogHeader + ": parameters missing in OnUpdateScript message from Scene, need to have agentID, itemID, primID, isRunning, and assetID");
                return;
            }
             * */
            UUID agentID = data["agentID"].AsUUID();
            UUID itemID = data["itemID"].AsUUID();
            UUID primID = data["primID"].AsUUID();
            bool isScriptRunning = data["running"].AsBoolean();
            UUID newAssetID = data["assetID"].AsUUID();
            m_log.Debug(LogHeader + ": HandleOnUpdateScript");
            //m_scene.EventManager.TriggerUpdateTaskInventoryScriptAsset(agentID, itemID, primID, isScriptRunning);
            ArrayList errors = m_validLocalScene.OnUpdateScript(agentID, itemID, primID, isScriptRunning, newAssetID);

            //now the script is re-rez'ed, return complication errors if there are

        }

        private void HandleSceneLocation(RegionSyncMessage msg)
        {
            OSDMap data = DeserializeMessage(msg);
            /*
            if (!data.ContainsKey("locX")|| !data.ContainsKey("locY"))
            {
                m_log.Warn(LogHeader + ": parameters missing in SceneLocation message from Scene, need to have locX, locY");
                return;
            }
             * */
            uint locX = data["locX"].AsUInteger();
            uint locY = data["locY"].AsUInteger();

            ScriptEngineToSceneConnectorModule connectorModule = GetSEToSceneConnectorMasterModule();
            connectorModule.RecordSceneLocation(m_addrString, m_port, locX, locY); 
        }

        private void HandleLoadBalanceRejection(RegionSyncMessage msg)
        {
            string reason = Encoding.ASCII.GetString(msg.Data, 0, msg.Length);
            m_log.Debug(LogHeader + ": Load balance request rejected: " + reason);
        }

        private void HandleLoadBalanceResponse(RegionSyncMessage msg)
        {
            //Start a seperate thread to handle the load migration process

            Thread migrationThread = new Thread(new ParameterizedThreadStart(TriggerLoadMigrationProcess));
            migrationThread.Name = "TriggerLoadMigrationProcess";
            migrationThread.Start((object)msg);
        }

        #endregion Handlers for events/updates from Scene

        #region Load migration functions: source side for migrating load away


        public void SendLoadBalanceRequest()
        {
            //Send a request to Scene, which, is also a load balancing server
            RegionSyncMessage msg = new RegionSyncMessage(RegionSyncMessage.MsgType.LoadBalanceRequest);
            Send(msg);
        }

        //Migration process handler. For now, we do binary space partitioning to balance load.
        private void TriggerLoadMigrationProcess(object arg)
        {
            if (m_loadMigrationSouceEnd!=null)
            {
                m_log.Warn(LogHeader + ": already in load balancing process");
                return;
            }

            RegionSyncMessage msg = (RegionSyncMessage)arg;
            OSDMap data = DeserializeMessage(msg);
            if (!data.ContainsKey("ip") || !data.ContainsKey("port"))
            {
                m_log.Warn(LogHeader + ": parameters missing in SceneLocation message from Scene, need to have ip, port");
                return;
            }

            string idleSEAddr = data["ip"].AsString();
            int idleSEPort = data["port"].AsInteger();
            m_log.Debug(LogHeader + " receive LB response: idle SE listening at " + idleSEAddr + ":" + idleSEPort);

            Thread.Sleep(500);
            //connect to the idle script engine's load migration listening port
            TcpClient loadMigrationSrc = new TcpClient();
            try
            {
                m_log.Debug(LogHeader + ", connecting to " + idleSEAddr + "," + idleSEPort);
                loadMigrationSrc.Connect(idleSEAddr, idleSEPort);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("{0} [Start] In load migration. Could not connect to {1}:{2}", LogHeader, idleSEAddr, idleSEPort);
                m_log.Warn(e.Message);
                return;
            }
            m_log.WarnFormat("{0} Connected to Load migration destination at {1}:{2}", LogHeader, idleSEAddr, idleSEPort);

            //Get the quarks that will be migrated out
            QuarkSubsriptionInfo migratingQuarks = PartitionLoad(m_subscribedQuarks);

            //Create an EndPoint to manage the migration process
            m_loadMigrationSouceEnd = new LoadMigrationEndPoint(loadMigrationSrc, migratingQuarks, this, m_log);

            OSDMap outData = GetMigrationSpaceDescription(migratingQuarks);
            msg = new RegionSyncMessage(RegionSyncMessage.MsgType.MigrationSpace, OSDParser.SerializeJsonString(outData));
            m_loadMigrationSouceEnd.Send(msg);

        }


        //For now, we simply implement a simple binary space partitioning. 
        //Ideally, a k-d tree structure should be used and we can divide the quarks accordingly
        /// <summary>
        /// Partition the suscribed quarks, and return the quarks that should be migrated away.
        /// </summary>
        /// <param name="quarkSubscription"></param>
        /// <returns></returns>
        private QuarkSubsriptionInfo PartitionLoad(QuarkSubsriptionInfo quarkSubscription)
        {
            Dictionary<string, int[]> partitionedPlains = RegionSyncUtil.BinarySpaceParition(quarkSubscription.MinX, quarkSubscription.MinY, quarkSubscription.MaxX, quarkSubscription.MaxY);

            int[] upperPlain = partitionedPlains["upper"];
            string partitionedSpace = RegionSyncUtil.GetSpaceStringRepresentationByCorners(upperPlain[0], upperPlain[1], upperPlain[2], upperPlain[3]);
            QuarkSubsriptionInfo partitionedQuarks = new QuarkSubsriptionInfo(partitionedSpace);

            return partitionedQuarks;
        }

        //For now, since all the quarks form a rectangle shape, we simple use the corners to represent the space.
        /// <summary>
        /// Return a OSDMap that describes the set of quarks that are being migrated.
        /// </summary>
        /// <param name="quarks"></param>
        /// <returns></returns>
        private OSDMap GetMigrationSpaceDescription(QuarkSubsriptionInfo quarks)
        {

            OSDMap outData = new OSDMap(5);

            //send the parameters as uint, to avoid seemingly some bugs in SerializeJsonString that somehow does not serialize integer '0' correctly
            outData["minX"] = OSD.FromUInteger((uint)quarks.MinX);
            outData["minY"] = OSD.FromUInteger((uint)quarks.MinY);
            outData["maxX"] = OSD.FromUInteger((uint)quarks.MaxX);
            outData["maxY"] = OSD.FromUInteger((uint)quarks.MaxY);

            outData["regionName"] = OSD.FromString(m_authSceneName);

            return outData;
        }

        public void RemoveQuarks(QuarkSubsriptionInfo removedQuarks)
        {
            //Remove the given quarks
            m_subscribedQuarks.RemoveQuarks(removedQuarks);

            //Inform Scene with the updated quark subscription
            SendQuarkSubscription();
        }

        #endregion Load migration functions: source side for migrating load away

        #region Load migration functions: load receiving side

        private void HanldeLoadMigrationNotice(RegionSyncMessage msg)
        {
            m_log.Debug(LogHeader + " received LoadMigrationNotice");

            if (m_loadMigrationListener == null)
            {

                //set up a listening thread for receiving load
                string addr = m_sysConfig.GetString("LoadBalancingListenerAddr", "127.0.0.1");
                int port = m_sysConfig.GetInt("LoadBalancingListenerAddrPort", 14000);
                m_loadMigrationListener = new LoadMigrationListener(addr, port, this, m_log);
                m_loadMigrationListener.Start();

                m_log.Debug("Scene's LB connector portal: local - "
                    + ((IPEndPoint)m_client.Client.LocalEndPoint).Address.ToString() + ":" + ((IPEndPoint)m_client.Client.LocalEndPoint).Port
                    + "; remote -" 
                    + ((IPEndPoint)m_client.Client.RemoteEndPoint).Address.ToString() + ":"+((IPEndPoint)m_client.Client.RemoteEndPoint).Port);
                //reply back with the ip:port
                OSDMap data = new OSDMap(2);
                data["ip"] = OSD.FromString(addr);
                data["port"] = OSD.FromInteger(port);
                Send(new RegionSyncMessage(RegionSyncMessage.MsgType.LoadMigrationListenerInitiated, OSDParser.SerializeJsonString(data)));
            }
            else
            {
                //otherwise, this script engine is already expecting incoming load, should reject the notice
                //TO FINISH
            }

        }

        public void LoadObjectsToValidLocalScene(string regionName)
        {
            
            m_validLocalScene.LoadPrimsFromStorageInGivenSpace(regionName, m_subscribedQuarks.MinX, m_subscribedQuarks.MinY, m_subscribedQuarks.MaxX, m_subscribedQuarks.MaxY);   

            //set the LocX,LocY information of each object's copy, so that later we can figure out which SEtoSceneConnector to forwards setproperty request
            OpenSim.Services.Interfaces.GridRegion regionInfo = m_validLocalScene.GridService.GetRegionByName(UUID.Zero, regionName);
            List<EntityBase> entities = m_validLocalScene.GetEntities();
            foreach (EntityBase group in entities)
            {
                if (group is SceneObjectGroup)
                {
                    SceneObjectGroup sog = (SceneObjectGroup)group;
                    //seems the returned regionInfo.RegionLocX is the absolute coordinates. need to convert it to the region loc values (e.g. 1000,1000)
                    sog.LocX = (uint)regionInfo.RegionLocX / Constants.RegionSize;
                    sog.LocY = (uint)regionInfo.RegionLocY / Constants.RegionSize;
                }
            }
        }


        public void StartSyncAfterLoadMigration()
        {
            //First, register new status with Scene (that it is sync'ing now, not idle anymore)
            RegionSyncMessage msg = new RegionSyncMessage(RegionSyncMessage.MsgType.ActorStatus, Convert.ToString((int)ActorStatus.Sync));
            Send(msg);
            
            //Second, modify the local record, from an idle connector to a busy sync'ing one
            ScriptEngineToSceneConnectorModule connectorModule = GetSEToSceneConnectorMasterModule();
            connectorModule.RecordSyncStartAfterLoadMigration(this);

            //Next, send the quark list to Scene, and start syncing (download terrian, start script instances, etc)
            SendQuarkSubscription();
            Thread.Sleep(100);
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.GetTerrain));
            Thread.Sleep(100);

            m_log.Debug(LogHeader + ": next start script instances");
            //m_validLocalScene.CreateScriptInstances();

            List<EntityBase> entities = m_validLocalScene.GetEntities();
            foreach (EntityBase group in entities)
            {
                if (group is SceneObjectGroup)
                {
                    ((SceneObjectGroup)group).RootPart.ParentGroup.CreateScriptInstances(0, false, m_validLocalScene.DefaultScriptEngine, 1);
                    ((SceneObjectGroup)group).ResumeScripts();
                }
            }
        }

#endregion Load migration functions: load receiving side
    }
        

    public class LoadMigrationListener
    {
        // The listener and the thread which listens for connections from overloaded script engines
        private TcpListener m_listener;
        private Thread m_listenerThread;
        //private TcpClient m_tcpClient;
        private LoadMigrationEndPoint m_loadMigrationReceiver = null;
        private IPAddress m_addr;
        private int m_port;
        private ILog m_log;
        private string LogHeader = "LoadMigrationListener";
        private ScriptEngineToSceneConnector m_seToSceneConnector;

        public LoadMigrationListener(string ip, int port, ScriptEngineToSceneConnector sceneConnector, ILog log)
        {
            m_addr = IPAddress.Parse(ip);
            m_port = port;
            m_log = log;
            m_seToSceneConnector = sceneConnector;

            m_log.Debug(LogHeader + ": to create a new LoadMigrationListener on " + m_addr + "," + m_port);
        }

        // Start the server
        public void Start()
        {
            m_listenerThread = new Thread(new ThreadStart(Listen));
            m_listenerThread.Name = "LoadMigrationListener Listener";
            m_log.WarnFormat(LogHeader + ": Starting {0} thread", m_listenerThread.Name);
            m_listenerThread.Start();
            //m_log.Warn("[REGION SYNC SERVER] Started");
        }

        private void Listen()
        {
            m_listener = new TcpListener(m_addr, m_port);

            try
            {
                // Start listening for clients
                m_listener.Start();
                while (true)
                {
                    // *** Move/Add TRY/CATCH to here, but we don't want to spin loop on the same error
                    m_log.WarnFormat(LogHeader + ": Listening for new connections on ip {0} port {1}...", m_addr.ToString(), m_port.ToString());
                    TcpClient tcpClient = m_listener.AcceptTcpClient();

                    IPAddress addr = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address;
                    int port = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Port;

                    m_log.Debug(LogHeader + ": accepted new client - " + addr.ToString() + ", " + port);

                    m_loadMigrationReceiver = new LoadMigrationEndPoint(tcpClient, m_seToSceneConnector,  m_log); 
                }
            }
            catch (SocketException e)
            {
                m_log.Warn(LogHeader + " [Listen] SocketException");
            }
        }

        public void Shutdown()
        {
            m_listener.Stop();
            m_listenerThread.Abort();
            if(m_loadMigrationReceiver!=null)
                m_loadMigrationReceiver.Shutdown();
        }
    }

    //The tcp EndPoint of either end of load migration (source or receiving side).
    public class LoadMigrationEndPoint
    {
        private Thread m_receive_loop;
        private TcpClient m_tcpclient;
        private ILog m_log;
        private ScriptEngineToSceneConnector m_seToSceneConnector;
        //private int m_minX;
        //private int m_minY;
        //private int m_maxX;
        //private int m_maxY;
        //private int QuarkInfo.SizeX;
        //private int QuarkInfo.SizeY;
        private QuarkSubsriptionInfo m_quarks;
        private bool m_inScriptStateSync = false;
        private Scene m_validLocalScene = null;

        public QuarkSubsriptionInfo QuarksInfo
        {
            get { return m_quarks; }
            set { m_quarks = value; }
        }

        public LoadMigrationEndPoint(TcpClient tcpClient, QuarkSubsriptionInfo quarks, ScriptEngineToSceneConnector sceneConnector, ILog log)
        {
            m_log = log;
            m_tcpclient = tcpClient;
            m_seToSceneConnector = sceneConnector;
            m_quarks = quarks;
            //QuarkInfo.SizeX = quarks.QuarkSizeX;
            //QuarkInfo.SizeY = quarks.QuarkSizeY;

            m_receive_loop = new Thread(new ThreadStart(delegate() { ReceiveLoop(); }));
            m_receive_loop.Name = "LoadMigrationReceiver";
            //m_log.WarnFormat("{0} Started thread: {1}", LogHeader, m_receive_loop.Name);
            m_receive_loop.Start();

            m_validLocalScene = sceneConnector.GetValidLocalScene();
        }

        public LoadMigrationEndPoint(TcpClient tcpClient, ScriptEngineToSceneConnector sceneConnector, ILog log)
        {
            m_log = log;
            m_tcpclient = tcpClient;
            m_seToSceneConnector = sceneConnector;
            m_validLocalScene = sceneConnector.GetValidLocalScene();
            //QuarkInfo.SizeX = quarkSizeX;
            //QuarkInfo.SizeY = quarkSizeY;

            m_receive_loop = new Thread(new ThreadStart(delegate() { ReceiveLoop(); }));
            m_receive_loop.Name = "LoadMigrationReceiver";
            //m_log.WarnFormat("{0} Started thread: {1}", LogHeader, m_receive_loop.Name);
            m_receive_loop.Start();
        }

        public void SetQuarksInfo (QuarkSubsriptionInfo quarks){
            m_quarks = quarks;
        }

        private void ReceiveLoop()
        {
            try
            {
                while (true)
                {
                    RegionSyncMessage msg = new RegionSyncMessage(m_tcpclient.GetStream());
                    HandleMessage(msg);
                }
            }
            catch (Exception e)
            {
                //m_log.WarnFormat("LoadMigrationEndPoint: has disconnected: {1}", e.Message);
                m_log.Warn("LoadMigrationEndPoint: has disconnected:");
            }
            Shutdown();
        }

        public void Shutdown()
        {
            // Abort ReceiveLoop Thread, close Socket and TcpClient
            m_receive_loop.Abort();
            m_tcpclient.Client.Close();
            m_tcpclient.Close();
        }

        private void HandleMessage(RegionSyncMessage msg)
        {
            switch (msg.Type)
            {
                case RegionSyncMessage.MsgType.MigrationSpace:
                    HandleMigrationSpaceNotice(msg);
                    break;
                case RegionSyncMessage.MsgType.ScriptStateSyncRequest:
                    HandleScriptStateSyncRequest(msg);
                    break;
                case RegionSyncMessage.MsgType.ScriptStateSyncStart:
                    m_log.Debug("LoadMigration receiving end: received ScriptStateSyncStart");
                    m_inScriptStateSync = true;
                    break;
                case RegionSyncMessage.MsgType.ScriptStateSyncPerObject:
                    HandleScriptStateSyncPerObject(msg);
                    break;
                case RegionSyncMessage.MsgType.ScriptStateSyncEnd:
                    HandleScriptStateSyncEnd(msg);
                    break;

                default:
                    break;
            }
        }

        private void HandleMigrationSpaceNotice(RegionSyncMessage msg)
        {
            //get the space description, 
            OSDMap data = DeserializeMessage(msg);
            if (!data.ContainsKey("minX") || !data.ContainsKey("minY") || !data.ContainsKey("maxX") || !data.ContainsKey("maxY") || !data.ContainsKey("regionName"))
            {
                m_log.Warn("Load migration receiving end : parameters missing in MigratingSpace message from migration source, need to have (minX,minY),(maxX,maxY),regionName");
                return;
            }
            int minX = (int) data["minX"].AsUInteger();
            int minY = (int) data["minY"].AsUInteger();
            int maxX = (int) data["maxX"].AsUInteger();
            int maxY = (int) data["maxY"].AsUInteger();
            string regionName = data["regionName"].AsString();
            m_log.Debug("Load migration receiving end: space migrating in -- (" + minX + "," + minY + ")" + ", (" + maxX + "," + maxY + ")");

            //set the quark subscription information
            m_quarks = new QuarkSubsriptionInfo( minX, minY, maxX, maxY);
            m_seToSceneConnector.SetQuarkSubscription(m_quarks);

            //load the objects into local scene 
            //TO FINISH: only load the objects with scripts, but not creating script instances yet
            m_seToSceneConnector.LoadObjectsToValidLocalScene(regionName);
            
            //request script state from the migration source 
            //RegionSyncMessage msg = new RegionSyncMessage(RegionSyncMessage.MsgType.ScriptStateSyncRequest, OSDParser.SerializeJsonString(data));
            RegionSyncMessage outMsg = new RegionSyncMessage(RegionSyncMessage.MsgType.ScriptStateSyncRequest);
            Send(outMsg);
        }

        //Upon receiving a ScriptStateSyncRequest from the receiving side, the source needs to suspend script execution, save 
        //the script's state, and migrate the state to the receiving end.
        private void HandleScriptStateSyncRequest(RegionSyncMessage msg)
        {
            List<EntityBase> entities = m_validLocalScene.GetEntities();
            List<SceneObjectGroup> sogInMigrationSpace = new List<SceneObjectGroup>();

            //Inform the receiving end the script state synchronization now starts.
            m_inScriptStateSync = true;
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.ScriptStateSyncStart));

            foreach (EntityBase e in entities)
            {
                if (e is SceneObjectGroup)
                {
                    SceneObjectGroup sog = (SceneObjectGroup)e;

                    //m_log.Debug("check object " + sog.UUID + ", at position " + sog.AbsolutePosition.ToString());

                    if (m_quarks.IsInSpace(sog.AbsolutePosition))
                    {
                        //m_log.Debug("object " + sog.UUID + " in space of " + m_quarks.StringRepresentation());

                        sogInMigrationSpace.Add(sog);
                        sog.SuspendScripts();
                        string scriptStateXml = sog.GetStateSnapshot();
                        OSDMap data = new OSDMap(2);
                        data["UUID"] = OSD.FromUUID(sog.UUID);
                        data["state"] = OSD.FromString(scriptStateXml);
                        RegionSyncMessage stateMsg = new RegionSyncMessage(RegionSyncMessage.MsgType.ScriptStateSyncPerObject, OSDParser.SerializeJsonString(data));
                        Send(stateMsg);
                    }
                }
            }

            //Inform the receiving end the script state synchronization now ends.
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.ScriptStateSyncEnd));
            m_inScriptStateSync = false;

            //Next, remove the space, objects/scripts in the space, 
            foreach (SceneObjectGroup sog in sogInMigrationSpace)
            {
                m_validLocalScene.DeleteSceneObject(sog, true);
            }
            //udpate the quark subscriptions, and informs Scene about the quark subscription changes
            m_seToSceneConnector.RemoveQuarks(m_quarks);
        }

        private void HandleScriptStateSyncPerObject(RegionSyncMessage msg)
        {
            OSDMap data = DeserializeMessage(msg);
            if (!data.ContainsKey("UUID") || !data.ContainsKey("state"))
            {
                m_log.Warn("LoadMigrationReceiver: ScriptStateSyncPerObject message missing parameters, need object's UUID and its script's state (string)");
                return;
            }

            UUID objID = data["UUID"].AsUUID();
            string stateXml = data["state"].AsString();

            SceneObjectGroup sog = m_validLocalScene.GetSceneObjectPart(objID).ParentGroup;
            sog.SetState(stateXml, m_validLocalScene);

            //m_log.Debug("LoadMigration receiving end: set state for object " + objID + ", script state: " + stateXml);
        }

        private void HandleScriptStateSyncEnd(RegionSyncMessage msg)
        {
            m_log.Debug("LoadMigration receiving end: received ScriptStateSyncEnd");
            m_inScriptStateSync = false;

            //Start sync'ing state with Scene: send quark subscription, get terrian, and start script execution
            m_seToSceneConnector.StartSyncAfterLoadMigration();
        }

        private OSDMap DeserializeMessage(RegionSyncMessage msg)
        {
            OSDMap data = null;
            try
            {
                data = OSDParser.DeserializeJson(Encoding.ASCII.GetString(msg.Data, 0, msg.Length)) as OSDMap;
            }
            catch (Exception e)
            {
                m_log.Error("LoadMigrationEndPoint " + Encoding.ASCII.GetString(msg.Data, 0, msg.Length));
                data = null;
            }
            return data;
        }

        
        public void Send(RegionSyncMessage msg)
        {
            byte[] data = msg.ToBytes();
            if (m_tcpclient.Connected)
            {
                try
                {
                    m_tcpclient.GetStream().Write(data, 0, data.Length);
                }
                // If there is a problem reading from the client, shut 'er down. 
                // *** Still need to alert the module that it's no longer connected!
                catch
                {
                    Shutdown();
                }
            }
        }
        
    }
}
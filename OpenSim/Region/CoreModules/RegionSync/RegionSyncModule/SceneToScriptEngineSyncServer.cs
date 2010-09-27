using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using log4net;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Region.Framework.Scenes.Serialization;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    public class QuarkInfo
    {
        public static int SizeX = -1; //the length along X axis, should be same for all quarks, need to be set by either the SyncServer on Scene, or by an ActorToSceneConnectorModule.
        public static int SizeY = -1; //the length along Y axis, should be same for all quarks

        public int PosX = 0; //the offset position of the left-bottom corner (0~255)
        public int PosY = 0;

        private ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private int maxX, maxY;

        //this field is only meaning for the QuarkInfo records on the Scene side
        private SceneToScriptEngineConnector m_seConnector=null;
        public SceneToScriptEngineConnector SEConnector
        {
            get { return m_seConnector; }
            set { m_seConnector = value; }
        }

        private string m_quarkString = "";
        public string QuarkStringRepresentation
        {
            get { return m_quarkString;}
        }

        //public QuarkInfo(int x, int y, int sizeX, int sizeY)
        public QuarkInfo(int x, int y)
        {
            PosX = x;
            PosY = y;
            //SizeX = sizeX;
            //SizeY = sizeY;
            maxX = PosX + SizeX;
            maxY = PosY + SizeY;
            m_quarkString = PosX + "_" + PosY;
        }

        //public QuarkInfo(string xy, int sizeX, int sizeY)
        public QuarkInfo(string xy)
        {
            string[] coordinate = xy.Split(new char[] { '_' });
            if (coordinate.Length < 2)
            {
                m_log.Warn("[QUARK INFO] QuarkInfo Constructor: missing x or y value, format expected x_y: " + xy); 
                return;
            }
            PosX = Convert.ToInt32(coordinate[0]);
            PosY = Convert.ToInt32(coordinate[1]);
            //SizeX = sizeX;
            //SizeY = sizeY;
            maxX = PosX + SizeX;
            maxY = PosY + SizeY;
            m_quarkString = PosX + "_" + PosY;
        }

        //public void SetPartitionedSceneInfo(int x, int y, int sizeX, int sizeY)
        public void SetPartitionedSceneInfo(int x, int y)
        {
            PosX = x;
            PosY = y;
            //SizeX = sizeX;
            //SizeY = sizeY;
            maxX = PosX + SizeX;
            maxY = PosY + SizeY;
            m_quarkString = PosX + "_" + PosY;
        }

        public bool IsPositionInQuarkSpace(Vector3 pos)
        {
            if (pos.X >= PosX && pos.X <= maxX && pos.Y >= PosY && pos.Y <= maxY)
                return true;
            else
                return false;
        }

    }

    //Information of a registered idle script engine.
    //Note, this is a temporary solution to inlcude idle script engines here. 
    //In the future, there might be a independent load balaner that keeps track
    //of available idle hardware.
    public class IdleScriptEngineInfo
    {
        public TcpClient TClient;
        //public IPAddress ScriptEngineIPAddr;
        //public int ScriptEnginePort;
        public string ID;

        //Will be used to store the overloaded SE that has send LB request and paired with this idle SE
        public SceneToScriptEngineConnector AwaitOverloadedSE=null; 

        public IdleScriptEngineInfo(TcpClient tclient)
        {
            if(tclient==null) return;
            TClient = tclient;
            IPAddress ipAddr = ((IPEndPoint)tclient.Client.RemoteEndPoint).Address;
            int port = ((IPEndPoint)tclient.Client.RemoteEndPoint).Port;
            ID = ipAddr.ToString()+":"+port;
        }
    }

    //Here is the per actor type listening server for Script Engines.
    public class SceneToScriptEngineSyncServer
    {
        #region SceneToScriptEngineSyncServer members
        // Set the addr and port for TcpListener
        private IPAddress m_addr;
        private Int32 m_port;

        private int seCounter;

        // The local scene.
        private Scene m_scene;

        private ILog m_log;

        // The listener and the thread which listens for connections from client managers
        private TcpListener m_listener;
        private Thread m_listenerThread;

        private object m_scriptEngineConnector_lock = new object();
        //private Dictionary<string, SceneToScriptEngineConnector> m_scriptEngineConnectors = new Dictionary<string, SceneToScriptEngineConnector>();
        private List<SceneToScriptEngineConnector> m_scriptEngineConnectors = new List<SceneToScriptEngineConnector>();

        //list of idle script engines that have registered.
        private List<IdleScriptEngineInfo> m_idleScriptEngineList = new List<IdleScriptEngineInfo>();

        //List of all quarks, each using the concatenation of x,y values of its left-bottom corners, where the x,y values are the offset 
        //position in the scene.
        private Dictionary<string, QuarkInfo> m_quarksInScene = new Dictionary<string, QuarkInfo>();

        private string LogHeader = "[SCENE TO SCRIPT ENGINE SYNC SERVER]";

        //Quark related info
        //private int QuarkInfo.SizeX;
        //private int QuarkInfo.SizeY;

        // Check if any of the client views are in a connected state
        public bool Synced
        {
            get
            {
                return (m_scriptEngineConnectors.Count > 0);
            }
        }



        #endregion

        // Constructor
        public SceneToScriptEngineSyncServer(Scene scene, string addr, int port)
        {
            if (QuarkInfo.SizeX == -1 || QuarkInfo.SizeY == -1)
            {
                m_log.Error(LogHeader + " QuarkInfo.SizeX or QuarkInfo.SizeY has not been configured yet.");
                Environment.Exit(0); ;
            }

            m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            //m_log.Warn(LogHeader + "Constructed");
            m_scene = scene;
            m_addr = IPAddress.Parse(addr);
            m_port = port;

            InitQuarksInScene();
            SubscribeToEvents();
        }


        private void SubscribeToEvents()
        {
            m_scene.EventManager.OnRezScript += SESyncServerOnRezScript;
            m_scene.EventManager.OnScriptReset += SESyncServerOnScriptReset;
            m_scene.EventManager.OnUpdateScript += SESyncServerOnUpdateScript;
        }

        private void UnSubscribeToEvents()
        {
            m_scene.EventManager.OnRezScript -= SESyncServerOnRezScript;
            m_scene.EventManager.OnScriptReset -= SESyncServerOnScriptReset;
            m_scene.EventManager.OnUpdateScript -= SESyncServerOnUpdateScript;
        }

        // Start the server
        public void Start()
        {
            m_listenerThread = new Thread(new ThreadStart(Listen));
            m_listenerThread.Name = "SceneToScriptEngineSyncServer Listener";
            m_log.WarnFormat(LogHeader + ": Starting {0} thread", m_listenerThread.Name);
            m_listenerThread.Start();
            //m_log.Warn("[REGION SYNC SERVER] Started");
        }



        // Stop the server and disconnect all RegionSyncClients
        public void Shutdown()
        {
            // Stop the listener and listening thread so no new clients are accepted
            m_listener.Stop();
            m_listenerThread.Abort();
            m_listenerThread = null;

            // Stop all existing SceneTOSEConnectors 
            //TO FINISH
            foreach (SceneToScriptEngineConnector seConnector in m_scriptEngineConnectors)
            {
                seConnector.Shutdown();
            }
            m_scriptEngineConnectors.Clear();

            UnSubscribeToEvents();
        }

        private void InitQuarksInScene()
        {
            List<QuarkInfo> quarkList = RegionSyncUtil.GetAllQuarksInScene();
            foreach (QuarkInfo quark in quarkList)
            {
                m_quarksInScene.Add(quark.QuarkStringRepresentation, quark);
            }
        }

        public void RegisterQuarkSubscription(List<QuarkInfo> quarkSubscriptions, SceneToScriptEngineConnector seConnector)
        {
            foreach (QuarkInfo quark in quarkSubscriptions)
            {
                string quarkID = quark.QuarkStringRepresentation;
                m_quarksInScene[quarkID].SEConnector = seConnector;
                m_log.Debug(LogHeader + ": " + quarkID + " subscribed by "+seConnector.Description);
            }
        }

        // Add a connector to a script engine
        public void AddSyncedScriptEngine(SceneToScriptEngineConnector seConnector)
        {
            lock (m_scriptEngineConnector_lock)
            {
                //Dictionary<string, SceneToScriptEngineConnector> currentlist = m_scriptEngineConnectors;
                //Dictionary<string, SceneToScriptEngineConnector> newlist = new Dictionary<string, SceneToScriptEngineConnector>(currentlist);
                m_scriptEngineConnectors.Add(seConnector);
                // Threads holding the previous version of the list can keep using it since
                // they will not hold it for long and get a new copy next time they need to iterate
                //m_scriptEngineConnectors = newlist;
            }
        }

        // Remove the client view from the list and decrement synced client counter
        public void RemoveSyncedScriptEngine(SceneToScriptEngineConnector seConnector)
        {
            lock (m_scriptEngineConnector_lock)
            {
                //Dictionary<string, SceneToScriptEngineConnector> currentlist = m_scriptEngineConnectors;
                //Dictionary<string, SceneToScriptEngineConnector> newlist = new Dictionary<string, SceneToScriptEngineConnector>(currentlist);
                m_scriptEngineConnectors.Remove(seConnector);
                // Threads holding the previous version of the list can keep using it since
                // they will not hold it for long and get a new copy next time they need to iterate
                //m_scriptEngineConnectors = newlist;
            }
        }

        // Listen for connections from a new RegionSyncClient
        // When connected, start the ReceiveLoop for the new client
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
                    m_log.WarnFormat(LogHeader + ": Listening for new connections on port {0}...", m_port.ToString());
                    TcpClient tcpclient = m_listener.AcceptTcpClient();
                    IPAddress addr = ((IPEndPoint)tcpclient.Client.RemoteEndPoint).Address;
                    int port = ((IPEndPoint)tcpclient.Client.RemoteEndPoint).Port;

                    ActorStatus actorStatus = GetActorStatus(tcpclient);

                    switch (actorStatus)
                    {
                        case ActorStatus.Sync:
                            // Add the SceneToScriptEngineConnector to the list 
                            SceneToScriptEngineConnector sceneToSEConnector = new SceneToScriptEngineConnector(++seCounter, m_scene, tcpclient, this);
                            AddSyncedScriptEngine(sceneToSEConnector);
                            break;
                        case ActorStatus.Idle:
                            IdleScriptEngineInfo idleSE = new IdleScriptEngineInfo(tcpclient);
                            m_log.Debug(": adding an idle SE ("+addr+","+port+")");
                            m_idleScriptEngineList.Add(idleSE);
                            break;
                        default:
                            break;
                    }

                }
            }
            catch (SocketException e)
            {
                m_log.WarnFormat(LogHeader + " [Listen] SocketException: {0}", e);
            }
        }

        /*
        public void RegisterSyncedScriptEngine(SceneToScriptEngineConnector sceneToSEConnector)
        {
            //first, remove it from the idle list
            m_idleScriptEngineList.Remove(sceneToSEConnector);

            //now, added to the synced SE list
            AddSyncedScriptEngine(sceneToSEConnector);
        }
         * */ 


        // Broadcast a message to all connected RegionSyncClients
        public void SendToAllConnectedSE(RegionSyncMessage msg)
        {
            if (m_scriptEngineConnectors.Count > 0)
            {
                m_log.Debug(LogHeader + ": region " + m_scene.RegionInfo.RegionName + " Broadcast to ScriptEngine, msg " + msg.Type);
                foreach (SceneToScriptEngineConnector seConnector in m_scriptEngineConnectors)
                {
                    seConnector.Send(msg);
                }
            }

        }

        //TO FINISH: Find the right SceneToSEConnector to forward the message
        public void SendToSE(RegionSyncMessage.MsgType msgType, SceneObjectGroup sog)
        {
            SceneToScriptEngineConnector seConnector = GetSceneToSEConnector(sog);
            if (seConnector != null)
            {
                seConnector.SendObjectUpdate(msgType, sog);
            }
            else
            {
                m_log.Warn(LogHeader + sog.AbsolutePosition.ToString() + " not covered by any script engine");
            }
        }

        //This is to send a message, rsm, to script engine, and the message is about object SOG. E.g. RemovedObject
        public void SendToSE(RegionSyncMessage rsm, SceneObjectGroup sog)
        {
            SceneToScriptEngineConnector seConnector = GetSceneToSEConnector(sog);
            if (seConnector != null)
            {
                seConnector.Send(rsm);
            }
            else
            {
                m_log.Warn(LogHeader + sog.AbsolutePosition.ToString() + " not covered by any script engine");
            }
        }


        private SceneToScriptEngineConnector GetSceneToSEConnector(SceneObjectGroup sog)
        {
            if (sog==null)
            {
                return m_scriptEngineConnectors[0];
            }
            else 
            {
                //Find the right SceneToSEConnector by the object's position
                //TO FINISH: Map the object to a quark first, then map the quark to SceneToSEConnector
                string quarkID = RegionSyncUtil.GetQuarkIDByPosition(sog.AbsolutePosition);
                SceneToScriptEngineConnector seConnector = m_quarksInScene[quarkID].SEConnector;
                return seConnector;
            }

        }

        private ActorStatus GetActorStatus(TcpClient tcpclient)
        {
            m_log.Debug(LogHeader+ ": Get Actor status");

            RegionSyncMessage msg = new RegionSyncMessage(tcpclient.GetStream());
            ActorStatus actorStatus;
            switch (msg.Type)
            {
                case RegionSyncMessage.MsgType.ActorStatus:
                    {
                        string status = Encoding.ASCII.GetString(msg.Data, 0, msg.Length);
                        m_log.Debug(LogHeader + ": recv status: " + status);
                        actorStatus = (ActorStatus)Convert.ToInt32(status);
                        break;
                    }
                default:
                    {
                        m_log.Error(LogHeader + ": Expect Message Type: ActorStatus");
                        RegionSyncMessage.HandleError("[REGION SYNC SERVER]", msg, String.Format("{0} Expect Message Type: ActorType", "[REGION SYNC SERVER]"));
                        return ActorStatus.Null;
                    }
            }
            return actorStatus;
        }


        #region Event Handlers
      
        private void SESyncServerOnRezScript(uint localID, UUID itemID, string script, int startParam, bool postOnRez, string engine, int stateSource)
        {
            SceneObjectGroup sog = m_scene.GetSceneObjectPart(localID).ParentGroup;
            if (sog != null)
            {
                //First, figure out which script engine to forward the event
                SceneToScriptEngineConnector seConnector = GetSceneToSEConnector(sog);
                if (seConnector != null)
                {
                    m_log.Debug(LogHeader + ": Caught event OnRezScript, send message to Script Engine " + seConnector.Description);

                    seConnector.SEConnectorOnRezScript(localID, itemID, script, startParam, postOnRez, engine, stateSource);
                }
            }
        }

        private void SESyncServerOnScriptReset(uint localID, UUID itemID)
        {

            SceneObjectGroup sog = m_scene.GetSceneObjectPart(localID).ParentGroup;
            if (sog != null)
            {
                //First, figure out which script engine to forward the event
                SceneToScriptEngineConnector seConnector = GetSceneToSEConnector(sog);
                if (seConnector != null)
                {
                    m_log.Debug(LogHeader + ": Caught event OnScriptReset, send message to Script Engine " + seConnector.Description);

                    seConnector.SEConnectorOnScriptReset(localID, itemID);
                }
            }
        }

        private void SESyncServerOnUpdateScript(UUID agentID, UUID itemId, UUID primId, bool isScriptRunning, UUID newAssetID)
        {
            SceneObjectGroup sog = m_scene.GetSceneObjectPart(primId).ParentGroup;
            if (sog != null)
            {
                //First, figure out which script engine to forward the event
                SceneToScriptEngineConnector seConnector = GetSceneToSEConnector(sog);
                if (seConnector != null)
                {
                    m_log.Debug(LogHeader + ": Caught event OnScriptReset, send message to Script Engine " + seConnector.Description);

                    seConnector.SEConnectorOnUpdateScript(agentID, itemId, primId, isScriptRunning, newAssetID);
                }
            }
        }

        #endregion Event Handlers

        #region Load balancing members and functions
        //keep track of idle script engines that are in the process of load balancing (they are off the idle list, but not a working script engine yet (not sync'ing with Scene yet)).
        private Dictionary<string, IdleScriptEngineInfo> m_loadBalancingIdleSEs = new Dictionary<string,IdleScriptEngineInfo>(); 
        public void HandleLoadBalanceRequest(SceneToScriptEngineConnector seConnctor)
        {
            //Let's start a thread to do the job, so that we can return quickly and don't block on ReceiveLoop()

            Thread partitionThread = new Thread(new ParameterizedThreadStart(TriggerLoadBalanceProcess));
            partitionThread.Name = "TriggerLoadBalanceProcess";
            partitionThread.Start((object)seConnctor);
        }

        public void TriggerLoadBalanceProcess(object arg)
        {
            SceneToScriptEngineConnector seConnctor = (SceneToScriptEngineConnector)arg;
            IdleScriptEngineInfo idleScriptEngineInfo = GetIdleScriptEngineConnector();
            if (idleScriptEngineInfo != null)
            {
                RegionSyncMessage msg = new RegionSyncMessage(RegionSyncMessage.MsgType.LoadMigrationNotice);
                Send(idleScriptEngineInfo.TClient, msg.ToBytes());
                m_log.Debug(LogHeader + ": HandleLoadBalanceRequest from " + seConnctor.Description + ", picked idle SE: " + idleScriptEngineInfo.ID);

                //keep track of which overload script engine is paired up with which idle script engine
                idleScriptEngineInfo.AwaitOverloadedSE = seConnctor;
                m_loadBalancingIdleSEs.Add(idleScriptEngineInfo.ID, idleScriptEngineInfo);

                m_log.Debug("ToSEConnector portal: local -" +
                    ((IPEndPoint)idleScriptEngineInfo.TClient.Client.LocalEndPoint).Address.ToString() + ":" + ((IPEndPoint)idleScriptEngineInfo.TClient.Client.LocalEndPoint).Port
                    + "; remote - " + ((IPEndPoint)idleScriptEngineInfo.TClient.Client.RemoteEndPoint).Address.ToString() + ":"
                    + ((IPEndPoint)idleScriptEngineInfo.TClient.Client.RemoteEndPoint).Port);

                //Now we expect the idle script engine to reply back
                msg = new RegionSyncMessage(idleScriptEngineInfo.TClient.GetStream());
                if (msg.Type != RegionSyncMessage.MsgType.LoadMigrationListenerInitiated)
                {
                    m_log.Warn(LogHeader + ": should receive a message of type LoadMigrationListenerInitiated, but received " + msg.Type.ToString());
                }
                else
                {
                    //Before the load is migrated from overloaded script engine to the idle engine, sync with the DB to update the state in DB
                    List<EntityBase> entities = m_scene.GetEntities();
                    foreach (EntityBase entity in entities)
                    {
                        if (!entity.IsDeleted && entity is SceneObjectGroup && ((SceneObjectGroup)entity).HasGroupChanged)
                        {
                            m_scene.ForceSceneObjectBackup((SceneObjectGroup)entity);
                        }
                    }

                    OSDMap data = DeserializeMessage(msg);
                    if (!data.ContainsKey("ip") || !data.ContainsKey("port") )
                    {
                        m_log.Warn(LogHeader + ": parameters missing in SceneLocation message from Scene, need to have ip, port");
                        return;
                    }
                    //echo the information back to the overloaded script engine
                    seConnctor.Send(new RegionSyncMessage(RegionSyncMessage.MsgType.LoadBalanceResponse, OSDParser.SerializeJsonString(data)));

                    m_log.Debug(LogHeader + " now remove script engine " + idleScriptEngineInfo.ID + " from idle SE list, and create SceneToScriptEngineConnector to it");
                    //create a SceneToSEConnector for the idle script engine, who will be sync'ing with this SyncServer soon
                    SceneToScriptEngineConnector sceneToSEConnector = new SceneToScriptEngineConnector(++seCounter, m_scene, idleScriptEngineInfo.TClient, this);
                    //Now remove the script engine from the idle SE list
                    m_idleScriptEngineList.Remove(idleScriptEngineInfo);
                    //AddSyncedScriptEngine(sceneToSEConnector);
                }

            }
            else
            {
                seConnctor.SendLoadBalanceRejection("no idle script engines");
            }
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

        private void Send(TcpClient tcpclient, byte[] data)
        {
            if (tcpclient.Connected)
            {
                try
                {
                    tcpclient.GetStream().BeginWrite(data, 0, data.Length, ar =>
                    {
                        if (tcpclient.Connected)
                        {
                            try
                            {
                                tcpclient.GetStream().EndWrite(ar);
                            }
                            catch (Exception)
                            { }
                        }
                    }, null);
                }
                catch (IOException)
                {
                    m_log.WarnFormat("{0} Script Engine has disconnected.", LogHeader);
                }
            }
        }

        private IdleScriptEngineInfo GetIdleScriptEngineConnector()
        {
            if (m_idleScriptEngineList.Count == 0)
                return null;
            IdleScriptEngineInfo idleSEInfo = m_idleScriptEngineList[0];
            m_idleScriptEngineList.Remove(idleSEInfo);
            return idleSEInfo;
        }

        #endregion Load balancing functions 
    }
}

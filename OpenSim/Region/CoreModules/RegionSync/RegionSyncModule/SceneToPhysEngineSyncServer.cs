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
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Framework.InterfaceCommander;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using log4net;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    //Information of a registered idle physics engine.
    //Note, this is a temporary solution to inlcude idle physics engines here. 
    //In the future, there might be a independent load balaner that keeps track
    //of available idle hardware.
    public class IdlePhysEngineInfo
    {
        public TcpClient TClient;
        //public IPAddress PhysEngineIPAddr;
        //public int PhysEnginePort;
        public string ID;

        //Will be used to store the overloaded PE that has send LB request and paired with this idle PE
        public SceneToPhysEngineConnector AwaitOverloadedSE=null; 

        public IdlePhysEngineInfo(TcpClient tclient)
        {
            if(tclient==null) return;
            TClient = tclient;
            IPAddress ipAddr = ((IPEndPoint)tclient.Client.RemoteEndPoint).Address;
            int port = ((IPEndPoint)tclient.Client.RemoteEndPoint).Port;
            ID = ipAddr.ToString()+":"+port;
        }
    }

    //Here is the per actor type listening server for physics Engines.
    public class SceneToPhysEngineSyncServer : ISceneToPhysEngineServer, ICommandableModule
    {
        #region SceneToPhysEngineSyncServer members
        // Set the addr and port for TcpListener
        private IPAddress m_addr;
        private Int32 m_port;

        //this field is only meaning for the QuarkInfo records on the Scene side
        private SceneToPhysEngineConnector m_peConnector=null;
        public SceneToPhysEngineConnector PEConnector
        {
            get { return m_peConnector; }
            set { m_peConnector = value; }
        }

        private int peCounter;

        // static counters that are used to compute global configuration state
        private static int m_syncServerInitialized = 0;
        private static int m_totalConnections = 0;
        private static List<Scene> m_allScenes = new List<Scene>();

        // The local scene.
        private Scene m_scene;

        private ILog m_log;

        // The listener and the thread which listens for connections from client managers
        private TcpListener m_listener;
        private Thread m_listenerThread;

        private object m_physEngineConnector_lock = new object();
        //private Dictionary<string, SceneToPhysEngineConnector> m_physEngineConnectors = new Dictionary<string, SceneToPhysEngineConnector>();
        private List<SceneToPhysEngineConnector> m_physEngineConnectors = new List<SceneToPhysEngineConnector>();
        // the last connector created
        private SceneToPhysEngineConnector m_sceneToPhysEngineConnector = null;

        //list of idle physics engines that have registered.
        private List<IdlePhysEngineInfo> m_idlePhysEngineList = new List<IdlePhysEngineInfo>();

        //List of all quarks, each using the concatenation of x,y values of its left-bottom corners, 
        // where the x,y values are the offset position in the scene.
        private Dictionary<string, QuarkInfo> m_quarksInScene = new Dictionary<string, QuarkInfo>();

        private string LogHeader = "[SCENE TO PHYS ENGINE SYNC SERVER]";

        //Quark related info
        //private int QuarkInfo.SizeX;
        //private int QuarkInfo.SizeY;

        #region ICommandableModule Members
        private readonly Commander m_commander = new Commander("phys");
        public ICommander CommandInterface
        {
            get { return m_commander; }
        }

        private void InstallInterfaces()
        {
            // Command cmdSyncStart = new Command("start", CommandIntentions.COMMAND_HAZARDOUS, SyncStart, "Begins synchronization with RegionSyncServer.");
            //cmdSyncStart.AddArgument("server_port", "The port of the server to synchronize with", "Integer");
            
            // Command cmdSyncStop = new Command("stop", CommandIntentions.COMMAND_HAZARDOUS, SyncStop, "Stops synchronization with RegionSyncServer.");
            //cmdSyncStop.AddArgument("server_address", "The IP address of the server to synchronize with", "String");
            //cmdSyncStop.AddArgument("server_port", "The port of the server to synchronize with", "Integer");

            Command cmdSyncStatus = new Command("status", CommandIntentions.COMMAND_HAZARDOUS, SyncStatus, "Displays synchronization status.");

            //The following two commands are more for easier debugging purpose
            // Command cmdSyncSetQuarks = new Command("quarkSpace", CommandIntentions.COMMAND_HAZARDOUS, SetQuarkList, "Set the set of quarks to subscribe to. For debugging purpose. Should be issued before \"sync start\"");
            // cmdSyncSetQuarks.AddArgument("quarkSpace", "The (rectangle) space of quarks to subscribe, represented by x0_y0,x1_y1, the left-bottom and top-right corners of the rectangel space", "String");

            // Command cmdSyncSetQuarkSize = new Command("quarksize", CommandIntentions.COMMAND_HAZARDOUS, SetQuarkSize, "Set the size of each quark. For debugging purpose. Should be issued before \"sync quarks\"");
            // cmdSyncSetQuarkSize.AddArgument("quarksizeX", "The size on x axis of each quark", "Integer");
            // cmdSyncSetQuarkSize.AddArgument("quarksizeY", "The size on y axis of each quark", "Integer");

            // m_commander.RegisterCommand("start", cmdSyncStart);
            // m_commander.RegisterCommand("stop", cmdSyncStop);
            m_commander.RegisterCommand("status", cmdSyncStatus);
            // m_commander.RegisterCommand("quarkSpace", cmdSyncSetQuarks);

            lock (m_scene)
            {
                // Add this to our scene so scripts can call these functions
                m_scene.RegisterModuleCommander(m_commander);
            }
        }

        /// <summary>
        /// Processes commandline input. Do not call directly.
        /// </summary>
        /// <param name="args">Commandline arguments</param>
        private void EventManager_OnPluginConsole(string[] args)
        {
            if (args[0] == "phys")
            {
                if (args.Length == 1)
                {
                    m_commander.ProcessConsoleCommand("help", new string[0]);
                    return;
                }

                string[] tmpArgs = new string[args.Length - 2];
                int i;
                for (i = 2; i < args.Length; i++)
                    tmpArgs[i - 2] = args[i];

                m_commander.ProcessConsoleCommand(args[1], tmpArgs);
            }
        }

        private void SyncStart(Object[] args)
        {
            return;
        }
        private void SyncStop(Object[] args)
        {
            return;
        }
        private void SyncStatus(Object[] args)
        {
            lock (m_physEngineConnector_lock)
            {
                if (m_physEngineConnectors.Count == 0)
                {
                    m_log.Warn(LogHeader + " Not currently synchronized");
                    return;
                }
                m_log.Warn(LogHeader + " Synchronized");
                foreach (SceneToPhysEngineConnector pec in m_physEngineConnectors)
                {
                    m_log.Warn(pec.StatisticLine(true));
                }
            }
        }

        #endregion

        // Check if any of the client views are in a connected state
        public bool IsPhysEngineScene() { return SceneToPhysEngineSyncServer.IsPhysEngineScene2S(); }
        public bool IsActivePhysEngineScene() { return SceneToPhysEngineSyncServer.IsActivePhysEngineScene2S(); }
        public bool IsPhysEngineActor() { return SceneToPhysEngineSyncServer.IsPhysEngineActorS; }

        public bool Synced
        {
            get { return (m_physEngineConnectors.Count > 0); }
        }
        public static bool IsPhysEngineSceneS
        {
            get { return (SceneToPhysEngineSyncServer.m_syncServerInitialized > 0); }
        }
        public static bool IsPhysEngineScene2S()
        {
            return (SceneToPhysEngineSyncServer.m_syncServerInitialized > 0);
        }
        public static bool IsActivePhysEngineSceneS
        {
            get {
                System.Console.WriteLine("IsActivePhysEngineScene: si={0} tc={1}", 
                    SceneToPhysEngineSyncServer.m_syncServerInitialized, 
                    SceneToPhysEngineSyncServer.m_totalConnections);
                return (SceneToPhysEngineSyncServer.m_syncServerInitialized > 0 
                                && SceneToPhysEngineSyncServer.m_totalConnections > 0); 
            }
        }
        public static bool IsActivePhysEngineScene2S()
        {
            return (SceneToPhysEngineSyncServer.m_syncServerInitialized > 0 
                            && SceneToPhysEngineSyncServer.m_totalConnections > 0); 
        }
        public static bool IsPhysEngineActorS
        {
            get { return PhysEngineToSceneConnectorModule.IsPhysEngineActorS; }
        }

        /// <summary>
        /// The scene is unknown by ODE so we have to look through the scenes to
        /// find the one with this PhysicsActor so we can send the update.
        /// </summary>
        /// <param name="pa"></param>
        public static void RouteUpdate(PhysicsActor pa)
        {
            SceneObjectPart sop = null;
            Scene s = null;
            foreach (Scene ss in m_allScenes)
            {
                try
                {
                    sop = ss.GetSceneObjectPart(pa.UUID);
                }
                catch
                {
                    sop = null;
                }
                if (sop != null)
                {
                    s = ss;
                    break;
                }
                else
                {
                    ScenePresence sp = ss.GetScenePresence(pa.UUID);
                    if (sp != null)
                    {
                        s = ss;
                        break;
                    }
                }
            }
            if (s != null)
            {
                if (s.SceneToPhysEngineSyncServer != null)
                {
                    s.SceneToPhysEngineSyncServer.SendUpdate(pa);
                }
                else
                {
                    Console.WriteLine("RouteUpdate: SceneToPhysEngineSyncServer is not available");
                }
            }
            else
            {
                Console.WriteLine("RouteUpdate: no SOP for update of {0}", pa.UUID);
            }
            return;
        }

        public void SendUpdate(PhysicsActor pa)
        {
            // m_log.DebugFormat("{0}: SendUpdate for {1}", LogHeader, pa.LocalID);
            if (m_sceneToPhysEngineConnector != null)
            {
                this.m_sceneToPhysEngineConnector.SendPhysUpdateAttributes(pa);
            }
        }

        #endregion

        // Constructor
        public SceneToPhysEngineSyncServer(Scene scene, string addr, int port)
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

            m_scene.RegisterModuleInterface<ISceneToPhysEngineServer>(this);

            // remember all the scenes that are configured for connection to physics engine
            if (!m_allScenes.Contains(m_scene))
            {
                m_allScenes.Add(m_scene);
            }

            InitQuarksInScene();
            SubscribeToEvents();
            m_scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
            InstallInterfaces();
        }


        private void SubscribeToEvents()
        {
        }

        private void UnSubscribeToEvents()
        {
        }

        // Start the server
        public void Start()
        {
            SceneToPhysEngineSyncServer.m_syncServerInitialized++;
            m_listenerThread = new Thread(new ThreadStart(Listen));
            m_listenerThread.Name = "SceneToPhysEngineSyncServer Listener";
            m_log.DebugFormat("{0}: Starting {1} thread", LogHeader, m_listenerThread.Name);
            m_listenerThread.Start();
            // m_log.DebugFormat("{0}: Started", LogHeader);
        }



        // Stop the server and disconnect all RegionSyncClients
        public void Shutdown()
        {
            m_log.DebugFormat("{0}: Shutdown", LogHeader);
            SceneToPhysEngineSyncServer.m_syncServerInitialized--;
            // Stop the listener and listening thread so no new clients are accepted
            m_listener.Stop();
            m_listenerThread.Abort();
            m_listenerThread = null;

            // Stop all existing SceneTOSEConnectors 
            //TO FINISH
            foreach (SceneToPhysEngineConnector peConnector in m_physEngineConnectors)
            {
                peConnector.Shutdown();
            }
            m_physEngineConnectors.Clear();

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

        public void RegisterQuarkSubscription(List<QuarkInfo> quarkSubscriptions, SceneToPhysEngineConnector peConnector)
        {
            foreach (QuarkInfo quark in quarkSubscriptions)
            {
                string quarkID = quark.QuarkStringRepresentation;
                // TODO: does the physics engine connect to quarks. Next line commented out.
                // m_quarksInScene[quarkID].PEConnector = peConnector;
                m_log.Debug(LogHeader + ": " + quarkID + " subscribed by "+peConnector.Description);
            }
        }

        // Add a connector to a physics engine
        public void AddSyncedPhysEngine(SceneToPhysEngineConnector peConnector)
        {
            lock (m_physEngineConnector_lock)
            {
                m_physEngineConnectors.Add(peConnector);
                m_sceneToPhysEngineConnector = peConnector;
            }
        }

        // Remove the client view from the list and decrement synced client counter
        public void RemoveSyncedPhysEngine(SceneToPhysEngineConnector peConnector)
        {
            lock (m_physEngineConnector_lock)
            {
                //Dictionary<string, SceneToPhysEngineConnector> currentlist = m_physEngineConnectors;
                //Dictionary<string, SceneToPhysEngineConnector> newlist = new Dictionary<string, SceneToPhysEngineConnector>(currentlist);
                m_physEngineConnectors.Remove(peConnector);
                // Threads holding the previous version of the list can keep using it since
                // they will not hold it for long and get a new copy next time they need to iterate
                //m_physEngineConnectors = newlist;
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
                    SceneToPhysEngineSyncServer.m_totalConnections++;
                    // m_log.DebugFormat("{0}: m_totalConnections = {1}", LogHeader, SceneToPhysEngineSyncServer.m_totalConnections);

                    ActorStatus actorStatus = GetActorStatus(tcpclient);

                    switch (actorStatus)
                    {
                        case ActorStatus.Sync:
                            // Add the SceneToPhysEngineConnector to the list 
                            SceneToPhysEngineConnector sceneToPEConnector = new SceneToPhysEngineConnector(++peCounter, m_scene, tcpclient, this);
                            AddSyncedPhysEngine(sceneToPEConnector);
                            break;
                        case ActorStatus.Idle:
                            IdlePhysEngineInfo idleSE = new IdlePhysEngineInfo(tcpclient);
                            m_log.DebugFormat("{0}: adding an idle SE ({1}:{2})", LogHeader, addr, port);
                            m_idlePhysEngineList.Add(idleSE);
                            break;
                        default:
                            m_log.DebugFormat("{0}: Unknown actor status", LogHeader);
                            break;
                    }

                }
            }
            catch (SocketException e)
            {
                m_log.WarnFormat("{0}: [Listen] SocketException: {1}", LogHeader, e);
            }
        }

        /*
        public void RegisterSyncedPhysEngine(SceneToPhysEngineConnector sceneToSEConnector)
        {
            //first, remove it from the idle list
            m_idlePhysEngineList.Remove(sceneToSEConnector);

            //now, added to the synced SE list
            AddSyncedPhysEngine(sceneToSEConnector);
        }
         * */ 


        // Broadcast a message to all connected RegionSyncClients
        public void SendToAllConnectedPE(RegionSyncMessage msg)
        {
            if (m_physEngineConnectors.Count > 0)
            {
                m_log.Debug(LogHeader + ": region " + m_scene.RegionInfo.RegionName + " Broadcast to PhysEngine, msg " + msg.Type);
                foreach (SceneToPhysEngineConnector peConnector in m_physEngineConnectors)
                {
                    peConnector.Send(msg);
                }
            }

        }

        //TO FINISH: Find the right SceneToSEConnector to forward the message
        public void SendToPE(RegionSyncMessage.MsgType msgType, SceneObjectGroup sog)
        {
            SceneToPhysEngineConnector peConnector = GetSceneToPEConnector(sog);
            if (peConnector != null)
            {
                peConnector.SendObjectUpdate(msgType, sog);
            }
        }

        //This is to send a message, rsm, to phys engine, and the message is about object SOG. E.g. RemovedObject
        public void SendToPE(RegionSyncMessage rsm, SceneObjectGroup sog)
        {
            SceneToPhysEngineConnector peConnector = GetSceneToPEConnector(sog);
            if (peConnector != null)
            {
                peConnector.Send(rsm);
            }
        }


        private SceneToPhysEngineConnector GetSceneToPEConnector(SceneObjectGroup sog)
        {
            if (m_physEngineConnectors.Count == 0)
                return null;
            if (sog == null)
            {
                return m_physEngineConnectors[0];
            }
            else
            {
                //Find the right SceneToSEConnector by the object's position
                //TO FINISH: Map the object to a quark first, then map the quark to SceneToSEConnector
                string quarkID = RegionSyncUtil.GetQuarkIDByPosition(sog.AbsolutePosition);
                // TODO: connection of physics engine to quarks. Next line commented out
                // SceneToPhysEngineConnector peConnector = m_quarksInScene[quarkID].PEConnector;

                if (PEConnector == null)
                {
                    m_log.Warn(LogHeader + sog.AbsolutePosition.ToString() + " not covered by any physics engine");
                }

                return PEConnector;
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
      
        #endregion Event Handlers

        #region Load balancing members and functions
        /*
        //keep track of idle physics engines that are in the process of load balancing (they are off the idle list, but not a working physics engine yet (not sync'ing with Scene yet)).
        private Dictionary<string, IdlePhysEngineInfo> m_loadBalancingIdleSEs = new Dictionary<string,IdlePhysEngineInfo>(); 
        public void HandleLoadBalanceRequest(SceneToPhysEngineConnector seConnctor)
        {
            //Let's start a thread to do the job, so that we can return quickly and don't block on ReceiveLoop()

            Thread partitionThread = new Thread(new ParameterizedThreadStart(TriggerLoadBalanceProcess));
            partitionThread.Name = "TriggerLoadBalanceProcess";
            partitionThread.Start((object)seConnctor);
        }

        public void TriggerLoadBalanceProcess(object arg)
        {
            SceneToPhysEngineConnector seConnctor = (SceneToPhysEngineConnector)arg;
            IdlePhysEngineInfo idlePhysEngineInfo = GetIdlePhysEngineConnector();
            if (idlePhysEngineInfo != null)
            {
                RegionSyncMessage msg = new RegionSyncMessage(RegionSyncMessage.MsgType.LoadMigrationNotice);
                Send(idlePhysEngineInfo.TClient, msg.ToBytes());
                m_log.Debug(LogHeader + ": HandleLoadBalanceRequest from " + seConnctor.Description + ", picked idle SE: " + idlePhysEngineInfo.ID);

                //keep track of which overload physics engine is paired up with which idle physics engine
                idlePhysEngineInfo.AwaitOverloadedSE = seConnctor;
                m_loadBalancingIdleSEs.Add(idlePhysEngineInfo.ID, idlePhysEngineInfo);

                m_log.Debug("ToSEConnector portal: local -" +
                    ((IPEndPoint)idlePhysEngineInfo.TClient.Client.LocalEndPoint).Address.ToString() + ":" + ((IPEndPoint)idlePhysEngineInfo.TClient.Client.LocalEndPoint).Port
                    + "; remote - " + ((IPEndPoint)idlePhysEngineInfo.TClient.Client.RemoteEndPoint).Address.ToString() + ":"
                    + ((IPEndPoint)idlePhysEngineInfo.TClient.Client.RemoteEndPoint).Port);

                //Now we expect the idle physics engine to reply back
                msg = new RegionSyncMessage(idlePhysEngineInfo.TClient.GetStream());
                if (msg.Type != RegionSyncMessage.MsgType.LoadMigrationListenerInitiated)
                {
                    m_log.Warn(LogHeader + ": should receive a message of type LoadMigrationListenerInitiated, but received " + msg.Type.ToString());
                }
                else
                {
                    //Before the load is migrated from overloaded physics engine to the idle engine, sync with the DB to update the state in DB
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
                    //echo the information back to the overloaded physics engine
                    seConnctor.Send(new RegionSyncMessage(RegionSyncMessage.MsgType.LoadBalanceResponse, OSDParser.SerializeJsonString(data)));

                    m_log.Debug(LogHeader + " now remove physics engine " + idlePhysEngineInfo.ID + " from idle SE list, and create SceneToPhysEngineConnector to it");
                    //create a SceneToSEConnector for the idle physics engine, who will be sync'ing with this SyncServer soon
                    SceneToPhysEngineConnector sceneToSEConnector = new SceneToPhysEngineConnector(++peCounter, m_scene, idlePhysEngineInfo.TClient, this);
                    //Now remove the physics engine from the idle SE list
                    m_idlePhysEngineList.Remove(idlePhysEngineInfo);
                    //AddSyncedPhysEngine(sceneToSEConnector);
                }

            }
            else
            {
                seConnctor.SendLoadBalanceRejection("no idle physics engines");
            }
        }
        */

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
                    m_log.WarnFormat("{0} physics Engine has disconnected.", LogHeader);
                }
            }
        }

        private IdlePhysEngineInfo GetIdlePhysEngineConnector()
        {
            if (m_idlePhysEngineList.Count == 0)
                return null;
            IdlePhysEngineInfo idleSEInfo = m_idlePhysEngineList[0];
            m_idlePhysEngineList.Remove(idleSEInfo);
            return idleSEInfo;
        }

        #endregion Load balancing functions 

        #region Message Logging
        public static bool logInput = false;
        public static bool logOutput = true;
        public static bool logEnabled = false;
        private class PhysMsgLogger
        {
            public DateTime startTime;
            public string path = null;
            public System.IO.TextWriter Log = null;
        }
        private static PhysMsgLogger logWriter = null;
        private static TimeSpan logMaxFileTime = new TimeSpan(0, 5, 0);   // (h,m,s) => 5 minutes
        public static string logDir = "/stats/stats";
        private static object logLocker = new Object();

        public static void PhysLogMessage(bool direction, RegionSyncMessage rsm)
        {
            if (!logEnabled) return;    // save to work of the ToStringFull if not enabled
            PhysLogMessage(direction, rsm.ToStringFull());
        }

        /// <summary>
        /// Log a physics bucket message
        /// </summary>
        /// <param name="direction">True of message originated from the agent</param>
        /// <param name="msg">the message to log</param>
        public static void PhysLogMessage(bool direction, string msg)
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
                        logWriter = new PhysMsgLogger();
                        logWriter.startTime = now;
                        logWriter.path = (logDir.Length > 0 ? logDir + System.IO.Path.DirectorySeparatorChar.ToString() : "")
                                + String.Format("physics-{0}.log", now.ToString("yyyyMMddHHmmss"));
                        logWriter.Log = new StreamWriter(File.Open(logWriter.path, FileMode.Append, FileAccess.Write));
                    }
                    if (logWriter != null && logWriter.Log != null)
                    {
                        StringBuilder buff = new StringBuilder();
                        buff.Append(now.ToString("yyyyMMddHHmmssfff"));
                        buff.Append(" ");
                        buff.Append(direction ? "A->S:" : "S->A:");
                        buff.Append(msg);
                        buff.Append("\r\n");
                        logWriter.Log.Write(buff.ToString());
                    }
                }
                catch (Exception e)
                {
                    // m_log.ErrorFormat("{0}: FAILURE WRITING TO LOGFILE: {1}", LogHeader, e);
                    logEnabled = false;
                }
            }
            return;
        }

        public static void PhysLogMessageClose()
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

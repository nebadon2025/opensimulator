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

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{



    //KittyL: NOTE -- We need to define an interface for all actors to connect into the Scene,
    //        e.g. IActorConnector, that runs on the Scene side, processes messages from actors,
    //        and apply Scene/Object operations.

    // The SceneToScriptEngineConnector acts as a thread on the RegionSyncServer to handle incoming
    // messages from ScriptEngineToSceneConnectors that run on Script Engines. It connects the 
    // authoratative Scene with remote script engines.
    public class SceneToScriptEngineConnector
    {
        #region SceneToScriptEngineConnector members

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

        private SceneToScriptEngineSyncServer m_syncServer = null;

        // A string of the format [REGION SYNC SCRIPT API (regionname)] for use in log headers
        private string LogHeader
        {
            get
            {
                if (m_regionName == null)
                    return String.Format("[SceneToScriptEngineConnector #{0}]", m_connection_number);
                return String.Format("[SceneToScriptEngineConnector #{0} ({1:10})]", m_connection_number, m_regionName);
            }
        }

        // A string of the format "RegionSyncClientView #X" for use in describing the object itself
        public string Description
        {
            get
            {
                if (m_regionName == null)
                    return String.Format("RegionSyncScriptAPI #{0}", m_connection_number);
                return String.Format("RegionSyncScriptAPI #{0} ({1:10})", m_connection_number, m_regionName);
            }
        }

        public int ConnectionNum
        {
            get { return m_connection_number; }
        }

        public string GetStats()
        {
            int syncedAvCount;
            string ret;
            //lock (m_syncRoot)
            //    syncedAvCount = m_syncedAvatars.Count;
            lock (stats)
            {
                double secondsSinceLastStats = DateTime.Now.Subtract(lastStatTime).TotalSeconds;
                lastStatTime = DateTime.Now;

                ret = String.Format("[{0,4}/{1,4}], [{2,4}/{3,4}], [{4,4}/{5,4}], [{6,4} ({7,4})], [{8,8} ({9,8:00.00})], [{10,4} ({11,4})], [{12,8} ({13,8:00.00})], [{14,8} ({15,4}]",
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
        public SceneToScriptEngineConnector(int num, Scene scene, TcpClient client, SceneToScriptEngineSyncServer syncServer)
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
            SendSceneLoc();
        }

        // Stop the listening thread, disconnecting the RegionSyncScriptEngine
        public void Shutdown()
        {
            m_syncServer.RemoveSyncedScriptEngine(this);
            // m_scene.EventManager.OnChatFromClient -= EventManager_OnChatFromClient;
            // Abort ReceiveLoop Thread, close Socket and TcpClient
            m_receive_loop.Abort();
            m_tcpclient.Client.Close();
            m_tcpclient.Close();

            //m_scene.EventManager.OnRezScript -= SEConnectorOnRezScript;
            //m_scene.EventManager.OnScriptReset -= SEConnectorOnScriptReset;
            //m_scene.EventManager.OnUpdateScript -= SEConnectorOnUpdateScript;
        }

        #region Send/Receive messages to/from the remote Script Engine

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
                case RegionSyncMessage.MsgType.QuarkSubscription:
                    HandleQuarkSubscription(msg);
                    return;
                case RegionSyncMessage.MsgType.RegionName:
                    {
                        m_regionName = Encoding.ASCII.GetString(msg.Data, 0, msg.Length);
                        Send(new RegionSyncMessage(RegionSyncMessage.MsgType.RegionName, m_scene.RegionInfo.RegionName));
                        RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Syncing to region \"{0}\"", m_regionName));
                        return;
                    }
               case RegionSyncMessage.MsgType.GetTerrain:
                    {
                        Send(new RegionSyncMessage(RegionSyncMessage.MsgType.Terrain, m_scene.Heightmap.SaveToXmlString()));
                        RegionSyncMessage.HandleSuccess(LogHeader, msg, "Terrain sent");
                        return;
                    }
                case RegionSyncMessage.MsgType.GetObjects:
                    {
                        //List<EntityBase> entities = m_scene.GetEntities();

                        //This should be a function of Scene, but since we don't have the quark concept in Scene yet, 
                        //for now we implement it here.
                        List<SceneObjectGroup> objectsInSpace = GetObjectsInGivenSpace(m_scene, m_quarkSubscriptions);
                        foreach (SceneObjectGroup sog in objectsInSpace)
                        {
                            Send(PrepareObjectUpdateMessage(RegionSyncMessage.MsgType.NewObject, sog));
                        }
                        RegionSyncMessage.HandleSuccess(LogHeader, msg, "Sent all scene objects");
                        return;
                    }
                case RegionSyncMessage.MsgType.SetObjectProperty:
                    {
                        OSDMap data = RegionSyncUtil.DeserializeMessage(msg, LogHeader);
                        if (!data.ContainsKey("UUID") || !data.ContainsKey("name"))
                        {
                            m_log.WarnFormat("{0} Parameters missing in SetObjectProperty request, need \"UUID\", \"name\" (property-name), and \"valParams\" (property-value)", LogHeader);
                            return;
                        }
                        UUID objID = data["UUID"].AsUUID();
                        string pName = data["name"].AsString();
                        //valParams
                        SetObjectProperty(objID, pName, data);
                    }
                    return;
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
                            m_syncServer.AddSyncedScriptEngine(this);
                        }
                        else
                        {
                            m_log.Warn(LogHeader + ": not supposed to received RegionSyncMessage.MsgType.ActorStatus==" + status.ToString());
                        }
                        return;
                    }
                     
                default:
                    {
                        m_log.WarnFormat("{0} Unable to handle unsupported message type", LogHeader);
                        return;
                    }
            }
        }

        //For simplicity, we assume the subscription sent by ScriptEngine is legistimate (no overlapping with other script engines, etc)
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

        public void SendObjectUpdate(RegionSyncMessage.MsgType msgType, SceneObjectGroup sog)
        {
            Send(PrepareObjectUpdateMessage(msgType, sog));
        }

        #endregion Send/Receive messages to/from the remote Script Engine

        #region stub functions for remote actors to set object properties

        //NOTE: the current implementation of setting various object properties is mimicking the way script engine sets object properties
        //      (i.e. as implemented in LSL_Api.cs. But the function calls are meant to be generic for all actors. We may need to change
        //      the implementation details later to accomdate more actors, and move it to the Scene implementation.

        //TODO: These functions should eventually be part of DSG interface, rather than part of SceneToScriptEngineConnector.

        private void SetObjectProperty(UUID primID, string pName, OSDMap valParams)
        {
            //m_log.Debug(LogHeader + " received SetObjectProperty request, " + primID + ", " + pName);
            switch (pName)
            {
                case "object_rez":
                    //rez a new object, rather than update an existing object's property
                    RezObjectOnScene(primID, valParams);
                    break;
                case "color":
                    SetPrimColor(primID, valParams);
                    break;
                case "pos":
                    SetPrimPos(primID, valParams);
                    break;
                default:
                    break;
            }
        }

        //Triggered when an object is rez'ed by script. Followed the implementation in LSL_Api.llRezAtRoot().
        //TODO: the real rez object part should be executed by Scene class, not here
        private void RezObjectOnScene(UUID primID, OSDMap data)
        {
            if(data["inventory"] == null || data["pos"]==null || data["vel"] == null || data["rot"]==null || data["param"]==null){
                m_log.Warn(LogHeader + ": not enough parameters for setting color on " + primID);
                return;
            }
            string inventory = data["inventory"].AsString();
            Vector3 pos = data["pos"].AsVector3();
            Vector3 vel = data["vel"].AsVector3();
            Quaternion rot = data["rot"].AsQuaternion();
            int param = data["param"].AsInteger();
            SceneObjectPart primToFetchObject = m_scene.GetSceneObjectPart(primID);

            if (primToFetchObject == null)
                return;

            TaskInventoryDictionary partInventory = (TaskInventoryDictionary)primToFetchObject.TaskInventory.Clone();

            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in partInventory)
            {
                if (inv.Value.Name == inventory)
                {
                    // make sure we're an object.
                    if (inv.Value.InvType != (int)InventoryType.Object)
                    {
                        //llSay(0, "Unable to create requested object. Object is missing from database.");
                        m_log.Warn(LogHeader + ": Unable to create requested object. Object is missing from database.");
                        return;
                    }

                    Vector3 llpos = pos;
                    Vector3 llvel = vel;

                    // need the magnitude later
                    float velmag = (float)Util.GetMagnitude(llvel);

                    SceneObjectGroup new_group = m_scene.RezObject(primToFetchObject, inv.Value, llpos, rot, llvel, param);

                    // If either of these are null, then there was an unknown error.
                    if (new_group == null)
                        continue;
                    if (new_group.RootPart == null)
                        continue;

                    // objects rezzed with this method are die_at_edge by default.
                    new_group.RootPart.SetDieAtEdge(true);

                    /*
                    m_ScriptEngine.PostObjectEvent(m_host.LocalId, new EventParams(
                            "object_rez", new Object[] {
                            new LSL_String(
                            new_group.RootPart.UUID.ToString()) },
                            new DetectParams[0]));
                     * */
                    //NOTE: should replace the above code with the following and implement TriggerRezObject. However, since
                    //      nothing is really doen with "object_rez" in XEngine, we ignore posting the event for now.
                    //m_scene.EventManager.TriggerRezObject();

                    float groupmass = new_group.GetMass();

                    if (new_group.RootPart.PhysActor != null && new_group.RootPart.PhysActor.IsPhysical && llvel != Vector3.Zero)
                    {
                        //Recoil.
                        //llApplyImpulse(new LSL_Vector(llvel.X * groupmass, llvel.Y * groupmass, llvel.Z * groupmass), 0);
                        //copy the implementation of llApplyImpulse here.
                        Vector3 v = new Vector3(llvel.X * groupmass, llvel.Y * groupmass, llvel.Z * groupmass);
                        if (v.Length() > 20000.0f)
                        {
                            v.Normalize();
                            v = v * 20000.0f;
                        }
                        new_group.RootPart.ApplyImpulse(v, false);
                    }
                    return;
                }
            }

        }

        private void SetPrimColor(UUID primID, OSDMap data)
        {
            if (!data.ContainsKey("color") || !data.ContainsKey("face"))
            {
                m_log.Warn(LogHeader + ": not enough parameters for setting color on " + primID);
                return;
            }
            SceneObjectPart primToUpdate = m_scene.GetSceneObjectPart(primID);
            if (primToUpdate == null)
                return;
            Vector3 color = data["color"].AsVector3();
            int face = data["face"].AsInteger();

            //m_log.DebugFormat("{0}: Set Color request, to set ({1}) on face {2}", LogHeader, color.ToString(), face);

            primToUpdate.SetFaceColor(color, face);
        }

        //Whichever actor that triggers this function call shall have already checked the legitimacy of the position. 
        private void SetPrimPos(UUID primID, OSDMap data)
        {
            if (!data.ContainsKey("pos"))
            {
                m_log.Warn(LogHeader + ": not enough parameters for setting pos on " + primID);
                return;
            }
            Vector3 pos = data["pos"].AsVector3();
            SceneObjectPart primToUpdate = m_scene.GetSceneObjectPart(primID);

            if (primToUpdate == null)
                return;

            SceneObjectGroup parent = primToUpdate.ParentGroup;

            if (parent.RootPart == primToUpdate)
            {
                //the prim is the root-part of the object group, set the position of the whole group
                parent.UpdateGroupPosition(pos);
            }
            else
            {
                //the prim is not the root-part, set the offset position
                primToUpdate.OffsetPosition = pos;
                parent.HasGroupChanged = true;
                parent.ScheduleGroupForTerseUpdate();
            }
        }

        #endregion 

        #region Event Handlers
        //calling by SceneToSESyncServer
        public void SEConnectorOnRezScript(uint localID, UUID itemID, string script, int startParam, bool postOnRez, string engine, int stateSource)
        {
            m_log.Debug(LogHeader + ": Caught event OnRezScript, send message to Script Engine");

            OSDMap data = new OSDMap(7);
            data["localID"] = OSD.FromUInteger(localID);
            data["itemID"] = OSD.FromUUID(itemID);
            data["script"] = OSD.FromString(script);
            data["startParam"] = OSD.FromInteger(startParam);
            data["postOnRez"] = OSD.FromBoolean(postOnRez);
            data["engine"] = OSD.FromString(engine);
            data["stateSource"] = OSD.FromInteger(stateSource);
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.OnRezScript, OSDParser.SerializeJsonString(data)));
        }

        public void SEConnectorOnScriptReset(uint localID, UUID itemID)
        {
            m_log.Debug(LogHeader + ": Caught event OnScriptReset, send message to Script Engine");

            OSDMap data = new OSDMap(2);
            data["localID"] = OSD.FromUInteger(localID);
            data["itemID"] = OSD.FromUUID(itemID);
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.OnScriptReset, OSDParser.SerializeJsonString(data)));
        }

        public void SEConnectorOnUpdateScript(UUID agentID, UUID itemId, UUID primId, bool isScriptRunning, UUID newAssetID)
        {
            m_log.Debug(LogHeader + ": Caught event OnUpdateScript, send message to Script Engine");

            OSDMap data = new OSDMap(5);
            data["agentID"] = OSD.FromUUID(agentID);
            data["itemID"] = OSD.FromUUID(itemId);
            data["primID"] = OSD.FromUUID(primId);
            data["running"] = OSD.FromBoolean(isScriptRunning);
            data["assetID"] = OSD.FromUUID(newAssetID);
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.OnUpdateScript, OSDParser.SerializeJsonString(data)));
        }

        #endregion Event Handlers

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
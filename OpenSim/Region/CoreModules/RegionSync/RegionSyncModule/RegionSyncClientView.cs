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
using log4net;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    #region MsgHandlerStatus Enum
    public enum MsgHandlerStatus
    {
        Success, // Everything went as expected
        Trivial, // Minor issue, nothing to worry about
        Warning, // Something went wrong, we can continue
        Error    // This should certainly not have happened! (A bug)
    }
    #endregion

    // The RegionSyncClientView acts as a thread on the RegionSyncServer to handle incoming
    // messages from RegionSyncClients. 
    public class RegionSyncClientView
    {
        #region RegionSyncClientView members

        object stats = new object();
        private DateTime lastStatTime;
        private long queuedUpdates;
        private long dequeuedUpdates;
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
        //private OpenMetaverse.BlockingQueue<RegionSyncMessage> inbox = new OpenMetaverse.BlockingQueue<RegionSyncMessage>();
        //private OpenMetaverse.BlockingQueue<RegionSyncMessage> outbox = new OpenMetaverse.BlockingQueue<RegionSyncMessage>();

        private BlockingUpdateQueue m_outQ = new BlockingUpdateQueue();

        private ILog m_log;

        private Thread m_receive_loop;
        private Thread m_send_loop;
        private string m_regionName;

        public string Name
        {
            get { return m_regionName; }
        }

        // A string of the format [REGION SYNC CLIENT VIEW (regionname)] for use in log headers
        private string LogHeader
        {
            get 
            { 
                if(m_regionName == null)
                    return String.Format("[REGION SYNC CLIENT VIEW #{0}]", m_connection_number);
                return String.Format("[REGION SYNC CLIENT VIEW #{0} ({1:10})]", m_connection_number, m_regionName); 
            }
        }

        // A string of the format "RegionSyncClientView #X" for use in describing the object itself
        public string Description
        {
            get 
            { 
                if(m_regionName == null)
                    return String.Format("RegionSyncClientView #{0}", m_connection_number);
                return String.Format("RegionSyncClientView #{0} ({1:10})", m_connection_number, m_regionName); 
            }
        }

        public string GetStats()
        {
            int syncedAvCount = SyncedAvCount; ;
            string ret;
            lock (stats)
            {
                double secondsSinceLastStats = DateTime.Now.Subtract(lastStatTime).TotalSeconds;
                lastStatTime = DateTime.Now;
                int totalAvCount = m_scene.SceneGraph.GetRootAgentCount();
                ret = String.Format("[{0,4}/{1,4}], [{2,4}/{3,4}], [{4,4}/{5,4}], [{6,4} ({7,4})], [{8,8} ({9,8:00.00})], [{10,4} ({11,4})], [{12,8} ({13,8:00.00})], [{14,8},{15,8},{16,8}]",
                    lastTotalCount, totalAvCount, // TOTAL AVATARS
                    lastLocalCount, syncedAvCount, // LOCAL TO THIS CLIENT VIEW
                    lastRemoteCount, totalAvCount - syncedAvCount, // REMOTE (SHOULD = TOTAL - LOCAL)
                    msgsIn, (int)(msgsIn / secondsSinceLastStats),
                    bytesIn, 8 * (bytesIn / secondsSinceLastStats / 1000000), // IN
                    msgsOut, (int)(msgsOut / secondsSinceLastStats),
                    bytesOut, 8 * (bytesOut / secondsSinceLastStats / 1000000), // OUT
                    m_outQ.Count, (int)(queuedUpdates / secondsSinceLastStats), (int)(dequeuedUpdates/secondsSinceLastStats)); // QUEUE ACTIVITY
                    msgsIn = msgsOut = bytesIn = bytesOut = pollBlocks = queuedUpdates = dequeuedUpdates = 0;
            }
            return ret;
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
            //m_log.WarnFormat("{0} Constructed", LogHeader);

            // Create a thread for the receive loop
            m_receive_loop = new Thread(new ThreadStart(delegate() { ReceiveLoop(); }));
            m_receive_loop.Name = Description + " (ReceiveLoop)";
            //m_log.WarnFormat("{0} Started thread: {1}", LogHeader, m_receive_loop.Name);
            m_receive_loop.Start();

            m_send_loop = new Thread(new ThreadStart(delegate() { SendLoop(); }));
            m_send_loop.Name = Description + " (SendLoop)";
            m_send_loop.Start();
        }

        // Stop the RegionSyncClientView, disconnecting the RegionSyncClient
        public void Shutdown()
        {
            m_scene.EventManager.OnChatFromClient -= EventManager_OnChatFromClient;
            m_tcpclient.Client.Close();
            m_tcpclient.Close();
            //Logout any synced avatars
            lock (m_syncRoot)
            {
                foreach (UUID agentID in m_syncedAvatars.Keys)
                {
                    ScenePresence presence;
                    if (m_scene.TryGetScenePresence(agentID, out presence))
                    {
                        string name = presence.Name;
                        m_scene.RemoveClient(agentID);
                        m_log.WarnFormat("{0} Agent \"{1}\" ({2}) was removed from scene.", LogHeader, name, agentID);
                    }
                    else
                    {
                        m_log.WarnFormat("{0} Agent {1} not found in the scene.", LogHeader, agentID);
                    }
                }
            }
        }

        // Listen for messages from a RegionSyncClient
        // *** This is the main receive loop thread for each connected client
        private void ReceiveLoop()
        {
            //SYMMETRIC SYNC: commenting out the code for handling chats
           // m_scene.EventManager.OnChatFromClient += new EventManager.ChatFromClientEvent(EventManager_OnChatFromClient);
           // m_scene.EventManager.OnChatFromWorld += new EventManager.ChatFromWorldEvent(EventManager_OnChatFromClient);
            //end of SYMMETRIC SYNC

            
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
                    try
                    {
                        lock (m_syncRoot)
                            HandleMessage(msg);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("{0} Exception in HandleMessage({1}) (ReceiveLoop):{2}", LogHeader, msg.Type.ToString(), e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("{0} RegionSyncClient has disconnected: {1} (ReceiveLoop)", LogHeader, e.Message);
            }
            Shutdown();
            // Thread exits here
        }

        // Send messages from the update Q as fast as we can DeQueue them
        // *** This is the main send loop thread for each connected client
        private void SendLoop()
        {
            try
            {
                while (true)
                {
                    // Dequeue is thread safe
                    byte[] update = m_outQ.Dequeue();
                    lock (stats)
                        dequeuedUpdates++;
                    Send(update);
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("{0} RegionSyncClient has disconnected: {1} (SendLoop)", LogHeader, e.Message);
            }
            Shutdown();
        }

        public void EnqueuePresenceUpdate(UUID id, byte[] update)
        {
            lock (stats)
                queuedUpdates++;
            // Enqueue is thread safe
            m_outQ.Enqueue(id, update);
        }


        void EventManager_OnChatFromClient(object sender, OSChatMessage chat)
        {
            OSDMap data = new OSDMap(5);
            data["channel"] = OSD.FromInteger(chat.Channel);
            data["msg"] = OSD.FromString(chat.Message);
            data["pos"] = OSD.FromVector3(chat.Position);
            data["name"] = OSD.FromString(chat.From);
            data["id"] = OSD.FromUUID(chat.SenderUUID);
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.ChatFromClient, OSDParser.SerializeJsonString(data)));
        }

        // Get a message from the RegionSyncClient
        private RegionSyncMessage GetMessage()
        {
            // Get a RegionSyncMessager from the incoming stream
            RegionSyncMessage msg = new RegionSyncMessage(m_tcpclient.GetStream());
            //m_log.WarnFormat("{0} Received {1}", LogHeader, msg.ToString());
            return msg;
        }

        HashSet<string> exceptions = new HashSet<string>();
        private OSDMap DeserializeMessage(RegionSyncMessage msg)
        {
            OSDMap data = null;
            try
            {
                data = OSDParser.DeserializeJson(Encoding.ASCII.GetString(msg.Data, 0, msg.Length)) as OSDMap;
            }
            catch(Exception e)
            {
                lock(exceptions)
                    // If this is a new message, then print the underlying data that caused it
                    if(!exceptions.Contains(e.Message))
                        m_log.Error(LogHeader + " " + Encoding.ASCII.GetString(msg.Data, 0, msg.Length));
                data = null;
            }
            return data;
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
                case RegionSyncMessage.MsgType.RegionName:
                    {
                        m_regionName = Encoding.ASCII.GetString(msg.Data, 0, msg.Length);
                        Send(new RegionSyncMessage(RegionSyncMessage.MsgType.RegionName, m_scene.RegionInfo.RegionName));
                        RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Syncing to region \"{0}\"", m_regionName));
                        return;
                    }
                //SYMMETRIC SYNC: do not handle terrian and object updates
                case RegionSyncMessage.MsgType.GetTerrain:
                    {
                        /*
                        if (!m_scene.RegionSyncServerModule.IsSymSync)
                        {
                            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.Terrain, m_scene.Heightmap.SaveToXmlString()));
                            RegionSyncMessage.HandleSuccess(LogHeader, msg, "Terrain sent");
                        }
                         * */ 
                        return;
                    }
                case RegionSyncMessage.MsgType.GetObjects:
                    {
                        /*
                        if (!m_scene.RegionSyncServerModule.IsSymSync)
                        {
                            EntityBase[] entities = m_scene.GetEntities();
                            foreach (EntityBase e in entities)
                            {
                                if (e is SceneObjectGroup)
                                {
                                    string sogxml = SceneObjectSerializer.ToXml2Format((SceneObjectGroup)e);
                                    Send(new RegionSyncMessage(RegionSyncMessage.MsgType.NewObject, sogxml));
                                }
                            }
                            RegionSyncMessage.HandleSuccess(LogHeader, msg, "Sent all scene objects");
                        }
                         * */ 
                        return;
                    }
                //end of SYMMETRIC SYNC
                case RegionSyncMessage.MsgType.GetAvatars:
                    {
                        m_scene.ForEachScenePresence(delegate(ScenePresence presence)
                        {
                            // Let the client managers know about this avatar
                            OSDMap data = new OSDMap(1);
                            data["agentID"] = OSD.FromUUID(presence.ControllingClient.AgentId);
                            data["localID"] = OSD.FromUInteger(presence.LocalId);
                            data["first"] = OSD.FromString(presence.ControllingClient.FirstName);
                            data["last"] = OSD.FromString(presence.ControllingClient.LastName);
                            data["startPos"] = OSD.FromVector3(presence.ControllingClient.StartPos);
                            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.NewAvatar, OSDParser.SerializeJsonString(data)));
                        });
                        RegionSyncMessage.HandleSuccess(LogHeader, msg, "Sent all scene avatars");
                        return;
                    }
                case RegionSyncMessage.MsgType.AgentAdd:
                    {
                        OSDMap data = DeserializeMessage(msg);
                        if (data != null)
                        {
                            UUID agentID = data["agentID"].AsUUID();
                            string first = data["first"].AsString();
                            string last = data["last"].AsString();
                            Vector3 startPos = data["startPos"].AsVector3();

                            if (agentID != null && first != null && last != null && startPos != null)
                            {
                                RegionSyncAvatar av = new RegionSyncAvatar(m_scene, agentID, first, last, startPos, this);
                                lock (m_syncRoot)
                                {
                                    if (m_syncedAvatars.ContainsKey(agentID))
                                    {
                                        RegionSyncMessage.HandleWarning(LogHeader, msg, String.Format("Attempted to add duplicate avatar with agentID {0}", agentID));
                                        return;
                                    }
                                    m_syncedAvatars.Add(agentID, av);
                                }
                                //m_scene.AddNewClient(av);
                                m_scene.AddNewClient2(av, true, false);
                                RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Handled AddAgent for UUID {0} at {1}", agentID, startPos.ToString()));
                                return;
                            }
                        }
                        RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                        return;
                        
                    }
                case RegionSyncMessage.MsgType.AgentUpdate:
                    {
                        int len = 0;
                        AgentUpdatePacket.AgentDataBlock agentData = new AgentUpdatePacket.AgentDataBlock();
                        agentData.FromBytes(msg.Data, ref len);

                        UUID agentID = agentData.AgentID;

                        RegionSyncAvatar av;
                        bool found;
                        lock (m_syncRoot)
                        {
                            found = m_syncedAvatars.TryGetValue(agentData.AgentID, out av);
                        }
                        if(!found)
                        {
                            RegionSyncMessage.HandleWarning(LogHeader, msg, String.Format("Received agent update for avatar not owned by this client view {0}", agentData.AgentID));
                            return;
                        }

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
                        {
                            //RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Handled AgentUpdate for UUID {0}", agentID));
                            return;
                        }
                        else
                        {
                            RegionSyncMessage.HandleWarning(LogHeader, msg, String.Format("Could not handle AgentUpdate UUID {0}", agentID));
                            return;
                        }
                    }
                case RegionSyncMessage.MsgType.AgentRemove:
                    {
                        // Get the data from message and error check
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }

                        // Get the parameters from data and error check
                        UUID agentID = data["agentID"].AsUUID();
                        if (agentID == null || agentID == UUID.Zero)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Missing or invalid JSON data.");
                            return;
                        }

                        lock (m_syncRoot)
                        {
                            if (m_syncedAvatars.ContainsKey(agentID))
                            {
                                m_syncedAvatars.Remove(agentID);
                                // Find the presence in the scene
                                ScenePresence presence;
                                if (m_scene.TryGetScenePresence(agentID, out presence))
                                {
                                    string name = presence.Name;
                                    m_scene.SceneGraph.DeleteSceneObject(UUID.Zero, true);
                                    m_scene.RemoveClient(agentID);
                                    RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Agent \"{0}\" was removed from scene.", name));
                                    return;
                                }
                                else
                                {
                                    RegionSyncMessage.HandleWarning(LogHeader, msg, String.Format("Agent {0} not found in the scene.", agentID));
                                    return;
                                }
                            }
                            else
                            {
                                RegionSyncMessage.HandleWarning(LogHeader, msg, String.Format("Agent {0} not in the list of synced avatars.", agentID));
                                return;
                            }
                        }
                    }
                case RegionSyncMessage.MsgType.AvatarAppearance:
                    {
                        m_log.DebugFormat("{0} START of AvatarAppearance handler", LogHeader); 
                        // Get the data from message and error check
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }

                        // Get the parameters from data and error check
                        UUID agentID = data["id"].AsUUID();
                        if (agentID == null || agentID == UUID.Zero)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Missing or invalid JSON data.");
                            return;
                        }

                        // Tells the avatar factory to pull an updated appearance from the avatar service
                        m_scene.AvatarFactory.RefreshAppearance(agentID);

                        m_log.DebugFormat("{0} END of AvatarAppearance handler", LogHeader); 
                        return;
                    }
                case RegionSyncMessage.MsgType.AgentRequestSit:
                    {
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }
                        UUID agentID = data["agentID"].AsUUID();
                        UUID targetID = data["targetID"].AsUUID();
                        Vector3 offset = data["offset"].AsVector3();

                        // m_log.DebugFormat("{0} AgentRequestSit for {1}", LogHeader, agentID.ToString());

                        ScenePresence sp;
                        m_scene.TryGetScenePresence(agentID, out sp);
                        if (sp != null)
                        {
                            sp.HandleAgentRequestSit(sp.ControllingClient, agentID, targetID, offset);
                        }
                        return;
                    }
                case RegionSyncMessage.MsgType.AgentSit:
                    {
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }
                        UUID agentID = data["agentID"].AsUUID();

                        // m_log.DebugFormat("{0} AgentSit for {1}", LogHeader, agentID.ToString());

                        ScenePresence sp;
                        m_scene.TryGetScenePresence(agentID, out sp);
                        if (sp != null)
                        {
                            sp.HandleAgentSit(sp.ControllingClient, agentID);
                        }
                        return;
                    }
                case RegionSyncMessage.MsgType.StartAnim:
                    {
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }
                        UUID agentID = data["agentID"].AsUUID();
                        UUID animID = data["id"].AsUUID();

                        // m_log.DebugFormat("{0} AnimStart for {1}", LogHeader, agentID.ToString());

                        ScenePresence sp;
                        m_scene.TryGetScenePresence(agentID, out sp);
                        if (sp != null)
                        {
                            sp.HandleStartAnim(sp.ControllingClient, animID);
                        }
                        return;
                    }
                case RegionSyncMessage.MsgType.StopAnim:
                    {
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }
                        UUID agentID = data["agentID"].AsUUID();
                        UUID animID = data["id"].AsUUID();

                        // m_log.DebugFormat("{0} AnimStop for {1}", LogHeader, agentID.ToString());

                        ScenePresence sp;
                        m_scene.TryGetScenePresence(agentID, out sp);
                        if (sp != null)
                        {
                            sp.HandleStopAnim(sp.ControllingClient, animID);
                        }
                        return;
                    }
                case RegionSyncMessage.MsgType.GrabObject:
                    {
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }
                        UUID agentID = data["agentID"].AsUUID();
                        uint localID = data["localID"].AsUInteger();
                        m_log.DebugFormat("{0} GrabObject for {1}. object={2}", 
                                        LogHeader, agentID.ToString(), localID);
                        Vector3 offsetPos = data["offsetPos"].AsVector3();
                        OSDArray surfaceArray = (OSDArray)data["surfaceArgs"];
                        List<SurfaceTouchEventArgs> surfaceArgs = ExtractSurfaceArgList(surfaceArray);

                        ScenePresence sp;
                        m_scene.TryGetScenePresence(agentID, out sp);
                        if (sp != null)
                        {
                            m_scene.ProcessObjectGrab(localID, offsetPos, sp.ControllingClient, surfaceArgs);
                        }
                        return;
                    }
                case RegionSyncMessage.MsgType.GrabUpdate:
                    {
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }
                        UUID agentID = data["agentID"].AsUUID();
                        UUID objectID = data["objectID"].AsUUID();
                        // m_log.DebugFormat("{0} GrabUpdate for {1}. ObjectID={2}", 
                        //                 LogHeader, agentID.ToString(), objectID.ToString());
                        Vector3 offset = data["offset"].AsVector3();
                        Vector3 pos = data["pos"].AsVector3();
                        OSDArray surfaceArray = (OSDArray)data["surfaceArgs"];
                        List<SurfaceTouchEventArgs> surfaceArgs = ExtractSurfaceArgList(surfaceArray);

                        ScenePresence sp;
                        m_scene.TryGetScenePresence(agentID, out sp);
                        if (sp != null)
                        {
                            m_scene.ProcessObjectGrabUpdate(objectID, offset, pos, sp.ControllingClient, surfaceArgs);
                        }
                        return;
                    }
                case RegionSyncMessage.MsgType.DeGrabObject:
                    {
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }
                        UUID agentID = data["agentID"].AsUUID();
                        m_log.DebugFormat("{0} DeGrabUpdate for {1}", LogHeader, agentID.ToString());
                        uint localID = data["objectID"].AsUInteger();
                        OSDArray surfaceArray = (OSDArray)data["surfaceArgs"];
                        List<SurfaceTouchEventArgs> surfaceArgs = ExtractSurfaceArgList(surfaceArray);

                        ScenePresence sp;
                        m_scene.TryGetScenePresence(agentID, out sp);
                        if (sp != null)
                        {
                            m_scene.ProcessObjectDeGrab(localID, sp.ControllingClient, surfaceArgs);
                        }
                        return;
                    }
                case RegionSyncMessage.MsgType.ChatFromClient:
                    {
                        // Get the data from message and error check
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }
                        OSChatMessage args = new OSChatMessage();
                        args.Channel = data["channel"].AsInteger();
                        args.Message = data["msg"].AsString();
                        args.Position = data["pos"].AsVector3();
                        args.From = data["name"].AsString();
                        UUID id = data["id"].AsUUID();
                        args.Scene = m_scene;
                        args.Type = ChatTypeEnum.Say;
                        ScenePresence sp;
                        m_scene.TryGetScenePresence(id, out sp);
                        if(sp != null)
                        {
                            args.Sender = sp.ControllingClient;
                            args.SenderUUID = id;
                            m_scene.EventManager.TriggerOnChatFromClient(sp.ControllingClient,args);
                        }
                        RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Received chat from \"{0}\"", args.From));
                        return;
                    }
                case RegionSyncMessage.MsgType.RegionStatus:
                    {
                        // Get the data from message and error check
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }
                        int t = data["total"].AsInteger();
                        int l = data["local"].AsInteger();
                        int r = data["remote"].AsInteger();
                        lastTotalCount = t;
                        lastLocalCount = l;
                        lastRemoteCount = r;
                        //RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Received stats: {0},{1},{2}", t, l, r));
                        return;
                    }
                case RegionSyncMessage.MsgType.AvatarTeleportIn:
                    {
                        // Get the data from message and error check
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }
                        UUID agentID = data["agentID"].AsUUID();
                        ScenePresence sp;
                        m_scene.TryGetScenePresence(agentID, out sp);
                        if (sp == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Presence is not in scene");
                            return;
                        }

                        if (sp.ControllingClient is RegionSyncAvatar)
                        {
                            m_syncedAvatars.Add(agentID, (RegionSyncAvatar)sp.ControllingClient);
                            RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Avatar {0} now owned by region {1}", sp.Name, m_regionName));
                            return;
                        }
                        else
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Presence is not a RegionSyncAvatar");
                            return;
                        }
                    }
                case RegionSyncMessage.MsgType.AvatarTeleportOut:
                    {
                        // Get the data from message and error check
                        OSDMap data = DeserializeMessage(msg);
                        if (data == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Could not deserialize JSON data.");
                            return;
                        }
                        UUID agentID = data["agentID"].AsUUID();
                        ScenePresence sp;
                        m_scene.TryGetScenePresence(agentID, out sp);
                        if (sp == null)
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Presence is not in scene");
                            return;
                        }

                        if (sp.ControllingClient is RegionSyncAvatar)
                        {
                            m_syncedAvatars.Remove(agentID);
                            RegionSyncMessage.HandleSuccess(LogHeader, msg, String.Format("Avatar {0} is no longer owned by region {1}", sp.Name, m_regionName));
                            return;
                        }
                        else
                        {
                            RegionSyncMessage.HandleError(LogHeader, msg, "Presence is not a RegionSyncAvatar");
                            return;
                        }
                    }
                default:
                    {
                        m_log.WarnFormat("{0} Unable to handle unsupported message type: {1}", LogHeader, msg.ToString());
                        return;
                    }
            }
        }

        private List<SurfaceTouchEventArgs> ExtractSurfaceArgList(OSDArray args)
        {
            List<SurfaceTouchEventArgs> surfaceArgs = new List<SurfaceTouchEventArgs>();
            for (int ii = 0; ii < args.Count; ii++)
            {
                SurfaceTouchEventArgs stea = new SurfaceTouchEventArgs();
                OSDMap entry = (OSDMap)args[ii];
                stea.Binormal = entry["binormal"].AsVector3();
                stea.FaceIndex = entry["faceIndex"].AsInteger();
                stea.Normal = entry["normal"].AsVector3();
                stea.Position = entry["position"].AsVector3();
                stea.STCoord = entry["stCoord"].AsVector3();
                stea.UVCoord = entry["uvCoord"].AsVector3();
                surfaceArgs.Add(stea);
            }
            return surfaceArgs;
        }

        private bool HandlerDebug(RegionSyncMessage msg, string handlerMessage)
        {
            m_log.WarnFormat("{0} DBG ({1}): {2}", LogHeader, msg.ToString(), handlerMessage);
            return true;
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
            //if (msg.Type == RegionSyncMessage.MsgType.AvatarAppearance)
                //m_log.WarnFormat("{0} Sending AvatarAppearance to client manager", LogHeader);
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
                        if(m_tcpclient.Connected)
                        {
                            try
                            {
                                m_tcpclient.GetStream().EndWrite(ar);
                            }
                            catch(Exception)
                            {}
                        }   
                    }, null);
                }
                catch (IOException)
                {
                    m_log.WarnFormat("{0} RegionSyncClient has disconnected.", LogHeader);
                }
            }
        }

        public int SyncedAvCount
        {
            get
            {
                lock (m_syncRoot)
                    return m_syncedAvatars.Count;
            }
        }

        public void ReportStatus()
        {
            int syncedAvCount = SyncedAvCount;
            lock (stats)
            {
                bool localcheck = true;
                bool remotecheck = true;
                bool totalcheck = true;
                if (syncedAvCount != lastLocalCount)
                    localcheck = false;
                if (m_scene.SceneGraph.GetRootAgentCount() != lastTotalCount)
                    totalcheck = false;
                if (m_scene.SceneGraph.GetRootAgentCount() - syncedAvCount != lastRemoteCount)
                    remotecheck = false;
                m_log.ErrorFormat("{0} Syncing {1,4} remote presences. Remote scene reporting {2,4} locals, {3,4} remotes, {4,4} total ({5},{6},{7})",
                    LogHeader, syncedAvCount, lastLocalCount, lastRemoteCount, lastTotalCount, localcheck ? " " : "!", remotecheck ? " " : "!", totalcheck ? " " : "!");
            }
        }

        public void BalanceClients(int targetLoad, string destinationRegion)
        {
            OSDMap data = new OSDMap(2);
            data["endCount"] = OSD.FromInteger(targetLoad);
            data["toRegion"] = OSD.FromString(destinationRegion);
            Send(new RegionSyncMessage(RegionSyncMessage.MsgType.BalanceClientLoad, OSDParser.SerializeJsonString(data)));
        }
    }
}

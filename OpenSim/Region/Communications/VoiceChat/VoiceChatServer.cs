using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework.Console;
using OpenSim.Framework.ServerStatus;
using OpenSim.Framework;
using libsecondlife;

namespace OpenSim.Region.Communications.VoiceChat
{
    public class VoiceChatServer
    {
        Thread m_listenerThread;
        Thread m_mainThread;
        Scene m_scene;
        Socket m_server;
        Socket m_selectCancel;

        Dictionary<Socket, VoiceClient> m_clients;
        Dictionary<LLUUID, VoiceClient> m_uuidToClient;


        public VoiceChatServer(Scene scene)
        {
            m_clients = new Dictionary<Socket, VoiceClient>();
            m_uuidToClient = new Dictionary<LLUUID, VoiceClient>();
            m_scene = scene;

            m_scene.EventManager.OnNewPresence += NewPresence;
            m_scene.EventManager.OnRemovePresence += RemovePresence;

            try
            {
                CreateListeningSocket();
            }
            catch (Exception e)
            {
                MainLog.Instance.Error("VOICECHAT", "Unable to start listening");
                return;
            }

            m_listenerThread = new Thread(new ThreadStart(ListenIncomingConnections));
            m_listenerThread.IsBackground = true;
            m_listenerThread.Start();

            m_mainThread = new Thread(new ThreadStart(RunVoiceChat));
            m_mainThread.IsBackground = true;
            m_mainThread.Start();

            Thread.Sleep(200);
            m_selectCancel = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            m_selectCancel.Connect("localhost", 59214);
        }

        public void NewPresence(ScenePresence presence)
        {
            MainLog.Instance.Verbose("VOICECHAT", "New scene presence: " + presence.UUID);
            lock (m_uuidToClient)
            {
                m_uuidToClient[presence.UUID] = null;
            }
        }

        public void RemovePresence(LLUUID uuid)
        {
            lock (m_uuidToClient)
            {
                if (m_uuidToClient.ContainsKey(uuid))
                {
                    if (m_uuidToClient[uuid] != null)
                    {
                        RemoveClient(m_uuidToClient[uuid].m_socket);
                    }
                    m_uuidToClient.Remove(uuid);
                }
                else
                {
                    MainLog.Instance.Error("VOICECHAT", "Presence not found on RemovePresence: " + uuid);
                }
            }
        }

        public bool AddClient(VoiceClient client, LLUUID uuid)
        {
            lock (m_uuidToClient)
            {
                if (m_uuidToClient.ContainsKey(uuid))
                {
                    if (m_uuidToClient[uuid] != null) {
                        MainLog.Instance.Warn("VOICECHAT", "Multiple login attempts for " + uuid);
                        return false;
                    }
                    m_uuidToClient[uuid] = client;
                    return true;
                } 
            }
            return false;
        }

        public void RemoveClient(Socket socket)
        {
            MainLog.Instance.Verbose("VOICECHAT", "Removing client");
            lock(m_clients)
            {
                VoiceClient client = m_clients[socket];

                lock(m_uuidToClient)
                {
                    if (m_uuidToClient.ContainsKey(client.m_clientId))
                    {
                        m_uuidToClient[client.m_clientId] = null;
                    }
                }

                m_clients.Remove(socket);
                client.m_socket.Close();
            }
        }

        protected void CreateListeningSocket()
        {
            IPEndPoint listenEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 12000);
            m_server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            m_server.Bind(listenEndPoint);
            m_server.Listen(50);
        }

        void ListenIncomingConnections()
        {
            MainLog.Instance.Verbose("VOICECHAT", "Listening connections...");
            ServerStatus.ReportThreadName("VoiceChat: Connection listener");

            byte[] dummyBuffer = new byte[1];

            while (true)
            {
                try
                {
                    Socket connection = m_server.Accept();
                    lock (m_clients)
                    {
                        m_clients[connection] = new VoiceClient(connection, this);
                        m_selectCancel.Send(dummyBuffer);
                        MainLog.Instance.Verbose("VOICECHAT", "Voicechat connection from " + connection.RemoteEndPoint.ToString());
                    }
                }
                catch (SocketException e)
                {
                    MainLog.Instance.Error("VOICECHAT", "During accept: " + e.ToString());
                }
            }
        }

        Socket ListenLoopbackSocket()
        {
            IPEndPoint listenEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 59214);
            Socket dummyListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            dummyListener.Bind(listenEndPoint);
            dummyListener.Listen(1);
            Socket socket = dummyListener.Accept();
            dummyListener.Close();
            return socket;
        }

        void RunVoiceChat()
        {
            MainLog.Instance.Verbose("VOICECHAT", "Connection handler started...");
            ServerStatus.ReportThreadName("VoiceChat: Connection handler");

            //Listen a loopback socket for aborting select call
            Socket dummySocket = ListenLoopbackSocket();
            
            MainLog.Instance.Verbose("VOICECHAT", "Got select abort socket...");

            List<Socket> sockets = new List<Socket>();
            byte[] buffer = new byte[65536];

            while (true)
            {
                if (m_clients.Count == 0)
                {
                    Thread.Sleep(100);
                    continue;
                }

                lock (m_clients)
                {
                    foreach (Socket s in m_clients.Keys)
                    {
                        sockets.Add(s);
                    }
                }
                sockets.Add(dummySocket);

                Socket.Select(sockets, null, null, 200000);

                foreach (Socket s in sockets)
                {
                    try
                    {
                        if (s.RemoteEndPoint != dummySocket.RemoteEndPoint)
                        {
                            ReceiveFromSocket(s, buffer);
                        }
                        else
                        {
                            if (s.Receive(buffer) <= 0) {
                                MainLog.Instance.Error("VOICECHAT", "Socket closed");
                            } else
                            {
                                MainLog.Instance.Verbose("VOICECHAT", "Select aborted");
                            }
                        }
                    }
                    catch(ObjectDisposedException e)
                    {
                        MainLog.Instance.Warn("VOICECHAT", "Connection has been already closed");
                    }
                    catch (Exception e)
                    {
                        MainLog.Instance.Error("VOICECHAT", "Exception: " + e.Message);

                        RemoveClient(s);
                    }
                }

                sockets.Clear();
            }
        }

        private void ReceiveFromSocket( Socket s, byte[] buffer )
        {
            int byteCount = s.Receive(buffer);
            if (byteCount <= 0)
            {
                MainLog.Instance.Verbose("VOICECHAT", "Connection lost to " + s.RemoteEndPoint);
                lock (m_clients)
                {
                    RemoveClient(s);
                }
            }
            else
            {
                ServerStatus.ReportInPacketTcp(byteCount);
                lock (m_clients)
                {
                    if (m_clients.ContainsKey(s))
                    {
                        m_clients[s].OnDataReceived(buffer, byteCount);
                    }
                    else
                    {
                        MainLog.Instance.Warn("VOICECHAT", "Got data from " + s.RemoteEndPoint +
                                                           ", but it's not registered as a voice client");
                    }
                }
            }
        }

        public void BroadcastVoice(VoicePacket packet)
        {
            libsecondlife.LLVector3 origPos = m_scene.GetScenePresence(packet.m_clientId).AbsolutePosition;

            byte[] bytes = packet.GetBytes();
            foreach (VoiceClient client in m_clients.Values)
            {
                if (client.IsEnabled() && client.m_clientId != packet.m_clientId &&
                    client.m_authenticated && client.IsCodecSupported(packet.m_codec))
                {
                    ScenePresence presence = m_scene.GetScenePresence(client.m_clientId);

                    if (presence != null && Util.GetDistanceTo(presence.AbsolutePosition, origPos) < 20)
                    {
                        client.SendTo(bytes);
                    }
                }
            }
        }
    }
}

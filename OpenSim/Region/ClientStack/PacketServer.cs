/*
* Copyright (c) Contributors, http://opensimulator.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
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
* 
*/

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;

namespace OpenSim.Region.ClientStack
{
    public class PacketServer
    {
        private static readonly log4net.ILog m_log 
            = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private ClientStackNetworkHandler m_networkHandler;
        private IScene m_scene;

        public struct QueuePacket
        {
            public Packet Packet;
            public uint CircuitCode;

            public QueuePacket(Packet p, uint c)
            {
                Packet = p;
                CircuitCode = c;
            }
        }

        private List<Thread> Threads = new List<Thread>();
        private BlockingQueue<QueuePacket> PacketQueue;

        //private readonly ClientManager m_clientManager = new ClientManager();
        //public ClientManager ClientManager
        //{
        //    get { return m_clientManager; }
        //}

        public PacketServer(ClientStackNetworkHandler networkHandler)
        {
            m_networkHandler = networkHandler;
            m_networkHandler.RegisterPacketServer(this);
            PacketQueue = new BlockingQueue<QueuePacket>();

            int ThreadCount = 4;
            m_log.Debug("[PacketServer]: launching " + ThreadCount.ToString() + " threads.");
            for (int x = 0; x < ThreadCount; x++)
            {
                Thread thread = new Thread(PacketRunner);
                thread.IsBackground = true;
                thread.Name = "Packet Runner";
                thread.Start();
                Threads.Add(thread);
            }
        }

        public void Enqueue(uint CircuitCode, Packet packet)
        {
            IClientAPI client;
            PacketQueue.Enqueue(new QueuePacket(packet, CircuitCode));
        }

        public IScene LocalScene
        {
            set { m_scene = value; }
        }

        private void PacketRunner()
        {
            while (true)
            {
                QueuePacket p;
                // Mantis 641
                lock(PacketQueue)
                    p = PacketQueue.Dequeue();
            
                if (p.Packet != null)
                {
                    m_scene.ClientManager.InPacket(p.CircuitCode, p.Packet);
                }
                else
                {
                    m_log.Debug("[PacketServer]: Empty packet from queue!");
                }
            }
        }

        public bool ClaimEndPoint(UseCircuitCodePacket usePacket, EndPoint ep)
        {
            IClientAPI client;
            if (m_scene.ClientManager.TryGetClient(usePacket.CircuitCode.Code, out client))
            {
                if (client.Claim(ep, usePacket))
                {
                    m_log.Debug("[PacketServer]: Claimed client.");
                    return true;
                }
            }
            m_log.Debug("[PacketServer]: Failed to claim client.");
            return false;
        }

        public virtual bool AddNewClient(uint circuitCode, AgentCircuitData agentData
            , AssetCache assetCache, AgentCircuitManager authenticateSessionsClass)
        {
            m_log.Debug("[PacketServer]: Creating new client for " + circuitCode.ToString());
            IClientAPI newuser;

            if (m_scene.ClientManager.TryGetClient(circuitCode, out newuser))
            {
                m_log.Debug("[PacketServer]: Already have client for code " + circuitCode.ToString());
                return false;
            }
            else
            {
                newuser = new ClientView(m_scene, assetCache, this, authenticateSessionsClass, agentData);

                m_scene.ClientManager.Add(circuitCode, newuser);

                newuser.OnViewerEffect += m_scene.ClientManager.ViewerEffectHandler;
                newuser.OnLogout += LogoutHandler;
                newuser.OnConnectionClosed += CloseClient;

                return true;
            }
        }

        public void LogoutHandler(IClientAPI client)
        {
            client.SendLogoutPacket();

            CloseClient(client);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="size"></param>
        /// <param name="flags"></param>
        /// <param name="circuitcode"></param>
        public virtual void SendPacketTo(byte[] buffer, int size, SocketFlags flags, uint circuitcode)
        {
            m_networkHandler.SendPacketTo(buffer, size, flags, circuitcode);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="circuitcode"></param>
        public virtual void CloseCircuit(uint circuitcode)
        {
            m_networkHandler.RemoveClientCircuit(circuitcode);

            //m_scene.ClientManager.CloseAllAgents(circuitcode);
        }

        /// <summary>
        /// Completely close down the given client.
        /// </summary>
        /// <param name="client"></param>
        public virtual void CloseClient(IClientAPI client)
        {
            //m_log.Info("PacketServer:CloseClient()");
         
            CloseCircuit(client.CircuitCode);
            m_scene.ClientManager.Remove(client.CircuitCode);                
            client.Close(false);
        }
    }
}

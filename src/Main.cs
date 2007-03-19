/*
Copyright (c) OpenSim project, http://osgrid.org/

* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Text;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.world;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.LocalServers;
using OpenSim.Assets;
using ServerConsole;
using PhysicsSystem;

namespace OpenSim
{
    /// <summary>
    /// Description of MainForm.
    /// </summary>
    public class OpenSimMain
    {
        private static OpenSimMain instance = null;

        public static OpenSimMain Instance
        {
            get
            {
                return instance;
            }
        }

        public World LocalWorld;
        public Grid GridServers;
        public SimConfig Cfg;
        public SimCAPSHTTPServer HttpServer;
        public AssetCache AssetCache;
        public InventoryManager InventoryCache;

        public Socket Server;
        private IPEndPoint ServerIncoming;
        private byte[] RecvBuffer = new byte[4096];
        private byte[] ZeroBuffer = new byte[8192];
        private IPEndPoint ipeSender;
        private EndPoint epSender;
        private AsyncCallback ReceivedData;

        public DateTime startuptime;
        public Dictionary<EndPoint, OpenSimClient> ClientThreads = new Dictionary<EndPoint, OpenSimClient>();
        private PhysicsManager physManager;
        private System.Timers.Timer timer1 = new System.Timers.Timer();
        private string ConfigDll = "SimConfig.dll";
        private string _physicsEngine = "PhysX";
        public bool sandbox = false;
        public bool loginserver = false;

        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("OpenSim " + VersionInfo.Version + "\n");
            Console.WriteLine("Starting...\n");
            ServerConsole.MainConsole.Instance = new MServerConsole(ServerConsole.ConsoleBase.ConsoleType.Local, "", 0);

            instance = new OpenSimMain();

            Instance.sandbox = false;
            Instance.loginserver = false;
            Instance._physicsEngine = "PhysX";

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-sandbox")
                {
                    Instance.sandbox = true;
                }

                if (args[i] == "-loginserver")
                {
                    Instance.loginserver = true;
                }
                if (args[i] == "-realphysx")
                {
                    Instance._physicsEngine = "RealPhysX";
                    OpenSim.world.Avatar.PhysicsEngineFlying = true;
                }
            }

            OpenSimMain.Instance.GridServers = new Grid();
            if (Instance.sandbox)
            {
                OpenSimMain.Instance.GridServers.AssetDll = "LocalGridServers.dll";
                OpenSimMain.Instance.GridServers.GridDll = "LocalGridServers.dll";
                OpenSimMain.Instance.GridServers.LoadPlugins();
                ServerConsole.MainConsole.Instance.WriteLine("Starting in Sandbox mode");
            }
            else
            {
                OpenSimMain.Instance.GridServers.AssetDll = "RemoteGridServers.dll";
                OpenSimMain.Instance.GridServers.GridDll = "RemoteGridServers.dll";
                OpenSimMain.Instance.GridServers.LoadPlugins();
                ServerConsole.MainConsole.Instance.WriteLine("Starting in Grid mode");
            }

            if (Instance.loginserver && Instance.sandbox)
            {
                LoginServer loginServer = new LoginServer(OpenSimMain.Instance.GridServers.GridServer);
                loginServer.Startup();
            }
            Instance.AssetCache = new AssetCache(OpenSimMain.Instance.GridServers.AssetServer);
            Instance.InventoryCache = new InventoryManager();

            Instance.Startup();

            while (true)
            {
                ServerConsole.MainConsole.Instance.MainConsolePrompt();
            }
        }

        private OpenSimMain()
        {
        }

        private void Startup()
        {
            startuptime = DateTime.Now;

            // We check our local database first, then the grid for config options
            ServerConsole.MainConsole.Instance.WriteLine("Main.cs:Startup() - Loading configuration");
            Cfg = this.LoadConfigDll(this.ConfigDll);
            Cfg.InitConfig(this.sandbox);
            ServerConsole.MainConsole.Instance.WriteLine("Main.cs:Startup() - Contacting gridserver");
            Cfg.LoadFromGrid();

            ServerConsole.MainConsole.Instance.WriteLine("Main.cs:Startup() - We are " + Cfg.RegionName + " at " + Cfg.RegionLocX.ToString() + "," + Cfg.RegionLocY.ToString());
            ServerConsole.MainConsole.Instance.WriteLine("Initialising world");
            Instance.LocalWorld = new World();
            Instance.LocalWorld.LandMap = Cfg.LoadWorld();

            this.physManager = new PhysicsSystem.PhysicsManager();
            this.physManager.LoadPlugins();
            ServerConsole.MainConsole.Instance.WriteLine("Main.cs:Startup() - Starting up messaging system");
            Instance.LocalWorld.PhysScene = this.physManager.GetPhysicsScene(this._physicsEngine); //should be reading from the config file what physics engine to use
            Instance.LocalWorld.PhysScene.SetTerrain(Instance.LocalWorld.LandMap);

            OpenSimMain.Instance.GridServers.AssetServer.SetServerInfo(OpenSimMain.Instance.Cfg.AssetURL, OpenSimMain.Instance.Cfg.AssetSendKey);
            OpenSimMain.Instance.GridServers.GridServer.SetServerInfo(OpenSimMain.Instance.Cfg.GridURL, OpenSimMain.Instance.Cfg.GridSendKey, OpenSimMain.Instance.Cfg.GridRecvKey);

            Instance.LocalWorld.LoadStorageDLL("Db4LocalStorage.dll"); //all these dll names shouldn't be hard coded.
            Instance.LocalWorld.LoadPrimsFromStorage();

            if (this.sandbox)
            {
                this.AssetCache.LoadDefaultTextureSet();
            }

            ServerConsole.MainConsole.Instance.WriteLine("Main.cs:Startup() - Starting CAPS HTTP server");
            HttpServer = new SimCAPSHTTPServer();

            timer1.Enabled = true;
            timer1.Interval = 100;
            timer1.Elapsed += new ElapsedEventHandler(this.Timer1Tick);

            MainServerListener();
        }

        private SimConfig LoadConfigDll(string dllName)
        {
            Assembly pluginAssembly = Assembly.LoadFrom(dllName);
            SimConfig config = null;

            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (pluginType.IsPublic)
                {
                    if (!pluginType.IsAbstract)
                    {
                        Type typeInterface = pluginType.GetInterface("ISimConfig", true);

                        if (typeInterface != null)
                        {
                            ISimConfig plug = (ISimConfig)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                            config = plug.GetConfigObject();
                            break;
                        }

                        typeInterface = null;
                    }
                }
            }
            pluginAssembly = null;
            return config;
        }

        private void OnReceivedData(IAsyncResult result)
        {
            ipeSender = new IPEndPoint(IPAddress.Any, 0);
            epSender = (EndPoint)ipeSender;
            Packet packet = null;
            int numBytes = Server.EndReceiveFrom(result, ref epSender);
            int packetEnd = numBytes - 1;
            packet = Packet.BuildPacket(RecvBuffer, ref packetEnd, ZeroBuffer);

            // This is either a new client or a packet to send to an old one
            if (ClientThreads.ContainsKey(epSender))
            {
                ClientThreads[epSender].InPacket(packet);
            }
            else if (packet.Type == PacketType.UseCircuitCode)
            { // new client
                OpenSimClient newuser = new OpenSimClient(epSender, (UseCircuitCodePacket)packet);
                ClientThreads.Add(epSender, newuser);
            }
            else
            { // invalid client
                Console.Error.WriteLine("Main.cs:OnReceivedData() - WARNING: Got a packet from an invalid client - " + epSender.ToString());
            }
            Server.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender, ReceivedData, null);
        }

        private void MainServerListener()
        {
            ServerConsole.MainConsole.Instance.WriteLine("Main.cs:MainServerListener() - New thread started");
            ServerConsole.MainConsole.Instance.WriteLine("Main.cs:MainServerListener() - Opening UDP socket on " + Cfg.IPListenAddr + ":" + Cfg.IPListenPort);

            ServerIncoming = new IPEndPoint(IPAddress.Any, Cfg.IPListenPort);
            Server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Server.Bind(ServerIncoming);

            ServerConsole.MainConsole.Instance.WriteLine("Main.cs:MainServerListener() - UDP socket bound, getting ready to listen");

            ipeSender = new IPEndPoint(IPAddress.Any, 0);
            epSender = (EndPoint)ipeSender;
            ReceivedData = new AsyncCallback(this.OnReceivedData);
            Server.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender, ReceivedData, null);

            ServerConsole.MainConsole.Instance.WriteLine("Main.cs:MainServerListener() - Listening...");

        }

        public static void Shutdown()
        {
            ServerConsole.MainConsole.Instance.WriteLine("Main.cs:Shutdown() - Closing all threads");
            ServerConsole.MainConsole.Instance.WriteLine("Main.cs:Shutdown() - Killing listener thread");
            ServerConsole.MainConsole.Instance.WriteLine("Main.cs:Shutdown() - Killing clients");
            // IMPLEMENT THIS
            ServerConsole.MainConsole.Instance.WriteLine("Main.cs:Shutdown() - Closing console and terminating");
            OpenSimMain.Instance.LocalWorld.Close();
            OpenSimMain.Instance.GridServers.Close();
            ServerConsole.MainConsole.Instance.Close();
            Environment.Exit(0);
        }

        void Timer1Tick(object sender, System.EventArgs e)
        {

            Instance.LocalWorld.Update();
        }
    }

    
}

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
 *     * Neither the name of the OpenSimulator Project nor the
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
 */

using System;
using System.IO;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Base;
using OpenSim.Framework.Servers.HttpServer;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using Mono.Addins;
using log4net;

using Ux = OpenSim.Services.IntegrationService.IUtils;

[assembly:AddinRoot ("IntegrationService", "1.0")]

namespace OpenSim.Services.IntegrationService
{
    [TypeExtensionPoint (Path="/OpenSim/IntegrationService", Name="IntegrationService")]
    public interface IntegrationPlugin
    {
        void Init(IConfigSource PluginConfig);
        string Name{ get; }
        string ConfigName { get; }
        string DefaultConfig { get; }
    }

     public class IntegrationServiceBase : ServiceBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ConfigName = "IntegrationService";
        
        protected IPresenceService m_PresenceService;
        protected IGridService m_GridService;
        protected IHttpServer m_Server;
        protected string m_IntegrationConfig;
        IConfig m_IntegrationServerConfig;
        string m_IntegrationConfigLoc;

        public IntegrationServiceBase(IConfigSource config, IHttpServer server)
            : base(config)
        {

            IConfig serverConfig = config.Configs[m_ConfigName];
            if (serverConfig == null)
                throw new Exception(String.Format("No section {0} in config file", m_ConfigName));

            // defaults to the ./bin directory
            string RegistryLocation = serverConfig.GetString("PluginRegistryLocation",
                    ".");

            // Deal with files only for now - will add url/environment later
            m_IntegrationConfigLoc = serverConfig.GetString("IntegrationConfig", String.Empty);
              if(String.IsNullOrEmpty(m_IntegrationConfigLoc))
                m_log.Error("[INTEGRATION SERVICE]: No IntegrationConfig defined in the Robust.ini");

            AddinManager.AddinLoaded += on_addinloaded_;
            AddinManager.AddinLoadError += on_addinloaderror_;

            m_Server = server;

            m_IntegrationServerConfig = config.Configs["IntegrationService"];
            if (m_IntegrationServerConfig == null)
            {
                throw new Exception("[INTEGRATION SERVICE]: Missing configuration");
                return;
            }


            // Add a command to the console
            if (MainConsole.Instance != null)
            {
                MainConsole.Instance.Commands.AddCommand("Integration", true,
                            "show repos",
                            "show repos",
                            "Show list of registered plugin repositories",
                            String.Empty,
                            HandleShowRepos);
            }

            suppress_console_output_(true);
            AddinManager.Initialize (RegistryLocation);
            AddinManager.Registry.Update ();
            suppress_console_output_(false);

            foreach (IntegrationPlugin cmd in AddinManager.GetExtensionObjects("/OpenSim/IntegrationService"))
            {
                string ConfigPath = String.Format("{0}/(1)", m_IntegrationConfigLoc,cmd.ConfigName);
                IConfigSource PlugConfig = Ux.GetConfigSource(m_IntegrationConfigLoc, cmd.ConfigName);

                // Fetch the starter ini
                if (PlugConfig == null)
                {

                    m_log.InfoFormat("[INTEGRATION SERVICE]: Fetching starter config for {0} from {1}", cmd.Name, cmd.DefaultConfig);

                    // Send the default data service
                    IConfig DataService = config.Configs["DatabaseService"];
                    m_log.InfoFormat("[INTEGRATION SERVICE]: Writing initial config to {0}", cmd.ConfigName);
                    // FileStream fs = File.Create(Path.Combine(m_IntegrationConfigLoc,cmd.ConfigName));
                    // System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
                    // Byte[] buf = enc.GetBytes("; Automatically Generated Configuration - Edit for your installation\n" );
                    // fs.Write(buf, 0, buf.Length);
                    // fs.Close();

                    IniConfigSource source = new IniConfigSource();
                    IConfig Init = source.AddConfig("DatabaseService");
                    Init.Set("StorageProvider",(string)DataService.GetString("StorageProvider"));
                    Init.Set("ConnectionString", (string)DataService.GetString("ConnectionString"));


                    PlugConfig = Ux.LoadInitialConfig(cmd.DefaultConfig);

                    source.Merge(PlugConfig);

                    source.Save(Path.Combine(m_IntegrationConfigLoc, cmd.ConfigName));

                    PlugConfig = source;
                }

                // We maintain a configuration per-plugin to enhance modularity
                // If ConfigSource is null, we will get the default from the repo
                // and write it to our directory
                cmd.Init (PlugConfig);
                server.AddStreamHandler((IRequestHandler)cmd);
                m_log.InfoFormat("[INTEGRATION SERVICE]: Loading IntegrationService plugin {0}", cmd.Name);
            }
        }

        private IConfigSource GetConfig(string configName)
        {
            return new IniConfigSource();
        }

        private void on_addinloaderror_(object sender, AddinErrorEventArgs args)
        {
            if (args.Exception == null)
                m_log.Error ("[INTEGRATION SERVICE]: Plugin Error: "
                        + args.Message);
            else
                m_log.Error ("[INTEGRATION SERVICE]: Plugin Error: "
                        + args.Exception.Message + "\n"
                        + args.Exception.StackTrace);
        }

        private void on_addinloaded_(object sender, AddinEventArgs args)
        {
            m_log.Info ("[INTEGRATION SERVICE]: Plugin Loaded: " + args.AddinId);
        }

        private static TextWriter prev_console_;
        public void suppress_console_output_(bool save)
        {
            if (save)
            {
                prev_console_ = System.Console.Out;
                System.Console.SetOut(new StreamWriter(Stream.Null));
            }
            else
            {
                if (prev_console_ != null)
                    System.Console.SetOut(prev_console_);
            }
        }




        #region console handlers
        private void HandleShowRepos(string module, string[] cmd)
        {
            if ( cmd.Length < 2 )
            {
                MainConsole.Instance.Output("Syntax: show repos");
                return;
            }

//            List<UserData> list = m_Database.ListNames();
//
//            foreach (UserData name in list)
//            {
//                MainConsole.Instance.Output(String.Format("{0} {1}",name.FirstName, name.LastName));
//            }
        }
        #endregion
    }
}

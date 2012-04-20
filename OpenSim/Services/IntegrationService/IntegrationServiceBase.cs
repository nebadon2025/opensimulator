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

[assembly:AddinRoot ("IntegrationService", "2.0")]

namespace OpenSim.Services.IntegrationService
{
    [TypeExtensionPoint (Path="/OpenSim/IntegrationService", Name="IntegrationService")]
    public interface IntegrationPlugin: IDisposable
    {
        void Init(IConfigSource PluginConfig, IHttpServer server, ServiceBase service);
        void Unload();
        string Name { get; }
        string ConfigName { get; }
        string DefaultConfig { get; }
        void Dispose();
    }

    // Hide the nasty stuff in here, let the IntegrationService be clean for
    // our command and request handlers
    public class IntegrationServiceBase : ServiceBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ConfigName = "IntegrationService";
        
        // protected IPresenceService m_PresenceService;
        // protected IGridService m_GridService;
        protected IHttpServer m_Server;

        protected string m_IntegrationConfig;
        protected PluginManager m_PluginManager;
        AddinManager am;

        //
        protected IConfig m_IntegrationServerConfig;
        protected string m_IntegrationConfigLoc;
        IConfigSource m_ConfigSource;

        public IntegrationServiceBase(IConfigSource config, IHttpServer server)
            : base(config)
        {
            m_ConfigSource = config;
            m_Server = server;

            IConfig serverConfig = m_ConfigSource.Configs[m_ConfigName];
            if (serverConfig == null)
                throw new Exception(String.Format("No section {0} in config file", m_ConfigName));
            
            m_IntegrationConfigLoc = serverConfig.GetString("IntegrationConfig", String.Empty);
            AddinRegistry registry ;
            bool DEVELOPMENT = serverConfig.GetBoolean("DevelopmentMode", false);

            // Are we developing plugins? We will load them now
            if (DEVELOPMENT == true)
            {
                AddinManager.Initialize (".");
                registry = new AddinRegistry(".", ".");
                registry.Update ();
    
                AddinManager.AddinLoaded += on_addinloaded_;
                AddinManager.AddinLoadError += on_addinloaderror_;
                AddinManager.AddinUnloaded += HandleAddinManagerAddinUnloaded;
                AddinManager.AddinEngine.ExtensionChanged += HandleAddinManagerAddinEngineExtensionChanged;
                // AddinManager.GetExtensionObjects("/OpenSim/IntegrationService");
    
                registry.Update ();
                foreach (IntegrationPlugin cmd in AddinManager.GetExtensionObjects("/OpenSim/IntegrationService"))
                {
                    m_log.InfoFormat("[INTEGRATION SERVICE]: Processing _Addin {0}", cmd.Name);
                    LoadingPlugin(cmd);
                }
    
                Addin[] addins = registry.GetAddins();
    
                foreach (Addin addin in addins)
                {
                    if (addin.Description.Category == "IntegrationPlugin")
                    {
                        m_log.InfoFormat("[INTEGRATION SERVICE]: Processing O Addin {0}", addin.Name);
                        addin.Enabled = true;
                        registry.EnableAddin(addin.Id);
                        registry.Update();
                        AddinManager.AddinEngine.LoadAddin(null, addin.Id);
                    }
                }
            }
            else
            {
                // defaults to the ./bin directory
                string RegistryLocation = serverConfig.GetString("PluginRegistryLocation",
                        ".");
    
                registry = new AddinRegistry(RegistryLocation, ".");
    
                m_PluginManager = new PluginManager(registry);
    
                // Deal with files only for now - will add url/environment later
                m_IntegrationConfigLoc = serverConfig.GetString("IntegrationConfig", String.Empty);
                  if(String.IsNullOrEmpty(m_IntegrationConfigLoc))
                    m_log.Error("[INTEGRATION SERVICE]: No IntegrationConfig defined in the Robust.ini");
    
    
                m_IntegrationServerConfig = m_ConfigSource.Configs["IntegrationService"];
                if (m_IntegrationServerConfig == null)
                {
                    throw new Exception("[INTEGRATION SERVICE]: Missing configuration");
                    return;
                }
    
                AddinManager.Initialize (RegistryLocation);
                AddinManager.Registry.Update ();
    
                AddinManager.AddinLoaded += on_addinloaded_;
                AddinManager.AddinLoadError += on_addinloaderror_;
                AddinManager.AddinUnloaded += HandleAddinManagerAddinUnloaded;
                // AddinManager.AddinEngine.ExtensionChanged += HandleAddinManagerAddinEngineExtensionChanged;
    
                AddinManager.AddExtensionNodeHandler ("/OpenSim/IntegrationService", OnExtensionChanged);

            }
        }

        void HandleAddinManagerAddinEngineExtensionChanged (object sender, ExtensionEventArgs args)
        {
            MainConsole.Instance.Output(String.Format ("Plugin Extension Change Path:{0}", args.Path));
        }

        private IConfigSource GetConfig(string configName)
        {
            return new IniConfigSource();
        }

        void HandleAddinManagerAddinUnloaded (object sender, AddinEventArgs args)
        {
            MainConsole.Instance.Output("Plugin Unloaded");
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

        // This is our init
        // We can do build-up and tear-down of our plugin
        void OnExtensionChanged (object s, ExtensionNodeEventArgs args)
        {
            IntegrationPlugin ip = (IntegrationPlugin) args.ExtensionObject;

            m_log.Info ("[INTEGRATION SERVICE]: Plugin Change");

            switch (args.Change)
            {
                // Build up
                case ExtensionChange.Add:

                    m_log.InfoFormat("[INTEGRATION SERVICE]: Plugin Added {0}", ip.Name);
                    LoadingPlugin(ip);
                    return;

                // Tear down
                case ExtensionChange.Remove:

                    m_log.InfoFormat("[INTEGRATION SERVICE]: Plugin Remove {0}", ip.Name);
                    UnLoadingPlugin(ip);
                    return;
            }
        }

        private void on_addinloaded_(object sender, AddinEventArgs args)
        {
            m_log.Info ("[INTEGRATION SERVICE]: Plugin Loaded: " + args.AddinId);
        }

        private void LoadingPlugin(IntegrationPlugin plugin)
        {
            string ConfigPath = String.Format("{0}/(1)", m_IntegrationConfigLoc,plugin.ConfigName);
            IConfigSource PlugConfig = Ux.GetConfigSource(m_IntegrationConfigLoc, plugin.ConfigName);

            // We maintain a configuration per-plugin to enhance modularity
            // If ConfigSource is null, we will get the default from the repo
            // and write it to our directory
            // Fetch the starter ini
            if (PlugConfig == null)
            {
                m_log.InfoFormat("[INTEGRATION SERVICE]: Fetching starter config for {0} from {1}", plugin.Name, plugin.DefaultConfig);

                // Send the default data service
                IConfig DataService = m_ConfigSource.Configs["DatabaseService"];
                m_log.InfoFormat("[INTEGRATION SERVICE]: Writing initial config to {0}", plugin.ConfigName);

                IniConfigSource source = new IniConfigSource();
                IConfig Init = source.AddConfig("DatabaseService");
                Init.Set("StorageProvider",(string)DataService.GetString("StorageProvider"));
				Init.Set("ConnectionString", String.Format ("\"{0}\"",DataService.GetString("ConnectionString")));


                PlugConfig = Ux.LoadInitialConfig(plugin.DefaultConfig);

                source.Merge(PlugConfig);

                source.Save(Path.Combine(m_IntegrationConfigLoc, plugin.ConfigName));

				PlugConfig = Ux.GetConfigSource(m_IntegrationConfigLoc, plugin.ConfigName);
                // PlugConfig = source;
            }

            m_log.InfoFormat("[INTEGRATION SERVICE]: ****** In Loading Plugin {0}", plugin.Name);
            plugin.Init(PlugConfig, m_Server, this);
        }

        private void UnLoadingPlugin(IntegrationPlugin plugin)
        {
            MainConsole.Instance.Output(plugin.Name);
            plugin.Unload();
        }
    }
}

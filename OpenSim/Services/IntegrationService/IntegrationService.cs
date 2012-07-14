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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using Ux = OpenSim.Services.IntegrationService.IntegrationUtils;


namespace OpenSim.Services.IntegrationService
{
    public class IntegrationService : IntegrationServiceBase, IIntegrationService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public IntegrationService(IConfigSource config, IHttpServer server)
            : base(config, server)
        {
            m_log.DebugFormat("[INTEGRATION SERVICE]: Loaded");

            // Add commands to the console
            if (MainConsole.Instance != null)
            {
                AddConsoleCommands();
            }
        }

        // Our console commands
        private void AddConsoleCommands()
        {
            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "install", "install \"plugin name\"", "Install plugin from repository",
                                                     HandleConsoleInstallPlugin);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "uninstall", "uninstall \"plugin name\"", "Remove plugin from repository",
                                                     HandleConsoleUnInstallPlugin);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "check installed", "check installed \"plugin name=\"","Check installed plugin",
                                                     HandleConsoleCheckInstalledPlugin);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "list installed", "list installed \"plugin name=\"","List install plugins",
                                                     HandleConsoleListInstalledPlugin);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "list available", "list available \"plugin name=\"","List available plugins",
                                                     HandleConsoleListAvailablePlugin);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "list updates", "list updates","List availble updates",
                                                     HandleConsoleListUpdates);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "update", "update \"plugin name=\"","Update the plugin",
                                                     HandleConsoleUpdatePlugin);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "add repo", "add repo \"url\"","Add repository",
                                                     HandleConsoleAddRepo);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "get repo", "get repo \"url\"", "Sync with a registered repository",
                                                     HandleConsoleGetRepo);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "remove repo", "remove repo \"[url | index]\"","Remove registered repository",
                                                     HandleConsoleRemoveRepo);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "enable repo", "enable repo \"[url | index]\"","Enable registered repository",
                                                     HandleConsoleEnableRepo);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "disable repo", "disable repo \"[url | index]\"","Disable registered repository",
                                                     HandleConsoleDisableRepo);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "list repos", "list repos","List registered repositories",
                                                     HandleConsoleListRepos);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "show info", "show info \"plugin name\"","Show detailed information for plugin",
                                                     HandleConsoleShowAddinInfo);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "disable plugin", "disable plugin \"plugin name\"","disable the plugin",
                                                     HandleConsoleDisablePlugin);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "enable plugin", "enable plugin \"plugin name\"","enable the plugin",
                                                     HandleConsoleEnablePlugin);
        }

        #region console handlers
        // Handle our console commands
        //
        // Install plugin from registered repository
        /// <summary>
        /// Handles the console install plugin command. Attempts to install the selected plugin
        /// and 
        /// </summary>
        /// <param name='module'>
        /// Module.
        /// </param>
        /// <param name='cmd'>
        /// Cmd.
        /// </param>
        private void HandleConsoleInstallPlugin(string module, string[] cmd)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (cmd.Length == 2)
            {
                int ndx = Convert.ToInt16(cmd[1]);
                if (m_PluginManager.InstallPlugin(ndx, out result) == true)
                {
                    ArrayList s = new ArrayList();
                    s.AddRange(result.Keys);
                    s.Sort();

                    var list = result.Keys.ToList();
                    list.Sort();
                    foreach (var k in list)
                    {
                        Dictionary<string, object> plugin = (Dictionary<string, object>)result[k];
                        bool enabled = (bool)plugin["enabled"];
                        MainConsole.Instance.OutputFormat("{0}) {1} {2} rev. {3}",
                                                  k,
                                                  enabled == true ? "[ ]" : "[X]",
                                                  plugin["name"], plugin["version"]);
                    }
                }
            }
            return;
        }

        // Remove installed plugin
        private void HandleConsoleUnInstallPlugin(string module, string[] cmd)
        {
            if (cmd.Length == 2)
            {
                int ndx = Convert.ToInt16(cmd[1]);
                m_PluginManager.UnInstall(ndx);
            }
            return;
        }

        // Check installed plugins **not working
        private void HandleConsoleCheckInstalledPlugin(string module, string[] cmd)
        {
            MainConsole.Instance.Output(m_PluginManager.CheckInstalled());
            return;
        }

        // List installed plugins
        private void HandleConsoleListInstalledPlugin(string module, string[] cmd)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            m_PluginManager.ListInstalledAddins(out result);

            ArrayList s = new ArrayList();
            s.AddRange(result.Keys);
            s.Sort();

            var list = result.Keys.ToList();
            list.Sort();
            foreach (var k in list)
            {
                Dictionary<string, object> plugin = (Dictionary<string, object>)result[k];
                bool enabled = (bool)plugin["enabled"];
                MainConsole.Instance.OutputFormat("{0}) {1} {2} rev. {3}",
                                                  k,
                                                  enabled == true ? "[ ]" : "[X]",
                                                  plugin["name"], plugin["version"]);
            }
            return;
        }

        // List available plugins on registered repositories
        private void HandleConsoleListAvailablePlugin(string module, string[] cmd)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            m_PluginManager.ListAvailable(out result);

            var list = result.Keys.ToList();
            list.Sort();
            foreach (var k in list)
            {
                // name, version, repository
                Dictionary<string, object> plugin = (Dictionary<string, object>)result[k];
                MainConsole.Instance.OutputFormat("{0}) {1} rev. {2} {3}",
                                                  k,
                                                  plugin["name"],
                                                  plugin["version"],
                                                  plugin["repository"]);
            }
            return;
        }

        // List available updates **not ready
        private void HandleConsoleListUpdates(string module, string[] cmd)
        {
            m_PluginManager.ListUpdates();
            return;
        }

        // Update plugin **not ready
        private void HandleConsoleUpdatePlugin(string module, string[] cmd)
        {
            MainConsole.Instance.Output(m_PluginManager.Update());
            return;
        }

        // Register repository
        private void HandleConsoleAddRepo(string module, string[] cmd)
        {
            if ( cmd.Length == 3)
            {
                m_PluginManager.AddRepository(cmd[2]);
            }
            return;
        }

        // Get repository status **not working
        private void HandleConsoleGetRepo(string module, string[] cmd)
        {
            m_PluginManager.GetRepository();
            return;
        }

        // Remove registered repository
        private void HandleConsoleRemoveRepo(string module, string[] cmd)
        {
            if (cmd.Length == 3)
                m_PluginManager.RemoveRepository(cmd);
            return;
        }

        // Enable repository
        private void HandleConsoleEnableRepo(string module, string[] cmd)
        {
            m_PluginManager.EnableRepository(cmd);
            return;
        }

        // Disable repository
        private void HandleConsoleDisableRepo(string module, string[] cmd)
        {
            m_PluginManager.DisableRepository(cmd);
            return;
        }

        // List repositories
        private void HandleConsoleListRepos(string module, string[] cmd)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            m_PluginManager.ListRepositories(out result);

            var list = result.Keys.ToList();
            list.Sort();
            foreach (var k in list)
            {
                Dictionary<string, object> repo = (Dictionary<string, object>)result[k];
                bool enabled = (bool)repo["enabled"];
                MainConsole.Instance.OutputFormat("{0}) {1} {2}",
                                                  k,
                                                  enabled == true ? "[ ]" : "[X]",
                                                  repo["name"], repo["url"]);
            }

            return;
        }

        // Show description information
        private void HandleConsoleShowAddinInfo(string module, string[] cmd)
        {
            if (cmd.Length >= 3)
            {
                
                Dictionary<string, object> result = new Dictionary<string, object>();

                int ndx = Convert.ToInt16(cmd[2]);
                m_PluginManager.AddinInfo(ndx, out result);

                MainConsole.Instance.OutputFormat("Name: {0}\nURL: {1}\nFile: {2}\nAuthor: {3}\nCategory: {4}\nDesc: {5}",
                                                  result["name"],
                                                  result["url"],
                                                  result["file_name"],
                                                  result["author"],
                                                  result["category"],
                                                  result["description"]);

                return;
            }
        }

        // Disable plugin
        private void HandleConsoleDisablePlugin(string module, string[] cmd)
        {
            m_PluginManager.DisablePlugin(cmd);
            return;
        }

        // Enable plugin
        private void HandleConsoleEnablePlugin(string module, string[] cmd)
        {
            m_PluginManager.EnablePlugin(cmd);
            return;
        }
        #endregion

        #region IIntegrationService implementation
        // Will hold back on implementing things here that can actually make changes
        // Need to secure it first
        public byte[] HandleWebListRepositories(OSDMap request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            m_PluginManager.ListRepositories(out result);
            string json = LitJson.JsonMapper.ToJson(result);
            return Ux.DocToBytes(json);
        }

        public byte[] HandleWebAddRepository(OSDMap request)
        {
            return Ux.FailureResult("Not Implemented");
        }

        public byte[] HandleWebRemoveRepositroy(OSDMap request)
        {
            return Ux.FailureResult("Not Implemented");
        }

        public byte[] HandleEnableRepository(OSDMap request)
        {
            return Ux.FailureResult("Not Implemented");
        }

        public byte[] HandleWebDisableRepository(OSDMap request)
        {
            return Ux.FailureResult("Not Implemented");
        }

        public byte[] HandleWebListPlugins(OSDMap request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            m_PluginManager.ListInstalledAddins(out result);
            string json = LitJson.JsonMapper.ToJson(result);
            return Ux.DocToBytes(json);
        }

        public byte[] HandleWebPluginInfo(OSDMap request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if(!String.IsNullOrEmpty(request["index"].ToString()))
            {
                int ndx = Convert.ToInt16(request["index"].ToString());
                m_PluginManager.AddinInfo(ndx, out result);
                string json = LitJson.JsonMapper.ToJson(result);
                return Ux.DocToBytes(json);
            }
            else
            {
                return Ux.FailureResult("No index supplied");
            }
        }

        public byte[] HandleWebListAvailablePlugins(OSDMap request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            m_PluginManager.ListAvailable(out result);
            string json = LitJson.JsonMapper.ToJson(result);
            return Ux.DocToBytes(json);
        }

        public byte[] HandleWebInstallPlugin(OSDMap request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            int ndx = Convert.ToInt16(request["index"].ToString());
            if (m_PluginManager.InstallPlugin(ndx, out result) == true)
            {
                string json = LitJson.JsonMapper.ToJson(result);
                return Ux.DocToBytes(json);
            }
            else
            {
                return Ux.FailureResult("No index supplied");
            }
        }

        public byte[] HandleWebUnInstallPlugin(OSDMap request)
        {
            return Ux.FailureResult("Not Implemented");
        }

        public byte[] HandleWebEnablePlugin(OSDMap request)
        {
            return Ux.FailureResult("Not Implemented");
        }

        public byte[] HandleWebDisablePlugin(OSDMap request)
        {
            return Ux.FailureResult("Not Implemented");
        }
        #endregion
    }
}
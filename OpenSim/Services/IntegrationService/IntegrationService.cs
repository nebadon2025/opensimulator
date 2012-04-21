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
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Nini.Config;
using log4net;
using Mono.Addins;

using Ux = OpenSim.Services.IntegrationService.IUtils;

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
        private void HandleConsoleInstallPlugin(string module, string[] cmd)
        {
            MainConsole.Instance.Output(m_PluginManager.InstallPlugin(cmd));
            return;
        }

        private void HandleConsoleUnInstallPlugin(string module, string[] cmd)
        {
            if (cmd.Length == 2)
            {
                m_PluginManager.UnInstall(cmd);
            }
            return;
        }

        private void HandleConsoleCheckInstalledPlugin(string module, string[] cmd)
        {
            MainConsole.Instance.Output(m_PluginManager.CheckInstalled());
            return;
        }

        private void HandleConsoleListInstalledPlugin(string module, string[] cmd)
        {
            m_PluginManager.ListInstalledAddins();
            return;
        }

        private void HandleConsoleListAvailablePlugin(string module, string[] cmd)
        {
            ArrayList list = m_PluginManager.ListAvailable();
            foreach (string entry in list)
                MainConsole.Instance.Output(entry);

            return;
        }

        private void HandleConsoleListUpdates(string module, string[] cmd)
        {
            m_PluginManager.ListUpdates();
            return;
        }

        private void HandleConsoleUpdatePlugin(string module, string[] cmd)
        {
            MainConsole.Instance.Output(m_PluginManager.Update());
            return;
        }

        private void HandleConsoleAddRepo(string module, string[] cmd)
        {
            if ( cmd.Length == 3)
            {
                m_PluginManager.AddRepository(cmd);
            }
            return;
        }

        private void HandleConsoleGetRepo(string module, string[] cmd)
        {
            m_PluginManager.GetRepository();
            return;
        }

        private void HandleConsoleRemoveRepo(string module, string[] cmd)
        {
            if (cmd.Length == 3)
                m_PluginManager.RemoveRepository(cmd);
            return;
        }

        // Enable repo
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
            ArrayList list = m_PluginManager.ListRepositories();
            foreach (string entry in list)
                MainConsole.Instance.Output(entry);

            return;
        }

        // Show description information
        private void HandleConsoleShowAddinInfo(string module, string[] cmd)
        {
            if ( cmd.Length >= 3 )
            {
                m_PluginManager.AddinInfo(cmd);
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

        #region web handlers
        public byte[] HandleWebListPlugins(OSDMap request)
        {
            return Ux.FailureResult("Not Implemented");
        }

        public byte[] HandleWebPluginInfo(OSDMap request)
        {
            return Ux.FailureResult("Not Implemented");
        }
        #endregion
    }
}
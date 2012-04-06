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
using System.Reflection;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenMetaverse;
using Nini.Config;
using log4net;
using Mono.Addins;


namespace OpenSim.Services.IntegrationService
{
    public class IntegrationService : IntegrationServiceBase, IIntegrationService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public IntegrationService(IConfigSource config, IHttpServer server)
            : base(config, server)
        {
            m_log.InfoFormat("[INTEGRATION SERVICE]: Loaded");

            // Add commands to the console
            if (MainConsole.Instance != null)
            {
                AddConsoleCommands();
            }
        }

        private void AddConsoleCommands()
        {
            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "install", "install \"plugin name\"", "Install plugin from repository",
                                                     HandleInstallPlugin);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "uninstall", "uninstall \"plugin name\"", "Remove plugin from repository",
                                                     HandleUnInstallPlugin);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "check installed", "check installed \"plugin name=\"","Check installed plugin",
                                                     HandleCheckInstalledPlugin);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "list installed", "list installed \"plugin name=\"","List install plugins",
                                                     HandleListInstalledPlugin);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "list available", "list available \"plugin name=\"","List available plugins",
                                                     HandleListAvailablePlugin);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "list updates", "list updates","List availble updates",
                                                     HandleListUpdates);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "update", "update \"plugin name=\"","Update the plugin",
                                                     HandleUpdatePlugin);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "add repo", "add repo \"url\"","Add repository",
                                                     HandleAddRepo);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "get repo", "get repo \"url\"", "Sync with a registered repository",
                                                     HandleGetRepo);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "remove repo", "remove repo \"[url | index]\"","Remove registered repository",
                                                     HandleRemoveRepo);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "enable repo", "enable repo \"[url | index]\"","Enable registered repository",
                                                     HandleEnableRepo);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "disable repo", "disable repo \"[url | index]\"","Disable registered repository",
                                                     HandleDisableRepo);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "list repos", "list repos","List registered repositories",
                                                     HandleListRepos);

            MainConsole.Instance.Commands.AddCommand("Integration", true,
                                                     "show info", "show info \"plugin name\"","Show detailed information for plugin",
                                                     HandleShowAddinInfo);

        }

        #region console handlers
        private void HandleInstallPlugin(string module, string[] cmd)
        {
            MainConsole.Instance.Output(m_PluginManager.Install());
            return;
        }

        private void HandleUnInstallPlugin(string module, string[] cmd)
        {
            MainConsole.Instance.Output(m_PluginManager.UnInstall());
            return;
        }

        private void HandleCheckInstalledPlugin(string module, string[] cmd)
        {
            MainConsole.Instance.Output(m_PluginManager.CheckInstalled());
            return;
        }

        private void HandleListInstalledPlugin(string module, string[] cmd)
        {
            m_PluginManager.ListInstalledAddins();
            return;
        }

        private void HandleListAvailablePlugin(string module, string[] cmd)
        {
            m_PluginManager.ListAvailable();
            return;
        }

        private void HandleListUpdates(string module, string[] cmd)
        {
            MainConsole.Instance.Output(m_PluginManager.ListUpdates());
            return;
        }

        private void HandleUpdatePlugin(string module, string[] cmd)
        {
            MainConsole.Instance.Output(m_PluginManager.Update());
            return;
        }

        private void HandleAddRepo(string module, string[] cmd)
        {
            MainConsole.Instance.Output(m_PluginManager.AddRepository());
            return;
        }

        private void HandleGetRepo(string module, string[] cmd)
        {
            MainConsole.Instance.Output(m_PluginManager.GetRepository());
            return;
        }

        private void HandleRemoveRepo(string module, string[] cmd)
        {
            MainConsole.Instance.Output(m_PluginManager.RemoveRepository());
            return;
        }

        private void HandleEnableRepo(string module, string[] cmd)
        {
            m_PluginManager.EnableRepository(cmd);
            return;
        }

        private void HandleDisableRepo(string module, string[] cmd)
        {
            MainConsole.Instance.Output(m_PluginManager.DisableRepository());
            return;
        }

        private void HandleListRepos(string module, string[] cmd)
        {
            m_PluginManager.ListRepositories();
            return;
        }

        private void HandleShowAddinInfo(string module, string[] cmd)
        {
            if ( cmd.Length < 2 )
            {
                MainConsole.Instance.Output(m_PluginManager.AddinInfo());
                return;
            }
        }
        #endregion
    }
}
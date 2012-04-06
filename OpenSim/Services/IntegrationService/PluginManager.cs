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
using Mono.Addins;
using Mono.Addins.Setup;
using OpenSim.Framework;

namespace OpenSim.Services.IntegrationService
{
    // This will maintain the plugin repositories and plugins
    public class PluginManager
    {
        protected AddinRegistry m_Registry;
        protected SetupService m_Service;

        public PluginManager(string registry_path)
        {
            m_Registry = new AddinRegistry(registry_path);
            m_Service = new SetupService(m_Registry);
        }

        public string Install()
        {
            return "Install";
        }

        public string UnInstall()
        {
            return "UnInstall";
        }

        public string CheckInstalled()
        {
            return "CheckInstall";
        }

        public void ListInstalledAddins()
        {
            ArrayList list = new ArrayList();
            list.AddRange(m_Registry.GetAddins());
            MainConsole.Instance.Output("Installed Plugins");
            foreach (Addin addin in list)
            {
                MainConsole.Instance.Output(" - " + addin.Name + " " + addin.Version);
            }

            return;
        }

        public void ListAvailable()
        {
            MainConsole.Instance.Output("Available Plugins");
            AddinRepositoryEntry[] addins = m_Service.Repositories.GetAvailableAddins ();
            // foreach (PackageRepositoryEntry addin in addins)
            foreach (AddinRepositoryEntry addin in addins)
            {
                MainConsole.Instance.OutputFormat("{0} - {1} ",addin.Addin.Name, addin.RepositoryName );
            }
        }

        public string ListUpdates()
        {
            return "ListUpdates";
        }

        public string Update()
        {
            return "Update";
        }

        public string AddRepository()
        {
            return "AddRepository";
        }

        public string GetRepository()
        {
            return "GetRepository";
        }

        public string RemoveRepository()
        {
            return "RemoveRepository";
        }

        public string EnableRepository(string[] args)
        {
            return "Test";
        }

        public string DisableRepository()
        {
            return DisableRepository();
        }

        public void ListRepositories()
        {
            AddinRepository[] reps = m_Service.Repositories.GetRepositories();
            Array.Sort (reps, (r1,r2) => r1.Title.CompareTo(r2.Title));
            if (reps.Length == 0)
            {
                MainConsole.Instance.Output("No repositories have been registered.");
                return;
            }

            int n = 0;
            MainConsole.Instance.Output("Registered Repositories");

            foreach (AddinRepository rep in reps)
            {
                string num = n.ToString ();
                MainConsole.Instance.Output(num + ") ");
                if (!rep.Enabled)
                    MainConsole.Instance.Output("(Disabled) ");
                MainConsole.Instance.Output(rep.Title);
                if (rep.Title != rep.Url)
                    MainConsole.Instance.Output(new string (' ', num.Length + 2) + rep.Url);
                n++;
            }
        }

        public void UpdateRegistry()
        {
            m_Registry.Update();
        }

        public string AddinInfo()
        {
            return "AddinInfo";
        }
    }
}
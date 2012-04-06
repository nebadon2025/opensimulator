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
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Mono.Addins;
using Mono.Addins.Setup;
using Mono.Addins.Description;
using OpenSim.Framework;

namespace OpenSim.Services.IntegrationService
{
    // This will maintain the plugin repositories and plugins
    public class PluginManager : SetupService
    {
        protected AddinRegistry m_Registry;
//        protected SetupService m_Service;

        internal PluginManager( AddinRegistry r): base (r)
        {
            m_Registry = r;
        }

//        public PluginManager(string registry_path)
//        {
//            m_Registry = new AddinRegistry(registry_path);
//            m_Service = new SetupService(m_Registry);
//        }
//
        public string InstallPlugin(string[] args)
        {
            PackageCollection pack = new PackageCollection();
            PackageCollection toUninstall;
            DependencyCollection unresolved;

            IProgressStatus ps = new ConsoleProgressStatus(true);

            string name = Addin.GetIdName(args[1]);
            string version = Addin.GetIdVersion(args[1]);

            AddinRepositoryEntry[] aentry = Repositories.GetAvailableAddin(name, version);

            foreach (AddinRepositoryEntry ae in aentry)
            {
                Package p = Package.FromRepository(ae);
                pack.Add(p);
            }


            ResolveDependencies(ps, pack, out toUninstall, out unresolved);


            if(Install(ps, pack) == true)
                return "Install";
            else
                return "Bomb";
        }

        public void UnInstall(string[] args)
        {
            IProgressStatus ps = new ConsoleProgressStatus(true);
            Addin addin =  m_Registry.GetAddin(args[1]);
            Uninstall(ps, addin.Id);
            return;
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
                MainConsole.Instance.OutputFormat("* {0} rev. {1}", addin.Name, addin.Version);
            }

            return;
        }

        public ArrayList ListAvailable()
        {
            AddinRepositoryEntry[] addins = Repositories.GetAvailableAddins ();
            ArrayList list = new ArrayList();

            foreach (AddinRepositoryEntry addin in addins)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(String.Format("{0} rev. {1}, repo {2}", addin.Addin.Id, addin.Addin.Version, addin.RepositoryUrl));
                list.Add(sb.ToString());
            }
            return list;
        }

        public string ListUpdates()
        {
            return "ListUpdates";
        }

        public string Update()
        {
            return "Update";
        }

        public string AddRepository(string[] args)
        {
            Repositories.RegisterRepository(null, args[2].ToString(), true);
            return "AddRepository";
        }

        public string GetRepository()
        {
            return "GetRepository";
        }

        public string RemoveRepository(string[] args)
        {
            return "RemoveRepository";
        }

        public void EnableRepository(string[] args)
        {
            AddinRepository[] reps = Repositories.GetRepositories();
            Array.Sort (reps, (r1,r2) => r1.Title.CompareTo(r2.Title));
            if (reps.Length == 0)
            {
                MainConsole.Instance.Output("No repositories have been registered.");
                return;
            }

            int n = Convert.ToInt16(args[2]);
            if (n > (reps.Length -1))
            {
                MainConsole.Instance.Output("Selection out of range");
                return;
            }

            AddinRepository rep = reps[n];
            //return "TEST";
            Repositories.SetRepositoryEnabled(rep.Url, true);
            return;
            //return DisableRepository();
        }

        public void DisableRepository(string[] args)
        {
            AddinRepository[] reps = Repositories.GetRepositories();
            Array.Sort (reps, (r1,r2) => r1.Title.CompareTo(r2.Title));
            if (reps.Length == 0)
            {
                MainConsole.Instance.Output("No repositories have been registered.");
                return;
            }

            int n = Convert.ToInt16(args[2]);
            if (n > (reps.Length -1))
            {
                MainConsole.Instance.Output("Selection out of range");
                return;
            }

            AddinRepository rep = reps[n];
            //return "TEST";
            Repositories.SetRepositoryEnabled(rep.Url, false);
            return;
            //return DisableRepository();
        }

        public ArrayList ListRepositories()
        {
            AddinRepository[] reps = Repositories.GetRepositories();
            Array.Sort (reps, (r1,r2) => r1.Title.CompareTo(r2.Title));
            if (reps.Length == 0)
            {
                MainConsole.Instance.Output("No repositories have been registered.");
                return null;
            }

            ArrayList list = new ArrayList();

            int n = 0;
            foreach (AddinRepository rep in reps)
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendFormat("{0}) ", n.ToString());
                if (!rep.Enabled)
                    sb.AppendFormat("(Disabled) ");
                sb.AppendFormat("{0}", rep.Title);
                if (rep.Title != rep.Url)
                    sb.AppendFormat("{0}", rep.Url);

                list.Add(sb.ToString());
                n++;
            }

            return list;
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
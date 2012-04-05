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
using Mono.Addins;
using Mono.Addins.Setup;

namespace OpenSim.Services.IntegrationService
{
    // This will maintain the plugin repositories and plugins
    public class PluginManager
    {
        protected AddinRegistry m_Registry;
        protected SetupService m_Manager;

        public PluginManager(string registry_path)
        {
            m_Registry = new AddinRegistry(".", registry_path);
            m_Manager = new SetupService(m_Registry);


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

        public string ListInstalled()
        {
            return "ListInstalled";
        }

        public string ListAvailable()
        {
            return "ListAvailable";
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

        public string EnableRepository()
        {
            return "EnableRepository";
        }

        public string DisableRepository()
        {
            return DisableRepository();
        }

        public string ListRepositories()
        {
            return "ListRepositories";
        }

        public string UpdateRegistry()
        {
            return "UpdateRegistry";
        }

        public string AddinInfo()
        {
            return "AddinInfo";
        }
    }
}
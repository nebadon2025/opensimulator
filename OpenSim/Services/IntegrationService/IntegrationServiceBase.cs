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
using OpenSim.Services.Interfaces;
using OpenSim.Services.Base;
using OpenSim.Framework.Servers.HttpServer;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using Mono.Addins;


[assembly:AddinRoot ("IntegrationService", "1.0")]

namespace OpenSim.Services.IntegrationService
{
    [TypeExtensionPoint (Path="/OpenSim/IntegrationService", Name="IntegrationService")]
    public interface IntegrationPlugin
    {
        void Init(IConfigSource config);
    }


     public class IntegrationServiceBase : ServiceBase
    {
        protected IPresenceService m_PresenceService;
        protected IGridService m_GridService;
        protected IHttpServer m_Server;
        IConfig m_IntegrationServerConfig;

        public IntegrationServiceBase(IConfigSource config, IHttpServer server)
            : base(config)
        {
            Object[] args = new Object[] { config };


            m_Server = server;

            AddinManager.Initialize (".");
            AddinManager.Registry.Update ();
            foreach (IntegrationPlugin cmd in AddinManager.GetExtensionObjects("/OpenSim/IntegrationService"))
            {
                cmd.Init (config);
                server.AddStreamHandler((IRequestHandler)cmd);
            }

            m_IntegrationServerConfig = config.Configs["IntegrationService"];
            if (m_IntegrationServerConfig == null)
            {
                throw new Exception("[IntegrationService]: Missing configuration");
                return;
            }

            string gridService = m_IntegrationServerConfig.GetString("GridService", String.Empty);
            string presenceService = m_IntegrationServerConfig.GetString("PresenceService", String.Empty);


            if (gridService != string.Empty)
                m_GridService = LoadPlugin<IGridService>(gridService, args);
            if (presenceService != string.Empty)
                m_PresenceService = LoadPlugin<IPresenceService>(presenceService, args);

        }
    }
}

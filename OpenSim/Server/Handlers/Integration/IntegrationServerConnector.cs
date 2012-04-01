using System;
using Nini.Config;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;
using OpenSim.Framework;


namespace OpenSim.Server.Handlers.Integration
{
    public class IntegrationServiceConnector : ServiceConnector
    {

        private IIntegrationService m_IntegrationService;
        private string m_ConfigName = "IntegrationService";

        public IntegrationServiceConnector(IConfigSource config, IHttpServer server, string configName) :
                base(config, server, configName)
        {
            IConfig serverConfig = config.Configs[m_ConfigName];
            if (serverConfig == null)
                throw new Exception(String.Format("No section {0} in config file", m_ConfigName));

            string service = serverConfig.GetString("LocalServiceModule",
                    String.Empty);

            if (service == String.Empty)
                throw new Exception("No LocalServiceModule in config file");

            Object[] args = new Object[] { config, server };
            m_IntegrationService = ServerUtils.LoadPlugin<IIntegrationService>(service, args);

            server.AddStreamHandler(new IntegrationServerHandler(m_IntegrationService));

        }

    }
}

using System;
using Nini.Config;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;

namespace OpenSim.Server.Handlers.WxServerHandler
{
    public class WxServiceConnector : ServiceConnector
    {

        private IWxService m_WxService;
        // Looks for [WxService] in the config file
        private string m_ConfigName = "WxService";

        public WxServiceConnector(IConfigSource config, IHttpServer server, string configName)
            : base(config, server, configName)
        {
            // Load wxService here
            // Add stream handlers vvv Grid
            IConfig serverConfig = config.Configs[m_ConfigName];
            if (serverConfig == null)
                throw new Exception(String.Format("No section {0} in config file", m_ConfigName));

            string WxService = serverConfig.GetString("LocalServiceModule",
                    String.Empty);

            if (WxService == String.Empty)
                throw new Exception("No LocalServiceModule in config file");

            Object[] args = new Object[] { config };
            m_WxService = ServerUtils.LoadPlugin<IWxService>(WxService, args);

            server.AddStreamHandler(new WxPostHandler(m_WxService));
        }
    }
}


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
        private IHttpServer m_HttpServer;

        public WxServiceConnector(IConfigSource config, IHttpServer server, string configName)
            : base(config, server, configName)
        {
            m_HttpServer = server;
            // Load wxService here
            // Add stream handlers vvv Grid
            IConfig serverConfig = config.Configs[m_ConfigName];
            if (serverConfig == null)
                throw new Exception(String.Format("No section {0} in config file", m_ConfigName));

            string WxService = serverConfig.GetString("LocalServiceModule",
                    String.Empty);

            if (WxService == String.Empty)
                throw new Exception("No LocalServiceModule in config file");

            Object[] args = new Object[] { config, server };
            m_WxService = ServerUtils.LoadPlugin<IWxService>(WxService, args);

            // BLUEWALL: Getting started again,
            // the WxService can be loaded up then it can take over loading if it knows the server
            // I'm pretty sure the entry below was done before the split. So, it may not be relevent now
            //
            // We can add the handlers here from some loader mechanism to make it pluggable
            // so from our application we do something like...
            // m_Connector.addHandler(new Handler(ServiceNameForMe))...
            // Then we will add the handler object and pass ServiceName
            // ***This is our time to pass the needed handlers back up to init
            //    the handler
            // BLUEWALL: need to pickup the handlers here and push them to the httpd server
            //            via the WxService handler initializer
            //
            //            May be able to push the server up to the WxService so it will be
            //            able to register them as it loads them.
            //
            // vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv
            //server.AddStreamHandler(new WxPostHandler(m_WxService));
        }
    }
}


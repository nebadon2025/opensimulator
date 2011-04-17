using System;
using System.Reflection;
using System.Collections.Generic;

using Nini.Config;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Data;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse;


namespace OpenSim.Services.WxService
{
    // This will be the extension type for the handlers
    public interface IWxHandler
    {

        void Test();
        void Init(IConfigSource config);
        void AddWxHandler();
    }
    
    /// <summary>
    /// Wx service.
    /// </summary>
    public class WxService : WxServiceBase, IWxService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private IHttpServer m_HttpServer;
        // A list of our loaded handlers
        private static List<IWxHandler> m_WxHandlers =
                new List<IWxHandler>();

        private IConfigSource m_Config;
        // The methods here will talk to the database
        // This is loaded by our ServerHandler as the "LocalServiceModule = " in the section named by the
        // m_ConfigName value
        //
        // Need to look at connectors
        //
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenSim.Services.WxService.WxService"/> class.
        /// </summary>
        /// <param name='config'>
        /// Config.
        /// </param>
        public WxService(IConfigSource config, IHttpServer server)
            : base (config)
        {
            m_HttpServer = server;
            m_log.InfoFormat("[BWx]: BlueWall Wx Loading ... ");
            m_Config = config;
            IConfig WxConfig = config.Configs["WxService"];
            // IConfig WxHandlerConfig = config.Configs["WxHandlers"];
            string handlerList = WxConfig.GetString("WxHandlers", String.Empty);

            // Load our configuration items
            if (WxConfig != null)
            {
                // Register handlers...
                // server.AddStreamHandler(new WxPostHandler(m_WxService));

                // Initialization: the ServerConnector just wants to get this loaded and doesn't
                //                 read the rest. Then, we can load assemblies that are needed
                //                 here to do our own work. The handlers will supply the information
                //                 needed to load their assemblies.

            }

            // Now load the handler plugins
            if (handlerList != null)
            {
                m_log.InfoFormat("[WxService]: Wx Loading Handlers... ");

                string dllName = null;
                string asyName = null;

                // [WxService]::WxHandlers contains the handler plugin configurations
                // Handler.dll:HandlerAssembly@HandlerConfig
                // string handlerList = WxConfig.GetString("WxHandlers", String.Empty);
                string[] handlers = handlerList.Split(new char[] {',', ' '});

                foreach (string handler in handlers )
                {
                    // split into dllName and AssemblyName@ConfigName
                    string[] s1 = handler.Split( new char[] {':'});
                    if (s1.Length > 1)
                    {
                        dllName = s1[0];
                        asyName = s1[1];
                    }
                    // Need an error message here and exit if we have mal-formed config settings

                    IWxHandler handlerObj = null;

                    // things we will send our constructior
                    Object[] args = new Object[] { this };

                    handlerObj = ServerUtils.LoadPlugin<IWxHandler>(dllName, asyName, args);

                    // Process the handler startup
                    //
                    // Will add any startup methods needed to the interface
                    if ( handlerObj != null )
                    {

                        m_WxHandlers.Add(handlerObj);
                        handlerObj.Init(m_Config);
                        handlerObj.AddWxHandler();

                    }
                }
            }
        }

        public void AddWxHandler(BaseStreamHandler handler)
        {
            m_HttpServer.AddStreamHandler(handler);
        }
    }
}
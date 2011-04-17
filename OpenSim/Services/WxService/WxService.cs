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
    /// <summary>
    /// Interface for user-provided WxService handlers.
    /// </summary>
    public interface IWxHandler
    {
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
            m_log.InfoFormat("[Wx]: WxService Loading ... ");
            m_Config = config;
            IConfig WxConfig = config.Configs["WxService"];
            string handlerList = null;

            if (WxConfig != null)
            {
                handlerList = WxConfig.GetString("WxHandlers", String.Empty);
            }

            // load the handler plugins
            if (handlerList != null)
            {
                m_log.InfoFormat("[Wx]: WxService Loading Handlers... ");

                string dllName = null;
                string asyName = null;

                // [WxService]::WxHandlers contains the handler plugin configurations
                // Handler.dll:HandlerAssembly;
                string[] handlers = handlerList.Split(new char[] {',', ' '});

                foreach (string handler in handlers )
                {
                    // split into dllName and AssemblyName
                    string[] s1 = handler.Split( new char[] {':'});
                    if (s1.Length > 1)
                    {
                        dllName = s1[0];
                        asyName = s1[1];
                    }
                    else
                    {
                        m_log.FatalFormat("[Wx]: Could not load handler: {0}", handler);
                        continue;
                    }

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
                    else
                    {
                        m_log.FatalFormat("[Wx]: Could not load handler: {0}", dllName);
                        throw new Exception("[Wx]: Could not load handler");
                    }
                }
            }
            else
            {
                m_log.Fatal("[Wx]: No WxHandlers section in ini, cannot proceed!");
                throw new Exception("No WxHandlers section in ini, cannot proceed!");
            }
        }

        /// <summary>
        /// Adds the wx handler to our server.
        /// </summary>
        /// <param name='handler'>
        /// Handler.
        /// </param>
        public void AddWxHandler(BaseStreamHandler handler)
        {
            m_HttpServer.AddStreamHandler(handler);
        }
    }
}
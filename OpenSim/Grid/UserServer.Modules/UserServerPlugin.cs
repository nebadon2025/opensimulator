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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using log4net;
using log4net.Config;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Statistics;
using OpenSim.Grid.Communications.OGS1;
using OpenSim.Grid.Framework;
using OpenSim.Grid.GridServer;

namespace OpenSim.Grid.UserServer.Modules
{
    public class UserServerPlugin : IGridPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected UserConfig m_cfg;

        protected UserDataBaseService m_userDataBaseService;

        public UserManager m_userManager;

        protected UserServerAvatarAppearanceModule m_avatarAppearanceModule;
        protected UserServerFriendsModule m_friendsModule;

        public UserLoginService m_loginService;
        public MessageServersConnector m_messagesService;

        protected GridInfoServiceModule m_gridInfoService;

        protected UserServerCommandModule m_consoleCommandModule;
        protected UserServerEventDispatchModule m_eventDispatcher;

        protected BaseHttpServer m_httpServer;

        protected IGridServiceCore m_core;

        protected ConsoleBase m_console;


        public UserServerPlugin()
        {
        }

        #region IGridPlugin Members

        public void Initialise(GridServerBase gridServer)
        {
            Initialise(gridServer.HttpServer, gridServer, gridServer.UConfig);
        }

        #endregion

        public void Initialise(BaseHttpServer httpServer, IGridServiceCore core, UserConfig config)
        {
            m_httpServer = httpServer;
            m_core = core;
            m_cfg = config;
            m_console = MainConsole.Instance;

            IInterServiceInventoryServices inventoryService = StartupCoreComponents();

            //setup services/modules
            StartupUserServerModules();

            StartOtherComponents(inventoryService);

            //PostInitialise the modules
            PostInitialiseModules();

            RegisterHttpHandlers();
        }

        protected virtual IInterServiceInventoryServices StartupCoreComponents()
        {
            
           m_core.RegisterInterface<ConsoleBase>(m_console);
           m_core.RegisterInterface<UserConfig>(m_cfg);

            //Should be in modules?
            IInterServiceInventoryServices inventoryService = new OGS1InterServiceInventoryService(m_cfg.InventoryUrl);
            // IRegionProfileRouter regionProfileService = new RegionProfileServiceProxy();

           m_core.RegisterInterface<IInterServiceInventoryServices>(inventoryService);
            // RegisterInterface<IRegionProfileRouter>(regionProfileService);

            return inventoryService;
        }

        /// <summary>
        /// Start up the user manager
        /// </summary>
        /// <param name="inventoryService"></param>
        protected virtual void StartupUserServerModules()
        {
            m_log.Info("[STARTUP]: Establishing data connection");
            //setup database access service, for now this has to be created before the other modules.
            m_userDataBaseService = new UserDataBaseService();
            m_userDataBaseService.Initialise(m_core);

            //TODO: change these modules so they fetch the databaseService class in the PostInitialise method
            m_userManager = new UserManager(m_userDataBaseService);
            m_userManager.Initialise(m_core);

            m_avatarAppearanceModule = new UserServerAvatarAppearanceModule(m_userDataBaseService);
            m_avatarAppearanceModule.Initialise(m_core);

            m_friendsModule = new UserServerFriendsModule(m_userDataBaseService);
            m_friendsModule.Initialise(m_core);

            m_consoleCommandModule = new UserServerCommandModule();
            m_consoleCommandModule.Initialise(m_core);

            m_messagesService = new MessageServersConnector();
            m_messagesService.Initialise(m_core);

            m_gridInfoService = new GridInfoServiceModule();
            m_gridInfoService.Initialise(m_core);
        }

        protected virtual void StartOtherComponents(IInterServiceInventoryServices inventoryService)
        {
            m_loginService = new UserLoginService(
              m_userDataBaseService, inventoryService, new LibraryRootFolder(m_cfg.LibraryXmlfile), m_cfg, m_cfg.DefaultStartupMsg, new RegionProfileServiceProxy());

            //
            // Get the minimum defaultLevel to access to the grid
            //
            m_loginService.setloginlevel((int)m_cfg.DefaultUserLevel);

           m_core.RegisterInterface<UserLoginService>(m_loginService); //TODO: should be done in the login service

            m_eventDispatcher = new UserServerEventDispatchModule(m_userManager, m_messagesService, m_loginService);
            m_eventDispatcher.Initialise(m_core);
        }

        protected virtual void PostInitialiseModules()
        {
            m_consoleCommandModule.PostInitialise(); //it will register its Console command handlers in here
            m_userDataBaseService.PostInitialise();
            m_messagesService.PostInitialise();
            m_eventDispatcher.PostInitialise(); //it will register event handlers in here
            m_gridInfoService.PostInitialise();
            m_userManager.PostInitialise();
            m_avatarAppearanceModule.PostInitialise();
            m_friendsModule.PostInitialise();
        }

        protected virtual void RegisterHttpHandlers()
        {
            m_loginService.RegisterHandlers(m_httpServer, m_cfg.EnableLLSDLogin, true);

            m_userManager.RegisterHandlers(m_httpServer);
            m_friendsModule.RegisterHandlers(m_httpServer);
            m_avatarAppearanceModule.RegisterHandlers(m_httpServer);
            m_messagesService.RegisterHandlers(m_httpServer);
            m_gridInfoService.RegisterHandlers(m_httpServer);
        }

        #region IPlugin Members

        public string Version
        {
            get { return "0.0"; }
        }

        public string Name
        {
            get { return "UserServerPlugin"; }
        }

        public void Initialise()
        {
            
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}

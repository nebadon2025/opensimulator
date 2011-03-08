using System;
using System.Reflection;


using Nini.Config;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Data;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Services.WxService
{
    public class WxService : WxServiceBase, IWxService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        protected IConfigSource m_config;
        protected IUserAccountService m_userAccounts = null;
        // The methods here will talk to the database
        // This is loaded by our ServerHandler as the "LocalServiceModule = " in the section named by the
        // m_ConfigName value
        //
        // Need to look at connectors
        //
        public WxService(IConfigSource config)
            : base (config)
        {
            m_log.InfoFormat("[BWx]: BlueWall Wx Loading ... ");
            m_config = config;
            IConfig WxConfig = config.Configs["WxService"];
            if (WxConfig != null)
            {
                string userService = WxConfig.GetString("UserAccountService", String.Empty);

                if (userService != String.Empty)
                {
                    Object[] args = new Object[] { config };
                    m_userAccounts = ServerUtils.LoadPlugin<IUserAccountService>(userService, args);
                }
            }


        }

        public UserAccount GetUserData(UUID userID) {

            UserAccount userInfo = null;

            userInfo = m_userAccounts.GetUserAccount(UUID.Zero, userID);

            return userInfo;
        }
    }
}
using System;
using log4net;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using OpenSim.Server.Base;
using OpenMetaverse;

namespace OpenSim.Services.Connectors
{
    // BLUEWALL: Looks like this is a bridge for region modules to connect back
    public class WxServiceConnector :  IWxService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;

        public WxServiceConnector()
        {
        }

        public UserAccount GetUserData(UUID userID) {
            return null;
        }
    }
}


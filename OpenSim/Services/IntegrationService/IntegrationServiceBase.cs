using System;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Base;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Services.IntegrationService
{
    public class IntegrationServiceBase : ServiceBase
    {
        protected IPresenceService m_PresenceService;
        protected IGridService m_GridService;
        IConfig m_IntegrationServerConfig;

        public IntegrationServiceBase(IConfigSource config)
            : base(config)
        {
            Object[] args = new Object[] { config };

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


using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenMetaverse;
using Nini.Config;



namespace OpenSim.Services.IntegrationService
{
    public class IntegrationService : IntegrationServiceBase, IIntegrationService
    {

        public IntegrationService(IConfigSource config, IHttpServer server)
            : base(config, server)
        {

        }

        #region IIntegrationService implementation
        public PresenceInfo VerifyAgent(UUID SecureSessionID)
        {
            return m_PresenceService.VerifyAgent(SecureSessionID);
        }
        #endregion
    }
}
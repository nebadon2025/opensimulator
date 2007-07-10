using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Framework.Configuration;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Services;
using OpenSim.Grid.Services.CoreFunctions.Local;
using OpenSim.Grid.Services.CoreFunctions.Remote;

namespace OpenSim.Grid.Services.Grid
{
    public class GridServices : ConfigurationMember
    {
        private IUserServicesCoreFunctions userCoreFunctions = new UserServicesCoreFunctionsRemote();
        private IGridServicesCoreFunctions gridCoreFunctions = new GridServicesCoreFunctionsLocal();
        private IAssetServicesCoreFunctions assetCoreFunctions = new AssetServicesCoreFunctionsRemote();

        private BaseHttpServer httpServer;

        public GridServices()
        {
            httpServer = new BaseHttpServer(8001);
            this.setConfigurationDescription("GRID SERVICES");
            this.setConfigurationFilename("grid_services.xml");
        }
        public override void handleConfigurationItem(string configuration_key, string configuration_value)
        {
        }
    }
}

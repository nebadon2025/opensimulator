using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Framework.Configuration;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Services;
using OpenSim.Grid.Services.CoreFunctions.Local;
using OpenSim.Grid.Services.CoreFunctions.Remote;


namespace OpenSim.Grid.Services.Asset
{
    public class AssetServices : ConfigurationMember
    {
        private IUserServicesCoreFunctions userCoreFunctions = new UserServicesCoreFunctionsRemote();
        private IGridServicesCoreFunctions gridCoreFunctions = new GridServicesCoreFunctionsRemote();
        private IAssetServicesCoreFunctions assetCoreFunctions = new AssetServicesCoreFunctionsLocal();

        private BaseHttpServer httpServer;

        public AssetServices()
        {
            httpServer = new BaseHttpServer(8003);
            this.setConfigurationDescription("ASSET SERVICES");
            this.setConfigurationFilename("asset_services.xml");
        }

        public override void handleConfigurationItem(string configuration_key, string configuration_value)
        {
        }
    }
}

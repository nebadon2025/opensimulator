using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Framework.Configuration;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Services;
using OpenSim.Grid.Services.CoreFunctions.Local;
using OpenSim.Grid.Services.CoreFunctions.Remote;

namespace OpenSim.Grid.Services.User
{
    public class UserServices : ConfigurationMember
    {
        private IUserServicesCoreFunctions userCoreFunctions = new UserServicesCoreFunctionsLocal();
        private IGridServicesCoreFunctions gridCoreFunctions = new GridServicesCoreFunctionsRemote();
        private IAssetServicesCoreFunctions assetCoreFunctions = new AssetServicesCoreFunctionsRemote();

        private BaseHttpServer httpServer;

        public UserServices()
        {
            httpServer = new BaseHttpServer(8002);
            this.setConfigurationDescription("USER SERVICES");
            this.setConfigurationFilename("user_services.xml");
        }

        public override void handleConfigurationItem(string configuration_key, string configuration_value)
        {
        }
    }
}

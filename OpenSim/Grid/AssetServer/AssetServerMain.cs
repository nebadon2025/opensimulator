using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Framework.Configuration;
using OpenSim.Grid.Services.Asset;
namespace OpenSim.Grid.AssetServer
{
    public class AssetServerMain
    {
        [STAThread]
        public static void Main(string[] args)
        {
            ConfigurationManager configManager = new ConfigurationManager();
            AssetServices assetServices = new AssetServices();

            configManager.addConfigurationMember((ConfigurationMember)assetServices);
            configManager.gatherConfiguration();
        }
    }
}

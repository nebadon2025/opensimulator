using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Framework.Configuration;
using OpenSim.Grid.Services.Grid;

namespace OpenSim.Grid.GridServer
{
    public class GridServerMain
    {
        [STAThread]
        public static void Main(string[] args)
        {
            ConfigurationManager configManager = new ConfigurationManager();
            GridServices gridServices = new GridServices();

            configManager.addConfigurationMember((ConfigurationMember)gridServices);
            configManager.gatherConfiguration();
        }
    }
}

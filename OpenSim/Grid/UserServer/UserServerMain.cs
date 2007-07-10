using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Framework.Configuration;
using OpenSim.Grid.Services.User;

namespace OpenSim.Grid.UserServer
{
    public class UserServerMain
    {
        [STAThread]
        public static void Main(string[] args)
        {
            ConfigurationManager configManager = new ConfigurationManager();
            UserServices userServices = new UserServices();

            configManager.addConfigurationMember((ConfigurationMember)userServices);
            configManager.gatherConfiguration();
        }
    }
}

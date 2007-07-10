using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Console;
namespace OpenSim.Framework.Configuration
{

    public class ConfigurationManager
    {
        List<ConfigurationMember> configurationMembers = new List<ConfigurationMember>();

        public ConfigurationManager()
        {
        }

        public void addConfigurationMember(ConfigurationMember configMember)
        {
            if (!configurationMembers.Contains(configMember))
            {
                configurationMembers.Add(configMember);
            }
        }

        public void gatherConfiguration()
        {
            foreach (ConfigurationMember configMember in configurationMembers)
            {
                if (configMember.configurationFilename.Trim() != "")
                {
                    XmlConfiguration xmlConfig = new XmlConfiguration(configMember.configurationFilename);
                    xmlConfig.LoadData();
                    string attribute = "";
                    foreach (ConfigurationOption configOption in configMember.configurationOptions)
                    {
                        attribute = xmlConfig.GetAttribute(configOption.configurationKey);
                        if (attribute == "")
                        {
                            if (configMember.configurationDescription.Trim() != "")
                            {
                                attribute = MainLog.Instance.CmdPrompt(configMember.configurationDescription + ": " + configOption.configurationQuestion, configOption.configurationDefault);
                            }
                            else
                            {
                                attribute = MainLog.Instance.CmdPrompt(configMember.configurationDescription + ": " + configOption.configurationQuestion, configOption.configurationDefault);
                            }
                            xmlConfig.SetAttribute(configOption.configurationKey, attribute);                            
                        }
                        configMember.handleConfigurationItem(configOption.configurationKey, attribute);
                    }
                    xmlConfig.Commit();
                    xmlConfig.Close();
                }
                else
                {
                    //Error out, no filename specified
                }
            }
        }

        public void clearConfigurationMembers()
        {
            configurationMembers.Clear();
        }
    }
}

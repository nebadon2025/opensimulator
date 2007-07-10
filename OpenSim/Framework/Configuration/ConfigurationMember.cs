using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Configuration
{
    public abstract class ConfigurationMember
    {
        public List<ConfigurationOption> configurationOptions = new List<ConfigurationOption>();
        public string configurationFilename = "";
        public string configurationDescription = "";
        public abstract void handleConfigurationItem(string configuration_key, string configuration_value);


        public void setConfigurationFilename(string filename)
        {
            configurationFilename = filename;
        }
        public void setConfigurationDescription(string desc)
        {
            configurationDescription = desc;
        }
        public void addConfigurationOption(string configuration_key, string configuration_question, string configuration_default)
        {
            ConfigurationOption configOption = new ConfigurationOption();
            configOption.configurationKey = configuration_key;
            configOption.configurationQuestion = configuration_question;
            configOption.configurationDefault = configuration_default;

            if (configuration_key != "" && configuration_question != "")
            {
                if (!configurationOptions.Contains(configOption))
                {
                    configurationOptions.Add(configOption);
                }
            }
            else
            {
                //Can't add it, error out.
            }
        }
    }
}

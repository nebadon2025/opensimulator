/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml;
using log4net;
using Nini.Config;
using OpenSim.Framework;

namespace OpenSim.Grid.GridServer
{
    public class GridConfigurationLoader
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected OpenSimConfigSource m_config;

        public GridConfigurationLoader()
        {
        }

        public OpenSimConfigSource LoadConfigSettings(IConfigSource configSource)
        {
            bool iniFileExists = false;

            IConfig startupConfig = configSource.Configs["Startup"];

            string masterFileName = startupConfig.GetString("inimaster", "");
            string masterfilePath = Path.Combine(Util.configDir(), masterFileName);

            string iniDirName = startupConfig.GetString("inidirectory", "GridConfig");
            string iniDirPath = Path.Combine(Util.configDir(), iniDirName);

            string iniFileName = startupConfig.GetString("inifile", "OpenSim.Grid.ini");
            string iniFilePath = Path.Combine(Util.configDir(), iniFileName);

            string xmlPath = Path.Combine(Util.configDir(), "OpenSim.Grid.xml");

            m_config = new OpenSimConfigSource();
            m_config.Source = new IniConfigSource();
            m_config.Source.Merge(DefaultConfig());

            m_log.Info("[CONFIG] Reading configuration settings");

            Uri configUri;

            //check for master .INI file (name passed in command line, no default), or XML over http
            if (masterFileName.Length > 0) // If a master file name is given ...
            {
                m_log.InfoFormat("[CONFIG] Reading config master file {0}", masterfilePath);

                bool isMasterUri = Uri.TryCreate(masterFileName, UriKind.Absolute, out configUri) && configUri.Scheme == Uri.UriSchemeHttp;

                if (!ReadConfig(masterFileName, masterfilePath, m_config, isMasterUri))
                {
                    m_log.FatalFormat("[CONFIG] Could not open master config file {0}", masterfilePath);
                }
            }

            if (Directory.Exists(iniDirName))
            {
                m_log.InfoFormat("Searching folder: {0} , for config ini files", iniDirName);
                string[] fileEntries = Directory.GetFiles(iniDirName);
                foreach (string filePath in fileEntries)
                {
                    // m_log.InfoFormat("reading ini file < {0} > from config dir", filePath);
                    ReadConfig(Path.GetFileName(filePath), filePath, m_config, false);
                }
            }

            // Check for .INI file (either default or name passed on command
            // line) or XML config source over http
            bool isIniUri = Uri.TryCreate(iniFileName, UriKind.Absolute, out configUri) && configUri.Scheme == Uri.UriSchemeHttp;
            iniFileExists = ReadConfig(iniFileName, iniFilePath, m_config, isIniUri);

            if (!iniFileExists)
            {
                // check for a xml config file                                
                if (File.Exists(xmlPath))
                {
                    iniFilePath = xmlPath;

                    m_log.InfoFormat("Reading XML configuration from {0}", Path.GetFullPath(xmlPath));
                    iniFileExists = true;

                    m_config.Source = new XmlConfigSource();
                    m_config.Source.Merge(new XmlConfigSource(iniFilePath));
                }
            }

            m_config.Source.Merge(configSource);

            if (!iniFileExists)
            {
                m_log.FatalFormat("[CONFIG] Could not load any configuration");
                if (!isIniUri)
                    m_log.FatalFormat("[CONFIG] Tried to load {0}, ", Path.GetFullPath(iniFilePath));
                else
                    m_log.FatalFormat("[CONFIG] Tried to load from URI {0}, ", iniFileName);
                m_log.FatalFormat("[CONFIG] and XML source {0}", Path.GetFullPath(xmlPath));

                m_log.FatalFormat("[CONFIG] Did you copy the OpenSim.Grid.ini.example file to OpenSim.Grid.ini?");
                Environment.Exit(1);
            }

            ReadConfigSettings();

            return m_config;
        }

        /// <summary>
        /// Provide same ini loader functionality for standard ini and master ini - file system or XML over http
        /// </summary>
        /// <param name="iniName">The name of the ini to load</param>
        /// <param name="iniPath">Full path to the ini</param>
        /// <param name="m_config">The current configuration source</param>
        /// <param name="isUri">Boolean representing whether the ini source is a URI path over http or a file on the system</param>
        /// <returns></returns>
        private bool ReadConfig(string iniName, string iniPath, OpenSimConfigSource m_config, bool isUri)
        {
            bool success = false;

            if (!isUri && File.Exists(iniPath))
            {
                m_log.InfoFormat("[CONFIG] Reading configuration file {0}", Path.GetFullPath(iniPath));

                // From reading Nini's code, it seems that later merged keys replace earlier ones.                
                m_config.Source.Merge(new IniConfigSource(iniPath));
                success = true;
            }
            else
            {
                if (isUri)
                {
                    m_log.InfoFormat("[CONFIG] {0} is a http:// URI, fetching ...", iniName);

                    // The ini file path is a http URI
                    // Try to read it
                    try
                    {
                        XmlReader r = XmlReader.Create(iniName);
                        XmlConfigSource cs = new XmlConfigSource(r);
                        m_config.Source.Merge(cs);

                        success = true;
                        m_log.InfoFormat("[CONFIG] Loaded config from {0}", iniName);
                    }
                    catch (Exception e)
                    {
                        m_log.FatalFormat("[CONFIG] Exception reading config from URI {0}\n" + e.ToString(), iniName);
                        Environment.Exit(1);
                    }
                }
            }
            return success;
        }

        /// <summary>
        /// Setup a default config values in case they aren't present in the ini file
        /// </summary>
        /// <returns></returns>
        public static IConfigSource DefaultConfig()
        {
            IConfigSource defaultConfig = new IniConfigSource();

            {
                IConfig config = defaultConfig.Configs["Startup"];

                if (null == config)
                    config = defaultConfig.AddConfig("Startup");

                config.Set("LoadPlugins", "GridServerPlugin,UserServerPlugin");
                config.Set("HttpPort", "8051");
            }

            {
                IConfig config = defaultConfig.Configs["GridServices"];

                if (null == config)
                    config = defaultConfig.AddConfig("GridServices");

                config.Set("enable", false);
               
            }

            return defaultConfig;
        }

        protected virtual void ReadConfigSettings()
        {
            IConfig startupConfig = m_config.Source.Configs["Startup"];
            if (startupConfig != null)
            {
               
            }

            IConfig standaloneConfig = m_config.Source.Configs["StandAlone"];
            if (standaloneConfig != null)
            {
               
            }
        }
    }

    public class OpenSimConfigSource
    {
        public IConfigSource Source;

        public void Save(string path)
        {
            if (Source is IniConfigSource)
            {
                IniConfigSource iniCon = (IniConfigSource)Source;
                iniCon.Save(path);
            }
            else if (Source is XmlConfigSource)
            {
                XmlConfigSource xmlCon = (XmlConfigSource)Source;
                xmlCon.Save(path);
            }
        }
    }
}

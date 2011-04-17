using System;
using OpenSim.Framework;
using OpenSim.Data;
using Nini.Config;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Base;

namespace OpenSim.Services.WxService
{
    public class WxServiceBase : ServiceBase
    {
        protected IRegionData m_Database = null;

        public WxServiceBase(IConfigSource config)
            : base(config)
        {
            string dllName = String.Empty;
            string connString = String.Empty;
            string realm = "regions";
            //
            // Try reading the [DatabaseService] section, if it exists
            //
            IConfig dbConfig = config.Configs["DatabaseService"];
            if (dbConfig != null)
            {
                if (dllName == String.Empty)
                    dllName = dbConfig.GetString("StorageProvider", String.Empty);
                if (connString == String.Empty)
                    connString = dbConfig.GetString("ConnectionString", String.Empty);
            }
            //
            // [WxService] section overrides [DatabaseService], if it exists
            //
            IConfig WxConfig = config.Configs["WxService"];
            if (WxConfig != null)
            {
                dllName = WxConfig.GetString("StorageProvider", dllName);
                connString = WxConfig.GetString("ConnectionString", connString);
                realm = WxConfig.GetString("Realm", realm);
            }
            //
            // We tried, but this doesn't exist. We can't proceed.
            //
            if (dllName.Equals(String.Empty))
                throw new Exception("No StorageProvider configured");

            m_Database = LoadPlugin<IRegionData>(dllName, new Object[] { connString, realm });
            if (m_Database == null)
                throw new Exception("Could not find a storage interface in the given module");

        }
    }
}


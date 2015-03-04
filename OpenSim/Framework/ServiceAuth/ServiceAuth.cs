using System;
using System.Collections.Generic;

using Nini.Config;

namespace OpenSim.Framework.ServiceAuth
{
    public class ServiceAuth
    {
        public static IServiceAuth Create(IConfigSource config, string section)
        {
            CompoundAuthentication compoundAuth = new CompoundAuthentication();

            bool allowLlHttpRequestIn
                = Util.GetConfigVarFromSections<bool>(config, "AllowllHTTPRequestIn", new string[] { "Network", section }, false);

            if (!allowLlHttpRequestIn)
                compoundAuth.AddAuthenticator(new DisallowLlHttpRequest());

            string authType = Util.GetConfigVarFromSections<string>(config, "AuthType", new string[] { "Network", section }, "None");

            switch (authType)
            {
                case "BasicHttpAuthentication":
                    compoundAuth.AddAuthenticator(new BasicHttpAuthentication(config, section));
                    break;
            }

            if (compoundAuth.Count > 0)
                return compoundAuth;
            else
                return null;
        }
    }
}
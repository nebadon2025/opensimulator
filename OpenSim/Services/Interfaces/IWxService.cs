using System;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;

using OpenMetaverse;


namespace OpenSim.Services.Interfaces
{
    // Define all our methods here
    // Which need to be things like initializers, etc. for the client handlers
    public interface IWxService
    {
        void AddWxHandler(BaseStreamHandler handler);
        // UserAccount GetUserData(UUID userID);
    }
}
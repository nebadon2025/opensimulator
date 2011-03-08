using System;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenMetaverse;


namespace OpenSim.Services.Interfaces
{
    // Define all our methods here
    public interface IWxService
    {
        UserAccount GetUserData(UUID userID);
    }
}
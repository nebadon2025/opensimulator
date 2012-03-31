using System;
using System.Collections;
using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;

namespace OpenSim.Services.Interfaces
{
    public interface IIntegrationService
    {
        PresenceInfo VerifyAgent(UUID SecretSessionID);
    }
}


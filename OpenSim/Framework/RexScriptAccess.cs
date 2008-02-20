// Rex, new file
using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Framework
{
    // Interface for script engine.
    public interface RexScriptAccessInterface
    {
        bool GetAvatarStartLocation(out LLVector3 vLoc, out LLVector3 vLookAt);
    }


    // Static class used for getting values from the script to server .net code.
    // At the moment supports only one engine -> static.
    public class RexScriptAccess
    {
        public static RexScriptAccessInterface MyScriptAccess = null;

        public static bool GetAvatarStartLocation(out LLVector3 vLoc, out LLVector3 vLookAt)
        {
            vLoc = new LLVector3(0,0,0);
            vLookAt = new LLVector3(0, 0, 0);

            if (MyScriptAccess != null)
                return MyScriptAccess.GetAvatarStartLocation(out vLoc, out vLookAt);
            else
                return false;
        }
    }


}

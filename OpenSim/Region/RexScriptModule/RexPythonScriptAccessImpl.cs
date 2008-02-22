using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework;
using System.Globalization;
using OpenSim.Region.ScriptEngine.Common;
using libsecondlife;

namespace OpenSim.Region.RexScriptModule
{
    public class RexPythonScriptAccessImpl : RexScriptAccessInterface
    {
        private RexScriptEngine myScriptEngine;

        public RexPythonScriptAccessImpl(RexScriptEngine vEngine)
        {
            myScriptEngine = vEngine;
        }

        public bool GetAvatarStartLocation(out LLVector3 vLoc, out LLVector3 vLookAt)
        {
            vLoc = new LLVector3(0, 0, 0);
            vLookAt = new LLVector3(0, 0, 0);
            
            try
            {
                string str = "GetAvatarStartLocation()";
                object resultobj = myScriptEngine.EvalutePythonCommand(str);

                if (resultobj != null)
                {
                    List<LSL_Types.Vector3> templist = resultobj as List<LSL_Types.Vector3>;
                    if (templist != null)
                    {
                        vLoc.X = (float)templist[0].x;
                        vLoc.Y = (float)templist[0].y;
                        vLoc.Z = (float)templist[0].z;
                        vLookAt.X = (float)templist[1].x;
                        vLookAt.Y = (float)templist[1].y;
                        vLookAt.Z = (float)templist[1].z;
                    }
                    return true;
                }                    
            }
            catch (Exception e)
            {
                Console.WriteLine("GetAvatarStartLocation exception " + e.ToString());
            }
            return false;
        }


        private bool GetFloatFromString(string vString,out float floatResult)
        {
            floatResult = 0;
            return (float.TryParse(vString, NumberStyles.AllowDecimalPoint, Culture.NumberFormatInfo, out floatResult));
        }
    }
}

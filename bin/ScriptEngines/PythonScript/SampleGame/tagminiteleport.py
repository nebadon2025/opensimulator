import rxactor

import sys
import clr
asm = clr.LoadAssemblyByName('OpenSim.Region.ScriptEngine.Common')
Vector3 = asm.OpenSim.Region.ScriptEngine.Common.LSL_Types.Vector3

class TagMiniTeleport(rxactor.Actor):
    def GetScriptClassName():
        return "tagminiteleport.TagMiniTeleport"
    
    def EventTouch(self,vAvatar):
        try:
            Loc = vAvatar.llGetPos()
            Offset = self.llVecNorm(self.llGetPos() - Loc)*8
            vAvatar.DoLocalTeleport(Loc+Offset)
        except:
            pass
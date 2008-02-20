import rxactor
import rxavatar
import sys
import clr

asm = clr.LoadAssemblyByName('OpenSim.Region.ScriptEngine.Common')
Vector3 = asm.OpenSim.Region.ScriptEngine.Common.LSL_Types.Vector3
List = asm.OpenSim.Region.ScriptEngine.Common.LSL_Types.list

import random
import math


class DialogActor(rxactor.Actor):
    def GetScriptClassName():
        return "sampledialog.DialogActor"

    def EventCreated(self):
        super(self.__class__,self).EventCreated()
        print "DialogActor EventCreated"

    def EventTouch(self, vAvatar):
        vAgentId = vAvatar.AgentId
        toucher = self.MyWorld.AllAvatars[vAgentId]
        str = self.llGetObjectName() +  " was touched in region "+self.llGetRegionName() + " by " + toucher.GetFullName()
        self.llShout(0, str)
        self.llSetText("On top of text", Vector3(1,0,0), 1)
        self.llDialog(vAgentId, "hep", List("a", "b"), 0)
        #self.llSetRot(r)


        
        


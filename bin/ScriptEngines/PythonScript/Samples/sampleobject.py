import rxactor
import rxavatar
import sys
import clr

asm = clr.LoadAssemblyByName('OpenSim.Region.ScriptEngine.Common')
Vector3 = asm.OpenSim.Region.ScriptEngine.Common.LSL_Types.Vector3

import random
import math


class TestActor(rxactor.Actor):
    def GetScriptClassName():
        return "sampleobject.TestActor"

    def EventCreated(self):
        super(self.__class__,self).EventCreated()
        print "TestActor EventCreated"

    def EventTouch(self,vAvatar):
        scalex = 0.5 + random.random()*2
        scaley = 0.5 + random.random()*2
        scalez = 0.5 + random.random()*2
        
        tempscale = Vector3(scalex,scaley,scalez)
        self.llSetScale(tempscale)

        str = self.llGetObjectName() +  " was touched in region "+self.llGetRegionName() + " by " + vAvatar.GetFullName()
        self.llShout(0,str)
        self.llSetText("On top of text",Vector3(1,0,0),1)
        
        rotx = 0.5 + random.random()*3.14
        roty = 0.5 + random.random()*3.14
        rotz = 0.5 + random.random()*3.14
        r = self.llEuler2Rot(Vector3(rotx,roty,rotz))
        self.llSetRot(r)


        
        


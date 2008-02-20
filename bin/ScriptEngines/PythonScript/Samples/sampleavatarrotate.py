# sampleavatarrotate.py
#print "sampleavatarrotate.................................."

import rxactor
import sys
import clr

asm = clr.LoadAssemblyByName('OpenSim.Region.ScriptEngine.Common')
Vector3 = asm.OpenSim.Region.ScriptEngine.Common.LSL_Types.Vector3

import random
import math


# Rotate avatar relatively 90 degrees right
class RotRight(rxactor.Actor):
    def GetScriptClassName():
        return "sampleavatarrotate.RotRight"

    def EventCreated(self):
        super(self.__class__,self).EventCreated()
        print "ClientScripting.RotRight created"

    def EventTouch(self,vAvatar):
        rotresult = self.llEuler2Rot(Vector3(0,0,math.pi*-0.5))
        vAvatar.SetRelativeRot(rotresult)
        self.llShout(0,"90 degrees right")


# Rotate north, south, east,west
class RotMainDirs(rxactor.Actor):
    def GetScriptClassName():
        return "sampleavatarrotate.RotMainDirs"

    def EventCreated(self):
        super(self.__class__,self).EventCreated()
        print "ClientScripting.RotMainDirs created"
        self.Status = 0

    def EventTouch(self,vAvatar):
        if(self.Status == 0): # North
            rotresult = self.llEuler2Rot(Vector3(0,0,math.pi*0.5))
            self.llShout(0,"North")
        elif(self.Status == 1): # South
            rotresult = self.llEuler2Rot(Vector3(0,0,math.pi*1.5))
            self.llShout(0,"South")
        elif(self.Status == 2): # East
            rotresult = self.llEuler2Rot(Vector3(0,0,0))
            self.llShout(0,"East")
        elif(self.Status == 3): # West
            rotresult = self.llEuler2Rot(Vector3(0,0,math.pi))
            self.llShout(0,"West")

        self.Status += 1
        if(self.Status > 3):
            self.Status = 0

        vAvatar.llSetRot(rotresult)
        
        
# Rotate avatar towards this object
class RotTowardsMe(rxactor.Actor):
    def GetScriptClassName():
        return "sampleavatarrotate.RotTowardsMe"

    def EventCreated(self):
        super(self.__class__,self).EventCreated()
        print "ClientScripting.RotTowardsMe created"

    def EventTouch(self,vAvatar):
        avatar_forward = self.llRot2Fwd(vAvatar.llGetRot())
        avatar_toward = self.llVecNorm(self.llGetPos() - vAvatar.llGetPos())
        rotresult = self.llRotBetween(avatar_forward,avatar_toward)
        vAvatar.llSetRot(rotresult)
        self.llShout(0,"Towards me!")
        
        
        
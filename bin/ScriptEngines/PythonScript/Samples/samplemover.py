import rxactor
# TestMover not working at the moment. - Tuco

import sys
import clr

asm = clr.LoadAssemblyByName('OpenSim.Region.ScriptEngine.Common')
Vector3 = asm.OpenSim.Region.ScriptEngine.Common.LSL_Types.Vector3


class TestMover(rxactor.Actor):

    def GetScriptClassName():
        return "samplemover.TestMover"
    
    def EventCreated(self):
        super(self.__class__,self).EventCreated()
        self.bActive = False
    
    def EventTouch(self,vAvatar):
        if(not self.bActive):
            self.Physics = True
            self.PhysicsMode = 1
            self.Gravity = False
            self.Velocity = Vector3(0,0,0)
            self.Dir = 0
            self.SetTimer(5.0,True)
            self.bActive = True
        else:
            self.Velocity = Vector3(0,0,0)
            self.Gravity = True
            self.PhysicsMode = 0
            self.Physics = False
            self.SetTimer(0,False)
            self.bActive = False
        
    def EventTimer(self):
        if(self.Dir == 0):
            self.Gravity = False
            self.Velocity = Vector3(5,0,5)
            self.Dir = 1
            return
        if(self.Dir == 1):
            self.Gravity = True
            self.Velocity = Vector3(0,0,0)
            self.Dir = 2
            return
        if(self.Dir == 2):
            self.Gravity = False
            self.Velocity = Vector3(-5,0,5)
            self.Dir = 3
            return
        if(self.Dir == 3):
            self.Gravity = True
            self.Velocity = Vector3(0,0,0)
            self.Dir = 0
            return

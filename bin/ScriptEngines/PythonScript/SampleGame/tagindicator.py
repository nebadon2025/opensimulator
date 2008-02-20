
import rxactor

import sys
import clr
asm = clr.LoadAssemblyByName('OpenSim.Region.ScriptEngine.Common')
Vector3 = asm.OpenSim.Region.ScriptEngine.Common.LSL_Types.Vector3



class TagIndicator(rxactor.Actor):
    MyPawn = None
    MyStartLocation = None
    MyGameInfo = None
    
    def GetScriptClassName():
        return "tagindicator.TagIndicator"
    
    def EventCreated(self):
        super(self.__class__,self).EventCreated()
        self.MyStartLocation = self.llGetPos()
    
    def AttachToPawn(self,vPawn,vGameInfo):
        self.MyPawn = vPawn
        self.MyGameInfo = vGameInfo
        self.MyPawn.TagTagged = True
        self.SetTimer(0.07,True)
        self.EnableTick()

    def DetachFromPawn(self):
        self.MyPawn = None
        self.SetTimer(0,False)
        self.DisableTick()
        self.llSetPos(self.MyStartLocation)

    def EventTimer(self):
        if self.MyPawn == None:
            return
            
        templist = self.GetRadiusAvatars(2)
        for i in templist:
            temppawn = self.MyWorld.AllActors[i]
            if not temppawn.TagTagged:
                self.MyGameInfo.PlayerGotTagged(temppawn)

        del templist

    def EventTick(self,vDeltaTime):
        if self.MyPawn != None:
            loc = self.MyPawn.llGetPos() + Vector3(0,0,0.1)
            self.llSetPos(loc) #SetLocationFast


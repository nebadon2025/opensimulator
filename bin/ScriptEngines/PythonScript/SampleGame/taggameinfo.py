
import rxactor

import sys
import clr
asm = clr.LoadAssemblyByName('OpenSim.Region.ScriptEngine.Common')
Vector3 = asm.OpenSim.Region.ScriptEngine.Common.LSL_Types.Vector3

import random
import tagindicator
import math


class TagGameInfo(rxactor.Actor):
    MyGameStatus = 0
    MyStarter = None
    MyWinner = ""
    MyCount = 0
    
    def GetScriptClassName():
        return "taggameinfo.TagGameInfo"
    
    def EventCreated(self):
        pass

    def EventDestroyed(self):
        pass
    
    def EventTouch(self,vAvatar):
        if self.MyGameStatus == 0 or self.MyGameStatus == 3:
            self.MyGameStatus = 1
            self.StartGame()
        else:
            print "Tag Game has already started..."


    def StartGame(self):
        try:
            self.MyWinner = None
            
            # Who is the initial tag?
            tempi = random.randint(0,len(self.MyWorld.AllAvatars)-1)

            # Set all to not tagged
            for iid, iplr in self.MyWorld.AllAvatars.iteritems():
                iplr.TagTagged = False
                iplr.SetMovementModifier(0.6)

            i = 0
            for iid, iplr in self.MyWorld.AllAvatars.iteritems():
                if i == tempi:
                    self.MyStarter = iplr
                    break
                else:
                    i = i+1

            self.MyStarter.DoLocalTeleport(self.llGetPos()+Vector3(0,0,0.5))
            self.MyStarter.SetFreezed(True)

            self.PlayerGotTagged(self.MyStarter)
            tempstr = self.MyStarter.GetFullName()+" is tag."
            self.llShout(0,tempstr)
            self.MyCount = 7
            self.SetTimer(1,True)

            for iid, iplr in self.MyWorld.AllAvatars.iteritems():
                if iplr != self.MyStarter:
                    temploc = self.llGetPos() + Vector3(0,0,0.5)
                    tempang = random.random()*2*math.pi
                    x = math.sin(tempang) * 12;
                    y = math.cos(tempang) * 12;
                    randloc = Vector3(x,y,0)
                    iplr.DoLocalTeleport(temploc+randloc)
                    iplr.SetFreezed(True)
        except:
            print "taggameinfo,StartGame", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]
    
    def GameEnded(self):
        for iid, iactor in self.MyWorld.AllActors.iteritems():
            if isinstance(iactor,tagindicator.TagIndicator):
                if iactor.MyPawn != None:
                    iactor.DetachFromPawn()

        for iid, iplr in self.MyWorld.AllAvatars.iteritems():
            iplr.SetMovementModifier(1)
            iplr.SetFreezed(False)

        self.MyCount = 15
        self.MyGameStatus = 3
    

    def PlayerGotTagged(self,vTaggedPlr):
        if self.MyGameStatus == 3:
            return;
        
        try:
            self.MyWinner = vTaggedPlr
            self.MyWinner.SetMovementModifier(0.55)
            
            for iid, iactor in self.MyWorld.AllActors.iteritems():
                if isinstance(iactor,tagindicator.TagIndicator):
                    if iactor.MyPawn == None:
                        iactor.AttachToPawn(vTaggedPlr,self)
                        break;

            # Check if game ended?
            plrcount = len(self.MyWorld.AllAvatars)

            plrtaggedcount = 0
            for iid, iplr in self.MyWorld.AllAvatars.iteritems():
                if iplr.TagTagged:
                    plrtaggedcount = plrtaggedcount+1

            if plrtaggedcount >= plrcount:
                self.GameEnded()
            else:
                tempstr = vTaggedPlr.GetFullName()+" was tagged"
                self.llShout(0,tempstr)
                self.SendGeneralAlertAll(tempstr)
        except:
            print "taggameinfo,PlayerGotTagged", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]
    
    
    def EventTimer(self):
        if self.MyGameStatus == 1:
            tempstr = self.MyStarter.GetFullName()+" is tag. Starting in:"+str(self.MyCount)
            self.llShout(0,tempstr)
            self.MyCount = self.MyCount-1
            if self.MyCount <= 0:
                self.MyGameStatus = 2
                for iid, iplr in self.MyWorld.AllAvatars.iteritems():
                    iplr.SetFreezed(False)
                
        elif self.MyGameStatus == 2:
            for iid, iplr in self.MyWorld.AllAvatars.iteritems():
                if not iplr.TagTagged:
                    dist = self.llVecDist(self.llGetPos(),iplr.llGetPos())
                    if (dist > float(17)):
                        self.PlayerGotTagged(iplr)

        elif self.MyGameStatus == 3:
            tempstr = self.MyWinner.GetFullName()+" is the winner!"
            self.llShout(0,tempstr)
            self.MyCount = self.MyCount-1
            if self.MyCount <= 0:
                self.SetTimer(0,False)

        
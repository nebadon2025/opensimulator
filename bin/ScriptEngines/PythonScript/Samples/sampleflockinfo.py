import rxactor

# Flockinfo not working at the moment. - Tuco

import sys
import clr
asm = clr.LoadAssemblyByName('OpenSim.Region.ScriptEngine.Common')
Vector3 = asm.OpenSim.Region.ScriptEngine.Common.LSL_Types.Vector3

import random
import math
import sampleflockmember

class FlockInfo(rxactor.Actor):
    
    def GetScriptClassName():
        return "sampleflockinfo.FlockInfo"
    
    def EventCreated(self):
        super(self.__class__,self).EventCreated()
        self.StartLoc = Vector3(0,0,0)
        self.BoidsInFlock = 0
        self.BoidMinDist = 1.33
        self.BoidMaxSpeed = 20

        self.Center = Vector3(0,0,0)
        self.AVGVel = Vector3(0,0,0)
        self.TargetVec = Vector3(0,0,0)

        self.ModifyCenterBias = 1.0
        self.CenterBiasCounter = 0
        self.MyBoids = []
        self.MyBoidIds = []
        self.bActive = False
    
    def EventTouch(self,vAvatar):
        try:
            if not self.bActive:
                self.StartLoc = self.llGetPos()
                self.SetNewTarget()
                self.BoidsInFlock = 0
                del self.MyBoidIds[:]

                for i in range(0, 4):
                    tempang = random.random()*2*math.pi
                    x = math.sin(tempang) * 5
                    y = math.cos(tempang) * 5
                    spawnloc = self.llGetPos() + Vector3(x,y,0)
                    tempboidid = self.SpawnActor(spawnloc,0,True,"sampleflockmember.FlockMember")
                    if(tempboidid != 0):
                        self.MyBoidIds.insert(0,tempboidid)
                    else:
                        print "Failed spawning flockmember!!!!!!!!!!"

                for j in self.MyBoidIds:
                    tempboid = self.MyWorld.AllActors[j]
                    self.AddToFlock(tempboid)

                for j in self.MyBoidIds:
                    tempboid = self.MyWorld.AllActors[j]
                    self.AddToFlock(tempboid)

                self.BoidsInFlock = len(self.MyBoids)
                self.SetTimer(0.1,True)

                for k in self.MyBoids:
                    k.Activate()

                self.bActive = True
            else:
                while len(self.MyBoids) > 0:
                    self.RemoveFromFlock(self.MyBoids[0])
                    
                self.SetTimer(0,False)
                self.bActive = False
        except:
            print "FlockInfo.EventTouch", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]
    
    def EventTimer(self):
        self.CountAVGs()

        if(self.CenterBiasCounter != 0):
            self.CenterBiasCounter = self.CenterBiasCounter + 1
            if(self.CenterBiasCounter > 20):
                self.ModifyCenterBias = 1.0
                self.CenterBiasCounter = 0

        if(random.random() < 0.01 and self.CenterBiasCounter == 0):
            self.ModifyCenterBias = -1.0
            self.CenterBiasCounter = 1
        
        if(random.random() < 0.01):
            self.SetNewTarget()

    def GetCurrentTarget(self):
        return self.TargetVec

    def SetNewTarget(self):
        try:
            tempang = random.random()*2*math.pi
            x = math.sin(tempang) * 7
            y = math.cos(tempang) * 7
            self.TargetVec = self.StartLoc + Vector3(x,y,0)
            self.llSetPos(self.TargetVec)
        except:
            print "FlockInfo.SetNewTarget", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]

    def AddToFlock(self, vNew):
        try:
            if(vNew.MyFlock == self):
                return
            if(vNew.MyFlock != None):
                vNew.MyFlock.RemoveFromFlock(vNew)

            self.MyBoids.insert(0,vNew)
            vNew.MyFlock = self
        except:
            print "FlockInfo.AddToFlock", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]

    def RemoveFromFlock(self,vRemoved):
        try:
            for i in self.MyBoids:
                if(i == vRemoved):
                    self.MyBoids.remove(i)
                    i.Deactivate()
                    return
            print "Unable to remove boid from flock."
        except:
            print "FlockInfo.RemoveFromFlock", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]
            
    def CountAVGs(self):
        try:
            self.Center = Vector3(0,0,0)
            self.AVGVel = Vector3(0,0,0)

            for i in self.MyBoids:
                self.Center = self.Center + i.llGetPos()
                self.AVGVel = self.AVGVel + i.Velocity
        except:
            print "FlockInfo.CountAVGs", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]



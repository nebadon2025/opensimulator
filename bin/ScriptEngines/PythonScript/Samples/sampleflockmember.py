import rxactor

# FlockMember not working at the moment. - Tuco

import sys
import clr
clr.AddReferenceToFile("OpenSim.Region.RexScriptModule.dll")
asm = clr.LoadAssemblyByName('OpenSim.Region.ScriptEngine.Common')
Vector3 = asm.OpenSim.Region.ScriptEngine.Common.LSL_Types.Vector3

import math
import sampleflockinfo

class FlockMember(rxactor.Actor):

    def __init__(self, vId):
        super(self.__class__,self).__init__(vId)
        self.MyFlock = None
        self.StartLoc = Vector3(0,0,0)
        self.bReady = False

    def GetScriptClassName():
        return "sampleflockmember.FlockMember"
    
    def EventCreated(self):
        super(self.__class__,self).EventCreated()
        self.bReady = True
        self.SetMesh("rabbit")
        self.Activate()
    
    def Activate(self):
        if(not self.bReady):
            return

        if(self.StartLoc == Vector3(0,0,0)):
            self.StartLoc = self.llGetPos()
        self.Physics = True
        self.PhysicsMode = 1
        self.Gravity = False
        self.Velocity = Vector3(0,0,0)
        self.Mass = 15.0
        self.SetTimer(0.1,True)
        
    def Deactivate(self):
        self.SetTimer(0,False)
        self.Velocity = Vector3(0,0,0)
        self.Gravity = True
        self.PhysicsMode = 0
        self.Physics = False
        self.llSetPos(self.StartLoc)
        self.MyFlock = None
        self.DestroyActor()
    
    def GetCenter(self):
        try:
            CurrentCenter = self.MyFlock.Center - self.llGetPos()
            CurrentCenter = CurrentCenter / (self.MyFlock.BoidsInFlock-1)
            return CurrentCenter
        except:
            print "FlockMember,GetCenter", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]

    def GetAverageVelocity(self):
        try:
            CurrentAVGVel = self.MyFlock.AVGVel - self.Velocity
            CurrentAVGVel = CurrentAVGVel / (self.MyFlock.BoidsInFlock-1)
            return CurrentAVGVel
        except:
            print "FlockMember,GetAverageVelocity", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]

    def AwayFromNeighbors(self):
        try:
            awayvec = Vector3(0,0,0)
            fullaway = Vector3(0,0,0)

            templist = self.GetRadiusActors(self.MyFlock.BoidMinDist)
            for i in templist:
                tempactor = self.MyWorld.AllActors[i]
                if((tempactor != self) and tempactor.__class__.__name__ == "FlockMember"):
                    awayvec = self.llGetPos() - tempactor.llGetPos()
                    fullaway = (fullaway + awayvec)

            return fullaway
        except:
            print "FlockMember,AwayFromNeighbors", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]

    def LimitNewVelocity(self, vStartVel,vLimiter):
        try:
            limitvel = Vector3(vStartVel)
            
            if(math.fabs(vStartVel.x) > math.fabs(vStartVel.y)):
                a = math.fabs(vStartVel.x)
            else:
                a = math.fabs(vStartVel.y)
            if(math.fabs(vStartVel.z) > a):
                a = math.fabs(vStartVel.z)

            if(a < vLimiter):
        	    return limitvel
            else:
        	    limitvel.x = (vStartVel.x * vLimiter / a)
        	    limitvel.y = (vStartVel.y * vLimiter / a)
        	    limitvel.z = (vStartVel.z * vLimiter / a)
        	    return limitvel
        except:
            print "FlockMember,LimitNewVelocity", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]

    def GetCurrentTarget(self):
        try:
            CurrentTarget = self.MyFlock.GetCurrentTarget()
            if(CurrentTarget != Vector3(0,0,0)):
                return (CurrentTarget-self.llGetPos())
            else:
                return Vector3(0,0,0)
        except:
            print "FlockMember,GetCurrentTarget", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]
        
    def EventTimer(self):
        try:
            if(self.MyFlock == None):
                return

            center_bias = self.GetCenter() - self.llGetPos()
            center_bias.x *= 0.6 * self.MyFlock.ModifyCenterBias
            center_bias.y *= 0.6 * self.MyFlock.ModifyCenterBias
            center_bias.z *= 0.6 * self.MyFlock.ModifyCenterBias

            avgvel_bias = self.GetAverageVelocity() - self.Velocity
            avgvel_bias.x *= 0.3
            avgvel_bias.y *= 0.3
            avgvel_bias.z *= 0.3

            awayfrommates = self.AwayFromNeighbors()
            awayfrommates.x *= 0.7
            awayfrommates.y *= 0.7
            awayfrommates.z *= 0.7

            targetbias = self.GetCurrentTarget()
            targetbias.x *= 0.6
            targetbias.y *= 0.6
            targetbias.z *= 0.6

            combinedvel = (center_bias + avgvel_bias + awayfrommates + targetbias)
            combinedvel.x *= 40
            combinedvel.y *= 40
            combinedvel.z *= 40
            limitvel = self.LimitNewVelocity(combinedvel,self.MyFlock.BoidMaxSpeed)
            self.Velocity = limitvel
        except:
            print "FlockMember,EventTimer", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]


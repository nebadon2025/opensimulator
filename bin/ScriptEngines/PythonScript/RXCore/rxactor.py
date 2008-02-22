# rxactor.py
# Parent class for all actors in the world.
#print "rxactor.................................."

import sys
import clr
clr.AddReferenceToFile("OpenSim.Region.RexScriptModule.dll")

import rxlslobject
import rxworld


class Actor(rxlslobject.LSLObject):

    def __init__(self, vId):
        #super(Actor,self).__init__()
        self.MyWorld = None
        self.Id = str(vId)

        self.MyTag = ""
        self.MyEvent = ""
        
        self.MyTimerCount = 0
        self.bTimerLoop = False

    #def __del__(self):
    #    #print "DELETING ACTOR!"
    #    #super(self.__class__, self).__del__()

    def GetScriptClassName():
        return "rxactor.Actor"

    def GetId(self):
        return str(self.Id)

    # Send python command to client
    def CommandToClient(self,vAgentId,vUnit,vCommand,vCmdParams):
        self.MyWorld.CS.CommandToClient(vAgentId,vUnit,vCommand,vCmdParams)

    # Velocity
    def SetVelocity(self,vVelocity):
        return self.MyWorld.CS.SetVelocity(self.Id,vVelocity)
    Velocity = property(fget=lambda self: self.llGetVel(),fset=lambda self, v: self.SetVelocity(v))

    # Physics
    def GetPhysics(self):
        return self.MyWorld.CS.GetPhysics(self.Id)
    def SetPhysics(self,vbPhysics):
        return self.MyWorld.CS.SetPhysics(self.Id,vbPhysics)
    Physics = property(fget=lambda self: self.GetPhysics(),fset=lambda self, v: self.SetPhysics(v))

    #Mass
    def SetMass(self,vMass):
        return self.MyWorld.CS.SetMass(self.Id,vMass)
    Mass = property(fget=lambda self: self.llGetMass(),fset=lambda self, v: self.SetMass(v))


    def GetUsePrimVolumeCollision(self):
        return self.MyWorld.CS.GetUsePrimVolumeCollision(self.Id)
    def SetUsePrimVolumeCollision(self,vUsePrimVolumeCol):
        self.MyWorld.CS.SetUsePrimVolumeCollision(self.Id,vUsePrimVolumeCol)

    def SendGeneralAlertAll(self,vString):
        self.MyWorld.CS.SendGeneralAlertAll(self.Id,vString)
    def SendAlertToAvatar(self,vAgentId,vString,vbModal):
        self.MyWorld.CS.SendAlertToAvatar(self.Id,vAgentId,vString,vbModal)

    def GetRadiusActors(self,vRadius):
        return self.MyWorld.CS.GetRadiusActors(self.Id,vRadius)
    def GetRadiusAvatars(self,vRadius):
        return self.MyWorld.CS.GetRadiusAvatars(self.Id,vRadius)

    def EnableTick(self):
        self.MyWorld.MyEventManager.EnableTickForActor(self)
    def DisableTick(self):
        self.MyWorld.MyEventManager.DisableTickForActor(self)

    def SetTimer(self,vTime,vbLoop):
        self.MyWorld.MyEventManager.SetTimerForActor(self,vTime,vbLoop)
        
    def SpawnActor(self,vLoc,vIndex,vbTemprorary,vPyClass):
        return self.MyWorld.CS.SpawnActor(vLoc,vIndex,vbTemprorary,vPyClass)
    def DestroyActor(self):
        return self.MyWorld.CS.DestroyActor(self.Id)

    def SetMesh(self,vMeshName):
        return self.MyWorld.CS.SetMesh(self.Id,vMeshName)
    def SetMeshByLLUUID(self,vMeshLLUUID):
        return self.MyWorld.CS.SetMeshByLLUUID(self.Id,vMeshLLUUID)
    
    def SetMaterial(self,vIndex,vName):
        return self.MyWorld.CS.SetMaterial(self.Id,vIndex,vName)

    # Scale
    Scale = property(fget=lambda self: self.llGetScale(),fset=lambda self, v: self.llSetScale(v))

    def GetTime(self):
        return self.MyWorld.MyEventManager.CurrTime


    # Events
    def EventPreCreated(self):
        pass

    def EventCreated(self):
        pass
        
    def EventDestroyed(self):
        pass

    def EventTouch(self,vAvatar):
        pass

    def EventTick(self,vDeltaTime):
        pass

    def EventTimer(self):
        pass

    def EventTrigger(self,vOther):
        pass

    def EventPrimVolumeCollision(self,vOther):
        pass


    # Trigger event
    def TriggerEvent(self,vEventStr,vOther):
        if len(vEventStr) == 0:
            print "TriggerEvent, no event string defined"
            return
        
        for iid, iactor in self.World.AllActors.iteritems():
            if iactor.MyTag == vEventStr:
                iactor.EventTrigger(vOther)
            

    def PrintActorList(self):
        print "Printing actor list..."
        print "Length is ",len(self.World.AllActors)
        for iid, iactor in self.World.AllActors.iteritems():
            print iid,iactor.Id




    #def GetFreezed(self):
    #    return self.MyWorld.CS.GetFreezed(self.Id)
    #def SetFreezed(self,vbFreeze):
    #    self.MyWorld.CS.SetFreezed(self.Id,vbFreeze)

    # PhysicsMode
    #def GetPhysicsMode(self):
    #    return self.MyWorld.CS.GetPhysicsMode(self.Id)
    #def SetPhysicsMode(self,vPhysicsMode):
    #    return self.MyWorld.CS.SetPhysicsMode(self.Id,vPhysicsMode)
    #PhysicsMode = property(fget=lambda self: self.GetPhysicsMode(),fset=lambda self, v: self.SetPhysicsMode(v))

    # Gravity
    #def GetUseGravity(self):
    #    return self.MyWorld.CS.GetUseGravity(self.Id)
    #def SetUseGravity(self,vbGravity):
    #    return self.MyWorld.CS.SetUseGravity(self.Id,vbGravity)
    #Gravity = property(fget=lambda self: self.GetUseGravity(),fset=lambda self, v: self.SetUseGravity(v))

    #def SetLocationFast(self,vLocation):
    #    self.MyWorld.CS.SetLocationFast(self.Id,vLocation)

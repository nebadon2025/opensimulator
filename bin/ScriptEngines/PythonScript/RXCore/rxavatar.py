# rxavatar.py
# Note:
#    Avatar inherits the rxlslobject but NOT all lsl functions from
#    that don't work. Just the ones which are overridden here work.
#    - Tuco

#print "rxavatar.................................."

import sys
import rxactor

class Avatar(rxactor.Actor):

    def GetScriptClassName():
        return "rxavatar.Avatar"
    
    def EventCreated(self):
        super(Avatar,self).EventCreated()
        #print "Avatar EventCreated",self.Id
        pass

    def EventDestroyed(self):
        super(Avatar,self).EventDestroyed()
        #print "Avatar EventDestroyed",self.Id
        pass


    def GetFullName(self):
        return self.MyWorld.CS.SPGetFullName(self.AgentId)
    def GetFirstName(self):
        return self.MyWorld.CS.SPGetFirstName(self.AgentId)
    def GetLastName(self):
        return self.MyWorld.CS.SPGetLastName(self.AgentId)
    def DoLocalTeleport(self,vLocation):
        self.MyWorld.CS.SPDoLocalTeleport(self.AgentId,vLocation)
        
    def llGetPos(self):
        return self.MyWorld.CS.SPGetPos(self.AgentId)
    def llSetPos(self,pos):
        self.MyWorld.CS.SPDoLocalTeleport(self.AgentId,pos)
        
    def llGetRot(self):
        return self.MyWorld.CS.SPGetRot(self.AgentId)
    def llSetRot(self,rot):
        return self.MyWorld.CS.SPSetRot(self.AgentId,rot,False)
    def SetRelativeRot(self,rot):
        return self.MyWorld.CS.SPSetRot(self.AgentId,rot,True)
    
    def GetMovementModifier(self):
        return self.MyWorld.CS.SPGetMovementModifier(self.AgentId)
    def SetMovementModifier(self,vSpeedMod):
        self.MyWorld.CS.SPSetMovementModifier(self.AgentId,vSpeedMod)
        
    def EventLeftMouseButtonPressed(self,vAgent):
        pass
    def EventRightMouseButtonPressed(self,vAgent):
        pass
    def EventMouseWheel(self,vAgent,vAction):
        pass

    # Hud functions
    def ShowInventoryMessage(self,vMessage):
        self.CommandToClient(self.AgentId,'hud','ShowInventoryMessage("'+vMessage+'")','')
    def ShowScrollMessage(self,vMessage,vTime):
        self.CommandToClient(self.AgentId,'hud','ShowScrollMessage("'+vMessage+'",'+str(vTime)+')','')
    def ShowTutorialBox(self,vMessage,vTime):
        self.CommandToClient(self.AgentId,'hud','ShowTutorialBox("'+vMessage+'",'+str(vTime)+')','')
    def DoFadeInOut(self,vIn,vBetween,vOut):
        self.CommandToClient(self.AgentId,'hud','DoFadeInOut('+str(vIn)+','+str(vBetween)+','+str(vOut)+')','')

    def SetSendMouseClickEvents(self,vbSendEvents):
        if(vbSendEvents):
            self.CommandToClient(self.AgentId,'client','mousebtns','1')
        else:
            self.CommandToClient(self.AgentId,'client','mousebtns','0')
    def SetSendMouseWheelEvents(self,vbSendEvents):
        if(vbSendEvents):
            self.CommandToClient(self.AgentId,'client','mousewheel','1')
        else:
            self.CommandToClient(self.AgentId,'client','mousewheel','0')


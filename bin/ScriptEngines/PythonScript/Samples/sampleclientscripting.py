# sampleclientscripting.py
#print "sampleclientscripting.................................."

import rxactor
import sys
import clr

asm = clr.LoadAssemblyByName('OpenSim.Region.ScriptEngine.Common')
Vector3 = asm.OpenSim.Region.ScriptEngine.Common.LSL_Types.Vector3

import random
import math

# Commands available on client-hud:
#  ShowInventoryMessage(vMessage)
#  ShowScrollMessage(vMessage,vTime)
#  ShowTutorialBox(vText,vTime):
#  DoFadeInOut(vInTime, vBetweenTime,vOutTime)
    
# Commands available on client-client:
#  mousebtns, 0=off, 1=on


# All effects in one class.
class ClientScripting(rxactor.Actor):
    def GetScriptClassName():
        return "sampleclientscripting.ClientScripting"

    def EventCreated(self):
        super(self.__class__,self).EventCreated()
        print "ClientScripting EventCreated"
        self.SendItem = 0

    def EventTouch(self,vAvatar):
        if(self.SendItem == 0):
            vAvatar.ShowInventoryMessage("This is a message from server")
            str = self.llGetObjectName() +  " sent ShowInventoryMessage command to client " + vAvatar.GetFullName()
            self.llShout(0,str)
        elif (self.SendItem == 1):
            vAvatar.ShowScrollMessage("This is a scrolling message from server lasting 10 seconds",10)
            str = self.llGetObjectName() +  " sent ShowScrollMessage command to client " + vAvatar.GetFullName()
            self.llShout(0,str)
        elif (self.SendItem == 2):
            vAvatar.ShowTutorialBox("This is a tutorial message box from server lasting 10 seconds",10)
            str = self.llGetObjectName() +  " sent ShowTutorialBox command to client " + vAvatar.GetFullName()
            self.llShout(0,str)
        elif (self.SendItem == 3):
            vAvatar.DoFadeInOut(3,3,3)
            str = self.llGetObjectName() +  " sent DoFadeInOut command to client " + vAvatar.GetFullName()
            self.llShout(0,str)
        elif (self.SendItem == 4):
            self.MyWorld.MyEventManager.SubsribeToEvent('lmb',self)
            self.MyWorld.MyEventManager.SubsribeToEvent('rmb',self)
            vAvatar.SetSendMouseClickEvents(True)
            str = self.llGetObjectName() +  " sent client-enablesendmousebtns " + vAvatar.GetFullName()
            self.llShout(0,str)
        elif (self.SendItem == 5):
            self.MyWorld.MyEventManager.UnSubsribeToEvent('lmb',self)
            self.MyWorld.MyEventManager.UnSubsribeToEvent('rmb',self)
            vAvatar.SetSendMouseClickEvents(False)
            str = self.llGetObjectName() +  " sent client-disablesendmousebtns " + vAvatar.GetFullName()
            self.llShout(0,str)
        elif (self.SendItem == 6):
            self.MyWorld.MyEventManager.SubsribeToEvent('mw',self)
            vAvatar.SetSendMouseWheelEvents(True)
            str = self.llGetObjectName() +  " sent client-enablesendmousewheel " + vAvatar.GetFullName()
            self.llShout(0,str)
        elif (self.SendItem == 7):
            self.MyWorld.MyEventManager.UnSubsribeToEvent('mw',self)
            vAvatar.SetSendMouseWheelEvents(False)
            str = self.llGetObjectName() +  " sent client-disablesendmousewheel " + vAvatar.GetFullName()
            self.llShout(0,str)
        
        self.SendItem = self.SendItem+1
        if(self.SendItem > 7):
            self.SendItem = 0


    def EventLeftMouseButtonPressed(self,vAgent):
        str = "Left mouse button was pressed by " + vAgent.GetFullName()
        self.llShout(0,str)

    def EventRightMouseButtonPressed(self,vAgent):
        str = "Right mouse button was pressed by " + vAgent.GetFullName()
        self.llShout(0,str)

    def EventMouseWheel(self,vAgent,vAction):
        if(vAction == "-1"):
            str = "Mouse wheel up by" + vAgent.GetFullName()
        else:
            str = "Mouse wheel down by" + vAgent.GetFullName()
        self.llShout(0,str)
        
        
        
# More info about an object by touching it.
class ArmChair(rxactor.Actor):
    def GetScriptClassName():
        return "sampleclientscripting.ArmChair"
    
    def EventCreated(self):
        super(self.__class__,self).EventCreated()
        print "ClientScripting.ArmChair EventCreated"
        self.Status = 0

    def EventTouch(self,vAvatar):
        if(self.Status == 0):
            self.CommandToClient(vAvatar.AgentId,'hud','ShowTutorialBox("RXR armchair for sale by rexuser, click again for more info)",6)','')
        else:
            self.CommandToClient(vAvatar.AgentId,'hud','ShowScrollMessage("RXR chair is very comfy to sit on. It features ultra modern fibers making sitting in it a comfortable experience. You will never sit in another chair again.",30)','')

        self.Status = self.Status+1
        if(self.Status > 1):
            self.Status = 0
        


# rxeventmanager.py
# print "rxeventmanager.................................."

import sys
import rxactor
import rxevent
import rxworld
import rxavatar


class EventManager(object):
    def __init__(self,vWorld):
        super(self.__class__,self).__init__()
        self.MyWorld = vWorld
        self.MyWorld.MyEventManager = self
        
        self.MyActiveListIndex = 0
        self.MyTickedActors = {}
        self.MyTimerList = []
        self.CurrTime = 0
        self.MyEventListA = []
        self.MyEventListB = []
        self.bShutDown = False

        # Register events
        self.MyEventClasses = {}
        self.MyEventClasses[rxevent.RexEventTouchStart.MyName] = getattr(self,"CreateEvent_"+rxevent.RexEventTouchStart.MyName),getattr(self,"HandleEvent_"+rxevent.RexEventTouchStart.MyName)
        self.MyEventClasses[rxevent.RexEventSetTimer.MyName] = getattr(self,"CreateEvent_"+rxevent.RexEventSetTimer.MyName),getattr(self,"HandleEvent_"+rxevent.RexEventSetTimer.MyName)
        self.MyEventClasses[rxevent.RexEventSetTick.MyName] = getattr(self,"CreateEvent_"+rxevent.RexEventSetTick.MyName),getattr(self,"HandleEvent_"+rxevent.RexEventSetTick.MyName)
        self.MyEventClasses[rxevent.RexEventAddEntity.MyName] = getattr(self,"CreateEvent_"+rxevent.RexEventAddEntity.MyName),getattr(self,"HandleEvent_"+rxevent.RexEventAddEntity.MyName)
        self.MyEventClasses[rxevent.RexEventRemoveEntity.MyName] = getattr(self,"CreateEvent_"+rxevent.RexEventRemoveEntity.MyName),getattr(self,"HandleEvent_"+rxevent.RexEventRemoveEntity.MyName)
        self.MyEventClasses[rxevent.RexEventAddPresence.MyName] = getattr(self,"CreateEvent_"+rxevent.RexEventAddPresence.MyName),getattr(self,"HandleEvent_"+rxevent.RexEventAddPresence.MyName)
        self.MyEventClasses[rxevent.RexEventRemovePresence.MyName] = getattr(self,"CreateEvent_"+rxevent.RexEventRemovePresence.MyName),getattr(self,"HandleEvent_"+rxevent.RexEventRemovePresence.MyName)
        self.MyEventClasses[rxevent.RexEventClientEvent.MyName] = getattr(self,"CreateEvent_"+rxevent.RexEventClientEvent.MyName),getattr(self,"HandleEvent_"+rxevent.RexEventClientEvent.MyName)
        self.MyEventClasses[rxevent.RexEventPrimVolumeCollision.MyName] = getattr(self,"CreateEvent_"+rxevent.RexEventPrimVolumeCollision.MyName),getattr(self,"HandleEvent_"+rxevent.RexEventPrimVolumeCollision.MyName)
        # RexEventTimer events are in their own list, so not added here.

        # Actors can register for receiving certain events
        self.EventNames = {}

    def ShutDown(self):
        try:
            self.bShutDown = True
            
            # Registered events
            self.MyEventClasses.clear()
            
            # Delete event lists
            #print "deleting MyEventListA A"
            while len(self.MyEventListA) > 0:
                TempEvent = self.MyEventListA.pop(0)
                del TempEvent

            #print "deleting MyEventListA B"
            while len(self.MyEventListB) > 0:
                TempEvent = self.MyEventListB.pop(0)
                del TempEvent

            # Delete timer event list
            #print "deleting Timerlist"
            while len(self.MyTimerList) > 0:
                TempEvent = self.MyTimerList.pop(0)
                del TempEvent
            
            # TickedActor list, clear list only
            #print "deleting MyTickedActors"
            while len(self.MyTickedActors) > 0:
                TempKey = self.MyTickedActors.keys()[0]
                TempActor = self.MyTickedActors.pop(TempKey)
                del TempActor
            
            #if len(self.MyTickedActors) > 0:
            #    del self.MyTickedActors[:]
            #while len(self.MyTickedActors) > 0:
            #    TempActor = self.MyTickedActors.pop(0)
            
            # Avatar list, clear list only
            #print "deleting AllAvatars"
            while len(self.MyWorld.AllAvatars) > 0:
                TempKey = self.MyWorld.AllAvatars.keys()[0]
                TempActor = self.MyWorld.AllAvatars.pop(TempKey)
                del TempActor
            #while len(self.MyWorld.AllAvatars) > 0:
            #    TempActor = self.MyWorld.AllAvatars.pop()

            # Actor list
            # print "deleting AllActors"
            while len(self.MyWorld.AllActors) > 0:
                TempKey = self.MyWorld.AllActors.keys()[0]
                self.DeleteActor(TempKey)

            # Eventnames list
            while len(self.EventNames) > 0:
                TempKey = self.EventNames.keys()[0]
                TempList = self.EventNames.pop(TempKey)
                del TempList


            # print "Settings lists to none"
            self.MyEventClasses = None
            self.MyEventListA = None
            self.MyEventListB = None
            self.EventNames = None
            self.MyTimerList = None
            self.MyTickedActors = None
        except:
            print "EventManager,shutDown", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]
        




    def CreateEventWithName(self,vName,*args):
        try:
            if self.bShutDown:
                return;
            
            TempEvent = None
            
            if(self.MyEventClasses.has_key(vName)):
                eventfuncdata = self.MyEventClasses[vName]
                TempEvent = eventfuncdata[0](*args)
            else:
                print "rxeventmanager,CreateEventWithName, no event named",vName

            if TempEvent != None:
                if self.MyActiveListIndex == 0:
                    self.MyEventListB.append(TempEvent);
                else:
                    self.MyEventListA.append(TempEvent);
        except:
            print "rxeventmanager,EventManager,CreateEventWithName", sys.exc_info()[0]
            print sys.exc_info()[1]
            print sys.exc_info()[2]



    def SwitchActiveList(self):
        if self.MyActiveListIndex == 0:
            self.MyActiveListIndex = 1
        else:
            self.MyActiveListIndex = 0


    def ProcessEvents(self,vDeltaTime):
        while True:
            try:
                TempEvent = None
                
                if self.MyActiveListIndex == 0:
                    if len(self.MyEventListA) == 0:
                        break
                    else:
                        TempEvent = self.MyEventListA.pop(0)
                else:
                    if len(self.MyEventListB) == 0:
                        break
                    else:
                        TempEvent = self.MyEventListB.pop(0)

                # What to do with events
                if(self.MyEventClasses.has_key(TempEvent.MyName)):
                    eventfuncdata = self.MyEventClasses[TempEvent.MyName]
                    eventfuncdata[1](TempEvent)
                else:
                    print "Unknown event not processed",TempEvent.MyName

                del TempEvent
            except:
                print "rxeventmanager,ProcessEvents:", sys.exc_info()[0]
                print sys.exc_info()[1]
                print sys.exc_info()[2]

    def CallTimeEvents(self,vDeltaTime):
        try:
            # Tick
            for TempId,TempA in self.MyTickedActors.iteritems():
                try:
                    TempA.EventTick(vDeltaTime)
                except:
                    print "rxeventmanager,CallTimeEvents,tick:", sys.exc_info()[0]
                    print sys.exc_info()[1]
                    print sys.exc_info()[2]

            # Timer
            while len(self.MyTimerList) > 0:
                TObj = self.MyTimerList[0]
                TempActor = None

                if TObj.TTime <= self.CurrTime:
                    TObj = self.MyTimerList.pop(0)
                    if self.MyWorld.AllActors.has_key(TObj.ObjectId):
                        try:
                            TempActor = self.MyWorld.AllActors[TObj.ObjectId]
                            TempActor.EventTimer()
                        except:
                            print "rxeventmanager,CallTimeEvents,timer:", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]
                    else:
                        print "No Actor for timer event",TObj.ObjectId

                    if TempActor != None:
                        if TempActor.bTimerLoop and TempActor.MyTimerCount > 0:
                            self.CreateTimerEventForLooping(TempActor)
                            #self.SetTimerForActor(TempActor,TempActor.MyTimerCount,TempActor.bTimerLoop)
                        else:
                            TempActor.MyTimerCount = 0
                            TempActor.bTimerLoop = False
                    del TObj
                else:
                    break
        except:
            print "CallTimeEvents", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]


    def CreateTimerEventForLooping(self,vActor):
        try:
            TempObj = rxevent.RexEventTimer(vActor.Id,self.CurrTime + vActor.MyTimerCount,vActor.bTimerLoop)

            index = 0
            for i in self.MyTimerList:
                if TempObj.TTime < i.TTime:
                    self.MyTimerList.insert(index,TempObj)
                    del TempObj
                    return
                else:
                    index += 1

            # Insert as last
            self.MyTimerList.append(TempObj)
            del TempObj
        except:
            print "CreateTimerEventForLooping", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]



    
    # Handle events
    # **********************************************************
    def CreateEvent_touch_start(self,*args):
        return rxevent.RexEventTouchStart(*args)

    def HandleEvent_touch_start(self,vEvent):
        try:
            if self.MyWorld.AllActors.has_key(vEvent.ObjectId):
                TempActor = self.MyWorld.AllActors[vEvent.ObjectId]

                if self.MyWorld.AllAvatars.has_key(vEvent.AgentId):
                    TempActor.EventTouch(self.MyWorld.AllAvatars[vEvent.AgentId])
                else:
                    print "MISSING AVATAR FOR TOUCH",vEvent.AgentId
            else:
                print "TOUCH EVENT FOR MISSING ACTOR",vEvent.ObjectId,vEvent.AgentId
        except:
            print "HandleEvent_TouchStart", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]



    def CreateEvent_add_presence(self,*args):
        return rxevent.RexEventAddPresence(*args)

    def HandleEvent_add_presence(self,vEvent):
        try:
            # If already on list, leave there.
            if self.MyWorld.AllAvatars.has_key(vEvent.ObjectId):
                print "HandleEvent_AddPresence, avatar is already on avatarlist",vEvent.AgentId
                return

            TempAvatar = rxavatar.Avatar(vEvent.ObjectId)
            TempAvatar.AgentId = vEvent.AgentId
            
            self.MyWorld.AllAvatars[vEvent.AgentId] = TempAvatar
            self.AddActor(TempAvatar)
        except:
            print "HandleEvent_AddPresence", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]


    def CreateEvent_remove_presence(self,*args):
        return rxevent.RexEventRemovePresence(*args)
    
    def HandleEvent_remove_presence(self,vEvent):
        try:
            if self.MyWorld.AllAvatars.has_key(vEvent.AgentId):
                TempAgent = self.MyWorld.AllAvatars.pop(vEvent.AgentId)
                AgentObjId = TempAgent.Id
                del TempAgent
                self.DeleteActor(AgentObjId)
            else:
                print "HandleEvent_RemovePresence, avatar not on avatarlist",vEvent.AgentId
        except:
            print "HandleEvent_RemovePresence", sys.exc_info()[0]
            print sys.exc_info()[1]
            print sys.exc_info()[2]


    def CreateEvent_add_entity(self,*args):
        return rxevent.RexEventAddEntity(*args)

    def HandleEvent_add_entity(self,vEvent):
        try:
            if self.MyWorld.AllActors.has_key(str(vEvent.ObjectId)):
                TempActor = self.MyWorld.AllActors[str(vEvent.ObjectId)]
                TempActor.EventPreCreated()
                TempActor.EventCreated()
            else:
                print "HandleEvent_AddEntity trying to be used the old way, actor not yet created."
        except:
            print "HandleEvent_AddEntity", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]



    def CreateEvent_remove_entity(self,*args):
        return rxevent.RexEventRemoveEntity(*args)
    
    def HandleEvent_remove_entity(self,vEvent):
        try:
            if not self.DeleteActor(vEvent.ObjectId):
                print "HandleEvent_RemoveEntity, no entity found with id:",vEvent.ObjectId
        except:
            print "HandleEvent_RemoveEntity", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]


    def CreateEvent_set_timer(self,*args):
        return rxevent.RexEventSetTimer(*args)
    
    def HandleEvent_set_timer(self,vEvent):
        try:
            # If has previous time on list, remove it!
            for i in self.MyTimerList:
                if i.ObjectId == vEvent.ObjectId:
                    TempObj = self.MyTimerList.remove(i)
                    del TempObj
                    break

            if not self.MyWorld.AllActors.has_key(vEvent.ObjectId):
                #print "HandleEvent_SetTimer, actor not found ",vEvent.ObjectId
                return

            TempActor = self.MyWorld.AllActors[vEvent.ObjectId]
            TempActor.MyTimerCount = vEvent.TTime
            TempActor.bTimerLoop = vEvent.bLoop

            if vEvent.TTime <= 0:
                return;

            TempObj = rxevent.RexEventTimer(vEvent.ObjectId,self.CurrTime + vEvent.TTime,vEvent.bLoop)

            index = 0
            for i in self.MyTimerList:
                if TempObj.TTime < i.TTime:
                    self.MyTimerList.insert(index,TempObj)
                    del TempObj
                    return
                else:
                    index += 1

            # Insert as last
            self.MyTimerList.append(TempObj)
            del TempObj
        except:
            print "HandleEvent_SetTimer", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]


    def CreateEvent_set_tick(self,*args):
        return rxevent.RexEventSetTick(*args)

    def HandleEvent_set_tick(self,vEvent):
        try:
            if vEvent.bTick:
                if not self.MyWorld.AllActors.has_key(vEvent.ObjectId):
                    print "HandleEvent_SetTick, actor not found ",vEvent.ObjectId
                    return
                TempActor = self.MyWorld.AllActors[vEvent.ObjectId]
                self.MyTickedActors[vEvent.ObjectId] = TempActor
            else:
                if self.MyTickedActors.has_key(vEvent.ObjectId):
                    self.MyTickedActors.pop(vEvent.ObjectId)
        except:
            print "HandleEvent_SetTick", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]


    def CreateEvent_client_event(self,*args):
        return rxevent.RexEventClientEvent(*args)

    # Client event which was sent by client
    # param 0 is avatarid (lluuid)
    # param 1 is command (str)
    def HandleEvent_client_event(self,vEvent):
        try:
            if self.MyWorld.AllAvatars.has_key(vEvent.AgentId):
                TempAgent = self.MyWorld.AllAvatars[vEvent.AgentId]
            else:
                print "HandleEvent_ClientEvent, avatar not on avatarlist",vEvent.AgentId
                return

            command = str(vEvent.Params[1])
            # Left mouse button pressed
            if(command == "lmb"):
                TempAgent.EventLeftMouseButtonPressed(TempAgent)
                if(self.EventNames.has_key(command)):
                    templist = self.EventNames[command]
                    for a in templist:
                        a.EventLeftMouseButtonPressed(TempAgent)
            # Right mouse button pressed
            elif(command == "rmb"):
                TempAgent.EventRightMouseButtonPressed(TempAgent)
                if(self.EventNames.has_key(command)):
                    templist = self.EventNames[command]
                    for a in templist:
                        a.EventRightMouseButtonPressed(TempAgent)
            # Mouse wheel
            elif(command == "mw"):
                TempAgent.EventMouseWheel(TempAgent,str(vEvent.Params[2]))
                if(self.EventNames.has_key(command)):
                    templist = self.EventNames[command]
                    for a in templist:
                        a.EventMouseWheel(TempAgent,str(vEvent.Params[2]))
            else:
                print "HandleEvent_ClientEvent, unhandled command:",command + " from " + TempAgent.GetFullName()
        except:
            print "HandleEvent_ClientEvent", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]


    def CreateEvent_primvol_col(self,*args):
        return rxevent.RexEventPrimVolumeCollision(*args)

    def HandleEvent_primvol_col(self,vEvent):
        try:
            if self.MyWorld.AllActors.has_key(vEvent.ObjectId):
                TempActor = self.MyWorld.AllActors[vEvent.ObjectId]

                if self.MyWorld.AllActors.has_key(vEvent.ColliderId):
                    TempActor.EventPrimVolumeCollision(self.MyWorld.AllActors[vEvent.ColliderId])
                else:
                    print "MISSING ACTOR FOR PRIMVOLUMECOLLISION",vEvent.ColliderId
            else:
                print "PRIMVOLUMECOLLISION EVENT FOR MISSING ACTOR",vEvent.ObjectId,vEvent.ColliderId
        except:
            print "HandleEvent_PrimVolumeCollision", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]





    def AddActor(self,vActor):
        try:
            if self.MyWorld.AllActors.has_key(vActor.Id):
                print "AddActor, actor already on list",vActor.Id
                return False

            vActor.MyWorld = self.MyWorld
            self.MyWorld.AllActors[vActor.Id] = vActor
            vActor.EventPreCreated()
            vActor.EventCreated()
            return True
        except:
            print "AddActor", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]
        

    def DeleteActor(self,vId):
        try:
            if self.MyWorld.AllActors.has_key(vId):
                TempActor = self.MyWorld.AllActors.pop(vId)
                TempActor.SetTimer(0,False)
                TempActor.DisableTick()
                self.UnSubsribeAll(TempActor)
                TempActor.EventDestroyed()
                del TempActor
                return True
            else:
                print "DeleteActor, no actor found with id:",vId
                return False
        except:
            print "DeleteActor", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]
            return False


    # Actors subscribing for global events
    # ****************************************************************
    def SubsribeToEvent(self,vEvent,vActor):
        try:
            if(not self.EventNames.has_key(vEvent)):
                self.EventNames[vEvent] = []

            templist = self.EventNames[vEvent]
            templist.append(vActor)
        except:
            print "SubsribeToEvent", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]


    def UnSubsribeToEvent(self,vEvent,vActor):
        try:
            if(self.EventNames.has_key(vEvent)):
                templist = self.EventNames[vEvent]
                templist.remove(vActor)
            else:
                print "Trying to unsubsrive to missing eventname:",vEvent
        except:
            print "UnSubsribeToEvent", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]

    def UnSubsribeAll(self,vActor):
        try:
            for ename, elist in self.EventNames.iteritems():
                try:
                    elist.remove(vActor)
                except:
                    pass
        except:
            print "UnSubsribeAll", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]




    def EnableTickForActor(self,vActor):
        self.CreateEventWithName("set_tick",vActor.Id,True)
        
    def DisableTickForActor(self,vActor):
        self.CreateEventWithName("set_tick",vActor.Id,False)

    def SetTimerForActor(self,vActor,vTime,vbLoop):
        self.CreateEventWithName("set_timer",vActor.Id,vTime,vbLoop)

    def PrintActorList(self):
        print "Printing actor list..."
        for iid, iactor in self.MyWorld.AllActors.iteritems():
            print iid,iactor
    




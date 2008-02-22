# rxmain.py
#print "rxmain.................................."

import gc
import time
import sys
from threading import Thread

# csharp imports
import System

# python imports
import rxactor
import rxworld
import rxeventmanager
import rxevent
import rxworldinfo

# Global variables
globals()["MainScriptThread"] = None
globals()["MyWorld"] = None
globals()["MyWorldInfo"] = None

if globals().has_key('objCSharp'):
    globals()["MyWorld"] = rxworld.World(objCSharp)
else:
    print "ERROR, OpenSim serverobject objCSharp not found!"

globals()["MyEventManager"] = rxeventmanager.EventManager(globals()["MyWorld"])



# Main thread 
class MainThread(Thread):
    def __init__ (self):
        Thread.__init__(self)
        self.prevticktime = 0
        self.currtime = 0
        self.sleeptime = 0
        self.Status = 1

    def run(self):
        print "Python: Started script thread"
        
        try:
            for TempId,TempObj in globals()["MyWorld"].AllActors.iteritems():
                if(isinstance(TempObj,rxworldinfo.WorldInfo)):
                    globals()["MyWorldInfo"] = TempObj
                    break
        except:
            print "rxmain,MainThread.run, worldinfo", sys.exc_info()[0]
            print sys.exc_info()[1]
            print sys.exc_info()[2]
        
        try:
            for TempId,TempObj in globals()["MyWorld"].AllActors.iteritems():
                TempObj.EventCreated()
        except:
            print "rxmain,MainThread.run, EventCreated on actors:", sys.exc_info()[0]
            print sys.exc_info()[1]
            print sys.exc_info()[2]
            
        #print "Printing actor list..."
        #print "Length is ",len(MyWorld.AllActors)
        #for iid, iactor in MyWorld.AllActors.iteritems():
        #    print iid,iactor.Id

        while self.Status == 1:
            try:
                # Windows time.clock has better resolution than time.time
                self.currtime = time.clock()
                globals()["MyEventManager"].CurrTime = self.currtime
                
                timedelta = self.currtime - self.prevticktime
                if timedelta < 0:
                    timedelta = 0

                # try to run 25 ticks per second
                self.sleeptime = self.currtime - self.prevticktime
                if self.sleeptime < 0.033:
                    self.sleeptime = 0.033 - self.sleeptime
                    time.sleep(self.sleeptime)
                    #print "SLEEP!",self.sleeptime

                self.prevticktime = self.currtime

                # Process events
                globals()["MyEventManager"].SwitchActiveList()
                globals()["MyEventManager"].ProcessEvents(timedelta)
                # Tick and Timer events
                globals()["MyEventManager"].CallTimeEvents(timedelta)
            except:
                print "rxmain,MainThread.run:", sys.exc_info()[0]
                print sys.exc_info()[1]
                print sys.exc_info()[2]

        try:
            if self.Status != 1:
                globals()["MyEventManager"].ShutDown()
                del globals()["MyEventManager"]
                globals()["MyWorld"].ShutDown()
                del globals()["MyWorld"]
                del globals()["objCSharp"]

            gc.collect()
        except:
            print "rxmain,MainThread.run end:", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]

        self.Status = 3
        print "Python: Ended script thread"




def StartMainThread():
    gc.enable()
    globals()["MainScriptThread"] = MainThread()
    globals()["MainScriptThread"].start()

def StopMainThread():
    globals()["MainScriptThread"].Status = 2

    i = 0
    while i < 10:
        time.sleep(1)
        if globals()["MainScriptThread"].Status == 3:
            del globals()["MainScriptThread"]
            break;
        else:
            i = i+1
    print "StopMainThread Finished"








# Called by c#, creates actors as they are created on the server.
def CreateActorOfClass(vId,vClassName,vTag):
    strid = str(vId)
    strtag = str(vTag)

    # If actor with same id exists, get rid of it.
    try:
        if globals()["MyWorld"].AllActors.has_key(strid):
            globals()["MyEventManager"].DeleteActor(strid)
    except:
        print "rxmain,CreateActorOfClass,del:", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]
        return

    # Try to create the new object using the class. The class name might be bogus.
    try:
        TempCommand = "tempactor = " + vClassName.GetScriptClassName() + "(" + str(vId) + ")"
        exec(TempCommand)
    except:
        print "rxmain,CreateActorOfClass, bad class:",sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]
        return

    # Add actor
    try:
        tempactor.MyWorld = globals()["MyWorld"]
        globals()["MyWorld"].AllActors[strid] = tempactor;
        tempactor.MyTag = strtag

        # If engine is already running, call EventPreCreated and EventCreated on the actor in the next loop.
        if(globals()["MainScriptThread"] != None and globals()["MainScriptThread"].Status == 1):
            CreateEventWithName("add_entity",strid)
    except:
        print "rxmain,CreateActorOfClass,add", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]



# Called by c#, creates events happening on the server.
def CreateEventWithName(vName,*args):
    try:
        globals()["MyEventManager"].CreateEventWithName(vName,*args)
    except:
        print "rxmain,CreateEventWithName:", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]


# Called by c#, gets the start location for agent
def GetAvatarStartLocation():
    try:
        if(globals()["MyWorldInfo"] != None):
            return globals()["MyWorldInfo"].GetAvatarStartLocation()
        else:
            return None
    except:
        print "rxmain,GetAvatarStartLocation:", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]
        
        
        
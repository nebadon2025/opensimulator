# rxevent.py
#print "rxevent.................................."

class RexEvent(object):
    MyName = ""

    #def __init__(self):
    #    super(self.__class__,self).__init__()

    #def __del__(self):
    #    pass

    def PrintDebugStr(self):
        pass

# Touch
class RexEventTouchStart(RexEvent):
    MyName = "touch_start"

    def __init__(self, *args):
        super(self.__class__,self).__init__()
        self.ObjectId = str(args[0])
        self.AgentId = str(args[1])

    def PrintDebugStr(self):
        print self.MyName,self.ObjectId,self.AgentId


# Timer
class RexEventTimer(RexEvent):
    MyName = "timer"

    def __init__(self, vObjId,vTime,vbLoop):
        super(self.__class__,self).__init__()
        self.ObjectId = str(vObjId)
        self.TTime = vTime
        self.bLoop = vbLoop

    def PrintDebugStr(self):
        print self.MyName,self.ObjectId,self.TTime,self.bLoop

# SetTimer
class RexEventSetTimer(RexEvent):
    MyName = "set_timer"

    def __init__(self, vObjId,vTime,vbLoop):
        super(self.__class__,self).__init__()
        self.ObjectId = str(vObjId)
        self.TTime = vTime
        self.bLoop = vbLoop

    def PrintDebugStr(self):
        print self.MyName,self.ObjectId,self.TTime,self.bLoop


# SetTick
class RexEventSetTick(RexEvent):
    MyName = "set_tick"

    def __init__(self, vObjId,vbTick):
        super(self.__class__,self).__init__()
        self.ObjectId = str(vObjId)
        self.bTick = vbTick

    def PrintDebugStr(self):
        print self.MyName,self.ObjectId,self.bTick



# Add entity
class RexEventAddEntity(RexEvent):
    MyName = "add_entity"

    def __init__(self,*args):
        super(self.__class__,self).__init__()
        self.ObjectId = str(args[0])

    def PrintDebugStr(self):
        print self.MyName,self.ObjectId



# Remove entity
class RexEventRemoveEntity(RexEvent):
    MyName = "remove_entity"

    def __init__(self,*args):
        super(self.__class__,self).__init__()
        self.ObjectId = str(args[0])

    def PrintDebugStr(self):
        print self.MyName,self.ObjectId



# Add presence
class RexEventAddPresence(RexEvent):
    MyName = "add_presence"

    def __init__(self,*args):
        super(self.__class__,self).__init__()
        self.ObjectId = str(args[0])
        self.AgentId = str(args[1])

    def PrintDebugStr(self):
        print self.MyName,self.ObjectId,self.AgentId

    
    
# Remove presence
class RexEventRemovePresence(RexEvent):
    MyName = "remove_presence"

    def __init__(self,*args):
        super(self.__class__,self).__init__()
        self.AgentId = str(args[0])

    def PrintDebugStr(self):
        print self.MyName,self.AgentId



# Client event
class RexEventClientEvent(RexEvent):
    MyName = "client_event"

    def __init__(self,*args):
        super(self.__class__,self).__init__()
        self.AgentId = str(args[0])
        self.Params = args

    def PrintDebugStr(self):
        print self.MyName,self.AgentId
        

# PrimVolumeCollision event
class RexEventPrimVolumeCollision(RexEvent):
    MyName = "primvol_col"

    def __init__(self,*args):
        super(self.__class__,self).__init__()
        self.ObjectId = str(args[0])
        self.ColliderId = str(args[1])

    def PrintDebugStr(self):
        print self.MyName,self.ObjectId,self.ColliderId

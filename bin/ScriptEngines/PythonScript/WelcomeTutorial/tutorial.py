import rxactor
import rxavatar

import sys
import clr
asm = clr.LoadAssemblyByName('OpenSim.Region.ScriptEngine.Common')
Vector3 = asm.OpenSim.Region.ScriptEngine.Common.LSL_Types.Vector3

class WalkStart(rxactor.Actor):
    """a.k.a. TutorialMaster (controls / runs them all)"""
    
    #def __init__(self, id):
    #    super(self.__class__, self).__init__(self, id)
    #    self.tutorials = {}
        
    def GetScriptClassName():
        return "tutorial.WalkStart"
        
    def EventCreated(self):
        super(self.__class__, self).EventCreated()
        print "tutorial.WalkStart EventCreated"
        self.SetUsePrimVolumeCollision(True)
        self.tutorials = {}
        self.SetTimer(0.1, True)
        
    def EventPrimVolumeCollision(self, other):
        if isinstance(other, rxavatar.Avatar):
            if not other.AgentId in self.tutorials:
                self.tutorials[other.AgentId] = WalkingTutorial(self, other)
                
    def EventTimer(self):
        try:
            for t in self.tutorials.itervalues():
                t.update()
            
        except:
            print "Tutorial,EventTimer", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]
            
    def done(self, tut, av):
        del self.tutorials[av.AgentId]

class WalkingGuide(rxactor.Actor):
    all = {} #id: instance, so that Tutorial can get the ref back
    GO = 1
    RETURN = 2
    WAIT = 3
    
    def GetScriptClassName():
        return "tutorial.Guide"
    
    def __init__(self, id):
        #global guides
        super(self.__class__, self).__init__(id) #converts id to string. ok?
        Guide.all[self.Id] = self
        #guides[id] = self
        #print "=>", guides
        print "-->", Guide.all
        self.tut = None #the tut where this guide is doing its part
        self.target = None
        
        self.state = Guide.GO
                
    def EventCreated(self):
        super(self.__class__, self).EventCreated()
        #Guide.all[self.Id] = self
        print "Guide created", Guide.all
        #self.Physics = True
        #self.PhysicsMode = 1
        #self.Gravity = False

        #self.bReady = True
        #self.SetMesh("rabbit")
        #self.Activate()
        
    def go_towards(self, t, r): #is the unimplemented llTarget the same?
        pos = self.llGetPos()
        tdist = t - pos
        if Vector3.Mag(tdist) > r:
            #no worky: self.Velocity = Vector3.Norm(tdist) * 0.01
            self.llSetPos(pos + (Vector3.Norm(tdist) * 0.1))
            return tdist #not there yet
                        
        else:
            #self.Velocity = Vector3(0, 0, 0)
            return None #reached target

                
    def update(self):
        if self.tut is not None:
            if self.target is not None:
                pos = self.llGetPos()
                avpos = self.tut.av.llGetPos()
                avdist = Vector3.Mag(avpos - pos)
                dist = -1 #failsafe for debug print
                if self.state == Guide.GO:
                    if avdist < 5.0: #NOTE: int comparison failed. ironpython bug, or what?
                        dist = self.go_towards(self.target, 0.5)
                        if dist is None:
                            self.state = Guide.WAIT
                    else:
                        self.state = Guide.RETURN
                        self.llSay(0, "This way, just follow..")
                        
                if self.state == Guide.RETURN:
                    if avdist < 3.0:
                        self.state = Guide.GO
                    else:
                        dist = self.go_towards(avpos, 4.0)
                        
                if self.state == Guide.WAIT:
                    if avdist < 2.0:
                        self.llSay(0, "There you go - now you know how to walk. If you want to learn how to interact with things, you can proceed to the signboard nearby.")
                        self.tut.done(self)
                        print self.Id, Guide.all
                        try:
                            del Guide.all[str(self.Id)] #uh how is that int when rxActor has str()ed it?
                        except KeyError:
                            print "Guide.all KeyError: no,", self.Id, "in", Guide.all
                        self.DestroyActor()
                        
                        #or? self.llDie()
                        
                msg = "%d, %s | %s -- %s" % (self.state, str(avdist), str(dist), str(avdist))
                self.llSetText(msg, Vector3(1,0,0), 1)
            
        else:
            print ","

class WalkingTutorial:
    """the guiding of walking for a single user"""
    
    def __init__(self, master, av):        
        print "starting tutorial for", av
        self.master = master
        self.av = av
        self.guide = None
        
        spawnloc = av.llGetPos() + Vector3(0, -2, 1) #llGetRot
        
        #REFACTOR: exceptions would be more pythonic
        guide_id = master.SpawnActor(spawnloc, 0, True, "tutorial.WalkingGuide")
        if guide_id != 0:
            self.guide_id = str(guide_id) #rxActor constructor str()s too :/
            
        else:
            print "TUTORIAL ERROR: Failed spawning guide!"
            self.guide_id = None
                                
    def update(self):
        #print "."
        if self.guide is None and self.guide_id is not None:
            if self.guide_id in Guide.all: #guides
                self.guide = Guide.all[self.guide_id] #guides[self.guide_id]
                print "Got guide", self.guide.Id, "for av", self.av.Id
                self.guide.tut = self
                self.guide.target = self.master.llGetPos() + Vector3(0, -12, 0)
            else:
                print ",", self.guide_id, " != ", Guide.all, type(self.guide_id), type(Guide.all.keys()[0]) #guides

        if self.guide is not None:
            self.guide.update() 
            #could have own timer too -
            
    def done(self, guide):
        self.guide = None
        self.guide_id = None
        self.master.done(self, self.av)

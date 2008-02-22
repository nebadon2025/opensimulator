# rxworld.py
# World object.
#print "rxworld.................................."

class World(object):
    CS = None
    MyEventManager = None
    AllActors = {}
    AllAvatars = {}

    def __init__(self, vCS):
        super(self.__class__,self).__init__()
        self.CS = vCS

    def ShutDown(self):
        try:
            self.CS = None
            self.AllActors = None
            self.AllAvatars = None
        except:
            print "World,shutDown", sys.exc_info()[0],sys.exc_info()[1],sys.exc_info()[2]
            
# rxworldinfo.py
# Parent class for worldinfo objects
# print "rxworldinfo.................................."

import rxactor

class WorldInfo(rxactor.Actor):

    def GetScriptClassName():
        return "rxworldinfo.WorldInfo"
    
    def GetAvatarStartLocation(self):
        return None
        

import rxactor
import rxavatar
import sys
import clr

asm = clr.LoadAssemblyByName('OpenSim.Region.ScriptEngine.Common')
Vector3 = asm.OpenSim.Region.ScriptEngine.Common.LSL_Types.Vector3

class SayHello(rxactor.Actor):
    def GetScriptClassName():
        return "samplevolumetrigger.SayHello"

    def EventCreated(self):
        super(self.__class__,self).EventCreated()
        self.SetUsePrimVolumeCollision(True)
        self.MyAvatars = {}
        
    # This event triggers every 1 second
    # It's enough to send text to avatar every 6 seconds
    def EventPrimVolumeCollision(self,vOther):
        if isinstance(vOther,rxavatar.Avatar):
            if self.MyAvatars.has_key(vOther.AgentId):
                if(self.GetTime() > self.MyAvatars[vOther.AgentId]):
                    self.ShowMyTextToAvatar(vOther)
            else:
                self.ShowMyTextToAvatar(vOther)

    def ShowMyTextToAvatar(self,vAvatar):
        self.MyAvatars[vAvatar.AgentId] = self.GetTime()+6
        vAvatar.ShowTutorialBox("This is a nice place to stand for a while",9)




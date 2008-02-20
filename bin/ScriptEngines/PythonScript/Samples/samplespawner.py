import rxactor
import sys
import clr
import random
import math

asm = clr.LoadAssemblyByName('OpenSim.Region.ScriptEngine.Common')
Vector3 = asm.OpenSim.Region.ScriptEngine.Common.LSL_Types.Vector3

# Sample tree
class Tree(rxactor.Actor):
    def GetScriptClassName():
        return "samplespawner.Tree"

    def EventCreated(self):
        super(self.__class__,self).EventCreated()
        self.GrowCount = 1
        self.SetMesh("birch2")
        self.SetMaterial(0,"oksa5")
        self.SetMaterial(1,"lehtipuu_kuori")
        self.SetTimer(0.05,True)
        
    def EventTimer(self):
        self.GrowCount += 1
        if(self.GrowCount > 70):
            self.SetTimer(0,False)
        else:
            self.Scale = Vector3(0.0175*self.GrowCount,0.03125*self.GrowCount,0.0375*self.GrowCount)


# Forest spawner
class Spawner(rxactor.Actor):
    def GetScriptClassName():
        return "samplespawner.Spawner"

    def EventTouch(self,vAvatar):
        for i in range(0, 3):
            tempang = random.random()*2*math.pi
            x = math.sin(tempang) * random.random() * 15
            y = math.cos(tempang) * random.random() * 15
            spawnloc = self.llGetPos() + Vector3(x,y,0)
            self.SpawnActor(spawnloc,0,False,"samplespawner.Tree")
        




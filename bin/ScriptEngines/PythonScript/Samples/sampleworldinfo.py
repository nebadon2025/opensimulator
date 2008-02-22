# sampleworldinfo.py
#print "sampleworldinfo.................................."

import rxworldinfo
import sys
import clr

from System.Collections.Generic import List as GenericList

asm = clr.LoadAssemblyByName('OpenSim.Region.ScriptEngine.Common')
Vector3 = asm.OpenSim.Region.ScriptEngine.Common.LSL_Types.Vector3


class SampleWorldInfo(rxworldinfo.WorldInfo):
    def GetScriptClassName():
        return "sampleworldinfo.SampleWorldInfo"
    
    def GetAvatarStartLocation(self):
        templist = GenericList[Vector3]()
        loc = Vector3(70,70,100)
        lookat = Vector3(128,128,100)
        
        templist.Add(loc)
        templist.Add(lookat)
        return templist

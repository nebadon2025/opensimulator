
#print "rxlslobject.................................."
#import sys
#import clr
#clr.AddReferenceToFile("OpenSim.Region.RexScriptModule.dll")

class LSLObject(object):

    # math
    def llSin(self,f):
        return self.MyWorld.CS.llSin(f)
    def llCos(self,f):
        return self.MyWorld.CS.llCos(f)
    def llTan(self,f):
        return self.MyWorld.CS.llTan(f)
    def llAtan2(self,x,y):
        return self.MyWorld.CS.llAtan2(x,y)
    def llSqrt(self,f):
        return self.MyWorld.CS.llSqrt(f)
    def llPow(self,fbase,fexponent):
        return self.MyWorld.CS.llPow(fbase,fexponent)
    def llAbs(self,i):
        return self.MyWorld.CS.llAbs(i)
    def llFabs(self,f):
        return self.MyWorld.CS.llFabs(f)
    def llFrand(self,mag):
        return self.MyWorld.CS.llFrand(mag)
    def llFloor(self,f):
        return self.MyWorld.CS.llFloor(f)
    def llCeil(self,f):
        return self.MyWorld.CS.llCeil(f)
    def llRound(self,f):
        return self.MyWorld.CS.llRound(f)

    def llVecMag(self,v):
        return self.MyWorld.CS.llVecMag(v)
    def llVecNorm(self, v):
        return self.MyWorld.CS.llVecNorm(v)
    def llVecDist(self,a,b):
        return self.MyWorld.CS.llVecDist(a,b)

    def llRot2Euler(self,r):
        return self.MyWorld.CS.llRot2Euler(r)
    def llEuler2Rot(self,v):
        return self.MyWorld.CS.llEuler2Rot(v)
        
    def llAxes2Rot(self,fwd, left, up):
        return self.MyWorld.CS.llAxes2Rot(fwd,left,up)
    def llRot2Fwd(self,r):
        return self.MyWorld.CS.llRot2Fwd(r)
    def llRot2Left(self,r):
        return self.MyWorld.CS.llRot2Left(r)
    def llRot2Up(self,r):
        return self.MyWorld.CS.llRot2Up(r)
    def llRotBetween(self,a,b):
        return self.MyWorld.CS.llRotBetween(a,b)
    
    def llWhisper(self,channelID,text):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llWhisper(channelID,text)
    def llSay(self,channelID,text):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSay(channelID,text)
    def llShout(self,channelID,text):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llShout(channelID,text)
        
    def llListen(self,channelID, name, ID,msg):
        print "llListen not implemented"
    def llListenControl(self,number,active):
        print "llListenControl not implemented"
    def llListenRemove(self,number):
        print "llListenRemove not implemented"
    def llSensor(self,name,id,type,range,arc):
        print "llSensor not implemented"
    def llSensorRepeat(self,name,id,type,range,arc,rate):
        print "llSensorRepeat not implemented"
    def llSensorRemove(self):
        print "llSensorRemove not implemented"


    def llDetectedName(self,number):
        print "llDetectedName not implemented"
        return ""
    def llDetectedKey(self,number):
        print "llDetectedKey not implemented"
        return ""
    def llDetectedOwner(self,number):
        print "llDetectedOwner not implemented"
        return ""
    def llDetectedType(self,number):
        print "llDetectedType not implemented"
        return ""
    def llDetectedPos(self,number):
        print "llDetectedPos not implemented"
        return Vector3(0,0,0)
    def llDetectedVel(self,number):
        print "llDetectedVel not implemented"
        return Vector3(0,0,0)
    def llDetectedGrab(self,number):
        print "llDetectedGrab not implemented"
        return Vector3(0,0,0)
    def llDetectedRot(self,number):
        print "llDetectedRot not implemented"
        #fixme, return Quaternion(0,0,0,0)

    def llDetectedGroup(self,number):
        print "llDetectedGroup not implemented"
        return 0
    def llDetectedLinkNumber(self,number):
        print "llDetectedLinkNumber not implemented"
        return 0

    def llDie(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)

    def llGround(self,offset):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGround(offset)
    
    def llCloud(self,offset):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llCloud(offset)

    def llWind(self,offset):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llWind(offset)

    def llSetStatus(self,status,value):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetStatus(status,value)

    def llGetStatus(self,status):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetStatus(status)

    def llSetScale(self,scale):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetScale(scale)

    def llGetScale(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetScale()

    def llSetColor(self,color,face):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetColor(color,face)

    def llGetAlpha(self, face):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetAlpha(face)

    def llSetAlpha(self,alpha,face):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetAlpha(alpha,face)

    def llGetColor(self,face):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetColor(face)

    def llSetTexture(self,texture,face):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetTexture(texture,face)

    def llScaleTexture(self,u,v,face):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llScaleTexture(u,v,face)

    def llOffsetTexture(self,u,v,face):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llOffsetTexture(u,v,face)

    def llRotateTexture(self,rotation,face):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llRotateTexture(rotation,face)
        
    def llGetTexture(self,face):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetTexture(face)


    def llSetPos(self,pos):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetPos(pos)

    def llGetPos(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetPos()

    def llGetLocalPos(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetLocalPos()

    def llSetRot(self,rot):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetRot(rot)

    def llGetRot(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetRot()

    def llGetLocalRot(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetLocalRot()


    def llSetForce(self,force,local):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetForce(force,local)
 
    def llGetForce(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetForce()
 
    def llTarget(self,position,range):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llTarget(position,range)


    def llTargetRemove(self,number):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llTargetRemove(number)

    def llRotTarget(self,rot,error):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llRotTarget(rot,error)

    def llRotTargetRemove(self,number):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llRotTargetRemove(number)

    def llMoveToTarget(self,target,tau):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llMoveToTarget(target,tau)

    def llStopMoveToTarget(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llStopMoveToTarget()

    def llApplyImpulse(self,force,local):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llApplyImpulse(force,local)

    def llApplyRotationalImpulse(self,force,local):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llApplyRotationalImpulse(force,local)

    def llSetTorque(self,force,local):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetTorque(force,local)

    def llGetTorque(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetTorque()

    def llSetForceAndTorque(self,force,torque,local):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetForceAndTorque(force,torque,local)

    def llGetVel(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetVel()
    
    def llGetAccel(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetAccel()

    def llGetOmega(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetOmega()

    def llGetTimeOfDay(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetTimeOfDay()

    def llGetWallclock(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetWallclock()

    def llGetTime(self):
        print "llGetTime not implemented"
        return 0;

    def llResetTime(self):
        print "llResetTime not implemented"

    def llGetAndResetTime(self):
        print "llGetAndResetTime not implemented"
        return 0;

    def llSound(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSound()

    def llPlaySound(self,sound,volume):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llPlaySound(sound,volume)

    def llLoopSound(self,sound,volume):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llLoopSound(sound,volume)

    def llLoopSoundMaster(self,sound,volume):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llLoopSoundMaster(sound,volume)
        
    def llLoopSoundSlave(self,sound,volume):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llLoopSoundSlave(sound,volume)

    def llPlaySoundSlave(self,sound,volume):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llPlaySoundSlave(sound,volume)

    def llTriggerSound(self,sound,volume):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llTriggerSound(sound,volume)

    def llStopSound(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llStopSound()

    def llPreloadSound(self,sound):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llPreloadSound(sound)




    def llGetSubString(self,src,start,end):
        return self.MyWorld.CS.llGetSubString(src,start,end)

    def llDeleteSubString(self,src,start,end):
        return self.MyWorld.CS.llDeleteSubString(src,start,end)

    def llInsertString(self,dst,position,src):
        return self.MyWorld.CS.llInsertString(dst,position,src)

    def llToUpper(self,src):
        return self.MyWorld.CS.llToUpper(src)

    def llToLower(self,src):
        return self.MyWorld.CS.llToLower(src)

    def llGiveMoney(self,destination,amount):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGiveMoney(destination,amount)


    def llMakeExplosion(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llMakeExplosion()

    def llMakeFountain(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llMakeFountain()
        
    def llMakeSmoke(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llMakeSmoke()
        
    def llMakeFire(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llMakeFire()
        
    def llRezObject(self,inventory,pos, rot,param):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llRezObject(inventory,pos, rot,param)


    def llLookAt(self,target,strength,damping):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llLookAt(target,strength,damping)

    def llStopLookAt(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llStopLookAt()

    def llSetTimerEvent(self):
        print "llSetTimerEvent not implemented"

    def llSleep(self):
        print "llSleep not implemented"

    def llGetMass(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetMass()

    def llCollisionFilter(self,name,id,accept):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llCollisionFilter(name,id,accept)

    def llTakeControls(self,controls,accept,pass_on):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llTakeControls(controls,accept,pass_on)

    def llReleaseControls(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llReleaseControls()

    def llAttachToAvatar(self,attachment):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llAttachToAvatar(attachment)


    def llDetachFromAvatar(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llDetachFromAvatar()

    def llTakeCamera(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llTakeCamera()
        
    def llReleaseCamera(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llReleaseCamera()
        
    def llGetOwner(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetOwner()
        
    def llInstantMessage(self,user,message):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llInstantMessage(user,message)

    def llEmail(self,address,subject,message):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llEmail(address,subject,message)

    def llGetNextEmail(self,address,subject):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llGetNextEmail(address,subject)

    def llGetKey(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetKey()

    def llSetBuoyancy(self,buoyancy):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetBuoyancy(buoyancy)

    def llSetHoverHeight(self,height,water,tau):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetHoverHeight(height,water,tau)

    def llStopHover(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llStopHover()

    def llMinEventDelay(self,delay):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llMinEventDelay(delay)

    def llSoundPreload(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSoundPreload()

    def llRotLookAt(self,target,strength,damping):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llRotLookAt(target,strength,damping)


    def llStringLength(self,str):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llStringLength(str)

    def llStartAnimation(self,anim):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llStartAnimation(anim)

    def llStopAnimation(self,anim):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llStopAnimation(anim)

    def llPointAt(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llPointAt()

    def llStopPointAt(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llStopPointAt()

    def llTargetOmega(self,axis,spinrate,gain):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llTargetOmega(axis,spinrate,gain)


    def llGetStartParameter(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetStartParameter()

    def llGodLikeRezObject(self,inventory,pos):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llGodLikeRezObject(inventory,pos)

    def llRequestPermissions(self,agent,perm):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llRequestPermissions(agent,perm)

    def llGetPermissionsKey(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llGetPermissionsKey()

    def llGetPermissions(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetPermissions()

    def llGetLinkNumber(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetLinkNumber()

    def llSetLinkColor(self,linknumber,color,face):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetLinkColor(linknumber,color,face)



    def llCreateLink(self,target,parent):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llCreateLink(target,parent)

    def llBreakLink(self,linknum):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llBreakLink(linknum)
        
    def llBreakAllLinks(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llBreakAllLinks()

    def llGetLinkKey(self,linknum):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetLinkKey(linknum)

    def llGetLinkName(self,linknum):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetLinkName(linknum)

    def llGetInventoryNumber(self,type):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetInventoryNumber(type)

    def llGetInventoryName(self,type,number):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetInventoryName(type,number)

    def llSetScriptState(self,name,run):
        print "llSetScriptState not implemented"

    def llGetEnergy(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetEnergy()

    def llGiveInventory(self,destination,inventory):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llGiveInventory(destination,inventory)

    def llRemoveInventory(self,item):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llRemoveInventory(item)

    # string text, vector color, double alpha
    def llSetText(self,text,color,alpha):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetText(text,color,alpha)

    def llWater(self,offset):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llWater(offset)

    def llPassTouches(self,vPass):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llPassTouches(vPass)

    def llRequestAgentData(self,id,data):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llRequestAgentData(id,data)

    def llRequestInventoryData(self,name):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llRequestInventoryData(name)


    def llSetDamage(self,damage):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetDamage(damage)

    def llTeleportAgentHome(self,agent):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llTeleportAgentHome(agent)

    def llModifyLand(self,action,brush):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llModifyLand(action,brush)

    def llCollisionSound(self,impact_sound,impact_volume):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llCollisionSound(impact_sound,impact_volume)


    def llCollisionSprite(self,impact_sprite):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llCollisionSprite(impact_sprite)

    def llGetAnimation(self,id):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetAnimation(id)


    def llResetScript(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llResetScript()


    def llMessageLinked(self,linknum,num,str,id):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llMessageLinked(linknum,num,str,id)

    def llPushObject(self,target,impulse,ang_impulse,local):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llPushObject(target,impulse,ang_impulse,local)

    def llPassCollisions(self,vPass):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llPassCollisions(vPass)


    def llGetScriptName(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetScriptName()

    def llGetNumberOfSides(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetNumberOfSides()


    def llAxisAngle2Rot(self,axis,angle):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llAxisAngle2Rot(axis,angle)

    def llRot2Axis(self,rot):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llRot2Axis(rot)

    def llRot2Angle(self,rot):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llRot2Angle(rot)

    def llAcos(self,val):
        return self.MyWorld.CS.llAcos(val)
    def llAsin(self,val):
        return self.MyWorld.CS.llAsin(val)
    def llAngleBetween(self,a,b):
        return self.MyWorld.CS.llAngleBetween(a,b)



    def llGetInventoryKey(self,name):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetInventoryKey(name)

    def llAllowInventoryDrop(self,add):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llAllowInventoryDrop(add)

    def llGetSunDirection(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetSunDirection()

    def llGetTextureOffset(self,face):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetTextureOffset(face)


    def llGetTextureScale(self,side):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetTextureScale(side)

    def llGetTextureRot(self,face):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetTextureRot(face)

    def llSubStringIndex(self,source,pattern):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llSubStringIndex(source,pattern)


    def llGetOwnerKey(self,id):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetOwnerKey(id)

    def llGetCenterOfMass(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetCenterOfMass()

    def llListSort(self,src,stride,ascending):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llListSort(src,stride,ascending)

    def llGetListLength(self,src):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetListLength(src)


    def llList2Integer(self,src,index):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llList2Integer(src,index)

    def osList2Double(self,src,index):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.osList2Double(src,index)

    def llList2Float(self,src,index):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llList2Float(src,index)

    def llList2String(self,src,index):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llList2String(src,index)

    def llList2Key(self,src,index):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llList2Key(src,index)

    def llList2Vector(self,src,index):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llList2Vector(src,index)

    def llList2Rot(self,src,index):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llList2Rot(src,index)

    def llList2List(self,src,start,end):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llList2List(src,start,end)

    def llDeleteSubList(self,src,start,end):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llDeleteSubList(src,start,end)

    def llGetListEntryType(self,src,index):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetListEntryType(src,index)

    def llList2CSV(self,src):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llList2CSV(src)

    def llCSV2List(self,src):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llCSV2List(src)

    def llListRandomize(self,src,stride):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llListRandomize(src,stride)

    def llList2ListStrided(self,src,start,end,stride):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llList2ListStrided(src,start,end,stride)

    def llGetRegionCorner(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetRegionCorner()

    def llListInsertList(self,dest,src,start):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llListInsertList(dest,src,start)

    def llListFindList(self,src,test):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llListFindList(src,test)






    def llGetObjectName(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetObjectName()

    def llSetObjectName(self,name):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetObjectName(name)

    def llGetDate(self):
        return self.MyWorld.CS.llGetDate()

    def llEdgeOfWorld(self,pos,dir):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llEdgeOfWorld(pos,dir)

    def llGetAgentInfo(self,id):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetAgentInfo(id)



    def llAdjustSoundVolume(self,volume):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llAdjustSoundVolume(volume)

    def llSetSoundQueueing(self,queue):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetSoundQueueing(queue)

    def llSetSoundRadius(self,radius):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetSoundRadius(radius)


    def llKey2Name(self,id):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llKey2Name(id)

    def llSetTextureAnim(self, mode,face,sizex,sizey,start,length,rate):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetTextureAnim(mode,face,sizex,sizey,start,length,rate)


    def llTriggerSoundLimited(self,sound,volume,top_north_east,bottom_south_west):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llTriggerSoundLimited(sound,volume,top_north_east,bottom_south_west)


    def llEjectFromLand(self,pest):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llEjectFromLand(pest)

    def llParseString2List(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llParseString2List()

    def llOverMyLand(self,id):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llOverMyLand(id)


    def llGetLandOwnerAt(self,pos):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetLandOwnerAt(pos)


    def llGetNotecardLine(self,name,line):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetNotecardLine(name,line)

    def llGetAgentSize(self,id):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetAgentSize(id)

    def llSameGroup(self,agent):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llSameGroup(agent)

    def llUnSit(self,id):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llUnSit(id)



    def llGroundSlope(self,offset):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGroundSlope(offset)

    def llGroundNormal(self,offset):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGroundNormal(offset)

    def llGroundContour(self,offset):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGroundContour(offset)

    def llGetAttached(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetAttached()

    def llGetFreeMemory(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetFreeMemory()

    def llGetRegionName(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetRegionName()

    def llGetRegionTimeDilation(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetRegionTimeDilation()
    
    def llGetRegionFPS(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetRegionFPS()

    def llParticleSystem(self,rules):
        print "llParticleSystem not implemented"
        
        
    def llGroundRepel(self,height,water,tau):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llGroundRepel(height,water,tau)
        
    def llGiveInventoryList(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llGiveInventoryList()

    def llSetVehicleType(self,type):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetVehicleType(type)


    def llSetVehicledoubleParam(self,param,value):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetVehicledoubleParam(param,value)


    def llSetVehicleVectorParam(self,param,vec):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetVehicleVectorParam(param,vec)


    def llSetVehicleRotationParam(self,param,rot):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetVehicleRotationParam(param,rot)

    def llSetVehicleFlags(self,flags):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetVehicleFlags(flags)

    def llRemoveVehicleFlags(self,flags):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llRemoveVehicleFlags(flags)

    def llSitTarget(self,offset,rot):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSitTarget(offset,rot)

    def llAvatarOnSitTarget(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llAvatarOnSitTarget()

    def llAddToLandPassList(self,avatar,hours):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llAddToLandPassList(avatar,hours)


    def llSetTouchText(self,text):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetTouchText(text)

    def llSetSitText(self,text):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetSitText(text)
        
    def llSetCameraEyeOffset(self,offset):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetCameraEyeOffset(offset)
        
    def llSetCameraAtOffset(self,offset):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetCameraAtOffset(offset)
        
        

    def llDumpList2String(self,src,separator):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llDumpList2String(src,separator)

    def llScriptDanger(self,pos):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llScriptDanger(pos)

    def llDialog(self,avatar,message,buttons,chat_channel):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llDialog(avatar,message,buttons,chat_channel)


    def llVolumeDetect(self,detect):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llVolumeDetect(detect)

    def llResetOtherScript(self,name):
        print "llResetOtherScript not implemented"
    def llGetScriptState(self,name):
        print "llGetScriptState not implemented"
    def llRemoteLoadScript(self):
        print "llRemoteLoadScript not implemented"
    def llSetRemoteScriptAccessPin(self,pin):
        print "llSetRemoteScriptAccessPin not implemented"
    def llRemoteLoadScriptPin(self,target,name,pin,running,start_param):
        print "llRemoteLoadScriptPin not implemented"


    def llOpenRemoteDataChannel(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llOpenRemoteDataChannel()

    def llSendRemoteData(self,channel,dest,idata,sdata):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSendRemoteData(channel,dest,idata,sdata)

    def llRemoteDataReply(self,channel,message_id,sdata,idata):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llRemoteDataReply(channel,message_id,sdata,idata)

    def llCloseRemoteDataChannel(self,channel):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llCloseRemoteDataChannel(channel)

    def llMD5String(self,src,nonce):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llMD5String(src,nonce)

    def llSetPrimitiveParams(self,rules):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetPrimitiveParams(rules)

    def llStringToBase64(self,str):
        return self.MyWorld.CS.llStringToBase64(str)

    def llBase64ToString(self,str):
        return self.MyWorld.CS.llBase64ToString(str)

    def llXorBase64Strings(self,str):
        self.MyWorld.CS.llXorBase64Strings()



    def llRemoteDataSetRegion(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llRemoteDataSetRegion()

    def llLog10(self,val):
        return self.MyWorld.CS.llLog10(val)
    def llLog(self,val):
        return self.MyWorld.CS.llLog(val)
    
    def llGetAnimationList(self,id):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetAnimationList(id)
    
    def llSetParcelMusicURL(self,url):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetParcelMusicURL(url)

    def llGetRootPosition(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetRootPosition()

    def llGetRootRotation(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetRootRotation()

    def llGetObjectDesc(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetObjectDesc()
    
    def llSetObjectDesc(self,desc):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetObjectDesc(desc)

    def llGetCreator(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetCreator()
 
    def llGetTimestamp(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetTimestamp()

    def llSetLinkAlpha(self,linknumber,alpha,face):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetLinkAlpha(linknumber,alpha,face)

    def llGetNumberOfPrims(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetNumberOfPrims()

    def llGetNumberOfNotecardLines(self,name):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetNumberOfNotecardLines(name)

    def llGetBoundingBox(self,obj):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetBoundingBox(obj)

    def llGetGeometricCenter(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetGeometricCenter()

    def llGetPrimitiveParams(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llGetPrimitiveParams()

    def llIntegerToBase64(self,number):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llIntegerToBase64(number)

    def llBase64ToInteger(self,str):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llBase64ToInteger(str)

    def llGetGMTclock(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetGMTclock()

    def llGetSimulatorHostname(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetSimulatorHostname()

    def llSetLocalRot(self,rot):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetLocalRot(rot)

    def llParseStringKeepNulls(self,src,seperators,spacers):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llParseStringKeepNulls(src,seperators,spacers)
    
    
    
    def llRezAtRoot(self,inventory,position,velocity,rot,param):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llRezAtRoot(inventory,position,velocity,rot,param)

    def llGetObjectPermMask(self,mask):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetObjectPermMask(mask)

    def llSetObjectPermMask(self,mask,value):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetObjectPermMask(mask,value)

    def llGetInventoryPermMask(self,item,mask):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llGetInventoryPermMask(item,mask)

    def llSetInventoryPermMask(self,item,mask,value):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetInventoryPermMask(item,mask,value)

    def llGetInventoryCreator(self,item):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetInventoryCreator(item)




    def llOwnerSay(self,msg):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llOwnerSay(msg)

    def llRequestSimulatorData(self,simulator,data):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llRequestSimulatorData(simulator,data)

    def llForceMouselook(self,mouselook):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llForceMouselook(mouselook)

    def llGetObjectMass(self,id):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetObjectMass(id)

    def llListReplaceList(self,dest,src,start,end):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llListReplaceList(dest,src,start,end)

    def llLoadURL(self,avatar_id,message,url):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llLoadURL(avatar_id,message,url)

    def llParcelMediaCommandList(self,commandList):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llParcelMediaCommandList(commandList)

    def llParcelMediaQuery(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llParcelMediaQuery()

    def llModPow(self,a,b,c):
        return self.MyWorld.CS.llModPow(a,b,c)

    def llGetInventoryType(self,name):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetInventoryType(name)
    
    def llSetPayPrice(self,price,quick_pay_buttons):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llSetPayPrice(price,quick_pay_buttons)
    
    def llGetCameraPos(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetCameraPos()

    def llGetCameraRot(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetCameraRot()


    def llSetPrimURL(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetPrimURL()

    def llRefreshPrimURL(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llRefreshPrimURL()

    def llEscapeURL(self,url):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llEscapeURL(url)

    def llUnescapeURL(self,url):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llUnescapeURL(url)
    
    
    
    def llMapDestination(self,simname,pos,look_at):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llMapDestination(simname,pos,look_at)

    def llAddToLandBanList(self,avatar,hours):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llAddToLandBanList(avatar,hours)

    def llRemoveFromLandPassList(self,avatar):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llRemoveFromLandPassList(avatar)

    def llRemoveFromLandBanList(self,avatar):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llRemoveFromLandBanList(avatar)

    def llSetCameraParams(self,rules):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llSetCameraParams(rules)

    def llClearCameraParams(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llClearCameraParams()

    def llListStatistics(self,operation,src):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llListStatistics(operation,src)

    def llGetUnixTime(self):
        return self.MyWorld.CS.llGetUnixTime()

    def llGetParcelFlags(self,pos):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetParcelFlags(pos)

    def llGetRegionFlags(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetRegionFlags()


    def llXorBase64StringsCorrect(self,str1,str2):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llXorBase64StringsCorrect(str1,str2)
    
    def llHTTPRequest(self,url,parameters,body):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llHTTPRequest(url,parameters,body)

    def llResetLandBanList(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llResetLandBanList()

    def llResetLandPassList(self):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.llResetLandPassList()


    def llGetParcelPrimCount(self,pos,category,sim_wide):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetParcelPrimCount(pos,category,sim_wide)

    def llGetParcelPrimOwners(self,pos):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetParcelPrimOwners(pos)

    def llGetObjectPrimCount(self,object_id):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetObjectPrimCount(object_id)

    def llGetParcelMaxPrims(self,pos,sim_wide):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetParcelMaxPrims(pos,sim_wide)


    def llGetParcelDetails(self,pos,param):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.llGetParcelDetails(pos,param)


    # Opensim functions
    def osTerrainSetHeight(self,x,y,val):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.osTerrainSetHeight(x,y,val)
    
    def osTerrainGetHeight(self,x,y):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.osTerrainGetHeight(x,y)

    def osRegionRestart(self,seconds):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.osRegionRestart(seconds)

    def osRegionNotice(self,msg):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        self.MyWorld.CS.osRegionNotice(msg)
        
    def osSetDynamicTextureURL(self,dynamicID,contentType,url,extraParams,timer):
        self.MyWorld.CS.SetScriptRunner(self.Id)
        return self.MyWorld.CS.osSetDynamicTextureURL(dynamicID,contentType,url,extraParams,timer)
    




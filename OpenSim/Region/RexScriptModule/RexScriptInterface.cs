using Axiom.Math;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Communications.Cache;

using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Scenes.Scripting;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.ScriptEngine.Common;
using OpenSim.Region.ScriptEngine.DotNetEngine;
using OpenSim.Region.ScriptEngine.DotNetEngine.Compiler;
using libsecondlife;

namespace OpenSim.Region.RexScriptModule
{
    public class RexScriptInterface : LSL_BuiltIn_Commands
    {
        private RexScriptEngine myScriptEngine;

        public RexScriptInterface(OpenSim.Region.ScriptEngine.DotNetEngine.ScriptEngine ScriptEngine, SceneObjectPart host, uint localID, LLUUID itemID, RexScriptEngine vScriptEngine)
            : base(ScriptEngine, host, localID, itemID)
        {   
            myScriptEngine = vScriptEngine;
            m_ScriptEngine = new OpenSim.Region.ScriptEngine.DotNetEngine.ScriptEngine();
            m_ScriptEngine.World = myScriptEngine.World;
        }

        private EntityBase GetEntityBase(uint vId)
        {
            SceneObjectPart part = myScriptEngine.World.GetSceneObjectPart(vId);
            if (part != null && (EntityBase)(part.ParentGroup) != null)
                return (EntityBase)(part.ParentGroup);
            else
                return null;
        }

        // Functions exposed to Python!
        // *********************************
        public bool SetScriptRunner(string vId)
        {
            uint id = System.Convert.ToUInt32(vId, 10);

            SceneObjectPart tempobj = myScriptEngine.World.GetSceneObjectPart(id);
            if (tempobj != null)
            {
                m_host = tempobj;
                m_localID = tempobj.LocalID;
                m_itemID = tempobj.UUID;
                return true;
            }
            else
                return false;
        }

        public void CommandToClient(string vPresenceId, string vUnit, string vCommand, string vCmdParams)
        {
            LLUUID TempId = new LLUUID(vPresenceId);
            ScenePresence temppre = myScriptEngine.World.GetScenePresence(TempId);
            if (temppre != null)
                temppre.ControllingClient.SendRexScriptCommand(vUnit,vCommand,vCmdParams);
        }

        public bool GetPhysics(string vId)
        {
            SceneObjectPart tempobj = myScriptEngine.World.GetSceneObjectPart(System.Convert.ToUInt32(vId, 10));
            if (tempobj != null)
                return ((tempobj.ObjectFlags & (uint)LLObject.ObjectFlags.Physics) != 0);
            else
                return false;
        }

        public void SetPhysics(string vId, bool vbUsePhysics)
        {
            SceneObjectPart tempobj = myScriptEngine.World.GetSceneObjectPart(System.Convert.ToUInt32(vId, 10));
            if (tempobj != null)
            {
                if (vbUsePhysics)
                    tempobj.AddFlag(LLObject.ObjectFlags.Physics);
                else
                    tempobj.RemFlag(LLObject.ObjectFlags.Physics);

                tempobj.DoPhysicsPropertyUpdate(vbUsePhysics, false);
                tempobj.ScheduleFullUpdate();
            }
            else
                myScriptEngine.Log.Verbose("PythonScript", "SetPhysics for nonexisting object:" + vId);     
        }

 
         
        public void SetMass(string vId, float vMass)
        {   
            SceneObjectPart tempobj = myScriptEngine.World.GetSceneObjectPart(System.Convert.ToUInt32(vId, 10));
            if (tempobj != null)
                tempobj.SetMass(vMass);
            else
                myScriptEngine.Log.Verbose("PythonScript", "SetMass for nonexisting object:" + vId); 
            
        }

        public void SetVelocity(string vId, LSL_Types.Vector3 vVelocity)
        {
            SceneObjectPart tempobj = myScriptEngine.World.GetSceneObjectPart(System.Convert.ToUInt32(vId, 10));
            if (((SceneObjectPart)tempobj) != null)
            {
                LLVector3 tempvel = new LLVector3((float)vVelocity.x, (float)vVelocity.y, (float)vVelocity.z);
                tempobj.Velocity = tempvel;
            }
        }

        public bool GetUsePrimVolumeCollision(string vId)
        {
            SceneObjectPart tempobj = myScriptEngine.World.GetSceneObjectPart(System.Convert.ToUInt32(vId, 10));
            if (tempobj != null)
                return tempobj.GetUsePrimVolumeCollision();
            else
                return false;
        }

        public void SetUsePrimVolumeCollision(string vId, bool vUseVolumeCollision)
        {
            SceneObjectPart tempobj = myScriptEngine.World.GetSceneObjectPart(System.Convert.ToUInt32(vId, 10));
            if (tempobj != null)
                tempobj.SetUsePrimVolumeCollision(vUseVolumeCollision);
            else
                myScriptEngine.Log.Verbose("PythonScript", "SetPrimVolumeCollision for nonexisting object:" + vId);
        }








        // text messaging
        // ******************************
        public void SendGeneralAlertAll(string vId, string vMessage)
        {
            myScriptEngine.World.SendGeneralAlert(vMessage);
        }

        public void SendAlertToAvatar(string vId,string vPresenceId, string vMessage, bool vbModal)
        {
            LLUUID TempId = new LLUUID(vPresenceId);
            ScenePresence temppre = myScriptEngine.World.GetScenePresence(TempId);
            if (temppre != null)
            {
                temppre.ControllingClient.SendAgentAlertMessage(vMessage, vbModal);
            }
        }



        // Actor finding.
        public List<string> GetRadiusActors(string vId,float vRadius)
        {
            List<string> TempList = new List<string>();
            EntityBase tempobj = GetEntityBase(System.Convert.ToUInt32(vId, 10));
            if (tempobj != null)
            {
                List<EntityBase> EntitiesList = myScriptEngine.World.GetEntities();
                foreach (EntityBase ent in EntitiesList) 
                {
                    if (ent is SceneObjectGroup || ent is ScenePresence)
                    {
                        if (Util.GetDistanceTo(ent.AbsolutePosition, tempobj.AbsolutePosition) < vRadius)
                            TempList.Add(ent.LocalId.ToString());
                    }
                }
            }
            return TempList;
        }

        public List<string> GetRadiusAvatars(string vId, float vRadius)
        {
            List<string> TempList = new List<string>();
            EntityBase tempobj = GetEntityBase(System.Convert.ToUInt32(vId, 10));
            if (tempobj != null)
            {
                List<EntityBase> EntitiesList = myScriptEngine.World.GetEntities();
                foreach (EntityBase ent in EntitiesList) 
                {
                    if (ent is ScenePresence)
                    {
                        if (Util.GetDistanceTo(ent.AbsolutePosition, tempobj.AbsolutePosition) < vRadius)
                            TempList.Add(ent.LocalId.ToString());
                    }
                }
            }
            return TempList;
        }



        public string SpawnActor(LSL_Types.Vector3 vLoc, int vShape, bool vbTemporary, string vPyClass)
        {
            LLUUID TempID = myScriptEngine.World.RegionInfo.MasterAvatarAssignedUUID;
            LLVector3 pos = new LLVector3((float)vLoc.x, (float)vLoc.y, (float)vLoc.z);
            LLQuaternion rot = new LLQuaternion(0.0f, 0.0f, 0.0f, 1.0f);

            uint AddResult = myScriptEngine.World.AddNewPrimReturningId(TempID, pos, rot, GetShape(vShape),vbTemporary,vPyClass);
            return AddResult.ToString();
        }

        public bool DestroyActor(string vId)
        {
           
            EntityBase tempobj = GetEntityBase(System.Convert.ToUInt32(vId, 10));
            if (((SceneObjectGroup)tempobj) != null)
            {
                ((SceneObjectGroup)tempobj).DeleteMe = true; // Do not call DeleteSceneObjectGroup for deleting directly
                return true;
            }
            else
                return false;
        }

        public bool SetMesh(string vId,string vsName)
        {
            try
            {
                SceneObjectPart tempobj = myScriptEngine.World.GetSceneObjectPart(System.Convert.ToUInt32(vId, 10));
                if (tempobj != null)
                {
                    if (vsName.Length > 0)
                    {
                        LLUUID tempid = myScriptEngine.World.AssetCache.ExistsAsset(43, vsName);
                        if (tempid != LLUUID.Zero)
                        {
                            tempobj.m_RexFlags |= SceneObjectPart.REXFLAGS_ISMESH | SceneObjectPart.REXFLAGS_ISVISIBLE;
                            tempobj.m_RexMeshUUID = tempid;
                            tempobj.UpdateRexParameters();
                            return true;
                        }
                    }
                    else
                    {
                        tempobj.m_RexFlags &= SceneObjectPart.REXFLAGS_ISMESH & SceneObjectPart.REXFLAGS_ISVISIBLE;
                        tempobj.UpdateRexParameters();
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                myScriptEngine.Log.Verbose("RexScriptEngine", "SetMeshByLLUUID exception:" + e.ToString());
            }
            return false;
        }

        public bool SetMeshByLLUUID(string vId, string vsLLUUID)
        {
            try
            {
                SceneObjectPart tempobj = myScriptEngine.World.GetSceneObjectPart(System.Convert.ToUInt32(vId, 10));
                if (tempobj != null)
                {
                    if (vsLLUUID.Length > 0)
                    {
                        tempobj.m_RexFlags |= SceneObjectPart.REXFLAGS_ISMESH | SceneObjectPart.REXFLAGS_ISVISIBLE;
                        tempobj.m_RexMeshUUID = new LLUUID(vsLLUUID);
                        tempobj.UpdateRexParameters();
                        return true;
                    }
                    else
                    {
                        tempobj.m_RexFlags &= SceneObjectPart.REXFLAGS_ISMESH & SceneObjectPart.REXFLAGS_ISVISIBLE;
                        tempobj.UpdateRexParameters();
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                myScriptEngine.Log.Verbose("RexScriptEngine", "SetMeshByLLUUID exception:" + e.ToString());
            }
            return false;
        }


        public bool SetMaterial(string vId,int vIndex,string vsName)
        {
            try
            {
                SceneObjectPart tempobj = myScriptEngine.World.GetSceneObjectPart(System.Convert.ToUInt32(vId, 10));
                if (tempobj != null)
                {
                    if (vsName.Length > 0)
                    {
                        LLUUID tempid = myScriptEngine.World.AssetCache.ExistsAsset(0, vsName);
                        if (tempid != LLUUID.Zero)
                        {
                            if (vIndex < tempobj.m_RexMaterialUUID.Count)
                            {
                                tempobj.m_RexMaterialUUID[vIndex] = tempid;
                            }
                            else
                            {
                                for (int i = tempobj.m_RexMaterialUUID.Count; i < (vIndex + 1); i++)
                                    tempobj.m_RexMaterialUUID.Add(LLUUID.Zero);

                                tempobj.m_RexMaterialUUID[vIndex] = tempid;
                            }
                            tempobj.UpdateRexParameters();
                            return true;
                        }
                    }
                    else
                    {
                        tempobj.UpdateRexParameters();
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                myScriptEngine.Log.Verbose("RexScriptEngine", "SetMaterial exception:" + e.ToString());
            }
            return false;
        }

  


        private static PrimitiveBaseShape GetShape(int vShape)
        {
            PrimitiveBaseShape shape = new PrimitiveBaseShape();
            
            shape.PCode = 9;
            shape.PathBegin = 0;
            shape.PathEnd = 0;
            shape.PathScaleX = 100;
            shape.PathScaleY = 100;
            shape.PathShearX = 0;
            shape.PathShearY = 0;
            shape.PathSkew = 0;
            shape.ProfileBegin = 0;
            shape.ProfileEnd = 0;
            shape.Scale.X = shape.Scale.Y = shape.Scale.Z = 0.5f;
            shape.PathCurve = 16;
            shape.ProfileCurve = 1;
            shape.ProfileHollow = 0;
            shape.PathRadiusOffset = 0;
            shape.PathRevolutions = 0;
            shape.PathTaperX = 0;
            shape.PathTaperY = 0;
            shape.PathTwist = 0;
            shape.PathTwistBegin = 0;
            LLObject.TextureEntry ntex = new LLObject.TextureEntry(new LLUUID("00000000-0000-1111-9999-000000000005"));
            shape.TextureEntry = ntex.ToBytes(); 
            return shape;
        }








        // Scenepresence related
        public string SPGetFullName(string vPresenceId)
        {
            LLUUID TempId = new LLUUID(vPresenceId);
            ScenePresence temppre = myScriptEngine.World.GetScenePresence(TempId);
            if (temppre != null)
            {
                string TempString = temppre.Firstname + " " + temppre.Lastname;
                return TempString;
            }
            else
                return "";
        }
        public string SPGetFirstName(string vPresenceId)
        {
            LLUUID TempId = new LLUUID(vPresenceId);
            ScenePresence temppre = myScriptEngine.World.GetScenePresence(TempId);
            if (temppre != null)
                return temppre.Firstname;           
            else
                return "";
        }
        public string SPGetLastName(string vPresenceId)
        {
            LLUUID TempId = new LLUUID(vPresenceId);
            ScenePresence temppre = myScriptEngine.World.GetScenePresence(TempId);
            if (temppre != null)
                return temppre.Lastname;
            else
                return "";
        }

        public void SPDoLocalTeleport(string vPresenceId, LSL_Types.Vector3 vLocation)
        {
            LLUUID TempId = new LLUUID(vPresenceId);
            ScenePresence temppre = myScriptEngine.World.GetScenePresence(TempId);
            if (temppre != null)
            {
                LLVector3 position = new LLVector3((float)vLocation.x, (float)vLocation.y, (float)vLocation.z);
                LLVector3 lookAt = new LLVector3(0,0,0);
                temppre.ControllingClient.SendTeleportLocationStart();
                temppre.ControllingClient.SendLocalTeleport(position, lookAt,0);
                temppre.Teleport(position);
            }
        }

        public float SPGetMovementModifier(string vPresenceId)
        {
            LLUUID TempId = new LLUUID(vPresenceId);
            ScenePresence temppre = myScriptEngine.World.GetScenePresence(TempId);
            if (temppre != null)
                return temppre.MovementSpeedMod;
            else
                return 0.0f;
        }

        public void SPSetMovementModifier(string vPresenceId,float vSpeedModifier)
        {
         
            LLUUID TempId = new LLUUID(vPresenceId);
            ScenePresence temppre = myScriptEngine.World.GetScenePresence(TempId);
            if (temppre != null)
                temppre.MovementSpeedMod = vSpeedModifier;    
        }

        public LSL_Types.Vector3 SPGetPos(string vPresenceId)
        {
            LSL_Types.Vector3 loc = new LSL_Types.Vector3(0, 0, 0);

            LLUUID TempId = new LLUUID(vPresenceId);
            ScenePresence temppre = myScriptEngine.World.GetScenePresence(TempId);
            if (temppre != null)
            {
                loc.x = temppre.AbsolutePosition.X;
                loc.y = temppre.AbsolutePosition.Y;
                loc.z = temppre.AbsolutePosition.Z;
            }
            return loc;
        }

        public LSL_Types.Quaternion SPGetRot(string vPresenceId)
        {
            LSL_Types.Quaternion rot = new LSL_Types.Quaternion(0, 0, 0, 1);

            LLUUID TempId = new LLUUID(vPresenceId);
            ScenePresence temppre = myScriptEngine.World.GetScenePresence(TempId);
            if (temppre != null)
            {
                rot.x = temppre.Rotation.x;
                rot.y = temppre.Rotation.y;
                rot.z = temppre.Rotation.y;
                rot.s = temppre.Rotation.w;
            }
            return rot;
        }

        public void SPSetRot(string vPresenceId,LSL_Types.Quaternion vRot, bool vbRelative)
        {
            LLUUID TempId = new LLUUID(vPresenceId);
            ScenePresence temppre = myScriptEngine.World.GetScenePresence(TempId);
            if (temppre != null)
            {
                string sparams = vRot.x.ToString() + " " + vRot.y.ToString() + " " + vRot.z.ToString() + " " + vRot.s.ToString();
                sparams = sparams.Replace(",", ".");
                if (vbRelative)
                    temppre.ControllingClient.SendRexScriptCommand("client", "setrelrot", sparams);
                else
                    temppre.ControllingClient.SendRexScriptCommand("client", "setrot", sparams);
            }    
        }

        

        // Functions not supported at the moment.
        /*  
        public bool GetFreezed(string vId)
        {
             tucofixme
            EntityBase tempobj = GetEntityBase(System.Convert.ToUInt32(vId, 10));
            if (tempobj != null)
            {
                return tempobj.IsFreezed;
            }
            else
            {
                return false;
            }
             
            return false;
        }

        public void SetFreezed(string vId, bool vbFreeze)
        {
             tucofixme 
            EntityBase tempobj = GetEntityBase(System.Convert.ToUInt32(vId, 10));
            if (tempobj != null)
            {
                tempobj.IsFreezed = vbFreeze;
                if (tempobj is ScenePresence && vbFreeze)
                    ((ScenePresence)tempobj).rxStopAvatarMovement();
            }
             
        } */

        /* 
        public int GetPhysicsMode(string vId)
        {
            // tucofixme
            SceneObjectPart tempobj = myScriptEngine.World.GetSceneObjectPart(System.Convert.ToUInt32(vId, 10));
            if (tempobj != null)
                return tempobj.GetPhysicsMode();
            else
                return 0;
             
            return 0;
        }

        public void SetPhysicsMode(string vId, int vPhysicsMode)
        {
            //  tucofixme
            SceneObjectPart tempobj = myScriptEngine.World.GetSceneObjectPart(System.Convert.ToUInt32(vId, 10));
            if (tempobj != null)
            {
                tempobj.SetPhysicsMode(vPhysicsMode);
            }
            else
                myScriptEngine.Log.Verbose("PythonScript", "SetPhysicsMode for nonexisting object:" + vId); 
        }
        */


        /* 
        public bool GetUseGravity(string vId)
        {
            // tucofixme
            SceneObjectPart tempobj = myScriptEngine.World.GetSceneObjectPart(System.Convert.ToUInt32(vId, 10));
            if (tempobj != null)
                return tempobj.GetUseGravity();
            else
                return false;
            
            return false;
        }

        public void SetUseGravity(string vId, bool vbUseGravity)
        {
            //  tucofixme
            SceneObjectPart tempobj = myScriptEngine.World.GetSceneObjectPart(System.Convert.ToUInt32(vId, 10));
            if (tempobj != null)
                tempobj.SetUseGravity(vbUseGravity);
            else
                myScriptEngine.Log.Verbose("PythonScript", "SetUseGravity for nonexisting object:" + vId);     
        }
        */

        /* 
        public void SetLocationFast(string vId,rxVector vLoc)
        {
            EntityBase tempobj = GetEntityBase(System.Convert.ToUInt32(vId, 10));
            if (((SceneObjectGroup)tempobj) != null)
            {
                bool hasPrim = ((SceneObjectGroup)tempobj).HasChildPrim(tempobj.UUID);
                if (hasPrim != false)
                {
                    LLVector3 TempLoc = new LLVector3((float)vLoc.x, (float)vLoc.y, (float)vLoc.z);
                    LLVector3 TempOffset = new LLVector3(0, 0, 0);
                    ((SceneObjectGroup)tempobj).GrabMovement(TempOffset, TempLoc, null); // tucofixme, might break some day, because sending null remoteClient parameter
                }
            }
        }
        */
    }
}










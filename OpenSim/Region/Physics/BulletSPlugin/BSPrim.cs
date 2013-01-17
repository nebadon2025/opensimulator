/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyrightD
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Xml;
using log4net;
using OMV = OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using OpenSim.Region.Physics.ConvexDecompositionDotNet;

namespace OpenSim.Region.Physics.BulletSPlugin
{
    [Serializable]
public sealed class BSPrim : BSPhysObject
{
    private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly string LogHeader = "[BULLETS PRIM]";

<<<<<<< HEAD
    private IMesh _mesh;
    private PrimitiveBaseShape _pbs;
    private ShapeData.PhysicsShapeType _shapeType;
    private ulong _meshKey;
    private ulong _hullKey;
    private List<ConvexResult> _hulls;

    private BSScene _scene;
    public BSScene Scene { get { return _scene; } }
    private String _avName;
    private uint _localID = 0;

    // _size is what the user passed. _scale is what we pass to the physics engine with the mesh.
    // Often _scale is unity because the meshmerizer will apply _size when creating the mesh.
=======
    // _size is what the user passed. Scale is what we pass to the physics engine with the mesh.
>>>>>>> upstream/master
    private OMV.Vector3 _size;  // the multiplier for each mesh dimension as passed by the user
    private OMV.Vector3 _scale; // the multiplier for each mesh dimension for the mesh as created by the meshmerizer

    private bool _stopped;
    private bool _grabbed;
    private bool _isSelected;
    private bool _isVolumeDetect;

    // _position is what the simulator thinks the positions of the prim is.
    private OMV.Vector3 _position;

    private float _mass;    // the mass of this object
    private float _density;
    private OMV.Vector3 _force;
    private OMV.Vector3 _velocity;
    private OMV.Vector3 _torque;
    private float _collisionScore;
    private OMV.Vector3 _acceleration;
    private OMV.Quaternion _orientation;
    private int _physicsActorType;
    private bool _isPhysical;
    private bool _flying;
    private float _friction;
    private float _restitution;
    private bool _setAlwaysRun;
    private bool _throttleUpdates;
    private bool _floatOnWater;
    private OMV.Vector3 _rotationalVelocity;
    private bool _kinematic;
    private float _buoyancy;

    // Membership in a linkset is controlled by this class.
    public override BSLinkset Linkset { get; set; }

    private int _subscribedEventsMs = 0;
    private int _nextCollisionOkTime = 0;
    long _collidingStep;
    long _collidingGroundStep;
    CollisionFlags m_currentCollisionFlags = 0;

    public override BulletBody BSBody { get; set; }
    public override BulletShape BSShape { get; set; }

    private BSDynamics _vehicle;

    private BSVMotor _targetMotor;
    private OMV.Vector3 _PIDTarget;
    private float _PIDTau;

    private BSFMotor _hoverMotor;
    private float _PIDHoverHeight;
    private PIDHoverType _PIDHoverType;
    private float _PIDHoverTau;

    public BSPrim(uint localID, String primName, BSScene parent_scene, OMV.Vector3 pos, OMV.Vector3 size,
                       OMV.Quaternion rotation, PrimitiveBaseShape pbs, bool pisPhysical)
    {
        // m_log.DebugFormat("{0}: BSPrim creation of {1}, id={2}", LogHeader, primName, localID);
        _localID = localID;
        _avName = primName;
        _physicsActorType = (int)ActorTypes.Prim;
        _scene = parent_scene;
        _position = pos;
        _size = size;
<<<<<<< HEAD
        _scale = new OMV.Vector3(1f, 1f, 1f);   // the scale will be set by CreateGeom depending on object type
=======
        Scale = size;   // prims are the size the user wants them to be (different for BSCharactes).
>>>>>>> upstream/master
        _orientation = rotation;
        _buoyancy = 0f;
        _velocity = OMV.Vector3.Zero;
        _rotationalVelocity = OMV.Vector3.Zero;
        _hullKey = 0;
        _meshKey = 0;
        _pbs = pbs;
        _isPhysical = pisPhysical;
        _isVolumeDetect = false;
<<<<<<< HEAD
        _subscribedEventsMs = 0;
        _friction = _scene.Params.defaultFriction;  // TODO: compute based on object material
        _density = _scene.Params.defaultDensity;    // TODO: compute based on object material
        _restitution = _scene.Params.defaultRestitution;
        Linkset = new BSLinkset(Scene, this);     // a linkset of one
        _vehicle = new BSDynamics(Scene, this);            // add vehicleness
        _mass = CalculateMass();
=======

        // Someday set default attributes based on the material but, for now, we don't know the prim material yet.
        // MaterialAttributes primMat = BSMaterials.GetAttributes(Material, pisPhysical);
        _density = PhysicsScene.Params.defaultDensity;
        _friction = PhysicsScene.Params.defaultFriction;
        _restitution = PhysicsScene.Params.defaultRestitution;

        _vehicle = new BSDynamics(PhysicsScene, this);            // add vehicleness

        _mass = CalculateMass();

        // Cause linkset variables to be initialized (like mass)
        Linkset.Refresh(this);

>>>>>>> upstream/master
        DetailLog("{0},BSPrim.constructor,call", LocalID);
        // do the actual object creation at taint time
        _scene.TaintedObject("BSPrim.create", delegate()
        {
            CreateGeomAndObject(true);

<<<<<<< HEAD
            // Get the pointer to the physical body for this object.
            // At the moment, we're still letting BulletSim manage the creation and destruction
            //    of the object. Someday we'll move that into the C# code.
            BSBody = new BulletBody(LocalID, BulletSimAPI.GetBodyHandle2(_scene.World.Ptr, LocalID));
            BSShape = new BulletShape(BulletSimAPI.GetCollisionShape2(BSBody.Ptr));
            m_currentCollisionFlags = BulletSimAPI.GetCollisionFlags2(BSBody.Ptr);
=======
            CurrentCollisionFlags = PhysicsScene.PE.GetCollisionFlags(PhysBody);
>>>>>>> upstream/master
        });
    }

    // called when this prim is being destroyed and we should free all the resources
    public override void Destroy()
    {
        // m_log.DebugFormat("{0}: Destroy, id={1}", LogHeader, LocalID);
        base.Destroy();

        // Undo any links between me and any other object
        BSPhysObject parentBefore = Linkset.LinksetRoot;
        int childrenBefore = Linkset.NumberOfChildren;

        Linkset = Linkset.RemoveMeFromLinkset(this);

        DetailLog("{0},BSPrim.Destroy,call,parentBefore={1},childrenBefore={2},parentAfter={3},childrenAfter={4}",
            LocalID, parentBefore.LocalID, childrenBefore, Linkset.LinksetRoot.LocalID, Linkset.NumberOfChildren);

        // Undo any vehicle properties
        this.VehicleType = (int)Vehicle.TYPE_NONE;

        _scene.TaintedObject("BSPrim.destroy", delegate()
        {
            DetailLog("{0},BSPrim.Destroy,taint,", LocalID);
<<<<<<< HEAD
            // everything in the C# world will get garbage collected. Tell the C++ world to free stuff.
            BulletSimAPI.DestroyObject(_scene.WorldID, LocalID);
=======
            // If there are physical body and shape, release my use of same.
            PhysicsScene.Shapes.DereferenceBody(PhysBody, true, null);
            PhysBody.Clear();
            PhysicsScene.Shapes.DereferenceShape(PhysShape, true, null);
            PhysShape.Clear();
>>>>>>> upstream/master
        });
    }
    
    public override bool Stopped { 
        get { return _stopped; } 
    }
    public override OMV.Vector3 Size { 
        get { return _size; } 
        set {
            _size = value;
<<<<<<< HEAD
            _scene.TaintedObject("BSPrim.setSize", delegate()
            {
                _mass = CalculateMass();   // changing size changes the mass
                // Since _size changed, the mesh needs to be rebuilt. If rebuilt, all the correct
                //   scale and margins are set.
                CreateGeomAndObject(true);
                DetailLog("{0}: BSPrim.setSize: size={1}, scale={2}, mass={3}, physical={4}", LocalID, _size, _scale, _mass, IsPhysical);
            });
        } 
    }
    public override PrimitiveBaseShape Shape { 
        set {
            _pbs = value;
            _scene.TaintedObject("BSPrim.setShape", delegate()
            {
                _mass = CalculateMass();   // changing the shape changes the mass
                CreateGeomAndObject(false);
            });
        } 
    }
    public override uint LocalID { 
        set { _localID = value; }
        get { return _localID; }
=======
            Scale = _size;
            ForceBodyShapeRebuild(false);
        }
    }

    public override PrimitiveBaseShape Shape {
        set {
            BaseShape = value;
            LastAssetBuildFailed = false;
            ForceBodyShapeRebuild(false);
        }
    }
    // Whatever the linkset wants is what I want.
    public override BSPhysicsShapeType PreferredPhysicalShape
        { get { return Linkset.PreferredPhysicalShape(this); } }

    public override bool ForceBodyShapeRebuild(bool inTaintTime)
    {
        PhysicsScene.TaintedObject(inTaintTime, "BSPrim.ForceBodyShapeRebuild", delegate()
        {
            _mass = CalculateMass();   // changing the shape changes the mass
            CreateGeomAndObject(true);
        });
        return true;
>>>>>>> upstream/master
    }
    public override bool Grabbed { 
        set { _grabbed = value; 
        } 
    }
<<<<<<< HEAD
    public override bool Selected { 
        set {
            _isSelected = value;
            _scene.TaintedObject("BSPrim.setSelected", delegate()
            {
                SetObjectDynamic();
            });
        } 
=======
    public override bool Selected {
        set
        {
            if (value != _isSelected)
            {
                _isSelected = value;
                PhysicsScene.TaintedObject("BSPrim.setSelected", delegate()
                {
                    DetailLog("{0},BSPrim.selected,taint,selected={1}", LocalID, _isSelected);
                    SetObjectDynamic(false);
                });
            }
        }
>>>>>>> upstream/master
    }
    public override bool IsSelected
    {
        get { return _isSelected; }
    }
    public override void CrossingFailure() { return; }

    // link me to the specified parent
    public override void link(PhysicsActor obj) {
        BSPrim parent = obj as BSPrim;
        if (parent != null)
        {
            BSPhysObject parentBefore = Linkset.LinksetRoot;
            int childrenBefore = Linkset.NumberOfChildren;

            Linkset = parent.Linkset.AddMeToLinkset(this);

            DetailLog("{0},BSPrim.link,call,parentBefore={1}, childrenBefore=={2}, parentAfter={3}, childrenAfter={4}", 
                LocalID, parentBefore.LocalID, childrenBefore, Linkset.LinksetRoot.LocalID, Linkset.NumberOfChildren);
        }
        return; 
    }

    // delink me from my linkset
    public override void delink() {
        // TODO: decide if this parent checking needs to happen at taint time
        // Race condition here: if link() and delink() in same simulation tick, the delink will not happen

        BSPhysObject parentBefore = Linkset.LinksetRoot;
        int childrenBefore = Linkset.NumberOfChildren;
        
        Linkset = Linkset.RemoveMeFromLinkset(this);

        DetailLog("{0},BSPrim.delink,parentBefore={1},childrenBefore={2},parentAfter={3},childrenAfter={4}, ", 
            LocalID, parentBefore.LocalID, childrenBefore, Linkset.LinksetRoot.LocalID, Linkset.NumberOfChildren);
        return; 
    }

    // Set motion values to zero.
    // Do it to the properties so the values get set in the physics engine.
    // Push the setting of the values to the viewer.
    // Called at taint time!
    public override void ZeroMotion()
    {
        _velocity = OMV.Vector3.Zero;
        _acceleration = OMV.Vector3.Zero;
        _rotationalVelocity = OMV.Vector3.Zero;

<<<<<<< HEAD
        // Zero some other properties directly into the physics engine
        BulletSimAPI.SetLinearVelocity2(BSBody.Ptr, OMV.Vector3.Zero);
        BulletSimAPI.SetAngularVelocity2(BSBody.Ptr, OMV.Vector3.Zero);
        BulletSimAPI.SetInterpolationVelocity2(BSBody.Ptr, OMV.Vector3.Zero, OMV.Vector3.Zero);
        BulletSimAPI.ClearForces2(BSBody.Ptr);
=======
        // Zero some other properties in the physics engine
        PhysicsScene.TaintedObject(inTaintTime, "BSPrim.ZeroMotion", delegate()
        {
            if (PhysBody.HasPhysicalBody)
                PhysicsScene.PE.ClearAllForces(PhysBody);
        });
    }
    public override void ZeroAngularMotion(bool inTaintTime)
    {
        _rotationalVelocity = OMV.Vector3.Zero;
        // Zero some other properties in the physics engine
        PhysicsScene.TaintedObject(inTaintTime, "BSPrim.ZeroMotion", delegate()
        {
            // DetailLog("{0},BSPrim.ZeroAngularMotion,call,rotVel={1}", LocalID, _rotationalVelocity);
            if (PhysBody.HasPhysicalBody)
            {
                PhysicsScene.PE.SetInterpolationAngularVelocity(PhysBody, _rotationalVelocity);
                PhysicsScene.PE.SetAngularVelocity(PhysBody, _rotationalVelocity);
            }
        });
>>>>>>> upstream/master
    }

    public override void LockAngularMotion(OMV.Vector3 axis)
    { 
        DetailLog("{0},BSPrim.LockAngularMotion,call,axis={1}", LocalID, axis);
        return;
    }

<<<<<<< HEAD
    public override OMV.Vector3 Position { 
        get { 
            if (!Linkset.IsRoot(this))
                // child prims move around based on their parent. Need to get the latest location
                _position = BulletSimAPI.GetPosition2(BSBody.Ptr);

            // don't do the GetObjectPosition for root elements because this function is called a zillion times
            // _position = BulletSimAPI.GetObjectPosition(_scene.WorldID, _localID);
            return _position; 
        } 
        set {
            _position = value;
            // TODO: what does it mean to set the position of a child prim?? Rebuild the constraint?
            _scene.TaintedObject("BSPrim.setPosition", delegate()
            {
                DetailLog("{0},BSPrim.SetPosition,taint,pos={1},orient={2}", LocalID, _position, _orientation);
                BulletSimAPI.SetTranslation2(BSBody.Ptr, _position, _orientation);
            });
        } 
=======
    public override OMV.Vector3 RawPosition
    {
        get { return _position; }
        set { _position = value; }
    }
    public override OMV.Vector3 Position {
        get {
            /* NOTE: this refetch is not necessary. The simulator knows about linkset children
             *    and does not fetch this position info for children. Thus this is commented out.
            // child prims move around based on their parent. Need to get the latest location
            if (!Linkset.IsRoot(this))
                _position = Linkset.PositionGet(this);
            */

            // don't do the GetObjectPosition for root elements because this function is called a zillion times.
            // _position = PhysicsScene.PE.GetObjectPosition2(PhysicsScene.World, BSBody) - PositionDisplacement;
            return _position;
        }
        set {
            // If the position must be forced into the physics engine, use ForcePosition.
            // All positions are given in world positions.
            if (_position == value)
            {
                DetailLog("{0},BSPrim.setPosition,call,positionNotChanging,pos={1},orient={2}", LocalID, _position, _orientation);
                return;
            }
            _position = value;
            PositionSanityCheck(false);

            // A linkset might need to know if a component information changed.
            Linkset.UpdateProperties(this, false);

            PhysicsScene.TaintedObject("BSPrim.setPosition", delegate()
            {
                DetailLog("{0},BSPrim.SetPosition,taint,pos={1},orient={2}", LocalID, _position, _orientation);
                ForcePosition = _position;
            });
        }
    }
    public override OMV.Vector3 ForcePosition {
        get {
            _position = PhysicsScene.PE.GetPosition(PhysBody) - PositionDisplacement;
            return _position;
        }
        set {
            _position = value;
            if (PhysBody.HasPhysicalBody)
            {
                PhysicsScene.PE.SetTranslation(PhysBody, _position + PositionDisplacement, _orientation);
                ActivateIfPhysical(false);
            }
        }
    }
    // Override to have position displacement immediately update the physical position.
    // A feeble attempt to keep the sim and physical positions in sync
    // Must be called at taint time.
    public override OMV.Vector3 PositionDisplacement
    {
        get
        {
            return base.PositionDisplacement;
        }
        set
        {
            base.PositionDisplacement = value;
            PhysicsScene.TaintedObject(PhysicsScene.InTaintTime, "BSPrim.setPosition", delegate()
            {
                if (PhysBody.HasPhysicalBody)
                    PhysicsScene.PE.SetTranslation(PhysBody, _position + base.PositionDisplacement, _orientation);
            });
        }
    }

    // Check that the current position is sane and, if not, modify the position to make it so.
    // Check for being below terrain and being out of bounds.
    // Returns 'true' of the position was made sane by some action.
    private bool PositionSanityCheck(bool inTaintTime)
    {
        bool ret = false;

        if (!PhysicsScene.TerrainManager.IsWithinKnownTerrain(RawPosition))
        {
            // The physical object is out of the known/simulated area.
            // Upper levels of code will handle the transition to other areas so, for
            //     the time, we just ignore the position.
            return ret;
        }

        float terrainHeight = PhysicsScene.TerrainManager.GetTerrainHeightAtXYZ(_position);
        OMV.Vector3 upForce = OMV.Vector3.Zero;
        if (RawPosition.Z < terrainHeight)
        {
            DetailLog("{0},BSPrim.PositionAdjustUnderGround,call,pos={1},terrain={2}", LocalID, _position, terrainHeight);
            float targetHeight = terrainHeight + (Size.Z / 2f);
            // If the object is below ground it just has to be moved up because pushing will
            //     not get it through the terrain
            _position.Z = targetHeight;
            if (inTaintTime)
                ForcePosition = _position;
            ret = true;
        }

        if ((CurrentCollisionFlags & CollisionFlags.BS_FLOATS_ON_WATER) != 0)
        {
            float waterHeight = PhysicsScene.TerrainManager.GetWaterLevelAtXYZ(_position);
            // TODO: a floating motor so object will bob in the water
            if (Math.Abs(RawPosition.Z - waterHeight) > 0.1f)
            {
                // Upforce proportional to the distance away from the water. Correct the error in 1 sec.
                upForce.Z = (waterHeight - RawPosition.Z) * 1f;

                // Apply upforce and overcome gravity.
                OMV.Vector3 correctionForce = upForce - PhysicsScene.DefaultGravity;
                DetailLog("{0},BSPrim.PositionSanityCheck,applyForce,pos={1},upForce={2},correctionForce={3}", LocalID, _position, upForce, correctionForce);
                AddForce(correctionForce, false, inTaintTime);
                ret = true;
            }
        }

        return ret;
>>>>>>> upstream/master
    }

    // Return the effective mass of the object.
        // The definition of this call is to return the mass of the prim.
        // If the simulator cares about the mass of the linkset, it will sum it itself.
    public override float Mass
    { 
        get
        {
<<<<<<< HEAD
            // return Linkset.LinksetMass;
=======
>>>>>>> upstream/master
            return _mass;
        }
    }

    // used when we only want this prim's mass and not the linkset thing
<<<<<<< HEAD
    public override float MassRaw { get { return _mass; }  }
=======
    public override float RawMass { 
        get { return _mass; }
    }
    // Set the physical mass to the passed mass.
    // Note that this does not change _mass!
    public override void UpdatePhysicalMassProperties(float physMass, bool inWorld)
    {
        if (PhysBody.HasPhysicalBody)
        {
            if (IsStatic)
            {
                PhysicsScene.PE.SetGravity(PhysBody, PhysicsScene.DefaultGravity);
                Inertia = OMV.Vector3.Zero;
                PhysicsScene.PE.SetMassProps(PhysBody, 0f, Inertia);
                PhysicsScene.PE.UpdateInertiaTensor(PhysBody);
            }
            else
            {
                OMV.Vector3 grav = ComputeGravity(Buoyancy);

                if (inWorld)
                {
                    // Changing interesting properties doesn't change proxy and collision cache
                    //    information. The Bullet solution is to re-add the object to the world
                    //    after parameters are changed.
                    PhysicsScene.PE.RemoveObjectFromWorld(PhysicsScene.World, PhysBody);
                }

                // The computation of mass props requires gravity to be set on the object.
                PhysicsScene.PE.SetGravity(PhysBody, grav);

                Inertia = PhysicsScene.PE.CalculateLocalInertia(PhysShape, physMass);
                PhysicsScene.PE.SetMassProps(PhysBody, physMass, Inertia);
                PhysicsScene.PE.UpdateInertiaTensor(PhysBody);

                // center of mass is at the zero of the object
                // DEBUG DEBUG PhysicsScene.PE.SetCenterOfMassByPosRot(PhysBody, ForcePosition, ForceOrientation);
                DetailLog("{0},BSPrim.UpdateMassProperties,mass={1},localInertia={2},grav={3},inWorld={4}", LocalID, physMass, Inertia, grav, inWorld);

                if (inWorld)
                {
                    AddObjectToPhysicalWorld();
                }

                // Must set gravity after it has been added to the world because, for unknown reasons,
                //     adding the object resets the object's gravity to world gravity
                PhysicsScene.PE.SetGravity(PhysBody, grav);

            }
        }
    }
>>>>>>> upstream/master

    // Return what gravity should be set to this very moment
    public OMV.Vector3 ComputeGravity(float buoyancy)
    {
        OMV.Vector3 ret = PhysicsScene.DefaultGravity;

        if (!IsStatic)
            ret *= (1f - buoyancy);

        return ret;
    }

    // Is this used?
    public override OMV.Vector3 CenterOfMass
    {
        get { return Linkset.CenterOfMass; }
    }

    // Is this used?
    public override OMV.Vector3 GeometricCenter
    {
        get { return Linkset.GeometricCenter; }
    }

    public override OMV.Vector3 Force { 
        get { return _force; } 
        set {
            _force = value;
<<<<<<< HEAD
            _scene.TaintedObject("BSPrim.setForce", delegate()
            {
                DetailLog("{0},BSPrim.setForce,taint,force={1}", LocalID, _force);
                BulletSimAPI.SetObjectForce2(BSBody.Ptr, _force);
            });
        } 
=======
            if (_force != OMV.Vector3.Zero)
            {
                // If the force is non-zero, it must be reapplied each tick because
                //    Bullet clears the forces applied last frame.
                RegisterPreStepAction("BSPrim.setForce", LocalID,
                    delegate(float timeStep)
                    {
                        DetailLog("{0},BSPrim.setForce,preStep,force={1}", LocalID, _force);
                        if (PhysBody.HasPhysicalBody)
                        {
                            PhysicsScene.PE.ApplyCentralForce(PhysBody, _force);
                            ActivateIfPhysical(false);
                        }
                    }
                );
            }
            else
            {
                UnRegisterPreStepAction("BSPrim.setForce", LocalID);
            }
        }
>>>>>>> upstream/master
    }

    public override int VehicleType { 
        get {
            return (int)_vehicle.Type;   // if we are a vehicle, return that type
        } 
        set {
            Vehicle type = (Vehicle)value;
<<<<<<< HEAD
            BSPrim vehiclePrim = this;
            _scene.TaintedObject("setVehicleType", delegate()
=======

            PhysicsScene.TaintedObject("setVehicleType", delegate()
>>>>>>> upstream/master
            {
                // Done at taint time so we're sure the physics engine is not using the variables
                // Vehicle code changes the parameters for this vehicle type.
                _vehicle.ProcessTypeChange(type);
<<<<<<< HEAD
                // Tell the scene about the vehicle so it will get processing each frame.
                _scene.VehicleInSceneTypeChanged(this, type);
=======
                ActivateIfPhysical(false);

                // If an active vehicle, register the vehicle code to be called before each step
                if (_vehicle.Type == Vehicle.TYPE_NONE)
                    UnRegisterPreStepAction("BSPrim.Vehicle", LocalID);
                else
                    RegisterPreStepAction("BSPrim.Vehicle", LocalID, _vehicle.Step);
>>>>>>> upstream/master
            });
        } 
    }
    public override void VehicleFloatParam(int param, float value) 
    {
        _scene.TaintedObject("BSPrim.VehicleFloatParam", delegate()
        {
            _vehicle.ProcessFloatVehicleParam((Vehicle)param, value);
        });
    }
    public override void VehicleVectorParam(int param, OMV.Vector3 value) 
    {
        _scene.TaintedObject("BSPrim.VehicleVectorParam", delegate()
        {
            _vehicle.ProcessVectorVehicleParam((Vehicle)param, value);
        });
    }
    public override void VehicleRotationParam(int param, OMV.Quaternion rotation) 
    {
        _scene.TaintedObject("BSPrim.VehicleRotationParam", delegate()
        {
            _vehicle.ProcessRotationVehicleParam((Vehicle)param, rotation);
        });
    }
    public override void VehicleFlags(int param, bool remove) 
    {
        _scene.TaintedObject("BSPrim.VehicleFlags", delegate()
        {
            _vehicle.ProcessVehicleFlags(param, remove);
        });
    }

<<<<<<< HEAD
    // Called each simulation step to advance vehicle characteristics.
    // Called from Scene when doing simulation step so we're in taint processing time.
    public override void StepVehicle(float timeStep)
    {
        if (IsPhysical)
            _vehicle.Step(timeStep);
    }

=======
>>>>>>> upstream/master
    // Allows the detection of collisions with inherently non-physical prims. see llVolumeDetect for more
    public override void SetVolumeDetect(int param) {
        bool newValue = (param != 0);
        _isVolumeDetect = newValue;
        _scene.TaintedObject("BSPrim.SetVolumeDetect", delegate()
        {
            SetObjectDynamic();
        });
        return; 
    }
<<<<<<< HEAD

    public override OMV.Vector3 Velocity { 
        get { return _velocity; } 
=======
    public override OMV.Vector3 RawVelocity
    {
        get { return _velocity; }
        set { _velocity = value; }
    }
    public override OMV.Vector3 Velocity {
        get { return _velocity; }
>>>>>>> upstream/master
        set {
            _velocity = value;
            _scene.TaintedObject("BSPrim.setVelocity", delegate()
            {
<<<<<<< HEAD
                DetailLog("{0},BSPrim.SetVelocity,taint,vel={1}", LocalID, _velocity);
                BulletSimAPI.SetLinearVelocity2(BSBody.Ptr, _velocity);
=======
                // DetailLog("{0},BSPrim.SetVelocity,taint,vel={1}", LocalID, _velocity);
                ForceVelocity = _velocity;
>>>>>>> upstream/master
            });
        } 
    }
<<<<<<< HEAD
    public override OMV.Vector3 Torque { 
        get { return _torque; } 
        set { _torque = value; 
            DetailLog("{0},BSPrim.SetTorque,call,torque={1}", LocalID, _torque);
        } 
=======
    public override OMV.Vector3 ForceVelocity {
        get { return _velocity; }
        set {
            PhysicsScene.AssertInTaintTime("BSPrim.ForceVelocity");

            _velocity = value;
            if (PhysBody.HasPhysicalBody)
            {
                DetailLog("{0},BSPrim.ForceVelocity,taint,vel={1}", LocalID, _velocity);
                PhysicsScene.PE.SetLinearVelocity(PhysBody, _velocity);
                ActivateIfPhysical(false);
            }
        }
    }
    public override OMV.Vector3 Torque {
        get { return _torque; }
        set {
            _torque = value;
            if (_torque != OMV.Vector3.Zero)
            {
                // If the torque is non-zero, it must be reapplied each tick because
                //    Bullet clears the forces applied last frame.
                RegisterPreStepAction("BSPrim.setTorque", LocalID,
                    delegate(float timeStep)
                    {
                        if (PhysBody.HasPhysicalBody)
                            AddAngularForce(_torque, false, true);
                    }
                );
            }
            else
            {
                UnRegisterPreStepAction("BSPrim.setTorque", LocalID);
            }
            // DetailLog("{0},BSPrim.SetTorque,call,torque={1}", LocalID, _torque);
        }
>>>>>>> upstream/master
    }
    public override float CollisionScore { 
        get { return _collisionScore; } 
        set { _collisionScore = value; 
        } 
    }
    public override OMV.Vector3 Acceleration { 
        get { return _acceleration; }
        set { _acceleration = value; }
    }
    public override OMV.Quaternion Orientation { 
        get {
<<<<<<< HEAD
            if (!Linkset.IsRoot(this))
            {
                // Children move around because tied to parent. Get a fresh value.
                _orientation = BulletSimAPI.GetOrientation2(BSBody.Ptr);
=======
            /* NOTE: this refetch is not necessary. The simulator knows about linkset children
             *    and does not fetch this position info for children. Thus this is commented out.
            // Children move around because tied to parent. Get a fresh value.
            if (!Linkset.IsRoot(this))
            {
                _orientation = Linkset.OrientationGet(this);
>>>>>>> upstream/master
            }
             */
            return _orientation;
        } 
        set {
            _orientation = value;
<<<<<<< HEAD
            // TODO: what does it mean if a child in a linkset changes its orientation? Rebuild the constraint?
            _scene.TaintedObject("BSPrim.setOrientation", delegate()
            {
                // _position = BulletSimAPI.GetObjectPosition(_scene.WorldID, _localID);
                DetailLog("{0},BSPrim.setOrientation,taint,pos={1},orient={2}", LocalID, _position, _orientation);
                BulletSimAPI.SetTranslation2(BSBody.Ptr, _position, _orientation);
            });
        } 
=======

            // A linkset might need to know if a component information changed.
            Linkset.UpdateProperties(this, false);

            PhysicsScene.TaintedObject("BSPrim.setOrientation", delegate()
            {
                ForceOrientation = _orientation;
            });
        }
    }
    // Go directly to Bullet to get/set the value.
    public override OMV.Quaternion ForceOrientation
    {
        get
        {
            _orientation = PhysicsScene.PE.GetOrientation(PhysBody);
            return _orientation;
        }
        set
        {
            _orientation = value;
            if (PhysBody.HasPhysicalBody)
                PhysicsScene.PE.SetTranslation(PhysBody, _position + PositionDisplacement, _orientation);
        }
>>>>>>> upstream/master
    }
    public override int PhysicsActorType { 
        get { return _physicsActorType; } 
        set { _physicsActorType = value; } 
    }
    public override bool IsPhysical { 
        get { return _isPhysical; } 
        set {
            _isPhysical = value;
            _scene.TaintedObject("BSPrim.setIsPhysical", delegate()
            {
<<<<<<< HEAD
                SetObjectDynamic();
            });
        } 
=======
                _isPhysical = value;
                PhysicsScene.TaintedObject("BSPrim.setIsPhysical", delegate()
                {
                    DetailLog("{0},setIsPhysical,taint,isPhys={1}", LocalID, _isPhysical);
                    SetObjectDynamic(true);
                    // whether phys-to-static or static-to-phys, the object is not moving.
                    ZeroMotion(true);
                });
            }
        }
>>>>>>> upstream/master
    }

    // An object is static (does not move) if selected or not physical
    private bool IsStatic
    {
        get { return _isSelected || !IsPhysical; }
    }

    // An object is solid if it's not phantom and if it's not doing VolumeDetect
    private bool IsSolid
    {
        get { return !IsPhantom && !_isVolumeDetect; }
    }

    // Make gravity work if the object is physical and not selected
    // No locking here because only called when it is safe
    // There are four flags we're interested in:
    //     IsStatic: Object does not move, otherwise the object has mass and moves
    //     isSolid: other objects bounce off of this object
    //     isVolumeDetect: other objects pass through but can generate collisions
    //     collisionEvents: whether this object returns collision events
    private void SetObjectDynamic()
    {
        // If it's becoming dynamic, it will need hullness
        VerifyCorrectPhysicalShape();
        UpdatePhysicalParameters();
    }

    private void UpdatePhysicalParameters()
    {
        /*
        // Bullet wants static objects to have a mass of zero
        float mass = IsStatic ? 0f : _mass;

<<<<<<< HEAD
        BulletSimAPI.SetObjectProperties(_scene.WorldID, LocalID, IsStatic, IsSolid, SubscribedEvents(), mass);
         */
        BulletSimAPI.RemoveObjectFromWorld2(Scene.World.Ptr, BSBody.Ptr);
=======
        // Mangling all the physical properties requires the object not be in the physical world.
        // This is a NOOP if the object is not in the world (BulletSim and Bullet ignore objects not found).
        PhysicsScene.PE.RemoveObjectFromWorld(PhysicsScene.World, PhysBody);
>>>>>>> upstream/master

        // Set up the object physicalness (does gravity and collisions move this object)
        MakeDynamic(IsStatic);

        // Make solid or not (do things bounce off or pass through this object)
        MakeSolid(IsSolid);

<<<<<<< HEAD
        // Arrange for collisions events if the simulator wants them
        EnableCollisions(SubscribedEvents());

        BulletSimAPI.AddObjectToWorld2(Scene.World.Ptr, BSBody.Ptr);
=======
        AddObjectToPhysicalWorld();

        // Rebuild its shape
        PhysicsScene.PE.UpdateSingleAabb(PhysicsScene.World, PhysBody);
>>>>>>> upstream/master

        // Recompute any linkset parameters.
        // When going from non-physical to physical, this re-enables the constraints that
        //     had been automatically disabled when the mass was set to zero.
        Linkset.Refresh(this);

<<<<<<< HEAD
        DetailLog("{0},BSPrim.UpdatePhysicalParameters,taint,static={1},solid={2},mass={3}, cf={4}", 
                        LocalID, IsStatic, IsSolid, _mass, m_currentCollisionFlags);
=======
        DetailLog("{0},BSPrim.UpdatePhysicalParameters,taintExit,static={1},solid={2},mass={3},collide={4},cf={5:X},cType={6},body={7},shape={8}",
                        LocalID, IsStatic, IsSolid, Mass, SubscribedEvents(), CurrentCollisionFlags, PhysBody.collisionType, PhysBody, PhysShape);
>>>>>>> upstream/master
    }

    // "Making dynamic" means changing to and from static.
    // When static, gravity does not effect the object and it is fixed in space.
    // When dynamic, the object can fall and be pushed by others.
    // This is independent of its 'solidness' which controls what passes through
    //    this object and what interacts with it.
    private void MakeDynamic(bool makeStatic)
    {
        if (makeStatic)
        {
            // Become a Bullet 'static' object type
<<<<<<< HEAD
            m_currentCollisionFlags = BulletSimAPI.AddToCollisionFlags2(BSBody.Ptr, CollisionFlags.CF_STATIC_OBJECT);
            // Stop all movement
            BulletSimAPI.ClearAllForces2(BSBody.Ptr);
            // Center of mass is at the center of the object
            BulletSimAPI.SetCenterOfMassByPosRot2(Linkset.LinksetRoot.BSBody.Ptr, _position, _orientation);
            // Mass is zero which disables a bunch of physics stuff in Bullet
            BulletSimAPI.SetMassProps2(BSBody.Ptr, 0f, OMV.Vector3.Zero);
            // There is no inertia in a static object
            BulletSimAPI.UpdateInertiaTensor2(BSBody.Ptr);
            // There can be special things needed for implementing linksets
            Linkset.MakeStatic(this);
            // The activation state is 'sleeping' so Bullet will not try to act on it
            BulletSimAPI.ForceActivationState2(BSBody.Ptr, ActivationState.ISLAND_SLEEPING);
=======
            CurrentCollisionFlags = PhysicsScene.PE.AddToCollisionFlags(PhysBody, CollisionFlags.CF_STATIC_OBJECT);
            // Stop all movement
            ZeroMotion(true);

            // Set various physical properties so other object interact properly
            MaterialAttributes matAttrib = BSMaterials.GetAttributes(Material, false);
            PhysicsScene.PE.SetFriction(PhysBody, matAttrib.friction);
            PhysicsScene.PE.SetRestitution(PhysBody, matAttrib.restitution);

            // Mass is zero which disables a bunch of physics stuff in Bullet
            UpdatePhysicalMassProperties(0f, false);
            // Set collision detection parameters
            if (BSParam.CcdMotionThreshold > 0f)
            {
                PhysicsScene.PE.SetCcdMotionThreshold(PhysBody, BSParam.CcdMotionThreshold);
                PhysicsScene.PE.SetCcdSweptSphereRadius(PhysBody, BSParam.CcdSweptSphereRadius);
            }

            // The activation state is 'disabled' so Bullet will not try to act on it.
            // PhysicsScene.PE.ForceActivationState(PhysBody, ActivationState.DISABLE_SIMULATION);
            // Start it out sleeping and physical actions could wake it up.
            PhysicsScene.PE.ForceActivationState(PhysBody, ActivationState.ISLAND_SLEEPING);

            // This collides like a static object
            PhysBody.collisionType = CollisionType.Static;

            // There can be special things needed for implementing linksets
            Linkset.MakeStatic(this);
>>>>>>> upstream/master
        }
        else
        {
            // Not a Bullet static object
<<<<<<< HEAD
            m_currentCollisionFlags = BulletSimAPI.RemoveFromCollisionFlags2(BSBody.Ptr, CollisionFlags.CF_STATIC_OBJECT);
=======
            CurrentCollisionFlags = PhysicsScene.PE.RemoveFromCollisionFlags(PhysBody, CollisionFlags.CF_STATIC_OBJECT);

            // Set various physical properties so other object interact properly
            MaterialAttributes matAttrib = BSMaterials.GetAttributes(Material, true);
            PhysicsScene.PE.SetFriction(PhysBody, matAttrib.friction);
            PhysicsScene.PE.SetRestitution(PhysBody, matAttrib.restitution);
>>>>>>> upstream/master

            // Set various physical properties so internal things will get computed correctly as they are set
            BulletSimAPI.SetFriction2(BSBody.Ptr, Scene.Params.defaultFriction);
            BulletSimAPI.SetRestitution2(BSBody.Ptr, Scene.Params.defaultRestitution);
            // per http://www.bulletphysics.org/Bullet/phpBB3/viewtopic.php?t=3382
<<<<<<< HEAD
            BulletSimAPI.SetInterpolationLinearVelocity2(BSBody.Ptr, OMV.Vector3.Zero);
            BulletSimAPI.SetInterpolationAngularVelocity2(BSBody.Ptr, OMV.Vector3.Zero);
            BulletSimAPI.SetInterpolationVelocity2(BSBody.Ptr, OMV.Vector3.Zero, OMV.Vector3.Zero);

            // A dynamic object has mass
            IntPtr collisionShapePtr = BulletSimAPI.GetCollisionShape2(BSBody.Ptr);
            OMV.Vector3 inertia = BulletSimAPI.CalculateLocalInertia2(collisionShapePtr, Linkset.LinksetMass);
            BulletSimAPI.SetMassProps2(BSBody.Ptr, _mass, inertia);
            // Inertia is based on our new mass
            BulletSimAPI.UpdateInertiaTensor2(BSBody.Ptr);

            // Various values for simulation limits
            BulletSimAPI.SetDamping2(BSBody.Ptr, Scene.Params.linearDamping, Scene.Params.angularDamping);
            BulletSimAPI.SetDeactivationTime2(BSBody.Ptr, Scene.Params.deactivationTime);
            BulletSimAPI.SetSleepingThresholds2(BSBody.Ptr, Scene.Params.linearSleepingThreshold, Scene.Params.angularSleepingThreshold);
            BulletSimAPI.SetContactProcessingThreshold2(BSBody.Ptr, Scene.Params.contactProcessingThreshold);

            // There can be special things needed for implementing linksets
            Linkset.MakeDynamic(this);

            // Force activation of the object so Bullet will act on it.
            BulletSimAPI.Activate2(BSBody.Ptr, true);
=======
            // Since this can be called multiple times, only zero forces when becoming physical
            // PhysicsScene.PE.ClearAllForces(BSBody);

            // For good measure, make sure the transform is set through to the motion state
            PhysicsScene.PE.SetTranslation(PhysBody, _position + PositionDisplacement, _orientation);

            // Center of mass is at the center of the object
            // DEBUG DEBUG PhysicsScene.PE.SetCenterOfMassByPosRot(Linkset.LinksetRoot.PhysBody, _position, _orientation);

            // A dynamic object has mass
            UpdatePhysicalMassProperties(RawMass, false);

            // Set collision detection parameters
            if (BSParam.CcdMotionThreshold > 0f)
            {
                PhysicsScene.PE.SetCcdMotionThreshold(PhysBody, BSParam.CcdMotionThreshold);
                PhysicsScene.PE.SetCcdSweptSphereRadius(PhysBody, BSParam.CcdSweptSphereRadius);
            }

            // Various values for simulation limits
            PhysicsScene.PE.SetDamping(PhysBody, BSParam.LinearDamping, BSParam.AngularDamping);
            PhysicsScene.PE.SetDeactivationTime(PhysBody, BSParam.DeactivationTime);
            PhysicsScene.PE.SetSleepingThresholds(PhysBody, BSParam.LinearSleepingThreshold, BSParam.AngularSleepingThreshold);
            PhysicsScene.PE.SetContactProcessingThreshold(PhysBody, BSParam.ContactProcessingThreshold);

            // This collides like an object.
            PhysBody.collisionType = CollisionType.Dynamic;

            // Force activation of the object so Bullet will act on it.
            // Must do the ForceActivationState2() to overcome the DISABLE_SIMULATION from static objects.
            PhysicsScene.PE.ForceActivationState(PhysBody, ActivationState.ACTIVE_TAG);

            // There might be special things needed for implementing linksets.
            Linkset.MakeDynamic(this);
>>>>>>> upstream/master
        }
    }

    // "Making solid" means that other object will not pass through this object.
    private void MakeSolid(bool makeSolid)
    {
<<<<<<< HEAD
        if (makeSolid)
        {
            // Easy in Bullet -- just remove the object flag that controls collision response
            m_currentCollisionFlags = BulletSimAPI.RemoveFromCollisionFlags2(BSBody.Ptr, CollisionFlags.CF_NO_CONTACT_RESPONSE);
        }
        else
        {
            m_currentCollisionFlags = BulletSimAPI.AddToCollisionFlags2(BSBody.Ptr, CollisionFlags.CF_NO_CONTACT_RESPONSE);
        }
    }

=======
        CollisionObjectTypes bodyType = (CollisionObjectTypes)PhysicsScene.PE.GetBodyType(PhysBody);
        if (makeSolid)
        {
            // Verify the previous code created the correct shape for this type of thing.
            if ((bodyType & CollisionObjectTypes.CO_RIGID_BODY) == 0)
            {
                m_log.ErrorFormat("{0} MakeSolid: physical body of wrong type for solidity. id={1}, type={2}", LogHeader, LocalID, bodyType);
            }
            CurrentCollisionFlags = PhysicsScene.PE.RemoveFromCollisionFlags(PhysBody, CollisionFlags.CF_NO_CONTACT_RESPONSE);
        }
        else
        {
            if ((bodyType & CollisionObjectTypes.CO_GHOST_OBJECT) == 0)
            {
                m_log.ErrorFormat("{0} MakeSolid: physical body of wrong type for non-solidness. id={1}, type={2}", LogHeader, LocalID, bodyType);
            }
            CurrentCollisionFlags = PhysicsScene.PE.AddToCollisionFlags(PhysBody, CollisionFlags.CF_NO_CONTACT_RESPONSE);

            // Change collision info from a static object to a ghosty collision object
            PhysBody.collisionType = CollisionType.VolumeDetect;
        }
    }

    // Enable physical actions. Bullet will keep sleeping non-moving physical objects so
    //     they need waking up when parameters are changed.
    // Called in taint-time!!
    private void ActivateIfPhysical(bool forceIt)
    {
        if (IsPhysical && PhysBody.HasPhysicalBody)
            PhysicsScene.PE.Activate(PhysBody, forceIt);
    }

>>>>>>> upstream/master
    // Turn on or off the flag controlling whether collision events are returned to the simulator.
    private void EnableCollisions(bool wantsCollisionEvents)
    {
        if (wantsCollisionEvents)
        {
<<<<<<< HEAD
            m_currentCollisionFlags = BulletSimAPI.AddToCollisionFlags2(BSBody.Ptr, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
        }
        else
        {
            m_currentCollisionFlags = BulletSimAPI.RemoveFromCollisionFlags2(BSBody.Ptr, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
=======
            CurrentCollisionFlags = PhysicsScene.PE.AddToCollisionFlags(PhysBody, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
        }
        else
        {
            CurrentCollisionFlags = PhysicsScene.PE.RemoveFromCollisionFlags(PhysBody, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
        }
    }

    // Add me to the physical world.
    // Object MUST NOT already be in the world.
    // This routine exists because some assorted properties get mangled by adding to the world.
    internal void AddObjectToPhysicalWorld()
    {
        if (PhysBody.HasPhysicalBody)
        {
            PhysicsScene.PE.AddObjectToWorld(PhysicsScene.World, PhysBody);
        }
        else
        {
            m_log.ErrorFormat("{0} Attempt to add physical object without body. id={1}", LogHeader, LocalID);
            DetailLog("{0},BSPrim.UpdatePhysicalParameters,addObjectWithoutBody,cType={1}", LocalID, PhysBody.collisionType);
>>>>>>> upstream/master
        }
    }

    // prims don't fly
    public override bool Flying { 
        get { return _flying; } 
        set {
            _flying = value;
        } 
    }
    public override bool SetAlwaysRun { 
        get { return _setAlwaysRun; } 
        set { _setAlwaysRun = value; } 
    }
    public override bool ThrottleUpdates { 
        get { return _throttleUpdates; } 
        set { _throttleUpdates = value; } 
    }
<<<<<<< HEAD
    public override bool IsColliding {
        get { return (_collidingStep == _scene.SimulationStep); } 
        set { _isColliding = value; } 
    }
    public override bool CollidingGround {
        get { return (_collidingGroundStep == _scene.SimulationStep); } 
        set { _collidingGround = value; } 
    }
    public override bool CollidingObj { 
        get { return _collidingObj; } 
        set { _collidingObj = value; } 
    }
=======
>>>>>>> upstream/master
    public bool IsPhantom {
        get {
            // SceneObjectPart removes phantom objects from the physics scene
            // so, although we could implement touching and such, we never
            // are invoked as a phantom object
            return false;
        }
    }
<<<<<<< HEAD
    public override bool FloatOnWater { 
        set { _floatOnWater = value; } 
=======
    public override bool FloatOnWater {
        set {
            _floatOnWater = value;
            PhysicsScene.TaintedObject("BSPrim.setFloatOnWater", delegate()
            {
                if (_floatOnWater)
                    CurrentCollisionFlags = PhysicsScene.PE.AddToCollisionFlags(PhysBody, CollisionFlags.BS_FLOATS_ON_WATER);
                else
                    CurrentCollisionFlags = PhysicsScene.PE.RemoveFromCollisionFlags(PhysBody, CollisionFlags.BS_FLOATS_ON_WATER);
            });
        }
>>>>>>> upstream/master
    }
    public override OMV.Vector3 RotationalVelocity { 
        get {
            /*
            OMV.Vector3 pv = OMV.Vector3.Zero;
            // if close to zero, report zero
            // This is copied from ODE but I'm not sure why it returns zero but doesn't
            //    zero the property in the physics engine.
            if (_rotationalVelocity.ApproxEquals(pv, 0.2f))
                return pv;
             */

            return _rotationalVelocity;
        } 
        set {
            _rotationalVelocity = value;
            // m_log.DebugFormat("{0}: RotationalVelocity={1}", LogHeader, _rotationalVelocity);
            _scene.TaintedObject("BSPrim.setRotationalVelocity", delegate()
            {
                DetailLog("{0},BSPrim.SetRotationalVel,taint,rotvel={1}", LocalID, _rotationalVelocity);
<<<<<<< HEAD
                BulletSimAPI.SetAngularVelocity2(BSBody.Ptr, _rotationalVelocity);
=======
                ForceRotationalVelocity = _rotationalVelocity;
>>>>>>> upstream/master
            });
        } 
    }
<<<<<<< HEAD
    public override bool Kinematic { 
        get { return _kinematic; } 
        set { _kinematic = value; 
=======
    public override OMV.Vector3 ForceRotationalVelocity {
        get {
            return _rotationalVelocity;
        }
        set {
            _rotationalVelocity = value;
            if (PhysBody.HasPhysicalBody)
            {
                PhysicsScene.PE.SetAngularVelocity(PhysBody, _rotationalVelocity);
                ActivateIfPhysical(false);
            }
        }
    }
    public override bool Kinematic {
        get { return _kinematic; }
        set { _kinematic = value;
>>>>>>> upstream/master
            // m_log.DebugFormat("{0}: Kinematic={1}", LogHeader, _kinematic);
        } 
    }
    public override float Buoyancy { 
        get { return _buoyancy; } 
        set {
            _buoyancy = value;
            _scene.TaintedObject("BSPrim.setBuoyancy", delegate()
            {
                DetailLog("{0},BSPrim.SetBuoyancy,taint,buoy={1}", LocalID, _buoyancy);
                // Buoyancy is faked by changing the gravity applied to the object
                float grav = Scene.Params.gravity * (1f - _buoyancy);
                BulletSimAPI.SetGravity2(BSBody.Ptr, new OMV.Vector3(0f, 0f, grav));
                // BulletSimAPI.SetObjectBuoyancy(_scene.WorldID, _localID, _buoyancy);
            });
<<<<<<< HEAD
        } 
=======
        }
    }
    public override float ForceBuoyancy {
        get { return _buoyancy; }
        set {
            _buoyancy = value;
            // DetailLog("{0},BSPrim.setForceBuoyancy,taint,buoy={1}", LocalID, _buoyancy);
            // Force the recalculation of the various inertia,etc variables in the object
            DetailLog("{0},BSPrim.ForceBuoyancy,buoy={1},mass={2}", LocalID, _buoyancy, _mass);
            UpdatePhysicalMassProperties(_mass, true);
            ActivateIfPhysical(false);
        }
>>>>>>> upstream/master
    }

    // Used for MoveTo
    public override OMV.Vector3 PIDTarget { 
        set { _PIDTarget = value; } 
    }
<<<<<<< HEAD
    public override bool PIDActive { 
        set { _usePID = value; } 
    }
    public override float PIDTau { 
        set { _PIDTau = value; } 
=======
    public override float PIDTau {
        set { _PIDTau = value; }
>>>>>>> upstream/master
    }
    public override bool PIDActive {
        set {
            if (value)
            {
                // We're taking over after this.
                ZeroMotion(true);

                _targetMotor = new BSVMotor("BSPrim.PIDTarget",
                                            _PIDTau,                    // timeScale
                                            BSMotor.Infinite,           // decay time scale
                                            BSMotor.InfiniteVector,     // friction timescale
                                            1f                          // efficiency
                );
                _targetMotor.PhysicsScene = PhysicsScene; // DEBUG DEBUG so motor will output detail log messages.
                _targetMotor.SetTarget(_PIDTarget);
                _targetMotor.SetCurrent(RawPosition);
                /*
                _targetMotor = new BSPIDVMotor("BSPrim.PIDTarget");
                _targetMotor.PhysicsScene = PhysicsScene; // DEBUG DEBUG so motor will output detail log messages.

                _targetMotor.SetTarget(_PIDTarget);
                _targetMotor.SetCurrent(RawPosition);
                _targetMotor.TimeScale = _PIDTau;
                _targetMotor.Efficiency = 1f;
                 */

                RegisterPreStepAction("BSPrim.PIDTarget", LocalID, delegate(float timeStep)
                {
                    OMV.Vector3 origPosition = RawPosition;     // DEBUG DEBUG (for printout below)

                    // 'movePosition' is where we'd like the prim to be at this moment.
                    OMV.Vector3 movePosition = _targetMotor.Step(timeStep);

                    // If we are very close to our target, turn off the movement motor.
                    if (_targetMotor.ErrorIsZero())
                    {
                        DetailLog("{0},BSPrim.PIDTarget,zeroMovement,movePos={1},pos={2},mass={3}",
                                                LocalID, movePosition, RawPosition, Mass);
                        ForcePosition = _targetMotor.TargetValue;
                        _targetMotor.Enabled = false;
                    }
                    else
                    {
                        ForcePosition = movePosition;
                    }
                    DetailLog("{0},BSPrim.PIDTarget,move,fromPos={1},movePos={2}", LocalID, origPosition, movePosition);
                });
            }
            else
            {
                // Stop any targetting
                UnRegisterPreStepAction("BSPrim.PIDTarget", LocalID);
            }
        }
    }

    // Used for llSetHoverHeight and maybe vehicle height
    // Hover Height will override MoveTo target's Z
<<<<<<< HEAD
    public override bool PIDHoverActive { 
        set { _useHoverPID = value; }
=======
    public override bool PIDHoverActive {
        set {
            if (value)
            {
                // Turning the target on
                _hoverMotor = new BSFMotor("BSPrim.Hover",
                                            _PIDHoverTau,               // timeScale
                                            BSMotor.Infinite,           // decay time scale
                                            BSMotor.Infinite,           // friction timescale
                                            1f                          // efficiency
                );
                _hoverMotor.SetTarget(ComputeCurrentPIDHoverHeight());
                _hoverMotor.SetCurrent(RawPosition.Z);
                _hoverMotor.PhysicsScene = PhysicsScene; // DEBUG DEBUG so motor will output detail log messages.

                RegisterPreStepAction("BSPrim.Hover", LocalID, delegate(float timeStep)
                {
                    _hoverMotor.SetCurrent(RawPosition.Z);
                    _hoverMotor.SetTarget(ComputeCurrentPIDHoverHeight());
                    float targetHeight = _hoverMotor.Step(timeStep);

                    // 'targetHeight' is where we'd like the Z of the prim to be at this moment.
                    // Compute the amount of force to push us there.
                    float moveForce = (targetHeight - RawPosition.Z) * Mass;
                    // Undo anything the object thinks it's doing at the moment
                    moveForce = -RawVelocity.Z * Mass;

                    PhysicsScene.PE.ApplyCentralImpulse(PhysBody, new OMV.Vector3(0f, 0f, moveForce));
                    DetailLog("{0},BSPrim.Hover,move,targHt={1},moveForce={2},mass={3}", LocalID, targetHeight, moveForce, Mass);
                });
            }
            else
            {
                UnRegisterPreStepAction("BSPrim.Hover", LocalID);
            }
        }
>>>>>>> upstream/master
    }
    public override float PIDHoverHeight { 
        set { _PIDHoverHeight = value; }
    }
    public override PIDHoverType PIDHoverType { 
        set { _PIDHoverType = value; }
    }
<<<<<<< HEAD
    public override float PIDHoverTau { 
        set { _PIDHoverTao = value; }
=======
    public override float PIDHoverTau {
        set { _PIDHoverTau = value; }
>>>>>>> upstream/master
    }
    // Based on current position, determine what we should be hovering at now.
    // Must recompute often. What if we walked offa cliff>
    private float ComputeCurrentPIDHoverHeight()
    {
        float ret = _PIDHoverHeight;
        float groundHeight = PhysicsScene.TerrainManager.GetTerrainHeightAtXYZ(RawPosition);

        switch (_PIDHoverType)
        {
            case PIDHoverType.Ground:
                ret = groundHeight + _PIDHoverHeight;
                break;
            case PIDHoverType.GroundAndWater:
                float waterHeight = PhysicsScene.TerrainManager.GetWaterLevelAtXYZ(RawPosition);
                if (groundHeight > waterHeight)
                {
                    ret = groundHeight + _PIDHoverHeight;
                }
                else
                {
                    ret = waterHeight + _PIDHoverHeight;
                }
                break;
        }
        return ret;
    }


    // For RotLookAt
    public override OMV.Quaternion APIDTarget { set { return; } }
    public override bool APIDActive { set { return; } }
    public override float APIDStrength { set { return; } }
    public override float APIDDamping { set { return; } }

    public override void AddForce(OMV.Vector3 force, bool pushforce) {
<<<<<<< HEAD
=======
        // Since this force is being applied in only one step, make this a force per second.
        OMV.Vector3 addForce = force / PhysicsScene.LastTimeStep;
        AddForce(addForce, pushforce, false);
    }
    // Applying a force just adds this to the total force on the object.
    // This added force will only last the next simulation tick.
    public void AddForce(OMV.Vector3 force, bool pushforce, bool inTaintTime) {
>>>>>>> upstream/master
        // for an object, doesn't matter if force is a pushforce or not
        if (!IsStatic)
        {
<<<<<<< HEAD
            m_log.WarnFormat("{0}: Got a NaN force applied to a prim. LocalID={1}", LogHeader, LocalID);
            return;
        }
        _scene.TaintedObject("BSPrim.AddForce", delegate()
        {
            OMV.Vector3 fSum = OMV.Vector3.Zero;
            lock (m_accumulatedForces)
            {
                foreach (OMV.Vector3 v in m_accumulatedForces)
=======
            if (force.IsFinite())
            {
                float magnitude = force.Length();
                if (magnitude > BSParam.MaxAddForceMagnitude)
>>>>>>> upstream/master
                {
                    // Force has a limit
                    force = force / magnitude * BSParam.MaxAddForceMagnitude;
                }

                OMV.Vector3 addForce = force;
                // DetailLog("{0},BSPrim.addForce,call,force={1}", LocalID, addForce);

                PhysicsScene.TaintedObject(inTaintTime, "BSPrim.AddForce", delegate()
                {
                    // Bullet adds this central force to the total force for this tick
                    DetailLog("{0},BSPrim.addForce,taint,force={1}", LocalID, addForce);
                    if (PhysBody.HasPhysicalBody)
                    {
                        PhysicsScene.PE.ApplyCentralForce(PhysBody, addForce);
                        ActivateIfPhysical(false);
                    }
                });
            }
<<<<<<< HEAD
            DetailLog("{0},BSPrim.AddObjectForce,taint,force={1}", LocalID, fSum);
            // For unknown reasons, "ApplyCentralForce" adds this force to the total force on the object.
            BulletSimAPI.ApplyCentralForce2(BSBody.Ptr, fSum);
        });
    }

    public override void AddAngularForce(OMV.Vector3 force, bool pushforce) { 
        DetailLog("{0},BSPrim.AddAngularForce,call,angForce={1},push={2}", LocalID, force, pushforce);
        // m_log.DebugFormat("{0}: AddAngularForce. f={1}, push={2}", LogHeader, force, pushforce);
    }
    public override void SetMomentum(OMV.Vector3 momentum) { 
        DetailLog("{0},BSPrim.SetMomentum,call,mom={1}", LocalID, momentum);
    }
    public override void SubscribeEvents(int ms) { 
        _subscribedEventsMs = ms;
        if (ms > 0)
        {
            // make sure first collision happens
            _nextCollisionOkTime = Util.EnvironmentTickCount() - _subscribedEventsMs;

            Scene.TaintedObject("BSPrim.SubscribeEvents", delegate()
            {
                m_currentCollisionFlags = BulletSimAPI.AddToCollisionFlags2(BSBody.Ptr, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
            });
        }
    }
    public override void UnSubscribeEvents() { 
        _subscribedEventsMs = 0;
        Scene.TaintedObject("BSPrim.UnSubscribeEvents", delegate()
        {
            m_currentCollisionFlags = BulletSimAPI.RemoveFromCollisionFlags2(BSBody.Ptr, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
=======
            else
            {
                m_log.WarnFormat("{0}: AddForce: Got a NaN force applied to a prim. LocalID={1}", LogHeader, LocalID);
                return;
            }
        }
    }

    public void AddForceImpulse(OMV.Vector3 impulse, bool pushforce, bool inTaintTime) {
        // for an object, doesn't matter if force is a pushforce or not
        if (!IsStatic)
        {
            if (impulse.IsFinite())
            {
                float magnitude = impulse.Length();
                if (magnitude > BSParam.MaxAddForceMagnitude)
                {
                    // Force has a limit
                    impulse = impulse / magnitude * BSParam.MaxAddForceMagnitude;
                }

                // DetailLog("{0},BSPrim.addForceImpulse,call,impulse={1}", LocalID, impulse);
                OMV.Vector3 addImpulse = impulse;
                PhysicsScene.TaintedObject(inTaintTime, "BSPrim.AddImpulse", delegate()
                {
                    // Bullet adds this impulse immediately to the velocity
                    DetailLog("{0},BSPrim.addForceImpulse,taint,impulseforce={1}", LocalID, addImpulse);
                    if (PhysBody.HasPhysicalBody)
                    {
                        PhysicsScene.PE.ApplyCentralImpulse(PhysBody, addImpulse);
                        ActivateIfPhysical(false);
                    }
                });
            }
            else
            {
                m_log.WarnFormat("{0}: AddForceImpulse: Got a NaN impulse applied to a prim. LocalID={1}", LogHeader, LocalID);
                return;
            }
        }
    }

    public override void AddAngularForce(OMV.Vector3 force, bool pushforce) {
        AddAngularForce(force, pushforce, false);
    }
    public void AddAngularForce(OMV.Vector3 force, bool pushforce, bool inTaintTime)
    {
        if (force.IsFinite())
        {
            OMV.Vector3 angForce = force;
            PhysicsScene.TaintedObject(inTaintTime, "BSPrim.AddAngularForce", delegate()
            {
                if (PhysBody.HasPhysicalBody)
                {
                    PhysicsScene.PE.ApplyTorque(PhysBody, angForce);
                    ActivateIfPhysical(false);
                }
            });
        }
        else
        {
            m_log.WarnFormat("{0}: Got a NaN force applied to a prim. LocalID={1}", LogHeader, LocalID);
            return;
        }
    }

    // A torque impulse.
    // ApplyTorqueImpulse adds torque directly to the angularVelocity.
    // AddAngularForce accumulates the force and applied it to the angular velocity all at once.
    // Computed as: angularVelocity += impulse * inertia;
    public void ApplyTorqueImpulse(OMV.Vector3 impulse, bool inTaintTime)
    {
        OMV.Vector3 applyImpulse = impulse;
        PhysicsScene.TaintedObject(inTaintTime, "BSPrim.ApplyTorqueImpulse", delegate()
        {
            if (PhysBody.HasPhysicalBody)
            {
                PhysicsScene.PE.ApplyTorqueImpulse(PhysBody, applyImpulse);
                ActivateIfPhysical(false);
            }
>>>>>>> upstream/master
        });
    }
    public override bool SubscribedEvents() { 
        return (_subscribedEventsMs > 0);
    }

    #region Mass Calculation

    private float CalculateMass()
    {
        float volume = _size.X * _size.Y * _size.Z; // default
        float tmp;

        float returnMass = 0;
        float hollowAmount = (float)_pbs.ProfileHollow * 2.0e-5f;
        float hollowVolume = hollowAmount * hollowAmount; 
        
        switch (_pbs.ProfileShape)
        {
            case ProfileShape.Square:
                // default box

                if (_pbs.PathCurve == (byte)Extrusion.Straight)
                    {
                    if (hollowAmount > 0.0)
                        {
                        switch (_pbs.HollowShape)
                            {
                            case HollowShape.Square:
                            case HollowShape.Same:
                                break;

                            case HollowShape.Circle:

                                hollowVolume *= 0.78539816339f;
                                break;

                            case HollowShape.Triangle:

                                hollowVolume *= (0.5f * .5f);
                                break;

                            default:
                                hollowVolume = 0;
                                break;
                            }
                        volume *= (1.0f - hollowVolume);
                        }
                    }

                else if (_pbs.PathCurve == (byte)Extrusion.Curve1)
                    {
                    //a tube 

                    volume *= 0.78539816339e-2f * (float)(200 - _pbs.PathScaleX);
                    tmp= 1.0f -2.0e-2f * (float)(200 - _pbs.PathScaleY);
                    volume -= volume*tmp*tmp;
                    
                    if (hollowAmount > 0.0)
                        {
                        hollowVolume *= hollowAmount;
                        
                        switch (_pbs.HollowShape)
                            {
                            case HollowShape.Square:
                            case HollowShape.Same:
                                break;

                            case HollowShape.Circle:
                                hollowVolume *= 0.78539816339f;;
                                break;

                            case HollowShape.Triangle:
                                hollowVolume *= 0.5f * 0.5f;
                                break;
                            default:
                                hollowVolume = 0;
                                break;
                            }
                        volume *= (1.0f - hollowVolume);
                        }
                    }

                break;

            case ProfileShape.Circle:

                if (_pbs.PathCurve == (byte)Extrusion.Straight)
                    {
                    volume *= 0.78539816339f; // elipse base

                    if (hollowAmount > 0.0)
                        {
                        switch (_pbs.HollowShape)
                            {
                            case HollowShape.Same:
                            case HollowShape.Circle:
                                break;

                            case HollowShape.Square:
                                hollowVolume *= 0.5f * 2.5984480504799f;
                                break;

                            case HollowShape.Triangle:
                                hollowVolume *= .5f * 1.27323954473516f;
                                break;

                            default:
                                hollowVolume = 0;
                                break;
                            }
                        volume *= (1.0f - hollowVolume);
                        }
                    }

                else if (_pbs.PathCurve == (byte)Extrusion.Curve1)
                    {
                    volume *= 0.61685027506808491367715568749226e-2f * (float)(200 - _pbs.PathScaleX);
                    tmp = 1.0f - .02f * (float)(200 - _pbs.PathScaleY);
                    volume *= (1.0f - tmp * tmp);
                    
                    if (hollowAmount > 0.0)
                        {

                        // calculate the hollow volume by it's shape compared to the prim shape
                        hollowVolume *= hollowAmount;

                        switch (_pbs.HollowShape)
                            {
                            case HollowShape.Same:
                            case HollowShape.Circle:
                                break;

                            case HollowShape.Square:
                                hollowVolume *= 0.5f * 2.5984480504799f;
                                break;

                            case HollowShape.Triangle:
                                hollowVolume *= .5f * 1.27323954473516f;
                                break;

                            default:
                                hollowVolume = 0;
                                break;
                            }
                        volume *= (1.0f - hollowVolume);
                        }
                    }
                break;

            case ProfileShape.HalfCircle:
                if (_pbs.PathCurve == (byte)Extrusion.Curve1)
                {
                volume *= 0.52359877559829887307710723054658f;
                }
                break;

            case ProfileShape.EquilateralTriangle:

                if (_pbs.PathCurve == (byte)Extrusion.Straight)
                    {
                    volume *= 0.32475953f;

                    if (hollowAmount > 0.0)
                        {

                        // calculate the hollow volume by it's shape compared to the prim shape
                        switch (_pbs.HollowShape)
                            {
                            case HollowShape.Same:
                            case HollowShape.Triangle:
                                hollowVolume *= .25f;
                                break;

                            case HollowShape.Square:
                                hollowVolume *= 0.499849f * 3.07920140172638f;
                                break;

                            case HollowShape.Circle:
                                // Hollow shape is a perfect cyllinder in respect to the cube's scale
                                // Cyllinder hollow volume calculation

                                hollowVolume *= 0.1963495f * 3.07920140172638f;
                                break;

                            default:
                                hollowVolume = 0;
                                break;
                            }
                        volume *= (1.0f - hollowVolume);
                        }
                    }
                else if (_pbs.PathCurve == (byte)Extrusion.Curve1)
                    {
                    volume *= 0.32475953f;
                    volume *= 0.01f * (float)(200 - _pbs.PathScaleX);
                    tmp = 1.0f - .02f * (float)(200 - _pbs.PathScaleY);
                    volume *= (1.0f - tmp * tmp);

                    if (hollowAmount > 0.0)
                        {

                        hollowVolume *= hollowAmount;

                        switch (_pbs.HollowShape)
                            {
                            case HollowShape.Same:
                            case HollowShape.Triangle:
                                hollowVolume *= .25f;
                                break;

                            case HollowShape.Square:
                                hollowVolume *= 0.499849f * 3.07920140172638f;
                                break;

                            case HollowShape.Circle:

                                hollowVolume *= 0.1963495f * 3.07920140172638f;
                                break;

                            default:
                                hollowVolume = 0;
                                break;
                            }
                        volume *= (1.0f - hollowVolume);
                        }
                    }
                    break;

            default:
                break;
            }



        float taperX1;
        float taperY1;
        float taperX;
        float taperY;
        float pathBegin;
        float pathEnd;
        float profileBegin;
        float profileEnd;

        if (_pbs.PathCurve == (byte)Extrusion.Straight || _pbs.PathCurve == (byte)Extrusion.Flexible)
            {
            taperX1 = _pbs.PathScaleX * 0.01f;
            if (taperX1 > 1.0f)
                taperX1 = 2.0f - taperX1;
            taperX = 1.0f - taperX1;

            taperY1 = _pbs.PathScaleY * 0.01f;
            if (taperY1 > 1.0f)
                taperY1 = 2.0f - taperY1;
            taperY = 1.0f - taperY1;
            }
        else
            {
            taperX = _pbs.PathTaperX * 0.01f;
            if (taperX < 0.0f)
                taperX = -taperX;
            taperX1 = 1.0f - taperX;

            taperY = _pbs.PathTaperY * 0.01f;
            if (taperY < 0.0f)
                taperY = -taperY;
            taperY1 = 1.0f - taperY;

            }


        volume *= (taperX1 * taperY1 + 0.5f * (taperX1 * taperY + taperX * taperY1) + 0.3333333333f * taperX * taperY);

        pathBegin = (float)_pbs.PathBegin * 2.0e-5f;
        pathEnd = 1.0f - (float)_pbs.PathEnd * 2.0e-5f;
        volume *= (pathEnd - pathBegin);

        // this is crude aproximation
        profileBegin = (float)_pbs.ProfileBegin * 2.0e-5f;
        profileEnd = 1.0f - (float)_pbs.ProfileEnd * 2.0e-5f;
        volume *= (profileEnd - profileBegin);

        returnMass = _density * volume;

        /*
         * This change means each object keeps its own mass and the Mass property
         * will return the sum if we're part of a linkset.
        if (IsRootOfLinkset)
        {
            foreach (BSPrim prim in _childrenPrims)
            {
                returnMass += prim.CalculateMass();
            }
        }
         */

<<<<<<< HEAD
        if (returnMass <= 0)
            returnMass = 0.0001f;

        if (returnMass > _scene.MaximumObjectMass)
            returnMass = _scene.MaximumObjectMass;
=======
        returnMass = Util.Clamp(returnMass, BSParam.MinimumObjectMass, BSParam.MaximumObjectMass);
>>>>>>> upstream/master

        return returnMass;
    }// end CalculateMass
    #endregion Mass Calculation

<<<<<<< HEAD
    // Create the geometry information in Bullet for later use.
    // The objects needs a hull if it's physical otherwise a mesh is enough.
    // No locking here because this is done when we know physics is not simulating.
    // if 'forceRebuild' is true, the geometry is rebuilt. Otherwise a previously built version is used.
    // Returns 'true' if the geometry was rebuilt.
    // Called at taint-time!
    private bool CreateGeom(bool forceRebuild)
    {
        bool ret = false;
        bool haveShape = false;

        // If the prim attributes are simple, this could be a simple Bullet native shape
        if ((_pbs.SculptEntry && !Scene.ShouldMeshSculptedPrim)
                || (_pbs.ProfileBegin == 0 && _pbs.ProfileEnd == 0
                    && _pbs.ProfileHollow == 0
                    && _pbs.PathTwist == 0 && _pbs.PathTwistBegin == 0
                    && _pbs.PathBegin == 0 && _pbs.PathEnd == 0
                    && _pbs.PathTaperX == 0 && _pbs.PathTaperY == 0
                    && _pbs.PathScaleX == 100 && _pbs.PathScaleY == 100
                    && _pbs.PathShearX == 0 && _pbs.PathShearY == 0) )
        {
            if (_pbs.ProfileShape == ProfileShape.HalfCircle && _pbs.PathCurve == (byte)Extrusion.Curve1)
            {
                haveShape = true;
                if (forceRebuild || (_shapeType != ShapeData.PhysicsShapeType.SHAPE_SPHERE))
                {
                    DetailLog("{0},BSPrim.CreateGeom,sphere (force={1}", LocalID, forceRebuild);
                    _shapeType = ShapeData.PhysicsShapeType.SHAPE_SPHERE;
                    // Bullet native objects are scaled by the Bullet engine so pass the size in
                    _scale = _size;
                    // TODO: do we need to check for and destroy a mesh or hull that might have been left from before?
                    ret = true;
                }
            }
            else
            {
                // m_log.DebugFormat("{0}: CreateGeom: Defaulting to box. lid={1}, type={2}, size={3}", LogHeader, LocalID, _shapeType, _size);
                haveShape = true;
                if (forceRebuild || (_shapeType != ShapeData.PhysicsShapeType.SHAPE_BOX))
                {
                    DetailLog("{0},BSPrim.CreateGeom,box (force={1})", LocalID, forceRebuild);
                    _shapeType = ShapeData.PhysicsShapeType.SHAPE_BOX;
                    _scale = _size;
                    // TODO: do we need to check for and destroy a mesh or hull that might have been left from before?
                    ret = true;
                }
            }
        }
        // If a simple shape isn't happening, create a mesh and possibly a hull
        if (!haveShape)
        {
            if (IsPhysical)
            {
                if (forceRebuild || _hullKey == 0)
                {
                    // physical objects require a hull for interaction.
                    // This also creates the mesh if it doesn't already exist
                    ret = CreateGeomHull();
                }
            }
            else
            {
                if (forceRebuild || _meshKey == 0)
                {
                    // Static (non-physical) objects only need a mesh for bumping into
                    ret = CreateGeomMesh();
                }
            }
        }
        return ret;
    }
=======
    // Rebuild the geometry and object.
    // This is called when the shape changes so we need to recreate the mesh/hull.
    // Called at taint-time!!!
    public void CreateGeomAndObject(bool forceRebuild)
    {
        // If this prim is part of a linkset, we must remove and restore the physical
        //    links if the body is rebuilt.
        bool needToRestoreLinkset = false;
        bool needToRestoreVehicle = false;

        // Create the correct physical representation for this type of object.
        // Updates PhysBody and PhysShape with the new information.
        // Ignore 'forceRebuild'. This routine makes the right choices and changes of necessary.
        PhysicsScene.Shapes.GetBodyAndShape(false, PhysicsScene.World, this, null, delegate(BulletBody dBody)
        {
            // Called if the current prim body is about to be destroyed.
            // Remove all the physical dependencies on the old body.
            // (Maybe someday make the changing of BSShape an event to be subscribed to by BSLinkset, ...)
            needToRestoreLinkset = Linkset.RemoveBodyDependencies(this);
            needToRestoreVehicle = _vehicle.RemoveBodyDependencies(this);
        });
>>>>>>> upstream/master

    // No locking here because this is done when we know physics is not simulating
    // Returns 'true' of a mesh was actually rebuild (we could also have one of these specs).
    // Called at taint-time!
    private bool CreateGeomMesh()
    {
        // level of detail based on size and type of the object
        float lod = _scene.MeshLOD;
        if (_pbs.SculptEntry) 
            lod = _scene.SculptLOD;
        float maxAxis = Math.Max(_size.X, Math.Max(_size.Y, _size.Z));
        if (maxAxis > _scene.MeshMegaPrimThreshold) 
            lod = _scene.MeshMegaPrimLOD;

        ulong newMeshKey = (ulong)_pbs.GetMeshKey(_size, lod);
        // m_log.DebugFormat("{0}: CreateGeomMesh: lID={1}, oldKey={2}, newKey={3}", LogHeader, _localID, _meshKey, newMeshKey);

        // if this new shape is the same as last time, don't recreate the mesh
        if (_meshKey == newMeshKey) return false;

        DetailLog("{0},BSPrim.CreateGeomMesh,create,key={1}", LocalID, newMeshKey);
        // Since we're recreating new, get rid of any previously generated shape
        if (_meshKey != 0)
        {
            // m_log.DebugFormat("{0}: CreateGeom: deleting old mesh. lID={1}, Key={2}", LogHeader, _localID, _meshKey);
            DetailLog("{0},BSPrim.CreateGeomMesh,deleteOld,key={1}", LocalID, _meshKey);
            BulletSimAPI.DestroyMesh(_scene.WorldID, _meshKey);
            _mesh = null;
            _meshKey = 0;
        }

        _meshKey = newMeshKey;
        // always pass false for physicalness as this creates some sort of bounding box which we don't need
        _mesh = _scene.mesher.CreateMesh(_avName, _pbs, _size, lod, false);

        int[] indices = _mesh.getIndexListAsInt();
        List<OMV.Vector3> vertices = _mesh.getVertexList();

        float[] verticesAsFloats = new float[vertices.Count * 3];
        int vi = 0;
        foreach (OMV.Vector3 vv in vertices)
        {
            verticesAsFloats[vi++] = vv.X;
            verticesAsFloats[vi++] = vv.Y;
            verticesAsFloats[vi++] = vv.Z;
        }

        // m_log.DebugFormat("{0}: CreateGeomMesh: calling CreateMesh. lid={1}, key={2}, indices={3}, vertices={4}", 
        //                  LogHeader, _localID, _meshKey, indices.Length, vertices.Count);
        BulletSimAPI.CreateMesh(_scene.WorldID, _meshKey, indices.GetLength(0), indices, 
                                                        vertices.Count, verticesAsFloats);

        _shapeType = ShapeData.PhysicsShapeType.SHAPE_MESH;
        // meshes are already scaled by the meshmerizer
        _scale = new OMV.Vector3(1f, 1f, 1f);
        return true;
    }

    // No locking here because this is done when we know physics is not simulating
    // Returns 'true' of a mesh was actually rebuild (we could also have one of these specs).
    private bool CreateGeomHull()
    {
        float lod = _pbs.SculptEntry ? _scene.SculptLOD : _scene.MeshLOD;
        ulong newHullKey = (ulong)_pbs.GetMeshKey(_size, lod);
        // m_log.DebugFormat("{0}: CreateGeomHull: lID={1}, oldKey={2}, newKey={3}", LogHeader, _localID, _hullKey, newHullKey);

        // if the hull hasn't changed, don't rebuild it
        if (newHullKey == _hullKey) return false;

        DetailLog("{0},BSPrim.CreateGeomHull,create,oldKey={1},newKey={2}", LocalID, _hullKey, newHullKey);
        
        // Since we're recreating new, get rid of any previously generated shape
        if (_hullKey != 0)
        {
            // m_log.DebugFormat("{0}: CreateGeom: deleting old hull. Key={1}", LogHeader, _hullKey);
            DetailLog("{0},BSPrim.CreateGeomHull,deleteOldHull,key={1}", LocalID, _hullKey);
            BulletSimAPI.DestroyHull(_scene.WorldID, _hullKey);
            _hullKey = 0;
        }

        _hullKey = newHullKey;

        // Make sure the underlying mesh exists and is correct
        CreateGeomMesh();

        int[] indices = _mesh.getIndexListAsInt();
        List<OMV.Vector3> vertices = _mesh.getVertexList();

        //format conversion from IMesh format to DecompDesc format
        List<int> convIndices = new List<int>();
        List<float3> convVertices = new List<float3>();
        for (int ii = 0; ii < indices.GetLength(0); ii++)
        {
            convIndices.Add(indices[ii]);
        }
        foreach (OMV.Vector3 vv in vertices)
        {
            convVertices.Add(new float3(vv.X, vv.Y, vv.Z));
        }

        // setup and do convex hull conversion
        _hulls = new List<ConvexResult>();
        DecompDesc dcomp = new DecompDesc();
        dcomp.mIndices = convIndices;
        dcomp.mVertices = convVertices;
        ConvexBuilder convexBuilder = new ConvexBuilder(HullReturn);
        // create the hull into the _hulls variable
        convexBuilder.process(dcomp);

        // Convert the vertices and indices for passing to unmanaged.
        // The hull information is passed as a large floating point array. 
        // The format is:
        //  convHulls[0] = number of hulls
        //  convHulls[1] = number of vertices in first hull
        //  convHulls[2] = hull centroid X coordinate
        //  convHulls[3] = hull centroid Y coordinate
        //  convHulls[4] = hull centroid Z coordinate
        //  convHulls[5] = first hull vertex X
        //  convHulls[6] = first hull vertex Y
        //  convHulls[7] = first hull vertex Z
        //  convHulls[8] = second hull vertex X
        //  ...
        //  convHulls[n] = number of vertices in second hull
        //  convHulls[n+1] = second hull centroid X coordinate
        //  ...
        //
        // TODO: is is very inefficient. Someday change the convex hull generator to return
        //   data structures that do not need to be converted in order to pass to Bullet.
        //   And maybe put the values directly into pinned memory rather than marshaling.
        int hullCount = _hulls.Count;
        int totalVertices = 1;          // include one for the count of the hulls
        foreach (ConvexResult cr in _hulls)
        {
            totalVertices += 4;                         // add four for the vertex count and centroid
            totalVertices += cr.HullIndices.Count * 3;  // we pass just triangles
        }
        float[] convHulls = new float[totalVertices];

        convHulls[0] = (float)hullCount;
        int jj = 1;
        foreach (ConvexResult cr in _hulls)
        {
            // copy vertices for index access
            float3[] verts = new float3[cr.HullVertices.Count];
            int kk = 0;
            foreach (float3 ff in cr.HullVertices)
            {
                verts[kk++] = ff;
            }

            // add to the array one hull's worth of data
            convHulls[jj++] = cr.HullIndices.Count;
            convHulls[jj++] = 0f;   // centroid x,y,z
            convHulls[jj++] = 0f;
            convHulls[jj++] = 0f;
            foreach (int ind in cr.HullIndices)
            {
                convHulls[jj++] = verts[ind].x;
                convHulls[jj++] = verts[ind].y;
                convHulls[jj++] = verts[ind].z;
            }
        }

        // create the hull definition in Bullet
        // m_log.DebugFormat("{0}: CreateGeom: calling CreateHull. lid={1}, key={2}, hulls={3}", LogHeader, _localID, _hullKey, hullCount);
        BulletSimAPI.CreateHull(_scene.WorldID, _hullKey, hullCount, convHulls);
        _shapeType = ShapeData.PhysicsShapeType.SHAPE_HULL;
        // meshes are already scaled by the meshmerizer
        _scale = new OMV.Vector3(1f, 1f, 1f);
        DetailLog("{0},BSPrim.CreateGeomHull,done", LocalID);
        return true;
    }

    // Callback from convex hull creater with a newly created hull.
    // Just add it to the collection of hulls for this shape.
    private void HullReturn(ConvexResult result)
    {
        _hulls.Add(result);
        return;
    }

    private void VerifyCorrectPhysicalShape()
    {
        if (!IsStatic)
        {
            // if not static, it will need a hull to efficiently collide with things
            if (_hullKey == 0)
            {
                CreateGeomAndObject(false);
            }

        }
    }

    // Create an object in Bullet if it has not already been created
    // No locking here because this is done when the physics engine is not simulating
    // Returns 'true' if an object was actually created.
    private bool CreateObject()
    {
        // this routine is called when objects are rebuilt. 

        // the mesh or hull must have already been created in Bullet
        ShapeData shape;
        FillShapeInfo(out shape);
        // m_log.DebugFormat("{0}: CreateObject: lID={1}, shape={2}", LogHeader, _localID, shape.Type);
        bool ret = BulletSimAPI.CreateObject(_scene.WorldID, shape);

        // the CreateObject() may have recreated the rigid body. Make sure we have the latest address.
        BSBody = new BulletBody(LocalID, BulletSimAPI.GetBodyHandle2(_scene.World.Ptr, LocalID));
        BSShape = new BulletShape(BulletSimAPI.GetCollisionShape2(BSBody.Ptr));

        return ret;
    }

    // Copy prim's info into the BulletSim shape description structure
    public void FillShapeInfo(out ShapeData shape)
    {
        shape.ID = _localID;
        shape.Type = _shapeType;
        shape.Position = _position;
        shape.Rotation = _orientation;
        shape.Velocity = _velocity;
        shape.Scale = _scale;
        shape.Mass = _isPhysical ? _mass : 0f;
        shape.Buoyancy = _buoyancy;
        shape.HullKey = _hullKey;
        shape.MeshKey = _meshKey;
        shape.Friction = _friction;
        shape.Restitution = _restitution;
        shape.Collidable = (!IsPhantom) ? ShapeData.numericTrue : ShapeData.numericFalse;
        shape.Static = _isPhysical ? ShapeData.numericFalse : ShapeData.numericTrue;
    }

    // Rebuild the geometry and object.
    // This is called when the shape changes so we need to recreate the mesh/hull.
    // No locking here because this is done when the physics engine is not simulating
    private void CreateGeomAndObject(bool forceRebuild)
    {
        // m_log.DebugFormat("{0}: CreateGeomAndObject. lID={1}, force={2}", LogHeader, _localID, forceRebuild);
        // Create the geometry that will make up the object
        if (CreateGeom(forceRebuild))
        {
            // Create the object and place it into the world
            CreateObject();
            // Make sure the properties are set on the new object
            UpdatePhysicalParameters();
        }
        return;
    }

    // The physics engine says that properties have updated. Update same and inform
    // the world that things have changed.
    public override void UpdateProperties(EntityProperties entprop)
    {
        /*
        UpdatedProperties changed = 0;
        // assign to the local variables so the normal set action does not happen
        // if (_position != entprop.Position)
        if (!_position.ApproxEquals(entprop.Position, POSITION_TOLERANCE))
        {
<<<<<<< HEAD
=======
            // A temporary kludge to suppress the rotational effects introduced on vehicles by Bullet
            // TODO: handle physics introduced by Bullet with computed vehicle physics.
            if (_vehicle.IsActive)
            {
                entprop.RotationalVelocity = OMV.Vector3.Zero;
            }

            // Assign directly to the local variables so the normal set actions do not happen
            entprop.Position -= PositionDisplacement;
>>>>>>> upstream/master
            _position = entprop.Position;
            changed |= UpdatedProperties.Position;
        }
        // if (_orientation != entprop.Rotation)
        if (!_orientation.ApproxEquals(entprop.Rotation, ROTATION_TOLERANCE))
        {
            _orientation = entprop.Rotation;
            changed |= UpdatedProperties.Rotation;
        }
        // if (_velocity != entprop.Velocity)
        if (!_velocity.ApproxEquals(entprop.Velocity, VELOCITY_TOLERANCE))
        {
            _velocity = entprop.Velocity;
            changed |= UpdatedProperties.Velocity;
        }
        // if (_acceleration != entprop.Acceleration)
        if (!_acceleration.ApproxEquals(entprop.Acceleration, ACCELERATION_TOLERANCE))
        {
            _acceleration = entprop.Acceleration;
            changed |= UpdatedProperties.Acceleration;
        }
        // if (_rotationalVelocity != entprop.RotationalVelocity)
        if (!_rotationalVelocity.ApproxEquals(entprop.RotationalVelocity, ROTATIONAL_VELOCITY_TOLERANCE))
        {
            _rotationalVelocity = entprop.RotationalVelocity;
<<<<<<< HEAD
            changed |= UpdatedProperties.RotationalVel;
        }
        if (changed != 0)
        {
            // Only update the position of single objects and linkset roots
            if (this._parentPrim == null)
=======

            // The sanity check can change the velocity and/or position.
            if (IsPhysical && PositionSanityCheck(true))
>>>>>>> upstream/master
            {
                base.RequestPhysicsterseUpdate();
            }
        }
        */

        // Don't check for damping here -- it's done in BulletSim and SceneObjectPart.

<<<<<<< HEAD
        // Updates only for individual prims and for the root object of a linkset.
        if (Linkset.IsRoot(this))
        {
            // Assign to the local variables so the normal set action does not happen
            _position = entprop.Position;
            _orientation = entprop.Rotation;
            _velocity = entprop.Velocity;
            _acceleration = entprop.Acceleration;
            _rotationalVelocity = entprop.RotationalVelocity;

            DetailLog("{0},BSPrim.UpdateProperties,call,pos={1},orient={2},vel={3},accel={4},rotVel={5}",
                    LocalID, _position, _orientation, _velocity, _acceleration, _rotationalVelocity);

            // BulletSimAPI.DumpRigidBody2(Scene.World.Ptr, BSBody.Ptr);
=======
            OMV.Vector3 direction = OMV.Vector3.UnitX * _orientation;   // DEBUG DEBUG DEBUG
            DetailLog("{0},BSPrim.UpdateProperties,call,pos={1},orient={2},dir={3},vel={4},rotVel={5}",
                    LocalID, _position, _orientation, direction, _velocity, _rotationalVelocity);

            // remember the current and last set values
            LastEntityProperties = CurrentEntityProperties;
            CurrentEntityProperties = entprop;
>>>>>>> upstream/master

            base.RequestPhysicsterseUpdate();
        }
            /*
        else
        {
            // For debugging, we can also report the movement of children
            DetailLog("{0},BSPrim.UpdateProperties,child,pos={1},orient={2},vel={3},accel={4},rotVel={5}",
                    LocalID, entprop.Position, entprop.Rotation, entprop.Velocity, 
                    entprop.Acceleration, entprop.RotationalVelocity);
        }
             */
    }

<<<<<<< HEAD
    // I've collided with something
    // Called at taint time from within the Step() function
    CollisionEventUpdate collisionCollection;
    public override bool Collide(uint collidingWith, BSPhysObject collidee, OMV.Vector3 contactPoint, OMV.Vector3 contactNormal, float pentrationDepth)
    {
        bool ret = false;

        // The following lines make IsColliding() and IsCollidingGround() work
        _collidingStep = Scene.SimulationStep;
        if (collidingWith <= Scene.TerrainManager.HighestTerrainID)
        {
            _collidingGroundStep = Scene.SimulationStep;
        }

        // DetailLog("{0},BSPrim.Collison,call,with={1}", LocalID, collidingWith);

        // prims in the same linkset cannot collide with each other
        if (collidee != null && (this.Linkset.LinksetID == collidee.Linkset.LinksetID))
        {
            return ret;
        }

        // if someone has subscribed for collision events....
        if (SubscribedEvents()) {
            // throttle the collisions to the number of milliseconds specified in the subscription
            int nowTime = Scene.SimulationNowTime;
            if (nowTime >= _nextCollisionOkTime) {
                _nextCollisionOkTime = nowTime + _subscribedEventsMs;

                if (collisionCollection == null)
                    collisionCollection = new CollisionEventUpdate();
                collisionCollection.AddCollider(collidingWith, new ContactPoint(contactPoint, contactNormal, pentrationDepth));
                ret = true;
            }
        }
        return ret;
    }

    // The scene is telling us it's time to pass our collected collisions into the simulator
    public override void SendCollisions()
    {
        if (collisionCollection != null && collisionCollection.Count > 0)
        {
            base.SendCollisionUpdate(collisionCollection);
            // The collisionCollection structure is passed around in the simulator.
            // Make sure we don't have a handle to that one and that a new one is used next time.
            collisionCollection = null;
        }
    }

    // Invoke the detailed logger and output something if it's enabled.
    private void DetailLog(string msg, params Object[] args)
    {
        Scene.PhysicsLogging.Write(msg, args);
=======
        // The linkset implimentation might want to know about this.
        Linkset.UpdateProperties(this, true);
>>>>>>> upstream/master
    }
}
}

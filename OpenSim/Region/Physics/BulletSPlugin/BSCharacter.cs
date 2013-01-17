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
using System.Collections.Generic;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.BulletSPlugin
{
public class BSCharacter : BSPhysObject
{
    private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly string LogHeader = "[BULLETS CHAR]";

    public BSScene Scene { get; private set; }
    private String _avName;
    // private bool _stopped;
    private Vector3 _size;
    private Vector3 _scale;
    private PrimitiveBaseShape _pbs;
    private uint _localID = 0;
    private bool _grabbed;
    private bool _selected;
    private Vector3 _position;
    private float _mass;
    public float _density;
    public float _avatarVolume;
    private Vector3 _force;
    private Vector3 _velocity;
    private Vector3 _torque;
    private float _collisionScore;
    private Vector3 _acceleration;
    private Quaternion _orientation;
    private int _physicsActorType;
    private bool _isPhysical;
    private bool _flying;
    private bool _setAlwaysRun;
    private bool _throttleUpdates;
<<<<<<< HEAD
    private bool _isColliding;
    private long _collidingStep;
    private bool _collidingGround;
    private long _collidingGroundStep;
    private bool _collidingObj;
=======
>>>>>>> upstream/master
    private bool _floatOnWater;
    private Vector3 _rotationalVelocity;
    private bool _kinematic;
    private float _buoyancy;

<<<<<<< HEAD
    public override BulletBody BSBody { get; set; }
    public override BulletShape BSShape { get; set; }
    public override BSLinkset Linkset { get; set; }

    private int _subscribedEventsMs = 0;
    private int _nextCollisionOkTime = 0;

    private Vector3 _PIDTarget;
=======
    // The friction and velocity of the avatar is modified depending on whether walking or not.
    private float _currentFriction;         // the friction currently being used (changed by setVelocity).

    private BSVMotor _velocityMotor;

    private OMV.Vector3 _PIDTarget;
>>>>>>> upstream/master
    private bool _usePID;
    private float _PIDTau;
    private bool _useHoverPID;
    private float _PIDHoverHeight;
    private PIDHoverType _PIDHoverType;
    private float _PIDHoverTao;

    public BSCharacter(uint localID, String avName, BSScene parent_scene, Vector3 pos, Vector3 size, bool isFlying)
    {
        _localID = localID;
        _avName = avName;
        Scene = parent_scene;
        _physicsActorType = (int)ActorTypes.Agent;
        _position = pos;
<<<<<<< HEAD
        _size = size;
        _flying = isFlying;
        _orientation = Quaternion.Identity;
        _velocity = Vector3.Zero;
        _buoyancy = ComputeBuoyancyFromFlying(isFlying);
        // The dimensions of the avatar capsule are kept in the scale.
        // Physics creates a unit capsule which is scaled by the physics engine.
        _scale = new Vector3(Scene.Params.avatarCapsuleRadius, Scene.Params.avatarCapsuleRadius, size.Z);
        _density = Scene.Params.avatarDensity;
        ComputeAvatarVolumeAndMass();   // set _avatarVolume and _mass based on capsule size, _density and _scale

        Linkset = new BSLinkset(Scene, this);

        ShapeData shapeData = new ShapeData();
        shapeData.ID = _localID;
        shapeData.Type = ShapeData.PhysicsShapeType.SHAPE_AVATAR;
        shapeData.Position = _position;
        shapeData.Rotation = _orientation;
        shapeData.Velocity = _velocity;
        shapeData.Scale = _scale;
        shapeData.Mass = _mass;
        shapeData.Buoyancy = _buoyancy;
        shapeData.Static = ShapeData.numericFalse;
        shapeData.Friction = Scene.Params.avatarFriction;
        shapeData.Restitution = Scene.Params.avatarRestitution;

        // do actual create at taint time
        Scene.TaintedObject("BSCharacter.create", delegate()
        {
            DetailLog("{0},BSCharacter.create", _localID);
            BulletSimAPI.CreateObject(Scene.WorldID, shapeData);

            // Set the buoyancy for flying. This will be refactored when all the settings happen in C#
            BulletSimAPI.SetObjectBuoyancy(Scene.WorldID, LocalID, _buoyancy);
=======

        _flying = isFlying;
        _orientation = OMV.Quaternion.Identity;
        _velocity = OMV.Vector3.Zero;
        _buoyancy = ComputeBuoyancyFromFlying(isFlying);
        _currentFriction = BSParam.AvatarStandingFriction;
        _avatarDensity = BSParam.AvatarDensity;

        // Old versions of ScenePresence passed only the height. If width and/or depth are zero,
        //     replace with the default values.
        _size = size;
        if (_size.X == 0f) _size.X = BSParam.AvatarCapsuleDepth;
        if (_size.Y == 0f) _size.Y = BSParam.AvatarCapsuleWidth;

        // The dimensions of the physical capsule are kept in the scale.
        // Physics creates a unit capsule which is scaled by the physics engine.
        Scale = ComputeAvatarScale(_size);
        // set _avatarVolume and _mass based on capsule size, _density and Scale
        ComputeAvatarVolumeAndMass();

        SetupMovementMotor();

        DetailLog("{0},BSCharacter.create,call,size={1},scale={2},density={3},volume={4},mass={5}",
                            LocalID, _size, Scale, _avatarDensity, _avatarVolume, RawMass);

        // do actual creation in taint time
        PhysicsScene.TaintedObject("BSCharacter.create", delegate()
        {
            DetailLog("{0},BSCharacter.create,taint", LocalID);
            // New body and shape into PhysBody and PhysShape
            PhysicsScene.Shapes.GetBodyAndShape(true, PhysicsScene.World, this);
>>>>>>> upstream/master

            BSBody = new BulletBody(LocalID, BulletSimAPI.GetBodyHandle2(Scene.World.Ptr, LocalID));
        });
            
        return;
    }

    // called when this character is being destroyed and the resources should be released
    public override void Destroy()
    {
        base.Destroy();

        DetailLog("{0},BSCharacter.Destroy", LocalID);
        Scene.TaintedObject("BSCharacter.destroy", delegate()
        {
<<<<<<< HEAD
            BulletSimAPI.DestroyObject(Scene.WorldID, _localID);
        });
    }

=======
            PhysicsScene.Shapes.DereferenceBody(PhysBody, true /* inTaintTime */, null /* bodyCallback */);
            PhysBody.Clear();
            PhysicsScene.Shapes.DereferenceShape(PhysShape, true /* inTaintTime */, null /* bodyCallback */);
            PhysShape.Clear();
        });
    }

    private void SetPhysicalProperties()
    {
        PhysicsScene.PE.RemoveObjectFromWorld(PhysicsScene.World, PhysBody);

        ZeroMotion(true);
        ForcePosition = _position;

        // Set the velocity and compute the proper friction
        _velocityMotor.Reset();
        _velocityMotor.SetTarget(_velocity);
        _velocityMotor.SetCurrent(_velocity);
        ForceVelocity = _velocity;

        // This will enable or disable the flying buoyancy of the avatar.
        // Needs to be reset especially when an avatar is recreated after crossing a region boundry.
        Flying = _flying;

        PhysicsScene.PE.SetRestitution(PhysBody, BSParam.AvatarRestitution);
        PhysicsScene.PE.SetMargin(PhysShape, PhysicsScene.Params.collisionMargin);
        PhysicsScene.PE.SetLocalScaling(PhysShape, Scale);
        PhysicsScene.PE.SetContactProcessingThreshold(PhysBody, BSParam.ContactProcessingThreshold);
        if (BSParam.CcdMotionThreshold > 0f)
        {
            PhysicsScene.PE.SetCcdMotionThreshold(PhysBody, BSParam.CcdMotionThreshold);
            PhysicsScene.PE.SetCcdSweptSphereRadius(PhysBody, BSParam.CcdSweptSphereRadius);
        }

        UpdatePhysicalMassProperties(RawMass, false);

        // Make so capsule does not fall over
        PhysicsScene.PE.SetAngularFactorV(PhysBody, OMV.Vector3.Zero);

        PhysicsScene.PE.AddToCollisionFlags(PhysBody, CollisionFlags.CF_CHARACTER_OBJECT);

        PhysicsScene.PE.AddObjectToWorld(PhysicsScene.World, PhysBody);

        // PhysicsScene.PE.ForceActivationState(PhysBody, ActivationState.ACTIVE_TAG);
        PhysicsScene.PE.ForceActivationState(PhysBody, ActivationState.DISABLE_DEACTIVATION);
        PhysicsScene.PE.UpdateSingleAabb(PhysicsScene.World, PhysBody);

        // Do this after the object has been added to the world
        PhysBody.collisionType = CollisionType.Avatar;
        PhysBody.ApplyCollisionMask(PhysicsScene);
    }

    // The avatar's movement is controlled by this motor that speeds up and slows down
    //    the avatar seeking to reach the motor's target speed.
    // This motor runs as a prestep action for the avatar so it will keep the avatar
    //    standing as well as moving. Destruction of the avatar will destroy the pre-step action.
    private void SetupMovementMotor()
    {
        // Infinite decay and timescale values so motor only changes current to target values.
        _velocityMotor = new BSVMotor("BSCharacter.Velocity", 
                                            0.2f,                       // time scale
                                            BSMotor.Infinite,           // decay time scale
                                            BSMotor.InfiniteVector,     // friction timescale
                                            1f                          // efficiency
        );
        // _velocityMotor.PhysicsScene = PhysicsScene; // DEBUG DEBUG so motor will output detail log messages.

        RegisterPreStepAction("BSCharactor.Movement", LocalID, delegate(float timeStep)
        {
            // TODO: Decide if the step parameters should be changed depending on the avatar's
            //     state (flying, colliding, ...). There is code in ODE to do this.

            OMV.Vector3 stepVelocity = _velocityMotor.Step(timeStep);

            // If falling, we keep the world's downward vector no matter what the other axis specify.
            if (!Flying && !IsColliding)
            {
                stepVelocity.Z = _velocity.Z;
                // DetailLog("{0},BSCharacter.MoveMotor,taint,overrideStepZWithWorldZ,stepVel={1}", LocalID, stepVelocity);
            }

            // 'stepVelocity' is now the speed we'd like the avatar to move in. Turn that into an instantanous force.
            OMV.Vector3 moveForce = (stepVelocity - _velocity) * Mass;

            // Should we check for move force being small and forcing velocity to zero?

            // Add special movement force to allow avatars to walk up stepped surfaces.
            moveForce += WalkUpStairs();

            // DetailLog("{0},BSCharacter.MoveMotor,move,stepVel={1},vel={2},mass={3},moveForce={4}", LocalID, stepVelocity, _velocity, Mass, moveForce);
            PhysicsScene.PE.ApplyCentralImpulse(PhysBody, moveForce);
        });
    }

    // Decide of the character is colliding with a low object and compute a force to pop the
    //    avatar up so it has a chance of walking up and over the low object.
    private OMV.Vector3 WalkUpStairs()
    {
        OMV.Vector3 ret = OMV.Vector3.Zero;

        // This test is done if moving forward, not flying and is colliding with something.
        // DetailLog("{0},BSCharacter.WalkUpStairs,IsColliding={1},flying={2},targSpeed={3},collisions={4}",
        //                 LocalID, IsColliding, Flying, TargetSpeed, CollisionsLastTick.Count);
        if (IsColliding && !Flying && TargetSpeed > 0.1f /* && ForwardSpeed < 0.1f */)
        {
            // The range near the character's feet where we will consider stairs
            float nearFeetHeightMin = RawPosition.Z - (Size.Z / 2f) + 0.05f;
            float nearFeetHeightMax = nearFeetHeightMin + BSParam.AvatarStepHeight;

            // Look for a collision point that is near the character's feet and is oriented the same as the charactor is
            foreach (KeyValuePair<uint, ContactPoint> kvp in CollisionsLastTick.m_objCollisionList)
            {
                // Don't care about collisions with the terrain
                if (kvp.Key > PhysicsScene.TerrainManager.HighestTerrainID)
                {
                    OMV.Vector3 touchPosition = kvp.Value.Position;
                    // DetailLog("{0},BSCharacter.WalkUpStairs,min={1},max={2},touch={3}",
                    //                 LocalID, nearFeetHeightMin, nearFeetHeightMax, touchPosition);
                    if (touchPosition.Z >= nearFeetHeightMin && touchPosition.Z <= nearFeetHeightMax)
                    {
                        // This contact is within the 'near the feet' range.
                        // The normal should be our contact point to the object so it is pointing away
                        //    thus the difference between our facing orientation and the normal should be small.
                        OMV.Vector3 directionFacing = OMV.Vector3.UnitX * RawOrientation;
                        OMV.Vector3 touchNormal = OMV.Vector3.Normalize(kvp.Value.SurfaceNormal);
                        float diff = Math.Abs(OMV.Vector3.Distance(directionFacing, touchNormal));
                        if (diff < BSParam.AvatarStepApproachFactor)
                        {
                            // Found the stairs contact point. Push up a little to raise the character.
                            float upForce = (touchPosition.Z - nearFeetHeightMin) * Mass * BSParam.AvatarStepForceFactor;
                            ret = new OMV.Vector3(0f, 0f, upForce);

                            // Also move the avatar up for the new height
                            OMV.Vector3 displacement = new OMV.Vector3(0f, 0f, BSParam.AvatarStepHeight / 2f);
                            ForcePosition = RawPosition + displacement;
                        }
                        DetailLog("{0},BSCharacter.WalkUpStairs,touchPos={1},nearFeetMin={2},faceDir={3},norm={4},diff={5},ret={6}",
                                LocalID, touchPosition, nearFeetHeightMin, directionFacing, touchNormal, diff, ret);
                    }
                }
            }
        }

        return ret;
    }

>>>>>>> upstream/master
    public override void RequestPhysicsterseUpdate()
    {
        base.RequestPhysicsterseUpdate();
    }
    // No one calls this method so I don't know what it could possibly mean
    public override bool Stopped { 
        get { return false; } 
    }
    public override Vector3 Size {
        get
        {
            // Avatar capsule size is kept in the scale parameter.
            return new Vector3(_scale.X * 2, _scale.Y * 2, _scale.Z);
        }

<<<<<<< HEAD
        set { 
            // When an avatar's size is set, only the height is changed
            //    and that really only depends on the radius.
            _size = value;
            _scale.Z = (_size.Z * 1.15f) - (_scale.X + _scale.Y);

            // TODO: something has to be done with the avatar's vertical position

=======
        set {
            _size = value;
            // Old versions of ScenePresence passed only the height. If width and/or depth are zero,
            //     replace with the default values.
            if (_size.X == 0f) _size.X = BSParam.AvatarCapsuleDepth;
            if (_size.Y == 0f) _size.Y = BSParam.AvatarCapsuleWidth;

            Scale = ComputeAvatarScale(_size);
>>>>>>> upstream/master
            ComputeAvatarVolumeAndMass();

            Scene.TaintedObject("BSCharacter.setSize", delegate()
            {
<<<<<<< HEAD
                BulletSimAPI.SetObjectScaleMass(Scene.WorldID, LocalID, _scale, _mass, true);
=======
                if (PhysBody.HasPhysicalBody && PhysShape.HasPhysicalShape)
                {
                    PhysicsScene.PE.SetLocalScaling(PhysShape, Scale);
                    UpdatePhysicalMassProperties(RawMass, true);
                    // Make sure this change appears as a property update event
                    PhysicsScene.PE.PushUpdate(PhysBody);
                }
>>>>>>> upstream/master
            });

        } 
    }
<<<<<<< HEAD
    public override PrimitiveBaseShape Shape { 
        set { _pbs = value; 
        } 
=======

    public override PrimitiveBaseShape Shape
    {
        set { BaseShape = value; }
>>>>>>> upstream/master
    }
    public override uint LocalID { 
        set { _localID = value; 
        }
        get { return _localID; }
    }
    public override bool Grabbed { 
        set { _grabbed = value; 
        } 
    }
    public override bool Selected { 
        set { _selected = value; 
        } 
    }
    public override bool IsSelected
    {
        get { return _selected; }
    }
    public override void CrossingFailure() { return; }
    public override void link(PhysicsActor obj) { return; }
    public override void delink() { return; }
    public override void LockAngularMotion(Vector3 axis) { return; }

<<<<<<< HEAD
    public override Vector3 Position { 
        get {
            // _position = BulletSimAPI.GetObjectPosition(Scene.WorldID, _localID);
            return _position; 
        } 
=======
    // Set motion values to zero.
    // Do it to the properties so the values get set in the physics engine.
    // Push the setting of the values to the viewer.
    // Called at taint time!
    public override void ZeroMotion(bool inTaintTime)
    {
        _velocity = OMV.Vector3.Zero;
        _acceleration = OMV.Vector3.Zero;
        _rotationalVelocity = OMV.Vector3.Zero;

        // Zero some other properties directly into the physics engine
        PhysicsScene.TaintedObject(inTaintTime, "BSCharacter.ZeroMotion", delegate()
        {
            if (PhysBody.HasPhysicalBody)
                PhysicsScene.PE.ClearAllForces(PhysBody);
        });
    }
    public override void ZeroAngularMotion(bool inTaintTime)
    {
        _rotationalVelocity = OMV.Vector3.Zero;

        PhysicsScene.TaintedObject(inTaintTime, "BSCharacter.ZeroMotion", delegate()
        {
            if (PhysBody.HasPhysicalBody)
            {
                PhysicsScene.PE.SetInterpolationAngularVelocity(PhysBody, OMV.Vector3.Zero);
                PhysicsScene.PE.SetAngularVelocity(PhysBody, OMV.Vector3.Zero);
                // The next also get rid of applied linear force but the linear velocity is untouched.
                PhysicsScene.PE.ClearForces(PhysBody);
            }
        });
    }


    public override void LockAngularMotion(OMV.Vector3 axis) { return; }

    public override OMV.Vector3 RawPosition
    {
        get { return _position; }
        set { _position = value; }
    }
    public override OMV.Vector3 Position {
        get {
            // Don't refetch the position because this function is called a zillion times
            // _position = PhysicsScene.PE.GetObjectPosition(Scene.World, LocalID);
            return _position;
        }
>>>>>>> upstream/master
        set {
            _position = value;

            Scene.TaintedObject("BSCharacter.setPosition", delegate()
            {
                DetailLog("{0},BSCharacter.SetPosition,taint,pos={1},orient={2}", LocalID, _position, _orientation);
<<<<<<< HEAD
                BulletSimAPI.SetObjectTranslation(Scene.WorldID, _localID, _position, _orientation);
            });
        } 
=======
                ForcePosition = _position;
            });
        }
    }
    public override OMV.Vector3 ForcePosition {
        get {
            _position = PhysicsScene.PE.GetPosition(PhysBody);
            return _position;
        }
        set {
            _position = value;
            if (PhysBody.HasPhysicalBody)
            {
                PositionSanityCheck();
                PhysicsScene.PE.SetTranslation(PhysBody, _position, _orientation);
            }
        }
>>>>>>> upstream/master
    }

    // Check that the current position is sane and, if not, modify the position to make it so.
    // Check for being below terrain and being out of bounds.
    // Returns 'true' of the position was made sane by some action.
    private bool PositionSanityCheck()
    {
        bool ret = false;
<<<<<<< HEAD
        
        // If below the ground, move the avatar up
        float terrainHeight = Scene.TerrainManager.GetTerrainHeightAtXYZ(_position);
=======

        // TODO: check for out of bounds
        if (!PhysicsScene.TerrainManager.IsWithinKnownTerrain(RawPosition))
        {
            // The character is out of the known/simulated area.
            // Upper levels of code will handle the transition to other areas so, for
            //     the time, we just ignore the position.
            return ret;
        }

        // If below the ground, move the avatar up
        float terrainHeight = PhysicsScene.TerrainManager.GetTerrainHeightAtXYZ(RawPosition);
>>>>>>> upstream/master
        if (Position.Z < terrainHeight)
        {
            DetailLog("{0},BSCharacter.PositionAdjustUnderGround,call,pos={1},terrain={2}", LocalID, _position, terrainHeight);
            _position.Z = terrainHeight + 2.0f;
            ret = true;
        }

        return ret;
    }

    // A version of the sanity check that also makes sure a new position value is
    //    pushed back to the physics engine. This routine would be used by anyone
    //    who is not already pushing the value.
    private bool PositionSanityCheck2()
    {
        bool ret = false;
        if (PositionSanityCheck())
        {
            // The new position value must be pushed into the physics engine but we can't
            //    just assign to "Position" because of potential call loops.
            Scene.TaintedObject("BSCharacter.PositionSanityCheck", delegate()
            {
                DetailLog("{0},BSCharacter.PositionSanityCheck,taint,pos={1},orient={2}", LocalID, _position, _orientation);
<<<<<<< HEAD
                BulletSimAPI.SetObjectTranslation(Scene.WorldID, _localID, _position, _orientation);
=======
                if (PhysBody.HasPhysicalBody)
                    PhysicsScene.PE.SetTranslation(PhysBody, _position, _orientation);
>>>>>>> upstream/master
            });
            ret = true;
        }
        return ret;
    }

    public override float Mass { 
        get { 
            return _mass; 
        } 
    }

    // used when we only want this prim's mass and not the linkset thing
<<<<<<< HEAD
    public override float MassRaw { get {return _mass; } }
=======
    public override float RawMass { 
        get {return _mass; }
    }
    public override void UpdatePhysicalMassProperties(float physMass, bool inWorld)
    {
        OMV.Vector3 localInertia = PhysicsScene.PE.CalculateLocalInertia(PhysShape, physMass);
        PhysicsScene.PE.SetMassProps(PhysBody, physMass, localInertia);
    }
>>>>>>> upstream/master

    public override Vector3 Force { 
        get { return _force; } 
        set {
            _force = value;
            // m_log.DebugFormat("{0}: Force = {1}", LogHeader, _force);
            Scene.TaintedObject("BSCharacter.SetForce", delegate()
            {
                DetailLog("{0},BSCharacter.setForce,taint,force={1}", LocalID, _force);
<<<<<<< HEAD
                BulletSimAPI.SetObjectForce(Scene.WorldID, LocalID, _force);
=======
                if (PhysBody.HasPhysicalBody)
                    PhysicsScene.PE.SetObjectForce(PhysBody, _force);
>>>>>>> upstream/master
            });
        } 
    }

    public override int VehicleType { 
        get { return 0; } 
        set { return; } 
    }
    public override void VehicleFloatParam(int param, float value) { }
    public override void VehicleVectorParam(int param, Vector3 value) {}
    public override void VehicleRotationParam(int param, Quaternion rotation) { }
    public override void VehicleFlags(int param, bool remove) { }

    // Allows the detection of collisions with inherently non-physical prims. see llVolumeDetect for more
    public override void SetVolumeDetect(int param) { return; }

<<<<<<< HEAD
    public override Vector3 GeometricCenter { get { return Vector3.Zero; } }
    public override Vector3 CenterOfMass { get { return Vector3.Zero; } }
    public override Vector3 Velocity { 
        get { return _velocity; } 
=======
    public override OMV.Vector3 GeometricCenter { get { return OMV.Vector3.Zero; } }
    public override OMV.Vector3 CenterOfMass { get { return OMV.Vector3.Zero; } }

    // Sets the target in the motor. This starts the changing of the avatar's velocity.
    public override OMV.Vector3 TargetVelocity
    {
        get
        {
            return _velocityMotor.TargetValue;
        }
        set
        {
            DetailLog("{0},BSCharacter.setTargetVelocity,call,vel={1}", LocalID, value);
            OMV.Vector3 targetVel = value;
            if (_setAlwaysRun)
                targetVel *= BSParam.AvatarAlwaysRunFactor;

            PhysicsScene.TaintedObject("BSCharacter.setTargetVelocity", delegate()
            {
                _velocityMotor.Reset();
                _velocityMotor.SetTarget(targetVel);
                _velocityMotor.SetCurrent(_velocity);
                _velocityMotor.Enabled = true;
            });
        }
    }
    public override OMV.Vector3 RawVelocity
    {
        get { return _velocity; }
        set { _velocity = value; }
    }
    // Directly setting velocity means this is what the user really wants now.
    public override OMV.Vector3 Velocity {
        get { return _velocity; }
>>>>>>> upstream/master
        set {
            _velocity = value;
            // m_log.DebugFormat("{0}: set velocity = {1}", LogHeader, _velocity);
            Scene.TaintedObject("BSCharacter.setVelocity", delegate()
            {
                _velocityMotor.Reset();
                _velocityMotor.SetCurrent(_velocity);
                _velocityMotor.SetTarget(_velocity);
                // Even though the motor is initialized, it's not used and the velocity goes straight into the avatar.
                _velocityMotor.Enabled = false;

                DetailLog("{0},BSCharacter.setVelocity,taint,vel={1}", LocalID, _velocity);
                BulletSimAPI.SetObjectVelocity(Scene.WorldID, _localID, _velocity);
            });
        } 
    }
<<<<<<< HEAD
    public override Vector3 Torque { 
        get { return _torque; } 
        set { _torque = value; 
        } 
=======
    public override OMV.Vector3 ForceVelocity {
        get { return _velocity; }
        set {
            PhysicsScene.AssertInTaintTime("BSCharacter.ForceVelocity");

            _velocity = value;
            // Depending on whether the avatar is moving or not, change the friction
            //    to keep the avatar from slipping around
            if (_velocity.Length() == 0)
            {
                if (_currentFriction != BSParam.AvatarStandingFriction)
                {
                    _currentFriction = BSParam.AvatarStandingFriction;
                    if (PhysBody.HasPhysicalBody)
                        PhysicsScene.PE.SetFriction(PhysBody, _currentFriction);
                }
            }
            else
            {
                if (_currentFriction != BSParam.AvatarFriction)
                {
                    _currentFriction = BSParam.AvatarFriction;
                    if (PhysBody.HasPhysicalBody)
                        PhysicsScene.PE.SetFriction(PhysBody, _currentFriction);
                }
            }

            PhysicsScene.PE.SetLinearVelocity(PhysBody, _velocity);
            PhysicsScene.PE.Activate(PhysBody, true);
        }
>>>>>>> upstream/master
    }
    public override float CollisionScore { 
        get { return _collisionScore; } 
        set { _collisionScore = value; 
        } 
    }
    public override Vector3 Acceleration { 
        get { return _acceleration; }
        set { _acceleration = value; }
    }
    public override Quaternion Orientation { 
        get { return _orientation; } 
        set {
<<<<<<< HEAD
            _orientation = value;
            // m_log.DebugFormat("{0}: set orientation to {1}", LogHeader, _orientation);
            Scene.TaintedObject("BSCharacter.setOrientation", delegate()
            {
                // _position = BulletSimAPI.GetObjectPosition(Scene.WorldID, _localID);
                BulletSimAPI.SetObjectTranslation(Scene.WorldID, _localID, _position, _orientation);
            });
        } 
    }
    public override int PhysicsActorType { 
        get { return _physicsActorType; } 
        set { _physicsActorType = value; 
        } 
=======
            // Orientation is set zillions of times when an avatar is walking. It's like
            //      the viewer doesn't trust us.
            if (_orientation != value)
            {
                _orientation = value;
                PhysicsScene.TaintedObject("BSCharacter.setOrientation", delegate()
                {
                    ForceOrientation = _orientation;
                });
            }
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
            {
                // _position = PhysicsScene.PE.GetPosition(BSBody);
                PhysicsScene.PE.SetTranslation(PhysBody, _position, _orientation);
            }
        }
>>>>>>> upstream/master
    }
    public override bool IsPhysical { 
        get { return _isPhysical; } 
        set { _isPhysical = value;
        } 
    }
    public override bool Flying { 
        get { return _flying; } 
        set {
            _flying = value;

            // simulate flying by changing the effect of gravity
            this.Buoyancy = ComputeBuoyancyFromFlying(_flying);
        } 
    }
    // Flying is implimented by changing the avatar's buoyancy.
    // Would this be done better with a vehicle type?
    private float ComputeBuoyancyFromFlying(bool ifFlying) {
        return ifFlying ? 1f : 0f;
    }
    public override bool 
        SetAlwaysRun { 
        get { return _setAlwaysRun; } 
        set { _setAlwaysRun = value; } 
    }
    public override bool ThrottleUpdates { 
        get { return _throttleUpdates; } 
        set { _throttleUpdates = value; } 
    }
<<<<<<< HEAD
    public override bool IsColliding {
        get { return (_collidingStep == Scene.SimulationStep); } 
        set { _isColliding = value; } 
    }
    public override bool CollidingGround {
        get { return (_collidingGroundStep == Scene.SimulationStep); } 
        set { _collidingGround = value; } 
    }
    public override bool CollidingObj { 
        get { return _collidingObj; } 
        set { _collidingObj = value; } 
=======
    public override bool FloatOnWater {
        set {
            _floatOnWater = value;
            PhysicsScene.TaintedObject("BSCharacter.setFloatOnWater", delegate()
            {
                if (PhysBody.HasPhysicalBody)
                {
                    if (_floatOnWater)
                        CurrentCollisionFlags = PhysicsScene.PE.AddToCollisionFlags(PhysBody, CollisionFlags.BS_FLOATS_ON_WATER);
                    else
                        CurrentCollisionFlags = PhysicsScene.PE.RemoveFromCollisionFlags(PhysBody, CollisionFlags.BS_FLOATS_ON_WATER);
                }
            });
        }
>>>>>>> upstream/master
    }
    public override bool FloatOnWater { 
        set { _floatOnWater = value; } 
    }
    public override Vector3 RotationalVelocity { 
        get { return _rotationalVelocity; } 
        set { _rotationalVelocity = value; } 
    }
    public override bool Kinematic { 
        get { return _kinematic; } 
        set { _kinematic = value; } 
    }
    // neg=fall quickly, 0=1g, 1=0g, pos=float up
    public override float Buoyancy { 
        get { return _buoyancy; } 
        set { _buoyancy = value; 
            Scene.TaintedObject("BSCharacter.setBuoyancy", delegate()
            {
                DetailLog("{0},BSCharacter.setBuoyancy,taint,buoy={1}", LocalID, _buoyancy);
                BulletSimAPI.SetObjectBuoyancy(Scene.WorldID, LocalID, _buoyancy);
            });
<<<<<<< HEAD
        } 
=======
        }
    }
    public override float ForceBuoyancy {
        get { return _buoyancy; }
        set { 
            PhysicsScene.AssertInTaintTime("BSCharacter.ForceBuoyancy");

            _buoyancy = value;
            DetailLog("{0},BSCharacter.setForceBuoyancy,taint,buoy={1}", LocalID, _buoyancy);
            // Buoyancy is faked by changing the gravity applied to the object
            float grav = PhysicsScene.Params.gravity * (1f - _buoyancy);
            if (PhysBody.HasPhysicalBody)
                PhysicsScene.PE.SetGravity(PhysBody, new OMV.Vector3(0f, 0f, grav));
        }
>>>>>>> upstream/master
    }

    // Used for MoveTo
    public override Vector3 PIDTarget { 
        set { _PIDTarget = value; } 
    }
    public override bool PIDActive { 
        set { _usePID = value; } 
    }
    public override float PIDTau { 
        set { _PIDTau = value; } 
    }

    // Used for llSetHoverHeight and maybe vehicle height
    // Hover Height will override MoveTo target's Z
    public override bool PIDHoverActive { 
        set { _useHoverPID = value; }
    }
    public override float PIDHoverHeight { 
        set { _PIDHoverHeight = value; }
    }
    public override PIDHoverType PIDHoverType { 
        set { _PIDHoverType = value; }
    }
    public override float PIDHoverTau { 
        set { _PIDHoverTao = value; }
    }

    // For RotLookAt
    public override Quaternion APIDTarget { set { return; } }
    public override bool APIDActive { set { return; } }
    public override float APIDStrength { set { return; } }
    public override float APIDDamping { set { return; } }

<<<<<<< HEAD
    public override void AddForce(Vector3 force, bool pushforce) { 
        if (force.IsFinite())
        {
            _force.X += force.X;
            _force.Y += force.Y;
            _force.Z += force.Z;
            // m_log.DebugFormat("{0}: AddForce. adding={1}, newForce={2}", LogHeader, force, _force);
            Scene.TaintedObject("BSCharacter.AddForce", delegate()
            {
                DetailLog("{0},BSCharacter.setAddForce,taint,addedForce={1}", LocalID, _force);
                BulletSimAPI.SetObjectForce2(BSBody.Ptr, _force);
=======
    public override void AddForce(OMV.Vector3 force, bool pushforce)
    {
        // Since this force is being applied in only one step, make this a force per second.
        OMV.Vector3 addForce = force / PhysicsScene.LastTimeStep;
        AddForce(addForce, pushforce, false);
    }
    private void AddForce(OMV.Vector3 force, bool pushforce, bool inTaintTime) {
        if (force.IsFinite())
        {
            float magnitude = force.Length();
            if (magnitude > BSParam.MaxAddForceMagnitude)
            {
                // Force has a limit
                force = force / magnitude * BSParam.MaxAddForceMagnitude;
            }

            OMV.Vector3 addForce = force;
            // DetailLog("{0},BSCharacter.addForce,call,force={1}", LocalID, addForce);

            PhysicsScene.TaintedObject(inTaintTime, "BSCharacter.AddForce", delegate()
            {
                // Bullet adds this central force to the total force for this tick
                // DetailLog("{0},BSCharacter.addForce,taint,force={1}", LocalID, addForce);
                if (PhysBody.HasPhysicalBody)
                {
                    PhysicsScene.PE.ApplyCentralForce(PhysBody, addForce);
                }
>>>>>>> upstream/master
            });
        }
        else
        {
            m_log.WarnFormat("{0}: Got a NaN force applied to a character. LocalID={1}", LogHeader, LocalID);
            return;
        }
    }

    public override void AddAngularForce(Vector3 force, bool pushforce) { 
    }
    public override void SetMomentum(Vector3 momentum) { 
    }

<<<<<<< HEAD
    // Turn on collision events at a rate no faster than one every the given milliseconds
    public override void SubscribeEvents(int ms) {
        _subscribedEventsMs = ms;
        if (ms > 0)
        {
            // make sure first collision happens
            _nextCollisionOkTime = Util.EnvironmentTickCount() - _subscribedEventsMs;

            Scene.TaintedObject("BSCharacter.SubscribeEvents", delegate()
            {
                BulletSimAPI.AddToCollisionFlags2(BSBody.Ptr, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
            });
        }
    }

    public override void ZeroMotion()
    {
        return;
    }

    // Stop collision events
    public override void UnSubscribeEvents() { 
        _subscribedEventsMs = 0;
        Scene.TaintedObject("BSCharacter.UnSubscribeEvents", delegate()
        {
            BulletSimAPI.RemoveFromCollisionFlags2(BSBody.Ptr, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
        });
    }
    // Return 'true' if someone has subscribed to events
    public override bool SubscribedEvents() {
        return (_subscribedEventsMs > 0);
=======
    private OMV.Vector3 ComputeAvatarScale(OMV.Vector3 size)
    {
        OMV.Vector3 newScale;
        
        // Bullet's capsule total height is the "passed height + radius * 2";
        // The base capsule is 1 diameter and 2 height (passed radius=0.5, passed height = 1)
        // The number we pass in for 'scaling' is the multiplier to get that base
        //     shape to be the size desired.
        // So, when creating the scale for the avatar height, we take the passed height
        //     (size.Z) and remove the caps.
        // Another oddity of the Bullet capsule implementation is that it presumes the Y
        //     dimension is the radius of the capsule. Even though some of the code allows
        //     for a asymmetrical capsule, other parts of the code presume it is cylindrical.

        // Scale is multiplier of radius with one of "0.5"
        newScale.X = size.X / 2f;
        newScale.Y = size.Y / 2f;

        // The total scale height is the central cylindar plus the caps on the two ends.
        newScale.Z = (size.Z + (Math.Min(size.X, size.Y) * 2)) / 2f;
        // If smaller than the endcaps, just fake like we're almost that small
        if (newScale.Z < 0)
            newScale.Z = 0.1f;

        return newScale;
>>>>>>> upstream/master
    }

    // set _avatarVolume and _mass based on capsule size, _density and _scale
    private void ComputeAvatarVolumeAndMass()
    {
        _avatarVolume = (float)(
                        Math.PI
<<<<<<< HEAD
                        * _scale.X
                        * _scale.Y  // the area of capsule cylinder
                        * _scale.Z  // times height of capsule cylinder
                      + 1.33333333f
                        * Math.PI
                        * _scale.X
                        * Math.Min(_scale.X, _scale.Y)
                        * _scale.Y  // plus the volume of the capsule end caps
=======
                        * Size.X / 2f
                        * Size.Y / 2f    // the area of capsule cylinder
                        * Size.Z         // times height of capsule cylinder
                      + 1.33333333f
                        * Math.PI
                        * Size.X / 2f
                        * Math.Min(Size.X, Size.Y) / 2
                        * Size.Y / 2f    // plus the volume of the capsule end caps
>>>>>>> upstream/master
                        );
        _mass = _density * _avatarVolume;
    }

    // The physics engine says that properties have updated. Update same and inform
    // the world that things have changed.
    public override void UpdateProperties(EntityProperties entprop)
    {
        _position = entprop.Position;
        _orientation = entprop.Rotation;
        _velocity = entprop.Velocity;
        _acceleration = entprop.Acceleration;
        _rotationalVelocity = entprop.RotationalVelocity;
<<<<<<< HEAD
        // Avatars don't report their changes the usual way. Changes are checked for in the heartbeat loop.
        // base.RequestPhysicsterseUpdate();

        // Do some sanity checking for the avatar. Make sure it's above ground and inbounds.
        PositionSanityCheck2();

        float heightHere = Scene.TerrainManager.GetTerrainHeightAtXYZ(_position);   // only for debug
        DetailLog("{0},BSCharacter.UpdateProperties,call,pos={1},orient={2},vel={3},accel={4},rotVel={5},terrain={6}",
                LocalID, _position, _orientation, _velocity, _acceleration, _rotationalVelocity, heightHere);
    }
=======

        // Do some sanity checking for the avatar. Make sure it's above ground and inbounds.
        if (PositionSanityCheck(true))
        {
            entprop.Position = _position;
        }
>>>>>>> upstream/master

    // Called by the scene when a collision with this object is reported
    // The collision, if it should be reported to the character, is placed in a collection
    //   that will later be sent to the simulator when SendCollisions() is called.
    CollisionEventUpdate collisionCollection = null;
    public override bool Collide(uint collidingWith, BSPhysObject collidee, Vector3 contactPoint, Vector3 contactNormal, float pentrationDepth)
    {
        bool ret = false;

<<<<<<< HEAD
        // The following makes IsColliding() and IsCollidingGround() work
        _collidingStep = Scene.SimulationStep;
        if (collidingWith <= Scene.TerrainManager.HighestTerrainID)
        {
            _collidingGroundStep = Scene.SimulationStep;
        }
        // DetailLog("{0},BSCharacter.Collison,call,with={1}", LocalID, collidingWith);

        // throttle collisions to the rate specified in the subscription
        if (SubscribedEvents()) {
            int nowTime = Scene.SimulationNowTime;
            if (nowTime >= _nextCollisionOkTime) {
                _nextCollisionOkTime = nowTime + _subscribedEventsMs;
=======
        // Tell the linkset about value changes
        Linkset.UpdateProperties(this, true);
>>>>>>> upstream/master

                if (collisionCollection == null)
                    collisionCollection = new CollisionEventUpdate();
                collisionCollection.AddCollider(collidingWith, new ContactPoint(contactPoint, contactNormal, pentrationDepth));
                ret = true;
            }
        }
        return ret;
    }

    public override void SendCollisions()
    {
        /*
        if (collisionCollection != null && collisionCollection.Count > 0)
        {
            base.SendCollisionUpdate(collisionCollection);
            collisionCollection = null;
        }
         */
        // Kludge to make a collision call even if there are no collisions.
        // This causes the avatar animation to get updated.
        if (collisionCollection == null)
            collisionCollection = new CollisionEventUpdate();
        base.SendCollisionUpdate(collisionCollection);
        // If there were any collisions in the collection, make sure we don't use the
        //    same instance next time.
        if (collisionCollection.Count > 0)
            collisionCollection = null;
        // End kludge
    }

    // Invoke the detailed logger and output something if it's enabled.
    private void DetailLog(string msg, params Object[] args)
    {
        Scene.PhysicsLogging.Write(msg, args);
    }
}
}

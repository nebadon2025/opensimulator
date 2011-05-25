/*
 * Copyright (c) Intel Corporation
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Intel Corporation nor the
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
public sealed class BSPrim : PhysicsActor
{
    private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly string LogHeader = "[BULLETS PRIM]";

    private IMesh _mesh;
    private PrimitiveBaseShape _pbs;
    private ShapeData.PhysicsShapeType _shapeType;
    private ulong _hullKey;
    private List<ConvexResult> _hulls;

    private BSScene _scene;
    private String _avName;
    private uint _localID = 0;

    private OMV.Vector3 _size;
    private OMV.Vector3 _scale;
    private bool _stopped;
    private bool _grabbed;
    private bool _isSelected;
    private bool _isVolumeDetect;
    private OMV.Vector3 _position;
    private float _mass;
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
    private bool _setAlwaysRun;
    private bool _throttleUpdates;
    private bool _isColliding;
    private bool _collidingGround;
    private bool _collidingObj;
    private bool _floatOnWater;
    private OMV.Vector3 _rotationalVelocity;
    private bool _kinematic;
    private float _buoyancy;

    private List<BSPrim> _childrenPrims;
    private BSPrim _parentPrim;

    private int _subscribedEventsMs = 0;
    long _collidingStep;
    long _collidingGroundStep;

    private OMV.Vector3 _PIDTarget;
    private bool _usePID;
    private float _PIDTau;
    private bool _useHoverPID;
    private float _PIDHoverHeight;
    private PIDHoverType _PIDHoverType;
    private float _PIDHoverTao;

    public BSPrim(uint localID, String primName, BSScene parent_scene, OMV.Vector3 pos, OMV.Vector3 size,
                       OMV.Quaternion rotation, IMesh mesh, PrimitiveBaseShape pbs, bool pisPhysical)
    {
        m_log.DebugFormat("{0}: BSPrim creation of {1}, id={2}", LogHeader, primName, localID);
        _localID = localID;
        _avName = primName;
        _scene = parent_scene;
        _position = pos;
        _size = size;
        _scale = new OMV.Vector3(1f, 1f, 1f);   // the scale will be set by CreateGeom depending on object type
        _orientation = rotation;
        _mesh = mesh;
        _hullKey = 0;
        _pbs = pbs;
        _isPhysical = pisPhysical;
        _isVolumeDetect = false;
        _subscribedEventsMs = 0;
        _friction = _scene.DefaultFriction; // TODO: compute based on object material
        _density = _scene.DefaultDensity; // TODO: compute based on object material
        _parentPrim = null;     // not a child or a parent
        _childrenPrims = new List<BSPrim>();
        if (_isPhysical)
            _mass = CalculateMass();
        else
            _mass = 0f;
        // do the actual object creation at taint time
        _scene.TaintedObject(delegate()
        {
            CreateGeom();
            CreateObject();
        });
    }

    // called when this prim is being destroyed and we should free all the resources
    public void Destroy()
    {
        // m_log.DebugFormat("{0}: Destroy", LogHeader);
        _scene.TaintedObject(delegate()
        {
            BulletSimAPI.DestroyObject(_scene.WorldID, _localID);
        });
    }
    
    public override bool Stopped { 
        get { return _stopped; } 
    }
    public override OMV.Vector3 Size { 
        get { return _size; } 
        set {
            _size = value;
            m_log.DebugFormat("{0}: Size={1}", LogHeader, _size);
            _scene.TaintedObject(delegate()
            {
                if (_isPhysical) _mass = CalculateMass();   // changing size changes the mass
                BulletSimAPI.SetObjectScaleMass(_scene.WorldID, _localID, _scale, _mass, _isPhysical);
                RecreateGeomAndObject();
                // SyncUpdated = true;
            });
        } 
    }
    public override PrimitiveBaseShape Shape { 
        set {
            _pbs = value;
            m_log.DebugFormat("{0}: set Shape", LogHeader);
            _scene.TaintedObject(delegate()
            {
                if (_isPhysical) _mass = CalculateMass();   // changing the shape changes the mass
                RecreateGeomAndObject();
                // SyncUpdated = true;
            });
        } 
    }
    public override uint LocalID { 
        set { _localID = value; }
        get { return _localID; }
    }
    public override bool Grabbed { 
        set { _grabbed = value; 
            m_log.DebugFormat("{0}: Grabbed={1}", LogHeader, _grabbed);
        } 
    }
    public override bool Selected { 
        set {
            _isSelected = value;
            _scene.TaintedObject(delegate()
            {
                m_log.DebugFormat("{0}: Selected={1}, localID={2}", LogHeader, _isSelected, _localID);
                SetObjectDynamic();
                // SyncUpdated = true;
            });
        } 
    }
    public override void CrossingFailure() { return; }

    // link me to the specified parent
    public override void link(PhysicsActor obj) {
        BSPrim parent = (BSPrim)obj;
        m_log.DebugFormat("{0}: link {1} to {2}", LogHeader, _localID, obj.LocalID);
        // TODO: decide if this parent checking needs to happen at taint time
        if (_parentPrim == null)
        {
            if (parent != null)
            {
                // I don't have a parent so I am joining a linkset
                parent.AddChildToLinkset(this);
            }
        }
        else
        {
            // I already have a parent, is parenting changing?
            if (parent != _parentPrim)
            {
                if (parent == null)
                {
                    // we are being removed from a linkset
                    _parentPrim.RemoveChildFromLinkset(this);
                }
                else
                {
                    // asking to reparent a prim should not happen
                    m_log.ErrorFormat("{0}: Reparenting a prim. ", LogHeader);
                }
            }
        }
        return; 
    }

    // delink me from my linkset
    public override void delink() {
        // TODO: decide if this parent checking needs to happen at taint time
        m_log.DebugFormat("{0}: delink {1}", LogHeader, _localID);
        if (_parentPrim != null)
        {
            _parentPrim.RemoveChildFromLinkset(this);
        }
        return; 
    }

    public void AddChildToLinkset(BSPrim pchild)
    {
        BSPrim child = pchild;
        _scene.TaintedObject(delegate()
        {
            if (!_childrenPrims.Contains(child))
            {
                _childrenPrims.Add(child);
                child.ParentPrim = this;    // the child has gained a parent
                RecreateGeomAndObject();    // rebuild my shape with the new child added
            }
        });
        return;
    }

    public void RemoveChildFromLinkset(BSPrim pchild)
    {
        BSPrim child = pchild;
        _scene.TaintedObject(delegate()
        {
            if (_childrenPrims.Contains(child))
            {
                _childrenPrims.Remove(child);
                child.ParentPrim = null;    // the child has lost its parent
                RecreateGeomAndObject();    // rebuild my shape with the child removed
            }
            else
            {
                m_log.ErrorFormat("{0}: Asked to remove child from linkset that was not in linkset");
            }
        });
        return;
    }

    public BSPrim ParentPrim
    {
        set { _parentPrim = value; }
    }

    public ulong HullKey
    {
        get { return _hullKey; }
    }

    // return true if we are the root of a linkset (there are children to manage)
    public bool IsRootOfLinkset
    {
        get { return (_parentPrim == null && _childrenPrims.Count != 0); }
    }

    public override void LockAngularMotion(OMV.Vector3 axis) { return; }

    public override OMV.Vector3 Position { 
        get { 
            // _position = BulletSimAPI.GetObjectPosition(_scene.WorldID, _localID);
            return _position; 
        } 
        set {
            _position = value;
            _scene.TaintedObject(delegate()
            {
                BulletSimAPI.SetObjectTranslation(_scene.WorldID, _localID, _position, _orientation);
                // SyncUpdated = true;
            });
        } 
    }
    public override float Mass { 
        get { return _mass; } 
    }
    public override OMV.Vector3 Force { 
        get { return _force; } 
        set {
            _force = value;
            m_log.DebugFormat("{0}: set Force. f={1}", LogHeader, _force);
            _scene.TaintedObject(delegate()
            {
                BulletSimAPI.SetObjectForce(_scene.WorldID, _localID, _force);
                // SyncUpdated = true;
            });
        } 
    }

    public override int VehicleType { 
        get { return 0; } 
        set { return; } 
    }
    public override void VehicleFloatParam(int param, float value) { }
    public override void VehicleVectorParam(int param, OMV.Vector3 value) {}
    public override void VehicleRotationParam(int param, OMV.Quaternion rotation) { }
    public override void VehicleFlags(int param, bool remove) { }

    // Allows the detection of collisions with inherently non-physical prims. see llVolumeDetect for more
    public override void SetVolumeDetect(int param) {
        bool newValue = (param != 0);
        if (_isVolumeDetect != newValue)
        {
            _isVolumeDetect = newValue;
            _scene.TaintedObject(delegate()
            {
                // make the object ghostly or not (walk throughable)
                BulletSimAPI.SetObjectGhost(_scene.WorldID, LocalID, _isVolumeDetect);
                // set whether we hear about collisions
                BulletSimAPI.SetObjectCollidable(_scene.WorldID, LocalID, !IsPhantom);
            });
        }
        return; 
    }

    public override OMV.Vector3 GeometricCenter { get { return OMV.Vector3.Zero; } }
    public override OMV.Vector3 CenterOfMass { get { return OMV.Vector3.Zero; } }
    public override OMV.Vector3 Velocity { 
        get { return _velocity; } 
        set { _velocity = value; 
            // SyncUpdated = true;
        } 
    }
    public override OMV.Vector3 Torque { 
        get { return _torque; } 
        set { _torque = value; 
            // SyncUpdated = true;
        } 
    }
    public override float CollisionScore { 
        get { return _collisionScore; } 
        set { _collisionScore = value; 
            // SyncUpdated = true;
        } 
    }
    public override OMV.Vector3 Acceleration { 
        get { return _acceleration; } 
    }
    public override OMV.Quaternion Orientation { 
        get { return _orientation; } 
        set {
            _orientation = value;
            _scene.TaintedObject(delegate()
            {
                _position = BulletSimAPI.GetObjectPosition(_scene.WorldID, _localID);
                BulletSimAPI.SetObjectTranslation(_scene.WorldID, _localID, _position, _orientation);
                // m_log.DebugFormat("{0}: set orientation: {1}", LogHeader, _orientation);
                // SyncUpdated = true;
            });
        } 
    }
    public override int PhysicsActorType { 
        get { return _physicsActorType; } 
        set { _physicsActorType = value; 
            // SyncUpdated = true;
        } 
    }
    public override bool IsPhysical { 
        get { return _isPhysical; } 
        set {
            _isPhysical = value;
            _scene.TaintedObject(delegate()
            {
                SetObjectDynamic();
                m_log.DebugFormat("{0}: ID={1}, IsPhysical={2}, IsSelected={3}, mass={4}", LogHeader, _localID, _isPhysical, _isSelected, _mass);
                // SyncUpdated = true;
            });
        } 
    }
    // make gravity work if the object is physical and not selected
    // no locking here because only called when it is safe
    private void SetObjectDynamic()
    {
        // a selected object is not physical
        if (_isSelected || !_isPhysical)
        {
            _mass = 0f;  // non-physical things work best with a mass of zero
            BulletSimAPI.SetObjectDynamic(_scene.WorldID, _localID, false, _mass);
        }
        else
        {
            _mass = CalculateMass();
            BulletSimAPI.SetObjectDynamic(_scene.WorldID, _localID, true, _mass);
        }
    }
    public override bool Flying { 
        get { return _flying; } 
        set {
            _flying = value;
            _scene.TaintedObject(delegate()
            {
                BulletSimAPI.SetObjectFlying(_scene.WorldID, LocalID, _flying);
                // SyncUpdated = true;
            });
        } 
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
    public override bool IsColliding { 
        get { return _isColliding; } 
        set { _isColliding = value; } 
    }
    public override bool CollidingGround { 
        get { return _collidingGround; } 
        set { _collidingGround = value; } 
    }
    public override bool CollidingObj { 
        get { return _collidingObj; } 
        set { _collidingObj = value; } 
    }
    public bool IsPhantom {
        get {
            // SceneObjectPart removes phantom objects from the physics scene
            // so, although we could implement touching and such, we never
            // are invoked as a phantom object
            return false;
        }
    }
    public override bool FloatOnWater { 
        set { _floatOnWater = value; } 
    }
    public override OMV.Vector3 RotationalVelocity { 
        get { return _rotationalVelocity; } 
        set { _rotationalVelocity = value; 
            m_log.DebugFormat("{0}: RotationalVelocity={1}", LogHeader, _rotationalVelocity);
        } 
    }
    public override bool Kinematic { 
        get { return _kinematic; } 
        set { _kinematic = value; 
            m_log.DebugFormat("{0}: Kinematic={1}", LogHeader, _kinematic);
        } 
    }
    public override float Buoyancy { 
        get { return _buoyancy; } 
        set { _buoyancy = value; } 
    }

    // Used for MoveTo
    public override OMV.Vector3 PIDTarget { 
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
    public override OMV.Quaternion APIDTarget { set { return; } }
    public override bool APIDActive { set { return; } }
    public override float APIDStrength { set { return; } }
    public override float APIDDamping { set { return; } }

    public override void AddForce(OMV.Vector3 force, bool pushforce) {
        if (force.IsFinite())
        {
            _force.X += force.X;
            _force.Y += force.Y;
            _force.Z += force.Z;
        }
        else
        {
            m_log.WarnFormat("{0}: Got a NaN force applied to a Character", LogHeader);
        }
        _scene.TaintedObject(delegate()
        {
            BulletSimAPI.SetObjectForce(_scene.WorldID, _localID, _force);
        });
    }

    public override void AddAngularForce(OMV.Vector3 force, bool pushforce) { 
        m_log.DebugFormat("{0}: AddAngularForce. f={1}, push={2}", LogHeader, force, pushforce);
    }
    public override void SetMomentum(OMV.Vector3 momentum) { 
    }
    public override void SubscribeEvents(int ms) { 
        _subscribedEventsMs = ms;
    }
    public override void UnSubscribeEvents() { 
        _subscribedEventsMs = 0;
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

        if (returnMass <= 0)
            returnMass = 0.0001f;//ckrinke: Mass must be greater then zero.

        if (IsRootOfLinkset)
        {
            foreach (BSPrim prim in _childrenPrims)
            {
                returnMass += prim.CalculateMass();
            }
        }

        if (returnMass > _scene.maximumMassObject)
            returnMass = _scene.maximumMassObject;
        return returnMass;
    }// end CalculateMass
    #endregion Mass Calculation

    // Create the geometry information in Bullet for later use
    // No locking here because this is done when we know physics is not simulating
    private void CreateGeom()
    {
        if (_mesh == null)
        {
            // the mesher thought this was too simple to mesh. Use a native Bullet collision shape.
            if (_pbs.ProfileShape == ProfileShape.HalfCircle && _pbs.PathCurve == (byte)Extrusion.Curve1)
            {
                if (_size.X == _size.Y && _size.Y == _size.Z && _size.X == _size.Z)
                {
                    m_log.DebugFormat("{0}: CreateGeom: mesh null. Defaulting to sphere of size {1}", LogHeader, _size);
                    _shapeType = ShapeData.PhysicsShapeType.SHAPE_SPHERE;
                    // Bullet native objects are scaled by the Bullet engine so pass the size in
                    _scale = _size;
                }
            }
            else
            {
                m_log.DebugFormat("{0}: CreateGeom: mesh null. Defaulting to box of size {1}", LogHeader, _size);
                _shapeType = ShapeData.PhysicsShapeType.SHAPE_BOX;
                _scale = _size;
            }
        }
        else
        {
            if (_hullKey != 0)
            {
                m_log.DebugFormat("{0}: CreateGeom: deleting old hull. Key={1}", LogHeader, _hullKey);
                BulletSimAPI.DestroyHull(_scene.WorldID, _hullKey);
                _hullKey = 0;
                _hulls.Clear();
            }

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

            // Convert the vertices and indices for passing to unmanaged
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
            _hullKey = (ulong)_pbs.GetHashCode();
            // m_log.DebugFormat("{0}: CreateGeom: calling CreateHull. key={1}, hulls={2}", LogHeader, _hullKey, hullCount);
            BulletSimAPI.CreateHull(_scene.WorldID, _hullKey, hullCount, convHulls);
            _shapeType = ShapeData.PhysicsShapeType.SHAPE_HULL;
            // meshes are already scaled by the meshmerizer
            _scale = new OMV.Vector3(1f, 1f, 1f);
        }
        return;
    }

    private void HullReturn(ConvexResult result)
    {
        _hulls.Add(result);
        return;
    }

    // Create an object in Bullet
    // No locking here because this is done when the physics engine is not simulating
    private void CreateObject()
    {
        if (IsRootOfLinkset)
        {
            // Create a linkset around this object
            // If I am the root prim of a linkset, replace my physical shape with all the
            // pieces of the children.
            // All of the children should have called CreateGeom so they have a hull
            // in the physics engine already. Here we pull together all of those hulls
            // into one shape.
            m_log.DebugFormat("{0}: CreateLinkset", LogHeader);
            int totalPrimsInLinkset = _childrenPrims.Count + 1;
            ShapeData[] shapes = new ShapeData[totalPrimsInLinkset];
            FillShapeInfo(out shapes[0]);
            int ii = 1;
            foreach (BSPrim prim in _childrenPrims)
            {
                m_log.DebugFormat("{0}: CreateLinkset: adding prim {1}", LogHeader, prim.LocalID);
                prim.FillShapeInfo(out shapes[ii]);
                ii++;
            }
            BulletSimAPI.CreateLinkset(_scene.WorldID, totalPrimsInLinkset, shapes);
        }
        else
        {
            // simple object
            ShapeData shape;
            FillShapeInfo(out shape);
            BulletSimAPI.CreateObject(_scene.WorldID, shape);
        }
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
        shape.MeshKey = _hullKey;
        shape.Collidable = (!IsPhantom) ? ShapeData.numericTrue : ShapeData.numericFalse;
        shape.Flying = _flying ? ShapeData.numericTrue : ShapeData.numericFalse; 
        shape.Friction = _friction;
        shape.Dynamic = _isPhysical ? ShapeData.numericTrue : ShapeData.numericFalse;
    }

    // Rebuild the geometry and object.
    // This is called when the shape changes so we need to recreate the mesh/hull.
    // No locking here because this is done when the physics engine is not simulating
    private void RecreateGeomAndObject()
    {
        if (_hullKey != 0)
        {
            // if a hull already exists, delete the old one
            BulletSimAPI.DestroyHull(_scene.WorldID, _hullKey);
            _hullKey = 0;
        }
        // If this object is complex or we are the root of a linkset, build a mesh.
        // The root of a linkset must be a mesh so we can create the linked compound object.
        if (_scene.NeedsMeshing(_pbs) || IsRootOfLinkset )
        {
            m_log.DebugFormat("{0}: RecreateGeomAndObject: creating mesh", LogHeader);
            _mesh = _scene.mesher.CreateMesh(_avName, _pbs, _size, _scene.meshLOD, _isPhysical);
        }
        else
        {
            // it's a BulletSim native shape.
            _mesh = null;
        }
        CreateGeom();   // create the geometry for this prim
        CreateObject();
        return;
    }

    // The physics engine says that properties have updated. Update same and inform
    // the world that things have changed.
    // TODO: do we really need to check for changed? Maybe just copy values and call RequestPhysicsterseUpdate()
    public void UpdateProperties(EntityProperties entprop)
    {
        bool changed = false;
        // we assign to the local variables so the normal set action does not happen
        if (_position != entprop.Position)
        {
            _position = entprop.Position;
            // m_log.DebugFormat("{0}: UpdateProperties: position = {1}", LogHeader, _position);
            changed = true;
        }
        if (_orientation != entprop.Rotation)
        {
            _orientation = entprop.Rotation;
            // m_log.DebugFormat("{0}: UpdateProperties: rotation = {1}", LogHeader, _orientation);
            changed = true;
        }
        if (_velocity != entprop.Velocity)
        {
            _velocity = entprop.Velocity;
            // m_log.DebugFormat("{0}: UpdateProperties: velocity = {1}", LogHeader, _velocity);
            changed = true;
        }
        if (_acceleration != entprop.Acceleration)
        {
            _acceleration = entprop.Acceleration;
            // m_log.DebugFormat("{0}: UpdateProperties: acceleration = {1}", LogHeader, _acceleration);
            changed = true;
        }
        if (_rotationalVelocity != entprop.AngularVelocity)
        {
            _rotationalVelocity = entprop.AngularVelocity;
            // m_log.DebugFormat("{0}: UpdateProperties: rotationalVelocity = {1}", LogHeader, _rotationalVelocity);
            changed = true;
        }
        if (changed)
        {
            base.RequestPhysicsterseUpdate();
        }
    }

    // I've collided with something
    public void Collide(uint collidingWith, ActorTypes type, OMV.Vector3 contactPoint, OMV.Vector3 contactNormal, float pentrationDepth)
    {
        // m_log.DebugFormat("{0}: Collide: ms={1}, id={2}, with={3}", LogHeader, _subscribedEventsMs, LocalID, collidingWith);
        // The following makes IsColliding() and IsCollidingGround() work
        _collidingStep = _scene.SimulationStep;
        if (collidingWith == BSScene.TERRAIN_ID || collidingWith == BSScene.GROUNDPLANE_ID)
        {
            _collidingGroundStep = _scene.SimulationStep;
        }

        if (_subscribedEventsMs == 0) return;   // nothing in the object is waiting for collision events

        // create the event for the collision
        Dictionary<uint, ContactPoint> contactPoints = new Dictionary<uint, ContactPoint>();
        contactPoints.Add(collidingWith, new ContactPoint(contactPoint, contactNormal, pentrationDepth));
        CollisionEventUpdate args = new CollisionEventUpdate(LocalID, (int)type, 1, contactPoints);
        base.SendCollisionUpdate(args);
    }
}
}

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
using System.Collections.Generic;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.PEPlugin
{
public class PECharacter : PhysicsActor
{
    private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private bool _stopped;
    private Vector3 _size;
    private PrimitiveBaseShape _pbs;
    private uint _localID = 0;
    private bool _grabbed;
    private bool _selected;
    private Vector3 _position;
    private float _mass;
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
    private bool _isColliding;
    private bool _collidingGround;
    private bool _collidingObj;
    private bool _floatOnWater;
    private Vector3 _rotationalVelocity;
    private bool _kinematic;
    private float _buoyancy;

    private Vector3 _PIDTarget;
    private bool _usePID;
    private float _PIDTau;
    private bool _useHoverPID;
    private float _PIDHoverHeight;
    private PIDHoverType _PIDHoverType;
    private float _PIDHoverTao;

    public PECharacter(String avName, PEScene parent_scene, Vector3 pos, 
                    CollisionLocker dode, Vector3 size, float pid_d, float pid_p, 
                    float capsule_radius, float tensor, float density, float height_fudge_factor, 
                    float walk_divisor, float rundivisor)
    {
        _position = pos;
        _size = size;
        return;
    }

    public override bool Stopped { 
        get { return _stopped; } 
    }
    public override Vector3 Size { 
        get { return _size; } 
        set { _size = value;
            m_log.Debug("[RPE] PEChar set Size");
            Prop.Set(_localID, PropType.Size, _size);
        } 
    }
    public override PrimitiveBaseShape Shape { 
        set { _pbs = value; 
            m_log.Debug("[RPE] PEChar set Shape");
            Prop.Set(_localID, PropType.Shape, _pbs);
        } 
    }
    public override uint LocalID { 
        set { _localID = value; 
            m_log.Debug("[RPE] PEChar set LocalID");
            Prop.Set(_localID, PropType.LocalID, _localID);
        }
        get { return _localID; }
    }
    public override bool Grabbed { 
        set { _grabbed = value; 
            m_log.Debug("[RPE] PEChar set Grabbed");
            Prop.Set(_localID, PropType.Grabbed, _grabbed);
        } 
    }
    public override bool Selected { 
        set { _selected = value; 
            m_log.Debug("[RPE] PEChar set Selected");
            Prop.Set(_localID, PropType.Selected, _selected);
        } 
    }
    public override void CrossingFailure() { return; }
    public override void link(PhysicsActor obj) { return; }
    public override void delink() { return; }
    public override void LockAngularMotion(Vector3 axis) { return; }

    public override Vector3 Position { 
        get { return _position; } 
        set { _position = value; 
            // m_log.DebugFormat("[RPE] PEChar set Position: {0}", _position);
            Prop.Set(_localID, PropType.Position, _position);
        } 
    }
    public override float Mass { 
        get { return _mass; } 
    }
    public override Vector3 Force { 
        get { return _force; } 
        set { _force = value; 
            m_log.Debug("[RPE] PEChar set Force");
            Prop.Set(_localID, PropType.Force, _force);
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

    public override Vector3 GeometricCenter { get { return Vector3.Zero; } }
    public override Vector3 CenterOfMass { get { return Vector3.Zero; } }
    public override Vector3 Velocity { 
        get { return _velocity; } 
        set { _velocity = value; 
            m_log.Debug("[RPE] PEChar set Velocity");
            Prop.Set(_localID, PropType.Velocity, _velocity);
        } 
    }
    public override Vector3 Torque { 
        get { return _torque; } 
        set { _torque = value; 
            Prop.Set(_localID, PropType.Torque, _torque);
        } 
    }
    public override float CollisionScore { 
        get { return _collisionScore; } 
        set { _collisionScore = value; 
            Prop.Set(_localID, PropType.CollisionScore, _collisionScore);
        } 
    }
    public override Vector3 Acceleration { 
        get { return _acceleration; } 
    }
    public override Quaternion Orientation { 
        get { return _orientation; } 
        set { _orientation = value; 
            Prop.Set(_localID, PropType.Orientation, _orientation);
        } 
    }
    public override int PhysicsActorType { 
        get { return _physicsActorType; } 
        set { _physicsActorType = value; 
        } 
    }
    public override bool IsPhysical { 
        get { return _isPhysical; } 
        set { _isPhysical = value; 
        } 
    }
    public override bool Flying { 
        get { return _flying; } 
        set { _flying = value; 
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
    public override float Buoyancy { 
        get { return _buoyancy; } 
        set { _buoyancy = value; } 
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

    public override void AddForce(Vector3 force, bool pushforce) { 
    }
    public override void AddAngularForce(Vector3 force, bool pushforce) { 
    }
    public override void SetMomentum(Vector3 momentum) { 
    }
    public override void SubscribeEvents(int ms) { 
    }
    public override void UnSubscribeEvents() { 
    }
    public override bool SubscribedEvents() { 
        return false; 
    }

}
}

/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
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
<<<<<<< HEAD
 */

/* RA: June 14, 2011. Copied from ODEDynamics.cs and converted to
 * call the BulletSim system.
 */
/* Revised Aug, Sept 2009 by Kitto Flora. ODEDynamics.cs replaces
 * ODEVehicleSettings.cs. It and ODEPrim.cs are re-organised:
 * ODEPrim.cs contains methods dealing with Prim editing, Prim
 * characteristics and Kinetic motion.
 * ODEDynamics.cs contains methods dealing with Prim Physical motion
 * (dynamics) and the associated settings. Old Linear and angular
 * motors for dynamic motion have been replace with  MoveLinear()
 * and MoveAngular(); 'Physical' is used only to switch ODE dynamic
 * simualtion on/off; VEHICAL_TYPE_NONE/VEHICAL_TYPE_<other> is to
 * switch between 'VEHICLE' parameter use and general dynamics
 * settings use.
=======
 *
 * The quotations from http://wiki.secondlife.com/wiki/Linden_Vehicle_Tutorial
 * are Copyright (c) 2009 Linden Research, Inc and are used under their license
 * of Creative Commons Attribution-Share Alike 3.0
 * (http://creativecommons.org/licenses/by-sa/3.0/).
>>>>>>> upstream/master
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.BulletSPlugin
{
    public class BSDynamics
    {
        private int frcount = 0;                                        // Used to limit dynamics debug output to
                                                                        // every 100th frame

        private BSScene m_physicsScene;
        private BSPrim m_prim;      // the prim this dynamic controller belongs to

        // Vehicle properties
        private Vehicle m_type = Vehicle.TYPE_NONE;                     // If a 'VEHICLE', and what kind
        public Vehicle Type
        {
            get { return m_type; }
        }
        // private Quaternion m_referenceFrame = Quaternion.Identity;   // Axis modifier
        private VehicleFlag m_flags = (VehicleFlag) 0;                  // Boolean settings:
                                                                        // HOVER_TERRAIN_ONLY
                                                                        // HOVER_GLOBAL_HEIGHT
                                                                        // NO_DEFLECTION_UP
                                                                        // HOVER_WATER_ONLY
                                                                        // HOVER_UP_ONLY
                                                                        // LIMIT_MOTOR_UP
                                                                        // LIMIT_ROLL_ONLY
        private Vector3 m_BlockingEndPoint = Vector3.Zero;
        private Quaternion m_RollreferenceFrame = Quaternion.Identity;
        // Linear properties
        private Vector3 m_linearMotorDirection = Vector3.Zero;          // velocity requested by LSL, decayed by time
        private Vector3 m_linearMotorDirectionLASTSET = Vector3.Zero;   // velocity requested by LSL
        private Vector3 m_newVelocity = Vector3.Zero;                   // velocity computed to be applied to body
        private Vector3 m_linearFrictionTimescale = Vector3.Zero;
        private float m_linearMotorDecayTimescale = 0;
        private float m_linearMotorTimescale = 0;
        private Vector3 m_lastLinearVelocityVector = Vector3.Zero;
        private Vector3 m_lastPositionVector = Vector3.Zero;
        // private bool m_LinearMotorSetLastFrame = false;
        // private Vector3 m_linearMotorOffset = Vector3.Zero;

        //Angular properties
        private Vector3 m_angularMotorDirection = Vector3.Zero;         // angular velocity requested by LSL motor
        private int m_angularMotorApply = 0;                            // application frame counter
        private Vector3 m_angularMotorVelocity = Vector3.Zero;          // current angular motor velocity
        private float m_angularMotorTimescale = 0;                      // motor angular velocity ramp up rate
        private float m_angularMotorDecayTimescale = 0;                 // motor angular velocity decay rate
        private Vector3 m_angularFrictionTimescale = Vector3.Zero;      // body angular velocity  decay rate
<<<<<<< HEAD
        private Vector3 m_lastAngularVelocity = Vector3.Zero;           // what was last applied to body
 //       private Vector3 m_lastVertAttractor = Vector3.Zero;             // what VA was last applied to body

        //Deflection properties
        // private float m_angularDeflectionEfficiency = 0;
        // private float m_angularDeflectionTimescale = 0;
        // private float m_linearDeflectionEfficiency = 0;
        // private float m_linearDeflectionTimescale = 0;
=======
        private Vector3 m_lastAngularVelocity = Vector3.Zero;
        private Vector3 m_lastVertAttractor = Vector3.Zero;             // what VA was last applied to body

        //Deflection properties
        private BSVMotor m_angularDeflectionMotor = new BSVMotor("AngularDeflection");
        private float m_angularDeflectionEfficiency = 0;
        private float m_angularDeflectionTimescale = 0;
        private float m_linearDeflectionEfficiency = 0;
        private float m_linearDeflectionTimescale = 0;
>>>>>>> upstream/master

        //Banking properties
        // private float m_bankingEfficiency = 0;
        // private float m_bankingMix = 0;
        // private float m_bankingTimescale = 0;

        //Hover and Buoyancy properties
        private BSVMotor m_hoverMotor = new BSVMotor("Hover");
        private float m_VhoverHeight = 0f;
//        private float m_VhoverEfficiency = 0f;
        private float m_VhoverTimescale = 0f;
        private float m_VhoverTargetHeight = -1.0f;     // if <0 then no hover, else its the current target height
        // Modifies gravity. Slider between -1 (double-gravity) and 1 (full anti-gravity)
        private float m_VehicleBuoyancy = 0f;
        private Vector3 m_VehicleGravity = Vector3.Zero;    // Gravity computed when buoyancy set

        //Attractor properties
<<<<<<< HEAD
        private float m_verticalAttractionEfficiency = 1.0f;        // damped
        private float m_verticalAttractionTimescale = 500f;         // Timescale > 300  means no vert attractor.

        public BSDynamics(BSScene myScene, BSPrim myPrim)
        {
            m_physicsScene = myScene;
            m_prim = myPrim;
            m_type = Vehicle.TYPE_NONE;
=======
        private BSVMotor m_verticalAttractionMotor = new BSVMotor("VerticalAttraction");
        private float m_verticalAttractionEfficiency = 1.0f; // damped
        private float m_verticalAttractionCutoff = 500f;     // per the documentation
        // Timescale > cutoff  means no vert attractor.
        private float m_verticalAttractionTimescale = 510f;

        // Just some recomputed constants:
        static readonly float PIOverFour = ((float)Math.PI) / 4f;
        static readonly float PIOverTwo = ((float)Math.PI) / 2f;

        // For debugging, flags to turn on and off individual corrections.
        private bool enableAngularVerticalAttraction;
        private bool enableAngularDeflection;
        private bool enableAngularBanking;

        public BSDynamics(BSScene myScene, BSPrim myPrim)
        {
            PhysicsScene = myScene;
            Prim = myPrim;
            Type = Vehicle.TYPE_NONE;
            SetupVehicleDebugging();
        }

        // Stopgap debugging enablement. Allows source level debugging but still checking
        //    in changes by making enablement of debugging flags from INI file.
        public void SetupVehicleDebugging()
        {
            enableAngularVerticalAttraction = true;
            enableAngularDeflection = false;
            enableAngularBanking = false;
            if (BSParam.VehicleDebuggingEnabled != ConfigurationParameters.numericFalse)
            {
                enableAngularVerticalAttraction = false;
                enableAngularDeflection = false;
                enableAngularBanking = false;
            }
        }

        // Return 'true' if this vehicle is doing vehicle things
        public bool IsActive
        {
            get { return (Type != Vehicle.TYPE_NONE && !Prim.IsStatic); }
>>>>>>> upstream/master
        }

        #region Vehicle parameter setting
        internal void ProcessFloatVehicleParam(Vehicle pParam, float pValue)
        {
            VDetailLog("{0},ProcessFloatVehicleParam,param={1},val={2}", m_prim.LocalID, pParam, pValue);
            switch (pParam)
            {
                case Vehicle.ANGULAR_DEFLECTION_EFFICIENCY:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_angularDeflectionEfficiency = pValue;
                    break;
                case Vehicle.ANGULAR_DEFLECTION_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_angularDeflectionTimescale = pValue;
                    break;
                case Vehicle.ANGULAR_MOTOR_DECAY_TIMESCALE:
<<<<<<< HEAD
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_angularMotorDecayTimescale = pValue;
=======
                    m_angularMotorDecayTimescale = ClampInRange(0.01f, pValue, 120);
                    m_angularMotor.TargetValueDecayTimeScale = m_angularMotorDecayTimescale;
>>>>>>> upstream/master
                    break;
                case Vehicle.ANGULAR_MOTOR_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_angularMotorTimescale = pValue;
                    break;
                case Vehicle.BANKING_EFFICIENCY:
<<<<<<< HEAD
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_bankingEfficiency = pValue;
=======
                    m_bankingEfficiency = ClampInRange(-1f, pValue, 1f);
>>>>>>> upstream/master
                    break;
                case Vehicle.BANKING_MIX:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_bankingMix = pValue;
                    break;
                case Vehicle.BANKING_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_bankingTimescale = pValue;
                    break;
                case Vehicle.BUOYANCY:
<<<<<<< HEAD
                    if (pValue < -1f) pValue = -1f;
                    if (pValue > 1f) pValue = 1f;
                    m_VehicleBuoyancy = pValue;
                    break;
//                case Vehicle.HOVER_EFFICIENCY:
//                    if (pValue < 0f) pValue = 0f;
//                    if (pValue > 1f) pValue = 1f;
//                    m_VhoverEfficiency = pValue;
//                    break;
=======
                    m_VehicleBuoyancy = ClampInRange(-1f, pValue, 1f);
                    m_VehicleGravity = Prim.ComputeGravity(m_VehicleBuoyancy);
                    break;
                case Vehicle.HOVER_EFFICIENCY:
                    m_VhoverEfficiency = ClampInRange(0f, pValue, 1f);
                    break;
>>>>>>> upstream/master
                case Vehicle.HOVER_HEIGHT:
                    m_VhoverHeight = pValue;
                    break;
                case Vehicle.HOVER_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_VhoverTimescale = pValue;
                    break;
                case Vehicle.LINEAR_DEFLECTION_EFFICIENCY:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_linearDeflectionEfficiency = pValue;
                    break;
                case Vehicle.LINEAR_DEFLECTION_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_linearDeflectionTimescale = pValue;
                    break;
                case Vehicle.LINEAR_MOTOR_DECAY_TIMESCALE:
<<<<<<< HEAD
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_linearMotorDecayTimescale = pValue;
=======
                    m_linearMotorDecayTimescale = ClampInRange(0.01f, pValue, 120);
                    m_linearMotor.TargetValueDecayTimeScale = m_linearMotorDecayTimescale;
>>>>>>> upstream/master
                    break;
                case Vehicle.LINEAR_MOTOR_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_linearMotorTimescale = pValue;
                    break;
                case Vehicle.VERTICAL_ATTRACTION_EFFICIENCY:
<<<<<<< HEAD
                    if (pValue < 0.1f) pValue = 0.1f;    // Less goes unstable
                    if (pValue > 1.0f) pValue = 1.0f;
                    m_verticalAttractionEfficiency = pValue;
=======
                    m_verticalAttractionEfficiency = ClampInRange(0.1f, pValue, 1f);
                    m_verticalAttractionMotor.Efficiency = m_verticalAttractionEfficiency;
>>>>>>> upstream/master
                    break;
                case Vehicle.VERTICAL_ATTRACTION_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_verticalAttractionTimescale = pValue;
                    break;

                // These are vector properties but the engine lets you use a single float value to
                // set all of the components to the same value
                case Vehicle.ANGULAR_FRICTION_TIMESCALE:
                    m_angularFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
                    m_angularMotorDirection = new Vector3(pValue, pValue, pValue);
                    m_angularMotorApply = 10;
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    m_linearFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    m_linearMotorDirection = new Vector3(pValue, pValue, pValue);
                    m_linearMotorDirectionLASTSET = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    // m_linearMotorOffset = new Vector3(pValue, pValue, pValue);
                    break;

            }
        }//end ProcessFloatVehicleParam

        internal void ProcessVectorVehicleParam(Vehicle pParam, Vector3 pValue)
        {
            VDetailLog("{0},ProcessVectorVehicleParam,param={1},val={2}", m_prim.LocalID, pParam, pValue);
            switch (pParam)
            {
                case Vehicle.ANGULAR_FRICTION_TIMESCALE:
                    m_angularFrictionTimescale = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
<<<<<<< HEAD
=======
                    // Limit requested angular speed to 2 rps= 4 pi rads/sec
                    pValue.X = ClampInRange(-12.56f, pValue.X, 12.56f);
                    pValue.Y = ClampInRange(-12.56f, pValue.Y, 12.56f);
                    pValue.Z = ClampInRange(-12.56f, pValue.Z, 12.56f);
>>>>>>> upstream/master
                    m_angularMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    // Limit requested angular speed to 2 rps= 4 pi rads/sec
                    if (m_angularMotorDirection.X > 12.56f) m_angularMotorDirection.X = 12.56f;
                    if (m_angularMotorDirection.X < - 12.56f) m_angularMotorDirection.X = - 12.56f;
                    if (m_angularMotorDirection.Y > 12.56f) m_angularMotorDirection.Y = 12.56f;
                    if (m_angularMotorDirection.Y < - 12.56f) m_angularMotorDirection.Y = - 12.56f;
                    if (m_angularMotorDirection.Z > 12.56f) m_angularMotorDirection.Z = 12.56f;
                    if (m_angularMotorDirection.Z < - 12.56f) m_angularMotorDirection.Z = - 12.56f;
                    m_angularMotorApply = 10;
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    m_linearFrictionTimescale = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    m_linearMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    m_linearMotorDirectionLASTSET = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    // m_linearMotorOffset = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.BLOCK_EXIT:
                    m_BlockingEndPoint = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
            }
        }//end ProcessVectorVehicleParam

        internal void ProcessRotationVehicleParam(Vehicle pParam, Quaternion pValue)
        {
            VDetailLog("{0},ProcessRotationalVehicleParam,param={1},val={2}", m_prim.LocalID, pParam, pValue);
            switch (pParam)
            {
                case Vehicle.REFERENCE_FRAME:
                    // m_referenceFrame = pValue;
                    break;
                case Vehicle.ROLL_FRAME:
                    m_RollreferenceFrame = pValue;
                    break;
            }
        }//end ProcessRotationVehicleParam

        internal void ProcessVehicleFlags(int pParam, bool remove)
        {
            VDetailLog("{0},ProcessVehicleFlags,param={1},remove={2}", m_prim.LocalID, pParam, remove);
            VehicleFlag parm = (VehicleFlag)pParam;
            if (remove)
            {
                if (pParam == -1)
                {
                    m_flags = (VehicleFlag)0;
                }
                else
                {
                    m_flags &= ~parm;
                }
            }
            else {
                m_flags |= parm;
            }
        }//end ProcessVehicleFlags

        internal void ProcessTypeChange(Vehicle pType)
        {
            VDetailLog("{0},ProcessTypeChange,type={1}", m_prim.LocalID, pType);
            // Set Defaults For Type
            m_type = pType;
            switch (pType)
            {
                    case Vehicle.TYPE_NONE:
                    m_linearFrictionTimescale = new Vector3(0, 0, 0);
                    m_angularFrictionTimescale = new Vector3(0, 0, 0);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 0;
                    m_linearMotorDecayTimescale = 0;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 0;
                    m_angularMotorDecayTimescale = 0;
                    m_VhoverHeight = 0;
                    m_VhoverTimescale = 0;
                    m_VehicleBuoyancy = 0;
                    m_flags = (VehicleFlag)0;
                    break;

                case Vehicle.TYPE_SLED:
                    m_linearFrictionTimescale = new Vector3(30, 1, 1000);
                    m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 1000;
                    m_linearMotorDecayTimescale = 120;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 1000;
                    m_angularMotorDecayTimescale = 120;
                    m_VhoverHeight = 0;
//                    m_VhoverEfficiency = 1;
                    m_VhoverTimescale = 10;
                    m_VehicleBuoyancy = 0;
<<<<<<< HEAD
                    // m_linearDeflectionEfficiency = 1;
                    // m_linearDeflectionTimescale = 1;
                    // m_angularDeflectionEfficiency = 1;
                    // m_angularDeflectionTimescale = 1000;
                    // m_bankingEfficiency = 0;
                    // m_bankingMix = 1;
                    // m_bankingTimescale = 10;
                    // m_referenceFrame = Quaternion.Identity;
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_ROLL_ONLY | VehicleFlag.LIMIT_MOTOR_UP);
                    m_flags &=
                         ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                           VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
=======

                    m_linearDeflectionEfficiency = 1;
                    m_linearDeflectionTimescale = 1;

                    m_angularDeflectionEfficiency = 1;
                    m_angularDeflectionTimescale = 1000;

                    m_verticalAttractionEfficiency = 0;
                    m_verticalAttractionTimescale = 0;

                    m_bankingEfficiency = 0;
                    m_bankingTimescale = 10;
                    m_bankingMix = 1;

                    m_referenceFrame = Quaternion.Identity;
                    m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY
                                | VehicleFlag.HOVER_TERRAIN_ONLY
                                | VehicleFlag.HOVER_GLOBAL_HEIGHT
                                | VehicleFlag.HOVER_UP_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP
                            | VehicleFlag.LIMIT_ROLL_ONLY
                            | VehicleFlag.LIMIT_MOTOR_UP);

>>>>>>> upstream/master
                    break;
                case Vehicle.TYPE_CAR:
                    m_linearFrictionTimescale = new Vector3(100, 2, 1000);
                    m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 1;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 1;
                    m_angularMotorDecayTimescale = 0.8f;
                    m_VhoverHeight = 0;
//                    m_VhoverEfficiency = 0;
                    m_VhoverTimescale = 1000;
                    m_VehicleBuoyancy = 0;
                    // // m_linearDeflectionEfficiency = 1;
                    // // m_linearDeflectionTimescale = 2;
                    // // m_angularDeflectionEfficiency = 0;
                    // m_angularDeflectionTimescale = 10;
                    m_verticalAttractionEfficiency = 1f;
                    m_verticalAttractionTimescale = 10f;
                    // m_bankingEfficiency = -0.2f;
                    // m_bankingMix = 1;
                    // m_bankingTimescale = 1;
                    // m_referenceFrame = Quaternion.Identity;
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_ROLL_ONLY |
                                VehicleFlag.LIMIT_MOTOR_UP);
                    m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY | VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    m_flags |= (VehicleFlag.HOVER_UP_ONLY);
                    break;
                case Vehicle.TYPE_BOAT:
                    m_linearFrictionTimescale = new Vector3(10, 3, 2);
                    m_angularFrictionTimescale = new Vector3(10,10,10);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 5;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 4;
                    m_angularMotorDecayTimescale = 4;
                    m_VhoverHeight = 0;
//                    m_VhoverEfficiency = 0.5f;
                    m_VhoverTimescale = 2;
                    m_VehicleBuoyancy = 1;
                    // m_linearDeflectionEfficiency = 0.5f;
                    // m_linearDeflectionTimescale = 3;
                    // m_angularDeflectionEfficiency = 0.5f;
                    // m_angularDeflectionTimescale = 5;
                    m_verticalAttractionEfficiency = 0.5f;
                    m_verticalAttractionTimescale = 5f;
                    // m_bankingEfficiency = -0.3f;
                    // m_bankingMix = 0.8f;
                    // m_bankingTimescale = 1;
                    // m_referenceFrame = Quaternion.Identity;
                    m_flags &= ~(VehicleFlag.HOVER_TERRAIN_ONLY |
                            VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
                    m_flags &= ~(VehicleFlag.LIMIT_ROLL_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP |
                                VehicleFlag.LIMIT_MOTOR_UP);
                    m_flags |= (VehicleFlag.HOVER_WATER_ONLY);
                    break;
                case Vehicle.TYPE_AIRPLANE:
                    m_linearFrictionTimescale = new Vector3(200, 10, 5);
                    m_angularFrictionTimescale = new Vector3(20, 20, 20);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 2;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 4;
                    m_angularMotorDecayTimescale = 4;
                    m_VhoverHeight = 0;
//                    m_VhoverEfficiency = 0.5f;
                    m_VhoverTimescale = 1000;
                    m_VehicleBuoyancy = 0;
                    // m_linearDeflectionEfficiency = 0.5f;
                    // m_linearDeflectionTimescale = 3;
                    // m_angularDeflectionEfficiency = 1;
                    // m_angularDeflectionTimescale = 2;
                    m_verticalAttractionEfficiency = 0.9f;
                    m_verticalAttractionTimescale = 2f;
                    // m_bankingEfficiency = 1;
                    // m_bankingMix = 0.7f;
                    // m_bankingTimescale = 2;
                    // m_referenceFrame = Quaternion.Identity;
                    m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                        VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
                    m_flags &= ~(VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_MOTOR_UP);
                    m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY);
                    break;
                case Vehicle.TYPE_BALLOON:
                    m_linearFrictionTimescale = new Vector3(5, 5, 5);
                    m_angularFrictionTimescale = new Vector3(10, 10, 10);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 5;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 6;
                    m_angularMotorDecayTimescale = 10;
                    m_VhoverHeight = 5;
//                    m_VhoverEfficiency = 0.8f;
                    m_VhoverTimescale = 10;
                    m_VehicleBuoyancy = 1;
                    // m_linearDeflectionEfficiency = 0;
                    // m_linearDeflectionTimescale = 5;
                    // m_angularDeflectionEfficiency = 0;
                    // m_angularDeflectionTimescale = 5;
                    m_verticalAttractionEfficiency = 1f;
                    m_verticalAttractionTimescale = 100f;
<<<<<<< HEAD
                    // m_bankingEfficiency = 0;
                    // m_bankingMix = 0.7f;
                    // m_bankingTimescale = 5;
                    // m_referenceFrame = Quaternion.Identity;
                    m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                        VehicleFlag.HOVER_UP_ONLY);
                    m_flags &= ~(VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_MOTOR_UP);
                    m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY);
                    m_flags |= (VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    break;
            }
        }//end SetDefaultsForType
=======

                    m_bankingEfficiency = 0;
                    m_bankingMix = 0.7f;
                    m_bankingTimescale = 5;

                    m_referenceFrame = Quaternion.Identity;

                    m_referenceFrame = Quaternion.Identity;
                    m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY
                                    | VehicleFlag.HOVER_TERRAIN_ONLY
                                    | VehicleFlag.HOVER_UP_ONLY
                                    | VehicleFlag.NO_DEFLECTION_UP
                                    | VehicleFlag.LIMIT_MOTOR_UP);
                    m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY
                                    | VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    break;
            }

            // Update any physical parameters based on this type.
            Refresh();

            m_linearMotor = new BSVMotor("LinearMotor", m_linearMotorTimescale,
                                m_linearMotorDecayTimescale, m_linearFrictionTimescale,
                                1f);
            m_linearMotor.PhysicsScene = PhysicsScene;  // DEBUG DEBUG DEBUG (enables detail logging)

            m_angularMotor = new BSVMotor("AngularMotor", m_angularMotorTimescale,
                                m_angularMotorDecayTimescale, m_angularFrictionTimescale,
                                1f);
            m_angularMotor.PhysicsScene = PhysicsScene;  // DEBUG DEBUG DEBUG (enables detail logging)

            /*  Not implemented
            m_verticalAttractionMotor = new BSVMotor("VerticalAttraction", m_verticalAttractionTimescale,
                                BSMotor.Infinite, BSMotor.InfiniteVector,
                                m_verticalAttractionEfficiency);
            // Z goes away and we keep X and Y
            m_verticalAttractionMotor.FrictionTimescale = new Vector3(BSMotor.Infinite, BSMotor.Infinite, 0.1f);
            m_verticalAttractionMotor.PhysicsScene = PhysicsScene;  // DEBUG DEBUG DEBUG (enables detail logging)
             */
        }
        #endregion // Vehicle parameter setting

        // Some of the properties of this prim may have changed.
        // Do any updating needed for a vehicle
        public void Refresh()
        {
            if (IsActive)
            {
                // Remember the mass so we don't have to fetch it every step
                m_vehicleMass = Prim.Linkset.LinksetMass;

                // Friction affects are handled by this vehicle code
                float friction = 0f;
                PhysicsScene.PE.SetFriction(Prim.PhysBody, friction);

                // Moderate angular movement introduced by Bullet.
                // TODO: possibly set AngularFactor and LinearFactor for the type of vehicle.
                //     Maybe compute linear and angular factor and damping from params.
                float angularDamping = BSParam.VehicleAngularDamping;
                PhysicsScene.PE.SetAngularDamping(Prim.PhysBody, angularDamping);

                // Vehicles report collision events so we know when it's on the ground
                PhysicsScene.PE.AddToCollisionFlags(Prim.PhysBody, CollisionFlags.BS_VEHICLE_COLLISIONS);

                Prim.Inertia = PhysicsScene.PE.CalculateLocalInertia(Prim.PhysShape, m_vehicleMass);
                PhysicsScene.PE.SetMassProps(Prim.PhysBody, m_vehicleMass, Prim.Inertia);
                PhysicsScene.PE.UpdateInertiaTensor(Prim.PhysBody);

                // Set the gravity for the vehicle depending on the buoyancy
                // TODO: what should be done if prim and vehicle buoyancy differ?
                m_VehicleGravity = Prim.ComputeGravity(m_VehicleBuoyancy);
                // The actual vehicle gravity is set to zero in Bullet so we can do all the application of same.
                PhysicsScene.PE.SetGravity(Prim.PhysBody, Vector3.Zero);

                VDetailLog("{0},BSDynamics.Refresh,mass={1},frict={2},inert={3},aDamp={4},grav={5}",
                        Prim.LocalID, m_vehicleMass, friction, Prim.Inertia, angularDamping, m_VehicleGravity);
            }
            else
            {
                PhysicsScene.PE.RemoveFromCollisionFlags(Prim.PhysBody, CollisionFlags.BS_VEHICLE_COLLISIONS);
            }
        }

        public bool RemoveBodyDependencies(BSPhysObject prim)
        {
            // If active, we need to add our properties back when the body is rebuilt.
            return IsActive;
        }

        public void RestoreBodyDependencies(BSPhysObject prim)
        {
            if (Prim.LocalID != prim.LocalID)
            {
                // The call should be on us by our prim. Error if not.
                PhysicsScene.Logger.ErrorFormat("{0} RestoreBodyDependencies: called by not my prim. passedLocalID={1}, vehiclePrimLocalID={2}",
                                LogHeader, prim.LocalID, Prim.LocalID);
                return;
            }
            Refresh();
        }

        #region Known vehicle value functions
        // Vehicle physical parameters that we buffer from constant getting and setting.
        // The "m_known*" values are unknown until they are fetched and the m_knownHas flag is set.
        //      Changing is remembered and the parameter is stored back into the physics engine only if updated.
        //      This does two things: 1) saves continuious calls into unmanaged code, and
        //      2) signals when a physics property update must happen back to the simulator
        //      to update values modified for the vehicle.
        private int m_knownChanged;
        private int m_knownHas;
        private float m_knownTerrainHeight;
        private float m_knownWaterLevel;
        private Vector3 m_knownPosition;
        private Vector3 m_knownVelocity;
        private Vector3 m_knownForce;
        private Vector3 m_knownForceImpulse;
        private Quaternion m_knownOrientation;
        private Vector3 m_knownRotationalVelocity;
        private Vector3 m_knownRotationalForce;
        private Vector3 m_knownRotationalImpulse;
        private Vector3 m_knownForwardVelocity;    // vehicle relative forward speed

        private const int m_knownChangedPosition           = 1 << 0;
        private const int m_knownChangedVelocity           = 1 << 1;
        private const int m_knownChangedForce              = 1 << 2;
        private const int m_knownChangedForceImpulse       = 1 << 3;
        private const int m_knownChangedOrientation        = 1 << 4;
        private const int m_knownChangedRotationalVelocity = 1 << 5;
        private const int m_knownChangedRotationalForce    = 1 << 6;
        private const int m_knownChangedRotationalImpulse  = 1 << 7;
        private const int m_knownChangedTerrainHeight      = 1 << 8;
        private const int m_knownChangedWaterLevel         = 1 << 9;
        private const int m_knownChangedForwardVelocity    = 1 <<10;

        private void ForgetKnownVehicleProperties()
        {
            m_knownHas = 0;
            m_knownChanged = 0;
        }
        // Push all the changed values back into the physics engine
        private void PushKnownChanged()
        {
            if (m_knownChanged != 0)
            {
                if ((m_knownChanged & m_knownChangedPosition) != 0)
                    Prim.ForcePosition = m_knownPosition;

                if ((m_knownChanged & m_knownChangedOrientation) != 0)
                    Prim.ForceOrientation = m_knownOrientation;

                if ((m_knownChanged & m_knownChangedVelocity) != 0)
                {
                    Prim.ForceVelocity = m_knownVelocity;
                    // Fake out Bullet by making it think the velocity is the same as last time.
                    // Bullet does a bunch of smoothing for changing parameters.
                    //    Since the vehicle is demanding this setting, we override Bullet's smoothing
                    //    by telling Bullet the value was the same last time.
                    PhysicsScene.PE.SetInterpolationLinearVelocity(Prim.PhysBody, m_knownVelocity);
                }

                if ((m_knownChanged & m_knownChangedForce) != 0)
                    Prim.AddForce((Vector3)m_knownForce, false /*pushForce*/, true /*inTaintTime*/);

                if ((m_knownChanged & m_knownChangedForceImpulse) != 0)
                    Prim.AddForceImpulse((Vector3)m_knownForceImpulse, false /*pushforce*/, true /*inTaintTime*/);

                if ((m_knownChanged & m_knownChangedRotationalVelocity) != 0)
                {
                    Prim.ForceRotationalVelocity = m_knownRotationalVelocity;
                    PhysicsScene.PE.SetInterpolationAngularVelocity(Prim.PhysBody, m_knownRotationalVelocity);
                }

                if ((m_knownChanged & m_knownChangedRotationalImpulse) != 0)
                    Prim.ApplyTorqueImpulse((Vector3)m_knownRotationalImpulse, true /*inTaintTime*/);

                if ((m_knownChanged & m_knownChangedRotationalForce) != 0)
                {
                    Prim.AddAngularForce((Vector3)m_knownRotationalForce, false /*pushForce*/, true /*inTaintTime*/);
                }

                // If we set one of the values (ie, the physics engine didn't do it) we must force
                //      an UpdateProperties event to send the changes up to the simulator.
                PhysicsScene.PE.PushUpdate(Prim.PhysBody);
            }
            m_knownChanged = 0;
        }

        // Since the computation of terrain height can be a little involved, this routine
        //    is used to fetch the height only once for each vehicle simulation step.
        private float GetTerrainHeight(Vector3 pos)
        {
            if ((m_knownHas & m_knownChangedTerrainHeight) == 0)
            {
                m_knownTerrainHeight = Prim.PhysicsScene.TerrainManager.GetTerrainHeightAtXYZ(pos);
                m_knownHas |= m_knownChangedTerrainHeight;
            }
            return m_knownTerrainHeight;
        }

        // Since the computation of water level can be a little involved, this routine
        //    is used ot fetch the level only once for each vehicle simulation step.
        private float GetWaterLevel(Vector3 pos)
        {
            if ((m_knownHas & m_knownChangedWaterLevel) == 0)
            {
                m_knownWaterLevel = Prim.PhysicsScene.TerrainManager.GetWaterLevelAtXYZ(pos);
                m_knownHas |= m_knownChangedWaterLevel;
            }
            return (float)m_knownWaterLevel;
        }

        private Vector3 VehiclePosition
        {
            get
            {
                if ((m_knownHas & m_knownChangedPosition) == 0)
                {
                    m_knownPosition = Prim.ForcePosition;
                    m_knownHas |= m_knownChangedPosition;
                }
                return m_knownPosition;
            }
            set
            {
                m_knownPosition = value;
                m_knownChanged |= m_knownChangedPosition;
                m_knownHas |= m_knownChangedPosition;
            }
        }

        private Quaternion VehicleOrientation
        {
            get
            {
                if ((m_knownHas & m_knownChangedOrientation) == 0)
                {
                    m_knownOrientation = Prim.ForceOrientation;
                    m_knownHas |= m_knownChangedOrientation;
                }
                return m_knownOrientation;
            }
            set
            {
                m_knownOrientation = value;
                m_knownChanged |= m_knownChangedOrientation;
                m_knownHas |= m_knownChangedOrientation;
            }
        }

        private Vector3 VehicleVelocity
        {
            get
            {
                if ((m_knownHas & m_knownChangedVelocity) == 0)
                {
                    m_knownVelocity = Prim.ForceVelocity;
                    m_knownHas |= m_knownChangedVelocity;
                }
                return (Vector3)m_knownVelocity;
            }
            set
            {
                m_knownVelocity = value;
                m_knownChanged |= m_knownChangedVelocity;
                m_knownHas |= m_knownChangedVelocity;
            }
        }

        private void VehicleAddForce(Vector3 pForce)
        {
            if ((m_knownHas & m_knownChangedForce) == 0)
            {
                m_knownForce = Vector3.Zero;
                m_knownHas |= m_knownChangedForce;
            }
            m_knownForce += pForce;
            m_knownChanged |= m_knownChangedForce;
        }

        private void VehicleAddForceImpulse(Vector3 pImpulse)
        {
            if ((m_knownHas & m_knownChangedForceImpulse) == 0)
            {
                m_knownForceImpulse = Vector3.Zero;
                m_knownHas |= m_knownChangedForceImpulse;
            }
            m_knownForceImpulse += pImpulse;
            m_knownChanged |= m_knownChangedForceImpulse;
        }

        private Vector3 VehicleRotationalVelocity
        {
            get
            {
                if ((m_knownHas & m_knownChangedRotationalVelocity) == 0)
                {
                    m_knownRotationalVelocity = Prim.ForceRotationalVelocity;
                    m_knownHas |= m_knownChangedRotationalVelocity;
                }
                return (Vector3)m_knownRotationalVelocity;
            }
            set
            {
                m_knownRotationalVelocity = value;
                m_knownChanged |= m_knownChangedRotationalVelocity;
                m_knownHas |= m_knownChangedRotationalVelocity;
            }
        }
        private void VehicleAddAngularForce(Vector3 aForce)
        {
            if ((m_knownHas & m_knownChangedRotationalForce) == 0)
            {
                m_knownRotationalForce = Vector3.Zero;
            }
            m_knownRotationalForce += aForce;
            m_knownChanged |= m_knownChangedRotationalForce;
            m_knownHas |= m_knownChangedRotationalForce;
        }
        private void VehicleAddRotationalImpulse(Vector3 pImpulse)
        {
            if ((m_knownHas & m_knownChangedRotationalImpulse) == 0)
            {
                m_knownRotationalImpulse = Vector3.Zero;
                m_knownHas |= m_knownChangedRotationalImpulse;
            }
            m_knownRotationalImpulse += pImpulse;
            m_knownChanged |= m_knownChangedRotationalImpulse;
        }

        // Vehicle relative forward velocity
        private Vector3 VehicleForwardVelocity
        {
            get
            {
                if ((m_knownHas & m_knownChangedForwardVelocity) == 0)
                {
                    m_knownForwardVelocity = VehicleVelocity * Quaternion.Inverse(Quaternion.Normalize(VehicleOrientation));
                    m_knownHas |= m_knownChangedForwardVelocity;
                }
                return m_knownForwardVelocity;
            }
        }
        private float VehicleForwardSpeed
        {
            get
            {
                return VehicleForwardVelocity.X;
            }
        }
>>>>>>> upstream/master

        #endregion // Known vehicle value functions

        // One step of the vehicle properties for the next 'pTimestep' seconds.
        internal void Step(float pTimestep)
        {
            if (m_type == Vehicle.TYPE_NONE) return;

<<<<<<< HEAD
            frcount++;  // used to limit debug comment output
            if (frcount > 100)
                frcount = 0;
=======
            if (PhysicsScene.VehiclePhysicalLoggingEnabled)
                PhysicsScene.PE.DumpRigidBody(PhysicsScene.World, Prim.PhysBody);

            ForgetKnownVehicleProperties();
>>>>>>> upstream/master

            MoveLinear(pTimestep);
            MoveAngular(pTimestep);
            LimitRotation(pTimestep);

            // remember the position so next step we can limit absolute movement effects
<<<<<<< HEAD
            m_lastPositionVector = m_prim.Position;

            VDetailLog("{0},BSDynamics.Step,done,pos={1},force={2},velocity={3},angvel={4}", 
                    m_prim.LocalID, m_prim.Position, m_prim.Force, m_prim.Velocity, m_prim.RotationalVelocity);
        }// end Step

        private void MoveLinear(float pTimestep)
        {
            // m_linearMotorDirection is the direction we are moving relative to the vehicle coordinates
            // m_lastLinearVelocityVector is the speed we are moving in that direction
            if (m_linearMotorDirection.LengthSquared() > 0.001f)
            {
                Vector3 origDir = m_linearMotorDirection;
                Vector3 origVel = m_lastLinearVelocityVector;

                // add drive to body
                // Vector3 addAmount = m_linearMotorDirection/(m_linearMotorTimescale / pTimestep);
                Vector3 addAmount = (m_linearMotorDirection - m_lastLinearVelocityVector)/(m_linearMotorTimescale / pTimestep);
                // lastLinearVelocityVector is the current body velocity vector
                // RA: Not sure what the *10 is for. A correction for pTimestep?
                // m_lastLinearVelocityVector += (addAmount*10);  
                m_lastLinearVelocityVector += addAmount;  

                // Limit the velocity vector to less than the last set linear motor direction
                if (Math.Abs(m_lastLinearVelocityVector.X) > Math.Abs(m_linearMotorDirectionLASTSET.X))
                    m_lastLinearVelocityVector.X = m_linearMotorDirectionLASTSET.X;
                if (Math.Abs(m_lastLinearVelocityVector.Y) > Math.Abs(m_linearMotorDirectionLASTSET.Y))
                    m_lastLinearVelocityVector.Y = m_linearMotorDirectionLASTSET.Y;
                if (Math.Abs(m_lastLinearVelocityVector.Z) > Math.Abs(m_linearMotorDirectionLASTSET.Z))
                    m_lastLinearVelocityVector.Z = m_linearMotorDirectionLASTSET.Z;

                /*
                // decay applied velocity
                Vector3 decayfraction = Vector3.One/(m_linearMotorDecayTimescale / pTimestep);
                // (RA: do not know where the 0.5f comes from)
                m_linearMotorDirection -= m_linearMotorDirection * decayfraction * 0.5f;
                 */
                float keepfraction = 1.0f - (1.0f / (m_linearMotorDecayTimescale / pTimestep));
                m_linearMotorDirection *= keepfraction;

                VDetailLog("{0},MoveLinear,nonZero,origdir={1},origvel={2},add={3},notDecay={4},dir={5},vel={6}",
                    m_prim.LocalID, origDir, origVel, addAmount, keepfraction, m_linearMotorDirection, m_lastLinearVelocityVector);
            }
            else
            {
                // if what remains of direction is very small, zero it.
                m_linearMotorDirection = Vector3.Zero;
                m_lastLinearVelocityVector = Vector3.Zero;
                VDetailLog("{0},MoveLinear,zeroed", m_prim.LocalID);
            }

            // convert requested object velocity to object relative vector
            Quaternion rotq = m_prim.Orientation;
            m_newVelocity = m_lastLinearVelocityVector * rotq;

            // Add the various forces into m_dir which will be our new direction vector (velocity)

            // add Gravity and Buoyancy
            // There is some gravity, make a gravity force vector that is applied after object velocity.
            // m_VehicleBuoyancy: -1=2g; 0=1g; 1=0g;
            Vector3 grav = m_prim.Scene.DefaultGravity * (m_prim.Mass * (1f - m_VehicleBuoyancy));

            /*
             * RA: Not sure why one would do this
            // Preserve the current Z velocity
            Vector3 vel_now = m_prim.Velocity;
            m_dir.Z = vel_now.Z;        // Preserve the accumulated falling velocity
             */

            Vector3 pos = m_prim.Position;
//            Vector3 accel = new Vector3(-(m_dir.X - m_lastLinearVelocityVector.X / 0.1f), -(m_dir.Y - m_lastLinearVelocityVector.Y / 0.1f), m_dir.Z - m_lastLinearVelocityVector.Z / 0.1f);

            // If below the terrain, move us above the ground a little.
            float terrainHeight = m_prim.Scene.TerrainManager.GetTerrainHeightAtXYZ(pos);
            // Taking the rotated size doesn't work here because m_prim.Size is the size of the root prim and not the linkset.
            //     Need to add a m_prim.LinkSet.Size similar to m_prim.LinkSet.Mass.
            // Vector3 rotatedSize = m_prim.Size * m_prim.Orientation;
            // if (rotatedSize.Z < terrainHeight)
            if (pos.Z < terrainHeight)
            {
                pos.Z = terrainHeight + 2;
                m_prim.Position = pos;
                VDetailLog("{0},MoveLinear,terrainHeight,terrainHeight={1},pos={2}", m_prim.LocalID, terrainHeight, pos);
            }

            // Check if hovering
=======
            m_lastPositionVector = VehiclePosition;

            // If we forced the changing of some vehicle parameters, update the values and
            //      for the physics engine to note the changes so an UpdateProperties event will happen.
            PushKnownChanged();

            if (PhysicsScene.VehiclePhysicalLoggingEnabled)
                PhysicsScene.PE.DumpRigidBody(PhysicsScene.World, Prim.PhysBody);

            VDetailLog("{0},BSDynamics.Step,done,pos={1}, force={2},velocity={3},angvel={4}",
                    Prim.LocalID, VehiclePosition, m_knownForce, VehicleVelocity, VehicleRotationalVelocity);
        }

        // Apply the effect of the linear motor and other linear motions (like hover and float).
        private void MoveLinear(float pTimestep)
        {
            ComputeLinearVelocity(pTimestep);

            ComputeLinearTerrainHeightCorrection(pTimestep);

            ComputeLinearHover(pTimestep);

            ComputeLinearBlockingEndPoint(pTimestep);

            ComputeLinearMotorUp(pTimestep);

            ApplyGravity(pTimestep);

            // If not changing some axis, reduce out velocity
            if ((m_flags & (VehicleFlag.NO_X | VehicleFlag.NO_Y | VehicleFlag.NO_Z)) != 0)
            {
                Vector3 vel = VehicleVelocity;
                if ((m_flags & (VehicleFlag.NO_X)) != 0)
                    vel.X = 0;
                if ((m_flags & (VehicleFlag.NO_Y)) != 0)
                    vel.Y = 0;
                if ((m_flags & (VehicleFlag.NO_Z)) != 0)
                    vel.Z = 0;
                VehicleVelocity = vel;
            }

            // ==================================================================
            // Clamp high or low velocities
            float newVelocityLengthSq = VehicleVelocity.LengthSquared();
            if (newVelocityLengthSq > 1000f)
            {
                VehicleVelocity /= VehicleVelocity.Length();
                VehicleVelocity *= 1000f;
            }
            else if (newVelocityLengthSq < 0.001f)
                VehicleVelocity = Vector3.Zero;

            VDetailLog("{0},  MoveLinear,done,isColl={1},newVel={2}", Prim.LocalID, Prim.IsColliding, VehicleVelocity );

        } // end MoveLinear()

        public void ComputeLinearVelocity(float pTimestep)
        {
            Vector3 linearMotorStep = m_linearMotor.Step(pTimestep);

            // The movement computed in the linear motor is relative to the vehicle
            //     coordinates. Rotate the movement to world coordinates.
            Vector3 linearMotorVelocity = linearMotorStep * VehicleOrientation;

            // If we're a ground vehicle, don't loose any Z action (like gravity acceleration).
            float mixFactor = 1f;   // 1 means use all linear motor Z value, 0 means use all existing Z
            if ((m_flags & VehicleFlag.LIMIT_MOTOR_UP) != 0)
            {
                if (!Prim.IsColliding)
                {
                    // If a ground vehicle and not on the ground, I want gravity effect
                    mixFactor = 0.2f;
                }
            }
            else
            {
                // I'm not a ground vehicle but don't totally loose the effect of the environment
                mixFactor = 0.8f;
            }
            linearMotorVelocity.Z = mixFactor * linearMotorVelocity.Z + (1f - mixFactor) * VehicleVelocity.Z;

            // What we want to contribute to the vehicle's existing velocity
            Vector3 linearMotorForce = linearMotorVelocity - VehicleVelocity;

            // Act against the inertia of the vehicle
            linearMotorForce *= m_vehicleMass;

            VehicleAddForceImpulse(linearMotorForce * pTimestep);

            VDetailLog("{0},  MoveLinear,velocity,vehVel={1},step={2},stepVel={3},mix={4},force={5}",
                        Prim.LocalID, VehicleVelocity, linearMotorStep, linearMotorVelocity, mixFactor, linearMotorForce);
        }

        public void ComputeLinearTerrainHeightCorrection(float pTimestep)
        {
            // If below the terrain, move us above the ground a little.
            // TODO: Consider taking the rotated size of the object or possibly casting a ray.
            if (VehiclePosition.Z < GetTerrainHeight(VehiclePosition))
            {
                // Force position because applying force won't get the vehicle through the terrain
                Vector3 newPosition = VehiclePosition;
                newPosition.Z = GetTerrainHeight(VehiclePosition) + 1f;
                VehiclePosition = newPosition;
                VDetailLog("{0},  MoveLinear,terrainHeight,terrainHeight={1},pos={2}",
                        Prim.LocalID, GetTerrainHeight(VehiclePosition), VehiclePosition);
            }
        }

        public void ComputeLinearHover(float pTimestep)
        {
            // m_VhoverEfficiency: 0=bouncy, 1=totally damped
            // m_VhoverTimescale: time to achieve height
>>>>>>> upstream/master
            if ((m_flags & (VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY | VehicleFlag.HOVER_GLOBAL_HEIGHT)) != 0)
            {
                // We should hover, get the target height
                if ((m_flags & VehicleFlag.HOVER_WATER_ONLY) != 0)
                {
<<<<<<< HEAD
                    m_VhoverTargetHeight = m_prim.Scene.GetWaterLevelAtXYZ(pos) + m_VhoverHeight;
                }
                if ((m_flags & VehicleFlag.HOVER_TERRAIN_ONLY) != 0)
                {
                    m_VhoverTargetHeight = terrainHeight + m_VhoverHeight;
=======
                    m_VhoverTargetHeight = GetWaterLevel(VehiclePosition) + m_VhoverHeight;
                }
                if ((m_flags & VehicleFlag.HOVER_TERRAIN_ONLY) != 0)
                {
                    m_VhoverTargetHeight = GetTerrainHeight(VehiclePosition) + m_VhoverHeight;
>>>>>>> upstream/master
                }
                if ((m_flags & VehicleFlag.HOVER_GLOBAL_HEIGHT) != 0)
                {
                    m_VhoverTargetHeight = m_VhoverHeight;
                }

                if ((m_flags & VehicleFlag.HOVER_UP_ONLY) != 0)
                {
<<<<<<< HEAD
                    // If body is aready heigher, use its height as target height
                    if (pos.Z > m_VhoverTargetHeight) m_VhoverTargetHeight = pos.Z;
=======
                    // If body is already heigher, use its height as target height
                    if (VehiclePosition.Z > m_VhoverTargetHeight)
                        m_VhoverTargetHeight = VehiclePosition.Z;
>>>>>>> upstream/master
                }
                if ((m_flags & VehicleFlag.LOCK_HOVER_HEIGHT) != 0)
                {
<<<<<<< HEAD
                    if ((pos.Z - m_VhoverTargetHeight) > .2 || (pos.Z - m_VhoverTargetHeight) < -.2)
                    {
                        m_prim.Position = pos;
=======
                    if (Math.Abs(VehiclePosition.Z - m_VhoverTargetHeight) > 0.2f)
                    {
                        Vector3 pos = VehiclePosition;
                        pos.Z = m_VhoverTargetHeight;
                        VehiclePosition = pos;

                        VDetailLog("{0},  MoveLinear,hover,pos={1},lockHoverHeight", Prim.LocalID, pos);
>>>>>>> upstream/master
                    }
                }
                else
                {
<<<<<<< HEAD
                    float herr0 = pos.Z - m_VhoverTargetHeight;
                    // Replace Vertical speed with correction figure if significant
                    if (Math.Abs(herr0) > 0.01f)
                    {
                        m_newVelocity.Z = -((herr0 * pTimestep * 50.0f) / m_VhoverTimescale);
                        //KF: m_VhoverEfficiency is not yet implemented
                    }
                    else
                    {
                        m_newVelocity.Z = 0f;
                    }
                }

                VDetailLog("{0},MoveLinear,hover,pos={1},dir={2},height={3},target={4}", m_prim.LocalID, pos, m_newVelocity, m_VhoverHeight, m_VhoverTargetHeight);
            }

=======
                    // Error is positive if below the target and negative if above.
                    Vector3 hpos = VehiclePosition;
                    float verticalError = m_VhoverTargetHeight - hpos.Z;
                    float verticalCorrection = verticalError / m_VhoverTimescale;
                    verticalCorrection *= m_VhoverEfficiency;

                    hpos.Z += verticalCorrection;
                    VehiclePosition = hpos;

                    // Since we are hovering, we need to do the opposite of falling -- get rid of world Z
                    Vector3 vel = VehicleVelocity;
                    vel.Z = 0f;
                    VehicleVelocity = vel;

                    /*
                    float verticalCorrectionVelocity = verticalError / m_VhoverTimescale;
                    Vector3 verticalCorrection = new Vector3(0f, 0f, verticalCorrectionVelocity);
                    verticalCorrection *= m_vehicleMass;

                    // TODO: implement m_VhoverEfficiency correctly
                    VehicleAddForceImpulse(verticalCorrection);
                     */

                    VDetailLog("{0},  MoveLinear,hover,pos={1},eff={2},hoverTS={3},height={4},target={5},err={6},corr={7}",
                                    Prim.LocalID, VehiclePosition, m_VhoverEfficiency,
                                    m_VhoverTimescale, m_VhoverHeight, m_VhoverTargetHeight,
                                    verticalError, verticalCorrection);
                }

            }
        }

        public bool ComputeLinearBlockingEndPoint(float pTimestep)
        {
            bool changed = false;

            Vector3 pos = VehiclePosition;
>>>>>>> upstream/master
            Vector3 posChange = pos - m_lastPositionVector;
            if (m_BlockingEndPoint != Vector3.Zero)
            {
                bool changed = false;
                if (pos.X >= (m_BlockingEndPoint.X - (float)1))
                {
                    pos.X -= posChange.X + 1;
                    changed = true;
                }
                if (pos.Y >= (m_BlockingEndPoint.Y - (float)1))
                {
                    pos.Y -= posChange.Y + 1;
                    changed = true;
                }
                if (pos.Z >= (m_BlockingEndPoint.Z - (float)1))
                {
                    pos.Z -= posChange.Z + 1;
                    changed = true;
                }
                if (pos.X <= 0)
                {
                    pos.X += posChange.X + 1;
                    changed = true;
                }
                if (pos.Y <= 0)
                {
                    pos.Y += posChange.Y + 1;
                    changed = true;
                }
                if (changed)
                {
<<<<<<< HEAD
                    m_prim.Position = pos;
                    VDetailLog("{0},MoveLinear,blockingEndPoint,block={1},origPos={2},pos={3}",
                                m_prim.LocalID, m_BlockingEndPoint, posChange, pos);
=======
                    VehiclePosition = pos;
                    VDetailLog("{0},  MoveLinear,blockingEndPoint,block={1},origPos={2},pos={3}",
                                Prim.LocalID, m_BlockingEndPoint, posChange, pos);
>>>>>>> upstream/master
                }
            }

<<<<<<< HEAD
            float Zchange = Math.Abs(posChange.Z);
            if ((m_flags & (VehicleFlag.LIMIT_MOTOR_UP)) != 0)
            {
                if (Zchange > .3)
                    grav.Z = (float)(grav.Z * 3);
                if (Zchange > .15)
                    grav.Z = (float)(grav.Z * 2);
                if (Zchange > .75)
                    grav.Z = (float)(grav.Z * 1.5);
                if (Zchange > .05)
                    grav.Z = (float)(grav.Z * 1.25);
                if (Zchange > .025)
                    grav.Z = (float)(grav.Z * 1.125);
                float postemp = (pos.Z - terrainHeight);
                if (postemp > 2.5f)
                    grav.Z = (float)(grav.Z * 1.037125);
                VDetailLog("{0},MoveLinear,limitMotorUp,grav={1}", m_prim.LocalID, grav);
            }
            if ((m_flags & (VehicleFlag.NO_X)) != 0)
                m_newVelocity.X = 0;
            if ((m_flags & (VehicleFlag.NO_Y)) != 0)
                m_newVelocity.Y = 0;
            if ((m_flags & (VehicleFlag.NO_Z)) != 0)
                m_newVelocity.Z = 0;

            // Apply velocity
            m_prim.Velocity = m_newVelocity;
            // apply gravity force
            // Why is this set here? The physics engine already does gravity.
            // m_prim.AddForce(grav, false);

            // Apply friction
            Vector3 keepFraction = Vector3.One - (Vector3.One / (m_linearFrictionTimescale / pTimestep));
            m_lastLinearVelocityVector *= keepFraction;

            VDetailLog("{0},MoveLinear,done,lmDir={1},lmVel={2},newVel={3},grav={4},1Mdecay={5}", 
                    m_prim.LocalID, m_linearMotorDirection, m_lastLinearVelocityVector, m_newVelocity, grav, keepFraction);

        } // end MoveLinear()

        private void MoveAngular(float pTimestep)
        {
            // m_angularMotorDirection         // angular velocity requested by LSL motor
            // m_angularMotorApply             // application frame counter
            // m_angularMotorVelocity          // current angular motor velocity (ramps up and down)
            // m_angularMotorTimescale         // motor angular velocity ramp up rate
            // m_angularMotorDecayTimescale    // motor angular velocity decay rate
            // m_angularFrictionTimescale      // body angular velocity  decay rate
            // m_lastAngularVelocity           // what was last applied to body

            // Get what the body is doing, this includes 'external' influences
            Vector3 angularVelocity = m_prim.RotationalVelocity;

            if (m_angularMotorApply > 0)
            {
                // Rather than snapping the angular motor velocity from the old value to
                //    a newly set velocity, this routine steps the value from the previous
                //    value (m_angularMotorVelocity) to the requested value (m_angularMotorDirection).
                // There are m_angularMotorApply steps.
                Vector3 origAngularVelocity = m_angularMotorVelocity;
                // ramp up to new value
                //   current velocity    +=                         error                          /    (  time to get there   / step interval)
                //                               requested speed       -       last motor speed
                m_angularMotorVelocity.X += (m_angularMotorDirection.X - m_angularMotorVelocity.X) /  (m_angularMotorTimescale / pTimestep);
                m_angularMotorVelocity.Y += (m_angularMotorDirection.Y - m_angularMotorVelocity.Y) /  (m_angularMotorTimescale / pTimestep);
                m_angularMotorVelocity.Z += (m_angularMotorDirection.Z - m_angularMotorVelocity.Z) /  (m_angularMotorTimescale / pTimestep);

                VDetailLog("{0},MoveAngular,angularMotorApply,apply={1},angTScale={2},timeStep={3},origvel={4},dir={5},vel={6}", 
                        m_prim.LocalID, m_angularMotorApply, m_angularMotorTimescale, pTimestep, origAngularVelocity, m_angularMotorDirection, m_angularMotorVelocity);

                // This is done so that if script request rate is less than phys frame rate the expected
                //    velocity may still be acheived.
                m_angularMotorApply--;
            }
            else
            {
                // No motor recently applied, keep the body velocity
                // and decay the velocity
                m_angularMotorVelocity -= m_angularMotorVelocity /  (m_angularMotorDecayTimescale / pTimestep);
                if (m_angularMotorVelocity.LengthSquared() < 0.00001)
                    m_angularMotorVelocity = Vector3.Zero;
            } // end motor section

            // Vertical attractor section
            Vector3 vertattr = Vector3.Zero;
            if (m_verticalAttractionTimescale < 300)
            {
                float VAservo = 0.2f / (m_verticalAttractionTimescale / pTimestep);
                // get present body rotation
                Quaternion rotq = m_prim.Orientation;
                // make a vector pointing up
                Vector3 verterr = Vector3.Zero;
                verterr.Z = 1.0f;
                // rotate it to Body Angle
                verterr = verterr * rotq;
                // verterr.X and .Y are the World error ammounts. They are 0 when there is no error (Vehicle Body is 'vertical'), and .Z will be 1.
                // As the body leans to its side |.X| will increase to 1 and .Z fall to 0. As body inverts |.X| will fall and .Z will go
                // negative. Similar for tilt and |.Y|. .X and .Y must be modulated to prevent a stable inverted body.
                if (verterr.Z < 0.0f)
                {
                    verterr.X = 2.0f - verterr.X;
                    verterr.Y = 2.0f - verterr.Y;
                }
                // Error is 0 (no error) to +/- 2 (max error)
                // scale it by VAservo
                verterr = verterr * VAservo;

                // As the body rotates around the X axis, then verterr.Y increases; Rotated around Y then .X increases, so
                // Change  Body angular velocity  X based on Y, and Y based on X. Z is not changed.
                vertattr.X =    verterr.Y;
                vertattr.Y =  - verterr.X;
                vertattr.Z = 0f;

                // scaling appears better usingsquare-law
                float bounce = 1.0f - (m_verticalAttractionEfficiency * m_verticalAttractionEfficiency);
                vertattr.X += bounce * angularVelocity.X;
                vertattr.Y += bounce * angularVelocity.Y;

                VDetailLog("{0},MoveAngular,verticalAttraction,verterr={1},bounce={2},vertattr={3}", 
                            m_prim.LocalID, verterr, bounce, vertattr);

            } // else vertical attractor is off

            // m_lastVertAttractor = vertattr;
=======
        // From http://wiki.secondlife.com/wiki/LlSetVehicleFlags :
        //    Prevent ground vehicles from motoring into the sky. This flag has a subtle effect when
        //    used with conjunction with banking: the strength of the banking will decay when the
        //    vehicle no longer experiences collisions. The decay timescale is the same as
        //    VEHICLE_BANKING_TIMESCALE. This is to help prevent ground vehicles from steering
        //    when they are in mid jump. 
        // TODO: this code is wrong. Also, what should it do for boats (height from water)?
        //    This is just using the ground and a general collision check. Should really be using
        //    a downward raycast to find what is below.
        public void ComputeLinearMotorUp(float pTimestep)
        {
            Vector3 ret = Vector3.Zero;

            if ((m_flags & (VehicleFlag.LIMIT_MOTOR_UP)) != 0)
            {
                // This code tries to decide if the object is not on the ground and then pushing down
                /*
                float targetHeight = Type == Vehicle.TYPE_BOAT ? GetWaterLevel(VehiclePosition) : GetTerrainHeight(VehiclePosition);
                distanceAboveGround = VehiclePosition.Z - targetHeight;
                // Not colliding if the vehicle is off the ground
                if (!Prim.IsColliding)
                {
                    // downForce = new Vector3(0, 0, -distanceAboveGround / m_bankingTimescale);
                    VehicleVelocity += new Vector3(0, 0, -distanceAboveGround);
                }
                // TODO: this calculation is wrong. From the description at
                //     (http://wiki.secondlife.com/wiki/Category:LSL_Vehicle), the downForce
                //     has a decay factor. This says this force should
                //     be computed with a motor.
                // TODO: add interaction with banking.
                VDetailLog("{0},  MoveLinear,limitMotorUp,distAbove={1},colliding={2},ret={3}",
                                Prim.LocalID, distanceAboveGround, Prim.IsColliding, ret);
                 */

                // Another approach is to measure if we're going up. If going up and not colliding,
                //     the vehicle is in the air.  Fix that by pushing down.
                if (!Prim.IsColliding && VehicleVelocity.Z > 0.1)
                {
                    // Get rid of any of the velocity vector that is pushing us up.
                    float upVelocity = VehicleVelocity.Z;
                    VehicleVelocity += new Vector3(0, 0, -upVelocity);

                    /*
                    // If we're pointed up into the air, we should nose down
                    Vector3 pointingDirection = Vector3.UnitX * VehicleOrientation;
                    // The rotation around the Y axis is pitch up or down
                    if (pointingDirection.Y > 0.01f)
                    {
                        float angularCorrectionForce = -(float)Math.Asin(pointingDirection.Y);
                        Vector3 angularCorrectionVector = new Vector3(0f, angularCorrectionForce, 0f);
                        // Rotate into world coordinates and apply to vehicle
                        angularCorrectionVector *= VehicleOrientation;
                        VehicleAddAngularForce(angularCorrectionVector);
                        VDetailLog("{0},  MoveLinear,limitMotorUp,newVel={1},pntDir={2},corrFrc={3},aCorr={4}",
                                    Prim.LocalID, VehicleVelocity, pointingDirection, angularCorrectionForce, angularCorrectionVector);
                    }
                        */
                    VDetailLog("{0},  MoveLinear,limitMotorUp,collide={1},upVel={2},newVel={3}",
                                    Prim.LocalID, Prim.IsColliding, upVelocity, VehicleVelocity);
                }
            }
        }

        private void ApplyGravity(float pTimeStep)
        {
            Vector3 appliedGravity = m_VehicleGravity * m_vehicleMass;
            VehicleAddForce(appliedGravity);

            VDetailLog("{0},  MoveLinear,applyGravity,vehGrav={1},appliedForce-{2}", 
                            Prim.LocalID, m_VehicleGravity, appliedGravity);
        }

        // =======================================================================
        // =======================================================================
        // Apply the effect of the angular motor.
        // The 'contribution' is how much angular correction velocity each function wants.
        //     All the contributions are added together and the resulting velocity is
        //     set directly on the vehicle.
        private void MoveAngular(float pTimestep)
        {
            // The user wants this many radians per second angular change?
            Vector3 angularMotorContribution = m_angularMotor.Step(pTimestep);

            // ==================================================================
            // From http://wiki.secondlife.com/wiki/LlSetVehicleFlags :
            //    This flag prevents linear deflection parallel to world z-axis. This is useful
            //    for preventing ground vehicles with large linear deflection, like bumper cars,
            //    from climbing their linear deflection into the sky. 
            // That is, NO_DEFLECTION_UP says angular motion should not add any pitch or roll movement
            // TODO: This is here because this is where ODE put it but documentation says it
            //    is a linear effect. Where should this check go?
            if ((m_flags & (VehicleFlag.NO_DEFLECTION_UP)) != 0)
            {
                angularMotorContribution.X = 0f;
                angularMotorContribution.Y = 0f;
                VDetailLog("{0},  MoveAngular,noDeflectionUp,angularMotorContrib={1}", Prim.LocalID, angularMotorContribution);
            }

            Vector3 verticalAttractionContribution = ComputeAngularVerticalAttraction();

            Vector3 deflectionContribution = ComputeAngularDeflection();

            Vector3 bankingContribution = ComputeAngularBanking();
>>>>>>> upstream/master

            // Bank section tba

            // Deflection section tba

<<<<<<< HEAD
            // Sum velocities
            m_lastAngularVelocity = m_angularMotorVelocity + vertattr; // + bank + deflection
            
            if ((m_flags & (VehicleFlag.NO_DEFLECTION_UP)) != 0)
            {
                m_lastAngularVelocity.X = 0;
                m_lastAngularVelocity.Y = 0;
                VDetailLog("{0},MoveAngular,noDeflectionUp,lastAngular={1}", m_prim.LocalID, m_lastAngularVelocity);
            }

            if (m_lastAngularVelocity.ApproxEquals(Vector3.Zero, 0.01f))
            {
                m_lastAngularVelocity = Vector3.Zero; // Reduce small value to zero.
                VDetailLog("{0},MoveAngular,zeroSmallValues,lastAngular={1}", m_prim.LocalID, m_lastAngularVelocity);
            }

             // apply friction
            Vector3 decayamount = Vector3.One / (m_angularFrictionTimescale / pTimestep);
            m_lastAngularVelocity -= m_lastAngularVelocity * decayamount;

            // Apply to the body
            m_prim.RotationalVelocity = m_lastAngularVelocity;

            VDetailLog("{0},MoveAngular,done,decay={1},lastAngular={2}", m_prim.LocalID, decayamount, m_lastAngularVelocity);
        } //end MoveAngular

        internal void LimitRotation(float timestep)
        {
            Quaternion rotq = m_prim.Orientation;
=======
            m_lastAngularVelocity = angularMotorContribution
                                    + verticalAttractionContribution
                                    + deflectionContribution
                                    + bankingContribution;

            // Add of the above computation are made relative to vehicle coordinates.
            // Convert to world coordinates.
            m_lastAngularVelocity *= VehicleOrientation;

            // ==================================================================
            // Apply the correction velocity.
            // TODO: Should this be applied as an angular force (torque)?
            if (!m_lastAngularVelocity.ApproxEquals(Vector3.Zero, 0.01f))
            {
                VehicleRotationalVelocity = m_lastAngularVelocity;

                VDetailLog("{0},  MoveAngular,done,nonZero,angMotorContrib={1},vertAttrContrib={2},bankContrib={3},deflectContrib={4},totalContrib={5}",
                                    Prim.LocalID,
                                    angularMotorContribution, verticalAttractionContribution,
                                    bankingContribution, deflectionContribution,
                                    m_lastAngularVelocity
                                    );
            }
            else
            {
                // The vehicle is not adding anything angular wise.
                VehicleRotationalVelocity = Vector3.Zero;
                VDetailLog("{0},  MoveAngular,done,zero", Prim.LocalID);
            }

            // ==================================================================
            //Offset section
            if (m_linearMotorOffset != Vector3.Zero)
            {
                //Offset of linear velocity doesn't change the linear velocity,
                //   but causes a torque to be applied, for example...
                //
                //      IIIII     >>>   IIIII
                //      IIIII     >>>    IIIII
                //      IIIII     >>>     IIIII
                //          ^
                //          |  Applying a force at the arrow will cause the object to move forward, but also rotate
                //
                //
                // The torque created is the linear velocity crossed with the offset

                // TODO: this computation should be in the linear section
                //    because that is where we know the impulse being applied.
                Vector3 torqueFromOffset = Vector3.Zero;
                // torqueFromOffset = Vector3.Cross(m_linearMotorOffset, appliedImpulse);
                if (float.IsNaN(torqueFromOffset.X))
                    torqueFromOffset.X = 0;
                if (float.IsNaN(torqueFromOffset.Y))
                    torqueFromOffset.Y = 0;
                if (float.IsNaN(torqueFromOffset.Z))
                    torqueFromOffset.Z = 0;

                VehicleAddAngularForce(torqueFromOffset * m_vehicleMass);
                VDetailLog("{0},  BSDynamic.MoveAngular,motorOffset,applyTorqueImpulse={1}", Prim.LocalID, torqueFromOffset);
            }

        }
        // From http://wiki.secondlife.com/wiki/Linden_Vehicle_Tutorial:
        //      Some vehicles, like boats, should always keep their up-side up. This can be done by
        //      enabling the "vertical attractor" behavior that springs the vehicle's local z-axis to
        //      the world z-axis (a.k.a. "up"). To take advantage of this feature you would set the
        //      VEHICLE_VERTICAL_ATTRACTION_TIMESCALE to control the period of the spring frequency,
        //      and then set the VEHICLE_VERTICAL_ATTRACTION_EFFICIENCY to control the damping. An
        //      efficiency of 0.0 will cause the spring to wobble around its equilibrium, while an
        //      efficiency of 1.0 will cause the spring to reach its equilibrium with exponential decay.
        public Vector3 ComputeAngularVerticalAttraction()
        {
            Vector3 ret = Vector3.Zero;

            // If vertical attaction timescale is reasonable
            if (enableAngularVerticalAttraction && m_verticalAttractionTimescale < m_verticalAttractionCutoff)
            {
                // Take a vector pointing up and convert it from world to vehicle relative coords.
                Vector3 verticalError = Vector3.UnitZ * VehicleOrientation;

                // If vertical attraction correction is needed, the vector that was pointing up (UnitZ)
                //    is now:
                //    leaning to one side: rotated around the X axis with the Y value going
                //        from zero (nearly straight up) to one (completely to the side)) or
                //    leaning front-to-back: rotated around the Y axis with the value of X being between
                //         zero and one.
                // The value of Z is how far the rotation is off with 1 meaning none and 0 being 90 degrees.

                // Y error means needed rotation around X axis and visa versa.
                // Since the error goes from zero to one, the asin is the corresponding angle.
                ret.X = (float)Math.Asin(verticalError.Y);
                // (Tilt forward (positive X) needs to tilt back (rotate negative) around Y axis.)
                ret.Y = -(float)Math.Asin(verticalError.X);

                // If verticalError.Z is negative, the vehicle is upside down. Add additional push.
                if (verticalError.Z < 0f)
                {
                    ret.X += PIOverFour;
                    ret.Y += PIOverFour;
                }

                // 'ret' is now the necessary velocity to correct tilt in one second.
                //     Correction happens over a number of seconds.
                Vector3 unscaledContrib = ret;
                ret /= m_verticalAttractionTimescale;

                VDetailLog("{0},  MoveAngular,verticalAttraction,,verticalError={1},unscaled={2},eff={3},ts={4},vertAttr={5}",
                                Prim.LocalID, verticalError, unscaledContrib, m_verticalAttractionEfficiency, m_verticalAttractionTimescale, ret);
            }
            return ret;
        }

        // Return the angular correction to correct the direction the vehicle is pointing to be
        //      the direction is should want to be pointing.
        // The vehicle is moving in some direction and correct its orientation to it is pointing
        //     in that direction.
        // TODO: implement reference frame.
        public Vector3 ComputeAngularDeflection()
        {
            Vector3 ret = Vector3.Zero;

            // Since angularMotorUp and angularDeflection are computed independently, they will calculate
            //     approximately the same X or Y correction. When added together (when contributions are combined)
            //     this creates an over-correction and then wabbling as the target is overshot.
            // TODO: rethink how the different correction computations inter-relate.

            if (enableAngularDeflection && m_angularDeflectionEfficiency != 0 && VehicleForwardSpeed > 0.2)
            {
                // The direction the vehicle is moving
                Vector3 movingDirection = VehicleVelocity;
                movingDirection.Normalize();

                // If the vehicle is going backward, it is still pointing forward
                movingDirection *= Math.Sign(VehicleForwardSpeed);

                // The direction the vehicle is pointing
                Vector3 pointingDirection = Vector3.UnitX * VehicleOrientation;
                pointingDirection.Normalize();

                // The difference between what is and what should be.
                Vector3 deflectionError = movingDirection - pointingDirection;

                // Don't try to correct very large errors (not our job)
                // if (Math.Abs(deflectionError.X) > PIOverFour) deflectionError.X = PIOverTwo * Math.Sign(deflectionError.X);
                // if (Math.Abs(deflectionError.Y) > PIOverFour) deflectionError.Y = PIOverTwo * Math.Sign(deflectionError.Y);
                // if (Math.Abs(deflectionError.Z) > PIOverFour) deflectionError.Z = PIOverTwo * Math.Sign(deflectionError.Z);
                if (Math.Abs(deflectionError.X) > PIOverFour) deflectionError.X = 0f;
                if (Math.Abs(deflectionError.Y) > PIOverFour) deflectionError.Y = 0f;
                if (Math.Abs(deflectionError.Z) > PIOverFour) deflectionError.Z = 0f;

                // ret = m_angularDeflectionCorrectionMotor(1f, deflectionError);

                // Scale the correction by recovery timescale and efficiency
                ret = (-deflectionError) * m_angularDeflectionEfficiency;
                ret /= m_angularDeflectionTimescale;

                VDetailLog("{0},  MoveAngular,Deflection,movingDir={1},pointingDir={2},deflectError={3},ret={4}",
                    Prim.LocalID, movingDirection, pointingDirection, deflectionError, ret);
                VDetailLog("{0},  MoveAngular,Deflection,fwdSpd={1},defEff={2},defTS={3}",
                    Prim.LocalID, VehicleForwardSpeed, m_angularDeflectionEfficiency, m_angularDeflectionTimescale);
            }
            return ret;
        }

        // Return an angular change to rotate the vehicle around the Z axis when the vehicle
        //     is tipped around the X axis.
        // From http://wiki.secondlife.com/wiki/Linden_Vehicle_Tutorial:
        //      The vertical attractor feature must be enabled in order for the banking behavior to
        //      function. The way banking works is this: a rotation around the vehicle's roll-axis will
        //      produce a angular velocity around the yaw-axis, causing the vehicle to turn. The magnitude
        //      of the yaw effect will be proportional to the
        //          VEHICLE_BANKING_EFFICIENCY, the angle of the roll rotation, and sometimes the vehicle's
        //                 velocity along its preferred axis of motion. 
        //          The VEHICLE_BANKING_EFFICIENCY can vary between -1 and +1. When it is positive then any
        //                  positive rotation (by the right-hand rule) about the roll-axis will effect a
        //                  (negative) torque around the yaw-axis, making it turn to the right--that is the
        //                  vehicle will lean into the turn, which is how real airplanes and motorcycle's work.
        //                  Negating the banking coefficient will make it so that the vehicle leans to the
        //                  outside of the turn (not very "physical" but might allow interesting vehicles so why not?). 
        //           The VEHICLE_BANKING_MIX is a fake (i.e. non-physical) parameter that is useful for making
        //                  banking vehicles do what you want rather than what the laws of physics allow.
        //                  For example, consider a real motorcycle...it must be moving forward in order for
        //                  it to turn while banking, however video-game motorcycles are often configured
        //                  to turn in place when at a dead stop--because they are often easier to control
        //                  that way using the limited interface of the keyboard or game controller. The
        //                  VEHICLE_BANKING_MIX enables combinations of both realistic and non-realistic
        //                  banking by functioning as a slider between a banking that is correspondingly
        //                  totally static (0.0) and totally dynamic (1.0). By "static" we mean that the
        //                  banking effect depends only on the vehicle's rotation about its roll-axis compared
        //                  to "dynamic" where the banking is also proportional to its velocity along its
        //                  roll-axis. Finding the best value of the "mixture" will probably require trial and error. 
        //      The time it takes for the banking behavior to defeat a preexisting angular velocity about the
        //      world z-axis is determined by the VEHICLE_BANKING_TIMESCALE. So if you want the vehicle to
        //      bank quickly then give it a banking timescale of about a second or less, otherwise you can
        //      make a sluggish vehicle by giving it a timescale of several seconds. 
        public Vector3 ComputeAngularBanking()
        {
            Vector3 ret = Vector3.Zero;

            if (enableAngularBanking && m_bankingEfficiency != 0 && m_verticalAttractionTimescale < m_verticalAttractionCutoff)
            {
                // Rotate a UnitZ vector (pointing up) to how the vehicle is oriented.
                // As the vehicle rolls to the right or left, the Y value will increase from
                //     zero (straight up) to 1 or -1 (full tilt right  or left)
                Vector3 rollComponents = Vector3.UnitZ * VehicleOrientation;
                
                // Figure out the yaw value for this much roll.
                // Squared because that seems to give a good value
                float yawAngle = (float)Math.Asin(rollComponents.Y * rollComponents.Y) * m_bankingEfficiency;

                //        actual error  =       static turn error            +           dynamic turn error
                float mixedYawAngle = yawAngle * (1f - m_bankingMix) + yawAngle * m_bankingMix * VehicleForwardSpeed;

                // TODO: the banking effect should not go to infinity but what to limit it to?
                mixedYawAngle = ClampInRange(-20f, mixedYawAngle, 20f);

                // Build the force vector to change rotation from what it is to what it should be
                ret.Z = -mixedYawAngle;

                // Don't do it all at once.
                ret /= m_bankingTimescale;

                VDetailLog("{0},  MoveAngular,Banking,rollComp={1},speed={2},rollComp={3},yAng={4},mYAng={5},ret={6}",
                            Prim.LocalID, rollComponents, VehicleForwardSpeed, rollComponents, yawAngle, mixedYawAngle, ret);
            }
            return ret;
        }

        // This is from previous instantiations of XXXDynamics.cs.
        // Applies roll reference frame.
        // TODO: is this the right way to separate the code to do this operation?
        //    Should this be in MoveAngular()?
        internal void LimitRotation(float timestep)
        {
            Quaternion rotq = VehicleOrientation;
>>>>>>> upstream/master
            Quaternion m_rot = rotq;
            bool changed = false;
            if (m_RollreferenceFrame != Quaternion.Identity)
            {
                if (rotq.X >= m_RollreferenceFrame.X)
                {
                    m_rot.X = rotq.X - (m_RollreferenceFrame.X / 2);
                    changed = true;
                }
                if (rotq.Y >= m_RollreferenceFrame.Y)
                {
                    m_rot.Y = rotq.Y - (m_RollreferenceFrame.Y / 2);
                    changed = true;
                }
                if (rotq.X <= -m_RollreferenceFrame.X)
                {
                    m_rot.X = rotq.X + (m_RollreferenceFrame.X / 2);
                    changed = true;
                }
                if (rotq.Y <= -m_RollreferenceFrame.Y)
                {
                    m_rot.Y = rotq.Y + (m_RollreferenceFrame.Y / 2);
                    changed = true;
                }
                changed = true;
            }
            if ((m_flags & VehicleFlag.LOCK_ROTATION) != 0)
            {
                m_rot.X = 0;
                m_rot.Y = 0;
                changed = true;
            }
            if (changed)
            {
<<<<<<< HEAD
                m_prim.Orientation = m_rot;
                VDetailLog("{0},LimitRotation,done,orig={1},new={2}", m_prim.LocalID, rotq, m_rot);
=======
                VehicleOrientation = m_rot;
                VDetailLog("{0},  LimitRotation,done,orig={1},new={2}", Prim.LocalID, rotq, m_rot);
>>>>>>> upstream/master
            }

        }

        private float ClampInRange(float low, float val, float high)
        {
            return Math.Max(low, Math.Min(val, high));
            // return Utils.Clamp(val, low, high);
        }

        // Invoke the detailed logger and output something if it's enabled.
        private void VDetailLog(string msg, params Object[] args)
        {
            if (m_prim.Scene.VehicleLoggingEnabled)
                m_prim.Scene.PhysicsLogging.Write(msg, args);
        }
    }
}

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
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.Physics.BulletSPlugin
{

public abstract class BSConstraint : IDisposable
{
<<<<<<< HEAD
    protected BulletSim m_world;
=======
    private static string LogHeader = "[BULLETSIM CONSTRAINT]";

    protected BulletWorld m_world;
    protected BSScene PhysicsScene;
>>>>>>> upstream/master
    protected BulletBody m_body1;
    protected BulletBody m_body2;
    protected BulletConstraint m_constraint;
    protected bool m_enabled = false;

<<<<<<< HEAD
    public BSConstraint()
=======
    public BulletBody Body1 { get { return m_body1; } }
    public BulletBody Body2 { get { return m_body2; } }
    public BulletConstraint Constraint { get { return m_constraint; } }
    public abstract ConstraintType Type { get; }
    public bool IsEnabled { get { return m_enabled; } }

    public BSConstraint(BulletWorld world)
>>>>>>> upstream/master
    {
        m_world = world;
        PhysicsScene = m_world.physicsScene;
    }

    public virtual void Dispose()
    {
        if (m_enabled)
        {
            m_enabled = false;
<<<<<<< HEAD
            bool success = BulletSimAPI.DestroyConstraint2(m_world.Ptr, m_constraint.Ptr);
            m_world.scene.DetailLog("{0},BSConstraint.Dispose,taint,body1={1},body2={2},success={3}", BSScene.DetailLogZero, m_body1.ID, m_body2.ID, success);
            m_constraint.Ptr = System.IntPtr.Zero;
=======
            if (m_constraint.HasPhysicalConstraint)
            {
                bool success = PhysicsScene.PE.DestroyConstraint(m_world, m_constraint);
                m_world.physicsScene.DetailLog("{0},BSConstraint.Dispose,taint,id1={1},body1={2},id2={3},body2={4},success={5}",
                                    BSScene.DetailLogZero,
                                    m_body1.ID, m_body1.AddrString,
                                    m_body2.ID, m_body2.AddrString,
                                    success);
                m_constraint.Clear();
            }
>>>>>>> upstream/master
        }
    }

    public BulletBody Body1 { get { return m_body1; } }
    public BulletBody Body2 { get { return m_body2; } }

    public virtual bool SetLinearLimits(Vector3 low, Vector3 high)
    {
        bool ret = false;
        if (m_enabled)
<<<<<<< HEAD
            ret = BulletSimAPI.SetLinearLimits2(m_constraint.Ptr, low, high);
=======
            ret = PhysicsScene.PE.SetLinearLimits(m_constraint, low, high);
>>>>>>> upstream/master
        return ret;
    }

    public virtual bool SetAngularLimits(Vector3 low, Vector3 high)
    {
        bool ret = false;
        if (m_enabled)
<<<<<<< HEAD
            ret = BulletSimAPI.SetAngularLimits2(m_constraint.Ptr, low, high);
=======
            ret = PhysicsScene.PE.SetAngularLimits(m_constraint, low, high);
>>>>>>> upstream/master
        return ret;
    }

    public virtual bool SetSolverIterations(float cnt)
    {
        bool ret = false;
        if (m_enabled)
        {
<<<<<<< HEAD
            BulletSimAPI.SetConstraintNumSolverIterations2(m_constraint.Ptr, cnt);
=======
            PhysicsScene.PE.SetConstraintNumSolverIterations(m_constraint, cnt);
>>>>>>> upstream/master
            ret = true;
        }
        return ret;
    }

    public virtual bool CalculateTransforms()
    {
        bool ret = false;
        if (m_enabled)
        {
            // Recompute the internal transforms
<<<<<<< HEAD
            BulletSimAPI.CalculateTransforms2(m_constraint.Ptr);
=======
            PhysicsScene.PE.CalculateTransforms(m_constraint);
>>>>>>> upstream/master
            ret = true;
        }
        return ret;
    }

    // Reset this constraint making sure it has all its internal structures
    //    recomputed and is enabled and ready to go.
    public virtual bool RecomputeConstraintVariables(float mass)
    {
        bool ret = false;
        if (m_enabled)
        {
            ret = CalculateTransforms();
            if (ret)
            {
                // Setting an object's mass to zero (making it static like when it's selected)
                //     automatically disables the constraints.
                // If the link is enabled, be sure to set the constraint itself to enabled.
<<<<<<< HEAD
                BulletSimAPI.SetConstraintEnable2(m_constraint.Ptr, m_world.scene.NumericBool(true));
=======
                PhysicsScene.PE.SetConstraintEnable(m_constraint, BSParam.NumericBool(true));
>>>>>>> upstream/master
            }
            else
            {
                m_world.scene.Logger.ErrorFormat("[BULLETSIM CONSTRAINT] CalculateTransforms failed. A={0}, B={1}", Body1.ID, Body2.ID);
            }
        }
        return ret;
    }
}
}

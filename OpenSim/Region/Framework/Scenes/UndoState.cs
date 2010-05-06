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
 */

using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.Framework.Scenes
{
    public class UndoState
    {
        public Vector3 Position = Vector3.Zero;
        public Vector3 Scale = Vector3.Zero;
        public Quaternion Rotation = Quaternion.Identity;

        public UndoState(SceneObjectPart part)
        {
            if (part != null)
            {
                if (part.ParentID == 0)
                    Position = part.ParentGroup.AbsolutePosition;
                else
                    Position = part.OffsetPosition;

                Rotation = part.RotationOffset;
                Scale = part.Shape.Scale;
            }
        }

        public bool Compare(SceneObjectPart part)
        {
            if (part != null)
            {
                if (part.ParentID == 0)
                {
                    if (Position == part.ParentGroup.AbsolutePosition && Rotation == part.ParentGroup.Rotation)
                        return true;
                    else
                        return false;
                }
                else
                {
                    if (Position == part.OffsetPosition && Rotation == part.RotationOffset && Scale == part.Shape.Scale)
                        return true;
                    else
                        return false;

                }
            }
            return false;
        }

        public void PlaybackState(SceneObjectPart part)
        {
            if (part != null)
            {
                part.Undoing = true;
                PrimUpdateFlags updateFlags = PrimUpdateFlags.Position | PrimUpdateFlags.Rotation;

                if (part.ParentID == 0)
                    part.ParentGroup.AbsolutePosition = Position;
                else
                    part.OffsetPosition = Position;

                part.RotationOffset = Rotation;

                if (Scale != part.Scale)
                {
                    part.Scale = Scale;
                    updateFlags |= PrimUpdateFlags.Scale;
                }

                part.Undoing = false;
                part.ScheduleUpdate(updateFlags);
            }
        }

        public void PlayfwdState(SceneObjectPart part)
        {
            if (part != null)
            {
                part.Undoing = true;
                PrimUpdateFlags updateFlags = PrimUpdateFlags.Position | PrimUpdateFlags.Rotation;

                if (part.ParentID == 0)
                    part.ParentGroup.AbsolutePosition = Position;
                else
                    part.OffsetPosition = Position;

                part.RotationOffset = Rotation;

                if (Scale != part.Scale)
                {
                    part.Scale = Scale;
                    updateFlags |= PrimUpdateFlags.Scale;
                }

                part.Undoing = false;
                part.ScheduleUpdate(updateFlags);
            }
        }
    }
    public class LandUndoState
    {
        public ITerrainModule m_terrainModule;
        public ITerrainChannel m_terrainChannel;

        public LandUndoState(ITerrainModule terrainModule, ITerrainChannel terrainChannel)
        {
            m_terrainModule = terrainModule;
            m_terrainChannel = terrainChannel;
        }

        public bool Compare(ITerrainChannel terrainChannel)
        {
            if (m_terrainChannel != terrainChannel)
                return false;
            else
                return false;
        }

        public void PlaybackState()
        {
            m_terrainModule.UndoTerrain(m_terrainChannel);
        }
    }
}

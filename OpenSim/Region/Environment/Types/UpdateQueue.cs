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
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System.Collections.Generic;
using System;
using libsecondlife;
using OpenSim.Region.Environment.Scenes;


namespace OpenSim.Region.Environment.Types
{
    public class UpdateQueue
    {
        class SceneObject
        {
            //The distance after to include object size as a priority factor
            static float m_maxSortDistance = 90 * 90;

            public SceneObjectPart m_part;
            public float m_priority;

            public SceneObject(SceneObjectPart part)
            {
                m_part = part;
            }


            public void DeterminePriority(LLVector3 pos)
            {
                m_priority = LLVector3.MagSquared(pos - m_part.AbsolutePosition);
                m_priority -= LLVector3.MagSquared(m_part.Scale) * 12;
                if (m_priority < 0)
                {
                    m_priority = 0;
                }
                else if (m_priority > m_maxSortDistance)
                {
                    m_priority = m_maxSortDistance;
                }
            }
        }

        class SceneObjectComparer : IComparer<SceneObject>
        {
            int IComparer<SceneObject>.Compare(SceneObject a, SceneObject b)
            {
                if (a.m_priority > b.m_priority)
                    return 1;

                if (a.m_priority < b.m_priority)
                    return -1;

                return 0;
            }
        }

        private List<SceneObject> m_queue;
        private Dictionary<LLUUID, LinkedListNode<SceneObjectPart>> m_ids;

        float m_objectResortDistance = 15 * 15;

        LLVector3 m_playerSpherePos = LLVector3.Zero;

        DateTime m_forceRefreshTime;
        bool m_forceRefreshTimeSet = false;

        public UpdateQueue()
        {
            m_queue = new List<SceneObject>();
            m_ids = new Dictionary<LLUUID, LinkedListNode<SceneObjectPart>>();
        }

        public bool HasUpdates()
        {
            if (m_queue.Count > 0)
                return true;

            return false;
        }

        public void ObjectUpdated(SceneObjectPart part)
        {
            lock (m_ids)
            {
                if (!m_ids.ContainsKey(part.UUID))
                {
                    m_queue.Add(new SceneObject(part));
                    m_ids[part.UUID] = null;
                }
                else if (!m_forceRefreshTimeSet)
                {
                    m_forceRefreshTime = DateTime.Now;
                    m_forceRefreshTime.AddSeconds(1);
                    m_forceRefreshTimeSet = true;
                }
            }
        }

        public void UpdateAvatarPosition(LLVector3 avatarPosition)
        {
            if (LLVector3.MagSquared(m_playerSpherePos - avatarPosition) > m_objectResortDistance)
            {
                m_playerSpherePos = avatarPosition;
                CollectNearestItems();
            }
            else if (m_forceRefreshTimeSet && m_forceRefreshTime < DateTime.Now)
            {
                m_playerSpherePos = avatarPosition;
                CollectNearestItems();
            }
        }

        public SceneObjectPart GetClosestUpdate()
        {
            SceneObjectPart part = m_queue[0].m_part;

            lock (m_ids)
            {
                m_queue.RemoveAt(0);
                m_ids.Remove(part.UUID);
            }

            return part;
        }

        protected void CollectNearestItems()
        {
            m_queue.ForEach(delegate(SceneObject obj) { obj.DeterminePriority(m_playerSpherePos); });
            m_queue.Sort(new SceneObjectComparer());

            m_forceRefreshTimeSet = false;
        }
    }
}
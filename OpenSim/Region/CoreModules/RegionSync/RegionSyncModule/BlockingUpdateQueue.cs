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
using System.Threading;
using System.Linq;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    // Class queues updates for UUID's. 
    // The updates could be JSON, serialized object data, or any string. 
    // Updates are queued to the end and dequeued from the front of the queue
    // Enqueuing an update with the same UUID will replace the previous update
    // so it will not lose its place.
    class BlockingUpdateQueue
    {
        private object m_syncRoot = new object();
        private Queue<UUID> m_queue = new Queue<UUID>();
        private Dictionary<UUID, byte[]> m_updates = new Dictionary<UUID, byte[]>();

        // Enqueue an update
        public void Enqueue(UUID id, byte[] update)
        {
            lock(m_syncRoot)
            {
                if (!m_updates.ContainsKey(id))
                    m_queue.Enqueue(id);
                m_updates[id] = update;
                Monitor.Pulse(m_syncRoot);
            }
        }

        // Dequeue an update
        public byte[] Dequeue()
        {
            lock (m_syncRoot)
            {
                // If the queue is empty, wait for it to contain something
                while (m_queue.Count == 0)
                    Monitor.Wait(m_syncRoot);
                UUID id = m_queue.Dequeue();
                byte[] update = m_updates[id];
                m_updates.Remove(id);
                return update;
            }
        }

        // Count of number of items currently queued
        public int Count
        {
            get
            {
                lock (m_syncRoot)
                    return m_queue.Count;
            }
        }
    }
}

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

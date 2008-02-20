using System;
using System.Threading;
using System.Collections.Generic;

namespace OpenSim.Framework
{
    public class PrioritizedQueue<T>
    {
        public delegate void AppendDelegate(LinkedList<Pair<T, int>> list, T item, int priority);
        public delegate int DeterminePriorityDelegate(T item);

        public AppendDelegate Appender { set { Append = value; } get { return Append; } }
        private AppendDelegate Append;

        DeterminePriorityDelegate DeterminePriority;
        public DeterminePriorityDelegate PriorityDeterminer
        {
            set { DeterminePriority = value; }
            get { return DeterminePriority; }
        }

        private LinkedList<Pair<T, int>> List;
        private object _queueSync = new object();

        public PrioritizedQueue()
        {
            Append = new AppendDelegate(this.DefaultAppend);
            List = new LinkedList<Pair<T, int>>();
        }

        /// <summary>
        /// Enqueues the given item and uses DeterminePriority to determine priority
        /// </summary>
        public void Enqueue(T item)
        {
            Enqueue(item, DeterminePriority(item));
        }

        /// <summary>
        /// Enqueues the given item with given priority
        /// </summary>
        public void Enqueue(T item, int priority)
        {
            lock (_queueSync)
            {
                Append(List, item, priority);
                Monitor.Pulse(_queueSync);
            }
        }

        /// <summary>
        /// Enqueues the given item with given priority
        /// </summary>
        public void AddFirst(T item, int priority)
        {
            lock (_queueSync)
            {
                List.AddFirst(new Pair<T, int>(item, priority));
                Monitor.Pulse(_queueSync);
            }
        }

        /// <summary>
        /// Dequeues an item from the queue
        /// </summary>
        /// <remarks>
        /// Blocks until an item is available for dequeueing
        /// </remarks>
        public T Dequeue()
        {
            lock (_queueSync)
            {
                if (List.First == null)
                {
                    Monitor.Wait(_queueSync);
                }

                T item = List.First.Value.First;
                List.RemoveFirst();

                return item;
            }
        }

        public int Count()
        {
            return List.Count;
        }

        public bool HasQueuedItems()
        {
            return (List.First != null);
        }

        /// <summary>
        /// Default appending implementation
        /// </summary>
        private void DefaultAppend(LinkedList<Pair<T, int>> list, T item, int priority)
        {
            LinkedListNode<Pair<T, int>> node = list.Last;

            if (node == null)
            {
                list.AddFirst(new Pair<T, int>(item, priority));
                return;
            }

            while (node.Value.Second < priority)
            {
                node.Value.Second++;

                node = node.Previous;
                if (node == null)
                {
                    list.AddFirst(new Pair<T, int>(item, priority));
                    return;
                }
            }

            list.AddAfter(node, new Pair<T, int>(item, priority));
        }
    }
}
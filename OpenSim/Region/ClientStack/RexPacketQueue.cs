using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

using OpenSim.Framework;
using libsecondlife.Packets;

namespace OpenSim.Region.ClientStack
{
    public class RexPacketQueue
    {
        //Object for syncing enqueue / dequeue
        private object m_queueSync = new object();

        LinkedList<QueItem> m_incoming; //Non throttled packets (highpr. and incoming)
        LinkedList<QueItem> m_nonThrottled; //Non throttled packets (highpr. and incoming)
        PrioritizedQueue<QueItem> m_queue; //Normally prioritized to be sent packets

        PacketPrioritizer m_prioritizer;
        Timer m_throttleUpdater;

        const int m_throttleAdjustInterval = 1000; //Adjust throttle every 5 seconds
        int m_throttleAdjustTimeLeft = m_throttleAdjustInterval;

        const int m_throttleRefreshRate = 200; //200ms
        int m_throttleOutboundMin = (5000 * m_throttleRefreshRate)/1000; //5KB/sec
        int m_throttleOutboundMax = (200000 * m_throttleRefreshRate)/1000; //200KB/sec
      

        //200KB/sec (attempt to start at full speed)
        int m_throttleOutbound = (200000 * m_throttleRefreshRate)/1000;
        
        int m_sentBytes = 0; //Amount of bytes sent currently

        
        int m_resentBytes = 0; //Amount of resent bytes
        int m_resendQueued = 0; //Amount of queued resend packets

        public RexPacketQueue(int startTraffic, int maxTraffic, int minTraffic)
        {
            m_throttleOutboundMin = (minTraffic * m_throttleRefreshRate) / 1000; //5KB/sec
            m_throttleOutboundMax = (maxTraffic * m_throttleRefreshRate) / 1000; //200KB/sec

            //200KB/sec (attempt to start at full speed)
            m_throttleOutbound = (startTraffic * m_throttleRefreshRate) / 1000;
            
            m_queue = new PrioritizedQueue<QueItem>();
            m_incoming = new LinkedList<QueItem>();
            m_nonThrottled = new LinkedList<QueItem>();
            m_prioritizer = new PacketPrioritizer();
            m_prioritizer.Bind(m_queue);

            m_throttleUpdater = new Timer(m_throttleRefreshRate);
            m_throttleUpdater.Elapsed += new ElapsedEventHandler(ThrottleRefresh);
            m_throttleUpdater.Start();
        }

        public void Enqueue(QueItem item)
        {
            lock (m_queueSync)
            {
                if (item.throttleType == ThrottleOutPacketType.Resend)
                    m_resendQueued++;

                if (item.Incoming)
                {
                    m_incoming.AddLast(item);
                }
                else
                {
                    if (item.Packet.Type == PacketType.ImprovedTerseObjectUpdate ||
                        item.Packet.Type == PacketType.CoarseLocationUpdate ||
                        item.Packet.Type == PacketType.AvatarAnimation ||
                        item.Packet.Type == PacketType.StartPingCheck ||
                        item.Packet.Type == PacketType.CompletePingCheck ||
                        item.Packet.Type == PacketType.PacketAck)
                    {
                        m_nonThrottled.AddLast(item);
                    }
                    else
                    {
                        m_queue.Enqueue(item);
                    }
                    if (item.Packet.Header.Resent)
                    {
                        m_resentBytes += item.Packet.ToBytes().Length;
                    }
                }
                Monitor.Pulse(m_queueSync);
            }
        }

        public QueItem Dequeue()
        {
            while (true)
            {
                lock (m_queueSync)
                {
                    QueItem item = null;

                    //Immediately process incoming packets
                    if (m_incoming.First != null)
                    {
                        item = m_incoming.First.Value;
                        m_incoming.RemoveFirst();
                    }
                    //Then try to pick a super-high-priority packet first
                    else if (m_nonThrottled.First != null)
                    {
                        item = m_nonThrottled.First.Value;
                        m_nonThrottled.RemoveFirst();
                    }
                    //Then check for other packets
                    else if (m_sentBytes < m_throttleOutbound && m_queue.HasQueuedItems())
                    {
                        item = m_queue.Dequeue();
                    }

                    if (item != null)
                    {
                        if (!item.Incoming)
                        {
                            m_sentBytes += item.Packet.ToBytes().Length;
                        }

                        if (item.throttleType == ThrottleOutPacketType.Resend)
                            m_resendQueued--;

                        return item;
                    }

                    Monitor.Wait(m_queueSync);
                }
            }
        }

        public void Close()
        {
            m_throttleUpdater.Stop();
        }

        public void Flush()
        {
            //not implemented
        }

        public void SetThrottleFromClient(byte[] data)
        {
            //not implemented
        }

        void ThrottleRefresh(object sender, ElapsedEventArgs e)
        {
            //Handle throttle adjusting
            m_throttleAdjustTimeLeft -= m_throttleRefreshRate;
            if (m_throttleAdjustTimeLeft <= 0)
            {
                AdjustThrottle();
                m_resentBytes = 0;
                m_throttleAdjustTimeLeft += m_throttleAdjustInterval;
            }

            //Lock to cause wait abort as we can send now more data
            lock (m_queueSync)
            {
                m_sentBytes = 0;
            }
        }

        void AdjustThrottle()
        {
            if (m_resentBytes == 0)
            {
                //Increase out traffic by 5% if we're trying to send data more than current max
                if (m_sentBytes >= m_throttleOutbound)
                {
                    m_throttleOutbound = (m_throttleOutbound * 105) / 100;
                }
            }
            else
            {
                //Determine proper traffic level
                int resentPerPeriod = (m_resentBytes * m_throttleRefreshRate) / m_throttleAdjustInterval;
                m_throttleOutbound -= resentPerPeriod;
            }

            //Limit throttle
            if (m_throttleOutbound > m_throttleOutboundMax)
            {
                m_throttleOutbound = m_throttleOutboundMax;
            }
            else if (m_throttleOutbound < m_throttleOutboundMin)
            {
                m_throttleOutbound = m_throttleOutboundMin;
            }
        }

        private bool IsHighestPriority(PacketType type)
        {
            return false;
        }
    }
}

using libsecondlife.Packets;
using System.Collections.Generic;
using OpenSim.Framework;


namespace OpenSim.Region.ClientStack
{
    public class PacketPrioritizer
    {
        private Dictionary<PacketType, int> Priorities;

        public PacketPrioritizer()
        {
            Priorities = new Dictionary<PacketType, int>();

            //High priority packets
            /*Priorities[PacketType.StartPingCheck] = 60000;
            Priorities[PacketType.PacketAck] = 60000;*/

            //Very low priority packets
            Priorities[PacketType.ObjectImage] = 1;
            Priorities[PacketType.ImageData] = 1;
            Priorities[PacketType.ImagePacket] = 1;
        }

        public void Bind(PrioritizedQueue<QueItem> queue)
        {
            queue.PriorityDeterminer = new PrioritizedQueue<QueItem>.DeterminePriorityDelegate(this.DeterminePriority);
        }

        public int DeterminePriority(QueItem item)
        {
            Packet packet = item.Packet;
            if (Priorities.ContainsKey(packet.Type))
                return Priorities[packet.Type];

            //Default priority is 20
            return 50;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim.Framework.Utilities
{
    public class Util
    {
        public static ulong UIntsToLong(uint X, uint Y)
        {
            return Helpers.UIntsToLong(X, Y);
        }

        public Util()
        {

        }
    }

    public class QueItem
    {
        public QueItem()
        {
        }

        public Packet Packet;
        public bool Incoming;
    }
}

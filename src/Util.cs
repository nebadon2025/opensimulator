/*
Copyright (c) OpenSim project, http://osgrid.org/

* Copyright (c) <year>, <copyright holder>
* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
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
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim
{
	/// <summary>
	/// </summary>
	/// 
		public class Util
		{
			public static ulong UIntsToLong(uint X, uint Y)
			{
				return Helpers.UIntsToLong(X,Y);
			}
			public Util()
			{
				
			}
		}
		
        public class QueItem {
                public QueItem()
                {
                }

                public Packet Packet;
                public bool Incoming;
        }


        public class BlockingQueue< T > {
                private Queue< T > _queue = new Queue< T >();
                private object _queueSync = new object();

                public void Enqueue(T value)
                {
                        lock(_queueSync)
                        {
                                _queue.Enqueue(value);
                                Monitor.Pulse(_queueSync);
                        }
                }

                public T Dequeue()
                {
                        lock(_queueSync)
                        {
                                if( _queue.Count < 1)
                                        Monitor.Wait(_queueSync);

                                return _queue.Dequeue();
                        }
                }
        }

	public class VoipConnectionPacket : Packet
    {
        /// <exclude/>
       // [XmlType("VoipConnection_Serverblock1")]
        public class ServerBlock
        {
            public uint Test1;
            public uint IP;
            public ushort Port;
            private byte[] userName;
            public byte[] UserName
            {
                get { return userName; }
                set
                {
                    if (value == null) { userName = null; return; }
                    if (value.Length > 255) { throw new OverflowException("Value exceeds 255 characters"); }
                    else { userName = new byte[value.Length]; Array.Copy(value, userName, value.Length); }
                }
            }

            //[XmlIgnore]
            public int Length
            {
                get
                {
                	int length = 4;
                	if (UserName != null) { length += 1 + UserName.Length; }
                    return length;
                }
            }

            public ServerBlock() { }
            public ServerBlock(byte[] bytes, ref int i)
            {
                try
                {
                    Test1 = (uint)(bytes[i++] + (bytes[i++] << 8) + (bytes[i++] << 16) + (bytes[i++] << 24));
                }
                catch (Exception)
                {
                    throw new MalformedDataException();
                }
            }

            public void ToBytes(byte[] bytes, ref int i)
            {
                bytes[i++] = (byte)(Test1 % 256);
                bytes[i++] = (byte)((Test1 >> 8) % 256);
                bytes[i++] = (byte)((Test1 >> 16) % 256);
                bytes[i++] = (byte)((Test1 >> 24) % 256);
                if(UserName == null) { Console.WriteLine("Warning: UserName is null, in " + this.GetType()); }
                bytes[i++] = (byte)UserName.Length;
                Array.Copy(UserName, 0, bytes, i, UserName.Length);
                i += UserName.Length;
            }

            public override string ToString()
            {
                string output = "-- ServerBlock1 --" + Environment.NewLine;
                output += "Test1: " + Test1.ToString() + "" + Environment.NewLine;
                output = output.Trim();
                return output;
            }
        }


        private Header header;
        public override Header Header { get { return header; } set { header = value; } }
        public override PacketType Type { get { return PacketType.TestMessage; } }
        public ServerBlock ServerBlock1;
        
        public VoipConnectionPacket()
        {
            Header = new LowHeader();
            Header.ID = 600;
            Header.Reliable = true;
            Header.Zerocoded = true;
            ServerBlock1 = new ServerBlock();
        }

        public VoipConnectionPacket(byte[] bytes, ref int i)
        {
            int packetEnd = bytes.Length - 1;
            Header = new LowHeader(bytes, ref i, ref packetEnd);
            ServerBlock1 = new ServerBlock(bytes, ref i);
        }

        public VoipConnectionPacket(Header head, byte[] bytes, ref int i)
        {
            Header = head;
            ServerBlock1 = new ServerBlock(bytes, ref i);
        }

        public override byte[] ToBytes()
        {
            int length = 8;
            length += ServerBlock1.Length;;
            if (header.AckList.Length > 0) { length += header.AckList.Length * 4 + 1; }
            byte[] bytes = new byte[length];
            int i = 0;
            header.ToBytes(bytes, ref i);
            ServerBlock1.ToBytes(bytes, ref i);
            if (header.AckList.Length > 0) { header.AcksToBytes(bytes, ref i); }
            return bytes;
        }

        public override string ToString()
        {
            string output = "--- VoipConnection ---" + Environment.NewLine;
            output += ServerBlock1.ToString() + Environment.NewLine;
            return output;
        }

    }
}

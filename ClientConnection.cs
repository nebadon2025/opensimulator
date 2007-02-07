/*
* Copyright (c) OpenSim project, http://sim.opensecondlife.org/
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
* 
*/

using System;
using System.Collections.Generic;
using System.Threading;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSimLite
{
	/// <summary>
	/// Hanldes a single clients connection. Runs in own thread.
	/// </summary>
	public class ClientConnection : CircuitConnection
	{
		public static GridManager Grid;
		public static SceneGraph Scene;
		
		private Thread _mthread;
		
		public ClientConnection()
		{
		}
		
		public override void Start()
		{
			_mthread = new Thread(new ThreadStart(RunClientRead));
			_mthread.Start();
		}
		
		private void RunClientRead()
		{
			try
			{
				for(;;)
				{
					Packet packet = null;
					packet=this.InQueue.Dequeue();
					switch(packet.Type)
					{
						case PacketType.UseCircuitCode:
							//should be a new user joining
							Console.WriteLine("new agent");
							break;
						case PacketType.CompleteAgentMovement:
							//Agent completing movement to region
							Grid.SendRegionData(this.NetInfo);
							break;
						case PacketType.RegionHandshakeReply:
							Console.WriteLine("RegionHandshake reply");
							Scene.SendTerrainData(this.NetInfo);
							break;
						default:
							break;
					}
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}
		}
	}
	
	public class CircuitConnection
	{
		public BlockingQueue<Packet> InQueue;
		public NetworkInfo NetInfo;
		
		public CircuitConnection()
		{
			InQueue = new BlockingQueue<Packet>();
		}
		
		public virtual void Start()
		{
			
		}
	}
	
	public class BlockingQueue< T >
	{
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
	
	public class NonBlockingQueue< T >
	{
		private Queue< T > _queue = new Queue< T >();
		private object _queueSync = new object();

		public void Enqueue(T value)
		{
			lock(_queueSync)
			{
				_queue.Enqueue(value);
			}
		}

		public T Dequeue()
		{
			T rValue = default(T);
			lock(_queueSync)
			{
				if( _queue.Count > 0)
				{
					rValue = _queue.Dequeue();
				}
			}
			return rValue;
		}
		
		public int Count
		{
			get
			{
				return(_queue.Count);
			}
		}
	}
	
}

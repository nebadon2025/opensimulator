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

namespace OpenSim
{
	/// <summary>
	/// Hanldes a single clients connection. Runs in own thread.
	/// </summary>
	public class ClientConnection : CircuitConnection
	{
		public static GridManager Grid;
		public static SceneGraph Scene;
		public static AgentManager AgentManager;
		public static PrimManager PrimManager;
		public static UserServer UserServer;
		public byte ConnectionType=1;
		private bool _authorised = false;
		
		private Thread _mthread;
		
		public ClientConnection()
		{
		}
		
		public override void Start()
		{
			_mthread = new Thread(new ThreadStart(RunClientRead));
			_mthread.IsBackground = true;
			_mthread.Start();
		}
		
		private void RunClientRead()
		{
			try
			{
				for(;;)
				{
					Packet packet = null;
					packet = this.InQueue.Dequeue();
					switch(packet.Type)
					{
						case PacketType.UseCircuitCode:
							Console.WriteLine("new circuit");
							
							//should check that this session/circuit is authorised
							UseCircuitCodePacket circuitPacket=(UseCircuitCodePacket)packet;
							AuthenticateResponse sessionInfo = UserServer.AuthenticateSession(circuitPacket.CircuitCode.SessionID, circuitPacket.CircuitCode.ID, circuitPacket.CircuitCode.Code);
							if(!sessionInfo.Authorised)
							{
								//session/circuit not authorised
								//so do something about it
							}
							else
							{
								//is authorised 
								string first = "",last ="";
								LLUUID baseFolder = null, inventoryFolder =null;
								first = sessionInfo.LogonInfo.First;
								last = sessionInfo.LogonInfo.Last;
								baseFolder = sessionInfo.LogonInfo.BaseFolder;
								inventoryFolder = sessionInfo.LogonInfo.InventoryFolder;
								AgentManager.NewAgent(this.NetInfo, first, last, baseFolder, inventoryFolder);
								this._authorised = true;
							}
							break;
						//should check this circuit is authorised before processing any other packets
						case PacketType.CompleteAgentMovement:
							//Agent completing movement to region
							// so send region handshake
							Grid.SendRegionData(this.NetInfo);
							// send movmentcomplete reply
							Scene.AgentCompletingMove(this.NetInfo);
							break;
						case PacketType.RegionHandshakeReply:
							Console.WriteLine("RegionHandshake reply");
							Scene.SendTerrainData(this.NetInfo);
							Scene.AddNewAvatar(AgentManager.GetAgent(this.NetInfo.User.AgentID).Avatar);
							// send current avatars and prims data
							break;
						default:
							break;
					}
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message + ", So die!");
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
}

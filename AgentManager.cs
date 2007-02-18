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
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim
{
	/// <summary>
	/// Manages Agent details.
	/// </summary>
	public class AgentManager
	{
		public Dictionary<libsecondlife.LLUUID,AgentProfile> AgentList;
		private uint _localAvatarNumber=0;
		private Server _server;
		
		public AgentManager(Server server)
		{
			_server=server;
			this.AgentList = new Dictionary<LLUUID, AgentProfile>();
		}
		
		public AgentName GetAgentName(LLUUID AgentID)
		{
			AgentName name;
			lock(AgentList)
			{
				if(AgentList.ContainsKey(AgentID))
				{
					name=AgentList[AgentID].Name;
				}
				else
				{
					name = new AgentName();
				}
			}
			return(name);
		}
		
		public AgentProfile GetAgent(LLUUID id)
		{
			lock(AgentList)
			{
				if(!this.AgentList.ContainsKey(id))
				{
					return null;
				}
				else
				{
					AgentProfile avatar = this.AgentList[id];
					return avatar;
				}
			}
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="agent"></param>
		public void AddAgent(AgentProfile agent)
		{
			this.AgentList.Add(agent.Avatar.FullID, agent);
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="userInfo"></param>
		/// <param name="first"></param>
		/// <param name="last"></param>
		/// <param name="baseFolder"></param>
		/// <param name="inventoryFolder"></param>
		/// <returns></returns>
		public bool NewAgent(NetworkInfo userInfo, string first, string last, LLUUID baseFolder, LLUUID inventoryFolder)
		{
			Console.WriteLine("new agent called");
			AgentProfile agent = new AgentProfile();
			agent.Avatar.FullID = userInfo.User.AgentID;
			agent.Avatar.NetInfo = userInfo;
			agent.Avatar.NetInfo.User.FirstName  =first;
			agent.Avatar.NetInfo.User.LastName = last;
			agent.Avatar.Position = new LLVector3(100, 100, 22);
			agent.Avatar.BaseFolder = baseFolder;
			agent.Avatar.InventoryFolder = inventoryFolder;
			agent.Avatar.LocalID = 8880000 + this._localAvatarNumber;
			this._localAvatarNumber++;
			this.AgentList.Add(agent.Avatar.FullID, agent);
			
			//Create new Wearable Assets and place in Inventory
			//this.assetManager.CreateNewInventorySet(ref agent, userInfo);
			
			return(true);
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="UserInfo"></param>
		public void RemoveAgent(NetworkInfo userInfo)
		{
			this.AgentList.Remove(userInfo.User.AgentID);
			
			//tell other clients to delete this avatar
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="userInfo"></param>
		public void AgentJoin(NetworkInfo userInfo)
		{
			//inform client of join comlete
			libsecondlife.Packets.AgentMovementCompletePacket mov = new AgentMovementCompletePacket();
			mov.AgentData.SessionID = userInfo.User.SessionID;
			mov.AgentData.AgentID = userInfo.User.AgentID;
			mov.Data.RegionHandle = Globals.Instance.RegionHandle;
			mov.Data.Timestamp = 1169838966;
			mov.Data.Position = new LLVector3(100f, 100f, 22f);
			mov.Data.LookAt = new LLVector3(0.99f, 0.042f, 0);
			_server.SendPacket(mov, true, userInfo);
		}
		
		public void RequestWearables(NetworkInfo userInfo)
		{
			AgentProfile Agent = this.AgentList[userInfo.User.AgentID];
			AgentWearablesUpdatePacket aw = new AgentWearablesUpdatePacket();
			aw.AgentData.AgentID = userInfo.User.AgentID;
			aw.AgentData.SerialNum = 0;
			aw.AgentData.SessionID = userInfo.User.SessionID;
			
			aw.WearableData = new AgentWearablesUpdatePacket.WearableDataBlock[13];
			AgentWearablesUpdatePacket.WearableDataBlock awb = null;
			awb = new AgentWearablesUpdatePacket.WearableDataBlock();
			awb.WearableType = (byte)0;
			awb.AssetID = Agent.Avatar.Wearables[0].AssetID;
			awb.ItemID = Agent.Avatar.Wearables[0].ItemID;
			aw.WearableData[0] = awb;
			
			awb = new AgentWearablesUpdatePacket.WearableDataBlock();
			awb.WearableType =(byte)1;
			awb.AssetID = Agent.Avatar.Wearables[1].AssetID;
			awb.ItemID = Agent.Avatar.Wearables[1].ItemID;
			aw.WearableData[1] = awb;
			
			for(int i=2; i<13; i++)
			{
				awb = new AgentWearablesUpdatePacket.WearableDataBlock();
				awb.WearableType = (byte)i;
				awb.AssetID = new LLUUID("00000000-0000-0000-0000-000000000000");
				awb.ItemID = new LLUUID("00000000-0000-0000-0000-000000000000");
				aw.WearableData[i] = awb;
			}
			
			_server.SendPacket(aw, true, userInfo);
		}
		
		public void SendPacketToAllExcept(Packet packet, LLUUID exceptAgent)
		{
			foreach (KeyValuePair<libsecondlife.LLUUID, AgentProfile> kp in this.AgentList)
			{
				if(kp.Value.Avatar.NetInfo.User.AgentID != exceptAgent)
				{
					_server.SendPacket(packet, true, kp.Value.Avatar.NetInfo);
				}
			}
		}
		
		public void SendPacketToALL(Packet packet)
		{
			foreach (KeyValuePair<libsecondlife.LLUUID, AgentProfile> kp in this.AgentList)
			{
				_server.SendPacket(packet, true, kp.Value.Avatar.NetInfo);
			}
		}
		
		public void SendTerseUpdateLists()
		{
			foreach (KeyValuePair<libsecondlife.LLUUID, AgentProfile> kp in this.AgentList)
			{
				if(kp.Value.Avatar.TerseUpdateList.Count > 0)
				{
					ImprovedTerseObjectUpdatePacket im = new ImprovedTerseObjectUpdatePacket();
					im.RegionData.RegionHandle = Globals.Instance.RegionHandle;;
					im.RegionData.TimeDilation = 64096;
					
					im.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[kp.Value.Avatar.TerseUpdateList.Count];
					
					for(int i = 0; i < kp.Value.Avatar.TerseUpdateList.Count; i++)
					{
						im.ObjectData[i] = kp.Value.Avatar.TerseUpdateList[i];
					}
					_server.SendPacket(im, true, kp.Value.Avatar.NetInfo);
					kp.Value.Avatar.TerseUpdateList.Clear();
				}
			}
		}
	}
	
	public struct AgentName
	{
		public string First;
		public string Last;
	}
	
	public class AgentProfile
	{
		public AgentName Name;
		public AvatarData Avatar;
		//public AgentInventory Inventory;
		
		public AgentProfile()
		{
			Name = new AgentName();
			Avatar = new AvatarData();
		}
	}
	
	public class AvatarData : Node
	{
		public NetworkInfo NetInfo;
		public LLUUID FullID;
		public uint LocalID;
		public LLQuaternion BodyRotation;
		public bool Walk = false;
		public bool Started = false;
		//public TextureEntry TextureEntry;
		public AvatarWearable[] Wearables; 
		public LLUUID InventoryFolder;
    	public LLUUID BaseFolder;
    	public AvatarParams VisParams;
		
    	public List<libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock> ObjectUpdateList;
    	public List<libsecondlife.Packets.ImprovedTerseObjectUpdatePacket.ObjectDataBlock> TerseUpdateList;
    	
		public AvatarData()
		{
			Wearables=new AvatarWearable[2]; //should be 13
			for(int i = 0; i < 2; i++)
			{
				Wearables[i] = new AvatarWearable();
			}
			this.SceneType = 1;  //Avatar type
			
			this.ObjectUpdateList = new List<libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock>();
			this.TerseUpdateList = new List<libsecondlife.Packets.ImprovedTerseObjectUpdatePacket.ObjectDataBlock>();
			VisParams = new AvatarParams();
		}
	}
	
	public class AvatarWearable
	{
		public LLUUID AssetID = new LLUUID("00000000-0000-0000-0000-000000000000");
		public LLUUID ItemID = new LLUUID("00000000-0000-0000-0000-000000000000");
		
		public AvatarWearable()
		{
			
		}
	}
	
	
	public class AvatarParams
	{
		public byte[] Params;
		
		public AvatarParams()
		{
			Params = new byte [ 218];
			for(int i = 0; i < 218; i++)
			{
				Params[i] = 100;
			}
		}
		
	}
	
}

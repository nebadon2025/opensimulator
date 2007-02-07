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
using System.IO;
using System.Xml;
using libsecondlife;
using libsecondlife.Packets;
using System.Collections.Generic;

namespace OpenSimLite
{

	/// <summary>
	/// Description of GridManager.
	/// </summary>
	public class GridManager
	{
		
		private Server _server;
		private System.Text.Encoding _enc = System.Text.Encoding.ASCII;
		private AgentManager _agentManager;
		private libsecondlife.Packets.RegionHandshakePacket _regionPacket;
		
		public Dictionary<ulong, RegionInfo> Grid;
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="serve"></param>
		/// <param name="agentManager"></param>
		public GridManager(Server server, AgentManager agentManager)
		{
			Grid = new Dictionary<ulong, RegionInfo>();
			_server = server;
			_agentManager = agentManager;
			LoadGrid();
			InitialiseRegionHandshake();
		}
		
				
		/// <summary>
		/// 
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public LLUUID RequestUUID(byte type)
		{
			//get next UUID in block for requested type;
			return(LLUUID.Zero);
		}
		
		public void InitialiseRegionHandshake()
		{
			_regionPacket = new RegionHandshakePacket();
			_regionPacket.RegionInfo.BillableFactor = 0;
			_regionPacket.RegionInfo.IsEstateManager = false;
			_regionPacket.RegionInfo.TerrainHeightRange00 = 60;
			_regionPacket.RegionInfo.TerrainHeightRange01 = 60;
			_regionPacket.RegionInfo.TerrainHeightRange10 = 60;
			_regionPacket.RegionInfo.TerrainHeightRange11 = 60;
			_regionPacket.RegionInfo.TerrainStartHeight00 = 20;
			_regionPacket.RegionInfo.TerrainStartHeight01 = 20;
			_regionPacket.RegionInfo.TerrainStartHeight10 = 20;
			_regionPacket.RegionInfo.TerrainStartHeight11 = 20;
			_regionPacket.RegionInfo.SimAccess = 13;
			_regionPacket.RegionInfo.WaterHeight = 5;
			_regionPacket.RegionInfo.RegionFlags = 72458694;
			_regionPacket.RegionInfo.SimName = _enc.GetBytes( Globals.Instance.RegionName);
			_regionPacket.RegionInfo.SimOwner = new LLUUID("00000000-0000-0000-0000-000000000000");
			_regionPacket.RegionInfo.TerrainBase0 = new LLUUID("b8d3965a-ad78-bf43-699b-bff8eca6c975");
			_regionPacket.RegionInfo.TerrainBase1 = new LLUUID("abb783e6-3e93-26c0-248a-247666855da3");
			_regionPacket.RegionInfo.TerrainBase2 = new LLUUID("179cdabd-398a-9b6b-1391-4dc333ba321f");
			_regionPacket.RegionInfo.TerrainBase3 = new LLUUID("beb169c7-11ea-fff2-efe5-0f24dc881df2");
			_regionPacket.RegionInfo.TerrainDetail0 = new LLUUID("00000000-0000-0000-0000-000000000000");
			_regionPacket.RegionInfo.TerrainDetail1 = new LLUUID("00000000-0000-0000-0000-000000000000");
			_regionPacket.RegionInfo.TerrainDetail2 = new LLUUID("00000000-0000-0000-0000-000000000000");
			_regionPacket.RegionInfo.TerrainDetail3 = new LLUUID("00000000-0000-0000-0000-000000000000");
			_regionPacket.RegionInfo.CacheID = LLUUID.Zero; // new LLUUID("545ec0a5-5751-1026-8a0b-216e38a7ab37");
			
		}
		
		public void SendRegionData(NetworkInfo userInfo)
		{
			_server.SendPacket(_regionPacket,true, userInfo);
			
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
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="UserInfo"></param>
		public void RequestMapLayer(NetworkInfo userInfo)
		{
			//send a layer covering the 800,800 - 1200,1200 area
			MapLayerReplyPacket MapReply = new MapLayerReplyPacket();
			MapReply.AgentData.AgentID = userInfo.User.AgentID;
			MapReply.AgentData.Flags = 0;
			MapReply.LayerData = new MapLayerReplyPacket.LayerDataBlock[1];
			MapReply.LayerData[0] = new MapLayerReplyPacket.LayerDataBlock();
			MapReply.LayerData[0].Bottom = 800;
			MapReply.LayerData[0].Left = 800;
			MapReply.LayerData[0].Top = 1200;
			MapReply.LayerData[0].Right = 1200;
			MapReply.LayerData[0].ImageID = new LLUUID("00000000-0000-0000-7007-000000000006");
			_server.SendPacket(MapReply, true, userInfo);
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="UserInfo"></param>
		/// <param name="MinX"></param>
		/// <param name="MinY"></param>
		/// <param name="MaxX"></param>
		/// <param name="MaxY"></param>
		public void RequestMapBlock(NetworkInfo userInfo, int minX, int minY,int maxX,int maxY)
		{
			foreach (KeyValuePair<ulong, RegionInfo> regionPair in this.Grid)
			{
				//check Region is inside the requested area
				RegionInfo Region = regionPair.Value;
				if(((Region.X > minX) && (Region.X < maxX)) && ((Region.Y > minY) && (Region.Y < maxY)))
				{
					MapBlockReplyPacket MapReply = new MapBlockReplyPacket();
					MapReply.AgentData.AgentID = userInfo.User.AgentID;
					MapReply.AgentData.Flags = 0;
					MapReply.Data = new MapBlockReplyPacket.DataBlock[1];
					MapReply.Data[0] = new MapBlockReplyPacket.DataBlock();
					MapReply.Data[0].MapImageID = Region.ImageID;
					MapReply.Data[0].X = Region.X;
					MapReply.Data[0].Y = Region.Y;
					MapReply.Data[0].WaterHeight = Region.WaterHeight;
					MapReply.Data[0].Name = _enc.GetBytes( Region.Name);
					MapReply.Data[0].RegionFlags = 72458694;
					MapReply.Data[0].Access = 13;
					MapReply.Data[0].Agents = 1;
					_server.SendPacket(MapReply, true, userInfo);
				}
			}
			
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="UserInfo"></param>
		/// <param name="Request"></param>
		public void RequestTeleport(NetworkInfo userInfo, TeleportLocationRequestPacket request)
		{
			if(Grid.ContainsKey(request.Info.RegionHandle))
			{
				RegionInfo Region = Grid[request.Info.RegionHandle];
				libsecondlife.Packets.TeleportStartPacket TeleportStart = new TeleportStartPacket();
				TeleportStart.Info.TeleportFlags = 16;
				_server.SendPacket(TeleportStart, true, userInfo);
				
				libsecondlife.Packets.TeleportFinishPacket Teleport = new TeleportFinishPacket();
				Teleport.Info.AgentID = userInfo.User.AgentID;
				Teleport.Info.RegionHandle = request.Info.RegionHandle;
				Teleport.Info.SimAccess = 13;
				Teleport.Info.SeedCapability = new byte[0];
				
				System.Net.IPAddress oIP = System.Net.IPAddress.Parse(Region.IPAddress.Address);
				byte[] byteIP = oIP.GetAddressBytes();
				uint ip=(uint)byteIP[3]<<24;
				ip+=(uint)byteIP[2]<<16;
				ip+=(uint)byteIP[1]<<8;
				ip+=(uint)byteIP[0];
				
				Teleport.Info.SimIP = ip;
				Teleport.Info.SimPort = Region.IPAddress.Port;
				Teleport.Info.LocationID = 4;
				Teleport.Info.TeleportFlags = 1 << 4;;
				_server.SendPacket(Teleport, true, userInfo);
				
				//this._agentManager.RemoveAgent(userInfo);
			}
			
		}
		
		/// <summary>
		/// 
		/// </summary>
		private void LoadGrid()
		{
			//should connect to a space server to see what grids there are
			//but for now we read static xml files
			ulong CurrentHandle = 0;
			bool Login = true;
			
			XmlDocument doc = new XmlDocument();
			
			try {
				doc.Load(Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Grid.ini" ));
			}
			catch ( Exception e)
			{
				Console.WriteLine(e.Message);
				return;
			}
			
			try
			{
				XmlNode root = doc.FirstChild;
				if (root.Name != "Root")
					throw new Exception("Error: Invalid File. Missing <Root>");
				
				XmlNode nodes = root.FirstChild;
				if (nodes.Name != "Grid")
					throw new Exception("Error: Invalid File. <project> first child should be <Grid>");
				
				if (nodes.HasChildNodes)   {
					foreach( XmlNode xmlnc in nodes.ChildNodes)
					{
						if(xmlnc.Name == "Region")
						{
							string xmlAttri;
							RegionInfo Region = new RegionInfo();
							if(xmlnc.Attributes["Name"] != null)
							{
								xmlAttri = ((XmlAttribute)xmlnc.Attributes.GetNamedItem("Name")).Value;
								Region.Name = xmlAttri+" \0";
							}
							if(xmlnc.Attributes["ImageID"] != null)
							{
								xmlAttri = ((XmlAttribute)xmlnc.Attributes.GetNamedItem("ImageID")).Value;
								Region.ImageID = new LLUUID(xmlAttri);
							}
							if(xmlnc.Attributes["IP_Address"] != null)
							{
								xmlAttri = ((XmlAttribute)xmlnc.Attributes.GetNamedItem("IP_Address")).Value;
								Region.IPAddress.Address = xmlAttri;
							}
							if(xmlnc.Attributes["IP_Port"] != null)
							{
								xmlAttri = ((XmlAttribute)xmlnc.Attributes.GetNamedItem("IP_Port")).Value;
								Region.IPAddress.Port = Convert.ToUInt16(xmlAttri);
							}
							if(xmlnc.Attributes["Location_X"] != null)
							{
								xmlAttri = ((XmlAttribute)xmlnc.Attributes.GetNamedItem("Location_X")).Value;
								Region.X = Convert.ToUInt16(xmlAttri);
							}
							if(xmlnc.Attributes["Location_Y"] != null)
							{
								xmlAttri = ((XmlAttribute)xmlnc.Attributes.GetNamedItem("Location_Y")).Value;
								Region.Y = Convert.ToUInt16(xmlAttri);
							}
							
							this.Grid.Add(Region.Handle, Region);
							
						}
						if(xmlnc.Name == "CurrentRegion")
						{
							
							string xmlAttri;
							uint Rx = 0, Ry = 0;
							if(xmlnc.Attributes["RegionHandle"] != null)
							{
								xmlAttri = ((XmlAttribute)xmlnc.Attributes.GetNamedItem("RegionHandle")).Value;
								CurrentHandle = Convert.ToUInt64(xmlAttri);
								
							}
							else
							{
								if(xmlnc.Attributes["Region_X"] != null)
								{
									xmlAttri = ((XmlAttribute)xmlnc.Attributes.GetNamedItem("Region_X")).Value;
									Rx = Convert.ToUInt32(xmlAttri);
								}
								if(xmlnc.Attributes["Region_Y"] != null)
								{
									xmlAttri = ((XmlAttribute)xmlnc.Attributes.GetNamedItem("Region_Y")).Value;
									Ry = Convert.ToUInt32(xmlAttri);
								}
							}
							if(xmlnc.Attributes["LoginServer"] != null)
							{
								xmlAttri = ((XmlAttribute)xmlnc.Attributes.GetNamedItem("LoginServer")).Value;
								Login = Convert.ToBoolean(xmlAttri);
								
							}
							if(CurrentHandle == 0)
							{
								//no RegionHandle set
								//so check for Region X and Y
								if((Rx > 0) && (Ry > 0))
								{
									CurrentHandle = Helpers.UIntsToLong((Rx*256), (Ry*256));
								}
								else
								{
									//seems to be no Region location set
									// so set default
									CurrentHandle = 1096213093147648;
								}
							}
						}
					}
					
					//finished loading grid, now set Globals to current region
					if(CurrentHandle != 0)
					{
						if(Grid.ContainsKey(CurrentHandle))
						{
							RegionInfo Region = Grid[CurrentHandle];
							Globals.Instance.RegionHandle = Region.Handle;
							Globals.Instance.RegionName = Region.Name;
							Globals.Instance.SimPort = Region.IPAddress.Port;
							Globals.Instance.StartLoginServer = Login;
						}
					}
					
				}
			}
			catch ( Exception e)
			{
				Console.WriteLine(e.Message);
				return;
			}
		}
	}
	
	public class RegionInfo
	{
		public RegionIP IPAddress;
		public string Name;
		private ushort _x;
		private ushort _y;
		private ulong _handle;
		public LLUUID ImageID;
		public uint Flags;
		public byte WaterHeight;
		
		public ushort X
		{
			get
			{
				return(_x);
			}
			set
			{
				_x = value;
				Handle = Helpers.UIntsToLong((((uint)_x)*256), (((uint)_y)*256));
			}
		}
		public ushort Y
		{
			get
			{
				return(_y);
			}
			set
			{
				_y = value;
				Handle = Helpers.UIntsToLong((((uint)_x)*256), (((uint)_y)*256));
			}
		}
		public ulong Handle
		{
			get
			{
				if(_handle > 0)
				{
					return(_handle);
				}
				else
				{
					//should never be called but just incase
					return(Helpers.UIntsToLong((((uint)_x)*256), (((uint)_y)*256)));
				}
			}
			set
			{
				_handle = value;
			}
			
		}
		
		public RegionInfo()
		{
			this.IPAddress = new RegionIP();
		}
	}
	
	public class RegionIP
	{
		public string Address;
		public ushort Port;
		
		public RegionIP()
		{
			
		}
		
	}
	
}

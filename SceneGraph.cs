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
using System.Collections.Generic;
using System.Threading;
using libsecondlife;
using libsecondlife.Packets;
using Axiom.MathLib;

namespace OpenSim
{
	/// <summary>
	/// Manages prim and avatar sim positions and movements and updating clients
	/// </summary>
	public class SceneGraph
	{
		public Node RootNode;
		public Terrain Terrain;
		
		private Thread _mthread;
		private PhysicsManager _physics;
		private Server _server;
		private System.Text.Encoding _enc = System.Text.Encoding.ASCII;
		private libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock _avatarTemplate;
		private int _objectCount=0;
		private UpdateSender _updateSender;
		private AgentManager _agentManager;
		
		public NonBlockingQueue<UpdateCommand> Commands;
		
		
		#region Thread Sync
		//public object CommandsSync = new object();
		private object _sendTerrainSync = new object();
		#endregion
		
		public SceneGraph(Server server, AgentManager agentManager)
		{	
			_server = server;
			RootNode = new Node();
			_physics = new PhysicsManager(this);
			Commands = new NonBlockingQueue<UpdateCommand>();
			Terrain = new Terrain();
			_updateSender = new UpdateSender(_server, agentManager);
			_agentManager = agentManager;
			//testing
			this.SetupTemplate("objectupate168.dat");
			_updateSender.Startup();
		}
		
		public void Startup()
		{
			_mthread = new Thread(new System.Threading.ThreadStart(RunScene));
			_mthread.IsBackground = true;
			_mthread.Start();
		}
		
		public void RunScene()
		{
			try
			{
				for(;;)
				{
					this.Update();
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}
		}
		
		public void Update()
		{
			// run physics engine to update positions etc since last frame
			this._physics.UpdatePhysics();
			
			// process command list
			lock(this.Commands)  //do we want to stop new commands being added while we process?
			{
				while(this.Commands.Count > 0)
				{
					UpdateCommand command = this.Commands.Dequeue();
					switch(command.CommandType)
					{
						case 1:
							//movement command
							if(command.SObject.SceneType == 1)
							{
								AvatarData avatar =(AvatarData) command.SObject;
								avatar.Walk = !avatar.Walk;
								command.SObject.InternVelocityX = command.InternVelocityX;
								command.SObject.InternVelocityY = command.InternVelocityY;
								command.SObject.InternVelocityZ = command.InternVelocityZ;
								
								command.SObject.Velocity = command.Velocity;
								if((command.SObject.UpdateFlag & (1)) != 1)
								{
									command.SObject.UpdateFlag += 1;
								}
							}
							break;
						default:
							break;
					}
				}
			}
			
			// check to see if any new avatars have joined since last frame
			//if so send data to clients.
			lock(this.RootNode)
			{
				for (int i = 0; i < this.RootNode.ChildrenCount; i++)
				{
					//for now we will limit avatars to being a child of the rootnode
					if(this.RootNode.GetChild(i).SceneType == 1) //check it is a avatar node
					{
						AvatarData avatar =(AvatarData) this.RootNode.GetChild(i);
						int updatemask = avatar.UpdateFlag & (128);
						if(updatemask == 128) //is a new avatar?
						{
							Console.WriteLine("new avatar has been added to scene so update it on the current scene");
						
							this.SendAvatarDataToAll(avatar);
							
							//should send a avatar appearance to other clients except the avatar's owner
							this.SendAvatarAppearanceToAllExcept(avatar);
							
							//and send complete scene update to the new avatar's owner
							this.SendCompleteSceneTo(avatar);
							avatar.Started = true;
						}
					}
				}
			}
			
			//send updates to clients
			//might be better to do these updates reverse to how is done here
			// ie.. loop through the avatars and find objects /other avatars in their range/vision
			//instead of looping through all objects/avatars and then finding avatars in range
			//but that would mean looping through all objects/avatar for each avatar,
			//rather than looping through just the avatars for each object/avatar
			lock(this.RootNode)
			{
				for (int i = 0; i < this.RootNode.ChildrenCount; i++)
				{
					if(this.RootNode.GetChild(i).SceneType == 1) //check it is a avatar node
					{
						AvatarData avatar =(AvatarData) this.RootNode.GetChild(i);
						int updatemask = avatar.UpdateFlag & (1);
						if(updatemask == 1)
						{
							//avatar has changed velocity	
							//check what avatars are in range and add to their update lists
							//but for now we will say all avatars are in range
							for (int n = 0; n < this.RootNode.ChildrenCount; n++)
							{
								if(this.RootNode.GetChild(n).SceneType == 1) //check it is a avatar node
								{
									AvatarData avatar2 =(AvatarData) this.RootNode.GetChild(i);
									int newmask = avatar2.UpdateFlag & (128);
									if(newmask != 128) 
									//is a new avatar?
									//if it is then we don't need to tell it about updates as it has already received a full update of the scene
									{
										//but if it is not then we add to its updatelist
										avatar2.TerseUpdateList.Add(this.CreateTerseBlock(avatar));
									}
								}
							}
						}
					}
				}
			}
			
			//now reset all update flags
			lock(this.RootNode)
			{
				for (int i = 0; i < this.RootNode.ChildrenCount; i++)
				{
					this.RootNode.GetChild(i).UpdateFlag = 0;
				}
			}
			this._agentManager.SendTerseUpdateLists();
		}
		
		public void AvatarMovementCommand(NetworkInfo userInfo, AgentUpdatePacket updatePacket)
		{
			uint mask = updatePacket.AgentData.ControlFlags & (1);
			AvatarData avatar = _agentManager.GetAgent(userInfo.User.AgentID).Avatar;
			if (avatar != null)
			{
				if (avatar.Started)
				{
					if (mask == (1))
					{
						if (!avatar.Walk)
						{
							UpdateCommand newCommand = new UpdateCommand();
							newCommand.CommandType = 1;
							newCommand.SObject = avatar;
							//start walking
							Axiom.MathLib.Vector3 v3 = new Axiom.MathLib.Vector3(1, 0, 0);
							Axiom.MathLib.Quaternion q = new Axiom.MathLib.Quaternion(updatePacket.AgentData.BodyRotation.W, updatePacket.AgentData.BodyRotation.X, updatePacket.AgentData.BodyRotation.Y, updatePacket.AgentData.BodyRotation.Z);
							Axiom.MathLib.Vector3 direc = q * v3;
							direc.Normalize();
							
							Axiom.MathLib.Vector3 internDirec = new Vector3(direc.x, direc.y, direc.z);
							
							//work out velocity for sim physics system
							direc = direc * ((0.03f) * 128f);
							//because of us using a frame based system we can't update the avatar directly
							//so we need to add the command to the list and let the update loop deal with it
							newCommand.Velocity = new libsecondlife.LLVector3(0, 0 , 0);
							newCommand.Velocity.X = direc.x;
							newCommand.Velocity.Y = direc.y;
							newCommand.Velocity.Z = direc.z;
							//avatar.Walk = true;
							
							//work out velocity for internal clients movement commands
							internDirec = internDirec * (0.03f);
							internDirec.x += 1;
							internDirec.y += 1;
							internDirec.z += 1;
							
							newCommand.InternVelocityX = (ushort)(32768 * internDirec.x);
							newCommand.InternVelocityY = (ushort)(32768 * internDirec.y);
							newCommand.InternVelocityZ = (ushort)(32768 * internDirec.z);
							lock(this.Commands)
							{
								this.Commands.Enqueue(newCommand);
							}
						}
					}
					else
					{
						if (avatar.Walk)
						{
							UpdateCommand newCommand = new UpdateCommand();
							newCommand.CommandType = 1;
							newCommand.SObject = avatar;
							//walking but key not pressed so need to stop
							//avatar.Walk = false;
							newCommand.Velocity.X = 0;
							newCommand.Velocity.Y = 0;
							newCommand.Velocity.Z = 0;
							
							newCommand.InternVelocityX = (ushort)(32768 );
							newCommand.InternVelocityY = (ushort)(32768 );
							newCommand.InternVelocityZ = (ushort)(32768 );
							lock(this.Commands)
							{
								this.Commands.Enqueue(newCommand);
							}
						}
					}
				}
			}
		}
		
		#region send terrain data
		/// <summary>
		/// 
		/// </summary>
		/// <param name="userInfo"></param>
		public void SendTerrainData(NetworkInfo userInfo)
		{
			lock(this._sendTerrainSync)
			{
				string data_path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory ,"layer_data" );
				
				//send layerdata
				LayerDataPacket layerpack = new LayerDataPacket();
				layerpack.LayerID.Type = 76;
				this.SendLayerData(userInfo, layerpack, Path.Combine(data_path,"layerdata0.dat"));
				Console.WriteLine("Sent terrain data");
				
				//test
				//this.SendAvatarData(userInfo);
			}
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="userInfo"></param>
		/// <param name="layer"></param>
		/// <param name="name"></param>
		private void SendLayerData(NetworkInfo userInfo, LayerDataPacket layer, string fileName)
		{
			FileInfo fInfo = new FileInfo(fileName);
			long numBytes = fInfo.Length;
			FileStream fStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
			BinaryReader br = new BinaryReader(fStream);
			byte [] data1 = br.ReadBytes((int)numBytes);
			br.Close();
			fStream.Close();
			layer.LayerData.Data = data1;
			_server.SendPacket(layer, true, userInfo);
		
		}
		#endregion
		
		public void AgentCompletingMove(NetworkInfo userInfo)
		{
			libsecondlife.Packets.AgentMovementCompletePacket mov = new AgentMovementCompletePacket();
			mov.AgentData.SessionID = userInfo.User.SessionID;
			mov.AgentData.AgentID = userInfo.User.AgentID;
			mov.Data.RegionHandle = Globals.Instance.RegionHandle;
			mov.Data.Timestamp = 1169838966;
			mov.Data.Position = new LLVector3(100f, 100f, 22f);
			mov.Data.LookAt = new LLVector3(0.99f, 0.042f, 0);
			_server.SendPacket(mov, true, userInfo);
		}
		
		public void AddNewAvatar(AvatarData avatar)
		{
			Console.WriteLine("adding avatar to scene");
			lock(this.RootNode)
			{
				avatar.SceneName = "Avatar" + this._objectCount.ToString("00000");
				avatar.Position = new LLVector3(100f, 100f, 22f);
				this._objectCount++;
				this.RootNode.AddChild(avatar);
			}
			avatar.UpdateFlag = 128;
			
			//add to new clients list?
			
		}
		
		#region testing
		//test only
		private void SendAvatarData(NetworkInfo userInfo)
		{
			ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
			objupdate.RegionData.RegionHandle = Globals.Instance.RegionHandle;
			objupdate.RegionData.TimeDilation = 64096;
			objupdate.ObjectData = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
			
			objupdate.ObjectData[0] = _avatarTemplate;
			//give this avatar object a local id and assign the user a name
			objupdate.ObjectData[0].ID = 8880000;// + this._localNumber;
			userInfo.User.AvatarLocalID = objupdate.ObjectData[0].ID;
			//User_info.name="Test"+this.local_numer+" User";
			//this.GetAgent(userInfo.UserAgentID).Started = true;
			objupdate.ObjectData[0].FullID = userInfo.User.AgentID;
			objupdate.ObjectData[0].NameValue = _enc.GetBytes("FirstName STRING RW SV " + userInfo.User.FirstName + "\nLastName STRING RW SV " + userInfo.User.LastName + " \0");
			//userInfo.User.FullName = "FirstName STRING RW SV " + userInfo.first_name + "\nLastName STRING RW SV " + userInfo.last_name + " \0";
			
			libsecondlife.LLVector3 pos2 = new LLVector3(100f, 100.0f, 22.0f);
			byte[] pb = pos2.GetBytes();
			Array.Copy(pb, 0, objupdate.ObjectData[0].ObjectData, 16, pb.Length);
			
			//this._localNumber++;
			_server.SendPacket(objupdate, true, userInfo);
		}
		
		//test only
		private void SetupTemplate(string name)
		{
			//should be replaced with completely code generated packets
			int i = 0;
			FileInfo fInfo = new FileInfo(name);
			long numBytes = fInfo.Length;
			FileStream fStream = new FileStream(name, FileMode.Open, FileAccess.Read);
			BinaryReader br = new BinaryReader(fStream);
			byte [] data1 = br.ReadBytes((int)numBytes);
			br.Close();
			fStream.Close();
			
			libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock objdata = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock(data1, ref i);
			
			System.Text.Encoding enc = System.Text.Encoding.ASCII;
			libsecondlife.LLVector3 pos = new LLVector3(objdata.ObjectData, 16);
			pos.X = 100f;
			objdata.ID = 8880000;
			objdata.NameValue = enc.GetBytes("FirstName STRING RW SV Test \nLastName STRING RW SV User \0");
			libsecondlife.LLVector3 pos2 = new LLVector3(13.981f,100.0f,20.0f);
			//objdata.FullID=user.AgentID;
			byte[] pb = pos.GetBytes();			
			Array.Copy(pb, 0, objdata.ObjectData, 16, pb.Length);
			
			_avatarTemplate = objdata;
				
		}
		#endregion
		private void SendAvatarDataToAll(AvatarData avatar)
		{
			Console.WriteLine("sending avatar data");
			ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
			objupdate.RegionData.RegionHandle = Globals.Instance.RegionHandle;
			objupdate.RegionData.TimeDilation = 0;
			objupdate.ObjectData = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
			
			objupdate.ObjectData[0] = _avatarTemplate;
			objupdate.ObjectData[0].ID = avatar.LocalID;
			objupdate.ObjectData[0].FullID = avatar.NetInfo.User.AgentID;
			objupdate.ObjectData[0].NameValue = _enc.GetBytes("FirstName STRING RW SV " + avatar.NetInfo.User.FirstName + "\nLastName STRING RW SV " + avatar.NetInfo.User.LastName + " \0");
			
			libsecondlife.LLVector3 pos2 = avatar.Position;
			byte[] pb = pos2.GetBytes();
			Array.Copy(pb, 0, objupdate.ObjectData[0].ObjectData, 16, pb.Length);
			
			SendInfo send = new SendInfo();
			send.Incr = true;
			send.NetInfo = avatar.NetInfo;
			send.Packet = objupdate;
			send.SentTo = 1; //to all clients
			this._updateSender.SendList.Enqueue(send);
		}
		
		public void SendCompleteSceneTo(AvatarData avatar)
		{
			
		}
		
		public ImprovedTerseObjectUpdatePacket.ObjectDataBlock CreateTerseBlock(AvatarData avatar)
		{
			byte[] bytes = new byte[60];
			int i=0;
			ImprovedTerseObjectUpdatePacket.ObjectDataBlock dat = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock();
			
			dat.TextureEntry = _avatarTemplate.TextureEntry;
			libsecondlife.LLVector3 pos2 = new LLVector3(avatar.Position.X, avatar.Position.Y, avatar.Position.Z);
			
			uint ID = avatar.LocalID;
			bytes[i++] = (byte)(ID % 256);
			bytes[i++] = (byte)((ID >> 8) % 256);
			bytes[i++] = (byte)((ID >> 16) % 256);
			bytes[i++] = (byte)((ID >> 24) % 256);
			
			bytes[i++] = 0;
			bytes[i++] = 1;

			i += 14;
			bytes[i++] = 128;
			bytes[i++] = 63;
			byte[] pb = pos2.GetBytes();
			
			Array.Copy(pb, 0, bytes, i, pb.Length);
			i += 12;
			ushort ac = 32767;
			bytes[i++] = (byte)(avatar.InternVelocityX % 256);
			bytes[i++] = (byte)((avatar.InternVelocityX >> 8) % 256);
			
			bytes[i++] = (byte)(avatar.InternVelocityY % 256);
			bytes[i++] = (byte)((avatar.InternVelocityY>> 8) % 256);
			
			bytes[i++] = (byte)(avatar.InternVelocityZ % 256);
			bytes[i++] = (byte)((avatar.InternVelocityZ >> 8) % 256);
			
			//accel
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			
			//rot
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			
			//rotation vel
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			
			dat.Data=bytes;
			return(dat);
		}
		
		public void SendAvatarAppearanceToAllExcept(AvatarData avatar)
		{
			AvatarAppearancePacket avp = new AvatarAppearancePacket();
		
			
			avp.VisualParam = new AvatarAppearancePacket.VisualParamBlock[218];
			//avp.ObjectData.TextureEntry=this.avatar_template.TextureEntry;// br.ReadBytes((int)numBytes);
			
			FileInfo fInfo = new FileInfo("Avatar_texture3.dat");
			long numBytes = fInfo.Length;
			FileStream fStream = new FileStream("Avatar_texture3.dat", FileMode.Open, FileAccess.Read);
			BinaryReader br = new BinaryReader(fStream);
			avp.ObjectData.TextureEntry = br.ReadBytes((int)numBytes);
			br.Close();
			fStream.Close();
			
			AvatarAppearancePacket.VisualParamBlock avblock = null;
			for(int i = 0; i < 218; i++)
			{
				avblock = new AvatarAppearancePacket.VisualParamBlock();
				avblock.ParamValue = (byte)avatar.VisParams.Params[i];
				avp.VisualParam[i] = avblock;
			}
			
			avp.Sender.IsTrial = false;
			avp.Sender.ID = avatar.NetInfo.User.AgentID;
			
			SendInfo send = new SendInfo();
			send.Incr = true;
			send.NetInfo = avatar.NetInfo;
			send.Packet = avp;
			send.SentTo = 2; //to all clients except avatar
			this._updateSender.SendList.Enqueue(send);
		}
	}
	
	//any need for separate nodes and sceneobjects? 
	//do we need multiple objects in the same node?
	public class Node
	{
		private List<Node> _children;
		//private List<SceneObject> _attached;
		public byte SceneType;
		public string SceneName;
		public LLVector3 Position;
		public LLVector3 Velocity = new LLVector3(0,0,0);
		public byte UpdateFlag;
		public ushort InternVelocityX = 32768;
		public ushort InternVelocityY = 32768;
		public ushort InternVelocityZ = 32768;
		
		public List<Node> ChildNodes
		{
			get
			{
				return(_children);
			}
		}
		
		public int ChildrenCount
		{
			get
			{
				return(_children.Count);
			}
		}
		/*
		public List<SceneObject> AttachedObjexts
		{
			get
			{
				return(_attached);
			}
		}
		*/
		public Node()
		{
			_children = new List<Node>();
			//_attached = new List<SceneObject>();
		}
		/*
		/// <summary>
		/// 
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public SceneObject GetAttachedObject(int index)
		{
			if(_attached.Count > index)
			{
				return(_attached[index]);
			}
			else
			{
				return(null);
			}
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="sceneObject"></param>
		public void AttachObject(SceneObject sceneObject)
		{
			_attached.Add(sceneObject);
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="sceneObject"></param>
		public void RemoveObject(SceneObject sceneObject)
		{
			_attached.Remove(sceneObject);
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="sceneObject"></param>
		/// <returns></returns>
		public int HasAttachedObject(SceneObject sceneObject)
		{
			int rValue = -1;
			for(int i = 0; i < this._attached.Count;  i++)
			{
				if(sceneObject == this._attached[i])
				{
					rValue = i;
				}
			}
			return(rValue);
		}
		*/
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public Node GetChild(int index)
		{
			if(_children.Count > index)
			{
				return(_children[index]);
			}
			else
			{
				return(null);
			}
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="node"></param>
		public void AddChild(Node node)
		{
			_children.Add(node);
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="node"></param>
		public void RemoveChild(Node node)
		{
			_children.Remove(node);
		}
	}
	
	/*
	public class SceneObject
	{
		public byte SceneType;
		public string SceneName;
		
		public SceneObject()
		{
			
		}
	}*/
	
	
	public class Terrain
	{
		public List<LLVector3> Vertices;
		public List<Face> Faces;
		
		public Terrain()
		{
			Vertices = new List<LLVector3>();
			Faces = new List<Face>();
		}
		
		public LLVector3 CastRay()
		{
			return(new LLVector3(0,0,0));
		}
	}
	
	public struct Face
	{
		public int V1;
		public int V2;
		public int V3;
	}
	
	public class UpdateCommand
	{
		public byte CommandType;
		public Node SObject;
		public LLVector3 Position;
		public LLVector3 Velocity;
		public ushort InternVelocityX;
		public ushort InternVelocityY;
		public ushort InternVelocityZ;
		public LLQuaternion Rotation;
		
		public UpdateCommand()
		{
			
		}
	}
	
	public class UpdateSender
	{
		public BlockingQueue<SendInfo> SendList;
		private Thread _mthread;
		private Server _server;
		private AgentManager _agentManager;
		
		public UpdateSender(Server server, AgentManager agentManager)
		{
			SendList = new BlockingQueue<SendInfo>();
			_server = server;
			_agentManager = agentManager;
		}
		
		public void Startup()
		{
			_mthread = new Thread(new System.Threading.ThreadStart(RunSender));
			_mthread.IsBackground = true;
			_mthread.Start();
		}
		
		private void RunSender()
		{
			//process SendList and send packets to clients
			try
			{
				for(;;)
				{
					SendInfo sendInfo;
					sendInfo = this.SendList.Dequeue();
					
					switch(sendInfo.SentTo)
					{
						case 0:
							this._server.SendPacket(sendInfo.Packet, sendInfo.Incr, sendInfo.NetInfo);
							break;
						case 1:
							this._agentManager.SendPacketToALL(sendInfo.Packet);
							break;
						case 2:
							this._agentManager.SendPacketToAllExcept(sendInfo.Packet, sendInfo.NetInfo.User.AgentID);
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

	public class SendInfo
	{
		public Packet Packet;
		public bool Incr = true;
		public NetworkInfo NetInfo;
		public byte SentTo = 0; // 0 just this client, 1 to all clients, 2 to all except this client.
		
		public SendInfo()
		{
			
		}
	}
}


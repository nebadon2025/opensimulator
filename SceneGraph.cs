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

namespace OpenSimLite
{
	/// <summary>
	/// Manages prim and avatar sim positions and movements and updating clients
	/// </summary>
	public class SceneGraph
	{
		public Node RootNode;
		public Terrain Terrain;
		private PhysicsManager _physics;
		private Server _server;
		private  System.Text.Encoding _enc = System.Text.Encoding.ASCII;
		private libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock _avatarTemplate;

		public NonBlockingQueue<UpdateCommand> Commands;
		
		#region Thread Sync
		//public object CommandsSync = new object();
		private object _sendTerrainSync = new object();
		#endregion
		
		public SceneGraph(Server server)
		{	
			_server = server;
			RootNode = new Node();
			_physics = new PhysicsManager(this);
			Commands = new NonBlockingQueue<UpdateCommand>();
			Terrain = new Terrain();
			
			//testing
			this.SetupTemplate("objectupate168.dat");
		}
		
		public void Update()
		{
			
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
				string data_path = System.AppDomain.CurrentDomain.BaseDirectory + @"\layer_data\";
				
				//send layerdata
				LayerDataPacket layerpack = new LayerDataPacket();
				layerpack.LayerID.Type = 76;
				this.SendLayerData(userInfo, layerpack, data_path+@"layerdata0.dat");
				Console.WriteLine("Sent terrain data");
				this.SendAvatarData(userInfo);
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
		
	}
	
	//any need for separate nodes and sceneobjects? 
	//do we need multiple objects in the same node?
	public class Node
	{
		private List<Node> _children;
		private List<SceneObject> _attached;
		
		public List<Node> ChildNodes
		{
			get
			{
				return(_children);
			}
		}
		
		public List<SceneObject> AttachedObjexts
		{
			get
			{
				return(_attached);
			}
		}
		
		public Node()
		{
			_children = new List<Node>();
			_attached = new List<SceneObject>();
		}
		
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
	
	public class SceneObject
	{
		public byte SceneType;
		
		public SceneObject()
		{
			
		}
	}
	
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
	
	public class Face
	{
		public int V1;
		public int V2;
		public int V3;
		
		public Face()
		{
			
		}
	}
	
	public class UpdateCommand
	{
		public byte CommandType;
		public SceneObject SObject;
		public LLVector3 Position;
		public LLVector3 Velocity;
		public LLQuaternion Rotation;
		
		public UpdateCommand()
		{
			
		}
	}
	
	public class UpdateSender
	{
		public BlockingQueue<SendInfo> SendList;
		private Thread mthread;
		
		public UpdateSender()
		{
			SendList = new BlockingQueue<SendInfo>();
		}
		
		public void Startup()
		{
			mthread = new Thread(new System.Threading.ThreadStart(RunSender));
			mthread.Start();
		}
		
		private void RunSender()
		{
			
		}
	}

	public class SendInfo
	{
		public Packet Packet;
		public bool Incr;
		public NetworkInfo NetInfo;
		
		public SendInfo()
		{
			
		}
	}
}


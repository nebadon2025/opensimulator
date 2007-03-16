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
using System.IO;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Assets;
using libsecondlife;
using Db4objects.Db4o;
using Db4objects.Db4o.Query;

namespace LocalGridServers
{
	/// <summary>
	/// 
	/// </summary>
	/// 
	public class LocalGridPlugin : IGridPlugin
	{
		public LocalGridPlugin()
		{
			
		}
		
		public IGridServer GetGridServer()
		{
			return(new LocalGridServer());
		}
	}
	
	public class LocalAssetPlugin : IAssetPlugin
	{
		public LocalAssetPlugin()
		{
			
		}
		
		public IAssetServer GetAssetServer()
		{
			return(new LocalAssetServer());
		}
	}
	
	public class LocalAssetServer : IAssetServer
	{
		private IAssetReceiver _receiver;
		private BlockingQueue<ARequest> _assetRequests;
		private IObjectContainer db;
		private Thread _localAssetServerThread;
		
		public LocalAssetServer()
		{
			bool yapfile;
			this._assetRequests = new BlockingQueue<ARequest>();
			yapfile = System.IO.File.Exists("assets.yap");
			
			ServerConsole.MainConsole.Instance.WriteLine("Local Asset Server class created");
			try 
			{
				db = Db4oFactory.OpenFile("assets.yap");
				ServerConsole.MainConsole.Instance.WriteLine("Db4 Asset database  creation");
			}
			catch(Exception e) 
			{
				db.Close();
				ServerConsole.MainConsole.Instance.WriteLine("Db4 Asset server :Constructor - Exception occured");
				ServerConsole.MainConsole.Instance.WriteLine(e.ToString());
			}
			if(!yapfile)
			{
				this.SetUpAssetDatabase();
			}
			this._localAssetServerThread = new Thread(new ThreadStart(RunRequests));
			this._localAssetServerThread.IsBackground = true;
			this._localAssetServerThread.Start();
			
		}
		
		public void SetReceiver(IAssetReceiver receiver)
		{
			this._receiver = receiver;
		}
		
		public void RequestAsset(LLUUID assetID, bool isTexture)
		{
			ARequest req = new ARequest();
			req.AssetID = assetID;
			req.IsTexture = isTexture;
			this._assetRequests.Enqueue(req);
		}
		
		public void UpdateAsset(AssetBase asset)
		{
			
		}
		
		public void UploadNewAsset(AssetBase asset)
		{
			
		}
		
		public void SetServerInfo(string ServerUrl, string ServerKey)
		{
			
		}
		public void Close()
		{
			if(db != null)
			{
				Console.WriteLine("Closing local Asset server database");
				db.Close();
			}
		}
		
		private void RunRequests()
		{
			while(true)
			{
				byte[] idata = null;
				bool found = false;
				AssetStorage foundAsset =null;
				ARequest req = this._assetRequests.Dequeue();
				IObjectSet result = db.Query(new AssetUUIDQuery(req.AssetID));
				if(result.Count>0)
				{
					 foundAsset = (AssetStorage) result.Next();
					 found = true;
				}
				
				AssetBase asset = new AssetBase();
				if(found)
				{
					asset.FullID = foundAsset.UUID ;
					asset.Type = foundAsset.Type;
					asset.InvType = foundAsset.Type;
					asset.Name = foundAsset.Name;
					idata = foundAsset.Data;
				}
				else
				{
					asset.FullID = LLUUID.Zero;
				}
				asset.Data = idata;
				_receiver.AssetReceived(asset, req.IsTexture );
			}
			
		}
		
		private void SetUpAssetDatabase()
		{
			Console.WriteLine("setting up Asset database");
			
			AssetBase Image = new AssetBase();
			Image.FullID = new LLUUID("00000000-0000-0000-9999-000000000001");
			Image.Name = "test Texture";
			this.LoadAsset(Image, true,  "testpic2.jp2");
			AssetStorage store = new AssetStorage();
			store.Data = Image.Data;
			store.Name = Image.Name;
			store.UUID = Image.FullID;
			db.Set(store);
			db.Commit();
			
			Image = new AssetBase();
			Image.FullID = new LLUUID("00000000-0000-0000-9999-000000000002");
			Image.Name = "test Texture2";
			this.LoadAsset(Image, true,  "map_base.jp2");
			store = new AssetStorage();
			store.Data = Image.Data;
			store.Name = Image.Name;
			store.UUID = Image.FullID;
			db.Set(store);
			db.Commit();
			
			
			Image.FullID = new LLUUID("00000000-0000-0000-5005-000000000005");
			Image.Name = "Prim Base Texture";
			store = new AssetStorage();
			store.Data = Image.Data;
			store.Name = Image.Name;
			store.UUID = Image.FullID;
			db.Set(store);
			db.Commit();
			
			Image = new AssetBase();
			Image.FullID = new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");
			Image.Name = "Shape";
			this.LoadAsset(Image, false,  "base_shape.dat");
			store = new AssetStorage();
			store.Data = Image.Data;
			store.Name = Image.Name;
			store.UUID = Image.FullID;
			db.Set(store);
			db.Commit();
			
			
		}
		
		private void LoadAsset(AssetBase info, bool image, string filename)
		{
			//should request Asset from storage manager
			//but for now read from file
			
			string dataPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory ,"assets"); //+ folder;
            string fileName = Path.Combine(dataPath, filename);
			FileInfo fInfo = new FileInfo(fileName);
			long numBytes = fInfo.Length;
			FileStream fStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
			byte[] idata = new byte[numBytes];
			BinaryReader br = new BinaryReader(fStream);
			idata= br.ReadBytes((int)numBytes);
			br.Close();
			fStream.Close();
			info.Data = idata;
			//info.loaded=true;
		}
	}
	
	public class LocalGridServer : LocalGridBase
	{
		public List<Login> Sessions = new List<Login>();  
		
		public LocalGridServer()
		{
			Sessions = new List<Login>();
			ServerConsole.MainConsole.Instance.WriteLine("Local Grid Server class created");
		}
		
		public override bool RequestConnection()
		{
			return true;
		}

        public override string GetName()
        {
            return "Local";
        }

		public override AuthenticateResponse AuthenticateSession(LLUUID sessionID, LLUUID agentID, uint circuitCode)
		{
			//we are running local
			AuthenticateResponse user = new AuthenticateResponse();
			
			lock(this.Sessions)
			{
				
				for(int i = 0; i < Sessions.Count; i++)
				{
					if((Sessions[i].Agent == agentID) && (Sessions[i].Session == sessionID))
					{
						user.Authorised = true;
						user.LoginInfo = Sessions[i];
					}
				}
			}
			return(user);
		}
		
		public override bool LogoutSession(LLUUID sessionID, LLUUID agentID, uint circuitCode)
		{
			return(true);
		}
		
		public override UUIDBlock RequestUUIDBlock()
		{
			UUIDBlock uuidBlock = new UUIDBlock();
			return(uuidBlock);
		}
		
		public override void RequestNeighbours()
		{
			return;
		}
		
		public override void SetServerInfo(string ServerUrl, string ServerKey)
		{
			
		}
		
		public override void Close()
		{
			
		}

		/// <summary>
		/// used by the local login server to inform us of new sessions
		/// </summary>
		/// <param name="session"></param>
		public override void AddNewSession(Login session)
		{
			lock(this.Sessions)
			{
				this.Sessions.Add(session);
			}
		}
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

	public class AssetUUIDQuery : Predicate
	{
		private LLUUID _findID;
		
		public AssetUUIDQuery(LLUUID find)
		{
			_findID = find;
		}
		public bool Match(AssetStorage asset)
		{
			return (asset.UUID == _findID);
		}
	}
	
	public class AssetStorage
	{
		public byte[] Data;
		public sbyte Type;
		public string Name;
		public LLUUID UUID;
	}
}

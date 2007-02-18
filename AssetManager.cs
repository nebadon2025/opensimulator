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
	/// Manages local cache of assets and their sending to viewers.
	/// </summary>
	public class AssetManager : IAssetReceived
	{
		public Dictionary<libsecondlife.LLUUID, AssetInfo> Assets;
		public Dictionary<libsecondlife.LLUUID, TextureImage> Textures;
		
		public List<AssetRequest> AssetRequests = new List<AssetRequest>();  //assets ready to be sent to viewers
		public List<TextureRequest> TextureRequests = new List<TextureRequest>(); //textures ready to be sent
		
		public List<AssetRequest> RequestedAssets = new List<AssetRequest>(); //Assets requested from the asset server
		public List<TextureRequest> RequestedTextures = new List<TextureRequest>(); //Textures requested from the asset server
		
		private IAssetServer _assetServer;
		private Server _server;
		
		/// <summary>
		/// 
		/// </summary>
		public AssetManager(Server server, IAssetServer assetServer)
		{
			_server = server;
			_assetServer = assetServer;
			_assetServer.SetReceiver(this);
		}
		
		/// <summary>
		/// 
		/// </summary>
		private void RunAssetManager()
		{
			this.ProcessAssetQueue();
			this.ProcessTextureQueue();
		}
		
		/// <summary>
		/// 
		/// </summary>
		private void ProcessTextureQueue()
		{
			if(this.TextureRequests.Count == 0)
			{
				//no requests waiting
				return;
			}
			int num;
			//should be running in its own thread but for now is called by timer
			
			if(this.TextureRequests.Count < 5)
			{
				//lower than 5 so do all of them
				num = this.TextureRequests.Count;
			}
			else
			{
				num=5;
			}
			TextureRequest req;
			for(int i = 0; i < num; i++)
			{
				req=(TextureRequest)this.TextureRequests[i];
				
				if(req.PacketCounter == 0)
				{
					//first time for this request so send imagedata packet
					if(req.NumPackets == 1)
					{
						//only one packet so send whole file
						ImageDataPacket im = new ImageDataPacket();
						im.ImageID.Packets = 1;
						im.ImageID.ID = req.ImageInfo.FullID;
						im.ImageID.Size = (uint)req.ImageInfo.Data.Length;
						im.ImageData.Data = req.ImageInfo.Data;
						im.ImageID.Codec = 2;
						_server.SendPacket(im, true, req.RequestUser);
						req.PacketCounter++;
						//req.ImageInfo.l= time;
						//System.Console.WriteLine("sent texture: "+req.image_info.FullID);
					}
					else
					{
						//more than one packet so split file up
						ImageDataPacket im = new ImageDataPacket();
						im.ImageID.Packets = (ushort)req.NumPackets;
						im.ImageID.ID = req.ImageInfo.FullID;
						im.ImageID.Size = (uint)req.ImageInfo.Data.Length;
						im.ImageData.Data = new byte[600];
						Array.Copy(req.ImageInfo.Data, 0, im.ImageData.Data, 0, 600);
						im.ImageID.Codec = 2;
						_server.SendPacket(im, true, req.RequestUser);
						req.PacketCounter++;
						//req.ImageInfo.last_used = time;
						//System.Console.WriteLine("sent first packet of texture:
					}
				}
				else
				{
					//send imagepacket
					//more than one packet so split file up
					ImagePacketPacket im = new ImagePacketPacket();
					im.ImageID.Packet = (ushort)req.PacketCounter;
					im.ImageID.ID = req.ImageInfo.FullID;
					int size = req.ImageInfo.Data.Length - 600 - 1000*(req.PacketCounter - 1);
					if(size > 1000) size = 1000;
					im.ImageData.Data = new byte[size];
					Array.Copy(req.ImageInfo.Data, 600 + 1000*(req.PacketCounter - 1), im.ImageData.Data, 0, size);
					_server.SendPacket(im, true, req.RequestUser);
					req.PacketCounter++;
					//req.ImageInfo.last_used = time;
					//System.Console.WriteLine("sent a packet of texture: "+req.image_info.FullID);
				}
			}
			
			//remove requests that have been completed
			for(int i = 0; i < num; i++)
			{
				req=(TextureRequest)this.TextureRequests[i];
				if(req.PacketCounter == req.NumPackets)
				{
					this.TextureRequests.Remove(req);
				}
			}
			
		}
		public void AssetReceived(AssetBase asset)
		{
			//check if it is a texture or not
			//then add to the correct cache list
			//then check for waiting requests for this asset/texture (in the Requested lists)
			//and move those requests into the Requests list.
		}
		#region Assets
		/// <summary>
		/// 
		/// </summary>
		/// <param name="userInfo"></param>
		/// <param name="transferRequest"></param>
		public void AddAssetRequest(NetworkInfo userInfo, TransferRequestPacket transferRequest)
		{
			LLUUID requestID = new LLUUID(transferRequest.TransferInfo.Params, 0);
			//check to see if asset is in local cache, if not we need to request it from asset server.
			if(!this.Assets.ContainsKey(requestID))
			{
				//not found asset	
				// so request from asset server
				AssetRequest request = new AssetRequest();
				request.RequestUser = userInfo;
				request.RequestImage = requestID;
				this.AssetRequests.Add(request);
				this._assetServer.RequestAsset(requestID);
				return;
			}
			//it is in our cache 
			AssetInfo info = this.Assets[requestID];
			
			//work out how many packets it  should be sent in 
			// and add to the AssetRequests list
		}
		
		/// <summary>
		/// 
		/// </summary>
		private void ProcessAssetQueue()
		{
			
		}
		
		#endregion
		
		#region Textures
		/// <summary>
		/// 
		/// </summary>
		/// <param name="userInfo"></param>
		/// <param name="imageID"></param>
		public void AddTextureRequest(NetworkInfo userInfo, LLUUID imageID)
		{
			//check to see if texture is in local cache, if not request from asset server
			if(!this.Textures.ContainsKey(imageID))
			{
				/*not found image so send back image not in data base message
				ImageNotInDatabasePacket im_not = new ImageNotInDatabasePacket();
				im_not.ImageID.ID=imageID;
				_server.SendPacket(im_not, true, userInfo);*/
				
				//not is cache so request from asset server
				TextureRequest request = new TextureRequest();
				request.RequestUser = userInfo;
				request.RequestImage = imageID;
				this.TextureRequests.Add(request);
				this._assetServer.RequestAsset(imageID);
				return;
			}
			TextureImage imag = this.Textures[imageID];
			TextureRequest req = new TextureRequest();
			req.RequestUser = userInfo;
			req.RequestImage = imageID;
			req.ImageInfo = imag;
			
			if(imag.Data.LongLength>600) 
			{
				//over 600 bytes so split up file
				req.NumPackets = 1 + (int)(imag.Data.Length-600+999)/1000;
			}
			else
			{
				req.NumPackets = 1;
			}
			
			this.TextureRequests.Add(req);
		}
		#endregion
		
	}

	public class AssetRequest
	{
		public NetworkInfo RequestUser;
		public LLUUID RequestImage;
		public AssetInfo asset_inf;
		public long data_pointer = 0;
		public int num_packets = 0;
		public int packet_counter = 0;
		//public bool AssetInCache;
		//public int TimeRequested; 
		
		public AssetRequest()
		{
			
		}
	}
	
	public class TextureRequest
	{
		public NetworkInfo RequestUser;
		public LLUUID RequestImage;
		public TextureImage ImageInfo;
		public long DataPointer = 0;
		public int NumPackets = 0;
		public int PacketCounter = 0;
		//public bool TextureInCache;
		//public int TimeRequested;
		
		public TextureRequest()
		{
			
		}
	}
}

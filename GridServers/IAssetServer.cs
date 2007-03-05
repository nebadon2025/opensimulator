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
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using libsecondlife;
using OpenSim.Assets;

namespace OpenSim.GridServers
{
	/// <summary>
	/// Description of IAssetServer.
	/// </summary>
	public class LocalAssetServer : IAssetServer
	{
		private IAssetReceiver _receiver;
		private BlockingQueue<ARequest> _assetRequests;
		
		public LocalAssetServer()
		{
			this._assetRequests = new BlockingQueue<ARequest>();
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
		
		private void RunRequests()
		{
			while(true)
			{
				
			}
		}
	}
	
	public class RemoteAssetServer : IAssetServer
	{
		private IAssetReceiver _receiver;
		private BlockingQueue<ARequest> _assetRequests;
		private Thread _remoteAssetServerThread;
		
		
		public RemoteAssetServer()
		{
			this._assetRequests = new BlockingQueue<ARequest>();
			this._remoteAssetServerThread = new Thread(new ThreadStart(RunRequests));
			this._remoteAssetServerThread.IsBackground = true;
			this._remoteAssetServerThread.Start();
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
		
		private void RunRequests()
		{
			while(true)
			{
				//we need to add support for the asset server not knowing about a requested asset
				ARequest req = this._assetRequests.Dequeue();
				LLUUID assetID = req.AssetID;
				Console.WriteLine(" RemoteAssetServer- Got a AssetServer request, processing it");
				WebRequest AssetLoad = WebRequest.Create(OpenSim_Main.cfg.AssetURL + "getasset/" + OpenSim_Main.cfg.AssetSendKey + "/" + assetID + "/data");
				WebResponse AssetResponse = AssetLoad.GetResponse();
				byte[] idata = new byte[(int)AssetResponse.ContentLength];
				BinaryReader br = new BinaryReader(AssetResponse.GetResponseStream());
				idata = br.ReadBytes((int)AssetResponse.ContentLength);
				br.Close();
				
				AssetBase asset = new AssetBase();
				asset.FullID = assetID;
				asset.Data = idata;
				_receiver.AssetReceived(asset, req.IsTexture );
			}
		}
	}
	
	public interface IAssetServer
	{
		void SetReceiver(IAssetReceiver receiver);
		void RequestAsset(LLUUID assetID, bool isTexture);
		void UpdateAsset(AssetBase asset);
		void UploadNewAsset(AssetBase asset);
	}
	
	// could change to delegate?
	public interface IAssetReceiver
	{
		void AssetReceived(AssetBase asset, bool IsTexture);
		void AssetNotFound(AssetBase asset);
	}
	
	public struct ARequest
	{
		public LLUUID AssetID;
		public bool IsTexture;
	}
}

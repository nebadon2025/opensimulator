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

namespace OpenSimLite
{
	/// <summary>
	/// Manages local cache of assets and their sending to viewers.
	/// </summary>
	public class AssetManager
	{
		public Dictionary<libsecondlife.LLUUID, AssetInfo> Assets;
		public Dictionary<libsecondlife.LLUUID, TextureImage> Textures;
		private AssetServer _assetServer;
		
		/// <summary>
		/// 
		/// </summary>
		public AssetManager(AssetServer assetServer)
		{
			_assetServer=assetServer;
		}
		
		/// <summary>
		/// 
		/// </summary>
		private void RunAssetManager()
		{
			
		}
		
		/// <summary>
		/// 
		/// </summary>
		private void ProcessAssetQueue()
		{
			
		}
		
		/// <summary>
		/// 
		/// </summary>
		private void ProcessTextureQueue()
		{
			
		}
		
		#region Assets
		/// <summary>
		/// 
		/// </summary>
		/// <param name="userInfo"></param>
		/// <param name="transferRequest"></param>
		public void AddAssetRequest(NetworkInfo userInfo, TransferRequestPacket transferRequest)
		{
			LLUUID RequestID = new LLUUID(transferRequest.TransferInfo.Params, 0);
			//check to see if asset is in local cache, if not we need to request it from asset server.
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
		}
		#endregion
		
	}
	
	public class AssetBase
	{
		public byte[] Data;
		public LLUUID FullID;
		public sbyte Type;
		public sbyte InvType;
		public string Name;
		public string Description;
		public string Filename;
		
		public AssetBase()
		{
			
		}
	}
	
	//needed?
	public class TextureImage
	{
		public TextureImage()
		{
			
		}
	}
	
	public class AssetInfo
	{
		public AssetInfo()
		{
			
		}
	}
}

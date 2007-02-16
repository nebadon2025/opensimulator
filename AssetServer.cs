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
using libsecondlife;
using BerkeleyDb;
using Kds.Serialization;
using Kds.Serialization.Buffer;

namespace OpenSim
{
	/// <summary>
	/// Handles connection to Asset Server.
	/// </summary>
	public class AssetServer  : IAssetServer
	{
		private IAssetReceived _receiver;
		
		public AssetServer()
		{
		}
		
		private void Initialise()
		{
			if(Globals.Instance.LocalRunning)
			{
				//we are running not connected to a grid, so use local database
				
			}
		}
		public void SetReceiver(IAssetReceived receiver)
		{
			this._receiver = receiver;
		}
		public void RequestAsset(LLUUID assetID)
		{
			AssetBase asset = null;
			byte[] dataBuffer = new byte[4096];
			byte[] keyBuffer = new byte[256];
			int index;
			DbEntry keyEntry;
			DbEntry dataEntry;
			dataEntry = DbEntry.InOut(dataBuffer, 0, 4096);
			index = 0;
			BerkeleyDatabases.Instance.dbs.Formatter.Serialize<string>(assetID.ToStringHyphenated(),  keyBuffer, ref index);
			byte[] co= new byte[44];
			Array.Copy(keyBuffer,co,44);
			keyEntry = DbEntry.InOut(co, 0, 44);
			ReadStatus status = BerkeleyDatabases.Instance.dbs.AssetDb.Get(null, ref keyEntry, ref dataEntry, DbFile.ReadFlags.None);
			if (status != ReadStatus.Success)
			{
				throw new ApplicationException("Read failed");
			}
			index = 0;
			BerkeleyDatabases.Instance.dbs.Formatter.Deserialize<AssetBase>(ref asset, dataEntry.Buffer, ref index);
			this._receiver.AssetReceived( asset);
		}
		public void UpdateAsset(AssetBase asset)
		{
			//can we use UploadNewAsset to update as well?
			this.UploadNewAsset(asset);
		}
		public void UploadNewAsset(AssetBase asset)
		{
			byte[] dataBuffer = new byte[4096];
			byte[] keyBuffer = new byte[256];
			int index;
			DbEntry keyEntry;
			DbEntry dataEntry;
			index = 0;
			BerkeleyDatabases.Instance.dbs.Formatter.Serialize<string>(asset.FullID.ToStringHyphenated(), keyBuffer, ref index);
			byte[] co= new byte[44];
			Array.Copy(keyBuffer,co,44);
			keyEntry = DbEntry.InOut(co, 0, 44);
			index = 0;
			BerkeleyDatabases.Instance.dbs.Formatter.Serialize<AssetBase>(asset, dataBuffer, ref index);
			dataEntry = DbEntry.InOut(dataBuffer, 0, index);
			WriteStatus status = BerkeleyDatabases.Instance.dbs.AssetDb.Put(null, ref keyEntry, ref dataEntry, DbFile.WriteFlags.None);
			if (status != WriteStatus.Success)
				throw new ApplicationException("Put failed");
		}
	
	}
	
	public interface IAssetServer
	{
		void SetReceiver(IAssetReceived receiver);
		void RequestAsset(LLUUID assetID);
		void UpdateAsset(AssetBase asset);
		void UploadNewAsset(AssetBase asset);
	}
	
	// could change to delegate
	public interface IAssetReceived
	{
		void AssetReceived(AssetBase asset);
	}
}

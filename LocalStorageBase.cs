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
using BerkeleyDb;
using Kds.Serialization;
using Kds.Serialization.Buffer;
using libsecondlife;

namespace OpenSim
{
	/// <summary>
	/// Description of LocalPrimDB.
	/// </summary>
	/// 
	public class LocalPrimDb :ILocalPrimStorage
	{
		public LocalPrimDb()
		{
			
		}
		
		public void LoadScene(IPrimReceiver receiver)
		{
			
		}
		public void CreateNewPrimStorage(PrimAsset prim)
		{
			Console.WriteLine("prim data length is: "+prim.Data.Length);
			byte[] dataBuffer = new byte[4096];
			byte[] keyBuffer = new byte[256];
			int index;
			DbEntry keyEntry;
			DbEntry dataEntry;
			index = 0;
			BerkeleyDatabases.Instance.dbs.Formatter.Serialize<string>(prim.FullID.ToStringHyphenated(), keyBuffer, ref index);
			byte[] co= new byte[44];
			Array.Copy(keyBuffer,co,44);
			keyEntry = DbEntry.InOut(co, 0, 44);
			index = 0;
			BerkeleyDatabases.Instance.dbs.Formatter.Serialize<PrimAsset>(prim, dataBuffer, ref index);
			dataEntry = DbEntry.InOut(dataBuffer, 0, index);
			WriteStatus status = BerkeleyDatabases.Instance.dbs.PrimDb.Put(null, ref keyEntry, ref dataEntry, DbFile.WriteFlags.None);
			if (status != WriteStatus.Success)
				throw new ApplicationException("Put failed");
			
		}
		public void UpdatePrimStorage(PrimAsset prim)
		{
			//can we just use CreateNewPrimStorage to update a prim?
			this.CreateNewPrimStorage(prim);
		}
		public void RemovePrimStorage()
		{
			
		}
		public PrimAsset GetPrimFromStroage(LLUUID primID)
		{
			PrimAsset prim = null;
			byte[] dataBuffer = new byte[4096];
			byte[] keyBuffer = new byte[256];
			int index;
			DbEntry keyEntry;
			DbEntry dataEntry;
			dataEntry = DbEntry.InOut(dataBuffer, 0, 4096);
			index = 0;
			BerkeleyDatabases.Instance.dbs.Formatter.Serialize<string>(primID.ToStringHyphenated(),  keyBuffer, ref index);
			byte[] co= new byte[44];
			Array.Copy(keyBuffer,co,44);
			keyEntry = DbEntry.InOut(co, 0, 44);
			ReadStatus status = BerkeleyDatabases.Instance.dbs.PrimDb.Get(null, ref keyEntry, ref dataEntry, DbFile.ReadFlags.None);
			if (status != ReadStatus.Success)
			{
				throw new ApplicationException("Read failed");
			}
			index = 0;
			BerkeleyDatabases.Instance.dbs.Formatter.Deserialize<PrimAsset>(ref prim, dataEntry.Buffer, ref index);
			return prim;
		}
		
		/// <summary>
		/// test function
		/// </summary>
		public void ReadWholedatabase()
		{
			foreach (KeyDataPair entry in BerkeleyDatabases.Instance.dbs.PrimDb.OpenCursor(null, DbFileCursor.CreateFlags.None)) {
				PrimAsset vendor = null;
				int index = entry.Data.Start;
				BerkeleyDatabases.Instance.dbs.Formatter.Deserialize<PrimAsset>(ref vendor, entry.Data.Buffer, ref index);
				Console.WriteLine("prim found: "+vendor.Name + " "+ vendor.Description);
			}
		}
	}
	
	public interface ILocalPrimStorage
	{
		void LoadScene(IPrimReceiver receiver);
		void CreateNewPrimStorage(PrimAsset prim);
		void UpdatePrimStorage(PrimAsset prim);
		void RemovePrimStorage();
		PrimAsset GetPrimFromStroage(LLUUID primID);
	}
	
	//change to delegate?
	public interface IPrimReceiver
	{
		void ReceivePrim(PrimAsset prim);
	}
}

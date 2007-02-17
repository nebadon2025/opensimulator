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
using System.IO;
using Kds.Serialization;
using Kds.Serialization.Buffer;

namespace OpenSim
{
	/// <summary>
	/// Description of BerkeleyDatabase.
	/// </summary>
	public class BerkeleyDatabases
	{
		
		public const string primDbName = "prim.db";
		public const string assetDbName = "asset.db";
		

		// will be initialized on first access to static field in a thread-safe way
		private static readonly BerkeleyDatabases instance = new BerkeleyDatabases();
		private FileStream errStream;
		private string dbDir;
		private DbBTree primDb;
		private DbBTree assetDb;
		
		// used for key gnerator call-backs, not thread-safe
		private BEFormatter formatter;
		private byte[] keyBuffer = new byte[2048];

		private BerkeleyDatabases() {
			formatter = new BEFormatter();
			formatter.RegisterField<PrimAsset>(new PrimAssetField(formatter));
			formatter.RegisterField<AssetBase>(new AssetBaseField(formatter));
		
		}
		
		public BerkeleyDatabases dbs;

		// singleton
		public static BerkeleyDatabases Instance {
			get { return instance; }
		}

		public void Open(string dbDir, string appName, Stream errStream) {
			this.dbDir = dbDir;

			// open local prim database
			Db db = new Db(DbCreateFlags.None);
			db.ErrorPrefix = appName;
			db.ErrorStream = errStream;
			primDb = (DbBTree)db.Open(null, PrimDbName, null, DbType.BTree, Db.OpenFlags.Create, 0);
			
			//open asset server database
			db = new Db(DbCreateFlags.None);
			db.ErrorPrefix = appName;
			db.ErrorStream = errStream;
			assetDb = (DbBTree)db.Open(null, AssetDbName, null, DbType.BTree, Db.OpenFlags.Create, 0);
			
		}

		public void Remove() {
			try {
				Db db = new Db(DbCreateFlags.None);
				db.Remove(PrimDbName, null);
			}
			catch { }
	
			try {
				Db db = new Db(DbCreateFlags.None);
				db.Remove(AssetDbName, null);
			}
			catch { }
			
		}

		public void Close() {
			if (primDb != null) {
				primDb.GetDb().Close();
				primDb = null;
			}
			
			if (assetDb != null) {
				assetDb.GetDb().Close();
				assetDb = null;
			}
			
		}

		public BEFormatter Formatter {
			get { return formatter; }
		}

		public string PrimDbName {
			get { return Path.Combine(dbDir, primDbName); }
		}

		public DbBTree PrimDb {
			get { return primDb; }
		}
		
		public string AssetDbName {
			get { return Path.Combine(dbDir, assetDbName); }
		}

		public DbBTree AssetDb {
			get { return assetDb; }
		}
		
		public void Startup()
		{
			dbs = BerkeleyDatabases.Instance;
			string dbPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Db");
			string appName = Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase);
			errStream = File.Open(Path.Combine(dbPath, "errors.txt"), FileMode.OpenOrCreate, FileAccess.Write);
			dbs.Open(dbPath, appName, errStream);
		}
	}
	
	public class PrimAssetField: ReferenceField<PrimAsset>
	{
		public PrimAssetField(Formatter formatter) : base(formatter) { }

		protected override void SerializeValue(PrimAsset value) {
			Formatter.Serialize<string>(value.Name);
			Formatter.Serialize<string>(value.Description);
			Formatter.Serialize<int>((int?)value.Data.Length);
			for( int i = 0; i < value.Data.Length; i++)
			{
				Formatter.Serialize<byte>((byte?)value.Data[i]);
			}
		}

		protected override void DeserializeInstance(ref PrimAsset instance) {
			if (instance == null)
				instance = new PrimAsset();
		}

		protected override void DeserializeMembers(PrimAsset instance) {
			Formatter.Deserialize<string>(ref instance.Name);
			Formatter.Deserialize<string>(ref instance.Description);
			int dataLength;
			dataLength =(int) Formatter.Deserialize<int>();
			instance.Data = new byte[dataLength];
			for( int i = 0; i < dataLength; i++)
			{
				instance.Data[i] =(byte) Formatter.Deserialize<byte>();
			}
			
		}

		protected override void SkipValue() {
			if (Formatter.Skip<string>())
				if	(Formatter.Skip<string>())
				if ( Formatter.Skip<int>())	
					Formatter.Skip<byte[]>();
			
		}
	}
	
	public class AssetBaseField: ReferenceField<AssetBase>
	{
		public AssetBaseField(Formatter formatter) : base(formatter) { }

		protected override void SerializeValue(AssetBase value) {
			Formatter.Serialize<string>(value.Name);
			Formatter.Serialize<string>(value.Description);
			Formatter.Serialize<int>((int?)value.Data.Length);
			for( int i = 0; i < value.Data.Length; i++)
			{
				Formatter.Serialize<byte>((byte?)value.Data[i]);
			}
		}

		protected override void DeserializeInstance(ref AssetBase instance) {
			if (instance == null)
				instance = new AssetBase();
		}

		protected override void DeserializeMembers(AssetBase instance) {
			Formatter.Deserialize<string>(ref instance.Name);
			Formatter.Deserialize<string>(ref instance.Description);
			int dataLength;
			dataLength =(int) Formatter.Deserialize<int>();
			instance.Data = new byte[dataLength];
			for( int i = 0; i < dataLength; i++)
			{
				instance.Data[i] =(byte) Formatter.Deserialize<byte>();
			}
		}

		protected override void SkipValue() {
			if (Formatter.Skip<string>())
				if 	(Formatter.Skip<string>())
				if ( Formatter.Skip<int>())	
					Formatter.Skip<byte[]>();
			
		}
	}
}

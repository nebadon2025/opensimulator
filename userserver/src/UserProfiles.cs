/*
Copyright (c) OpenGrid project, http://osgrid.org/


* All rights reserved.
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
*/

using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;
using Nwc.XmlRpc;
using ServerConsole;

namespace OpenGridServices
{
	/// <summary>
	/// </summary>
	public class UserProfileManager {
	
		public Dictionary<LLUUID, UserProfile> UserProfiles = new Dictionary<LLUUID, UserProfile>();

		public UserProfileManager() {
		}
		
		public void InitUserProfiles() {
			// TODO: need to load from database
		}

		public UserProfile GetProfileByName(string firstname, string lastname) {
			foreach (libsecondlife.LLUUID UUID in UserProfiles.Keys) {
				if((UserProfiles[UUID].firstname==firstname) && (UserProfiles[UUID].lastname==lastname)) return UserProfiles[UUID];
			}
			return null;
		}

		public UserProfile GetProfileByLLUUID(LLUUID ProfileLLUUID) {
			return UserProfiles[ProfileLLUUID];
		}
	
		public bool AuthenticateUser(string firstname, string lastname, string passwd) {
			UserProfile TheUser=GetProfileByName(firstname, lastname);
			if(TheUser != null) 
			if(TheUser.MD5passwd==passwd) {
				return true;
			} else {
				return false;
			} else return false;
			
		}

		public void SetGod(LLUUID GodID) {
			this.UserProfiles[GodID].IsGridGod=true;
		}

		public UserProfile CreateNewProfile(string firstname, string lastname, string MD5passwd) {
			UserProfile newprofile = new UserProfile();
			newprofile.homeregionhandle=Util.UIntsToLong((997*256), (996*256));
			newprofile.firstname=firstname;
			newprofile.lastname=lastname;
			newprofile.MD5passwd=MD5passwd;
                        newprofile.UUID=LLUUID.Random();			
			this.UserProfiles.Add(newprofile.UUID,newprofile);
			return newprofile;
		}

	}

	public class UserProfile {
	
		public string firstname;
		public string lastname;
		public ulong homeregionhandle;
		public LLVector3 homepos;
		public LLVector3 homelookat;

		public bool IsGridGod=false;
		public bool IsLocal=true;	// will be used in future for visitors from foreign grids
		public string AssetURL;
		public string MD5passwd;

		public LLUUID CurrentSessionID;
		public LLUUID CurrentSecureSessionID;
		public LLUUID UUID;
		public Dictionary<LLUUID, uint> Circuits = new Dictionary<LLUUID, uint>();	// tracks circuit codes
		public Dictionary<LLUUID, InventoryFolder> InventoryFolders;
		public Dictionary<LLUUID, InventoryItem> InventoryItems;
	
		public UserProfile() {
			Circuits = new Dictionary<LLUUID, uint>();
			InventoryFolders = new Dictionary<LLUUID, InventoryFolder>();
			InventoryItems = new Dictionary<LLUUID, InventoryItem>();
			homeregionhandle=Util.UIntsToLong((997*256), (996*256));;
		}
	
		public void InitSessionData() {
			CurrentSessionID=LLUUID.Random();
			CurrentSecureSessionID=LLUUID.Random();
		}
	
		public void AddSimCircuit(uint circuit_code, LLUUID region_UUID) {
			if(!this.Circuits.ContainsKey(region_UUID)) this.Circuits.Add(region_UUID, circuit_code);
		}
	}

	public class InventoryFolder {
		public LLUUID FolderID;
		public LLUUID ParentID;
		public string FolderName;
		public ushort DefaultType;
		public ushort Version;
	}

	public class InventoryItem {			//TODO: Fixup this and add full permissions etc
		public LLUUID FolderID;
		public LLUUID OwnerID;
		public LLUUID ItemID;
		public LLUUID AssetID;
		public LLUUID CreatorID = LLUUID.Zero;
		public sbyte InvType;
		public sbyte Type;
		public string Name;
		public string Description;
	}

	public class SimProfile {
                public LLUUID UUID;
                public ulong regionhandle;
                public string regionname;
                public string sim_ip;
                public uint sim_port;
                public string caps_url;
                public uint RegionLocX;
                public uint RegionLocY;
                public string sendkey;
                public string recvkey;


               	public SimProfile LoadFromGrid(ulong region_handle, string GridURL, string SendKey, string RecvKey) {
			try {
			Hashtable GridReqParams = new Hashtable();
			GridReqParams["region_handle"]=region_handle.ToString();
			GridReqParams["caller"]="userserver";
			GridReqParams["authkey"]=SendKey;
			ArrayList SendParams = new ArrayList();
			SendParams.Add(GridReqParams);
			XmlRpcRequest GridReq = new XmlRpcRequest("get_sim_info",GridReqParams);
	
			XmlRpcResponse GridResp = GridReq.Send(GridURL,3000);
		
			Hashtable RespData=(Hashtable)GridResp.Value;	
			Console.WriteLine(RespData.ToString());
			} catch(Exception e) {
				Console.WriteLine(e.ToString());
			}
			return this;
		}
 
		public SimProfile() {
		}


        }

}

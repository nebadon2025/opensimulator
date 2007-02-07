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

namespace OpenSimLite
{
	/// <summary>
	/// Local cache of Inventories
	/// </summary>
	public class InventoryManager
	{
		private List<AgentInventory> _agentsInventory;
		private List<UserServerRequest> _serverRequests; //list of requests made to user server.
		
		public InventoryManager()
		{
			_agentsInventory = new List<AgentInventory>();
			_serverRequests = new List<UserServerRequest>();
		}
	}
	
	public class AgentInventory
	{
		//Holds the local copy of Inventory info for a agent
		public List<InventoryFolder> Folders;
		public int LastCached;  //time this was last stored/compared to user server
		public LLUUID AgentID;
		
		public AgentInventory()
		{
			Folders = new List<InventoryFolder>();
		}
	}
	
	public class InventoryFolder
	{
		public List<InventoryItem> Items;
		//public List<InventoryFolder> Subfolders;
		public LLUUID FolderID;
		public LLUUID OwnerID;
		public LLUUID ParentID;
		public string Name;
		public byte Type;
		
		public InventoryFolder()
		{
			Items = new List<InventoryItem>();
			//Subfolders = new List<InventoryFolder>();
		}
		
	}
	
	public class InventoryItem
	{
		public LLUUID FolderID;
		public LLUUID OwnerID;
		public LLUUID ItemID;
		public LLUUID AssetID;
		public LLUUID CreatorID;
		public sbyte InvType;
		public sbyte Type;
		public string Name;
		public string Description;
		
		public InventoryItem()
		{
			this.CreatorID = LLUUID.Zero;
		}
	}
	
	public class UserServerRequest
	{
		public UserServerRequest()
		{
			
		}
	}
}

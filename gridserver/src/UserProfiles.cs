/*
Copyright (c) OpenSim project, http://osgrid.org/


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
using ServerConsole;

namespace OpenGridServices
{
	/// <summary>
	/// </summary>
	public class UserProfileManager {
	
		private Dictionary<LLUUID, UserProfile> UserProfiles = new Dictionary<LLUUID, UserProfile>();

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
	}

	public class UserProfile {
	
		public string firstname;
		public string lastname;
		public bool IsGridGod;
		public string MD5passwd;
		
		public UserProfile() {
		}

	}
}

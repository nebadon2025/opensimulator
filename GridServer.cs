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

namespace OpenSim
{
	/// <summary>
	/// Handles connection to Grid Servers.
	/// also Sim to Sim connections?
	/// </summary>
	public class GridServer //:IGridServer
	{
		public List<Logon> Sessions = new List<Logon>();  //should change to something other than logon classes? 
		
		public GridServer()
		{
			Sessions = new List<Logon>();
		}
		
		public AuthenticateResponse AuthenticateSession(LLUUID sessionID, LLUUID agentID, uint circuitCode)
		{
			//For Grid use:
			//should check to see if it is a teleportation, if so then we should be expecting this session, agent. (?)
			//if not check with User server/ login server that it is authorised.
			
			//but for now we are running local
			AuthenticateResponse user = new AuthenticateResponse();
			
			lock(this.Sessions)
			{
				
				for(int i = 0; i < Sessions.Count; i++)
				{
					if((Sessions[i].Agent == agentID) && (Sessions[i].Session == sessionID))
					{
						user.Authorised = true;
						user.LogonInfo = Sessions[i];
					}
				}
			}
			return(user);
		}
		
		public void AddNewSession(Logon session)
		{
			lock(this.Sessions)
			{
				this.Sessions.Add(session);
			}
		}
	}
	
	public interface IGridServer
	{
		bool RequestConnection(RegionInfo myRegion);
		UUIDBlock RequestUUIDBlock();
		RegionInfo[] RequestNeighbours();
		AuthenticateResponse AuthenticateSession(LLUUID sessionID, LLUUID agentID, uint circuitCode);
		void AddNewSession(Logon session);
	}
	
	public struct UUIDBlock
	{
		public LLUUID BlockStart;
		public LLUUID BlockEnd;
	}
	
	public class AuthenticateResponse
	{
		public bool Authorised;
		public Logon LogonInfo;
		
		public AuthenticateResponse()
		{
			
		}
			
	}
}


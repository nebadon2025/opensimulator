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
using System.Net;
using System.Net.Sockets;
using System.IO;
using libsecondlife;

namespace OpenSim.GridServers
{
	/// <summary>
	/// Handles connection to Grid Servers.
	/// also Sim to Sim connections?
	/// </summary>
	public class LocalGridServer :IGridServer
	{
		public List<Login> Sessions = new List<Login>();  
		
		public LocalGridServer()
		{
			Sessions = new List<Login>();
		}
		
		public bool RequestConnection()
		{
			return true;
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
						user.LoginInfo = Sessions[i];
					}
				}
			}
			return(user);
		}
		
		public UUIDBlock RequestUUIDBlock()
		{
			UUIDBlock uuidBlock = new UUIDBlock();
			return(uuidBlock);
		}
		
		public void RequestNeighbours()
		{
			return;
		}
		
		/// <summary>
		/// used by the local login server to inform us of new sessions
		/// </summary>
		/// <param name="session"></param>
		public void AddNewSession(Login session)
		{
			lock(this.Sessions)
			{
				this.Sessions.Add(session);
			}
		}
	}
	
	public class RemoteGridServer :IGridServer
	{
		
		public RemoteGridServer()
		{
		}
		
		public bool RequestConnection()
		{
			return true;
		}
		public AuthenticateResponse AuthenticateSession(LLUUID sessionID, LLUUID agentID, uint circuitCode)
		{
			AuthenticateResponse user = new AuthenticateResponse();
			
			WebRequest CheckSession = WebRequest.Create(OpenSim_Main.cfg.GridURL + "/usersessions/" + OpenSim_Main.cfg.GridSendKey + "/" + agentID.ToString() + "/" + circuitCode.ToString() + "/exists");
			WebResponse GridResponse = CheckSession.GetResponse();
			StreamReader sr = new StreamReader(GridResponse.GetResponseStream());
			String grTest = sr.ReadLine();
			sr.Close();
			GridResponse.Close();
			if(String.IsNullOrEmpty(grTest) || grTest.Equals("1")) 
			{
				// YAY! Valid login
				user.Authorised = true;
				user.LoginInfo = new Login();
				user.LoginInfo.Agent = agentID;
				user.LoginInfo.Session = sessionID;
				user.LoginInfo.First = "";
				user.LoginInfo.Last = "";
				
			}
			else 
			{
				// Invalid
				user.Authorised = false;	
			}
			
			return(user);
		}
		
		public UUIDBlock RequestUUIDBlock()
		{
			UUIDBlock uuidBlock = new UUIDBlock();
			return(uuidBlock);
		}
		
		public void RequestNeighbours()
		{
			return;
		}
		
	}
	
	public interface IGridServer
	{
		bool RequestConnection();
		UUIDBlock RequestUUIDBlock();
		void RequestNeighbours(); //should return a array of neighbouring regions
		AuthenticateResponse AuthenticateSession(LLUUID sessionID, LLUUID agentID, uint circuitCode);
	}
	
	public struct UUIDBlock
	{
		public LLUUID BlockStart;
		public LLUUID BlockEnd;
	}
	
	public class AuthenticateResponse
	{
		public bool Authorised;
		public Login LoginInfo;
		
		public AuthenticateResponse()
		{
			
		}
			
	}
	
	public class Login
    {
    	public string First = "Test";
    	public string Last = "User";
    	public LLUUID Agent;
    	public LLUUID Session;
    	public LLUUID InventoryFolder;
    	public LLUUID BaseFolder;
    	public Login()
    	{
    		
    	}
    }

}

/*
* 
Copyright (c) OpenSim project, http://sim.opensecondlife.org/
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
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim
{
	class Controller
	{
		private Server _viewerServer;
		private BackboneServers _backboneServers;
		private LoginServer _loginServer;
		private GridManager _gridManager;
		private AgentManager _agentManager;
		private AssetManager _assetManager;
		private SceneGraph _scene;
		private PrimManager _primManager;
		
		public static void Main(string[] args)
		{
			Controller c = new Controller();
			bool Run = true;
            while( Run ) 
            {
            	
            	string input = Console.ReadLine();

            	if(input == "Exit")
            	{
            		Run = false;
            	}
           	 	
            }
            
            BerkeleyDatabases.Instance.Close();
		}
		
		public Controller()
		{
			_backboneServers = new BackboneServers();
			_viewerServer = new Server(_backboneServers.UserServer);
			_agentManager = new AgentManager(_viewerServer);
			_gridManager = new GridManager(_viewerServer, _agentManager);
			_scene = new SceneGraph(_viewerServer, _agentManager);
			_assetManager = new AssetManager(_viewerServer, _backboneServers.AssetServer);
			ClientConnection.Grid = _gridManager;
			ClientConnection.Scene = _scene;
			ClientConnection.AgentManager = _agentManager;
			ClientConnection.UserServer = _backboneServers.UserServer;
			_viewerServer.Startup();
			BerkeleyDatabases.Instance.Startup();
			_primManager = new PrimManager();
			
			if(Globals.Instance.StartLoginServer)
			{
				_loginServer = new LoginServer(_backboneServers.UserServer);
				_loginServer.Startup();
			}
			
		}
		
		private void Initialise()
		{
			LoadSettings();
			
		}
		
		private void LoadSettings()
		{
			
		}
		
	}
	
	public class BackboneServers
	{
		public GridServer GridServer;
		public UserServer UserServer;
		public AssetServer AssetServer;
		
		public BackboneServers()
		{
			this.GridServer = new GridServer();
			this.UserServer = new UserServer();
			this.AssetServer = new AssetServer();
		}
	}
}

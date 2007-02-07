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
	/// Description of Globals.
	/// </summary>
	public sealed class Globals
	{
		private static Globals instance = new Globals();
		private GridManager _grid;
		
		public static Globals Instance 
		{
			get 
			{
				return instance;
			}
		}
		
		public GridManager Grid
		{
			set
			{
				_grid = value;
			}
		}
		
		public bool LocalRunning = true;
		public string SimIPAddress = "127.0.0.1";
		public int SimPort = 50000;
		public string RegionName = "Test Sandbox\0";
		public ulong RegionHandle = 1096213093147648;
		
		public bool StartLoginServer = true;
		public ushort LoginServerPort = 8080;
		public List<Logon> IncomingLogins = new List<Logon>();
		
		/// <summary>
		/// 
		/// </summary>
		private Globals()
		{
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public LLUUID RequestUUID(byte type)
		{
			return(_grid.RequestUUID(type));
		}
	}
}

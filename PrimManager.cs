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

namespace OpenSim
{
	/// <summary>
	/// Manages and provides local cache of Prims.
	/// </summary>
	public class PrimManager
	{
		private LocalPrimDb _localPrimDB;
		
		public PrimManager()
		{
			this._localPrimDB = new LocalPrimDb();
			
			
			//database test
			/*PrimAsset prim = new PrimAsset();
			prim.Name="happy now";
			prim.Description= "test";
			prim.FullID = new LLUUID("00000000-0000-0000-0000-000000000008");
			this._localPrimDB.CreateNewPrimStorage(prim);
			
			PrimAsset prim1 = this._localPrimDB.GetPrimFromStroage( new LLUUID("00000000-0000-0000-0000-000000000008"));
			Console.WriteLine("prim recieved : "+prim1.Name + " "+ prim1.Description);
			
			//this._localPrimDB.ReadWholedatabase();
			*/
		}
		
	}
}

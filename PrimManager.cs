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
using System.IO;
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim
{
	/// <summary>
	/// Manages and provides local cache of Prims.
	/// </summary>
	public class PrimManager
	{
		private LocalPrimDb _localPrimDB;
		private int _localPrimNumber;
		private Dictionary<libsecondlife.LLUUID,PrimAsset> PrimList;
		
		public PrimManager()
		{
			this._localPrimDB = new LocalPrimDb();
			this.PrimList = new Dictionary<libsecondlife.LLUUID,PrimAsset>();
		}
		
		/// <summary>
		/// Called when a prim is created in-world
		/// </summary>
		/// <param name="userInfo"></param>
		/// <param name="addPacket"></param>
		/// <returns></returns>
		public PrimAsset CreateNewPrim(NetworkInfo userInfo, ObjectAddPacket addPacket)
		{
			PrimData PData = new PrimData();
			PData.OwnerID = userInfo.User.AgentID;
			PData.PCode = addPacket.ObjectData.PCode;
			PData.PathBegin = addPacket.ObjectData.PathBegin;
			PData.PathEnd = addPacket.ObjectData.PathEnd;
			PData.PathScaleX = addPacket.ObjectData.PathScaleX;
			PData.PathScaleY = addPacket.ObjectData.PathScaleY;
			PData.PathShearX = addPacket.ObjectData.PathShearX;
			PData.PathShearY = addPacket.ObjectData.PathShearY;
			PData.PathSkew = addPacket.ObjectData.PathSkew;
			PData.ProfileBegin = addPacket.ObjectData.ProfileBegin;
			PData.ProfileEnd = addPacket.ObjectData.ProfileEnd;
			PData.Scale = addPacket.ObjectData.Scale;
			PData.PathCurve = addPacket.ObjectData.PathCurve;
			PData.ProfileCurve = addPacket.ObjectData.ProfileCurve;
			PData.ParentID = 0;
			PData.ProfileHollow = addPacket.ObjectData.ProfileHollow;
			PData.AddFlags = addPacket.ObjectData.AddFlags;
			
			//finish off copying rest of shape data
			PData.LocalID = (uint)(702000 + _localPrimNumber);
			PData.FullID = new LLUUID("edba7151-5857-acc5-b30b-f01efefd"+_localPrimNumber.ToString("0000"));
			PData.Position = addPacket.ObjectData.RayEnd;
			_localPrimNumber++;
			
			//for now give it a default texture
			PData.Texture = new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-5005-000000000005"));
			
			PrimAsset prim = new PrimAsset();
			prim.FullID = PData.FullID;
			prim.PrimData = PData;
			this.PrimList.Add(prim.FullID, prim);
			
			//should copy to local prim database (and at some point upload to the asset server)
			
			return(prim);
		}
		
	}
}

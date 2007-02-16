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
	/// Description of Assets.
	/// </summary>
	public class AssetBase
	{
		public byte[] Data;
		public LLUUID FullID;
		public sbyte Type;
		public sbyte InvType;
		public string Name;
		public string Description;
		public string Filename;
		
		public AssetBase()
		{
			
		}
	}
	
	public class PrimAsset: AssetBase
	{
		public PrimData PrimData;
		
		public PrimAsset()
		{
			this.PrimData = new PrimData();
		}
	}
	public class PrimData
	{
		public LLUUID OwnerID;
		public byte PCode;
		public byte PathBegin;
		public byte PathEnd;
		public byte PathScaleX;
		public byte PathScaleY;
		public byte PathShearX;
		public byte PathShearY;
		public sbyte PathSkew;
		public byte ProfileBegin;
		public byte ProfileEnd;
		public LLVector3 Scale;
		public byte PathCurve;
		public byte ProfileCurve;
		public uint ParentID=0;
		public byte ProfileHollow;
		//public bool DataBaseStorage=false;
		
		public PrimData()
		{
			
		}
	}
	
	//a hangover from the old code, so is it needed for future use?
	public class TextureImage: AssetBase
	{
		public TextureImage()
		{
			
		}
	}
	
	//a hangover from the old code, so is it needed for future use?
	public class AssetInfo :AssetBase
	{
		public AssetInfo()
		{
			
		}
	}
	
}

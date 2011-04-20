/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */


using System.IO;
using OpenMetaverse;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface ITerrainModule
    {
        void LoadFromFile(string filename);
        void SaveToFile(string filename);
        void ModifyTerrain(UUID user, Vector3 pos, byte size, byte action, UUID agentId);

        /// <summary>
        /// Taint the terrain. This will lead to sending the terrain data to the clients again.
        /// Use this if you change terrain data outside of the terrain module (e.g. in osTerrainSetHeight)
        /// </summary>
        void TaintTerrain();
        
        /// <summary>
        /// Load a terrain from a stream.
        /// </summary>
        /// <param name="filename">
        /// Only required here to identify the image type.  Not otherwise used in the loading itself.
        /// </param>
        /// <param name="stream"></param>
        void LoadFromStream(string filename, Stream stream);
        void LoadFromStream(string filename, System.Uri pathToTerrainHeightmap);
        /// <summary>
        /// Save a terrain to a stream.
        /// </summary>
        /// <param name="filename">
        /// Only required here to identify the image type.  Not otherwise used in the saving itself.
        /// </param>
        /// <param name="stream"></param>
        void SaveToStream(string filename, Stream stream);

        void InstallPlugin(string name, ITerrainEffect plug);

        void UndoTerrain(ITerrainChannel channel);

        //DSG SYNC
        /// <summary>
        /// Invoked by receiving a terrain sync message. First, check if the 
        /// timestamp is more advance than the local copy. If so, update the 
        /// local terrain copy.
        /// </summary>
        /// <param name="timeStamp">The time that the updated terrain was 
        /// created</param>
        /// <param name="actorID">The actor who created the update.</param>
        /// <param name="terrainData">The updated terrain</param>
        bool UpdateTerrianBySync(long timeStamp, string actorID, string terrainData);
        /// <summary>
        /// Return true if the most recent update on terrain is done locally (i.e. not by receiving a terrain-sync message).
        /// </summary>
        /// <param name="localActorID"></param>
        /// <returns></returns>
        bool TerrianModifiedLocally(string localActorID);
        /// <summary>
        /// Obtain the timestemp and actorID information for the most recent update on terrain.
        /// </summary>
        /// <param name="lastUpdateTimeStamp"></param>
        /// <param name="lastUpdateActorID"></param>
        void GetSyncInfo(out long lastUpdateTimeStamp, out string lastUpdateActorID);

        /// <summary>
        /// This is only supposed to be called by Persistence actor, which will
        /// set the timestamp and actorID values for terrain upon initialization time.
        /// </summary>
        /// <param name="lastUpdateTimeStamp"></param>
        /// <param name="lastUpdateActorID"></param>
        void SetSyncInfo(long lastUpdateTimeStamp, string lastUpdateActorID);
        //end of DSG SYNC
    }
}

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
 *     * Neither the name of the OpenSim Project nor the
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

//KittyL: Added to support running script engine actor
using System;
using System.Collections.Generic;

using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Framework.Interfaces
{
    public enum DSGActorTypes
    {
        Unknown,
        ScenePersistence, //the Scene (data store), which is now considered as a persistence actor
        ClientManager,
        ScriptEngine,
        PhysicsEngine
    }



    /////////////////////////////////////////////////////////////////////////////////////
    //Interface for SceneGraph to call into RegionSyncModule
    /////////////////////////////////////////////////////////////////////////////////////
    public interface IRegionSyncModule
    {
        bool Active { get; } //if true, this RegionSyncModule is connected into the synchronization overlay 
        string ActorID { get; } //might be phased out soon
        string SyncID { get; }
        //DSGActorTypes DSGActorType { get; set; }
        bool IsSyncRelay { get; }

        /// <summary>
        /// The mapping of a property (identified by its name) to the index of a bucket.
        /// </summary>
        Dictionary<SceneObjectPartProperties, string> PrimPropertyBucketMap { get; }
        /// <summary>
        /// The text description of the properties in each bucket, e.g. "General", "Physics"
        /// </summary>
        List<string> PropertyBucketDescription { get; }

        //Enqueue updates for scene-objects and scene-presences
        void QueueSceneObjectPartForUpdate(SceneObjectPart part);
        void QueueScenePresenceForTerseUpdate(ScenePresence presence);
        //void QueueSceneObjectGroupForUpdate(SceneObjectGroup sog);

        //The folloiwng calls deal with object updates, and will insert each update into an outgoing queue of each SyncConnector
        void SendSceneUpdates();
        void SendNewObject(SceneObjectGroup sog);
        void SendDeleteObject(SceneObjectGroup sog, bool softDelete);
        void SendLinkObject(SceneObjectGroup linkedGroup, SceneObjectPart root, List<SceneObjectPart> children);
        void SendDeLinkObject(List<SceneObjectPart> prims, List<SceneObjectGroup> beforeDelinkGroups, List<SceneObjectGroup> afterDelinkGroups);

        //In RegionSyncModule's implementation, 
        //The following calls send out a message immediately, w/o putting it in the SyncConnector's outgoing queue.
        //May need some optimization there on the priorities.
        void SendTerrainUpdates(long updateTimeStamp, string lastUpdateActorID);
        //For propogating scene events to other actors
        void PublishSceneEvent(EventManager.EventNames ev, Object[] evArgs);

        //TODO LIST:
        //Special API for handling avatars
        //void QueuePresenceForTerseUpdate(ScenePresence presence)
        //void SendAvatarUpdates();

    }

    /// <summary>
    /// Interface for invoking DSGActor specific functions or accessing members whose values depend on the actor's type.
    /// </summary>
    public interface IDSGActorSyncModule
    {
        DSGActorTypes ActorType { get; }
        string ActorID { get; }
    }

}
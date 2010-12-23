/*
 * Copyright (c) Contributors: TO BE FILLED
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Region.CoreModules.Framework.InterfaceCommander;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using log4net;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    /*
    public class DSGActorBase
    {
        protected RegionSyncModule m_regionSyncModule;

        public DSGActorBase(RegionSyncModule regionSyncModule)
        {
            m_regionSyncModule = regionSyncModule;
        }

        public virtual void init()
        {

        }


    }
     * */

    public class ScenePersistenceSyncModule : INonSharedRegionModule, IDSGActorSyncModule    
    {
        #region INonSharedRegionModule

        public void Initialise(IConfigSource config)
        {
            m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            IConfig syncConfig = config.Configs["RegionSyncModule"];
            m_active = false;
            if (syncConfig == null)
            {
                m_log.Warn("[Scene Persistence Sync MODULE] No RegionSyncModule config section found. Shutting down.");
                return;
            }
            else if (!syncConfig.GetBoolean("Enabled", false))
            {
                m_log.Warn("[Scene Persistence Sync MODULE] RegionSyncModule is not enabled. Shutting down.");
                return;
            }

            string actorType = syncConfig.GetString("DSGActorType", "").ToLower();
            if (!actorType.Equals("scene_persistence"))
            {
                m_log.Warn("[Scene Persistence Sync MODULE]: not configured as Scene Persistence Actor. Shut down.");
                return;
            }

        }

        //Called after Initialise()
        public void AddRegion(Scene scene)
        {
            if (!m_active)
                return;

            //connect with scene
            m_scene = scene;

            //register the module with SceneGraph. If needed, SceneGraph checks the module's ActorType to know what type of module it is.
            m_scene.RegisterModuleInterface<IDSGActorSyncModule>(this);

            // Setup the command line interface
            //m_scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
            //InstallInterfaces();
        }

        //Called after AddRegion() has been called for all region modules of the scene
        public void RegionLoaded(Scene scene)
        {
            if (m_scene.RegionSyncModule != null)
            {
                m_scene.RegionSyncModule.DSGActorType = m_actorType;
            }
            else
            {
                m_log.Warn("RegionSyncModule is not initiated!!");
            }
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close()
        {
            m_scene = null;
        }

        public string Name
        {
            get { return "ScenePersistenceSyncModule"; }
        }



        #endregion //IRegionModule

        #region IDSGActorSyncModule members and functions

        private DSGActorTypes m_actorType = DSGActorTypes.ScenePersistence;
        public DSGActorTypes ActorType
        {
            get { return m_actorType; }
        }

        #endregion //INonSharedRegionModule 

        #region ScenePersistenceSyncModule memebers and functions
        private ILog m_log;
        private bool m_active = false;
        public bool Active
        {
            get { return m_active; }
        }

        private Scene m_scene;


        #endregion //ScenePersistenceSyncModule
    }



}
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
using Mono.Addins;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AttachmentsModule")]
    public class ScriptEngineSyncModule : INonSharedRegionModule, IDSGActorSyncModule
    {
        #region INonSharedRegionModule

        public void Initialise(IConfigSource config)
        {
            m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            IConfig syncConfig = config.Configs["RegionSyncModule"];
            m_active = false;
            if (syncConfig == null)
            {
                m_log.Warn(LogHeader + " No RegionSyncModule config section found. Shutting down.");
                return;
            }
            else if (!syncConfig.GetBoolean("Enabled", false))
            {
                m_log.Warn(LogHeader + " RegionSyncModule is not enabled. Shutting down.");
                return;
            }

            string actorType = syncConfig.GetString("DSGActorType", "").ToLower();
            if (!actorType.Equals("script_engine"))
            {
                m_log.Warn(LogHeader + ": not configured as Scene Persistence Actor. Shut down.");
                return;
            }

            m_actorID = syncConfig.GetString("ActorID", "");
            if (m_actorID.Equals(""))
            {
                m_log.Warn(LogHeader + ": ActorID not specified in config file. Shutting down.");
                return;
            }

            m_active = true;

            LogHeader += "-" + m_actorID;
            m_log.Warn(LogHeader + " Initialised");

        }

        //Called after Initialise()
        public void AddRegion(Scene scene)
        {
            if (!m_active)
                return;
            m_log.Warn(LogHeader + " AddRegion() called");
            //connect with scene
            m_scene = scene;

            //register the module with SceneGraph. If needed, SceneGraph checks the module's ActorType to know what type of module it is.
            m_scene.RegisterModuleInterface<IDSGActorSyncModule>(this);

            // Setup the command line interface
            //m_scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
            //InstallInterfaces();

            //Register for the OnPostSceneCreation event
            //m_scene.EventManager.OnPostSceneCreation += OnPostSceneCreation;

            //Register for Scene/SceneGraph events
            //m_scene.SceneGraph.OnObjectCreate += new ObjectCreateDelegate(ScriptEngine_OnObjectCreate);
            m_scene.SceneGraph.OnObjectCreateBySync += new ObjectCreateBySyncDelegate(ScriptEngine_OnObjectCreateBySync);
            m_scene.EventManager.OnSymmetricSyncStop += ScriptEngine_OnSymmetricSyncStop;

            //for local OnUpdateScript, we'll handle it the same way as a remove OnUpdateScript. 
            //RegionSyncModule will capture a locally initiated OnUpdateScript event and publish it to other actors.
            m_scene.EventManager.OnNewScript += ScriptEngine_OnNewScript;
            m_scene.EventManager.OnUpdateScript += ScriptEngine_OnUpdateScript; 
            //m_scene.EventManager.OnUpdateScriptBySync += ScriptEngine_OnUpdateScript;

            LogHeader += "-" + m_actorID + "-" + m_scene.RegionInfo.RegionName;
        }

        //Called after AddRegion() has been called for all region modules of the scene.
        //NOTE::However, at this point, Scene may not have requested all the needed region module interfaces yet.
        public void RegionLoaded(Scene scene)
        {
            if (!m_active)
                return;

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
            get { return "ScriptEngineSyncModule"; }
        }

        #endregion //INonSharedRegionModule

        #region IDSGActorSyncModule members and functions

        private DSGActorTypes m_actorType = DSGActorTypes.ScriptEngine;
        public DSGActorTypes ActorType
        {
            get { return m_actorType; }
        }

        private string m_actorID;
        public string ActorID
        {
            get { return m_actorID; }
        }

        #endregion //IDSGActorSyncModule


        #region ScriptEngineSyncModule memebers and functions
        private ILog m_log;
        private bool m_active = false;
        public bool Active
        {
            get { return m_active; }
        }

        private Scene m_scene;

        private string LogHeader = "[ScriptEngineSyncModule]";

        public void OnPostSceneCreation(Scene createdScene)
        {
            //If this is the local scene the actor is working on, do something
            if (createdScene == m_scene)
            {
            }
        }

        /// <summary>
        /// Script Engine's action upon an object is added to the local scene
        /// </summary>
        private void ScriptEngine_OnObjectCreateBySync(EntityBase entity)
        {
            if (entity is SceneObjectGroup)
            {
                m_log.Warn(LogHeader + ": start script for obj " + entity.UUID);
                SceneObjectGroup sog = (SceneObjectGroup)entity; 
                sog.CreateScriptInstances(0, false, m_scene.DefaultScriptEngine, 0);
                sog.ResumeScripts();
            }
        }

        public void ScriptEngine_OnSymmetricSyncStop()
        {
            //Inform script engine to save script states and stop scripts
            m_scene.EventManager.TriggerScriptEngineSyncStop();
            //remove all objects
            m_scene.DeleteAllSceneObjects();
        }

        public void ScriptEngine_OnNewScript(UUID agentID, SceneObjectPart part, UUID itemID)
        {
            m_log.Debug(LogHeader + " ScriptEngine_OnUpdateScript");

            m_scene.SymSync_OnNewScript(agentID, itemID, part);
        }

        //Assumption, when this function is triggered, the new script asset has already been saved.
        public void ScriptEngine_OnUpdateScript(UUID agentID, UUID itemID, UUID primID, bool isScriptRunning, UUID newAssetID)
        {
            m_log.Debug(LogHeader + " ScriptEngine_OnUpdateScript");
            m_scene.SymSync_OnUpdateScript(agentID, itemID, primID, isScriptRunning, newAssetID);
        }

        #endregion //ScriptEngineSyncModule

    }

}
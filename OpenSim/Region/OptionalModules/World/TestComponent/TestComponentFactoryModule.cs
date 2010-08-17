using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Components;

namespace OpenSim.Region.OptionalModules.World.TestComponent
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class TestComponentFactoryModule : ComponentFactory, ISharedRegionModule 
    {
        private List<Scene> m_scenes = new List<Scene>();

        private static readonly ILog m_log =
                    LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region Overrides of ComponentFactory

        protected override IComponent CreateComponent(Type componentType, IComponentState componentState)
        {
            if(componentType == typeof(TestComponent))
            {
                string tmp;
                if(componentState.TryGet("Hello", out tmp))
                {
                    m_log.Info("[TestComponentFactory] Successfully recovered '" + tmp + "' from a component via serialisation.");
                }
                return new TestComponent(componentState);
            }

            return null;
        }

        #endregion

        #region Implementation of IRegionModuleBase

        public string Name
        {
            get { return "TestComponentFactoryModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return typeof (TestComponentFactoryModule); }
        }

        public void Initialise(IConfigSource source)
        {
            m_log.Info("[TESTCOMPONENT] Loading test factory...");
        }

        public void Close()
        {
            
        }

        public void AddRegion(Scene scene)
        {
            
        }

        public void RemoveRegion(Scene scene)
        {
            
        }

        public void RegionLoaded(Scene scene)
        {
            m_log.Info("[TESTCOMPONENT] Loading test factory for " + scene.RegionInfo.RegionName);
            m_scenes.Add(scene);
        }

        public void PostInitialise()
        {
            foreach (Scene scene in m_scenes)
            {
                m_log.Info("[TESTCOMPONENT] Adding new test component to Scene");
                List<EntityBase> sogs = scene.Entities.GetAllByType<SceneObjectGroup>();
                foreach (EntityBase entityBase in sogs)
                {
                    SceneObjectGroup sog = (SceneObjectGroup) entityBase;
                    m_log.Info("[TESTCOMPONENT] Adding new test component to SOG");
                    foreach (SceneObjectPart part in sog.GetParts())
                    {
                        m_log.Info("[TESTCOMPONENT] Adding new test component to SOP");
                        part.SetComponent(
                            new TestComponent(
                                new ComponentState()
                                )
                            );
                    }
                }
            }

            m_log.Info("[TESTCOMPONENT] Test factory loaded");
        }

        #endregion
    }
}

using System;
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse.StructuredData;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Components;

namespace OpenSim.Region.CoreModules.World.Objects.Components
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "ComponentManagerModule")]
    class ComponentManagerModule : IComponentManagerModule, ISharedRegionModule 
    {
        private static readonly ILog m_log =
                    LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public string Name
        {
            get
            {
                return "ComponentManager";
            }
        }

        public Type ReplaceableInterface
        {
            get { return typeof (IComponentManagerModule); }
        }

        public void Initialise(IConfigSource source)
        {
            
        }

        public void Close()
        {
            
        }

        public void AddRegion(Scene scene)
        {
            scene.RegisterModuleInterface<IComponentManagerModule>(this);
        }

        public void RemoveRegion(Scene scene)
        {

        }

        public void RegionLoaded(Scene scene)
        {
            
        }

        public void PostInitialise()
        {
            
        }

        #region Implementation of IComponentManagerModule

        public void CreateComponent(SceneObjectPart target, string componentType, OSDMap state)
        {
            if (OnCreateComponent != null)
            {
                foreach (OnCreateComponentDelegate h in OnCreateComponent.GetInvocationList())
                {
                    IComponent x = h(componentType, state);
                    if (x != null)
                    {
                        target.SetComponent(x);
                        x.SetParent(target);
                        return;
                    }
                }
            } else
            {
                m_log.Warn("[Components] No component handlers loaded. Are you missing a region module?");
                return;
            }

            m_log.Warn("[Components] Unable to create component " + componentType + ". No ComponentFactory was able to recognize it. Could you be missing a region module?");
        }

        public event OnCreateComponentDelegate OnCreateComponent;

        #endregion
    }
}

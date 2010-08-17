using System;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.Framework.Scenes.Components
{
    public abstract class ComponentFactory
    {
        public void InitComponentHandler(Scene x)
        {
            IComponentManagerModule cmm = x.RequestModuleInterface<IComponentManagerModule>();
            cmm.OnCreateComponent += CreateComponent;
        }

        protected abstract IComponent CreateComponent(string componentType, ComponentState componentState);
    }
}

using System;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Components;

namespace OpenSim.Region.Framework.Interfaces
{
    public delegate IComponent OnCreateComponentDelegate(string componentType, ComponentState componentState);

    public interface IComponentManagerModule
    {
        void CreateComponent(SceneObjectPart part, string componentType, ComponentState state);
        event OnCreateComponentDelegate OnCreateComponent;
    }
}

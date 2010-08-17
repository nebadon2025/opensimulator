using System;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Components;

namespace OpenSim.Region.Framework.Interfaces
{
    public delegate IComponent OnCreateComponentDelegate(Type componentType, IComponentState componentState);

    public interface IComponentManagerModule
    {
        void CreateComponent(SceneObjectPart part, Type componentType, IComponentState state);
        event OnCreateComponentDelegate OnCreateComponent;
    }
}

using System;
using OpenMetaverse.StructuredData;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Components;

namespace OpenSim.Region.Framework.Interfaces
{
    public delegate IComponent OnCreateComponentDelegate(string componentType, OSDMap componentState);

    public interface IComponentManagerModule
    {
        void CreateComponent(SceneObjectPart part, string componentType, OSDMap state);
        event OnCreateComponentDelegate OnCreateComponent;
    }
}

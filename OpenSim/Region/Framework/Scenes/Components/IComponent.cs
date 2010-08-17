using System;

namespace OpenSim.Region.Framework.Scenes.Components
{
    /// <summary>
    /// A component on an object
    /// TODO: Better documentation
    /// </summary>
    public interface IComponent
    {
        /// <summary>
        /// The type of the component, only one of each 'type' can be loaded.
        /// </summary>
        Type BaseType { get; }

        /// <summary>
        /// A representation of the current state of the component, to be deserialised later.
        /// </summary>
        ComponentState State { get; }

        void SetParent(SceneObjectPart part);
    }
}

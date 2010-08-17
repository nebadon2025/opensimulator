using System;
using System.Reflection;
using log4net;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Components;

namespace OpenSim.Region.OptionalModules.World.TestComponent
{
    /// <summary>
    /// Components must be public classes
    /// </summary>
    public class TestComponent : IComponent 
    {
        private int m_theAnswerToTheQuestionOfLifeTheUniverseAndEverything = 42;

        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region Implementation of IComponent

        public TestComponent(ComponentState state)
        {
            m_log.Info("Its alive!");
        }

        public Type BaseType
        {
            get { return typeof (TestComponent); }
        }

        public ComponentState State
        {
            get
            {
                ComponentState x = new ComponentState();
                x.Set("Hello","World");
                x.Set("HitchhikersReference", m_theAnswerToTheQuestionOfLifeTheUniverseAndEverything);
                return x;
            }
        }

        public void SetParent(SceneObjectPart part)
        {
            m_log.Info("My parent's name is: " + part.Name);
        }

        #endregion
    }
}

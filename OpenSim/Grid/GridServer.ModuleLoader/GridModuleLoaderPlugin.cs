using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Grid.Framework;
using log4net;
using System.Reflection;
using OpenSim.Framework.Servers;

namespace OpenSim.Grid.GridServer.ModuleLoader
{
    public class GridModuleLoaderPlugin : IGridPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected List<IGridServiceModule> m_modules;
        protected GridServerBase m_core;

        #region IGridPlugin Members

        public void Initialise(GridServerBase gridServer)
        {
            m_core = gridServer;

            GridModuleLoader<IGridServiceModule> moduleLoader = new GridModuleLoader<IGridServiceModule>();

            m_modules = moduleLoader.PickupModules(".");

            InitializeModules();
            PostInitializeModules();
            RegisterModuleHandlers();
        }

        #endregion

        protected void InitializeModules()
        {
            foreach (IGridServiceModule m in m_modules)
            {
                m_log.InfoFormat("[MODULES]: Initialising Grid Service Module {0}", m.Name);
                m.Initialise(m_core);
            }
        }

        protected void PostInitializeModules()
        {
            foreach (IGridServiceModule m in m_modules)
            {
                //m_log.InfoFormat("[MODULES]: Initialising Grid Service Module {0}", m.Name);
                m.PostInitialise();
            }
        }

        protected void RegisterModuleHandlers()
        {
            BaseHttpServer httpServer;
            if (m_core.TryGet<BaseHttpServer>(out httpServer))
            {
                foreach (IGridServiceModule m in m_modules)
                {
                    //m_log.InfoFormat("[MODULES]: Initialising Grid Service Module {0}", m.Name);
                    m.RegisterHandlers(httpServer);
                }
            }
        }

        #region IPlugin Members

        public string Version
        {
            get { return "0.1"; }
        }

        public string Name
        {
            get { return "GridModuleLoaderPlugin"; }
        }

        public void Initialise()
        {          
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
        }

        #endregion
    }
}

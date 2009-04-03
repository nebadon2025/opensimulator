using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using log4net;

namespace OpenSim.Grid.GridServer
{
    public class GridModuleLoader<T>
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public Dictionary<string, Assembly> LoadedAssemblys = new Dictionary<string, Assembly>();

        public List<T> PickupModules(string path)
        {
            DirectoryInfo dir = new DirectoryInfo(path);
            List<T> modules = new List<T>();

            foreach (FileInfo fileInfo in dir.GetFiles("*.dll"))
            {
                List<T> foundModules = this.LoadModules(fileInfo.FullName);
                modules.AddRange(foundModules);
            }
            return modules;
        }

        public List<T> LoadModules(string dllName)
        {
            List<T> modules = new List<T>();

            Assembly pluginAssembly;
            if (!LoadedAssemblys.TryGetValue(dllName, out pluginAssembly))
            {
                try
                {
                    pluginAssembly = Assembly.LoadFrom(dllName);
                    LoadedAssemblys.Add(dllName, pluginAssembly);
                }
                catch (BadImageFormatException)
                {
                    //m_log.InfoFormat("[MODULES]: The file [{0}] is not a module assembly.", e.FileName);
                }
            }

            if (pluginAssembly != null)
            {
                try
                {
                    foreach (Type pluginType in pluginAssembly.GetTypes())
                    {
                        if (pluginType.IsPublic)
                        {
                            if (!pluginType.IsAbstract)
                            {
                                if (pluginType.GetInterface(typeof(T).Name) != null)
                                {
                                    modules.Add((T)Activator.CreateInstance(pluginType));
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[MODULES]: Could not load types for [{0}].  Exception {1}", pluginAssembly.FullName, e);

                    // justincc: Right now this is fatal to really get the user's attention
                    throw e;
                }
            }

            return modules;
        }
    }
}

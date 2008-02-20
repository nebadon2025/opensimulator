using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Scenes.Scripting;
using OpenSim.Region.Environment.Interfaces;
using libsecondlife;
using Nini.Config;

namespace OpenSim.Region.RexScriptModule
{
    public class RexScriptEngine : IRegionModule
    {
        public OpenSim.Region.RexScriptModule.RexScriptInterface mCSharp;

        internal Scene World; 
        internal RexEventManager m_EventManager;                 

        private LogBase m_log;
        private IronPython.Hosting.PythonEngine mPython = null;
        private bool m_PythonEnabled;
        private bool m_EngineStarted;

        public RexScriptEngine()
        {
        }

        public LogBase Log
        {
            get { return m_log; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        public bool IsPythonEnabled
        {
            get { return m_PythonEnabled; }
        }

        public bool IsEngineStarted
        {
            get { return m_EngineStarted; }
        }


        public void Initialise(Scene scene, IConfigSource config)
        {
            try
            {
                m_PythonEnabled = config.Configs["Startup"].GetBoolean("python_enabled", true);
            }
            catch (Exception)
            {
                m_PythonEnabled = true;
            }

            InitializeEngine(scene, MainLog.Instance);
        }

        public void PostInitialise()
        {

        }

        public void CloseDown()
        {
        }

        public string GetName()
        {
            return "RexPythonScriptModule";
        }

        public void InitializeEngine(Scene Sceneworld, LogBase logger)
        {
            World = Sceneworld;
            m_log = logger;

            if (m_PythonEnabled)
            {
                Log.Verbose("RexScriptEngine", "Rex PythonScriptEngine initializing");

                RexScriptAccess.MyScriptAccess = new RexPythonScriptAccessImpl(this);
                m_EventManager = new RexEventManager(this);
                mCSharp = new RexScriptInterface(null, null, 0, LLUUID.Zero, this);
                StartPythonEngine();
            }
            else
                Log.Verbose("RexScriptEngine", "Rex PythonScriptEngine disabled");
       }

        public void StartPythonEngine()
        {
            try
            {
                Log.Verbose("RexScriptEngine","IronPython init");
                m_EngineStarted = false;
                bool bNewEngine = true;

                if (mPython == null)
                {
                    IronPython.Hosting.EngineOptions engineOptions = new IronPython.Hosting.EngineOptions();
                    engineOptions.ClrDebuggingEnabled = false;
                    IronPython.Compiler.Options.Verbose = false;
                    IronPython.Compiler.Options.GenerateModulesAsSnippets = true;
                    mPython = new IronPython.Hosting.PythonEngine(engineOptions);
                }
                else
                    bNewEngine = false;

                // Add script folder paths to python path
                mPython.AddToPath(AppDomain.CurrentDomain.BaseDirectory);

                string rexdlldir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScriptEngines");
                mPython.AddToPath(rexdlldir);

                string PytProjectPath = Path.Combine(rexdlldir, "PythonScript");
                mPython.AddToPath(PytProjectPath);

                DirectoryInfo TempDirInfo = new DirectoryInfo(@PytProjectPath);
                DirectoryInfo[] dirs = TempDirInfo.GetDirectories("*.*");
                string TempPath = "";
                foreach (DirectoryInfo dir in dirs)
                {
                    TempPath = Path.Combine(PytProjectPath, dir.Name);
                    mPython.AddToPath(TempPath);
                }
                String PytLibPath = Path.Combine(rexdlldir, "Lib");
                mPython.AddToPath(PytLibPath);

                // Import Core and init
                mPython.Execute("from RXCore import *"); 
                if (!bNewEngine)
                {
                    ExecutePythonStartCommand("reload(rxlslobject)");
                    ExecutePythonStartCommand("reload(rxactor)");
                    ExecutePythonStartCommand("reload(rxavatar)");
                    ExecutePythonStartCommand("reload(rxevent)");
                    ExecutePythonStartCommand("reload(rxeventmanager)");
                    ExecutePythonStartCommand("reload(rxworld)");
                }
                mPython.Globals.Add("objCSharp", mCSharp);
                mPython.ExecuteFile(PytProjectPath + "/RXCore/rxmain.py"); // tucofixme, possible error with path???

                // Import other packages
                foreach (DirectoryInfo dir in dirs)
                {
                    if (dir.Name.IndexOf(".") != -1)
                        continue;
                    else if (dir.Name.Length >= 6 && dir.Name.Substring(0, 6).ToLower() == "rxcore")
                        continue;
                    else
                    {
                        mPython.Execute("from " + dir.Name + " import *");
                        if (!bNewEngine)
                        {
                            FileInfo[] files = dir.GetFiles("*.py");
                            foreach (FileInfo file in files)
                            {
                                if (file.Name.ToLower() == "__init__.py")
                                    continue;
                                else
                                    ExecutePythonStartCommand("reload(" + file.Name.Substring(0, file.Name.Length - 3) + ")");
                            }
                        }
                    }
                }

                // Create objects
                string PythonClassName = "";
                string PythonTag = "";
                string PyText = "";
                int tagindex = 0;

                List<EntityBase> EntityList = World.GetEntities();
                foreach (EntityBase ent in EntityList)
                {
                    if (ent is SceneObjectGroup)
                    {
                        PythonClassName = "rxactor.Actor";
                        PythonTag = "";

                        SceneObjectPart part = ((SceneObjectGroup)ent).GetChildPart(((SceneObjectGroup)ent).UUID);
                        if (part != null)
                        {
                            part.GetRexParameters();

                            // First check m_RexClassName, then description of object
                            if (part.m_RexClassName.Length > 0)
                            {
                                tagindex = part.m_RexClassName.IndexOf("?", 0);
                                if (tagindex > -1)
                                {
                                    PythonClassName = part.m_RexClassName.Substring(0, tagindex);
                                    PythonTag = part.m_RexClassName.Substring(tagindex + 1);
                                }
                                else
                                    PythonClassName = part.m_RexClassName;
                            }
                            else if (part.Description.Length > 9 && part.Description.Substring(0, 4).ToLower() == "<py>")
                            {
                                tagindex = part.Description.IndexOf("</py>", 4);
                                if (tagindex > -1)
                                    PyText = part.Description.Substring(4, tagindex - 4);
                                else
                                    continue;

                                tagindex = PyText.IndexOf("?", 0);
                                if (tagindex > -1)
                                {
                                    PythonClassName = PyText.Substring(0, tagindex);
                                    PythonTag = PyText.Substring(tagindex + 1);
                                }
                                else
                                    PythonClassName = PyText;
                            }
                        }
                        CreateActorToPython(ent.LocalId.ToString(), PythonClassName, PythonTag);
                    }
                }
                
 
                // Create avatars
                string PParams = "";              
                List<ScenePresence> ScenePresencesList = World.GetScenePresences();
                foreach (ScenePresence avatar in ScenePresencesList)
                {
                    PParams = "\"add_presence\"," + avatar.LocalId.ToString() + "," + "\"" + avatar.m_uuid.ToString() + "\"";
                    ExecutePythonStartCommand("CreateEventWithName(" + PParams + ")");
                }
             
                // start script thread
                m_EngineStarted = true;
                mPython.Execute("StartMainThread()");
            }
            catch (Exception e)
            {
                Log.Verbose("RexScriptEngine", "Python init exception: " + e.ToString());
            }
        }


        public void RestartPythonEngine()
        {
            if (!m_PythonEnabled)
            {
                Log.Verbose("RexScriptEngine", "Rex PythonScriptEngine disabled");
                return;
            }

            try
            {
                Log.Verbose("RexScriptEngine", "Restart");
                // ShutDownPythonEngine();
                mPython.Execute("StopMainThread()");
                GC.Collect();
                GC.WaitForPendingFinalizers(); // tucofixme, blocking???
                StartPythonEngine();
            }
            catch (Exception e)
            {
                Log.Verbose("RexScriptEngine", "restart exception: " + e.ToString());
            }

        }


        public void ExecutePythonCommand(string vCommand)
        {
            if (!m_EngineStarted)
                return;
            try
            {
                mPython.Execute(vCommand);
            }
            catch (Exception e)
            {
                Log.Verbose("RexScriptEngine", "ExecutePythonCommand exception " + e.ToString());
            }
        }

        public void ExecutePythonStartCommand(string vCommand)
        {
            try
            {
                mPython.Execute(vCommand);
            }
            catch (Exception e)
            {
                Log.Verbose("RexScriptEngine", "ExecutePythonStartCommand exception " + e.ToString());
            }
        }


        public object EvalutePythonCommand(string vCommand)
        {
            try
            {
                return mPython.Evaluate(vCommand);
            }
            catch (Exception e)
            {
                Log.Verbose("RexScriptEngine", "ExecutePythonStartCommand exception " + e.ToString());
            }
            return null;
        }





        public void CreateActorToPython(string vLocalId, string vPythonClassName, string vPythonTag)
        {
            try
            {
                mPython.Execute("CreateActorOfClass(" + vLocalId + "," + vPythonClassName + ",\"" + vPythonTag + "\")");
            }
            catch (Exception)
            {
                try
                {
                    if (vPythonClassName.Length > 0)
                        Log.Verbose("RexScriptEngine", "Could not load class:" + vPythonClassName);
                    mPython.Execute("CreateActorOfClass(" + vLocalId + ",rxactor.Actor,\"\")");
                }
                catch (Exception)
                {
                }
            }
        }

        public void Shutdown()
        {
            ShutDownPythonEngine();
            // We are shutting down
        }

        public void Close()
        {
            ShutDownPythonEngine();
            // We are shutting down
        }

        private void ShutDownPythonEngine()
        {
            if (mPython != null)
            {
                mPython.Execute("StopMainThread()");
                mPython.Shutdown();
                mPython.Dispose();
                mPython = null;
            }
        }

        public string Name
        {
            get { return "RexPythonScriptModule"; }
        }
    }
}

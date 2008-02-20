using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.RexScriptModule
{
    [Serializable]
    class RexEventManager
    {
        private RexScriptEngine myScriptEngine;

        // tuco fixme, is there a better way to do this search???
        private EntityBase GetEntityBase(uint vId)
        {
            SceneObjectPart part = myScriptEngine.World.GetSceneObjectPart(vId);
            if (part != null && (EntityBase)(part.ParentGroup) != null)
                return (EntityBase)(part.ParentGroup);
            else              
                return null;
        }

        public RexEventManager(RexScriptEngine vScriptEngine)
        {   
            myScriptEngine = vScriptEngine;
            myScriptEngine.Log.Verbose("RexScriptEngine", "Hooking up to server events");
            myScriptEngine.World.EventManager.OnObjectGrab += touch_start;
            // myScriptEngine.World.EventManager.OnRezScript += OnRezScript;
            // myScriptEngine.World.EventManager.OnRemoveScript += OnRemoveScript;
            // myScriptEngine.World.EventManager.OnFrame += OnFrame;
            // myScriptEngine.World.EventManager.OnNewClient += OnNewClient; 
            myScriptEngine.World.EventManager.OnNewPresence += OnNewPresence;
            myScriptEngine.World.EventManager.OnRemovePresence += OnRemovePresence;
            myScriptEngine.World.EventManager.OnShutdown += OnShutDown;
            myScriptEngine.World.EventManager.OnAddEntity += OnAddEntity;
            myScriptEngine.World.EventManager.OnRemoveEntity += OnRemoveEntity;
            myScriptEngine.World.EventManager.OnPythonScriptCommand += OnPythonScriptCommand;
            myScriptEngine.World.EventManager.OnPythonClassChange += OnPythonClassChange;
            myScriptEngine.World.EventManager.OnRexClientScriptCommand += OnRexClientScriptCommand;
            myScriptEngine.World.EventManager.OnPrimVolumeCollision += OnPrimVolumeCollision;            
        }

        public void touch_start(uint localID, LLVector3 offsetPos, IClientAPI remoteClient)
        {
            string EventParams = "\"touch_start\"," + localID.ToString() + "," + "\"" + remoteClient.AgentId.ToString() + "\"";
            myScriptEngine.ExecutePythonCommand("CreateEventWithName(" + EventParams + ")");
        }

        /* 
        public void OnRezScript(uint localID, LLUUID itemID, string script)
        {

        }
        public void OnRemoveScript(uint localID, LLUUID itemID)
        {         

        }

        public void OnFrame()
        {

        }

        public void OnNewClient(IClientAPI vClient)
        {
            string EventParams = "\"new_client\"," + "\"" + vClient.AgentId.ToString() + "\"";
            myScriptEngine.ExecutePythonCommand("CreateEventWithName(" + EventParams + ")");
        }
        */ 

        public void OnNewPresence(ScenePresence vPresence)
        {
            string EventParams = "\"add_presence\"," + vPresence.LocalId.ToString() + "," + "\"" + vPresence.m_uuid.ToString() + "\"";
            myScriptEngine.ExecutePythonCommand("CreateEventWithName(" + EventParams + ")");
        }

        public void OnRemovePresence(LLUUID uuid)
        {
            string EventParams = "\"remove_presence\"," + "\"" + uuid.ToString() + "\"";
            myScriptEngine.ExecutePythonCommand("CreateEventWithName(" + EventParams + ")");
        }

        public void OnShutDown()
        {
            Console.WriteLine("REX OnShutDown");
        }

        public void OnAddEntity(uint localID)
        {
            string PythonClassName = "rxactor.Actor";
            string PythonTag = "";

            SceneObjectGroup tempobj = (SceneObjectGroup)GetEntityBase(localID);
            if (tempobj != null && tempobj.RootPart != null && tempobj.RootPart.m_RexClassName.Length > 0)
                PythonClassName = tempobj.RootPart.m_RexClassName;

            // Create the actor directly without using an event.
            myScriptEngine.CreateActorToPython(localID.ToString(), PythonClassName, PythonTag);
        }

        public void OnRemoveEntity(uint localID)
        {
            string EventParams = "\"remove_entity\"," + "\"" + localID.ToString() + "\"";
            myScriptEngine.ExecutePythonCommand("CreateEventWithName(" + EventParams + ")");
        }

        public void OnPythonScriptCommand(string vCommand)
        {
            if (vCommand.ToLower() == "restart")
                myScriptEngine.RestartPythonEngine();
            else
                Console.WriteLine("Unknown PythonScriptEngine command:"+vCommand);
        }

        public void OnPythonClassChange(uint localID)
        {
            string PythonClassName = "";
            string PythonTag = "";

            SceneObjectGroup tempobj = (SceneObjectGroup)GetEntityBase(localID);
            if (tempobj != null && tempobj.RootPart != null && tempobj.RootPart.m_RexClassName.Length > 0)
                PythonClassName = tempobj.RootPart.m_RexClassName;

            if (myScriptEngine.IsEngineStarted)
                myScriptEngine.CreateActorToPython(localID.ToString(), PythonClassName, PythonTag);
        }

        public void OnRexClientScriptCommand(ScenePresence avatar, List<string> vCommands)
        {
            string Paramlist = "";
            foreach (string s in vCommands)
                Paramlist = Paramlist + "," + "\"" + s + "\"";
        
            string EventParams = "\"client_event\",\"" + avatar.m_uuid.ToString() + "\"" + Paramlist;
            myScriptEngine.ExecutePythonCommand("CreateEventWithName(" + EventParams + ")");
        }

        public void OnPrimVolumeCollision(uint ownID, uint colliderID)
        {
            string EventParams = "\"primvol_col\"," + ownID.ToString() + "," + "\"" + colliderID.ToString() + "\"";
            myScriptEngine.ExecutePythonCommand("CreateEventWithName(" + EventParams + ")");
        }




        // TODO: Replace placeholders below
        //  These needs to be hooked up to OpenSim during init of this class.
        // When queued in EventQueueManager they need to be LSL compatible (name and params)

        //public void state_entry() { } // 
        public void state_exit() { }
        //public void touch_start() { }
        public void touch() { }
        public void touch_end() { }
        public void collision_start() { }
        public void collision() { }
        public void collision_end() { }
        public void land_collision_start() { }
        public void land_collision() { }
        public void land_collision_end() { }
        public void timer() { }
        public void listen() { }
        public void on_rez() { }
        public void sensor() { }
        public void no_sensor() { }
        public void control() { }
        public void money() { }
        public void email() { }
        public void at_target() { }
        public void not_at_target() { }
        public void at_rot_target() { }
        public void not_at_rot_target() { }
        public void run_time_permissions() { }
        public void changed() { }
        public void attach() { }
        public void dataserver() { }
        public void link_message() { }
        public void moving_start() { }
        public void moving_end() { }
        public void object_rez() { }
        public void remote_data() { }
        public void http_response() { }

    }


}

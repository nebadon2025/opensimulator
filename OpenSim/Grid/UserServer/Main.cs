/*
* Copyright (c) Contributors, http://opensimulator.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using Nwc.XmlRpc;
using OpenSim.Framework.Statistics;

namespace OpenSim.Grid.UserServer
{
    /// <summary>
    /// </summary>
    public class OpenUser_Main : BaseOpenSimServer, conscmd_callback
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private UserConfig Cfg;
        
        public UserManager m_userManager;
        public UserLoginService m_loginService;
        public MessageServersConnector m_messagesService;        

        private LLUUID m_lastCreatedUser = LLUUID.Random();

        private Boolean m_rexMode;

        [STAThread]
        public static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();

            m_log.Info("Launching UserServer...");

            OpenUser_Main userserver = new OpenUser_Main();

            userserver.Startup();
            userserver.Work();
        }

        private OpenUser_Main()
        {
            m_console = new ConsoleBase("OpenUser", this);
            MainConsole.Instance = m_console;
        }

        private void Work()
        {
            m_console.Notice("Enter help for a list of commands\n");

            while (true)
            {
                m_console.Prompt();
            }
        }

        public void Startup()
        {
            Cfg = new UserConfig("USER SERVER", (Path.Combine(Util.configDir(), "UserServer_Config.xml")));
            
            StatsManager.StartCollectingUserStats();

            m_log.Info("[REGION]: Establishing data connection");
            m_userManager = new UserManager();            
            m_userManager._config = Cfg;
            m_userManager.AddPlugin(Cfg.DatabaseProvider);            

            m_loginService = new UserLoginService(
                 m_userManager, new LibraryRootFolder(), Cfg, Cfg.DefaultStartupMsg);
            m_rexMode = Cfg.RexMode;

            m_messagesService = new MessageServersConnector();

            m_loginService.OnUserLoggedInAtLocation += NotifyMessageServersUserLoggedInToLocation;

            m_log.Info("[REGION]: Starting HTTP process");
            BaseHttpServer httpServer = new BaseHttpServer(Cfg.HttpPort);

            httpServer.AddXmlRPCHandler("login_to_simulator", m_loginService.XmlRpcLoginMethod);

            httpServer.AddHTTPHandler("login", m_loginService.ProcessHTMLLogin);
            
            httpServer.SetLLSDHandler(m_loginService.LLSDLoginMethod);

            httpServer.AddXmlRPCHandler("get_user_by_name", m_userManager.XmlRPCGetUserMethodName);
            httpServer.AddXmlRPCHandler("get_user_by_uuid", m_userManager.XmlRPCGetUserMethodUUID);
            httpServer.AddXmlRPCHandler("get_avatar_picker_avatar", m_userManager.XmlRPCGetAvatarPickerAvatar);
            httpServer.AddXmlRPCHandler("add_new_user_friend", m_userManager.XmlRpcResponseXmlRPCAddUserFriend);
            httpServer.AddXmlRPCHandler("remove_user_friend", m_userManager.XmlRpcResponseXmlRPCRemoveUserFriend);
            httpServer.AddXmlRPCHandler("update_user_friend_perms", m_userManager.XmlRpcResponseXmlRPCUpdateUserFriendPerms);
            httpServer.AddXmlRPCHandler("get_user_friend_list", m_userManager.XmlRpcResponseXmlRPCGetUserFriendList);
            httpServer.AddXmlRPCHandler("logout_of_simulator", m_userManager.XmlRPCLogOffUserMethodUUID);
           
            // Message Server ---> User Server
            httpServer.AddXmlRPCHandler("register_messageserver", m_messagesService.XmlRPCRegisterMessageServer);
            httpServer.AddXmlRPCHandler("agent_change_region", m_messagesService.XmlRPCUserMovedtoRegion);
            httpServer.AddXmlRPCHandler("deregister_messageserver", m_messagesService.XmlRPCDeRegisterMessageServer);

                // Message Server ---> User Server
                httpServer.AddXmlRPCHandler("register_messageserver", m_messagesService.XmlRPCRegisterMessageServer);
                httpServer.AddXmlRPCHandler("agent_change_region", m_messagesService.XmlRPCUserMovedtoRegion);
                httpServer.AddXmlRPCHandler("deregister_messageserver", m_messagesService.XmlRPCDeRegisterMessageServer);
            }
            else {
                httpServer.AddXmlRPCHandler("get_user_by_name", new RexRemoteHandler("get_user_by_name").rexRemoteXmlRPCHandler);
                httpServer.AddXmlRPCHandler("get_user_by_uuid", new RexRemoteHandler("get_user_by_uuid").rexRemoteXmlRPCHandler);
                httpServer.AddXmlRPCHandler("get_avatar_picker_avatar", new RexRemoteHandler("get_avatar_picker_avatar").rexRemoteXmlRPCHandler);
                httpServer.AddXmlRPCHandler("add_new_user_friend", new RexRemoteHandler("add_new_user_friend").rexRemoteXmlRPCHandler);
                httpServer.AddXmlRPCHandler("remove_user_friend", new RexRemoteHandler("remove_user_friend").rexRemoteXmlRPCHandler);
                httpServer.AddXmlRPCHandler("update_user_friend_perms", new RexRemoteHandler("update_user_friend_perms").rexRemoteXmlRPCHandler);
                httpServer.AddXmlRPCHandler("get_user_friend_list", new RexRemoteHandler("get_user_friend_list").rexRemoteXmlRPCHandler);

                // Message Server ---> User Server
                httpServer.AddXmlRPCHandler("register_messageserver", new RexRemoteHandler("register_messageserver").rexRemoteXmlRPCHandler);
                httpServer.AddXmlRPCHandler("agent_change_region", new RexRemoteHandler("agent_change_region").rexRemoteXmlRPCHandler);
                httpServer.AddXmlRPCHandler("deregister_messageserver", new RexRemoteHandler("deregister_messageserver").rexRemoteXmlRPCHandler);
            }

            httpServer.AddStreamHandler(
                new RestStreamHandler("DELETE", "/usersessions/", m_userManager.RestDeleteUserSessionMethod));

            httpServer.Start();
            m_log.Info("[SERVER]: Userserver 0.5 - Startup complete");
        }

        public void do_create(string what)
        {
            switch (what)
            {
                case "user":
                    string tempfirstname;
                    string templastname;
                    string tempMD5Passwd;
                    uint regX = 1000;
                    uint regY = 1000;

                    tempfirstname = m_console.CmdPrompt("First name");
                    templastname = m_console.CmdPrompt("Last name");
                    //tempMD5Passwd = m_console.PasswdPrompt("Password");
                    tempMD5Passwd = m_console.CmdPrompt("Password");
                    regX = Convert.ToUInt32(m_console.CmdPrompt("Start Region X"));
                    regY = Convert.ToUInt32(m_console.CmdPrompt("Start Region Y"));

                    if (null != m_userManager.GetUserProfile(tempfirstname, templastname))
                    {
                        m_log.ErrorFormat("[USERS]: A user with the name {0} {1} already exists!", tempfirstname, templastname);
                        break;
                    }
                    
                    tempMD5Passwd = Util.Md5Hash(Util.Md5Hash(tempMD5Passwd) + ":" + String.Empty);

                    LLUUID userID = new LLUUID();
                    try
                    {
                        userID =
                            m_userManager.AddUserProfile(tempfirstname, templastname, tempMD5Passwd, regX, regY);
                    } catch (Exception ex)
                    {
                        m_log.ErrorFormat("[USERS]: Error creating user: {0}", ex.ToString());
                    }

                    try
                    {
                        RestObjectPoster.BeginPostObject<Guid>(m_userManager._config.InventoryUrl + "CreateInventory/",
                                                               userID.UUID);
                    }
                    catch (Exception ex)
                    {
                        m_log.ErrorFormat("[USERS]: Error creating inventory for user: {0}", ex.ToString());
                    }
                    m_lastCreatedUser = userID;
                    break;
            }
        }

        public override void RunCmd(string cmd, string[] cmdparams)
        {
            base.RunCmd(cmd, cmdparams);
            
            switch (cmd)
            {
                case "help":
                    m_console.Notice("create user - create a new user");
                    m_console.Notice("stats - statistical information for this server");                    
                    m_console.Notice("shutdown - shutdown the grid (USE CAUTION!)");
                    break;

                case "create":
                    do_create(cmdparams[0]);
                    break;

                case "shutdown":
                    m_loginService.OnUserLoggedInAtLocation -= NotifyMessageServersUserLoggedInToLocation;
                    m_console.Close();
                    Environment.Exit(0);
                    break;
                    
                case "stats":
                    m_console.Notice(StatsManager.UserStats.Report());
                    break;                    

                case "test-inventory":
                    //  RestObjectPosterResponse<List<InventoryFolderBase>> requester = new RestObjectPosterResponse<List<InventoryFolderBase>>();
                    // requester.ReturnResponseVal = TestResponse;
                    // requester.BeginPostObject<LLUUID>(m_userManager._config.InventoryUrl + "RootFolders/", m_lastCreatedUser);
                    SynchronousRestObjectPoster.BeginPostObject<LLUUID, List<InventoryFolderBase>>("POST",
                                                                                                   m_userManager.
                                                                                                       _config.
                                                                                                       InventoryUrl +
                                                                                                   "RootFolders/",
                                                                                                   m_lastCreatedUser);
                    break;
            }
        }

        public void TestResponse(List<InventoryFolderBase> resp)
        {
            m_console.Notice("response got");
        }

        public void NotifyMessageServersUserLoggedInToLocation(LLUUID agentID, LLUUID sessionID, LLUUID RegionID, ulong regionhandle, LLVector3 Position)
        {
            m_messagesService.TellMessageServersAboutUser(agentID, sessionID, RegionID, regionhandle, Position);
        }

        /*private void ConfigDB(IGenericConfig configData)
        {
            try
            {
                string attri = String.Empty;
                attri = configData.GetAttribute("DataBaseProvider");
                if (attri == String.Empty)
                {
                    StorageDll = "OpenSim.Framework.Data.DB4o.dll";
                    configData.SetAttribute("DataBaseProvider", "OpenSim.Framework.Data.DB4o.dll");
                }
                else
                {
                    StorageDll = attri;
                }
                configData.Commit();
            }
            catch
            {

            }
        }*/
    }

    /// <summary>
    /// for forwarding some requests to authentication server
    /// </summary>
    class RexRemoteHandler
    {
        private string methodName;

        public RexRemoteHandler(String method)
        {
            methodName = method;
        }

        public XmlRpcResponse rexRemoteXmlRPCHandler(XmlRpcRequest request)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            string authAddr;
            if (requestData.Contains("AuthenticationAddress") && requestData["AuthenticationAddress"] != null)
            {
                authAddr = (string)requestData["AuthenticationAddress"];
            }
            else
            {
                return CreateErrorResponse("unknown_authentication",
                                           "Request did not contain authentication address");
            }

            ArrayList SendParams = new ArrayList();
            foreach (Object obj in request.Params)
            {
                SendParams.Add(obj);
            }
            XmlRpcRequest req = new XmlRpcRequest(methodName, SendParams);
            if(!authAddr.StartsWith("http://"))
                authAddr = "http://"+ authAddr;
            XmlRpcResponse res = req.Send(authAddr, 30000);
            return res;
        }

        public XmlRpcResponse CreateErrorResponse(string type, string desc)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            responseData["error_type"] = type;
            responseData["error_desc"] = desc;

            response.Value = responseData;
            return response;
        }
    }

}

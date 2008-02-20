using System;
using System.Collections;
using System.Threading;
using libsecondlife.StructuredData;
using libsecondlife;
using Nwc.XmlRpc;
using OpenSim.Framework.Console;



namespace OpenSim.Framework.UserManagement
{
    public class RexLoginHandler
    {
        private LoginService m_ls;
        protected UserManagerBase m_userManager = null;

        public RexLoginHandler(LoginService loginservice, UserManagerBase userManager)
        {
            m_ls = loginservice;
            m_userManager = userManager;
        }

        public LLSD LLSDLoginMethod(LLSD request)
        {
                string clientVersion = "not set"; //rex

                LoginResponse logResponse = new LoginResponse();

                string account = "";
                string sessionhash = "";
                string AuthenticationAddress = "";

                if (request.Type == LLSDType.Map)
                {
                    LLSDMap map = (LLSDMap)request;

                    if (map.ContainsKey("account") && map.ContainsKey("sessionhash") &&
                        map.ContainsKey("AuthenticationAddress"))
                    {
                        account = map["account"].AsString();
                        sessionhash = map["sessionhash"].AsString();
                        AuthenticationAddress = map["AuthenticationAddress"].AsString();

                        if (map.ContainsKey("version"))
                        {
                            clientVersion = map["version"].AsString();
                        }
                    }
                    else {
                        return logResponse.CreateLoginFailedResponseLLSD();
                    }
                    return (LLSD)CommonLoginProcess(account, sessionhash, AuthenticationAddress, clientVersion, true);
                }
                return logResponse.CreateLoginFailedResponseLLSD();
        }

        public XmlRpcResponse XmlRpcLoginMethod(XmlRpcRequest request)
        {
            MainLog.Instance.Verbose("LOGIN", "Attempting login to rexmode sim now...");
            LoginResponse logResponse = new LoginResponse();

            Hashtable requestData = (Hashtable)request.Params[0];

            bool GoodXML = (//requestData.Contains("first") && requestData.Contains("last") &&
                            requestData.Contains("account") && requestData.Contains("sessionhash") &&
                            requestData.Contains("AuthenticationAddress"));
            
            

            

            #region GoodXML // authentication and getting UserProfileData
            if (GoodXML)
            {
                string account = (string)requestData["account"];
                string sessionhash = (string)requestData["sessionhash"];
                string AuthenticationAddress = (string)requestData["AuthenticationAddress"];
                string clientVersion = "not set";
                if (requestData.ContainsKey("version"))
                {
                    clientVersion = (string)requestData["version"];
                }

                return (XmlRpcResponse) CommonLoginProcess(account, sessionhash, AuthenticationAddress,
                                                           clientVersion, false);
            } 
            else {
                return logResponse.CreateGridErrorResponse();
            }
        }


        private object CommonLoginProcess(string account, string sessionhash, string AuthenticationAddress,
                                          string clientVersion, bool useLLSD)
        {
            string asAddress;
            UserProfileData userProfile;
            bool GoodLogin = false;
            XmlRpcResponse response = new XmlRpcResponse();

            LoginResponse logResponse = new LoginResponse();

            // in rex mode first thing to do is authenticate
            GoodLogin = AuthenticateUser(account, ref sessionhash, AuthenticationAddress);

            if (!GoodLogin)
                return logResponse.CreateLoginFailedResponse();

            userProfile = GetTheUser(account, sessionhash, AuthenticationAddress, out asAddress);


            if (userProfile == null)
            {
                if (!useLLSD)
                    return logResponse.CreateLoginFailedResponse();
                else
                    return logResponse.CreateAlreadyLoggedInResponseLLSD();
            }

            // Set at least here if not filled elsewhere later...
            userProfile.authenticationAddr = AuthenticationAddress;

            #endregion

            #region GoodLogin // Agent storing issues
            if (!GoodLogin)
            {
                return logResponse.CreateLoginFailedResponse();
            }
            else
            {
                // If we already have a session...
                //if (userProfile.currentAgent != null && userProfile.currentAgent.agentOnline)
                //{
                //    userProfile.currentAgent = null;

                //m_userManager.CommitAgent(ref userProfile);// not needed
                // Reject the login
                //    return logResponse.CreateAlreadyLoggedInResponse();
                //}
                // Otherwise...
                // TODO: Actually this is needed at least for now as otherwise crashes to agent being null
                //m_ls.CreateAgent(userProfile, request); // not needed

            #endregion

                #region AllTheRest
                // All the rest in this method goes like in LoginServices method

                try
                {
                    LLUUID agentID = userProfile.UUID;

                    // Inventory Library Section

                    OpenSim.Framework.UserManagement.LoginService.InventoryData inventData = m_ls.CreateInventoryData(agentID);
                    ArrayList AgentInventoryArray = inventData.InventoryArray;

                    Hashtable InventoryRootHash = new Hashtable();
                    InventoryRootHash["folder_id"] = inventData.RootFolderID.ToString();
                    ArrayList InventoryRoot = new ArrayList();
                    InventoryRoot.Add(InventoryRootHash);
                    userProfile.rootInventoryFolderID = inventData.RootFolderID;

                    // Circuit Code
                    uint circode = (uint)(Util.RandomClass.Next());

                    logResponse.Lastname = userProfile.surname;
                    logResponse.Firstname = userProfile.username;
                    logResponse.AgentID = agentID.ToString();
                    // TODO: Authentication server does not send these, so use random generated defaults! (at least for now)
                    logResponse.SessionID = userProfile.currentAgent.sessionID.ToString();
                    logResponse.SecureSessionID = userProfile.currentAgent.secureSessionID.ToString();
                    logResponse.InventoryRoot = InventoryRoot;
                    logResponse.InventorySkeleton = AgentInventoryArray;
                    logResponse.InventoryLibrary = m_ls.GetInventoryLibrary();
                    logResponse.ClientVersion = clientVersion;

                    Hashtable InventoryLibRootHash = new Hashtable();
                    InventoryLibRootHash["folder_id"] = "00000112-000f-0000-0000-000100bba000";
                    ArrayList InventoryLibRoot = new ArrayList();
                    InventoryLibRoot.Add(InventoryLibRootHash);
                    logResponse.InventoryLibRoot = InventoryLibRoot;

                    logResponse.InventoryLibraryOwner = m_ls.GetLibraryOwner();
                    logResponse.CircuitCode = (Int32)circode;
                    //logResponse.RegionX = 0; //overwritten
                    //logResponse.RegionY = 0; //overwritten
                    logResponse.Home = "!!null temporary value {home}!!"; // Overwritten
                    //logResponse.LookAt = "\n[r" + TheUser.homeLookAt.X.ToString() + ",r" + TheUser.homeLookAt.Y.ToString() + ",r" + TheUser.homeLookAt.Z.ToString() + "]\n";
                    //logResponse.SimAddress = "127.0.0.1"; //overwritten
                    //logResponse.SimPort = 0; //overwritten
                    logResponse.Message = m_ls.GetMessage();

                    try
                    {
                        m_ls.CustomiseResponse(logResponse, userProfile, asAddress);
                    }
                    catch (Exception e)
                    {
                        MainLog.Instance.Verbose("LOGIN", e.ToString());
                        if (!useLLSD)
                            return logResponse.CreateDeadRegionResponse();
                        else
                            return logResponse.CreateDeadRegionResponseLLSD();
                        //return logResponse.ToXmlRpcResponse();
                    }
                    //m_ls.CommitAgent(ref userProfile);
                    if (!useLLSD)
                        return logResponse.ToXmlRpcResponse();
                    else
                        return logResponse.ToLLSDResponse();

                }
                catch (Exception E)
                {
                    MainLog.Instance.Verbose("LOGIN", E.ToString());
                }
                #endregion
            }
            if (!useLLSD)
                return response;
            else
                return logResponse.CreateFailedResponseLLSD();
        }


        /// <summary>
        /// Does authentication to Authentication server
        /// </summary>
        /// <param name="account"></param>
        /// <param name="loginSessionHash"></param>
        /// <returns>new sessionhash ?</returns>
        public bool AuthenticateUser(string account, ref string loginSessionHash, string authenticationAddr)
        {
            Hashtable requestParams = new Hashtable();
            requestParams.Add("account", account);
            requestParams.Add("sessionhash", loginSessionHash);
            XmlRpcResponse res = doRequest("SimAuthenticationAccount", requestParams, authenticationAddr);

            if ((string)((Hashtable)res.Value)["login"] == "success")
            {
                loginSessionHash = (string)((Hashtable)res.Value)["sessionHash"];
                return true;
            }
            else
            {
                return false;
            }
        }

        public virtual UserProfileData GetTheUser(string account, string sessionhash, string authenticationAddr,
                                                  out string asAddress)
        {
            Hashtable requestParams = new Hashtable();
            requestParams.Add("avatar_account", account);
            requestParams.Add("sessionhash", sessionhash);
            XmlRpcResponse res = doRequest("get_user_by_account", requestParams, authenticationAddr);

            // should do better check
            if ((string)((Hashtable)res.Value)["uuid"] != null)
            {
                if ((string)((Hashtable)res.Value)["as_address"] != null)
                    asAddress = (string)((Hashtable)res.Value)["as_address"];
                else
                    asAddress = "";

                return HashtableToUserProfileData((Hashtable)res.Value);
            }
            asAddress = "";
            return null;
        }


        protected XmlRpcResponse doRequest(string method,
                                            Hashtable requestParams,
                                            string authenticationAddr)
        {
            ArrayList SendParams = new ArrayList();
            SendParams.Add(requestParams);
            XmlRpcRequest req = new XmlRpcRequest(method, SendParams);
            if (!authenticationAddr.StartsWith("http://"))
                authenticationAddr = "http://" + authenticationAddr;
            return req.Send(authenticationAddr, 300000);

        }

        static public UserProfileData HashtableToUserProfileData(Hashtable responseData)
        {
            UserProfileData profile = new UserProfileData();
            // Account information
            profile.username = (string)responseData["firstname"];
            profile.surname = (string)responseData["lastname"];

            profile.UUID = LLUUID.Parse((string)responseData["uuid"]);
            // Server Information
            profile.userInventoryURI = (string)responseData["server_inventory"];
            profile.userAssetURI = (string)responseData["server_asset"];
            // Profile Information
            profile.profileAboutText = (string)responseData["profile_about"];
            profile.profileFirstText = (string)responseData["profile_firstlife_about"];
            profile.profileFirstImage = LLUUID.Parse((string)responseData["profile_firstlife_image"]);

            profile.profileCanDoMask = uint.Parse((string)responseData["profile_can_do"]);
            profile.profileWantDoMask = uint.Parse((string)responseData["profile_want_do"]);

            profile.profileImage = LLUUID.Parse((string)responseData["profile_image"]);

            profile.created = int.Parse((string)responseData["profile_created"]);
            profile.lastLogin = int.Parse((string)responseData["profile_lastlogin"]);
            // Home region information
            profile.homeLocation = new LLVector3(float.Parse((string)responseData["home_coordinates_x"]),
                                                 float.Parse((string)responseData["home_coordinates_y"]),
                                                 float.Parse((string)responseData["home_coordinates_z"]));

            profile.homeRegion = ulong.Parse((string)responseData["home_region"]);

            profile.homeLookAt = new LLVector3(float.Parse((string)responseData["home_look_x"]),
                                               float.Parse((string)responseData["home_look_y"]),
                                               float.Parse((string)responseData["home_look_z"]));

            Hashtable UADtable = (Hashtable)responseData["currentAgent"];
            if (UADtable != null)
            {
                profile.currentAgent = new UserAgentData();
                HashtableToAgentData(ref UADtable, ref profile.currentAgent);
            }
            else
            {
                System.Console.WriteLine("No currentAgent in response!");
            }

            return profile;
        }

        static public void HashtableToAgentData(ref Hashtable h, ref UserAgentData uad)
        {

            uad.UUID = LLUUID.Parse((string)h["UUID"]);

            uad.agentIP = (string)h["agentIP"];

            uad.agentPort = uint.Parse((string)h["agentPort"]);

            uad.agentOnline = Boolean.Parse((string)h["agentOnline"]);

            uad.sessionID = LLUUID.Parse((string)h["sessionID"]);

            uad.secureSessionID = LLUUID.Parse((string)h["secureSessionID"]);

            uad.regionID = LLUUID.Parse((string)h["regionID"]);

            uad.loginTime = int.Parse((string)h["loginTime"]);

            uad.logoutTime = int.Parse((string)h["logoutTime"]);

            uad.currentRegion = LLUUID.Parse((string)h["currentRegion"]);

            uad.currentHandle = ulong.Parse((string)h["currentHandle"]);

            try
            {
                string pos1 = (string)h["currentPos"];//<123,3132,3123>
                string pos2 = pos1.Substring(1, pos1.Length - 2); // remove < & >
                string[] vals = pos2.Split(',');
                uad.currentPos = new LLVector3(float.Parse(vals[0]), float.Parse(vals[1]), float.Parse(vals[2]));
            }
            catch (Exception)
            {
                uad.currentPos = new LLVector3();
            }
        }


    }
}

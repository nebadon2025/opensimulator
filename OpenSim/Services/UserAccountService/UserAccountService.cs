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
 *     * Neither the name of the OpenSimulator Project nor the
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
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using Nini.Config;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using log4net;

namespace OpenSim.Services.UserAccountService
{
    public class UserAccountService : UserAccountServiceBase, IUserAccountService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static UserAccountService m_RootInstance;

        protected IGridService m_GridService;
        protected IAuthenticationService m_AuthenticationService;
        protected IGridUserService m_GridUserService;
        protected IInventoryService m_InventoryService;

        protected static UUID s_libOwner = new UUID("11111111-1111-0000-0000-000100bba000");        

        public UserAccountService(IConfigSource config)
            : base(config)
        {
            IConfig userConfig = config.Configs["UserAccountService"];
            if (userConfig == null)
                throw new Exception("No UserAccountService configuration");

            // In case there are several instances of this class in the same process,
            // the console commands are only registered for the root instance
            if (m_RootInstance == null)
            {
                m_RootInstance = this;
                string gridServiceDll = userConfig.GetString("GridService", string.Empty);
                if (gridServiceDll != string.Empty)
                    m_GridService = LoadPlugin<IGridService>(gridServiceDll, new Object[] { config });

                string authServiceDll = userConfig.GetString("AuthenticationService", string.Empty);
                if (authServiceDll != string.Empty)
                    m_AuthenticationService = LoadPlugin<IAuthenticationService>(authServiceDll, new Object[] { config });

                string presenceServiceDll = userConfig.GetString("GridUserService", string.Empty);
                if (presenceServiceDll != string.Empty)
                    m_GridUserService = LoadPlugin<IGridUserService>(presenceServiceDll, new Object[] { config });

                string invServiceDll = userConfig.GetString("InventoryService", string.Empty);
                if (invServiceDll != string.Empty)
                    m_InventoryService = LoadPlugin<IInventoryService>(invServiceDll, new Object[] { config });

                if (MainConsole.Instance != null)
                {
                    MainConsole.Instance.Commands.AddCommand("UserService", false,
                            "create user",
                            "create user [<first> [<last> [<pass> [<email>]]]]",
                            "Create a new user", HandleCreateUser);
                    MainConsole.Instance.Commands.AddCommand("UserService", false, "reset user password",
                            "reset user password [<first> [<last> [<password>]]]",
                            "Reset a user password", HandleResetUserPassword);
                }
            }
        }

        #region IUserAccountService

        public UserAccount GetUserAccount(UUID scopeID, string firstName,
                string lastName)
        {
//            m_log.DebugFormat(
//                "[USER ACCOUNT SERVICE]: Retrieving account by username for {0} {1}, scope {2}",
//                firstName, lastName, scopeID);

            UserAccountData[] d;

            if (scopeID != UUID.Zero)
            {
                d = m_Database.Get(
                        new string[] { "ScopeID", "FirstName", "LastName" },
                        new string[] { scopeID.ToString(), firstName, lastName });
                if (d.Length < 1)
                {
                    d = m_Database.Get(
                            new string[] { "ScopeID", "FirstName", "LastName" },
                            new string[] { UUID.Zero.ToString(), firstName, lastName });
                }
            }
            else
            {
                d = m_Database.Get(
                        new string[] { "FirstName", "LastName" },
                        new string[] { firstName, lastName });
            }

            if (d.Length < 1)
                return null;

            return MakeUserAccount(d[0]);
        }

        private UserAccount MakeUserAccount(UserAccountData d)
        {
            UserAccount u = new UserAccount();
            u.FirstName = d.FirstName;
            u.LastName = d.LastName;
            u.PrincipalID = d.PrincipalID;
            u.ScopeID = d.ScopeID;
            if (d.Data.ContainsKey("Email") && d.Data["Email"] != null)
                u.Email = d.Data["Email"].ToString();
            else
                u.Email = string.Empty;
            u.Created = Convert.ToInt32(d.Data["Created"].ToString());
            if (d.Data.ContainsKey("UserTitle") && d.Data["UserTitle"] != null)
                u.UserTitle = d.Data["UserTitle"].ToString();
            else
                u.UserTitle = string.Empty;
            if (d.Data.ContainsKey("UserLevel") && d.Data["UserLevel"] != null)
                Int32.TryParse(d.Data["UserLevel"], out u.UserLevel);
            if (d.Data.ContainsKey("UserFlags") && d.Data["UserFlags"] != null)
                Int32.TryParse(d.Data["UserFlags"], out u.UserFlags);

            if (d.Data.ContainsKey("ServiceURLs") && d.Data["ServiceURLs"] != null)
            {
                string[] URLs = d.Data["ServiceURLs"].ToString().Split(new char[] { ' ' });
                u.ServiceURLs = new Dictionary<string, object>();

                foreach (string url in URLs)
                {
                    string[] parts = url.Split(new char[] { '=' });

                    if (parts.Length != 2)
                        continue;

                    string name = System.Web.HttpUtility.UrlDecode(parts[0]);
                    string val = System.Web.HttpUtility.UrlDecode(parts[1]);

                    u.ServiceURLs[name] = val;
                }
            }
            else
                u.ServiceURLs = new Dictionary<string, object>();

            return u;
        }

        public UserAccount GetUserAccount(UUID scopeID, string email)
        {
            UserAccountData[] d;

            if (scopeID != UUID.Zero)
            {
                d = m_Database.Get(
                        new string[] { "ScopeID", "Email" },
                        new string[] { scopeID.ToString(), email });
                if (d.Length < 1)
                {
                    d = m_Database.Get(
                            new string[] { "ScopeID", "Email" },
                            new string[] { UUID.Zero.ToString(), email });
                }
            }
            else
            {
                d = m_Database.Get(
                        new string[] { "Email" },
                        new string[] { email });
            }

            if (d.Length < 1)
                return null;

            return MakeUserAccount(d[0]);
        }

        public UserAccount GetUserAccount(UUID scopeID, UUID principalID)
        {
            UserAccountData[] d;

            if (scopeID != UUID.Zero)
            {
                d = m_Database.Get(
                        new string[] { "ScopeID", "PrincipalID" },
                        new string[] { scopeID.ToString(), principalID.ToString() });
                if (d.Length < 1)
                {
                    d = m_Database.Get(
                            new string[] { "ScopeID", "PrincipalID" },
                            new string[] { UUID.Zero.ToString(), principalID.ToString() });
                }
            }
            else
            {
                d = m_Database.Get(
                        new string[] { "PrincipalID" },
                        new string[] { principalID.ToString() });
            }

            if (d.Length < 1)
            {
                return null;
            }

            return MakeUserAccount(d[0]);
        }

        public bool StoreUserAccount(UserAccount data)
        {
//            m_log.DebugFormat(
//                "[USER ACCOUNT SERVICE]: Storing user account for {0} {1} {2}, scope {3}",
//                data.FirstName, data.LastName, data.PrincipalID, data.ScopeID);

            UserAccountData d = new UserAccountData();

            d.FirstName = data.FirstName;
            d.LastName = data.LastName;
            d.PrincipalID = data.PrincipalID;
            d.ScopeID = data.ScopeID;
            d.Data = new Dictionary<string, string>();
            d.Data["Email"] = data.Email;
            d.Data["Created"] = data.Created.ToString();
            d.Data["UserLevel"] = data.UserLevel.ToString();
            d.Data["UserFlags"] = data.UserFlags.ToString();
            if (data.UserTitle != null)
                d.Data["UserTitle"] = data.UserTitle.ToString();

            List<string> parts = new List<string>();

            foreach (KeyValuePair<string, object> kvp in data.ServiceURLs)
            {
                string key = System.Web.HttpUtility.UrlEncode(kvp.Key);
                string val = System.Web.HttpUtility.UrlEncode(kvp.Value.ToString());
                parts.Add(key + "=" + val);
            }

            d.Data["ServiceURLs"] = string.Join(" ", parts.ToArray());

            return m_Database.Store(d);
        }

        public List<UserAccount> GetUserAccounts(UUID scopeID, string query)
        {
            UserAccountData[] d = m_Database.GetUsers(scopeID, query);

            if (d == null)
                return new List<UserAccount>();

            List<UserAccount> ret = new List<UserAccount>();

            foreach (UserAccountData data in d)
                ret.Add(MakeUserAccount(data));

            return ret;
        }

        #endregion

        #region Console commands

        /// <summary>
        /// Handle the create user command from the console.
        /// </summary>
        /// <param name="cmdparams">string array with parameters: firstname, lastname, password, locationX, locationY, email</param>
        protected void HandleCreateUser(string module, string[] cmdparams)
        {
            string firstName;
            string lastName;
            string password;
            string email;

            List<char> excluded = new List<char>(new char[]{' '});

            if (cmdparams.Length < 3)
                firstName = MainConsole.Instance.CmdPrompt("First name", "Default", excluded);
            else firstName = cmdparams[2];

            if (cmdparams.Length < 4)
                lastName = MainConsole.Instance.CmdPrompt("Last name", "User", excluded);
            else lastName = cmdparams[3];

            if (cmdparams.Length < 5)
                password = MainConsole.Instance.PasswdPrompt("Password");
            else password = cmdparams[4];

            if (cmdparams.Length < 6)
                email = MainConsole.Instance.CmdPrompt("Email", "");
            else email = cmdparams[5];

            CreateUser(firstName, lastName, password, email);
        }

        protected void HandleResetUserPassword(string module, string[] cmdparams)
        {
            string firstName;
            string lastName;
            string newPassword;

            if (cmdparams.Length < 4)
                firstName = MainConsole.Instance.CmdPrompt("First name");
            else firstName = cmdparams[3];

            if (cmdparams.Length < 5)
                lastName = MainConsole.Instance.CmdPrompt("Last name");
            else lastName = cmdparams[4];

            if (cmdparams.Length < 6)
                newPassword = MainConsole.Instance.PasswdPrompt("New password");
            else newPassword = cmdparams[5];

            UserAccount account = GetUserAccount(UUID.Zero, firstName, lastName);
            if (account == null)
                m_log.ErrorFormat("[USER ACCOUNT SERVICE]: No such user");

            bool success = false;
            if (m_AuthenticationService != null)
                success = m_AuthenticationService.SetPassword(account.PrincipalID, newPassword);
            if (!success)
                m_log.ErrorFormat("[USER ACCOUNT SERVICE]: Unable to reset password for account {0} {1}.",
                   firstName, lastName);
            else
                m_log.InfoFormat("[USER ACCOUNT SERVICE]: Password reset for user {0} {1}", firstName, lastName);
        }

        #endregion

        public UserAccount CreateUserAccount(UserAccount data, string password)
        {
            CreateUser(data.FirstName, data.LastName, password, data.Email);
            return data;
        }
        
        /// <summary>
        /// Create a user
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="password"></param>
        /// <param name="email"></param>
        private void CreateUser(string firstName, string lastName, string password, string email)
        {
            UserAccount account = GetUserAccount(UUID.Zero, firstName, lastName);
            if (null == account)
            {
                account = new UserAccount(UUID.Zero, firstName, lastName, email);
                if (account.ServiceURLs == null || (account.ServiceURLs != null && account.ServiceURLs.Count == 0))
                {
                    account.ServiceURLs = new Dictionary<string, object>();
                    account.ServiceURLs["HomeURI"] = string.Empty;
                    account.ServiceURLs["GatekeeperURI"] = string.Empty;
                    account.ServiceURLs["InventoryServerURI"] = string.Empty;
                    account.ServiceURLs["AssetServerURI"] = string.Empty;
                }

                if (StoreUserAccount(account))
                {
                    bool success;
                    if (m_AuthenticationService != null)
                    {
                        success = m_AuthenticationService.SetPassword(account.PrincipalID, password);
                        if (!success)
                            m_log.WarnFormat("[USER ACCOUNT SERVICE]: Unable to set password for account {0} {1}.",
                                firstName, lastName);
                    }

                    GridRegion home = null;
                    if (m_GridService != null)
                    {
                        List<GridRegion> defaultRegions = m_GridService.GetDefaultRegions(UUID.Zero);
                        if (defaultRegions != null && defaultRegions.Count >= 1)
                            home = defaultRegions[0];

                        if (m_GridUserService != null && home != null)
                            m_GridUserService.SetHome(account.PrincipalID.ToString(), home.RegionID, new Vector3(128, 128, 0), new Vector3(0, 1, 0));
                        else
                            m_log.WarnFormat("[USER ACCOUNT SERVICE]: Unable to set home for account {0} {1}.",
                               firstName, lastName);
                    }
                    else
                    {
                        m_log.WarnFormat("[USER ACCOUNT SERVICE]: Unable to retrieve home region for account {0} {1}.",
                           firstName, lastName);
                    }

                    Console.WriteLine("here");
                    if (m_InventoryService != null)
                    {
                        Console.WriteLine("here2");
                        if (m_InventoryService.CreateUserInventory(account.PrincipalID))
                        {
                            Console.WriteLine("here3");
                            CreateDefaultInventory(account.PrincipalID);
                        }
                        else
                        {
                            Console.WriteLine("here4");
                            m_log.WarnFormat("[USER ACCOUNT SERVICE]: Unable to create inventory for account {0} {1}.",
                                firstName, lastName);
                        }
                        Console.WriteLine("here5");
                    }                        

                    Console.WriteLine("here6");
                    m_log.InfoFormat("[USER ACCOUNT SERVICE]: Account {0} {1} created successfully", firstName, lastName);
                } 
                else 
                {
                    m_log.ErrorFormat("[USER ACCOUNT SERVICE]: Account creation failed for account {0} {1}", firstName, lastName);
                }
            }
            else
            {
                m_log.ErrorFormat("[USER ACCOUNT SERVICE]: A user with the name {0} {1} already exists!", firstName, lastName);
            }
        }
        
        protected void CreateDefaultInventory(UUID principalID)
        {
            m_log.InfoFormat("[USER ACCOUNT SERVICE]: Creating default inventory for {0}", principalID);
            
            InventoryFolderBase rootFolder = m_InventoryService.GetRootFolder(principalID);
            
//            XInventoryFolder[] sysFolders = GetSystemFolders(principalID);

            CreateFolder(principalID, rootFolder.ID, (int)AssetType.Animation, "Animations");
            InventoryFolderBase bodypartFolder = CreateFolder(principalID, rootFolder.ID, (int)AssetType.Bodypart, "Body Parts");
            CreateFolder(principalID, rootFolder.ID, (int)AssetType.CallingCard, "Calling Cards");
            InventoryFolderBase clothingFolder = CreateFolder(principalID, rootFolder.ID, (int)AssetType.Clothing, "Clothing");
            CreateFolder(principalID, rootFolder.ID, (int)AssetType.Gesture, "Gestures");
            CreateFolder(principalID, rootFolder.ID, (int)AssetType.Landmark, "Landmarks");
            CreateFolder(principalID, rootFolder.ID, (int)AssetType.LostAndFoundFolder, "Lost And Found");
            CreateFolder(principalID, rootFolder.ID, (int)AssetType.Notecard, "Notecards");
            CreateFolder(principalID, rootFolder.ID, (int)AssetType.Object, "Objects");
            CreateFolder(principalID, rootFolder.ID, (int)AssetType.SnapshotFolder, "Photo Album");
            CreateFolder(principalID, rootFolder.ID, (int)AssetType.LSLText, "Scripts");
            CreateFolder(principalID, rootFolder.ID, (int)AssetType.Sound, "Sounds");
            CreateFolder(principalID, rootFolder.ID, (int)AssetType.Texture, "Textures");
            CreateFolder(principalID, rootFolder.ID, (int)AssetType.TrashFolder, "Trash");
            
            // Default minimum body parts for viewer 2 appearance
            InventoryItemBase defaultShape = new InventoryItemBase();
            defaultShape.Name = "Default shape";
            defaultShape.Description = "Default shape description";
            defaultShape.AssetType = (int)AssetType.Bodypart;
            defaultShape.InvType = (int)InventoryType.Wearable;
            defaultShape.Flags = (uint)WearableType.Shape;
            defaultShape.ID = AvatarWearable.DEFAULT_BODY_ITEM;
            defaultShape.AssetID = AvatarWearable.DEFAULT_BODY_ASSET;
            defaultShape.Folder = bodypartFolder.ID;
            defaultShape.CreatorId = s_libOwner.ToString();
            defaultShape.Owner = principalID;
            defaultShape.BasePermissions = (uint)PermissionMask.All;
            defaultShape.CurrentPermissions = (uint)PermissionMask.All;
            defaultShape.EveryOnePermissions = (uint)PermissionMask.None;
            defaultShape.NextPermissions = (uint)PermissionMask.All;
            m_InventoryService.AddItem(defaultShape);                        
            
            InventoryItemBase defaultSkin = new InventoryItemBase();
            defaultSkin.Name = "Default skin";
            defaultSkin.Description = "Default skin description";
            defaultSkin.AssetType = (int)AssetType.Bodypart;
            defaultSkin.InvType = (int)InventoryType.Wearable;
            defaultSkin.Flags = (uint)WearableType.Skin;
            defaultSkin.ID = AvatarWearable.DEFAULT_SKIN_ITEM;
            defaultSkin.AssetID = AvatarWearable.DEFAULT_SKIN_ASSET;
            defaultSkin.Folder = bodypartFolder.ID;
            defaultSkin.CreatorId = s_libOwner.ToString();
            defaultSkin.Owner = principalID;
            defaultSkin.BasePermissions = (uint)PermissionMask.All;
            defaultSkin.CurrentPermissions = (uint)PermissionMask.All;
            defaultSkin.EveryOnePermissions = (uint)PermissionMask.None;
            defaultSkin.NextPermissions = (uint)PermissionMask.All;            
            m_InventoryService.AddItem(defaultSkin);   
            
            InventoryItemBase defaultHair = new InventoryItemBase();
            defaultHair.Name = "Default hair";
            defaultHair.Description = "Default hair description";
            defaultHair.AssetType = (int)AssetType.Bodypart;
            defaultHair.InvType = (int)InventoryType.Wearable;
            defaultHair.Flags = (uint)WearableType.Hair;
            defaultHair.ID = AvatarWearable.DEFAULT_HAIR_ITEM;
            defaultHair.AssetID = AvatarWearable.DEFAULT_HAIR_ASSET;
            defaultHair.Folder = bodypartFolder.ID;
            defaultHair.CreatorId = s_libOwner.ToString();
            defaultHair.Owner = principalID;
            defaultHair.BasePermissions = (uint)PermissionMask.All;
            defaultHair.CurrentPermissions = (uint)PermissionMask.All;
            defaultHair.EveryOnePermissions = (uint)PermissionMask.None;
            defaultHair.NextPermissions = (uint)PermissionMask.All;            
            m_InventoryService.AddItem(defaultHair); 
            
            InventoryItemBase defaultEyes = new InventoryItemBase();
            defaultEyes.Name = "Default eyes";
            defaultEyes.Description = "Default eyes description";
            defaultEyes.AssetType = (int)AssetType.Bodypart;
            defaultEyes.InvType = (int)InventoryType.Wearable;
            defaultEyes.Flags = (uint)WearableType.Eyes;
            defaultEyes.ID = AvatarWearable.DEFAULT_EYES_ITEM;
            defaultEyes.AssetID = AvatarWearable.DEFAULT_EYES_ASSET;
            defaultEyes.Folder = bodypartFolder.ID;
            defaultEyes.CreatorId = s_libOwner.ToString();
            defaultEyes.Owner = principalID;
            defaultEyes.BasePermissions = (uint)PermissionMask.All;
            defaultEyes.CurrentPermissions = (uint)PermissionMask.All;
            defaultEyes.EveryOnePermissions = (uint)PermissionMask.None;
            defaultEyes.NextPermissions = (uint)PermissionMask.All;            
            m_InventoryService.AddItem(defaultEyes);   
            
            // Default minimum clothes for viewer 2 non-naked appearance
            InventoryItemBase defaultShirt = new InventoryItemBase();
            defaultShirt.Name = "Default shirt";
            defaultShirt.Description = "Default shirt description";
            defaultShirt.AssetType = (int)AssetType.Clothing;
            defaultShirt.InvType = (int)InventoryType.Wearable;
            defaultShirt.Flags = (uint)WearableType.Shirt;
            defaultShirt.ID = AvatarWearable.DEFAULT_SHIRT_ITEM;
            defaultShirt.AssetID = AvatarWearable.DEFAULT_SHIRT_ASSET;
            defaultShirt.Folder = clothingFolder.ID;
            defaultShirt.CreatorId = s_libOwner.ToString();
            defaultShirt.Owner = principalID;
            defaultShirt.BasePermissions = (uint)PermissionMask.All;
            defaultShirt.CurrentPermissions = (uint)PermissionMask.All;
            defaultShirt.EveryOnePermissions = (uint)PermissionMask.None;
            defaultShirt.NextPermissions = (uint)PermissionMask.All;            
            m_InventoryService.AddItem(defaultShirt);   
            
            InventoryItemBase defaultPants = new InventoryItemBase();
            defaultPants.Name = "Default pants";
            defaultPants.Description = "Default pants description";
            defaultPants.AssetType = (int)AssetType.Clothing;
            defaultPants.InvType = (int)InventoryType.Wearable;
            defaultPants.Flags = (uint)WearableType.Pants;
            defaultPants.ID = AvatarWearable.DEFAULT_PANTS_ITEM;
            defaultPants.AssetID = AvatarWearable.DEFAULT_PANTS_ASSET;
            defaultPants.Folder = clothingFolder.ID;
            defaultPants.CreatorId = s_libOwner.ToString();
            defaultPants.Owner = principalID;
            defaultPants.BasePermissions = (uint)PermissionMask.All;
            defaultPants.CurrentPermissions = (uint)PermissionMask.All;
            defaultPants.EveryOnePermissions = (uint)PermissionMask.None;
            defaultPants.NextPermissions = (uint)PermissionMask.All;            
            m_InventoryService.AddItem(defaultPants);       
        }
                                         
        protected InventoryFolderBase CreateFolder(UUID principalID, UUID parentID, int type, string name)
        {
            InventoryFolderBase folder 
                = new InventoryFolderBase(UUID.Random(), name, principalID, (short)type, parentID, 1);
            
            if (m_InventoryService.AddFolder(folder))
                return folder;
            else
                return null;
        }          
    }
}

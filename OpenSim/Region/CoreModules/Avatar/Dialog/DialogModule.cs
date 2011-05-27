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
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;

using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

//DSG
using Nwc.XmlRpc;
using System.Net;
using System.Collections;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using PresenceInfo = OpenSim.Services.Interfaces.PresenceInfo;

namespace OpenSim.Region.CoreModules.Avatar.Dialog
{
    public class DialogModule : IRegionModule, IDialogModule 
    { 
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        protected Scene m_scene;
        
        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<IDialogModule>(this);

            m_scene.AddCommand(
                this, "alert", "alert <message>",
                "Send an alert to everyone",
                HandleAlertConsoleCommand);

            m_scene.AddCommand(
                this, "alert-user", "alert-user <first> <last> <message>",
                "Send an alert to a user",
                HandleAlertConsoleCommand);
        }
        
        //public void PostInitialise() {}
        public void Close() {}
        public string Name { get { return "Dialog Module"; } }
        public bool IsSharedModule { get { return false; } }
        
        public void SendAlertToUser(IClientAPI client, string message)
        {
            SendAlertToUser(client, message, false);
        }
        
        public void SendAlertToUser(IClientAPI client, string message, bool modal)
        {
            client.SendAgentAlertMessage(message, modal);
        } 
        
        public void SendAlertToUser(UUID agentID, string message)
        {
            SendAlertToUser(agentID, message, false);
        }
        
        public void SendAlertToUser(UUID agentID, string message, bool modal)
        {
            ScenePresence sp = m_scene.GetScenePresence(agentID);
            
            if (sp != null)
                sp.ControllingClient.SendAgentAlertMessage(message, modal);
        }
        
        public void SendAlertToUser(string firstName, string lastName, string message, bool modal)
        {
            ScenePresence presence = m_scene.GetScenePresence(firstName, lastName);
            if (presence != null)
                presence.ControllingClient.SendAgentAlertMessage(message, modal);
        }
        
        public void SendGeneralAlert(string message)
        {
            m_scene.ForEachScenePresence(delegate(ScenePresence presence)
            {
                if (!presence.IsChildAgent)
                    presence.ControllingClient.SendAlertMessage(message);
            });
        }

        public void SendDialogToUser(
            UUID avatarID, string objectName, UUID objectID, UUID ownerID,
            string message, UUID textureID, int ch, string[] buttonlabels)
        {
            UserAccount account = m_scene.UserAccountService.GetUserAccount(m_scene.RegionInfo.ScopeID, ownerID);
            string ownerFirstName, ownerLastName;
            if (account != null)
            {
                ownerFirstName = account.FirstName;
                ownerLastName = account.LastName;
            }
            else
            {
                ownerFirstName = "(unknown";
                ownerLastName = "user)";
            }

            ScenePresence sp = m_scene.GetScenePresence(avatarID);
            if (sp != null)
                sp.ControllingClient.SendDialog(objectName, objectID, ownerFirstName, ownerLastName, message, textureID, ch, buttonlabels);
        }

        public void SendUrlToUser(
            UUID avatarID, string objectName, UUID objectID, UUID ownerID, bool groupOwned, string message, string url)
        {
            ScenePresence sp = m_scene.GetScenePresence(avatarID);
            
            if (sp != null)
                sp.ControllingClient.SendLoadURL(objectName, objectID, ownerID, groupOwned, message, url);
        }
        
        public void SendTextBoxToUser(UUID avatarid, string message, int chatChannel, string name, UUID objectid, UUID ownerid)
        {
            UserAccount account = m_scene.UserAccountService.GetUserAccount(m_scene.RegionInfo.ScopeID, ownerid);
            string ownerFirstName, ownerLastName;
            if (account != null)
            {
                ownerFirstName = account.FirstName;
                ownerLastName = account.LastName;
            }
            else
            {
                ownerFirstName = "(unknown";
                ownerLastName = "user)";
            }

            ScenePresence sp = m_scene.GetScenePresence(avatarid);
            
            if (sp != null)
                sp.ControllingClient.SendTextBoxRequest(message, chatChannel, name, ownerFirstName, ownerLastName, objectid);
        }

        public void SendNotificationToUsersInRegion(
            UUID fromAvatarID, string fromAvatarName, string message)
        {
            m_scene.ForEachScenePresence(delegate(ScenePresence presence)
            {
                if (!presence.IsChildAgent)
                    presence.ControllingClient.SendBlueBoxMessage(fromAvatarID, fromAvatarName, message);
            });
        }
        
        /// <summary>
        /// Handle an alert command from the console.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cmdparams"></param>
        public void HandleAlertConsoleCommand(string module, string[] cmdparams)
        {
            if (m_scene.ConsoleScene() != null && m_scene.ConsoleScene() != m_scene)
                return;

            string message = string.Empty;

            if (cmdparams[0].ToLower().Equals("alert"))
            {
                message = CombineParams(cmdparams, 1);
                m_log.InfoFormat("[DIALOG]: Sending general alert in region {0} with message {1}",
                    m_scene.RegionInfo.RegionName, message);
                SendGeneralAlert(message);
            }
            else if (cmdparams.Length > 3)
            {
                string firstName = cmdparams[1];
                string lastName = cmdparams[2];
                message = CombineParams(cmdparams, 3);
                m_log.InfoFormat(
                    "[DIALOG]: Sending alert in region {0} to {1} {2} with message {3}",
                    m_scene.RegionInfo.RegionName, firstName, lastName, message);
                SendAlertToUser(firstName, lastName, message, false);
            }
            else
            {
                OpenSim.Framework.Console.MainConsole.Instance.Output(
                    "Usage: alert <message> | alert-user <first> <last> <message>");
                return;
            }
        }

        private string CombineParams(string[] commandParams, int pos)
        {
            string result = string.Empty;
            for (int i = pos; i < commandParams.Length; i++)
            {
                result += commandParams[i] + " ";
            }
            
            return result;
        }

        #region GridCommunication
        private IPresenceService m_PresenceService;
        protected IPresenceService PresenceService
        {
            get
            {
                if (m_PresenceService == null)
                    m_PresenceService = m_scene.RequestModuleInterface<IPresenceService>();
                return m_PresenceService;
            }
        }
        //DSG added to support llDialog with distributed script engine and client manager,
        //following the same communication pattern as grid_instant_message

        public virtual void SendGridDialogViaXMLRPCAsync(UUID avatarID, string objectName, UUID objectID, string ownerFirstName,
            string ownerLastName, string message, UUID textureID, int ch, string[] buttonlabels, UUID prevRegionID)
        {
            UUID toAgentID;
            PresenceInfo upd = null;
            // Non-cached user agent lookup.
            PresenceInfo[] presences = PresenceService.GetAgents(new string[] { avatarID.ToString() });

            if (presences != null && presences.Length > 0)
            {
                foreach (PresenceInfo p in presences)
                {
                    if (p.RegionID != UUID.Zero)
                    {
                        upd = p;
                        break;
                    }
                }

                if (upd != null)
                {
                    // check if we've tried this before..
                    // This is one way to end the recursive loop
                    //
                    if (upd.RegionID == prevRegionID)
                    {
                        //Dialog content undelivered
                        m_log.WarnFormat("Couldn't deliver dialog to {0}" + avatarID);
                        return;
                    }
                }
                else
                {
                    //Dialog content undelivered
                    m_log.WarnFormat("Couldn't deliver dialog to {0}" + toAgentID);
                    return;
                }
            }

            if (upd != null)
            {
                GridRegion reginfo = m_scene.GridService.GetRegionByUUID(m_scene.RegionInfo.ScopeID,
                    upd.RegionID);
                if (reginfo != null)
                {
                    Hashtable msgdata = ConvertGridDialogToXMLRPC(avatarID, objectName, objectID, ownerFirstName, ownerLastName, message, textureID, ch, buttonlabels);
                    //= ConvertGridInstantMessageToXMLRPC(im);
                    // Not actually used anymore, left in for compatibility
                    // Remove at next interface change
                    //
                    msgdata["region_handle"] = 0;

                    bool imresult = doDialogSending(reginfo, msgdata);
                    if (imresult)
                    {
                        SendGridDialogViaXMLRPCAsync(avatarID, objectName, objectID, ownerFirstName, ownerLastName, message, textureID, ch, buttonlabels, prevRegionID);
                    }
                }
            }
        }

        private Hashtable ConvertGridDialogToXMLRPC(UUID avatarID, string objectName, UUID objectID, string ownerFirstName, string ownerLastName,
            string message, UUID textureID, int ch, string[] buttonlabels)
        {
            Hashtable msgdata = new Hashtable();
            return msgdata;
        }

        /// <summary>
        /// This actually does the XMLRPC Request
        /// </summary>
        /// <param name="reginfo">RegionInfo we pull the data out of to send the request to</param>
        /// <param name="xmlrpcdata">The Instant Message data Hashtable</param>
        /// <returns>Bool if the message was successfully delivered at the other side.</returns>
        protected virtual bool doDialogSending(GridRegion reginfo, Hashtable xmlrpcdata)
        {

            ArrayList SendParams = new ArrayList();
            SendParams.Add(xmlrpcdata);
            XmlRpcRequest GridReq = new XmlRpcRequest("grid_dialog", SendParams);
            try
            {

                XmlRpcResponse GridResp = GridReq.Send(reginfo.ServerURI, 3000);

                Hashtable responseData = (Hashtable)GridResp.Value;

                if (responseData.ContainsKey("success"))
                {
                    if ((string)responseData["success"] == "TRUE")
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[GRID INSTANT MESSAGE]: Error sending grid_dialog to {0} the host didn't respond " + e.ToString(), reginfo.ServerURI.ToString());
            }

            return false;
        }

        public virtual void PostInitialise()
        {
            MainServer.Instance.AddXmlRPCHandler(
                "grid_dialog", processXMLRPCGridDialog);
        }

                /// <summary>
        /// Process a XMLRPC Grid Instant Message
        /// </summary>
        /// <param name="request">XMLRPC parameters
        /// </param>
        /// <returns>Nothing much</returns>
        protected virtual XmlRpcResponse processXMLRPCGridDialog(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            bool successful = false;

            //Send response back to region calling if it was successful
            // calling region uses this to know when to look up a user's location again.
            XmlRpcResponse resp = new XmlRpcResponse();
            Hashtable respdata = new Hashtable();
            if (successful)
                respdata["success"] = "TRUE";
            else
                respdata["success"] = "FALSE";
            resp.Value = respdata;

            return resp;
        }

#endregion //GridCommunication
    }
}
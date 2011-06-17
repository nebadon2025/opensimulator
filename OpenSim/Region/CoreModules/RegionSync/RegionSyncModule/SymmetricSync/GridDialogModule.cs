/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyrightD
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
using System.Linq;
using System.Text;
using System.Collections;
using System.Net;
using System.Reflection;

using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenMetaverse;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using Mono.Addins;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using PresenceInfo = OpenSim.Services.Interfaces.PresenceInfo;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    //DSG added to support llDialog with distributed script engine and client manager,
    //following the same communication pattern as grid_instant_message
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "GridDialogModule")]
    public class GridDialogModule : ISharedRegionModule, IGridDialogModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //private bool m_Enabled = false;
        protected List<Scene> m_Scenes = new List<Scene>();

        private IPresenceService m_PresenceService;
        protected IPresenceService PresenceService
        {
            get
            {
                if (m_PresenceService == null)
                    m_PresenceService = m_Scenes[0].RequestModuleInterface<IPresenceService>();
                return m_PresenceService;
            }
        }

        #region ISharedRegionModule

        public virtual void Initialise(IConfigSource config)
        {
            //m_log.Debug("[GRID DIALOG MODULE]: Initialise");
        }

        public virtual void AddRegion(Scene scene)
        {
            lock (m_Scenes)
            {
                m_log.Debug("[GRID DIALOG MODULE]: Grid Dialog Module active");
                scene.RegisterModuleInterface<IGridDialogModule>(this);
                m_Scenes.Add(scene);
            }
        }

        public virtual void PostInitialise()
        {
            //if (!m_Enabled)
            //    return;

            MainServer.Instance.AddXmlRPCHandler(
                "grid_dialog", processXMLRPCGridDialog);
        }

        public virtual void RegionLoaded(Scene scene)
        {
        }

        public virtual void RemoveRegion(Scene scene)
        {
            //if (!m_Enabled)
            //    return;

            lock (m_Scenes)
            {
                m_Scenes.Remove(scene);
            }
        }

        public virtual void Close()
        {
        }

        public virtual string Name
        {
            get { return "GridDialogModule"; }
        }

        public virtual Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion //ISharedRegionModule


        #region GridCommunication

        public delegate void GridDialogDelegate(UUID avatarID, string objectName, UUID objectID, UUID ownerID, string ownerFirstName,
            string ownerLastName, string message, UUID textureID, int ch, string[] buttonlabels, UUID prevRegionID);

        protected virtual void GridDialogCompleted(IAsyncResult iar)
        {
            GridDialogDelegate icon =
                    (GridDialogDelegate)iar.AsyncState;
            icon.EndInvoke(iar);
        }

        public void SendGridDialogViaXMLRPC(UUID avatarID, string objectName, UUID objectID, UUID ownerID, string ownerFirstName,
            string ownerLastName, string message, UUID textureID, int ch, string[] buttonlabels, UUID prevRegionID)
        {
            GridDialogDelegate d = SendGridDialogViaXMLRPCAsync;

            d.BeginInvoke(avatarID, objectName, objectID, ownerID, ownerFirstName, ownerLastName, message, textureID, ch, buttonlabels, prevRegionID,
                GridDialogCompleted, d);
        }


        private void SendGridDialogViaXMLRPCAsync(UUID avatarID, string objectName, UUID objectID, UUID ownerID, string ownerFirstName,
            string ownerLastName, string message, UUID textureID, int ch, string[] buttonlabels, UUID prevRegionID)
        {
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
                    m_log.WarnFormat("Couldn't deliver dialog to {0}" + avatarID);
                    return;
                }
            }

            if (upd != null)
            {
                GridRegion reginfo = m_Scenes[0].GridService.GetRegionByUUID(m_Scenes[0].RegionInfo.ScopeID,
                    upd.RegionID);
                if (reginfo != null)
                {
                    Hashtable msgdata = ConvertGridDialogToXMLRPC(avatarID, objectName, objectID, ownerID, ownerFirstName, ownerLastName, message, textureID, ch, buttonlabels);
                    //= ConvertGridInstantMessageToXMLRPC(im);
                    // Not actually used anymore, left in for compatibility
                    // Remove at next interface change
                    //
                    msgdata["region_handle"] = 0;

                    bool imresult = doDialogSending(reginfo, msgdata);
                    if (!imresult)
                    {
                        SendGridDialogViaXMLRPCAsync(avatarID, objectName, objectID, ownerID, ownerFirstName, ownerLastName, message, textureID, ch, buttonlabels, prevRegionID);
                        m_log.WarnFormat("Couldn't deliver dialog to {0}" + avatarID);
                        return;
                    }
                }
            }
        }

        private Hashtable ConvertGridDialogToXMLRPC(UUID avatarID, string objectName, UUID objectID, UUID ownerID, string ownerFirstName, string ownerLastName,
            string message, UUID textureID, int ch, string[] buttonlabels)
        {
            Hashtable msgdata = new Hashtable();
            msgdata["avatarID"] = avatarID.ToString();
            msgdata["objectName"] = objectName;
            msgdata["objectID"] = objectID.ToString();
            msgdata["ownerID"] = ownerID.ToString();
            msgdata["ownerFirstName"] = ownerFirstName;
            msgdata["ownerLastName"] = ownerLastName;
            msgdata["message"] = message;
            msgdata["textureID"] = textureID.ToString();
            msgdata["ch"] = ch.ToString();
            msgdata["buttonlabelsNum"] = buttonlabels.Length.ToString();

            for (int i = 0; i < buttonlabels.Length; i++)
            {
                string key = "buttonlabel_" + i;
                msgdata[key] = buttonlabels[i];
            }

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

        /// <summary>
        /// Process a XMLRPC Grid Instant Message
        /// </summary>
        /// <param name="request">XMLRPC parameters
        /// </param>
        /// <returns>Nothing much</returns>
        protected virtual XmlRpcResponse processXMLRPCGridDialog(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            bool decodingSuccessful = true;
            bool deliverSuccessful = false;
            UUID avatarID = UUID.Zero;
            UUID objectID = UUID.Zero;
            UUID textureID = UUID.Zero;
            UUID ownerID = UUID.Zero;
            string objectName="", ownerFirstName="", ownerLastName="";
            string message="";
            int ch=0;
            string[] buttonlabels = null;

            Hashtable requestData = (Hashtable)request.Params[0];

                            // Check if it's got all the data
            if (requestData.ContainsKey("avatarID")
                    && requestData.ContainsKey("objectName") && requestData.ContainsKey("objectID")
                    && requestData.ContainsKey("ownerID")
                    && requestData.ContainsKey("ownerFirstName") && requestData.ContainsKey("ownerLastName")
                    && requestData.ContainsKey("message") && requestData.ContainsKey("textureID")
                    && requestData.ContainsKey("ch")
                    && requestData.ContainsKey("buttonlabelsNum"))
            {
                try
                {
                    // Do the easy way of validating the UUIDs
                    UUID.TryParse((string)requestData["avatarID"], out avatarID);
                    UUID.TryParse((string)requestData["objectID"], out objectID);
                    UUID.TryParse((string)requestData["textureID"], out textureID);
                    UUID.TryParse((string)requestData["ownerID"], out ownerID);

                    objectName = (string)requestData["objectName"];
                    ownerFirstName = (string)requestData["ownerFirstName"];
                    ownerLastName = (string)requestData["ownerLastName"];
                    ch = Convert.ToInt32((string)requestData["ch"]);

                    int buttonlabelsNum = Convert.ToInt32((string)requestData["buttonlabelsNum"]);
                    buttonlabels = new string[buttonlabelsNum];

                    for (int i = 0; i < buttonlabelsNum; i++)
                    {
                        string key = "buttonlabel_" + i;
                        if (requestData.ContainsKey(key))
                        {
                            buttonlabels[i] = (string)requestData[key];
                        }
                        else
                        {
                            decodingSuccessful = false;
                            break;
                        }
                    }

                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("Error in processXMLRPCGridDialog: {0}", e.Message);
                    deliverSuccessful = false;
                }

                if (decodingSuccessful)
                {
                    //Deliver the dialog to the client
                    foreach (Scene scene in m_Scenes)
                    {
                        if (scene.Entities.ContainsKey(avatarID) &&
                                scene.Entities[avatarID] is ScenePresence)
                        {
                            ScenePresence user =
                                    (ScenePresence)scene.Entities[avatarID];

                            if (!user.IsChildAgent)
                            {
                                user.ControllingClient.SendDialog(objectName, objectID, ownerID, ownerFirstName, ownerLastName, message, textureID, ch, buttonlabels);
                                deliverSuccessful = true;
                            }
                        }
                    }
                }
            }

            //Send response back to region calling if it was successful
            // calling region uses this to know when to look up a user's location again.
            XmlRpcResponse resp = new XmlRpcResponse();
            Hashtable respdata = new Hashtable();
            if (deliverSuccessful)
                respdata["success"] = "TRUE";
            else
                respdata["success"] = "FALSE";
            resp.Value = respdata;

            return resp;
        }

        #endregion //GridCommunication
    }
}

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
using System.Collections.Specialized;
using System.Net;
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Services.Connectors.SimianGrid
{
    /// <summary>
    /// Handles logins and teleports
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class RezAvatar : INonSharedRegionModule
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private GridRegion m_gridRegion;
        private ISimulationService m_simulationService;

        #region INonSharedRegionModule

        public Type ReplaceableInterface { get { return null; } }
        public void RegionLoaded(Scene scene) { }
        public void Close() { }

        public RezAvatar() { }
        public string Name { get { return "RezAvatar"; } }
        
        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            m_gridRegion = new GridRegion
            {
                RegionID = scene.RegionInfo.RegionID,
                RegionLocX = (int)scene.RegionInfo.RegionLocX,
                RegionLocY = (int)scene.RegionInfo.RegionLocY,
                RegionName = scene.RegionInfo.RegionName
            };

            if (Simian.IsSimianEnabled(scene.Config, "UserAccountServices", "SimianUserAccountServiceConnector"))
            {
                m_simulationService = scene.RequestModuleInterface<ISimulationService>();
                if (m_simulationService != null)
                    m_simulationService = m_simulationService.GetInnerService();

                string urlFriendlySceneName = WebUtil.UrlEncode(scene.RegionInfo.RegionName);

                MainServer.Instance.AddStreamHandler(new HttpStreamHandler("application/xml+llsd", "GET", "/scenes/" + urlFriendlySceneName + "/public_region_seed_capability",
                    PublicRegionSeedCapabilityHandler));
                MainServer.Instance.AddStreamHandler(new HttpStreamHandler(null, "POST", "/scenes/" + urlFriendlySceneName + "/rez_avatar/request",
                    RezAvatarRequestHandler));
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (Simian.IsSimianEnabled(scene.Config, "UserAccountServices", this.Name))
            {
                string urlFriendlySceneName = WebUtil.UrlEncode(scene.RegionInfo.RegionName);

                MainServer.Instance.RemoveStreamHandler("GET", "/scenes/" + urlFriendlySceneName + "/public_region_seed_capability");
                MainServer.Instance.RemoveStreamHandler("GET", "/scenes/" + urlFriendlySceneName + "/rez_avatar/request");
            }

            m_scene = null;
        }

        #endregion INonSharedRegionModule

        public RezAvatar(IConfigSource source)
        {
        }

        public void Initialise(IConfigSource source)
        {
        }

        private void PublicRegionSeedCapabilityHandler(OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            // Build a seed capability response that contains the rez_avatar/request capability (a hardcoded URL)
            OSDMap responseMap = new OSDMap();

            Uri httpAddress = new Uri("http://" + m_scene.RegionInfo.ExternalHostName + ":" + m_scene.RegionInfo.HttpPort + "/");
            string urlFriendlySceneName = WebUtil.UrlEncode(m_scene.RegionInfo.RegionName);

            OSDMap capabilities = new OSDMap();
            capabilities["rez_avatar/request"] = OSD.FromUri(new Uri(httpAddress, "/scenes/" + urlFriendlySceneName + "/rez_avatar/request"));

            responseMap["capabilities"] = capabilities;

            WebUtil.SendJSONResponse(httpResponse, responseMap);
        }

        private void RezAvatarRequestHandler(OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            OSDMap requestMap = null;
            try { requestMap = OSDParser.Deserialize(httpRequest.InputStream) as OSDMap; }
            catch { }

            Vector3 lookAt = Vector3.UnitX;
            RegionInfo regInfo = m_scene.RegionInfo;

            // Unpack the rez_avatar/request into an AgentCircuitData structure
            AgentCircuitData agentCircuit = BuildAgentCircuitFromRezAvatarRequest(requestMap);

            OSDMap responseMap = new OSDMap();

            if (agentCircuit != null && agentCircuit.AgentID != UUID.Zero)
            {
                if (agentCircuit.startpos == Vector3.Zero)
                {
                    m_log.Info("rez_avatar/request did not contain a position, setting to default");
                    agentCircuit.startpos = new Vector3(128f, 128f, 25f);
                }

                // Attempt to create an agent from the AgentCircuitData info
                string reason;
                if (m_simulationService.CreateAgent(m_gridRegion, agentCircuit, agentCircuit.teleportFlags, out reason))
                {
                    // Build the seed capability URL for this agent
                    Uri seedCapability = new Uri("http://" + regInfo.ExternalHostName + ":" + regInfo.HttpPort + "/CAPS/" + agentCircuit.CapsPath + "0000/");

                    m_log.Info("rez_avatar/request created agent " + agentCircuit.firstname + " " + agentCircuit.lastname +
                        " with circuit code " + agentCircuit.circuitcode + " and seed capability " + seedCapability);

                    IPAddress externalAddress = regInfo.ExternalEndPoint.Address;
                    uint regionX, regionY;
                    Utils.LongToUInts(regInfo.RegionHandle, out regionX, out regionY);

                    responseMap["connect"] = OSD.FromBoolean(true);
                    responseMap["agent_id"] = OSD.FromUUID(agentCircuit.AgentID);
                    responseMap["sim_host"] = OSD.FromString(externalAddress.ToString());
                    responseMap["sim_port"] = OSD.FromInteger(regInfo.ExternalEndPoint.Port);
                    responseMap["region_seed_capability"] = OSD.FromUri(seedCapability);
                    responseMap["position"] = OSD.FromVector3(agentCircuit.startpos);
                    responseMap["look_at"] = OSD.FromVector3(lookAt);

                    // Region information
                    responseMap["region_id"] = OSD.FromUUID(regInfo.RegionID);
                    responseMap["region_x"] = OSD.FromInteger(regionX);
                    responseMap["region_y"] = OSD.FromInteger(regionY);
                }
                else
                {
                    m_log.Warn("Denied rez_avatar/request for " + agentCircuit.firstname + " " + agentCircuit.lastname + ": " + reason);
                    responseMap["message"] = OSD.FromString(reason);
                }
            }
            else
            {
                responseMap["message"] = OSD.FromString("Invalid or incomplete request");
            }

            WebUtil.SendJSONResponse(httpResponse, responseMap);
        }

        private static AgentCircuitData BuildAgentCircuitFromRezAvatarRequest(OSDMap request)
        {
            AgentCircuitData agentCircuit = new AgentCircuitData();
            agentCircuit.AgentID = request["agent_id"].AsUUID();
            agentCircuit.BaseFolder = request["base_folder"].AsUUID();
            agentCircuit.CapsPath = request["caps_path"].AsString();
            agentCircuit.child = request["child_agent"].AsBoolean();
            agentCircuit.circuitcode = request["circuit_code"].AsUInteger();
            agentCircuit.firstname = request["first_name"].AsString();
            agentCircuit.InventoryFolder = request["inventory_folder"].AsUUID();
            agentCircuit.lastname = request["last_name"].AsString();
            agentCircuit.SecureSessionID = request["secure_session_id"].AsUUID();
            agentCircuit.ServiceSessionID = String.Empty;
            agentCircuit.ServiceURLs = new Dictionary<string, object>(0);
            agentCircuit.SessionID = request["session_id"].AsUUID();
            agentCircuit.startpos = request["position"].AsVector3();
            agentCircuit.teleportFlags = request["teleport_flags"].AsUInteger();
            if (agentCircuit.teleportFlags == 0)
                agentCircuit.teleportFlags = (uint)TeleportFlags.ViaLogin;

            #region Children Seed Caps

            if (request["children_seeds"].Type == OSDType.Array)
            {
                OSDArray childrenSeeds = (OSDArray)request["children_seeds"];
                agentCircuit.ChildrenCapSeeds = new Dictionary<ulong, string>(childrenSeeds.Count);
                for (int i = 0; i < childrenSeeds.Count; i++)
                {
                    if (childrenSeeds[i].Type == OSDType.Map)
                    {
                        OSDMap pair = (OSDMap)childrenSeeds[i];
                        ulong handle;
                        if (!UInt64.TryParse(pair["handle"].AsString(), out handle))
                            continue;
                        string seed = pair["seed"].AsString();
                        if (!agentCircuit.ChildrenCapSeeds.ContainsKey(handle))
                            agentCircuit.ChildrenCapSeeds.Add(handle, seed);
                    }
                }
            }
            else
            {
                agentCircuit.ChildrenCapSeeds = new Dictionary<ulong, string>(0);
            }

            #endregion Children Seed Caps

            #region Appearance

            agentCircuit.Appearance = new AvatarAppearance(agentCircuit.AgentID);
            agentCircuit.Appearance.Serial = request["appearance_serial"].AsInteger();
            if (request["wearables"].Type == OSDType.Array)
            {
                OSDArray wearables = (OSDArray)request["wearables"];
                for (int i = 0; i < wearables.Count / 2; i++)
                {
                    agentCircuit.Appearance.Wearables[i].ItemID = wearables[i * 2].AsUUID();
                    agentCircuit.Appearance.Wearables[i].AssetID = wearables[(i * 2) + 1].AsUUID();
                }
            }

            if (request["attachments"].Type == OSDType.Array)
            {
                OSDArray attachArray = (OSDArray)request["attachments"];
                List<AttachmentData> attachments = new List<AttachmentData>(attachArray.Count);
                for (int i = 0; i < attachArray.Count; i++)
                {
                    if (attachArray[i].Type == OSDType.Map)
                        attachments.Add(new AttachmentData((OSDMap)attachArray[i]));
                }
                agentCircuit.Appearance.SetAttachments(attachments.ToArray());
            }

            #endregion Appearance

            return agentCircuit;
        }

        private static OSDMap BuildRezAvatarRequestFromAgentCircuit(AgentCircuitData agentCircuit)
        {
            return new OSDMap();
        }
    }
}

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
using System.IO;
using System.Net;
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Services.Connectors.SimianGrid
{
    /// <summary>
    /// Connects region registration and neighbor lookups to the SimianGrid
    /// backend
    /// </summary>
    public class SimianGridServiceConnector : IGridService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;
//        private bool m_Enabled = false;

        public SimianGridServiceConnector() { }
        public SimianGridServiceConnector(string serverURI)
        {
            m_ServerURI = serverURI.TrimEnd('/');
        }

        public SimianGridServiceConnector(IConfigSource source)
        {
            CommonInit(source);
        }

        public void Initialise(IConfigSource source)
        {
            CommonInit(source);
        }

        private void CommonInit(IConfigSource source)
        {
            IConfig gridConfig = source.Configs["GridService"];
            if (gridConfig == null)
            {
                m_log.Error("[SIMIAN GRID CONNECTOR]: GridService missing from OpenSim.ini");
                throw new Exception("Grid connector init error");
            }

            string serviceUrl = gridConfig.GetString("GridServerURI");
            if (String.IsNullOrEmpty(serviceUrl))
            {
                m_log.Error("[SIMIAN GRID CONNECTOR]: No Server URI named in section GridService");
                throw new Exception("Grid connector init error");
            }
            
            if (!serviceUrl.EndsWith("/") && !serviceUrl.EndsWith("="))
                serviceUrl = serviceUrl + '/';
            m_ServerURI = serviceUrl;
//            m_Enabled = true;
        }

        #region IGridService

        public string RegisterRegion(UUID scopeID, GridRegion regionInfo)
        {
            Vector3d minPosition = new Vector3d(regionInfo.RegionLocX, regionInfo.RegionLocY, 0.0);
            Vector3d maxPosition = minPosition + new Vector3d(Constants.RegionSize, Constants.RegionSize, 4096.0);

            OSDMap extraData = new OSDMap
            {
                { "ServerURI", OSD.FromString(regionInfo.ServerURI) },
                { "InternalAddress", OSD.FromString(regionInfo.InternalEndPoint.Address.ToString()) },
                { "InternalPort", OSD.FromInteger(regionInfo.InternalEndPoint.Port) },
                { "ExternalAddress", OSD.FromString(regionInfo.ExternalEndPoint.Address.ToString()) },
                { "ExternalPort", OSD.FromInteger(regionInfo.ExternalEndPoint.Port) },
                { "MapTexture", OSD.FromUUID(regionInfo.TerrainImage) },
                { "Access", OSD.FromInteger(regionInfo.Access) },
                { "RegionSecret", OSD.FromString(regionInfo.RegionSecret) },
                { "EstateOwner", OSD.FromUUID(regionInfo.EstateOwner) },
                { "Token", OSD.FromString(regionInfo.Token) }
            };

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddScene" },
                { "SceneID", regionInfo.RegionID.ToString() },
                { "Name", regionInfo.RegionName },
                { "MinPosition", minPosition.ToString() },
                { "MaxPosition", maxPosition.ToString() },
                { "Address", regionInfo.ServerURI },
                { "Enabled", "1" },
                { "ExtraData", OSDParser.SerializeJsonString(extraData) }
            };

            OSDMap response = WebUtil.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
                return String.Empty;
            else
                return "Region registration for " + regionInfo.RegionName + " failed: " + response["Message"].AsString();
        }

        public bool DeregisterRegion(UUID regionID)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddScene" },
                { "SceneID", regionID.ToString() },
                { "Enabled", "0" }
            };

            OSDMap response = WebUtil.PostToService(m_ServerURI, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("[SIMIAN GRID CONNECTOR]: Region deregistration for " + regionID + " failed: " + response["Message"].AsString());

            return success;
        }

        public List<GridRegion> GetNeighbours(UUID scopeID, UUID regionID)
        {
            const int NEIGHBOR_RADIUS = 128;

            GridRegion region = GetRegionByUUID(scopeID, regionID);

            if (region != null)
            {
                List<GridRegion> regions = GetRegionRange(scopeID,
                    region.RegionLocX - NEIGHBOR_RADIUS, region.RegionLocX + (int)Constants.RegionSize + NEIGHBOR_RADIUS,
                    region.RegionLocY - NEIGHBOR_RADIUS, region.RegionLocY + (int)Constants.RegionSize + NEIGHBOR_RADIUS);

                for (int i = 0; i < regions.Count; i++)
                {
                    if (regions[i].RegionID == regionID)
                    {
                        regions.RemoveAt(i);
                        break;
                    }
                }

//                m_log.Debug("[SIMIAN GRID CONNECTOR]: Found " + regions.Count + " neighbors for region " + regionID);
                return regions;
            }

            return new List<GridRegion>(0);
        }

        public GridRegion GetRegionByUUID(UUID scopeID, UUID regionID)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetScene" },
                { "SceneID", regionID.ToString() }
            };

            // m_log.DebugFormat("[SIMIAN GRID CONNECTOR] request region with uuid {0}",regionID.ToString());

            OSDMap response = WebUtil.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                // m_log.DebugFormat("[SIMIAN GRID CONNECTOR] uuid request successful {0}",response["Name"].AsString());
                return ResponseToGridRegion(response);
            }
            else
            {
                m_log.Warn("[SIMIAN GRID CONNECTOR]: Grid service did not find a match for region " + regionID);
                return null;
            }
        }

        public GridRegion GetRegionByPosition(UUID scopeID, int x, int y)
        {
            // Go one meter in from the requested x/y coords to avoid requesting a position
            // that falls on the border of two sims
            Vector3d position = new Vector3d(x + 1, y + 1, 0.0);

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetScene" },
                { "Position", position.ToString() },
                { "Enabled", "1" }
            };

            // m_log.DebugFormat("[SIMIAN GRID CONNECTOR] request grid at {0}",position.ToString());
            
            OSDMap response = WebUtil.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                // m_log.DebugFormat("[SIMIAN GRID CONNECTOR] position request successful {0}",response["Name"].AsString());
                return ResponseToGridRegion(response);
            }
            else
            {
                // m_log.InfoFormat("[SIMIAN GRID CONNECTOR]: Grid service did not find a match for region at {0},{1}",
                //     x / Constants.RegionSize, y / Constants.RegionSize);
                return null;
            }
        }

        public GridRegion GetRegionByName(UUID scopeID, string regionName)
        {
            List<GridRegion> regions = GetRegionsByName(scopeID, regionName, 1);

            m_log.Debug("[SIMIAN GRID CONNECTOR]: Got " + regions.Count + " matches for region name " + regionName);

            if (regions.Count > 0)
                return regions[0];

            return null;
        }

        public List<GridRegion> GetRegionsByName(UUID scopeID, string name, int maxNumber)
        {
            List<GridRegion> foundRegions = new List<GridRegion>();

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetScenes" },
                { "NameQuery", name },
                { "Enabled", "1" }
            };
            if (maxNumber > 0)
                requestArgs["MaxNumber"] = maxNumber.ToString();

            // m_log.DebugFormat("[SIMIAN GRID CONNECTOR] request regions with name {0}",name);

            OSDMap response = WebUtil.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                // m_log.DebugFormat("[SIMIAN GRID CONNECTOR] found regions with name {0}",name);

                OSDArray array = response["Scenes"] as OSDArray;
                if (array != null)
                {
                    for (int i = 0; i < array.Count; i++)
                    {
                        GridRegion region = ResponseToGridRegion(array[i] as OSDMap);
                        if (region != null)
                            foundRegions.Add(region);
                    }
                }
            }

            return foundRegions;
        }

        public List<GridRegion> GetRegionRange(UUID scopeID, int xmin, int xmax, int ymin, int ymax)
        {
            List<GridRegion> foundRegions = new List<GridRegion>();

            Vector3d minPosition = new Vector3d(xmin, ymin, 0.0);
            Vector3d maxPosition = new Vector3d(xmax, ymax, 4096.0);

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetScenes" },
                { "MinPosition", minPosition.ToString() },
                { "MaxPosition", maxPosition.ToString() },
                { "Enabled", "1" }
            };

            //m_log.DebugFormat("[SIMIAN GRID CONNECTOR] request regions by range {0} to {1}",minPosition.ToString(),maxPosition.ToString());
            

            OSDMap response = WebUtil.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                OSDArray array = response["Scenes"] as OSDArray;
                if (array != null)
                {
                    for (int i = 0; i < array.Count; i++)
                    {
                        GridRegion region = ResponseToGridRegion(array[i] as OSDMap);
                        if (region != null)
                            foundRegions.Add(region);
                    }
                }
            }

            return foundRegions;
        }

        public List<GridRegion> GetDefaultRegions(UUID scopeID)
        {
            // TODO: Allow specifying the default grid location
            const int DEFAULT_X = 1000 * 256;
            const int DEFAULT_Y = 1000 * 256;

            GridRegion defRegion = GetNearestRegion(new Vector3d(DEFAULT_X, DEFAULT_Y, 0.0), true);
            if (defRegion != null)
                return new List<GridRegion>(1) { defRegion };
            else
                return new List<GridRegion>(0);
        }

        public List<GridRegion> GetFallbackRegions(UUID scopeID, int x, int y)
        {
            GridRegion defRegion = GetNearestRegion(new Vector3d(x, y, 0.0), true);
            if (defRegion != null)
                return new List<GridRegion>(1) { defRegion };
            else
                return new List<GridRegion>(0);
        }

        public List<GridRegion> GetHyperlinks(UUID scopeID)
        {
            // Hypergrid/linked regions are not supported
            return new List<GridRegion>();
        }
        
        public int GetRegionFlags(UUID scopeID, UUID regionID)
        {
            const int REGION_ONLINE = 4;

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetScene" },
                { "SceneID", regionID.ToString() }
            };

            // m_log.DebugFormat("[SIMIAN GRID CONNECTOR] request region flags for {0}",regionID.ToString());

            OSDMap response = WebUtil.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                return response["Enabled"].AsBoolean() ? REGION_ONLINE : 0;
            }
            else
            {
                m_log.Warn("[SIMIAN GRID CONNECTOR]: Grid service did not find a match for region " + regionID + " during region flags check");
                return -1;
            }
        }

        #endregion IGridService

        private GridRegion GetNearestRegion(Vector3d position, bool onlyEnabled)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetScene" },
                { "Position", position.ToString() },
                { "FindClosest", "1" }
            };
            if (onlyEnabled)
                requestArgs["Enabled"] = "1";

            OSDMap response = WebUtil.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                return ResponseToGridRegion(response);
            }
            else
            {
                m_log.Warn("[SIMIAN GRID CONNECTOR]: Grid service did not find a match for region at " + position);
                return null;
            }
        }

        private GridRegion ResponseToGridRegion(OSDMap response)
        {
            if (response == null)
                return null;

            OSDMap extraData = response["ExtraData"] as OSDMap;
            if (extraData == null)
                return null;

            GridRegion region = new GridRegion();

            region.RegionID = response["SceneID"].AsUUID();
            region.RegionName = response["Name"].AsString();

            Vector3d minPosition = response["MinPosition"].AsVector3d();
            region.RegionLocX = (int)minPosition.X;
            region.RegionLocY = (int)minPosition.Y;

            Uri httpAddress = response["Address"].AsUri();
            region.ExternalHostName = httpAddress.Host;
            region.HttpPort = (uint)httpAddress.Port;

            region.ServerURI = extraData["ServerURI"].AsString();

            IPAddress internalAddress;
            IPAddress.TryParse(extraData["InternalAddress"].AsString(), out internalAddress);
            if (internalAddress == null)
                internalAddress = IPAddress.Any;

            region.InternalEndPoint = new IPEndPoint(internalAddress, extraData["InternalPort"].AsInteger());
            region.TerrainImage = extraData["MapTexture"].AsUUID();
            region.Access = (byte)extraData["Access"].AsInteger();
            region.RegionSecret = extraData["RegionSecret"].AsString();
            region.EstateOwner = extraData["EstateOwner"].AsUUID();
            region.Token = extraData["Token"].AsString();

            return region;
        }

        #region SYNC SERVER
        /// <summary>
        /// Register the SyncServerID, address and port for a particular endpoint.
        /// The internal address and port are not registered.
        /// </summary>
        /// <param name="gei"></param>
        /// <returns>'true' if the registration was successful and 'false' otherwise</returns>
        public virtual bool RegisterEndpoint(GridEndpointInfo gei)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddEndpoint" },
                { "SyncServerID", gei.syncServerID },
                { "Address", gei.address },
                { "Port", gei.port.ToString() },
            };

            OSDMap response = WebUtil.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                m_log.WarnFormat("{0}: Registration of endpoint {1} at addr={2}:{3} successful",
                        "[SIMIAN GRID CONNECTOR]", gei.syncServerID, gei.address, gei.port.ToString());
                return true;
            }
            m_log.ErrorFormat("{0}: Registration of endpoint {1} at addr={2}:{3} failed: {4}",
                "[SIMIAN GRID CONNECTOR]", gei.syncServerID, gei.address, gei.port.ToString(), response["Message"]);
            return false;
        }

        /// <summary>
        /// Lookup a particular endpoint given the SyncServerID of the endpoint.
        /// </summary>
        /// <param name="syncServerID"></param>
        /// <returns>endpoint information or 'null' if not found</returns>
        public virtual GridEndpointInfo LookupEndpoint(string syncServerID)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetEndpoint" },
                { "SyncServerID", syncServerID },
            };

            OSDMap response = WebUtil.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                GridEndpointInfo gai = new GridEndpointInfo();
                gai.syncServerID = response["SyncServerID"].AsString();
                gai.address = response["Address"].AsString();
                gai.port = (uint)response["Port"].AsInteger();
                gai.internalAddress = response["InternalAddress"].AsString();
                gai.internalPort = (uint)response["InternalPort"].AsInteger();
                return gai;
            }
            m_log.ErrorFormat("{0}: Lookup of endpoint failed: {1}",
                        "[SIMIAN GRID CONNECTOR]", response["Message"]);
            return null;
        }

        /// <summary>
        /// Register an actor association with a SyncServer.
        /// </summary>
        /// <param name="actorID">unique identification of the actor (actor's "name")</param>
        /// <param name="actorType">actor type identification string</param>
        /// <param name="syncServerID">sync server identification</param>
        /// <returns>'true' if registration successful. 'false' otherwise</returns>
        public virtual bool RegisterActor(string actorID, string actorType, string syncServerID)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddActor" },
                { "ActorID", actorID },
                { "ActorType", actorType },
                { "SyncServerID", syncServerID },
            };

            OSDMap response = WebUtil.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                m_log.WarnFormat("{0}: Registration of actor {1} of type {2} successful",
                        "[SIMIAN GRID CONNECTOR]", actorID, actorType);
                return true;
            }
            m_log.ErrorFormat("{0}: Registration of actor {1} of type {2} failed: {3}",
                "[SIMIAN GRID CONNECTOR]", actorID, actorType, response["Message"]);
            return false;
            return false;
        }

        /// <summary>
        /// Register a quark on a SyncServer.
        /// </summary>
        /// <param name="syncServerID"></param>
        /// <param name="locX"></param>
        /// <param name="locY"></param>
        /// <returns>'true' if registration successful. 'false' otherwise</returns>
        public virtual bool RegisterQuark(string syncServerID, uint locX, uint locY)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddQuark" },
                { "SyncServerID", syncServerID },
                { "LocX", locX.ToString() },
                { "LocY", locY.ToString() },
            };

            OSDMap response = WebUtil.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                m_log.WarnFormat("{0}: Registration of quark at {1}/{2} successful",
                        "[SIMIAN GRID CONNECTOR]", locX.ToString(), locY.ToString());
                return true;
            }
            m_log.ErrorFormat("{0}: Registration of quark at {1}/{2} failed: {3}",
                        "[SIMIAN GRID CONNECTOR]", locX.ToString(), locY.ToString(), response["Message"]);
            return false;
        }

        /// <summary>
        /// Given a quark location, return all the endpoints associated with that quark;
        /// </summary>
        /// <param name="locX">quark home X location</param>
        /// <param name="locY">quark home Y location</param>
        /// <returns>list of endpoints and actor types associated with the quark</returns>
        public virtual List<GridEndpointInfo> LookupQuark(uint locX, uint locY)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetQuark" },
                { "LocX", locX.ToString() },
                { "LocY", locY.ToString() }
            };
            return LookupQuark(requestArgs);
        }

        /// <summary>
        /// Given a quark location and an actor type, return all endpoints associated with
        /// the quark that have that actor. This is equivilent to the previous request but
        /// filtered by the actor type.
        /// </summary>
        /// <param name="locX">quark home X location</param>
        /// <param name="locY">quark home Y location</param>
        /// <param name="actorType">actor definition string</param>
        /// <returns>list of endpoints having that actor and the quark</returns>
        public virtual List<GridEndpointInfo> LookupQuark(uint locX, uint locY, string actorType)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetQuark" },
                { "LocX", locX.ToString() },
                { "LocY", locY.ToString() },
                { "ActorType", actorType }
            };
            return LookupQuark(requestArgs);
        }

        private List<GridEndpointInfo> LookupQuark(NameValueCollection requestArgs)
        {
            OSDMap response = WebUtil.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                List<GridEndpointInfo> lgai = new List<GridEndpointInfo>();
                OSDArray gridEndpoints = (OSDArray)response["Endpoints"];
                m_log.WarnFormat("{0}: Lookup of quark successful. {1} addresses",
                        "[SIMIAN GRID CONNECTOR]", gridEndpoints.Count);
                for (int ii = 0; ii < gridEndpoints.Count; ii++)
                {
                    OSDMap thisEndpoint = (OSDMap)gridEndpoints[ii];
                    GridEndpointInfo gai = new GridEndpointInfo();
                    gai.syncServerID = thisEndpoint["SyncServerID"].AsString();
                    gai.actorType = thisEndpoint["ActorType"].AsString();
                    gai.address = thisEndpoint["Address"].AsString();
                    gai.port = (uint)thisEndpoint["Port"].AsInteger();
                    lgai.Add(gai);
                }
                return lgai;
            }
            m_log.ErrorFormat("{0}: Lookup of quark failed: {1}",
                        "[SIMIAN GRID CONNECTOR]", response["Message"]);
            return null;
        }

        // Clean up the information for this endpoint. Removes both the endpoint
        // information from the Endpoint table but also removes ALL the quarks associated
        // with the endpoint.
        public virtual bool CleanUpEndpoint(string syncServerID)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "RemoveEndpoint" },
                { "SyncServerID", syncServerID },
            };

            OSDMap response = WebUtil.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                requestArgs = new NameValueCollection
                {
                    { "RequestMethod", "RemoveQuark" },
                    { "SyncServerID", syncServerID },
                };

                response = WebUtil.PostToService(m_ServerURI, requestArgs);
                if (response["Success"].AsBoolean())
                {
                    return true;
                }
                m_log.ErrorFormat("{0}: removal of quarks for Endpoint failed: {1}",
                            "[SIMIAN GRID CONNECTOR]", response["Message"]);
                return false;
            }
            m_log.ErrorFormat("{0}: removal of Endpoint failed: {1}",
                        "[SIMIAN GRID CONNECTOR]", response["Message"]);
            return false;
        }
        #endregion SYNC SERVER
    }
}

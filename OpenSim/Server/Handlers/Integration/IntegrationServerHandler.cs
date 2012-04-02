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

using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using System;
using System.Reflection;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;

namespace OpenSim.Server.Handlers.Integration
{
    public class IntegrationServerHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IIntegrationService m_IntegrationService;

        public IntegrationServerHandler(IIntegrationService service) :
                base("POST", "/integration")
        {
            m_IntegrationService = service;
        }

        public override byte[] Handle(string path, Stream requestData, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            StreamReader sr = new StreamReader(requestData);
            string body = sr.ReadToEnd();
            sr.Close();
            body = body.Trim();

            try
            {
                OSDMap request = null;
                if (ServerUtils.ParseStringToOSDMap(body, out request) == false)
                    return FailureResult();
                
                // Dictionary<string, object> request = ServerUtils.ParseQueryString(body);

                if (!request.ContainsKey("command"))
                    return FailureResult("Error, no command defined!");
                string command = request["command"].AsString();

                // command...
                switch (command)
                {
                    // agent
                    case "verify_agent_ssession":
                        return HandleVerifyAgentSession(request);

                    case "verify_agent_region":
                        return FailureResult("Not Implemented");

                    default:
                        m_log.DebugFormat("[IntegrationHandler]: unknown method {0} request {1}", command.Length, command);
                        return FailureResult("IntegrationHandler: Unrecognized method requested!");
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[IntegrationHandler]: Exception {0}", e);
            }

            return FailureResult();
        }

        #region Handlers
        /// <summary>
        /// Verifies the agent to external applications.
        /// </summary>
        /// <returns>
        /// UUID of the agent.
        /// </returns>
        /// <param name='request'>
        /// request - Send SecureSessionID and optionally Encoding=xml for xml Output
        /// </param>
        byte[] HandleVerifyAgentSession(OSDMap request)
        {
            UUID s_session = UUID.Zero;

            if (!request.ContainsKey("SecureSessionID"))
                return FailureResult();

            if (!UUID.TryParse(request["SecureSessionID"].AsString(), out s_session))
                return FailureResult();

            PresenceInfo pinfo = m_IntegrationService.VerifyAgent(s_session);

            OSDMap result = new OSDMap();

            if (pinfo == null)
                result["agent_id"] = OSD.FromUUID(UUID.Zero);
            else
                result["agent_id"] = OSD.FromString(pinfo.UserID.ToString());

            return Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(result));
        }
        #endregion Handlers

        #region utility
        private byte[] FailureResult()
        {
            return FailureResult(String.Empty);
        }

        private byte[] FailureResult(string msg)
        {
            OSDMap doc = new OSDMap(2);
            doc["Result"] = OSD.FromString("Failure");
            doc["Message"] = OSD.FromString(msg);

            return DocToBytes(doc);
        }

        private byte[] DocToBytes(OSDMap doc)
        {
            return Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(doc));
        }
        #endregion utility
    }
}

/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using libsecondlife;
using OpenSim.Framework.Configuration;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;

namespace OpenSim.Framework.Types
{
    public class RegionInfo : ConfigurationMember
    {
        public LLUUID SimUUID = new LLUUID();
        public string RegionName = "";

        private IPEndPoint m_internalEndPoint;
        public IPEndPoint InternalEndPoint
        {
            get
            {
                return m_internalEndPoint;
            }
        }

        public IPEndPoint ExternalEndPoint
        {
            get
            {
                // Old one defaults to IPv6
                //return new IPEndPoint( Dns.GetHostAddresses( m_externalHostName )[0], m_internalEndPoint.Port );

                // New method favors IPv4
                IPAddress ia = null;
                foreach (IPAddress Adr in Dns.GetHostAddresses(m_externalHostName))
                {
                    if (ia == null)
                        ia = Adr;

                    if (Adr.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ia = Adr;
                        break;
                    }

                }
                
                return new IPEndPoint(ia, m_internalEndPoint.Port);
            }
        }
        
        private string m_externalHostName;
        public string ExternalHostName
        {
            get
            {
                return m_externalHostName;
            }
        }

        private uint? m_regionLocX;
        public uint RegionLocX
        {
            get
            {
                return m_regionLocX.Value;
            }
        }

        private uint? m_regionLocY;
        public uint RegionLocY
        {
            get
            {
                return m_regionLocY.Value;
            }
        }

        private ulong? m_regionHandle;
        public ulong RegionHandle
        {
            get
            {
                if (!m_regionHandle.HasValue)
                {
                    m_regionHandle = Util.UIntsToLong((RegionLocX * 256), (RegionLocY * 256));
                }

                return m_regionHandle.Value;
            }
        }

        public string DataStore = "";
        public bool isSandbox = false;

        public LLUUID MasterAvatarAssignedUUID = new LLUUID();
        public string MasterAvatarFirstName = "";
        public string MasterAvatarLastName = "";
        public string MasterAvatarSandboxPassword = "";

        public EstateSettings estateSettings;

        public RegionInfo()
        {
            estateSettings = new EstateSettings();
            this.setConfigurationDescription("REGION INFORMATION");
            this.addConfigurationOption("SimUUID", "UUID of Simulator (Default is recommended, random UUID)", LLUUID.Random().ToString());
            this.addConfigurationOption("SimName", "Simulator Name", "OpenSim Test");
            this.addConfigurationOption("SimLocationX", "Grid Location (X Axis)", "1000");
            this.addConfigurationOption("SimLocationY", "Grid Location (Y Axis)", "1000");
            this.addConfigurationOption("Datastore", "Filename for local storage", "localworld.yap");
            this.addConfigurationOption("InternalIPAddress", "Internal IP Address for incoming UDP client connections", "0.0.0.0");
            this.addConfigurationOption("InternalIPPort", "Internal IP Port for incoming UDP client connections", "9000");
            this.addConfigurationOption("ExternalHostName", "External Host Name", "127.0.0.1");
            this.addConfigurationOption("TerrainFile", "Default Terrain File", "default.r32");
            this.addConfigurationOption("TerrainMultiplier", "Terrain Height Multiplier", "60.0");
            this.addConfigurationOption("MasterAvatarFirst", "First Name of Master Avatar", "Test");
            this.addConfigurationOption("MasterAvatarLast", "Last Name of Master Avatar", "User");
        }
        public RegionInfo(uint regionLocX, uint regionLocY, IPEndPoint internalEndPoint, string externalUri)
            : this()
        {
            m_regionLocX = regionLocX;
            m_regionLocY = regionLocY;

            m_internalEndPoint = internalEndPoint;
            m_externalHostName = externalUri;
        }

        public override void handleConfigurationItem(string configuration_key, string configuration_value)
        {
            switch (configuration_key)
            {
                case "SimUUID":
                    this.SimUUID = new LLUUID(configuration_value);
                    break;
                case "SimName":
                    this.RegionName = configuration_value;
                    break;
                case "SimLocationX":
                    this.m_regionLocX = Convert.ToUInt32(configuration_value);
                    break;
                case "SimLocationY":
                    this.m_regionLocY = Convert.ToUInt32(configuration_value);
                    break;
                case "Datastore":
                    this.DataStore = configuration_value;
                    break;
                case "InternalIPAddress":
                    
                    IPAddress address;
                    if (IPAddress.TryParse(configuration_value, out address))
                    {
                        this.m_internalEndPoint = new IPEndPoint(address, 0);
                    }
                    else
                    {
                        MainLog.Instance.Error("Invalid Internal IP Address. Using default (0.0.0.0).");
                        IPAddress.TryParse("0.0.0.0", out address);
                        this.m_internalEndPoint = new IPEndPoint(address, 0);
                    }
                    break;
                case "InternalIPPort":
                    this.m_internalEndPoint.Port = Convert.ToInt32(configuration_value);
                    break;
                case "ExternalHostName":
                    this.m_externalHostName = configuration_value;
                    break;
                case "TerrainFile":
                    this.estateSettings.terrainFile = configuration_value;
                    break;
                case "TerrainMultiplier":
                    this.estateSettings.terrainMultiplier = Convert.ToDouble(configuration_value);
                    break;
                case "MasterAvatarFirst":
                    this.MasterAvatarFirstName = configuration_value;
                    break;
                case "MasterAvatarLast":
                    this.MasterAvatarLastName = configuration_value;
                    break;
            }
        }
        
    }
}

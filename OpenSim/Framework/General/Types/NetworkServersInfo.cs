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
using OpenSim.Framework.Configuration;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;

namespace OpenSim.Framework.Types
{
    public class NetworkServersInfo : ConfigurationMember
    {
        public string AssetURL = "http://127.0.0.1:8003/";
        public string AssetSendKey = "";

        public string GridURL = "";
        public string GridSendKey = "";
        public string GridRecvKey = "";
        public string UserURL = "";
        public string UserSendKey = "";
        public string UserRecvKey = "";
        public bool isSandbox;

        public uint DefaultHomeLocX = 0;
        public uint DefaultHomeLocY = 0;

        public int HttpListenerPort = 9000;
        public int RemotingListenerPort = 8895;

        public NetworkServersInfo()
        {
            this.setConfigurationDescription("NETWORK SERVERS INFORMATION");
            this.setConfigurationFilename("network_servers_information.xml");

            this.addConfigurationOption("HttpListenerPort","HTTP Listener Port","9000");
            this.addConfigurationOption("RemotingListenerPort","Remoting Listener Port","8895");
            this.addConfigurationOption("DefaultLocationX","Default Home Location (X Axis)","1000");
            this.addConfigurationOption("DefaultLocationY","Default Home Location (Y Axis)","1000");

            this.addConfigurationOption("GridServerURL","Grid Server URL","http://127.0.0.1:8001");
            this.addConfigurationOption("GridSendKey","Key to send to grid server","null");
            this.addConfigurationOption("GridRecvKey","Key to expect from grid server","null");

            this.addConfigurationOption("UserServerURL","User Server URL","http://127.0.0.1:8002");
            this.addConfigurationOption("UserSendKey","Key to send to user server","null");
            this.addConfigurationOption("UserRecvKey","Key to expect from user server","null");

            this.addConfigurationOption("AssetServerURL","Asset Server URL","http://127.0.0.1:8003");
        }

        public override void handleConfigurationItem(string configuration_key, string configuration_value)
        {
            switch(configuration_key)
            {
                case "HttpListenerPort":
                    this.HttpListenerPort = Convert.ToInt32(configuration_value);
                    break;
                case "RemotingListenerPort":
                    this.RemotingListenerPort = Convert.ToInt32(configuration_value);
                    break;
                case "DefaultLocationX":
                    this.DefaultHomeLocX = Convert.ToUInt32(configuration_value);
                    break;
                case "DefaultLocationY":
                    this.DefaultHomeLocY = Convert.ToUInt32(configuration_value);
                    break;
                case "GridServerURL":
                    this.GridURL = configuration_value;
                    break;
                case "GridSendKey":
                    this.GridSendKey = configuration_value;
                    break;
                case "GridRecvKey":
                    this.GridRecvKey = configuration_value;
                    break;
                case "UserServerURL":
                    this.UserURL = configuration_value;
                    break;
                case "UserSendKey":
                    this.UserSendKey = configuration_value;
                    break;
                case "UserRecvKey":
                    this.UserRecvKey = configuration_value;
                    break;
                case "AssetServerURL":
                    this.AssetURL = configuration_value;
                    break;
            }
        }
    }
}

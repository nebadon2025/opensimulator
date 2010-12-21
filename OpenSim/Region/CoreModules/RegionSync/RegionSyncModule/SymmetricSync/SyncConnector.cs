/*
 * Copyright (c) Contributors: TO BE FILLED
 */


using System.Collections.Generic;
using System.Net.Sockets;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    public class SyncConnector
    {
        private TcpClient m_tcpConnection = null;

        public SyncConnector(TcpClient tcpclient)
        {
            m_tcpConnection = tcpclient;
        }
    }
}
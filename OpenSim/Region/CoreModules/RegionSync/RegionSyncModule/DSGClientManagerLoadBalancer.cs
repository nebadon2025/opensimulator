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
using log4net;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    class DSGClientManagerLoadBalancer
    {
        private List<RegionSyncClientView> m_cvs = new List<RegionSyncClientView>();
        private ILog m_log;
        private int m_maxClientsPerClientManager;
        private Scene m_scene;

        // The list of clients and the threads handling IO for each client
        // The list is read most of the time and only updated when a new client manager
        // connects, so we just replace the list when it changes. Iterators on this
        // list need to be able to handle if an element is shutting down.
        private object m_clientview_lock = new object();
        private HashSet<RegionSyncClientView> m_client_views = new HashSet<RegionSyncClientView>();

        public int Count
        {
            get
            {
                return m_client_views.Count;
            }
        }

        public void ReportStats(System.IO.TextWriter tw)
        {

        }

        public DSGClientManagerLoadBalancer(int maxClientsPerClientManager, Scene scene)
        {
            m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            m_maxClientsPerClientManager = maxClientsPerClientManager;
            m_scene = scene;
        }

        // Add the client view to the list and increment synced client counter
        public void AddSyncedClient(RegionSyncClientView rscv)
        {
            lock (m_clientview_lock)
            {
                HashSet<RegionSyncClientView> currentlist = m_client_views;
                HashSet<RegionSyncClientView> newlist = new HashSet<RegionSyncClientView>(currentlist);
                newlist.Add(rscv);
                // Anyone holding the previous version of the list can keep using it since
                // they will not hold it for long and get a new copy next time they need to iterate
                m_client_views = newlist;
            }
        }

        // A CV is essentially requesting to be shut down. 
        // Other than a hard disconnect, this is the only way a CV will close its connection to CM
        // Making this request prompts the balancer to shift all connected clients on that CM to another CM if one exists
        // Remove the client view from the list and decrement synced client counter
        public void RemoveSyncedClient(RegionSyncClientView rscv)
        {
            lock (m_clientview_lock)
            {
                HashSet<RegionSyncClientView> currentlist = m_client_views;
                HashSet<RegionSyncClientView> newlist = new HashSet<RegionSyncClientView>(currentlist);
                newlist.Remove(rscv);
                // Anyone holding the previous version of the list can keep using it since
                // they will not hold it for long and get a new copy next time they need to iterate
                m_client_views = newlist;
            }
        }

        public void ForEachClientManager(Action<RegionSyncClientView> action)
        {
            foreach (RegionSyncClientView cv in m_client_views)
            {
                action(cv);
            }
        }

        public void BalanceLoad()
        {
            // If we have 1 - 10 agents connected, we are just testing things all. Move all of them to another region
            if (m_scene.SceneGraph.GetRootAgentCount() > 0 && m_scene.SceneGraph.GetRootAgentCount() < 10)
            {
                SpecialRebalance();
                return;
            }
            Dictionary<RegionSyncClientView, int> avSourceRegionCounts = new Dictionary<RegionSyncClientView, int>();
            KeyValuePair<RegionSyncClientView, int> destinationRegionCount = new KeyValuePair<RegionSyncClientView,int>(null, m_maxClientsPerClientManager);
            foreach (RegionSyncClientView client in m_client_views)
            {
                int clientCount = client.SyncedAvCount;
                if (clientCount > m_maxClientsPerClientManager)
                {
                    avSourceRegionCounts.Add(client, clientCount);
                }
                else if (clientCount < destinationRegionCount.Value)
                {
                    destinationRegionCount = new KeyValuePair<RegionSyncClientView, int>(client, clientCount);
                }
            }
            // Now we should have a list of regions over the limit and a target region with the lowest number of clients
            // Find average load of overloaded and least loaded regions
            int currentLoad = 0;
            foreach (KeyValuePair<RegionSyncClientView, int> kvp in avSourceRegionCounts)
                currentLoad += kvp.Value;
            currentLoad += destinationRegionCount.Value;
            int targetLoad = (int)(currentLoad/(avSourceRegionCounts.Count + 1));
            foreach (KeyValuePair<RegionSyncClientView, int> kvp in avSourceRegionCounts)
            {
                kvp.Key.BalanceClients(targetLoad, destinationRegionCount.Key.Name);
            }
        }

        private void SpecialRebalance()
        {
            m_log.Warn("[CLIENT LOAD BALANCER] Begin special rebalance.");
            if (m_client_views.Count < 2)
            {
                m_log.Error("[CLIENT LOAD BALANCER] Could not special balance because there are not at least 2 client managers.");
                return;
            }
            RegionSyncClientView src = null;
            RegionSyncClientView dst = null;
            // Find the one client
            foreach (RegionSyncClientView client in m_client_views)
            {
                //m_log.WarnFormat("[CLIENT LOAD BALANCER] Client RSCV {0} SyncedAvCount = {1}.", client.Name, client.SyncedAvCount);
                if (src != null && dst != null)
                    break;
                // Find a src region with some avatars
                if (src == null && client.SyncedAvCount > 0)
                    src = client;
                // Find a dst region with no avatars
                else if (dst == null && client.SyncedAvCount == 0)
                    dst = client;
            }
            if (src == null || dst == null)
            {
                m_log.Error("[CLIENT LOAD BALANCER] Could not special balance because a src and dst client manager region could not be identified.");
                return;
            }

            // Send all clients from src to dst
            m_log.WarnFormat("[CLIENT LOAD BALANCER] Moving all clients from client manager region {0} to region {1}", src.Name, dst.Name);
            src.BalanceClients(0, dst.Name);
        }
    }
}

/* Copyright 2011 (c) Intel Corporation
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * The name of the copyright holder may not be used to endorse or promote products
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
using System.IO;
using OpenMetaverse;
using log4net;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    //Initial code in SymmetricSyncMessage copied from RegionSyncMessage. 

    /// <summary>
    /// Types of symmetric sync messages among actors. 
    /// NOTE:: To enable message subscription, we may need to move the definition of MsgType to, say IRegionSyncModule, so that it can be exposed to other region modules.
    /// </summary>
    public class SymmetricSyncMessage
    {
        #region MsgType Enum
        public enum MsgType
        {
            Null,
            // Actor -> SIM(Scene)
            GetTerrain,
            GetObjects,
            
            // SIM <-> CM
            Terrain,
            NewObject,       // objects
            UpdatedPrimProperties, //per property sync
            //UpdatedObject,   // objects
            UpdatedBucketProperties, //object properties in one bucket
            RemovedObject,   // objects
            LinkObject,
            DelinkObject,
            RegionName,
            //RegionStatus,
            ActorID,
            ActorType,
            //events
            NewScript,
            UpdateScript,
            ScriptReset,
            ChatFromClient,
            ChatFromWorld,
            ChatBroadcast,
            ObjectGrab,
            ObjectGrabbing,
            ObjectDeGrab,
            Attach,
            PhysicsCollision,
            ScriptCollidingStart,
            ScriptColliding,
            ScriptCollidingEnd,
            //contorl command
            SyncStateReport,
        }
        #endregion

        #region Member Data
        private MsgType m_type;
        private byte[] m_data;
        static ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion

        #region Constructors
        public SymmetricSyncMessage(MsgType type, byte[] data)
        {
            m_type = type;
            m_data = data;
        }

        public SymmetricSyncMessage(MsgType type, string msg)
        {
            m_type = type;
            m_data = System.Text.Encoding.ASCII.GetBytes(msg);
        }

        public SymmetricSyncMessage(MsgType type)
        {
            m_type = type;
            m_data = new byte[0];
        }

        public SymmetricSyncMessage(Stream stream)
        {
            //ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            //try
            {
                m_type = (MsgType)Utils.BytesToInt(GetBytesFromStream(stream, 4));
                int length = Utils.BytesToInt(GetBytesFromStream(stream, 4));
                m_data = GetBytesFromStream(stream, length);
                //log.WarnFormat("RegionSyncMessage Constructed {0} ({1} bytes)", m_type.ToString(), length);
            }
        }

        private byte[] GetBytesFromStream(Stream stream, int count)
        {
            // Loop to receive the message length
            byte[] ret = new byte[count];
            int i = 0;
            while (i < count)
            {
                i += stream.Read(ret, i, count - i);
            }
            return ret;
        }

        #endregion

        #region Accessors
        public MsgType Type
        {
            get { return m_type; }
        }

        public int Length
        {
            get { return m_data.Length; }
        }

        public byte[] Data
        {
            get { return m_data; }
        }
        #endregion

        #region Conversions
        public byte[] ToBytes()
        {
            byte[] buf = new byte[m_data.Length + 8];
            Utils.IntToBytes((int)m_type, buf, 0);
            Utils.IntToBytes(m_data.Length, buf, 4);
            Array.Copy(m_data, 0, buf, 8, m_data.Length);
            return buf;
        }

        public override string ToString()
        {
            return String.Format("{0} ({1} bytes)", m_type.ToString(), m_data.Length.ToString());
        }
        #endregion


        public static void HandleSuccess(string header, SymmetricSyncMessage msg, string message)
        {
            m_log.WarnFormat("{0} Handled {1}: {2}", header, msg.ToString(), message);
        }

        public static void HandleTrivial(string header, SymmetricSyncMessage msg, string message)
        {
            m_log.WarnFormat("{0} Issue handling {1}: {2}", header, msg.ToString(), message);
        }

        public static void HandleWarning(string header, SymmetricSyncMessage msg, string message)
        {
            m_log.WarnFormat("{0} Warning handling {1}: {2}", header, msg.ToString(), message);
        }

        public static void HandleError(string header, SymmetricSyncMessage msg, string message)
        {
            m_log.WarnFormat("{0} Error handling {1}: {2}", header, msg.ToString(), message);
        }

        public static bool HandlerDebug(string header, SymmetricSyncMessage msg, string message)
        {
            m_log.WarnFormat("{0} DBG ({1}): {2}", header, msg.ToString(), message);
            return true;
        }
    }
}
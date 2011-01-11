/*
 * Copyright (c) Contributors: TO BE FILLED
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
            UpdatedObject,   // objects
            RemovedObject,   // objects
            RegionName,
            //RegionStatus,
            ActorID,
            //events
            UpdateScript,
            ScriptReset,
            ChatFromClient,
            ChatFromWorld,
            ObjectGrab,
            ObjectGrabbing,
            ObjectDeGrab,
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
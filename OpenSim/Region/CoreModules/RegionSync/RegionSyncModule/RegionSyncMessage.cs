using System;
using System.IO;
using OpenMetaverse;
using log4net;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    /// <summary>
    /// A message for synchonization message between scenes
    /// </summary>
    public class RegionSyncMessage
    {
        #region MsgType Enum
        public enum MsgType
        {
            Null,
            //ConnectSyncClient,
            //DisconnectSyncClient,
            // CM -> SIM
            ActorConnect,
            AgentAdd,       
            AgentUpdate,
            AgentRemove,
            GetTerrain,
            GetObjects,
            SubscribeObjects,
            GetAvatars,
            SubscribeAvatars,
            ChatFromClient,
            AvatarTeleportOut, // An LLClientView (real client) was converted to a RegionSyncAvatar
            AvatarTeleportIn,  // A RegionSyncAvatar was converted to an LLClientView (real client)
            // SIM -> CM
            Terrain,
            NewObject,       // objects
            UpdatedObject,   // objects
            RemovedObject,   // objects
            NewAvatar,       // avatars
            UpdatedAvatar,   // avatars
            AnimateAvatar,
            AvatarAppearance,
            RemovedAvatar,   // avatars
            BalanceClientLoad, // Tells CM a client load target and a place to teleport the extras
            ChatFromSim,
            // BIDIR
            EchoRequest,
            EchoResponse,
            RegionName,
            RegionStatus
        }
        #endregion

        #region Member Data
        private MsgType m_type;
        private byte[] m_data;
        static ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion

        #region Constructors
        public RegionSyncMessage(MsgType type, byte[] data)
        {
            m_type = type;
            m_data = data;
        }

        public RegionSyncMessage(MsgType type, string msg)
        {
            m_type = type;
            m_data = System.Text.Encoding.ASCII.GetBytes(msg);
        }

        public RegionSyncMessage(MsgType type)
        {
            m_type = type;
            m_data = new byte[0];
        }

        public RegionSyncMessage(Stream stream)
        {
            //ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            //try
            {
                m_type = (MsgType)Utils.BytesToInt(GetBytesFromStream(stream, 4));
                int length = Utils.BytesToInt(GetBytesFromStream(stream, 4));
                m_data = GetBytesFromStream(stream, length);
                //log.WarnFormat("RegionSyncMessage Constructed {0} ({1} bytes)", m_type.ToString(), length);
            }
                /*
            catch (Exception e)
            {
                log.WarnFormat("[REGION SYNC MESSAGE] RegionSyncMessage Constructor encountered an exception {0}", e.Message);
            }
                 * */
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

        public static void HandleSuccess(string header, RegionSyncMessage msg, string message)
        {
            m_log.WarnFormat("{0} Handled {1}: {2}", header, msg.ToString(), message);
        }

        public static void HandleTrivial(string header, RegionSyncMessage msg, string message)
        {
            m_log.WarnFormat("{0} Issue handling {1}: {2}", header, msg.ToString(), message);
        }

        public static void HandleWarning(string header, RegionSyncMessage msg, string message)
        {
            m_log.WarnFormat("{0} Warning handling {1}: {2}", header, msg.ToString(), message);
        }

        public static void HandleError(string header, RegionSyncMessage msg, string message)
        {
            m_log.WarnFormat("{0} Error handling {1}: {2}", header, msg.ToString(), message);
        }

        public static bool HandlerDebug(string header, RegionSyncMessage msg, string message)
        {
            m_log.WarnFormat("{0} DBG ({1}): {2}", header, msg.ToString(), message);
            return true;
        }

    }
}

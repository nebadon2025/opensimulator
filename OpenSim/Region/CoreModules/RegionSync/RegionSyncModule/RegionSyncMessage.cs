using System;
using System.IO;
using OpenMetaverse;
using log4net;

namespace OpenSim.Region.Examples.RegionSyncModule
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
            AgentAdd,       
            AgentUpdate,
            AgentRemove,
            GetTerrain,
            GetObjects,
            SubscribeObjects,
            GetAvatars,
            SubscribeAvatars,
            ChatFromClient,
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
            ChatFromSim,
            // BIDIR
            EchoRequest,
            EchoResponse
        }
        #endregion

        #region Member Data
        private MsgType m_type;
        private byte[] m_data;
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
            m_data = System.Text.Encoding.ASCII.GetBytes(msg + System.Environment.NewLine);
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
    }
}

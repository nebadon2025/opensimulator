using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using OpenMetaverse.StructuredData;
using log4net;

using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    class RegionSyncUtil
    {
        // The logfile
        private static ILog m_log;

        //HashSet<string> exceptions = new HashSet<string>();
        public static OSDMap DeserializeMessage(RegionSyncMessage msg, string logHeader)
        {
            OSDMap data = null;
            try
            {
                data = OSDParser.DeserializeJson(Encoding.ASCII.GetString(msg.Data, 0, msg.Length)) as OSDMap;
            }
            catch (Exception e)
            {
                m_log.Error(logHeader + " " + Encoding.ASCII.GetString(msg.Data, 0, msg.Length));
                data = null;
            }
            return data;
        }

        #region Quark operations

        //Convert a list of quarks, each identified by "x_y", where (x,y) are the offset position (in a 256x256 scene) of the left-bottom corner, to a single string
        public static string QuarkStringListToString(List<string> quarks)
        {
            string quarkString = "";
            foreach (string quark in quarks)
            {
                quarkString += quark + ",";
            }
            //trim the last ','
            char[] trimChar = {','};
            return quarkString.TrimEnd(trimChar);
            //return quarkString;
        }

        //Convert a list of quarks, each identified by a QuarkInfo data structure, to a single string
        public static string QuarkInfoToString(List<QuarkInfo> quarks)
        {
            string quarkString = "";
            foreach (QuarkInfo quark in quarks)
            {
                quarkString += quark.QuarkStringRepresentation + ",";
            }
            //trim the last ','
            char[] trimChar = { ',' };
            return quarkString.TrimEnd(trimChar);
            //return quarkString;
        }

        public static List<string> QuarkStringToStringList(string quarkString)
        {
            string[] data = quarkString.Split(new char[] { ',' });
            List<string> quarkList = new List<string>(data);

            return quarkList;
        }

        //public static List<QuarkInfo> GetQuarkInfoList(List<string> quarkStringList, int quarkSizeX, int quarkSizeY)
        public static List<QuarkInfo> GetQuarkInfoList(List<string> quarkStringList)
        {
            List<QuarkInfo> quarkInfoList = new List<QuarkInfo>();
            foreach (string quarkString in quarkStringList)
            {
                QuarkInfo quark = new QuarkInfo(quarkString);
                quarkInfoList.Add(quark);
            }
            return quarkInfoList;
        }

        //public static List<QuarkInfo> GetAllQuarksInScene(int QuarkInfo.SizeX, int QuarkInfo.SizeY)
        public static List<QuarkInfo> GetAllQuarksInScene()
        {
            List<QuarkInfo> quarkList = new List<QuarkInfo>();
            int xSlots =(int) Constants.RegionSize / QuarkInfo.SizeX;
            int ySlots = (int) Constants.RegionSize / QuarkInfo.SizeY;

            for (int i = 0; i < xSlots; i++)
            {
                int posX = i * QuarkInfo.SizeX;
                for (int j = 0; j < ySlots; j++)
                {
                    int posY = j * QuarkInfo.SizeY;
                    QuarkInfo quark = new QuarkInfo(posX, posY);
                    quarkList.Add(quark);
                }
            }
            return quarkList;
        }

        //public static List<string> GetAllQuarkStringInScene(int QuarkInfo.SizeX, int QuarkInfo.SizeY)
        public static List<string> GetAllQuarkStringInScene()
        {
            List<string> quarkStringList = new List<string>();
            int xSlots = (int)Constants.RegionSize / QuarkInfo.SizeX;
            int ySlots = (int)Constants.RegionSize / QuarkInfo.SizeY;

            for (int i = 0; i < xSlots; i++)
            {
                int posX = i * QuarkInfo.SizeX;
                for (int j = 0; j < ySlots; j++)
                {
                    int posY = j * QuarkInfo.SizeY;
                    string quarkString = "";
                    quarkString += posX + "_" + posY;
                    quarkStringList.Add(quarkString);
                }
            }
            return quarkStringList;
        }

        //public static string GetQuarkIDByPosition(Vector3 pos, int QuarkInfo.SizeX, int QuarkInfo.SizeY)
        public static string GetQuarkIDByPosition(Vector3 pos)
        {
            float x, y;
            if (pos.X < 0)
            {
                x = 0;
            }
            else if (pos.X >= Constants.RegionSize)
            {
                x = (int)Constants.RegionSize - 1;
            }
            else
                x = pos.X;

            if (pos.Y < 0)
            {
                y = 0;
            }
            else if (pos.Y >= Constants.RegionSize)
            {
                y = (int)Constants.RegionSize - 1;
            }
            else
                y = pos.Y;

            int xRange = (int) x / QuarkInfo.SizeX;
            int yRange = (int) y / QuarkInfo.SizeY;

            int quarkPosX = xRange * QuarkInfo.SizeX;
            int quarkPosY = yRange * QuarkInfo.SizeY;

            string qID = quarkPosX + "_" + quarkPosX;
            return qID;
        }

        //public static List<QuarkInfo> GetQuarkSubscriptions(int QuarkInfo.SizeX, int QuarkInfo.SizeY, int xmin, int ymin, int xmax, int ymax)
        public static List<QuarkInfo> GetQuarkSubscriptions(int xmin, int ymin, int xmax, int ymax)
        {
            List<QuarkInfo> quarkList = new List<QuarkInfo>();
            int xStart = (int)xmin / QuarkInfo.SizeX;
            int yStart = (int)ymin / QuarkInfo.SizeY;
            int xEnd = (int)xmax / QuarkInfo.SizeX;
            int yEnd = (int)ymax / QuarkInfo.SizeY;

            for (int i = xStart; i < xEnd; i++)
            {
                int posX = i * QuarkInfo.SizeX;
                for (int j = yStart; j < yEnd; j++)
                {
                    int posY = j * QuarkInfo.SizeY;
                    QuarkInfo quark = new QuarkInfo(posX, posY);
                    quarkList.Add(quark);
                }
            }
            return quarkList;
        }

        public static int[] GetCornerCoordinates(string space)
        {
            if (space == "") return null;
            string[] corners = space.Split(new char[] { ',' });
            string leftBottom = corners[0];
            string rightTop = corners[1];
            string[] coordinates = leftBottom.Split(new char[] { '_' });

            int [] results = new int[4];
            results[0] = Convert.ToInt32(coordinates[0]);
            results[1] = Convert.ToInt32(coordinates[1]);
            coordinates = rightTop.Split(new char[] { '_' });
            results[2] = Convert.ToInt32(coordinates[0]);
            results[3] = Convert.ToInt32(coordinates[1]);
            return results;
        }

        public static string GetSpaceStringRepresentationByCorners(int minX, int minY, int maxX, int maxY)
        {
            string spaceString = minX + "_" + minY+","+maxX+"_"+maxY;
            return spaceString;
        }

        public static Dictionary<string, int[]> BinarySpaceParition(int minX, int minY, int maxX, int maxY)
        {
            int xLen = maxX - minX;
            int yLen = maxY - minY;

            //We always return the half space that share the same right-top corner, (maxX, maxY), with the original space.
            int upperPlainStartX, upperPlainStartY;
            int upperPlainEndX = maxX, upperPlainEndY = maxY;

            bool partitionOnX;
            if (xLen >= yLen)
            {
                //partition along x axis
                partitionOnX = true;
                int partXLen = xLen / 2;
                upperPlainStartX = minX + partXLen;
                upperPlainStartY = minY;
            }
            else
            {
                //partition along y axis
                partitionOnX = false;
                int partYLen = yLen / 2;
                upperPlainStartY = minY + partYLen;
                upperPlainStartX = minX;
            }
            Dictionary<string, int[]> partitionedSpace = new Dictionary<string, int[]>();

            //upper plain refers to the right (if partition along x) or top (if partition along y) part
            int[] upperPlain = new int[4];
            upperPlain[0] = upperPlainStartX;
            upperPlain[1] = upperPlainStartY;
            upperPlain[2] = upperPlainEndX;
            upperPlain[3] = upperPlainEndY;

            int[] lowerPlain = new int[4];
            if (partitionOnX)
            {
                lowerPlain[0] = minX;
                lowerPlain[1] = minY;
                lowerPlain[2] = upperPlainStartX;
                lowerPlain[3] = maxY;
            }
            else
            {
                lowerPlain[0] = minX;
                lowerPlain[1] = minY;
                lowerPlain[2] = maxX;
                lowerPlain[3] = upperPlainStartY;
            }

            partitionedSpace.Add("lower", lowerPlain);
            partitionedSpace.Add("upper", upperPlain);

            return partitionedSpace;
        }

        public static int[] RemoveSpace(int originMinX, int originMinY, int originMaxX, int originMaxY, int toRemoveMinX, int toRemoveMinY, int toRemoveMaxX, int toRemoveMaxY)
        {
            int[] remainingBox = new int[4];
            remainingBox[0] = originMinX;
            remainingBox[1] = originMinY;
            if (originMinX == toRemoveMinX)
            {
                //partitioned along Y
                remainingBox[2] = originMaxX;
                remainingBox[3] = toRemoveMinY;
            }
            else
            {
                //partitioned along X
                remainingBox[2] = toRemoveMinX;
                remainingBox[3] = originMaxY;
            }
            return remainingBox;
        }

        #endregion Quark operations
    }
}

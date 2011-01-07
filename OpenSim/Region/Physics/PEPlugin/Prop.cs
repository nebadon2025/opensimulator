using System;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.PEPlugin
{
    public enum PropType
    {
        Size,
        Shape,
        LocalID,
        Grabbed,
        Selected,
        Position,
        Force,
        Velocity,
        Torque,
        CollisionScore,
        Orientation
    };

public class Prop
{
    public static void Set(uint localID, PropType type, uint val)
    {
    }
    public static void Set(uint localID, PropType type, Vector3 val)
    {
    }
    public static void Set(uint localID, PropType type, PrimitiveBaseShape val)
    {
    }
    public static void Set(uint localID, PropType type, bool val)
    {
    }
    public static void Set(uint localID, PropType type, float val)
    {
    }
    public static void Set(uint localID, PropType type, Quaternion val)
    {
    }

}
}

/*
* Copyright (c) Contributors, http://opensimulator.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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
using System.Text; // rex, StringBuilder needed
using System.Collections.Generic;
using System.Drawing;
using System.Xml;
using System.Xml.Serialization;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes.Scripting;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    public partial class SceneObjectPart : IScriptHost
    {
        private const LLObject.ObjectFlags OBJFULL_MASK_GENERAL =
            LLObject.ObjectFlags.ObjectCopy | LLObject.ObjectFlags.ObjectModify | LLObject.ObjectFlags.ObjectTransfer;

        private const LLObject.ObjectFlags OBJFULL_MASK_OWNER =
            LLObject.ObjectFlags.ObjectCopy | LLObject.ObjectFlags.ObjectModify | LLObject.ObjectFlags.ObjectOwnerModify |
            LLObject.ObjectFlags.ObjectTransfer | LLObject.ObjectFlags.ObjectYouOwner;

        private const uint OBJNEXT_OWNER = 2147483647;

        private const uint FULL_MASK_PERMISSIONS_GENERAL = 2147483647;
        private const uint FULL_MASK_PERMISSIONS_OWNER = 2147483647;

        [XmlIgnore] public PhysicsActor PhysActor = null;
        
        public LLUUID LastOwnerID;
        public LLUUID OwnerID;
        public LLUUID GroupID;
        public int OwnershipCost;
        public byte ObjectSaleType;
        public int SalePrice;
        public uint Category;

        public Int32 CreationDate;
        public uint ParentID = 0;

        private Vector3 m_sitTargetPosition = new Vector3(0, 0, 0);
        private Quaternion m_sitTargetOrientation = new Quaternion(0, 0, 0, 1);
        private LLUUID m_SitTargetAvatar = LLUUID.Zero;

        //
        // Main grid has default permissions as follows
        // 
        public uint OwnerMask = FULL_MASK_PERMISSIONS_OWNER;
        public uint NextOwnerMask = OBJNEXT_OWNER;
        public uint GroupMask = (uint) LLObject.ObjectFlags.None;
        public uint EveryoneMask = (uint) LLObject.ObjectFlags.None;
        public uint BaseMask = FULL_MASK_PERMISSIONS_OWNER;

        protected byte[] m_particleSystem = new byte[0];

        [XmlIgnore] public uint TimeStampFull = 0;
        [XmlIgnore] public uint TimeStampTerse = 0;
        [XmlIgnore] public uint TimeStampLastActivity = 0; // Will be used for AutoReturn

        /// <summary>
        /// Only used internally to schedule client updates
        /// </summary>
        private byte m_updateFlag;

        // rex, extra parameters & their definitions

        // reX extra block parameters in easily readable format       
        public string m_RexClassName = "";
        public byte m_RexFlags = 0;
        public byte m_RexCollisionType = 0;
        public float m_RexDrawDistance = 0.0F;
        public float m_RexLOD = 0.0F;
        public LLUUID m_RexMeshUUID = LLUUID.Zero;
        public LLUUID m_RexCollisionMeshUUID = LLUUID.Zero;
        public List<LLUUID> m_RexMaterialUUID = new List<LLUUID>();
        public byte m_RexFixedMaterial = 0;
        public LLUUID m_RexParticleScriptUUID = LLUUID.Zero;

        // reX extra parameter block defines
        public const int PARAMS_REX = 0x0100;

        // Bit values for flags
        public const int REXFLAGS_ISMESH = 0x01;
        public const int REXFLAGS_ISVISIBLE = 0x02;
        public const int REXFLAGS_CASTSHADOWS = 0x04;
        public const int REXFLAGS_SHOWTEXT = 0x08;
        public const int REXFLAGS_SCALEMESH = 0x10;
        public const int REXFLAGS_SOLIDALPHA = 0x20;
        public const int REXFLAGS_ISBILLBOARD = 0x40;
        public const int REXFLAGS_USEPARTICLESCRIPT = 0x80;

        // Collision type enumeration (still unused :)) 
        public const int REXCOLLISION_VOLUME = 0x01;
        public const int REXCOLLISION_TRIMESH = 0x02;

        // Attachment parameters
        private ScenePresence m_attachPresence = null;
        private byte m_attachPt;
        private LLQuaternion m_attachRot;
        private RegionInfo m_attachRegInfo;
        private LLUUID m_attachAgentId;
        
        // rexend

        #region Properties

        public LLUUID CreatorID;

        public LLUUID ObjectCreator
        {
            get { return CreatorID; }
        }

        protected LLUUID m_uuid;

        public LLUUID UUID
        {
            get { return m_uuid; }
            set { m_uuid = value; }
        }

        protected uint m_localID;

        public uint LocalID
        {
            get { return m_localID; }
            set { m_localID = value; }
        }

        protected string m_name;

        public virtual string Name
        {
            get { return m_name; }
            set { m_name = value; }
        }

        protected LLObject.ObjectFlags m_flags = 0;

        public uint ObjectFlags
        {
            get { return (uint) m_flags; }
            set { m_flags = (LLObject.ObjectFlags) value; }
        }

        protected LLObject.MaterialType m_material = 0;

        public byte Material
        {
            get { return (byte) m_material; }
            set { m_material = (LLObject.MaterialType) value; }
        }

        protected ulong m_regionHandle;

        public ulong RegionHandle
        {
            get { return m_regionHandle; }
            set { m_regionHandle = value; }
        }

        //unkown if this will be kept, added as a way of removing the group position from the group class
        protected LLVector3 m_groupPosition;


        public LLVector3 GroupPosition
        {
            get
            {
                if (PhysActor != null)
                {
                    m_groupPosition.X = PhysActor.Position.X;
                    m_groupPosition.Y = PhysActor.Position.Y;
                    m_groupPosition.Z = PhysActor.Position.Z;
                }
                return m_groupPosition;
            }
            set
            {
                if (PhysActor != null)
                {
                    try
                    {
                        //lock (m_parentGroup.Scene.SyncRoot)
                        //{
                        PhysActor.Position = new PhysicsVector(value.X, value.Y, value.Z);
                        m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
                        //}
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
                m_groupPosition = value;
            }
        }

        protected LLVector3 m_offsetPosition;

        public LLVector3 OffsetPosition
        {
            get { return m_offsetPosition; }
            set { m_offsetPosition = value; }
        }

        public LLVector3 AbsolutePosition
        {
            get { return m_offsetPosition + m_groupPosition; }
        }

        protected LLQuaternion m_rotationOffset;

        public LLQuaternion RotationOffset
        {
            get
            {
                if (PhysActor != null)
                {
                    if (PhysActor.Orientation.x != 0 || PhysActor.Orientation.y != 0
                        || PhysActor.Orientation.z != 0 || PhysActor.Orientation.w != 0)
                    {
                        m_rotationOffset.X = PhysActor.Orientation.x;
                        m_rotationOffset.Y = PhysActor.Orientation.y;
                        m_rotationOffset.Z = PhysActor.Orientation.z;
                        m_rotationOffset.W = PhysActor.Orientation.w;
                    }
                }
                return m_rotationOffset;
            }
            set
            {
                if (PhysActor != null)
                {
                    try
                    {
                        //lock (Scene.SyncRoot)
                        //{
                        PhysActor.Orientation = new Quaternion(value.W, value.X, value.Y, value.Z);
                        m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
                        //}
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                m_rotationOffset = value;
            }
        }

        protected LLVector3 m_velocity;
        protected LLVector3 m_rotationalvelocity;

        /// <summary></summary>
        public LLVector3 Velocity
        {
            get
            {
                //if (PhysActor.Velocity.x != 0 || PhysActor.Velocity.y != 0
                //|| PhysActor.Velocity.z != 0)
                //{
                if (PhysActor != null)
                {
                    if (PhysActor.IsPhysical)
                    {
                        m_velocity.X = PhysActor.Velocity.X;
                        m_velocity.Y = PhysActor.Velocity.Y;
                        m_velocity.Z = PhysActor.Velocity.Z;
                    }
                }

                return m_velocity;
            }
            set { m_velocity = value; }
        }

        public LLVector3 RotationalVelocity
        {
            get
            {
                //if (PhysActor.Velocity.x != 0 || PhysActor.Velocity.y != 0
                //|| PhysActor.Velocity.z != 0)
                //{
                if (PhysActor != null)
                {
                    if (PhysActor.IsPhysical)
                    {
                        m_rotationalvelocity.X = PhysActor.RotationalVelocity.X;
                        m_rotationalvelocity.Y = PhysActor.RotationalVelocity.Y;
                        m_rotationalvelocity.Z = PhysActor.RotationalVelocity.Z;
                    }
                }

                return m_rotationalvelocity;
            }
            set { m_rotationalvelocity = value; }
        }


        protected LLVector3 m_angularVelocity;

        /// <summary></summary>
        public LLVector3 AngularVelocity
        {
            get { return m_angularVelocity; }
            set { m_angularVelocity = value; }
        }

        protected LLVector3 m_acceleration;

        /// <summary></summary>
        public LLVector3 Acceleration
        {
            get { return m_acceleration; }
            set { m_acceleration = value; }
        }

        private string m_description = "";

        public string Description
        {
            get { return m_description; }
            set { m_description = value; }
        }

        private Color m_color = Color.Black;

        public Color Color
        {
            get { return m_color; }
            set
            {
                m_color = value;
                /* ScheduleFullUpdate() need not be called b/c after
                 * setting the color, the text will be set, so then
                 * ScheduleFullUpdate() will be called. */
                //ScheduleFullUpdate();
            }
        }

        private string m_text = "";

        public Vector3 SitTargetPosition
        {
            get { return m_sitTargetPosition; }
        }

        public Quaternion SitTargetOrientation
        {
            get { return m_sitTargetOrientation; }
        }

        public string Text
        {
            get { return m_text; }
            set
            {
                m_text = value;
                ScheduleFullUpdate();
            }
        }

        private string m_sitName = "";

        public string SitName
        {
            get { return m_sitName; }
            set { m_sitName = value; }
        }

        private string m_touchName = "";

        //rex (hack)
        private LLUUID touchedBy = LLUUID.Zero;

        //rex (hack)
        public LLUUID TouchedBy
        {
            set
            {
                touchedBy = value;
            }
            get
            {
                return touchedBy;
            }
        }

        public string TouchName
        {
            get { return m_touchName; }
            set { m_touchName = value; }
        }

        private int m_linkNum = 0;

        public int LinkNum
        {
            get { return m_linkNum; }
            set { m_linkNum = value; }
        }

        private byte m_clickAction = 0;

        public byte ClickAction
        {
            get { return m_clickAction; }
            set
            {
                m_clickAction = value;
                ScheduleFullUpdate();
            }
        }

        protected PrimitiveBaseShape m_shape;

        public PrimitiveBaseShape Shape
        {
            get { return m_shape; }
            set { m_shape = value; }
        }

        public LLVector3 Scale
        {
            set { m_shape.Scale = value; }
            get { return m_shape.Scale; }
        }

        public bool Stopped
        {
            get {
                double threshold = 0.02;
                return (Math.Abs(Velocity.X) < threshold &&
                        Math.Abs(Velocity.Y) < threshold &&
                        Math.Abs(Velocity.Z) < threshold &&
                        Math.Abs(AngularVelocity.X) < threshold &&
                        Math.Abs(AngularVelocity.Y) < threshold &&
                        Math.Abs(AngularVelocity.Z) < threshold);
            }
        }

        #endregion

        public LLUUID ObjectOwner
        {
            get { return OwnerID; }
        }

        // FIXME, TODO, ERROR: 'ParentGroup' can't be in here, move it out.
        protected SceneObjectGroup m_parentGroup;

        public SceneObjectGroup ParentGroup
        {
            get { return m_parentGroup; }
        }

        public byte UpdateFlag
        {
            get { return m_updateFlag; }
            set { m_updateFlag = value; }
        }

        #region Constructors

        /// <summary>
        /// No arg constructor called by region restore db code
        /// </summary>
        public SceneObjectPart()
        {
            // It's not necessary to persist this
            m_inventoryFileName = "taskinventory" + LLUUID.Random().ToString();
        }

        public SceneObjectPart(ulong regionHandle, SceneObjectGroup parent, LLUUID ownerID, uint localID,
                               PrimitiveBaseShape shape, LLVector3 groupPosition, LLVector3 offsetPosition)
            : this(regionHandle, parent, ownerID, localID, shape, groupPosition, LLQuaternion.Identity, offsetPosition)
        {
        }

        /// <summary>
        /// Create a completely new SceneObjectPart (prim)
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="parent"></param>
        /// <param name="ownerID"></param>
        /// <param name="localID"></param>
        /// <param name="shape"></param>
        /// <param name="position"></param>
        public SceneObjectPart(ulong regionHandle, SceneObjectGroup parent, LLUUID ownerID, uint localID,
                               PrimitiveBaseShape shape, LLVector3 groupPosition, LLQuaternion rotationOffset,
                               LLVector3 offsetPosition)
        {
            m_name = "Primitive";
            m_regionHandle = regionHandle;
            m_parentGroup = parent;

            CreationDate = (Int32) (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            OwnerID = ownerID;
            CreatorID = OwnerID;
            LastOwnerID = LLUUID.Zero;
            UUID = LLUUID.Random();
            LocalID = (uint) (localID);
            Shape = shape;
            // Todo: Add More Object Parameter from above!
            OwnershipCost = 0;
            ObjectSaleType = (byte) 0;
            SalePrice = 0;
            Category = (uint) 0;
            LastOwnerID = CreatorID;
            // End Todo: ///
            GroupPosition = groupPosition;
            OffsetPosition = offsetPosition;
            RotationOffset = rotationOffset;
            Velocity = new LLVector3(0, 0, 0);
            m_rotationalvelocity = new LLVector3(0, 0, 0);
            AngularVelocity = new LLVector3(0, 0, 0);
            Acceleration = new LLVector3(0, 0, 0);

            m_inventoryFileName = "taskinventory" + LLUUID.Random().ToString();
            m_folderID = LLUUID.Random();

            m_flags = 0;
            m_flags |= LLObject.ObjectFlags.Touch |
                       LLObject.ObjectFlags.AllowInventoryDrop |
                       LLObject.ObjectFlags.CreateSelected;

            ApplySanePermissions();

            ScheduleFullUpdate();
        }

        /// <summary>
        /// Re/create a SceneObjectPart (prim)
        /// currently not used, and maybe won't be
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="parent"></param>
        /// <param name="ownerID"></param>
        /// <param name="localID"></param>
        /// <param name="shape"></param>
        /// <param name="position"></param>
        public SceneObjectPart(ulong regionHandle, SceneObjectGroup parent, int creationDate, LLUUID ownerID,
                               LLUUID creatorID, LLUUID lastOwnerID, uint localID, PrimitiveBaseShape shape,
                               LLVector3 position, LLQuaternion rotation, uint flags)
        {
            m_regionHandle = regionHandle;
            m_parentGroup = parent;
            TimeStampTerse = (uint) Util.UnixTimeSinceEpoch();
            CreationDate = creationDate;
            OwnerID = ownerID;
            CreatorID = creatorID;
            LastOwnerID = lastOwnerID;
            UUID = LLUUID.Random();
            LocalID = (uint) (localID);
            Shape = shape;
            OwnershipCost = 0;
            ObjectSaleType = (byte) 0;
            SalePrice = 0;
            Category = (uint) 0;
            LastOwnerID = CreatorID;
            OffsetPosition = position;
            RotationOffset = rotation;
            ObjectFlags = flags;

            ApplySanePermissions();
            // ApplyPhysics();

            ScheduleFullUpdate();
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlreader"></param>
        /// <returns></returns>
        public static SceneObjectPart FromXml(XmlReader xmlReader)
        {
            XmlSerializer serializer = new XmlSerializer(typeof (SceneObjectPart));
            SceneObjectPart newobject = (SceneObjectPart) serializer.Deserialize(xmlReader);
            return newobject;
        }

        public void ApplyPhysics()
        {
            bool isPhysical = ((ObjectFlags & (uint) LLObject.ObjectFlags.Physics) != 0);
            bool isPhantom = ((ObjectFlags & (uint) LLObject.ObjectFlags.Phantom) != 0);

            bool usePhysics = isPhysical && !isPhantom;

            if (usePhysics)
            {
                PhysActor = m_parentGroup.Scene.PhysicsScene.AddPrimShape(
                    Name,
                    Shape,
                    new PhysicsVector(AbsolutePosition.X, AbsolutePosition.Y,
                                      AbsolutePosition.Z),
                    new PhysicsVector(Scale.X, Scale.Y, Scale.Z),
                    new Quaternion(RotationOffset.W, RotationOffset.X,
                                   RotationOffset.Y, RotationOffset.Z), usePhysics,LocalID);
            }

            DoPhysicsPropertyUpdate(usePhysics, true);
        }

        public void ApplyNextOwnerPermissions()
        {
            BaseMask = NextOwnerMask;
            OwnerMask = NextOwnerMask;
        }

        public void ApplySanePermissions()
        {
            // These are some flags that The OwnerMask should never have
            OwnerMask &= ~(uint) LLObject.ObjectFlags.ObjectGroupOwned;
            OwnerMask &= ~(uint) LLObject.ObjectFlags.Physics;
            OwnerMask &= ~(uint) LLObject.ObjectFlags.Phantom;
            OwnerMask &= ~(uint) LLObject.ObjectFlags.Scripted;
            OwnerMask &= ~(uint) LLObject.ObjectFlags.Touch;
            OwnerMask &= ~(uint) LLObject.ObjectFlags.Temporary;
            OwnerMask &= ~(uint) LLObject.ObjectFlags.TemporaryOnRez;
            OwnerMask &= ~(uint) LLObject.ObjectFlags.ZlibCompressed;
            OwnerMask &= ~(uint) LLObject.ObjectFlags.AllowInventoryDrop;
            OwnerMask &= ~(uint) LLObject.ObjectFlags.AnimSource;
            OwnerMask &= ~(uint) LLObject.ObjectFlags.Money;
            OwnerMask &= ~(uint) LLObject.ObjectFlags.CastShadows;
            OwnerMask &= ~(uint) LLObject.ObjectFlags.InventoryEmpty;
            OwnerMask &= ~(uint) LLObject.ObjectFlags.CreateSelected;


            // These are some flags that the next owner mask should never have
            NextOwnerMask &= ~(uint) LLObject.ObjectFlags.ObjectYouOwner;
            NextOwnerMask &= ~(uint) LLObject.ObjectFlags.ObjectTransfer;
            NextOwnerMask &= ~(uint) LLObject.ObjectFlags.ObjectOwnerModify;
            NextOwnerMask &= ~(uint) LLObject.ObjectFlags.ObjectGroupOwned;
            NextOwnerMask &= ~(uint) LLObject.ObjectFlags.Physics;
            NextOwnerMask &= ~(uint) LLObject.ObjectFlags.Phantom;
            NextOwnerMask &= ~(uint) LLObject.ObjectFlags.Scripted;
            NextOwnerMask &= ~(uint) LLObject.ObjectFlags.Touch;
            NextOwnerMask &= ~(uint) LLObject.ObjectFlags.Temporary;
            NextOwnerMask &= ~(uint) LLObject.ObjectFlags.TemporaryOnRez;
            NextOwnerMask &= ~(uint) LLObject.ObjectFlags.ZlibCompressed;
            NextOwnerMask &= ~(uint) LLObject.ObjectFlags.AllowInventoryDrop;
            NextOwnerMask &= ~(uint) LLObject.ObjectFlags.AnimSource;
            NextOwnerMask &= ~(uint) LLObject.ObjectFlags.Money;
            NextOwnerMask &= ~(uint) LLObject.ObjectFlags.CastShadows;
            NextOwnerMask &= ~(uint) LLObject.ObjectFlags.InventoryEmpty;
            NextOwnerMask &= ~(uint) LLObject.ObjectFlags.CreateSelected;


            // These are some flags that the GroupMask should never have
            GroupMask &= ~(uint) LLObject.ObjectFlags.ObjectYouOwner;
            GroupMask &= ~(uint) LLObject.ObjectFlags.ObjectTransfer;
            GroupMask &= ~(uint) LLObject.ObjectFlags.ObjectOwnerModify;
            GroupMask &= ~(uint) LLObject.ObjectFlags.ObjectGroupOwned;
            GroupMask &= ~(uint) LLObject.ObjectFlags.Physics;
            GroupMask &= ~(uint) LLObject.ObjectFlags.Phantom;
            GroupMask &= ~(uint) LLObject.ObjectFlags.Scripted;
            GroupMask &= ~(uint) LLObject.ObjectFlags.Touch;
            GroupMask &= ~(uint) LLObject.ObjectFlags.Temporary;
            GroupMask &= ~(uint) LLObject.ObjectFlags.TemporaryOnRez;
            GroupMask &= ~(uint) LLObject.ObjectFlags.ZlibCompressed;
            GroupMask &= ~(uint) LLObject.ObjectFlags.AllowInventoryDrop;
            GroupMask &= ~(uint) LLObject.ObjectFlags.AnimSource;
            GroupMask &= ~(uint) LLObject.ObjectFlags.Money;
            GroupMask &= ~(uint) LLObject.ObjectFlags.CastShadows;
            GroupMask &= ~(uint) LLObject.ObjectFlags.InventoryEmpty;
            GroupMask &= ~(uint) LLObject.ObjectFlags.CreateSelected;


            // These are some flags that EveryoneMask should never have
            EveryoneMask &= ~(uint) LLObject.ObjectFlags.ObjectYouOwner;
            EveryoneMask &= ~(uint) LLObject.ObjectFlags.ObjectTransfer;
            EveryoneMask &= ~(uint) LLObject.ObjectFlags.ObjectOwnerModify;
            EveryoneMask &= ~(uint) LLObject.ObjectFlags.ObjectGroupOwned;
            EveryoneMask &= ~(uint) LLObject.ObjectFlags.Physics;
            EveryoneMask &= ~(uint) LLObject.ObjectFlags.Phantom;
            EveryoneMask &= ~(uint) LLObject.ObjectFlags.Scripted;
            EveryoneMask &= ~(uint) LLObject.ObjectFlags.Touch;
            EveryoneMask &= ~(uint) LLObject.ObjectFlags.Temporary;
            EveryoneMask &= ~(uint) LLObject.ObjectFlags.TemporaryOnRez;
            EveryoneMask &= ~(uint) LLObject.ObjectFlags.ZlibCompressed;
            EveryoneMask &= ~(uint) LLObject.ObjectFlags.AllowInventoryDrop;
            EveryoneMask &= ~(uint) LLObject.ObjectFlags.AnimSource;
            EveryoneMask &= ~(uint) LLObject.ObjectFlags.Money;
            EveryoneMask &= ~(uint) LLObject.ObjectFlags.CastShadows;
            EveryoneMask &= ~(uint) LLObject.ObjectFlags.InventoryEmpty;
            EveryoneMask &= ~(uint) LLObject.ObjectFlags.CreateSelected;
            EveryoneMask &= ~(uint)LLObject.ObjectFlags.ObjectYouOfficer;
            EveryoneMask &= ~(uint)LLObject.ObjectFlags.ObjectModify;


            // These are some flags that ObjectFlags (m_flags) should never have
            ObjectFlags &= ~(uint) LLObject.ObjectFlags.ObjectYouOwner;
            ObjectFlags &= ~(uint) LLObject.ObjectFlags.ObjectTransfer;
            ObjectFlags &= ~(uint) LLObject.ObjectFlags.ObjectOwnerModify;
            ObjectFlags &= ~(uint) LLObject.ObjectFlags.ObjectYouOfficer;
            ObjectFlags &= ~(uint) LLObject.ObjectFlags.ObjectCopy;
            ObjectFlags &= ~(uint) LLObject.ObjectFlags.ObjectModify;
            ObjectFlags &= ~(uint) LLObject.ObjectFlags.ObjectMove;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlWriter"></param>
        public void ToXml(XmlWriter xmlWriter)
        {
            XmlSerializer serializer = new XmlSerializer(typeof (SceneObjectPart));
            serializer.Serialize(xmlWriter, this);
        }

        public EntityIntersection TestIntersection(Ray iray, Quaternion parentrot)
        {
            // In this case we're using a sphere with a radius of the largest dimention of the prim
            // TODO: Change to take shape into account


            EntityIntersection returnresult = new EntityIntersection();
            Vector3 vAbsolutePosition = new Vector3(AbsolutePosition.X, AbsolutePosition.Y, AbsolutePosition.Z);

            Vector3 vScale = new Vector3(Scale.X, Scale.Y, Scale.Z);
            Quaternion qRotation =
                new Quaternion(RotationOffset.W, RotationOffset.X, RotationOffset.Y, RotationOffset.Z);


            //Quaternion worldRotation = (qRotation*parentrot);
            //Matrix3 worldRotM = worldRotation.ToRotationMatrix();


            Vector3 rOrigin = iray.Origin;
            Vector3 rDirection = iray.Direction;

            

            //rDirection = rDirection.Normalize();
            // Buidling the first part of the Quadratic equation
            Vector3 r2ndDirection = rDirection*rDirection;
            float itestPart1 = r2ndDirection.x + r2ndDirection.y + r2ndDirection.z;

            // Buidling the second part of the Quadratic equation
            Vector3 tmVal2 = rOrigin - vAbsolutePosition;
            Vector3 r2Direction = rDirection*2.0f;
            Vector3 tmVal3 = r2Direction*tmVal2;

            float itestPart2 = tmVal3.x + tmVal3.y + tmVal3.z;

            // Buidling the third part of the Quadratic equation
            Vector3 tmVal4 = rOrigin*rOrigin;
            Vector3 tmVal5 = vAbsolutePosition*vAbsolutePosition;

            Vector3 tmVal6 = vAbsolutePosition*rOrigin;


            // Set Radius to the largest dimention of the prim
            float radius = 0f;
            if (vScale.x > radius)
                radius = vScale.x;
            if (vScale.y > radius)
                radius = vScale.y;
            if (vScale.z > radius)
                radius = vScale.z;

            //radius = radius;

            float itestPart3 = tmVal4.x + tmVal4.y + tmVal4.z + tmVal5.x + tmVal5.y + tmVal5.z -
                               (2.0f*(tmVal6.x + tmVal6.y + tmVal6.z + (radius*radius)));

            // Yuk Quadradrics..    Solve first
            float rootsqr = (itestPart2*itestPart2) - (4.0f*itestPart1*itestPart3);
            if (rootsqr < 0.0f)
            {
                // No intersection
                return returnresult;
            }
            float root = ((-itestPart2) - (float) Math.Sqrt((double) rootsqr))/(itestPart1*2.0f);

            if (root < 0.0f)
            {
                // perform second quadratic root solution
                root = ((-itestPart2) + (float) Math.Sqrt((double) rootsqr))/(itestPart1*2.0f);

                // is there any intersection?
                if (root < 0.0f)
                {
                    // nope, no intersection
                    return returnresult;
                }
            }

            // We got an intersection.  putting together an EntityIntersection object with the 
            // intersection information
            Vector3 ipoint =
                new Vector3(iray.Origin.x + (iray.Direction.x*root), iray.Origin.y + (iray.Direction.y*root),
                            iray.Origin.z + (iray.Direction.z*root));

            returnresult.HitTF = true;
            returnresult.ipoint = ipoint;

            // Normal is calculated by the difference and then normalizing the result
            Vector3 normalpart = ipoint - vAbsolutePosition;
            returnresult.normal = normalpart.Normalize();

            // It's funny how the LLVector3 object has a Distance function, but the Axiom.Math object doesnt.
            // I can write a function to do it..    but I like the fact that this one is Static.

            LLVector3 distanceConvert1 = new LLVector3(iray.Origin.x, iray.Origin.y, iray.Origin.z);
            LLVector3 distanceConvert2 = new LLVector3(ipoint.x, ipoint.y, ipoint.z);
            float distance = (float) Util.GetDistanceTo(distanceConvert1, distanceConvert2);

            returnresult.distance = distance;

            return returnresult;
        }


        /// <summary>
        /// 
        /// </summary>
        public void SetParent(SceneObjectGroup parent)
        {
            m_parentGroup = parent;
        }

        public void SetSitTarget(Vector3 offset, Quaternion orientation)
        {
            m_sitTargetPosition = offset;
            m_sitTargetOrientation = orientation;
        }

        public LLVector3 GetSitTargetPositionLL()
        {
            return new LLVector3(m_sitTargetPosition.x, m_sitTargetPosition.y, m_sitTargetPosition.z);
        }

        public LLQuaternion GetSitTargetOrientationLL()
        {
            return
                new LLQuaternion(m_sitTargetOrientation.x, m_sitTargetOrientation.y, m_sitTargetOrientation.z,
                                 m_sitTargetOrientation.w);
        }

        // Utility function so the databases don't have to reference axiom.math
        public void SetSitTargetLL(LLVector3 offset, LLQuaternion orientation)
        {
            if (
                !(offset.X == 0 && offset.Y == 0 && offset.Z == 0 && (orientation.W == 0 || orientation.W == 1) &&
                  orientation.X == 0 && orientation.Y == 0 && orientation.Z == 0))
            {
                m_sitTargetPosition = new Vector3(offset.X, offset.Y, offset.Z);
                m_sitTargetOrientation = new Quaternion(orientation.W, orientation.X, orientation.Y, orientation.Z);
            }
        }

        public Vector3 GetSitTargetPosition()
        {
            return m_sitTargetPosition;
        }

        public Quaternion GetSitTargetOrientation()
        {
            return m_sitTargetOrientation;
        }

        public void SetAvatarOnSitTarget(LLUUID avatarID)
        {
            m_SitTargetAvatar = avatarID;
        }

        public LLUUID GetAvatarOnSitTarget()
        {
            return m_SitTargetAvatar;
        }


        public LLUUID GetRootPartUUID()
        {
            if (m_parentGroup != null)
            {
                return m_parentGroup.UUID;
            }
            return LLUUID.Zero;
        }

        public static SceneObjectPart Create()
        {
            SceneObjectPart part = new SceneObjectPart();
            part.UUID = LLUUID.Random();

            PrimitiveBaseShape shape = PrimitiveBaseShape.Create();
            part.Shape = shape;

            part.Name = "Primitive";
            part.OwnerID = LLUUID.Random();

            return part;
        }

        #region Copying

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public SceneObjectPart Copy(uint localID, LLUUID AgentID, LLUUID GroupID)
        {
            SceneObjectPart dupe = (SceneObjectPart) MemberwiseClone();
            dupe.m_shape = m_shape.Copy();
            dupe.m_regionHandle = m_regionHandle;
            dupe.UUID = LLUUID.Random();
            dupe.LocalID = localID;
            dupe.OwnerID = AgentID;
            dupe.GroupID = GroupID;
            dupe.GroupPosition = new LLVector3(GroupPosition.X, GroupPosition.Y, GroupPosition.Z);
            dupe.OffsetPosition = new LLVector3(OffsetPosition.X, OffsetPosition.Y, OffsetPosition.Z);
            dupe.RotationOffset =
                new LLQuaternion(RotationOffset.X, RotationOffset.Y, RotationOffset.Z, RotationOffset.W);
            dupe.Velocity = new LLVector3(0, 0, 0);
            dupe.Acceleration = new LLVector3(0, 0, 0);
            dupe.AngularVelocity = new LLVector3(0, 0, 0);
            dupe.ObjectFlags = ObjectFlags;

            dupe.OwnershipCost = OwnershipCost;
            dupe.ObjectSaleType = ObjectSaleType;
            dupe.SalePrice = SalePrice;
            dupe.Category = Category;

            // This may be wrong...    it might have to be applied in SceneObjectGroup to the object that's being duplicated.
            dupe.LastOwnerID = ObjectOwner;

            byte[] extraP = new byte[Shape.ExtraParams.Length];
            Array.Copy(Shape.ExtraParams, extraP, extraP.Length);
            dupe.Shape.ExtraParams = extraP;
            bool UsePhysics = ((dupe.ObjectFlags & (uint) LLObject.ObjectFlags.Physics) != 0);
            dupe.DoPhysicsPropertyUpdate(UsePhysics, true);

            return dupe;
        }

        #endregion

        #region Update Scheduling

        /// <summary>
        /// 
        /// </summary>
        private void ClearUpdateSchedule()
        {
            m_updateFlag = 0;
        }

        /// <summary>
        /// 
        /// </summary>
        public void ScheduleFullUpdate()
        {
            if (m_parentGroup != null)
            {
                m_parentGroup.HasChanged = true;
            }
            TimeStampFull = (uint) Util.UnixTimeSinceEpoch();
            m_updateFlag = 2;
        }

        public void AddFlag(LLObject.ObjectFlags flag)
        {
            LLObject.ObjectFlags prevflag = m_flags;
            //uint objflags = m_flags;
            if ((ObjectFlags & (uint) flag) == 0)
            {
                //Console.WriteLine("Adding flag: " + ((LLObject.ObjectFlags) flag).ToString());
                m_flags |= flag;
            }
            //uint currflag = (uint)m_flags;
            //System.Console.WriteLine("Aprev: " + prevflag.ToString() + " curr: " + m_flags.ToString());
            //ScheduleFullUpdate();
        }

        public void RemFlag(LLObject.ObjectFlags flag)
        {
            LLObject.ObjectFlags prevflag = m_flags;
            if ((ObjectFlags & (uint) flag) != 0)
            {
                //Console.WriteLine("Removing flag: " + ((LLObject.ObjectFlags)flag).ToString());
                m_flags &= ~flag;
            }
            //System.Console.WriteLine("prev: " + prevflag.ToString() + " curr: " + m_flags.ToString());
            //ScheduleFullUpdate();
        }

        /// <summary>
        /// 
        /// </summary>
        public void ScheduleTerseUpdate()
        {
            if (m_updateFlag < 1)
            {
                if (m_parentGroup != null)
                {
                    m_parentGroup.HasChanged = true;
                }
                TimeStampTerse = (uint) Util.UnixTimeSinceEpoch();
                m_updateFlag = 1;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendScheduledUpdates()
        {
            if (m_updateFlag == 1) //some change has been made so update the clients
            {
                AddTerseUpdateToAllAvatars();
                ClearUpdateSchedule();

                // This causes the Scene to 'poll' physical objects every couple of frames
                // bad, so it's been replaced by an event driven method.
                //if ((ObjectFlags & (uint)LLObject.ObjectFlags.Physics) != 0)
                //{
                // Only send the constant terse updates on physical objects!   
                //ScheduleTerseUpdate();
                //}
            }
            else
            {
                if (m_updateFlag == 2) // is a new prim, just created/reloaded or has major changes
                {
                    AddFullUpdateToAllAvatars();
                    ClearUpdateSchedule();
                }
            }
        }

        #endregion

        #region Shape

        /// <summary>
        /// 
        /// </summary>
        /// <param name="shapeBlock"></param>
        public void UpdateShape(ObjectShapePacket.ObjectDataBlock shapeBlock)
        {
            m_shape.PathBegin = shapeBlock.PathBegin;
            m_shape.PathEnd = shapeBlock.PathEnd;
            m_shape.PathScaleX = shapeBlock.PathScaleX;
            m_shape.PathScaleY = shapeBlock.PathScaleY;
            m_shape.PathShearX = shapeBlock.PathShearX;
            m_shape.PathShearY = shapeBlock.PathShearY;
            m_shape.PathSkew = shapeBlock.PathSkew;
            m_shape.ProfileBegin = shapeBlock.ProfileBegin;
            m_shape.ProfileEnd = shapeBlock.ProfileEnd;
            m_shape.PathCurve = shapeBlock.PathCurve;
            m_shape.ProfileCurve = shapeBlock.ProfileCurve;
            m_shape.ProfileHollow = shapeBlock.ProfileHollow;
            m_shape.PathRadiusOffset = shapeBlock.PathRadiusOffset;
            m_shape.PathRevolutions = shapeBlock.PathRevolutions;
            m_shape.PathTaperX = shapeBlock.PathTaperX;
            m_shape.PathTaperY = shapeBlock.PathTaperY;
            m_shape.PathTwist = shapeBlock.PathTwist;
            m_shape.PathTwistBegin = shapeBlock.PathTwistBegin;
            ScheduleFullUpdate();
        }

        #endregion

        #region ExtraParams

        public void UpdatePrimFlags(ushort type, bool inUse, byte[] data)
        {
            bool usePhysics = false;
            bool IsTemporary = false;
            bool IsPhantom = false;
            bool castsShadows = false;
            bool wasUsingPhysics = ((ObjectFlags & (uint) LLObject.ObjectFlags.Physics) != 0);
            //bool IsLocked = false;
            int i = 0;
            //rex
            LLUUID AgentID = LLUUID.Zero, SessionID = LLUUID.Zero;
            uint ObjectLocalID;

            try
            {
                //rex
                i += 10;
                AgentID = new LLUUID(data, i); i += 16;
                SessionID = new LLUUID(data, i); i += 16;
                ObjectLocalID = (uint)(data[i++] + (data[i++] << 8) + (data[i++] << 16) + (data[i++] << 24));
                //IsLocked = (data[i++] != 0) ? true : false;
                usePhysics = ((data[i++] != 0) && m_parentGroup.Scene.m_physicalPrim) ? true : false;
                //System.Console.WriteLine("U" + packet.ToBytes().Length.ToString());
                IsTemporary = (data[i++] != 0) ? true : false;
                IsPhantom = (data[i++] != 0) ? true : false;
                castsShadows = (data[i++] != 0) ? true : false;
            }
            catch (Exception)
            {
                Console.WriteLine("Ignoring invalid Packet:");
                //Silently ignore it - TODO: FIXME Quick
            }

            #region rex added flags
            if (AgentID == this.OwnerID)
            {
                AddFlag(LLObject.ObjectFlags.ObjectYouOwner);
            }
            else
            {
                RemFlag(LLObject.ObjectFlags.ObjectYouOwner);
            }
            #endregion

            if (usePhysics)
            {
                AddFlag(LLObject.ObjectFlags.Physics);
                if (!wasUsingPhysics)
                {
                    DoPhysicsPropertyUpdate(usePhysics, false);
                }
            }
            else
            {
                RemFlag(LLObject.ObjectFlags.Physics);
                if (wasUsingPhysics)
                {
                    DoPhysicsPropertyUpdate(usePhysics, false);
                }
            }


            if (IsPhantom)
            {
                AddFlag(LLObject.ObjectFlags.Phantom);
                if (PhysActor != null)
                {
                    m_parentGroup.Scene.PhysicsScene.RemovePrim(PhysActor);
                    /// that's not wholesome.  Had to make Scene public
                    PhysActor = null;
                }
            }
            else
            {
                RemFlag(LLObject.ObjectFlags.Phantom);
                if (PhysActor == null)
                {
                    PhysActor = m_parentGroup.Scene.PhysicsScene.AddPrimShape(
                        Name,
                        Shape,
                        new PhysicsVector(AbsolutePosition.X, AbsolutePosition.Y,
                                          AbsolutePosition.Z),
                        new PhysicsVector(Scale.X, Scale.Y, Scale.Z),
                        new Quaternion(RotationOffset.W, RotationOffset.X,
                                       RotationOffset.Y, RotationOffset.Z), usePhysics, LocalID);
                    DoPhysicsPropertyUpdate(usePhysics, true);
                }
                else
                {
                    PhysActor.IsPhysical = usePhysics;
                    DoPhysicsPropertyUpdate(usePhysics, false);
                }
            }

            if (IsTemporary)
            {
                AddFlag(LLObject.ObjectFlags.TemporaryOnRez);
            }
            else
            {
                RemFlag(LLObject.ObjectFlags.TemporaryOnRez);
            }
            //            System.Console.WriteLine("Update:  PHY:" + UsePhysics.ToString() + ", T:" + IsTemporary.ToString() + ", PHA:" + IsPhantom.ToString() + " S:" + CastsShadows.ToString());
            ScheduleFullUpdate();
        }

        public void DoPhysicsPropertyUpdate(bool UsePhysics, bool isNew)
        {
            if (PhysActor != null)
            {
                if (UsePhysics != PhysActor.IsPhysical || isNew)
                {
                    if (PhysActor.IsPhysical)
                    {
                        if (!isNew)
                            ParentGroup.Scene.RemovePhysicalPrim(1);

                        PhysActor.OnRequestTerseUpdate -= PhysicsRequestingTerseUpdate;
                        PhysActor.OnOutOfBounds -= PhysicsOutOfBounds;
                    }

                    PhysActor.IsPhysical = UsePhysics;
                    // If we're not what we're supposed to be in the physics scene, recreate ourselves.
                    //m_parentGroup.Scene.PhysicsScene.RemovePrim(PhysActor);
                    /// that's not wholesome.  Had to make Scene public
                    //PhysActor = null;


                    if ((ObjectFlags & (uint) LLObject.ObjectFlags.Phantom) == 0)
                    {
                        //PhysActor = m_parentGroup.Scene.PhysicsScene.AddPrimShape(
                        //Name,
                        //Shape,
                        //new PhysicsVector(AbsolutePosition.X, AbsolutePosition.Y,
                        //AbsolutePosition.Z),
                        //new PhysicsVector(Scale.X, Scale.Y, Scale.Z),
                        //new Quaternion(RotationOffset.W, RotationOffset.X,
                        //RotationOffset.Y, RotationOffset.Z), UsePhysics);
                        if (UsePhysics)
                        {
                            ParentGroup.Scene.AddPhysicalPrim(1);

                            PhysActor.OnRequestTerseUpdate += PhysicsRequestingTerseUpdate;
                            PhysActor.OnOutOfBounds += PhysicsOutOfBounds;
                        }
                    }
                }
                m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
            }
        }

        public void UpdateExtraParam(ushort type, bool inUse, byte[] data)
        {
            // rex, function fixed for handling multiple parameter blocks and disabling them

            //m_shape.ExtraParams = new byte[data.Length + 7];
            //int i = 0;
            //uint length = (uint) data.Length;
            //m_shape.ExtraParams[i++] = 1;
            //m_shape.ExtraParams[i++] = (byte) (type%256);
            //m_shape.ExtraParams[i++] = (byte) ((type >> 8)%256);

            //m_shape.ExtraParams[i++] = (byte) (length%256);
            //m_shape.ExtraParams[i++] = (byte) ((length >> 8)%256);
            //m_shape.ExtraParams[i++] = (byte) ((length >> 16)%256);
            //m_shape.ExtraParams[i++] = (byte) ((length >> 24)%256);
            //Array.Copy(data, 0, m_shape.ExtraParams, i, data.Length);

            // Amount of param blocks in new & old extra params
            int numOld = 0;
            int numNew = 0;

            // If old param block exists, take its length & amount of param blocks in it
            int totalSizeOld = 0;
            int idxOld = 0;
            if (m_shape.ExtraParams != null)
            {
                numOld = m_shape.ExtraParams[idxOld++];
                totalSizeOld = m_shape.ExtraParams.Length;
            }

            // New extra params: maximum size = old extra params + size of new data + possible new param block header + num of blocks
            byte[] newExtraParams = new byte[totalSizeOld + data.Length + 6 + 1];

            int idxNew = 1; // Don't know the amount of new param blocks yet, fill it later     
            bool isNewBlock = true;

            // Go through each of the old params, and see if this new update disables or changes it
            for (int i = 0; i < numOld; i++)
            {
                int typeOld = m_shape.ExtraParams[idxOld++] | (m_shape.ExtraParams[idxOld++] << 8);
                int lengthOld = m_shape.ExtraParams[idxOld++] | (m_shape.ExtraParams[idxOld++] << 8) |
                                (m_shape.ExtraParams[idxOld++] << 16) | (m_shape.ExtraParams[idxOld++] << 24);

                // Not changed, copy verbatim
                if (typeOld != type)
                {
                    newExtraParams[idxNew++] = (byte)(typeOld % 256);
                    newExtraParams[idxNew++] = (byte)((typeOld >> 8) % 256);
                    newExtraParams[idxNew++] = (byte)(lengthOld % 256);
                    newExtraParams[idxNew++] = (byte)((lengthOld >> 8) % 256);
                    newExtraParams[idxNew++] = (byte)((lengthOld >> 16) % 256);
                    newExtraParams[idxNew++] = (byte)((lengthOld >> 24) % 256);
                    Array.Copy(m_shape.ExtraParams, idxOld, newExtraParams, idxNew, lengthOld);

                    idxNew += lengthOld;
                    numNew++;
                }
                else
                {
                    isNewBlock = false;

                    // Old parameter updated, check if still in use, or if should remove
                    if (inUse)
                    {
                        newExtraParams[idxNew++] = (byte)(type % 256);
                        newExtraParams[idxNew++] = (byte)((type >> 8) % 256);
                        newExtraParams[idxNew++] = (byte)(data.Length % 256);
                        newExtraParams[idxNew++] = (byte)((data.Length >> 8) % 256);
                        newExtraParams[idxNew++] = (byte)((data.Length >> 16) % 256);
                        newExtraParams[idxNew++] = (byte)((data.Length >> 24) % 256);
                        Array.Copy(data, 0, newExtraParams, idxNew, data.Length);

                        idxNew += data.Length;
                        numNew++;
                    }
                }
                idxOld += lengthOld;
            }
            // If type was not listed, create new block
            if ((isNewBlock) && (inUse))
            {
                newExtraParams[idxNew++] = (byte)(type % 256);
                newExtraParams[idxNew++] = (byte)((type >> 8) % 256);
                newExtraParams[idxNew++] = (byte)(data.Length % 256);
                newExtraParams[idxNew++] = (byte)((data.Length >> 8) % 256);
                newExtraParams[idxNew++] = (byte)((data.Length >> 16) % 256);
                newExtraParams[idxNew++] = (byte)((data.Length >> 24) % 256);
                Array.Copy(data, 0, newExtraParams, idxNew, data.Length);

                idxNew += data.Length;
                numNew++;
            }

            // Now we know final size and number of param blocks
            newExtraParams[0] = (byte)numNew;
            m_shape.ExtraParams = new byte[idxNew];
            Array.Copy(newExtraParams, m_shape.ExtraParams, idxNew);

            string OldPythonClass = m_RexClassName;
            LLUUID OldColMesh = m_RexCollisionMeshUUID;
            bool OldMeshScaling = ((m_RexFlags & REXFLAGS_SCALEMESH) != 0);

            GetRexParameters();

            if (m_RexClassName != OldPythonClass)
                m_parentGroup.Scene.EventManager.TriggerOnChangePythonClass(LocalID);

            if (GlobalSettings.Instance.m_3d_collision_models)
            {
                if (m_RexCollisionMeshUUID != OldColMesh && PhysActor != null)
                {
                    if (m_RexCollisionMeshUUID != LLUUID.Zero)
                        RexUpdateCollisionMesh();
                    else
                        PhysActor.SetCollisionMesh(null, "", false);
                }

                bool NewMeshScaling = ((m_RexFlags & REXFLAGS_SCALEMESH) != 0);
                if (NewMeshScaling != OldMeshScaling && PhysActor != null)
                {
                    PhysActor.SetBoundsScaling(NewMeshScaling);
                    m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
                }
            }
            // rexend

            ScheduleFullUpdate();
        }

        // rex, new function, compiles / sends rex parameters after serverside modification
        public void UpdateRexParameters()
        {
            // Compile reX member variables into an extraparam-block
            int size = m_RexClassName.Length + 1 // Name + endzero
                + 1 + 1 + 4 + 4 // Flags, collisiontype, drawdistance, lod
                + 16 + 16 // Mesh UUID & collisionmesh UUID
                + 2 + m_RexMaterialUUID.Count * 16 // Material count and UUID's
                + 1 // Fixed material               
                + 16; // Particle script UUID

            byte[] buffer = new byte[size];
            int idx = 0;

            for (int i = 0; i < m_RexClassName.Length; i++)
            {
                buffer[idx++] = (byte)m_RexClassName[i];
            }
            buffer[idx++] = 0;

            buffer[idx++] = m_RexFlags;

            buffer[idx++] = m_RexCollisionType;

            System.BitConverter.GetBytes(m_RexDrawDistance).CopyTo(buffer, idx);
            idx += 4;

            System.BitConverter.GetBytes(m_RexLOD).CopyTo(buffer, idx);
            idx += 4;

            m_RexMeshUUID.GetBytes().CopyTo(buffer, idx);
            idx += 16;

            m_RexCollisionMeshUUID.GetBytes().CopyTo(buffer, idx);
            idx += 16;

            System.BitConverter.GetBytes((short)m_RexMaterialUUID.Count).CopyTo(buffer, idx);
            idx += 2;
            for (int i = 0; i < m_RexMaterialUUID.Count; i++)
            {
                m_RexMaterialUUID[i].GetBytes().CopyTo(buffer, idx);
                idx += 16;
            }

            buffer[idx++] = m_RexFixedMaterial;

            m_RexParticleScriptUUID.GetBytes().CopyTo(buffer, idx);
            idx += 16;

            UpdateExtraParam(PARAMS_REX, true, buffer);
        }

        // rex, new function, extract reX parameters from the parameter block
        public void GetRexParameters()
        {
            if (m_shape.ExtraParams == null) return;

            int idx = 0;
            int numParams = m_shape.ExtraParams[idx++];

            for (int i = 0; i < numParams; i++)
            {
                // Is this the reX parameter block?
                int type = m_shape.ExtraParams[idx++] | (m_shape.ExtraParams[idx++] << 8);
                int length = m_shape.ExtraParams[idx++] | (m_shape.ExtraParams[idx++] << 8) |
                            (m_shape.ExtraParams[idx++] << 16) | (m_shape.ExtraParams[idx++] << 24);
                int start = idx;

                if (type == PARAMS_REX)
                {
                    // Class name
                    StringBuilder buffer = new StringBuilder();
                    while ((idx < (length + start)) && (m_shape.ExtraParams[idx] != 0))
                    {
                        char c = (char)m_shape.ExtraParams[idx++];
                        buffer.Append(c);
                    }
                    m_RexClassName = buffer.ToString();
                    idx++;

                    // Rex flags
                    if (idx < (length + start))
                    {
                        m_RexFlags = m_shape.ExtraParams[idx++];
                    }

                    // Collision type
                    if (idx < (length + start))
                    {
                        m_RexCollisionType = m_shape.ExtraParams[idx++];
                    }

                    // Draw distance
                    if (idx < (length + start - 3))
                    {
                        m_RexDrawDistance = System.BitConverter.ToSingle(m_shape.ExtraParams, idx);
                        idx += 4;
                    }

                    // Mesh LOD
                    if (idx < (length + start - 3))
                    {
                        m_RexLOD = System.BitConverter.ToSingle(m_shape.ExtraParams, idx);
                        idx += 4;
                    }

                    // Mesh UUID
                    if (idx < (length + start - 15))
                    {
                        m_RexMeshUUID = new LLUUID(m_shape.ExtraParams, idx);
                        idx += 16;
                    }

                    // Collision mesh UUID
                    if (idx < (length + start - 15))
                    {
                        m_RexCollisionMeshUUID = new LLUUID(m_shape.ExtraParams, idx);
                        idx += 16;
                    }

                    // Number of materials
                    if (idx < (length + start - 1))
                    {
                        short rexMaterials = System.BitConverter.ToInt16(m_shape.ExtraParams, idx);
                        idx += 2;
                        m_RexMaterialUUID = new List<LLUUID>();

                        for (short j = 0; j < rexMaterials; j++)
                        {
                            if (idx < (length + start - 15))
                            {
                                m_RexMaterialUUID.Add(new LLUUID(m_shape.ExtraParams, idx));
                                idx += 16;
                            }
                            else break;
                        }
                    }
                    // Fixed material
                    if (idx < (length + start))
                    {
                       m_RexFixedMaterial = m_shape.ExtraParams[idx++];
                    }
                    // Particle script UUID
                    if (idx < (length + start - 15))
                    {
                        m_RexParticleScriptUUID = new LLUUID(m_shape.ExtraParams, idx);
                        idx += 16;
                    }

                    //System.Console.WriteLine("Rex parameters of an object updated");
                    //System.Console.WriteLine("Rex class name: " + m_RexClassName);
                    //System.Console.WriteLine("Rex flags: " + (short)m_RexFlags);
                    //System.Console.WriteLine("Rex collision type: " + (short)m_RexCollisionType);
                    //System.Console.WriteLine("Rex draw distance: " + m_RexDrawDistance);
                    //System.Console.WriteLine("Rex LOD: " + m_RexLOD);
                    //System.Console.WriteLine("Rex mesh UUID: " + m_RexMeshUUID);
                    //System.Console.WriteLine("Rex collisionmesh UUID: " + m_RexCollisionMeshUUID);
                    //System.Console.WriteLine("Rex material count: " + m_RexMaterialUUID.Count);
                    //for (int j = 0; j < m_RexMaterialUUID.Count; j++)
                    //{
                    //    System.Console.WriteLine("Rex material UUID " + j + ": " + m_RexMaterialUUID[j]);
                    //}
                    break;
                }
                else idx += length;
            }
        }

        #endregion

        #region Physics

        public float GetMass()
        {
            if (PhysActor != null)
            {
                return PhysActor.Mass;
            }
            else
            {
                return 0;
            }
        }

        // rex, added
        public void SetMass(float vValue)
        {
            if (PhysActor != null)
            {
                // PhysActor.Mass = vValue;
            }
        }

        // rex, added
        public bool GetUsePrimVolumeCollision()
        {
            if (PhysActor != null)
                return (PhysActor.PhysicsActorType == 4);
            else
                return false;
        }

        // rex, added
        public void SetUsePrimVolumeCollision(bool vUseVolumeCollision)
        {
            if (PhysActor != null)
            {
                if (vUseVolumeCollision)
                {
                    if (PhysActor.PhysicsActorType != 4)
                        PhysActor.OnCollisionUpdate += PhysicsCollisionUpdate;
                    PhysActor.PhysicsActorType = 4;
                }
                else
                {
                    if (PhysActor.PhysicsActorType == 4)
                        PhysActor.OnCollisionUpdate -= PhysicsCollisionUpdate;

                    PhysActor.PhysicsActorType = 2;
                }
            }
        }

        // rex, added
        private void PhysicsCollisionUpdate(EventArgs e)
        {
            if (PhysActor != null && PhysActor.PhysicsActorType == 4)
                m_parentGroup.Scene.EventManager.TriggerOnPrimVolumeCollision(LocalID, (e as CollisionEventUpdate).m_LocalID);
        }



        // rex, added
        public void RexUpdateCollisionMesh()
        {
            if (!GlobalSettings.Instance.m_3d_collision_models)
                return;

            if (m_RexCollisionMeshUUID != LLUUID.Zero && PhysActor != null)
            {
                bool ScaleMesh = ((m_RexFlags & REXFLAGS_SCALEMESH) != 0);
                AssetBase tempmodel = m_parentGroup.Scene.AssetCache.FetchAsset(m_RexCollisionMeshUUID);
                if (tempmodel != null)
                    PhysActor.SetCollisionMesh(tempmodel.Data, tempmodel.Name, ScaleMesh);
            }
        }



        public LLVector3 GetGeometricCenter()
        {
            if (PhysActor != null)
            {
                return new LLVector3(PhysActor.CenterOfMass.X, PhysActor.CenterOfMass.Y, PhysActor.CenterOfMass.Z);
            }
            else
            {
                return new LLVector3(0, 0, 0);
            }
        }

        #endregion

        #region Texture

        /// <summary>
        /// 
        /// </summary>
        /// <param name="textureEntry"></param>
        public void UpdateTextureEntry(byte[] textureEntry)
        {
            m_shape.TextureEntry = textureEntry;
            ScheduleFullUpdate();
        }

        // Added to handle bug in libsecondlife's TextureEntry.ToBytes() 
        // not handling RGBA properly. Cycles through, and "fixes" the color
        // info
        public void UpdateTexture(LLObject.TextureEntry tex)
        {
            //LLColor tmpcolor;
            //for (uint i = 0; i < 32; i++)
            //{
            //    if (tex.FaceTextures[i] != null)
            //    {
            //        tmpcolor = tex.GetFace((uint) i).RGBA;
            //        tmpcolor.A = tmpcolor.A*255;
            //        tmpcolor.R = tmpcolor.R*255;
            //        tmpcolor.G = tmpcolor.G*255;
            //        tmpcolor.B = tmpcolor.B*255;
            //        tex.FaceTextures[i].RGBA = tmpcolor;
            //    }
            //}
            //tmpcolor = tex.DefaultTexture.RGBA;
            //tmpcolor.A = tmpcolor.A*255;
            //tmpcolor.R = tmpcolor.R*255;
            //tmpcolor.G = tmpcolor.G*255;
            //tmpcolor.B = tmpcolor.B*255;
            //tex.DefaultTexture.RGBA = tmpcolor;
            UpdateTextureEntry(tex.ToBytes());
        }

        #endregion

        #region ParticleSystem

        public void AddNewParticleSystem(Primitive.ParticleSystem pSystem)
        {
            m_particleSystem = pSystem.GetBytes();
        }

        #endregion

        #region Position

        public void AttachToAvatar(LLUUID agentId, ScenePresence presence, byte attachPt, LLQuaternion rotation,  RegionInfo regionInfo)
        {
            m_attachAgentId = agentId;
            m_attachPresence = presence;
            m_attachPt = attachPt;
            m_attachRot = rotation;
            m_attachRegInfo = regionInfo;

            RotationOffset = new LLQuaternion(0, 0, 0, 1);

            ScheduleFullUpdate();
        }

        public void Detach()
        {
            m_attachPresence = null;
            ScheduleFullUpdate();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        public void UpdateOffSet(LLVector3 pos)
        {
            LLVector3 newPos = new LLVector3(pos.X, pos.Y, pos.Z);
            OffsetPosition = newPos;
            ScheduleTerseUpdate();
        }

        public void UpdateGroupPosition(LLVector3 pos)
        {
            LLVector3 newPos = new LLVector3(pos.X, pos.Y, pos.Z);
            GroupPosition = newPos;
            ScheduleTerseUpdate();
        }

        #endregion

        #region rotation

        public void UpdateRotation(LLQuaternion rot)
        {
            RotationOffset = new LLQuaternion(rot.X, rot.Y, rot.Z, rot.W);
            ScheduleTerseUpdate();
        }

        #endregion

        #region Resizing/Scale

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scale"></param>
        public void Resize(LLVector3 scale)
        {
            m_shape.Scale = scale;
            ScheduleFullUpdate();
        }

        #endregion

        public void UpdatePermissions(LLUUID AgentID, byte field, uint localID, uint mask, byte addRemTF)
        {
            // Are we the owner?
            if (AgentID == OwnerID)
            {
                MainLog.Instance.Verbose("PERMISSIONS",
                                         "field: " + field.ToString() + ", mask: " + mask.ToString() + " addRemTF: " +
                                         addRemTF.ToString());

                //Field 8 = EveryoneMask
                if (field == (byte) 8)
                {
                    MainLog.Instance.Verbose("PERMISSIONS", "Left over: " + (OwnerMask - EveryoneMask));
                    if (addRemTF == (byte) 0)
                    {
                        //EveryoneMask = (uint)0;
                        EveryoneMask &= ~mask;
                        //EveryoneMask &= ~(uint)57344;
                    }
                    else
                    {
                        //EveryoneMask = (uint)0;
                        EveryoneMask |= mask;
                        //EveryoneMask |= (uint)57344;
                    }
                    //ScheduleFullUpdate();
                    SendFullUpdateToAllClients();
                }
                //Field 16 = NextownerMask
                if (field == (byte) 16)
                {
                    if (addRemTF == (byte) 0)
                    {
                        NextOwnerMask &= ~mask;
                    }
                    else
                    {
                        NextOwnerMask |= mask;
                    }
                    SendFullUpdateToAllClients();
                }
            }
        }

        #region Client Update Methods

        public void AddFullUpdateToAllAvatars()
        {
            List<ScenePresence> avatars = m_parentGroup.GetScenePresences();
            for (int i = 0; i < avatars.Count; i++)
            {
                avatars[i].QueuePartForUpdate(this);
            }
        }

        public void AddFullUpdateToAvatar(ScenePresence presence)
        {
            presence.QueuePartForUpdate(this);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendFullUpdateToAllClients()
        {
            List<ScenePresence> avatars = m_parentGroup.GetScenePresences();
            for (int i = 0; i < avatars.Count; i++)
            {
                // Ugly reference :(
                m_parentGroup.SendPartFullUpdate(avatars[i].ControllingClient, this,
                                                 avatars[i].GenerateClientFlags(UUID));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        public void SendFullUpdate(IClientAPI remoteClient, uint clientFlags)
        {
            m_parentGroup.SendPartFullUpdate(remoteClient, this, clientFlags);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        public void SendFullUpdateToClient(IClientAPI remoteClient, uint clientflags)
        {
            LLVector3 lPos;
            lPos = OffsetPosition;
            SendFullUpdateToClient(remoteClient, lPos, clientflags);
        }

        public void SendAttachedUpdateToClient(IClientAPI remoteClient, LLVector3 lPos, uint clientFlags)
        {
            MainLog.Instance.Verbose("OBJECTPART", "Sending as attached object to " + remoteClient.FirstName + " " + remoteClient.LastName);
            ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
            objupdate.RegionData.RegionHandle = m_attachRegInfo.RegionHandle;
            objupdate.RegionData.TimeDilation = 64096;
            objupdate.ObjectData = new ObjectUpdatePacket.ObjectDataBlock[2];

            // avatar stuff - horrible group copypaste
            objupdate.ObjectData[0] = new ObjectUpdatePacket.ObjectDataBlock();
            objupdate.ObjectData[0].PSBlock = new byte[0];
            objupdate.ObjectData[0].ExtraParams = new byte[1];
            objupdate.ObjectData[0].MediaURL = new byte[0];
            objupdate.ObjectData[0].NameValue = new byte[0];
            objupdate.ObjectData[0].Text = new byte[0];
            objupdate.ObjectData[0].TextColor = new byte[4];
            objupdate.ObjectData[0].JointAxisOrAnchor = new LLVector3(0, 0, 0);
            objupdate.ObjectData[0].JointPivot = new LLVector3(0, 0, 0);
            objupdate.ObjectData[0].Material = 4;
            objupdate.ObjectData[0].TextureAnim = new byte[0];
            objupdate.ObjectData[0].Sound = LLUUID.Zero;

            objupdate.ObjectData[0].State = 0;
            objupdate.ObjectData[0].Data = new byte[0];

            objupdate.ObjectData[0].ObjectData = new byte[76];
            objupdate.ObjectData[0].ObjectData[15] = 128;
            objupdate.ObjectData[0].ObjectData[16] = 63;
            objupdate.ObjectData[0].ObjectData[56] = 128;
            objupdate.ObjectData[0].ObjectData[61] = 102;
            objupdate.ObjectData[0].ObjectData[62] = 40;
            objupdate.ObjectData[0].ObjectData[63] = 61;
            objupdate.ObjectData[0].ObjectData[64] = 189;


            objupdate.ObjectData[0].UpdateFlags = 61 + (9 << 8) + (130 << 16) + (16 << 24);
            objupdate.ObjectData[0].PathCurve = 16;
            objupdate.ObjectData[0].ProfileCurve = 1;
            objupdate.ObjectData[0].PathScaleX = 100;
            objupdate.ObjectData[0].PathScaleY = 100;
            objupdate.ObjectData[0].ParentID = 0;
            objupdate.ObjectData[0].OwnerID = LLUUID.Zero;
            objupdate.ObjectData[0].Scale = new LLVector3(1, 1, 1);
            objupdate.ObjectData[0].PCode = 47;
            objupdate.ObjectData[0].TextureEntry = ScenePresence.DefaultTexture;

            objupdate.ObjectData[0].ID = m_attachPresence.LocalId;
            objupdate.ObjectData[0].FullID = m_attachAgentId;
            objupdate.ObjectData[0].ParentID = 0;
            objupdate.ObjectData[0].NameValue =
                   Helpers.StringToField("FirstName STRING RW SV " + m_attachPresence.Firstname + "\nLastName STRING RW SV " + m_attachPresence.Lastname);
            LLVector3 pos2 = m_attachPresence.AbsolutePosition;
            // new LLVector3((float) Pos.X, (float) Pos.Y, (float) Pos.Z);
            byte[] pb = pos2.GetBytes();
            Array.Copy(pb, 0, objupdate.ObjectData[0].ObjectData, 16, pb.Length);


            // primitive part
            objupdate.ObjectData[1] = new ObjectUpdatePacket.ObjectDataBlock();
            // SetDefaultPrimPacketValues
            objupdate.ObjectData[1].PSBlock = new byte[0];
            objupdate.ObjectData[1].ExtraParams = new byte[1];
            objupdate.ObjectData[1].MediaURL = new byte[0];
            objupdate.ObjectData[1].NameValue = new byte[0];
            objupdate.ObjectData[1].Text = new byte[0];
            objupdate.ObjectData[1].TextColor = new byte[4];
            objupdate.ObjectData[1].JointAxisOrAnchor = new LLVector3(0, 0, 0);
            objupdate.ObjectData[1].JointPivot = new LLVector3(0, 0, 0);
            objupdate.ObjectData[1].Material = 3;
            objupdate.ObjectData[1].TextureAnim = new byte[0];
            objupdate.ObjectData[1].Sound = LLUUID.Zero;
            objupdate.ObjectData[1].State = 0;
            objupdate.ObjectData[1].Data = new byte[0];

            objupdate.ObjectData[1].ObjectData = new byte[60];
            objupdate.ObjectData[1].ObjectData[46] = 128;
            objupdate.ObjectData[1].ObjectData[47] = 63;

            // SetPrimPacketShapeData
            PrimitiveBaseShape primData = Shape;

            objupdate.ObjectData[1].TextureEntry = primData.TextureEntry;
            objupdate.ObjectData[1].PCode = primData.PCode;
            objupdate.ObjectData[1].State = (byte)(((byte)m_attachPt) << 4);
            objupdate.ObjectData[1].PathBegin = primData.PathBegin;
            objupdate.ObjectData[1].PathEnd = primData.PathEnd;
            objupdate.ObjectData[1].PathScaleX = primData.PathScaleX;
            objupdate.ObjectData[1].PathScaleY = primData.PathScaleY;
            objupdate.ObjectData[1].PathShearX = primData.PathShearX;
            objupdate.ObjectData[1].PathShearY = primData.PathShearY;
            objupdate.ObjectData[1].PathSkew = primData.PathSkew;
            objupdate.ObjectData[1].ProfileBegin = primData.ProfileBegin;
            objupdate.ObjectData[1].ProfileEnd = primData.ProfileEnd;
            objupdate.ObjectData[1].Scale = primData.Scale;
            objupdate.ObjectData[1].PathCurve = primData.PathCurve;
            objupdate.ObjectData[1].ProfileCurve = primData.ProfileCurve;
            objupdate.ObjectData[1].ProfileHollow = primData.ProfileHollow;
            objupdate.ObjectData[1].PathRadiusOffset = primData.PathRadiusOffset;
            objupdate.ObjectData[1].PathRevolutions = primData.PathRevolutions;
            objupdate.ObjectData[1].PathTaperX = primData.PathTaperX;
            objupdate.ObjectData[1].PathTaperY = primData.PathTaperY;
            objupdate.ObjectData[1].PathTwist = primData.PathTwist;
            objupdate.ObjectData[1].PathTwistBegin = primData.PathTwistBegin;
            objupdate.ObjectData[1].ExtraParams = primData.ExtraParams;

            objupdate.ObjectData[1].UpdateFlags = 276957500; // flags;  // ??
            objupdate.ObjectData[1].ID = LocalID;
            objupdate.ObjectData[1].FullID = UUID;
            objupdate.ObjectData[1].OwnerID = OwnerID;
            objupdate.ObjectData[1].Text = Helpers.StringToField(Text);
            objupdate.ObjectData[1].TextColor[0] = 255;
            objupdate.ObjectData[1].TextColor[1] = 255;
            objupdate.ObjectData[1].TextColor[2] = 255;
            objupdate.ObjectData[1].TextColor[3] = 128;
            objupdate.ObjectData[1].ParentID = objupdate.ObjectData[0].ID;
            //objupdate.ObjectData[1].PSBlock = particleSystem;
            //objupdate.ObjectData[1].ClickAction = clickAction;
            objupdate.ObjectData[1].Radius = 20;
            objupdate.ObjectData[1].NameValue =
            Helpers.StringToField("AttachItemID STRING RW SV " + UUID);
            LLVector3 pos = new LLVector3((float)0.0, (float)0.0, (float)0.0);

            pb = pos.GetBytes();
            Array.Copy(pb, 0, objupdate.ObjectData[1].ObjectData, 0, pb.Length);

            byte[] brot = m_attachRot.GetBytes();
            Array.Copy(brot, 0, objupdate.ObjectData[1].ObjectData, 36, brot.Length);

            remoteClient.OutPacket(objupdate, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="lPos"></param>
        public void SendFullUpdateToClient(IClientAPI remoteClient, LLVector3 lPos, uint clientFlags)
        {
            if (m_attachPresence != null)
            {
                SendAttachedUpdateToClient(remoteClient, lPos, clientFlags);
                return;
            }

            LLQuaternion lRot;
            lRot = RotationOffset;
            clientFlags &= ~(uint) LLObject.ObjectFlags.CreateSelected;

            if (remoteClient.AgentId == OwnerID)
            {
                if ((uint) (m_flags & LLObject.ObjectFlags.CreateSelected) != 0)
                {
                    clientFlags |= (uint) LLObject.ObjectFlags.CreateSelected;
                    m_flags &= ~LLObject.ObjectFlags.CreateSelected;
                }
            }


            byte[] color = new byte[] {m_color.R, m_color.G, m_color.B, m_color.A};
            remoteClient.SendPrimitiveToClient(m_regionHandle, 64096, LocalID, m_shape, lPos, clientFlags, m_uuid,
                                               OwnerID,
                                               m_text, color, ParentID, m_particleSystem, lRot, m_clickAction);
        }

        /// Terse updates
        public void AddTerseUpdateToAllAvatars()
        {
            List<ScenePresence> avatars = m_parentGroup.GetScenePresences();
            for (int i = 0; i < avatars.Count; i++)
            {
                avatars[i].QueuePartForUpdate(this);
            }
        }

        public void AddTerseUpdateToAvatar(ScenePresence presence)
        {
            presence.QueuePartForUpdate(this);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendTerseUpdateToAllClients()
        {
            List<ScenePresence> avatars = m_parentGroup.GetScenePresences();
            for (int i = 0; i < avatars.Count; i++)
            {
                m_parentGroup.SendPartTerseUpdate(avatars[i].ControllingClient, this);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        public void SendTerseUpdate(IClientAPI remoteClient)
        {
            m_parentGroup.SendPartTerseUpdate(remoteClient, this);
        }

        public void SendTerseUpdateToClient(IClientAPI remoteClient)
        {
            LLVector3 lPos;
            lPos = OffsetPosition;
            LLQuaternion mRot = RotationOffset;
            if ((ObjectFlags & (uint) LLObject.ObjectFlags.Physics) == 0)
            {
                remoteClient.SendPrimTerseUpdate(m_regionHandle, 64096, LocalID, lPos, mRot);
            }
            else
            {
                remoteClient.SendPrimTerseUpdate(m_regionHandle, 64096, LocalID, lPos, mRot, Velocity,
                                                 RotationalVelocity);
            }
        }

        public void SendTerseUpdateToClient(IClientAPI remoteClient, LLVector3 lPos)
        {
            LLQuaternion mRot = RotationOffset;
            if ((ObjectFlags & (uint) LLObject.ObjectFlags.Physics) == 0)
            {
                remoteClient.SendPrimTerseUpdate(m_regionHandle, 64096, LocalID, lPos, mRot);
            }
            else
            {
                remoteClient.SendPrimTerseUpdate(m_regionHandle, 64096, LocalID, lPos, mRot, Velocity,
                                                 RotationalVelocity);
                //System.Console.WriteLine("RVel:" + RotationalVelocity);
            }
        }

        #endregion

        public virtual void UpdateMovement()
        {
        }

        #region Events

        public void PhysicsRequestingTerseUpdate()
        {
            ScheduleTerseUpdate();

            //SendTerseUpdateToAllClients();
        }

        #endregion

        public void PhysicsOutOfBounds(PhysicsVector pos)
        {
            MainLog.Instance.Verbose("PHYSICS", "Physical Object went out of bounds.");
            RemFlag(LLObject.ObjectFlags.Physics);
            DoPhysicsPropertyUpdate(false, true);
            m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
        }

        public virtual void OnGrab(LLVector3 offsetPos, IClientAPI remoteClient)
        {
        }

        public void SetText(string text, Vector3 color, double alpha)
        {
            Color = Color.FromArgb(0xff - (int) (alpha*0xff),
                                   (int) (color.x*0xff),
                                   (int) (color.y*0xff),
                                   (int) (color.z*0xff));
            Text = text;
        }
    }
}

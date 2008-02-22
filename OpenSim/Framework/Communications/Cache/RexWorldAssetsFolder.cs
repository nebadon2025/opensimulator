// Rex, new file
using System.Collections.Generic;
using System.IO;
using System.Xml;
using libsecondlife;
using Nini.Config;

using OpenSim.Framework.Console;

namespace OpenSim.Framework.Communications.Cache
{
    // Rex, new class implementing the world assets folder
    public class RexWorldAssetsFolder : InventoryFolderImpl
    {
        private LLUUID libOwner = new LLUUID("11111111-1111-0000-0000-000100bba001");
        private InventoryFolderImpl m_WorldTexturesFolder;
        private InventoryFolderImpl m_World3DModelsFolder;
        private AssetCache AssetCache;

        public RexWorldAssetsFolder(AssetCache vAssetCache)
        {
            MainLog.Instance.Verbose("LIBRARYINVENTORY", "Creating World library folder");
            AssetCache = vAssetCache;
   
            agentID = libOwner;
            folderID = new LLUUID("00000112-000f-0000-0000-000100bba005");
            name = "World Library";
            parentID = new LLUUID("00000112-000f-0000-0000-000100bba000");
            type = (short)8;
            version = (ushort)1;

            CreateNewSubFolder(new LLUUID("00000112-000f-0000-0000-000100bba006"), "Textures", (ushort)8);
            m_WorldTexturesFolder = HasSubFolder("00000112-000f-0000-0000-000100bba006");

            CreateNewSubFolder(new LLUUID("00000112-000f-0000-0000-000100bba007"), "3D Models", (ushort)8);
            m_World3DModelsFolder = HasSubFolder("00000112-000f-0000-0000-000100bba007");
        }

        public InventoryItemBase CreateItem(LLUUID inventoryID, LLUUID assetID, string name, string description,
                                            int assetType, int invType, LLUUID parentFolderID)
        {
            InventoryItemBase item = new InventoryItemBase();
            item.avatarID = libOwner;
            item.creatorsID = libOwner;
            item.inventoryID = inventoryID;
            item.assetID = assetID;
            item.inventoryDescription = description;
            item.inventoryName = name;
            item.assetType = assetType;
            item.invType = invType;
            item.parentFolderID = parentFolderID;
            item.inventoryBasePermissions = 0x7FFFFFFF;
            item.inventoryEveryOnePermissions = 0x7FFFFFFF;
            item.inventoryCurrentPermissions = 0x7FFFFFFF;
            item.inventoryNextPermissions = 0x7FFFFFFF;
            return item;
        }

        // Rex, function added.
        public void UpdateWorldAssetFolders()
        {
            // Textures
            List<AssetBase> allTex = AssetCache.GetAssetList(0);
            m_WorldTexturesFolder.ClearFolder();

            InventoryItemBase item;
            foreach (AssetBase asset in allTex)
            {
                item = CreateItem(LLUUID.Random(), asset.FullID, asset.Name, asset.Description, (int)AssetType.Texture, (int)InventoryType.Texture, m_WorldTexturesFolder.folderID);
                m_WorldTexturesFolder.Items.Add(item.inventoryID, item);
            }

            // 3D Models
            List<AssetBase> allModels = AssetCache.GetAssetList(6);
            m_World3DModelsFolder.ClearFolder();
            foreach (AssetBase asset in allModels)
            {
                if (asset.Name != "Primitive")
                {
                    item = CreateItem(LLUUID.Random(), asset.FullID, asset.Name, asset.Description, 43, 6, m_World3DModelsFolder.folderID);
                    m_World3DModelsFolder.Items.Add(item.inventoryID, item);
                }
            }
        }
    }
}

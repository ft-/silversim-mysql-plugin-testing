﻿// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3 with
// the following clarification and special exception.

// Linking this library statically or dynamically with other modules is
// making a combined work based on this library. Thus, the terms and
// conditions of the GNU Affero General Public License cover the whole
// combination.

// As a special exception, the copyright holders of this library give you
// permission to link this library with independent modules to produce an
// executable, regardless of the license terms of these independent
// modules, and to copy and distribute the resulting executable under
// terms of your choice, provided that you also meet, for each linked
// independent module, the terms and conditions of the license of that
// module. An independent module is a module which is not derived from
// or based on this library. If you modify this library, you may extend
// this exception to your version of the library, but you are not
// obligated to do so. If you do not wish to do so, delete this
// exception statement from your version.

using MySql.Data.MySqlClient;
using SilverSim.Scene.Types.Object;
using SilverSim.ServiceInterfaces.Statistics;
using SilverSim.Threading;
using SilverSim.Types;
using SilverSim.Types.StructuredData.Llsd;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace SilverSim.Database.MySQL.SimulationData
{
    public partial class MySQLSimulationDataStorage
    {
        private readonly RwLockedList<MySQLSceneListener> m_SceneListenerThreads = new RwLockedList<MySQLSceneListener>();
        public class MySQLSceneListener : SceneListener
        {
            private readonly string m_ConnectionString;
            private readonly RwLockedList<MySQLSceneListener> m_SceneListenerThreads;

            public MySQLSceneListener(string connectionString, UUID regionID, RwLockedList<MySQLSceneListener> sceneListenerThreads)
                : base(regionID)
            {
                m_ConnectionString = connectionString;
                m_SceneListenerThreads = sceneListenerThreads;
            }

            public QueueStat GetStats()
            {
                int count = m_StorageMainRequestQueue.Count;
                return new QueueStat(count != 0 ? "PROCESSING" : "IDLE", count, (uint)m_ProcessedPrims);
            }

            private int m_ProcessedPrims;

            public struct PrimKey : IEquatable<PrimKey>, IComparable<PrimKey>
            {
                public readonly UUID PartID;
                public readonly UUID ItemID;

                public PrimKey(ObjectInventoryUpdateInfo info)
                {
                    PartID = info.PartID;
                    ItemID = info.ItemID;
                }

                public int CompareTo(PrimKey other)
                {
                    int i = PartID.CompareTo(other.PartID);
                    if (i == 0)
                    {
                        i = ItemID.CompareTo(other.ItemID);
                    }

                    return i;
                }

                public bool Equals(PrimKey other)
                {
                    return PartID.Equals(other.PartID) && ItemID.Equals(other.ItemID);
                }

                public override int GetHashCode()
                {
                    return PartID.GetHashCode() ^ ItemID.GetHashCode();
                }
            }

            private readonly C5.TreeDictionary<PrimKey, bool> m_PrimItemDeletions = new C5.TreeDictionary<PrimKey, bool>();
            private readonly C5.TreeDictionary<PrimKey, ObjectInventoryUpdateInfo> m_PrimItemUpdates = new C5.TreeDictionary<PrimKey, ObjectInventoryUpdateInfo>();

            protected override void OnUpdate(ObjectInventoryUpdateInfo info)
            {
                if(info.IsRemoved)
                {
                    m_PrimItemUpdates.Remove(new PrimKey(info));
                    m_PrimItemDeletions[new PrimKey(info)] = true;
                }
                else
                {
                    m_PrimItemUpdates[new PrimKey(info)] = info;
                }
            }

            protected override bool HasPendingData =>
                !m_PrimDeletions.IsEmpty || !m_PrimUpdates.IsEmpty ||
                !m_PrimItemDeletions.IsEmpty || !m_PrimItemUpdates.IsEmpty ||
                !m_GroupDeletions.IsEmpty || !m_GroupUpdates.IsEmpty;

            private readonly C5.TreeDictionary<UUID, bool> m_PrimDeletions = new C5.TreeDictionary<UUID, bool>();
            private readonly C5.TreeDictionary<UUID, Dictionary<string, object>> m_PrimUpdates = new C5.TreeDictionary<UUID, Dictionary<string, object>>();
            private readonly C5.TreeDictionary<UUID, int> m_PrimSerials = new C5.TreeDictionary<UUID, int>();

            private readonly C5.TreeDictionary<UUID, bool> m_GroupDeletions = new C5.TreeDictionary<UUID, bool>();
            private readonly C5.TreeDictionary<UUID, Dictionary<string, object>> m_GroupUpdates = new C5.TreeDictionary<UUID, Dictionary<string, object>>();

            protected override void OnUpdate(ObjectUpdateInfo info)
            {
                if (info.IsKilled)
                {
                    if (info.Part.ObjectGroup.RootPart == info.Part)
                    {
                        m_GroupUpdates.Remove(info.ID);
                        m_GroupDeletions[info.ID] = true;
                    }
                    m_PrimUpdates.Remove(info.ID);
                    m_PrimDeletions[info.ID] = true;
                }
                else
                {
                    bool havePrimSerial = m_PrimSerials.Contains(info.ID);
                    if (havePrimSerial && m_PrimSerials[info.ID] == info.Part.SerialNumber)
                    {
                        /* ignore update */
                    }
                    else
                    {
                        if(!havePrimSerial)
                        {
                            foreach(ObjectPartInventoryItem item in info.Part.Inventory.Values)
                            {
                                ObjectInventoryUpdateInfo invinfo = item.UpdateInfo;
                                m_PrimItemUpdates[new PrimKey(invinfo)] = invinfo;
                            }
                        }
                        if (info.Part.ObjectGroup.RootPart != info.Part)
                        {
                            m_GroupDeletions[info.ID] = true;
                        }
                        Dictionary<string, object> data = GenerateUpdateObjectPart(info.Part);
                        data["RegionID"] = m_RegionID;
                        m_PrimDeletions.Remove(info.ID);
                        m_PrimUpdates[info.ID] = data;
                        ObjectGroup grp = info.Part.ObjectGroup;
                        m_GroupDeletions.Remove(grp.ID);
                        m_GroupUpdates[grp.ID] = GenerateUpdateObjectGroup(grp);
                    }
                }
            }

            private void ProcessPrimItemDeletions(MySqlConnection conn)
            {
                var sb = new StringBuilder();

                var removedItems = new List<PrimKey>();

                foreach (PrimKey k in m_PrimItemDeletions.Keys.ToArray())
                {
                    if (sb.Length != 0)
                    {
                        sb.Append(" OR ");
                    }
                    else
                    {
                        sb.Append("DELETE FROM primitems WHERE ");
                    }

                    sb.AppendFormat("(RegionID = \"{0}\" AND PrimID = \"{1}\" AND InventoryID = \"{2}\")",
                        m_RegionID, k.PartID, k.ItemID);
                    removedItems.Add(k);
                    if (removedItems.Count == 255)
                    {
                        using (var cmd = new MySqlCommand(sb.ToString(), conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                        foreach (PrimKey r in removedItems)
                        {
                            m_PrimItemDeletions.Remove(r);
                        }
                        sb.Clear();
                        removedItems.Clear();
                    }
                }

                if (removedItems.Count != 0)
                {
                    using (var cmd = new MySqlCommand(sb.ToString(), conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    foreach (PrimKey r in removedItems)
                    {
                        m_PrimItemDeletions.Remove(r);
                    }
                }
            }

            private void ProcessPrimDeletions(MySqlConnection conn)
            {
                var removedItems = new List<UUID>();

                foreach (UUID k in m_PrimDeletions.Keys.ToArray())
                {
                    removedItems.Add(k);
                    if (removedItems.Count == 255)
                    {
                        string c1 = string.Format("DELETE FROM prims WHERE RegionID = \"{0}\" AND (ID=\"{1}\")",
                            m_RegionID,
                            string.Join("\" OR ID=\"", removedItems));
                        string c2 = string.Format("DELETE FROM primitems WHERE RegionID = \"{0}\" AND (PrimID=\"{1}\")",
                            m_RegionID,
                            string.Join("\" OR PrimID=\"", removedItems));
                        using (var cmd = new MySqlCommand(c1, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }

                        using (var cmd = new MySqlCommand(c2, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                        foreach (UUID r in removedItems)
                        {
                            m_PrimDeletions.Remove(r);
                            Interlocked.Increment(ref m_ProcessedPrims);
                        }
                        removedItems.Clear();
                    }
                }

                if (removedItems.Count != 0)
                {
                    string c1 = string.Format("DELETE FROM prims WHERE RegionID = \"{0}\" AND (ID=\"{1}\")",
                        m_RegionID,
                        string.Join("\" OR ID=\"", removedItems));
                    string c2 = string.Format("DELETE FROM primitems WHERE RegionID = \"{0}\" AND (PrimID=\"{1}\")",
                        m_RegionID,
                        string.Join("\" OR PrimID=\"", removedItems));
                    using (var cmd = new MySqlCommand(c1, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmd = new MySqlCommand(c2, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    foreach (UUID r in removedItems)
                    {
                        m_PrimDeletions.Remove(r);
                        Interlocked.Increment(ref m_ProcessedPrims);
                    }
                }
            }

            private void ProcessGroupDeletions(MySqlConnection conn)
            {
                var removedItems = new List<UUID>();

                foreach (UUID k in m_GroupDeletions.Keys.ToArray())
                {
                    removedItems.Add(k);
                    if (removedItems.Count == 255)
                    {
                        string c = string.Format("DELETE FROM objects WHERE RegionID=\"{0}\" AND (ID=\"{1}\")", m_RegionID,
                            string.Join("\" OR ID=\"", removedItems));
                        using (var cmd = new MySqlCommand(c, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                        foreach (UUID r in removedItems)
                        {
                            m_GroupDeletions.Remove(r);
                        }
                        removedItems.Clear();
                    }
                }

                if (removedItems.Count != 0)
                {
                    string c = string.Format("DELETE FROM objects WHERE RegionID=\"{0}\" AND (ID=\"{1}\")", m_RegionID,
                        string.Join("\" OR ID=\"", removedItems));
                    using (var cmd = new MySqlCommand(c, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    foreach (UUID r in removedItems)
                    {
                        m_GroupDeletions.Remove(r);
                    }
                }
            }

            private void ProcessPrimItemUpdates(MySqlConnection conn)
            {
                var replaceInto1 = new StringBuilder();
                var replaceInto2 = new StringBuilder();
                bool replaceInto1Inited = false;
                var processedItems = new List<PrimKey>();

                foreach (PrimKey k in m_PrimItemUpdates.Keys.ToArray())
                {
                    ObjectInventoryUpdateInfo update = m_PrimItemUpdates[k];
                    if(update.IsRemoved)
                    {
                        /* do not save removed data */
                        m_PrimItemUpdates.Remove(k);
                        continue;
                    }
                    Dictionary<string, object> data = GenerateUpdateObjectPartInventoryItem(update.PartID, update.Item);
                    data["RegionID"] = m_RegionID;

                    if (!replaceInto1Inited)
                    {
                        replaceInto1Inited = true;
                        replaceInto1.Append("REPLACE INTO primitems (");
                        replaceInto1.Append(MySQLUtilities.GenerateFieldNames(data));
                    }

                    if(replaceInto2.Length == 0)
                    { 
                        replaceInto2.Append(") VALUES (");
                    }
                    else
                    {
                        replaceInto2.Append("),(");
                    }
                    processedItems.Add(k);
                    replaceInto2.Append(MySQLUtilities.GenerateValues(data));

                    if(processedItems.Count == 255)
                    {
                        var replaceInto = new StringBuilder();
                        replaceInto.Append(replaceInto1);
                        replaceInto.Append(replaceInto2);
                        replaceInto.Append(")");
                        using (var cmd = new MySqlCommand(replaceInto.ToString(), conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                        foreach (PrimKey r in processedItems)
                        {
                            m_PrimItemUpdates.Remove(r);
                            Interlocked.Increment(ref m_ProcessedPrims);
                        }
                        replaceInto2.Clear();
                        processedItems.Clear();
                    }
                }

                if (processedItems.Count > 0)
                {
                    var replaceInto = new StringBuilder();
                    replaceInto.Append(replaceInto1);
                    replaceInto.Append(replaceInto2);
                    replaceInto.Append(")");
                    using (var cmd = new MySqlCommand(replaceInto.ToString(), conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    foreach (PrimKey r in processedItems)
                    {
                        m_PrimItemUpdates.Remove(r);
                        Interlocked.Increment(ref m_ProcessedPrims);
                    }
                }
            }

            private void ProcessPrimUpdates(MySqlConnection conn)
            {
                var replaceInto1 = new StringBuilder();
                var replaceInto2 = new StringBuilder();
                bool replaceInto1Inited = false;
                var processedItems = new List<UUID>();

                foreach (UUID k in m_PrimUpdates.Keys.ToArray())
                {
                    Dictionary<string, object> data = m_PrimUpdates[k];
                    if (!replaceInto1Inited)
                    {
                        replaceInto1Inited = true;
                        replaceInto1.Append("REPLACE INTO prims (");
                        replaceInto1.Append(MySQLUtilities.GenerateFieldNames(data));
                    }

                    if (replaceInto2.Length == 0)
                    {
                        replaceInto2.Append(") VALUES (");
                    }
                    else
                    {
                        replaceInto2.Append("),(");
                    }
                    processedItems.Add(k);
                    replaceInto2.Append(MySQLUtilities.GenerateValues(data));

                    if (processedItems.Count == 255)
                    {
                        var replaceInto = new StringBuilder();
                        replaceInto.Append(replaceInto1);
                        replaceInto.Append(replaceInto2);
                        replaceInto.Append(")");
                        using (var cmd = new MySqlCommand(replaceInto.ToString(), conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                        foreach (UUID r in processedItems)
                        {
                            m_PrimUpdates.Remove(r);
                        }
                        replaceInto2.Clear();
                        processedItems.Clear();
                    }
                }

                if (processedItems.Count > 0)
                {
                    var replaceInto = new StringBuilder();
                    replaceInto.Append(replaceInto1);
                    replaceInto.Append(replaceInto2);
                    replaceInto.Append(")");
                    using (var cmd = new MySqlCommand(replaceInto.ToString(), conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    foreach (UUID r in processedItems)
                    {
                        m_PrimUpdates.Remove(r);
                    }
                }
            }


            private void ProcessGroupUpdates(MySqlConnection conn)
            {
                var replaceInto1 = new StringBuilder();
                var replaceInto2 = new StringBuilder();
                bool replaceInto1Inited = false;
                var processedItems = new List<UUID>();

                foreach (UUID k in m_GroupUpdates.Keys.ToArray())
                {
                    Dictionary<string, object> data = m_GroupUpdates[k];
                    if (!replaceInto1Inited)
                    {
                        replaceInto1Inited = true;
                        replaceInto1.Append("REPLACE INTO objects (");
                        replaceInto1.Append(MySQLUtilities.GenerateFieldNames(data));
                    }

                    if (replaceInto2.Length == 0)
                    {
                        replaceInto2.Append(") VALUES (");
                    }
                    else
                    {
                        replaceInto2.Append("),(");
                    }
                    processedItems.Add(k);
                    replaceInto2.Append(MySQLUtilities.GenerateValues(data));

                    if (processedItems.Count == 255)
                    {
                        var replaceInto = new StringBuilder();
                        replaceInto.Append(replaceInto1);
                        replaceInto.Append(replaceInto2);
                        replaceInto.Append(")");
                        using (var cmd = new MySqlCommand(replaceInto.ToString(), conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                        foreach (UUID r in processedItems)
                        {
                            m_GroupUpdates.Remove(r);
                        }
                        replaceInto2.Clear();
                        processedItems.Clear();
                    }
                }

                if (processedItems.Count > 0)
                {
                    var replaceInto = new StringBuilder();
                    replaceInto.Append(replaceInto1);
                    replaceInto.Append(replaceInto2);
                    replaceInto.Append(")");
                    using (var cmd = new MySqlCommand(replaceInto.ToString(), conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    foreach (UUID r in processedItems)
                    {
                        m_GroupUpdates.Remove(r);
                    }
                }
            }

            protected override void OnIdle()
            {
                using (var conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();

                    ProcessPrimItemUpdates(conn);
                    ProcessPrimItemDeletions(conn);
                    ProcessPrimDeletions(conn);
                    ProcessGroupDeletions(conn);
                    ProcessPrimUpdates(conn);
                    ProcessGroupUpdates(conn);
                }
            }

            protected override void OnStart()
            {
                m_SceneListenerThreads.Add(this);
            }

            protected override void OnStop()
            {
                m_SceneListenerThreads.Remove(this);
            }

            private Dictionary<string, object> GenerateUpdateObjectPartInventoryItem(UUID primID, ObjectPartInventoryItem item)
            {
                ObjectPartInventoryItem.PermsGranterInfo grantinfo = item.PermsGranter;
                return new Dictionary<string, object>
                {
                    ["AssetId"] = item.AssetID,
                    ["AssetType"] = item.AssetType,
                    ["CreationDate"] = item.CreationDate,
                    ["Creator"] = item.Creator,
                    ["Description"] = item.Description,
                    ["Flags"] = item.Flags,
                    ["Group"] = item.Group,
                    ["GroupOwned"] = item.IsGroupOwned,
                    ["PrimID"] = primID,
                    ["Name"] = item.Name,
                    ["InventoryID"] = item.ID,
                    ["InventoryType"] = item.InventoryType,
                    ["LastOwner"] = item.LastOwner,
                    ["Owner"] = item.Owner,
                    ["BasePermissions"] = item.Permissions.Base,
                    ["CurrentPermissions"] = item.Permissions.Current,
                    ["EveryOnePermissions"] = item.Permissions.EveryOne,
                    ["GroupPermissions"] = item.Permissions.Group,
                    ["NextOwnerPermissions"] = item.Permissions.NextOwner,
                    ["SaleType"] = item.SaleInfo.Type,
                    ["SalePrice"] = item.SaleInfo.Price,
                    ["SalePermMask"] = item.SaleInfo.PermMask,
                    ["PermsGranter"] = grantinfo.PermsGranter.ToString(),
                    ["PermsMask"] = grantinfo.PermsMask,
                    ["NextOwnerAssetID"] = item.NextOwnerAssetID,
                    ["ExperienceID"] = item.ExperienceID,
                    ["CollisionFilterData"] = item.CollisionFilter.DbSerialization
                };
            }

            private Dictionary<string, object> GenerateUpdateObjectGroup(ObjectGroup objgroup) => new Dictionary<string, object>
            {
                ["ID"] = objgroup.ID,
                ["RegionID"] = objgroup.Scene.ID,
                ["IsTemporary"] = objgroup.IsTemporary,
                ["Owner"] = objgroup.Owner,
                ["LastOwner"] = objgroup.LastOwner,
                ["Group"] = objgroup.Group,
                ["OriginalAssetID"] = objgroup.OriginalAssetID,
                ["NextOwnerAssetID"] = objgroup.NextOwnerAssetID,
                ["SaleType"] = objgroup.SaleType,
                ["SalePrice"] = objgroup.SalePrice,
                ["PayPrice0"] = objgroup.PayPrice0,
                ["PayPrice1"] = objgroup.PayPrice1,
                ["PayPrice2"] = objgroup.PayPrice2,
                ["PayPrice3"] = objgroup.PayPrice3,
                ["PayPrice4"] = objgroup.PayPrice4,
                ["AttachedPos"] = objgroup.AttachedPos,
                ["AttachedRot"] = objgroup.AttachedRot,
                ["AttachPoint"] = objgroup.AttachPoint,
                ["IsIncludedInSearch"] = objgroup.IsIncludedInSearch,
                ["RezzingObjectID"] = objgroup.RezzingObjectID
            };

            private Dictionary<string, object> GenerateUpdateObjectPart(ObjectPart objpart)
            {
                var data = new Dictionary<string, object>
                {
                    ["ID"] = objpart.ID,
                    ["LinkNumber"] = objpart.LinkNumber,
                    ["RootPartID"] = objpart.ObjectGroup.RootPart.ID,
                    ["Position"] = objpart.Position,
                    ["Rotation"] = objpart.Rotation,
                    ["SitText"] = objpart.SitText,
                    ["TouchText"] = objpart.TouchText,
                    ["Name"] = objpart.Name,
                    ["Description"] = objpart.Description,
                    ["SitTargetOffset"] = objpart.SitTargetOffset,
                    ["SitTargetOrientation"] = objpart.SitTargetOrientation,
                    ["SitAnimation"] = objpart.SitAnimation,
                    ["PhysicsShapeType"] = objpart.PhysicsShapeType,
                    ["PathfindingType"] = objpart.PathfindingType,
                    ["PathfindingCharacterType"] = objpart.PathfindingCharacterType,
                    ["WalkableCoefficientAvatar"] = objpart.WalkableCoefficientAvatar,
                    ["WalkableCoefficientA"] = objpart.WalkableCoefficientA,
                    ["WalkableCoefficientB"] = objpart.WalkableCoefficientB,
                    ["WalkableCoefficientC"] = objpart.WalkableCoefficientC,
                    ["WalkableCoefficientD"] = objpart.WalkableCoefficientD,
                    ["Material"] = objpart.Material,
                    ["Size"] = objpart.Size,
                    ["MediaURL"] = objpart.MediaURL,
                    ["Creator"] = objpart.Creator,
                    ["CreationDate"] = objpart.CreationDate,
                    ["RezDate"] = objpart.RezDate,
                    ["Flags"] = objpart.Flags,
                    ["AngularVelocity"] = objpart.AngularVelocity,
                    ["LightData"] = objpart.PointLight.DbSerialization,
                    ["HoverTextData"] = objpart.Text.Serialization,
                    ["FlexibleData"] = objpart.Flexible.DbSerialization,
                    ["ProjectionData"] = objpart.Projection.DbSerialization,
                    ["LoopedSoundData"] = objpart.Sound.Serialization,
                    ["ImpactSoundData"] = objpart.CollisionSound.Serialization,
                    ["PrimitiveShapeData"] = objpart.Shape.Serialization,
                    ["ParticleSystem"] = objpart.ParticleSystemBytes,
                    ["TextureEntryBytes"] = objpart.TextureEntryBytes,
                    ["TextureAnimationBytes"] = objpart.TextureAnimationBytes,
                    ["ScriptAccessPin"] = objpart.ScriptAccessPin,
                    ["CameraAtOffset"] = objpart.CameraAtOffset,
                    ["CameraEyeOffset"] = objpart.CameraEyeOffset,
                    ["ForceMouselook"] = objpart.ForceMouselook,
                    ["BasePermissions"] = objpart.BaseMask,
                    ["CurrentPermissions"] = objpart.OwnerMask,
                    ["EveryOnePermissions"] = objpart.EveryoneMask,
                    ["GroupPermissions"] = objpart.GroupMask,
                    ["NextOwnerPermissions"] = objpart.NextOwnerMask,
                    ["ClickAction"] = objpart.ClickAction,
                    ["PassCollisionMode"] = objpart.PassCollisionMode,
                    ["PassTouchMode"] = objpart.PassTouchMode,
                    ["Velocity"] = objpart.Velocity,
                    ["IsSoundQueueing"] = objpart.IsSoundQueueing,
                    ["IsAllowedDrop"] = objpart.IsAllowedDrop,
                    ["PhysicsDensity"] = objpart.PhysicsDensity,
                    ["PhysicsFriction"] = objpart.PhysicsFriction,
                    ["PhysicsRestitution"] = objpart.PhysicsRestitution,
                    ["PhysicsGravityMultiplier"] = objpart.PhysicsGravityMultiplier,

                    ["IsRotateXEnabled"] = objpart.IsRotateXEnabled,
                    ["IsRotateYEnabled"] = objpart.IsRotateYEnabled,
                    ["IsRotateZEnabled"] = objpart.IsRotateZEnabled,
                    ["IsVolumeDetect"] = objpart.IsVolumeDetect,
                    ["IsPhantom"] = objpart.IsPhantom,
                    ["IsPhysics"] = objpart.IsPhysics,
                    ["IsBlockGrab"] = objpart.IsBlockGrab,
                    ["IsSandbox"] = objpart.IsSandbox,
                    ["IsDieAtEdge"] = objpart.IsDieAtEdge,
                    ["IsReturnAtEdge"] = objpart.IsReturnAtEdge,
                    ["IsBlockGrabObject"] = objpart.IsBlockGrabObject,
                    ["SandboxOrigin"] = objpart.SandboxOrigin,
                    ["ExtendedMeshData"] = objpart.ExtendedMesh.DbSerialization,
                    ["IsSitTargetActive"] = objpart.IsSitTargetActive,
                    ["IsScriptedSitOnly"] = objpart.IsScriptedSitOnly,
                    ["AllowUnsit"] = objpart.AllowUnsit,
                    ["IsUnSitTargetActive"] = objpart.IsUnSitTargetActive,
                    ["UnSitTargetOffset"] = objpart.UnSitTargetOffset,
                    ["UnSitTargetOrientation"] = objpart.UnSitTargetOrientation,
                    ["LocalizationData"] = objpart.LocalizationSerialization,
                    ["VehicleData"] = objpart.VehicleParams.ToSerialization(),
                    ["Damage"] = objpart.Damage,
                    ["AnimationData"] = objpart.AnimationController.DbSerialization
                };
                using (var ms = new MemoryStream())
                {
                    LlsdBinary.Serialize(objpart.DynAttrs, ms);
                    data.Add("DynAttrs", ms.ToArray());
                }

                return data;
            }
        }

        public override SceneListener GetSceneListener(UUID regionID) =>
            new MySQLSceneListener(m_ConnectionString, regionID, m_SceneListenerThreads);
    }
}

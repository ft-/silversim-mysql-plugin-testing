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
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Inventory;
using System;
using System.Collections.Generic;

namespace SilverSim.Database.MySQL.Inventory
{
    public partial class MySQLInventoryService : IInventoryItemServiceInterface
    {
        bool IInventoryItemServiceInterface.ContainsKey(UUID key)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT ID FROM " + m_InventoryItemTable + " WHERE ID LIKE ?itemid", connection))
                {
                    cmd.Parameters.AddParameter("?itemid", key);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        if (dbReader.Read())
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        bool IInventoryItemServiceInterface.TryGetValue(UUID key, out InventoryItem item)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM " + m_InventoryItemTable + " WHERE ID LIKE ?itemid", connection))
                {
                    cmd.Parameters.AddParameter("?itemid", key);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        if (dbReader.Read())
                        {
                            item = dbReader.ToItem();
                            return true;
                        }
                    }
                }
            }

            item = default(InventoryItem);
            return false;
        }

        InventoryItem IInventoryItemServiceInterface.this[UUID key]
        {
            get
            {
                InventoryItem item;
                if(!Item.TryGetValue(key, out item))
                {
                    throw new KeyNotFoundException();
                }
                return item;
            }
        }

        List<InventoryItem> IInventoryItemServiceInterface.this[UUID principalID, List<UUID> itemids]
        {
            get
            {
                if(itemids == null || itemids.Count == 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(itemids));
                }
                var items = new List<InventoryItem>();
                using (var connection = new MySqlConnection(m_ConnectionString))
                {
                    connection.Open();
                    var matchStrings = new List<string>();
                    foreach(UUID itemid in itemids)
                    {
                        matchStrings.Add(string.Format("\"{0}\"", itemid.ToString()));
                    }
                    string qStr = string.Join(",", matchStrings);
                    using (var cmd = new MySqlCommand("SELECT * FROM " + m_InventoryItemTable + " WHERE OwnerID LIKE ?ownerid AND ID IN (" + qStr + ")", connection))
                    {
                        cmd.Parameters.AddParameter("?ownerid", principalID);
                        using (MySqlDataReader dbReader = cmd.ExecuteReader())
                        {
                            while (dbReader.Read())
                            {
                                items.Add(dbReader.ToItem());
                            }
                        }
                    }
                }

                return items;
            }
        }

        bool IInventoryItemServiceInterface.ContainsKey(UUID principalID, UUID key)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT ID FROM " + m_InventoryItemTable + " WHERE OwnerID LIKE ?ownerid AND ID LIKE ?itemid", connection))
                {
                    cmd.Parameters.AddParameter("?ownerid", principalID);
                    cmd.Parameters.AddParameter("?itemid", key);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        if (dbReader.Read())
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        bool IInventoryItemServiceInterface.TryGetValue(UUID principalID, UUID key, out InventoryItem item)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM " + m_InventoryItemTable + " WHERE OwnerID LIKE ?ownerid AND ID LIKE ?itemid", connection))
                {
                    cmd.Parameters.AddParameter("?ownerid", principalID);
                    cmd.Parameters.AddParameter("?itemid", key);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        if (dbReader.Read())
                        {
                            item = dbReader.ToItem();
                            return true;
                        }
                    }
                }
            }

            item = default(InventoryItem);
            return false;
        }

        InventoryItem IInventoryItemServiceInterface.this[UUID principalID, UUID key]
        {
            get
            {
                InventoryItem item;
                if(!Item.TryGetValue(principalID, key, out item))
                {
                    throw new KeyNotFoundException();
                }
                return item;
            }
        }

        void IInventoryItemServiceInterface.Add(InventoryItem item)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                connection.InsertInto(m_InventoryItemTable, item.ToDictionary());
            }
            IncrementVersion(item.Owner.ID, item.ParentFolderID);
        }

        void IInventoryItemServiceInterface.Update(InventoryItem item)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                var newVals = new Dictionary<string, object>
                {
                    ["AssetID"] = item.AssetID.ToString(),
                    ["Name"] = item.Name,
                    ["Description"] = item.Description,
                    ["BasePermissionsMask"] = item.Permissions.Base,
                    ["CurrentPermissionsMask"] = item.Permissions.Current,
                    ["EveryOnePermissionsMask"] = item.Permissions.EveryOne,
                    ["NextOwnerPermissionsMask"] = item.Permissions.NextOwner,
                    ["GroupPermissionsMask"] = item.Permissions.Group,
                    ["SalePrice"] = item.SaleInfo.Price,
                    ["SaleType"] = item.SaleInfo.Type
                };
                connection.UpdateSet(m_InventoryItemTable, newVals, string.Format("OwnerID LIKE '{0}' AND ID LIKE '{1}'", item.Owner.ID, item.ID));
            }
            IncrementVersion(item.Owner.ID, item.ParentFolderID);
        }

        void IInventoryItemServiceInterface.Delete(UUID principalID, UUID id)
        {
            InventoryItem item = Item[principalID, id];
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("DELETE FROM " + m_InventoryItemTable + " WHERE OwnerID LIKE ?ownerid AND ID LIKE ?itemid", connection))
                {
                    cmd.Parameters.AddParameter("?ownerid", principalID);
                    cmd.Parameters.AddParameter("?itemid", id);
                    if (1 > cmd.ExecuteNonQuery())
                    {
                        throw new InventoryItemNotFoundException(id);
                    }
                }
            }
            IncrementVersion(principalID, item.ParentFolderID);
        }

        List<UUID> IInventoryItemServiceInterface.Delete(UUID principalID, List<UUID> itemids)
        {
            var deleted = new List<UUID>();
            foreach(UUID id in itemids)
            {
                try
                {
                    Item.Delete(principalID, id);
                    deleted.Add(id);
                }
                catch
                {
                    /* nothing else to do */
                }
            }
            return deleted;
        }

        void IInventoryItemServiceInterface.Move(UUID principalID, UUID id, UUID toFolderID)
        {
            InventoryItem item = Item[principalID, id];
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand(string.Format("BEGIN; IF EXISTS (SELECT NULL FROM " + m_InventoryFolderTable + " WHERE ID LIKE '{0}' AND OwnerID LIKE '{2}')" +
                    "UPDATE " + m_InventoryItemTable + " SET ParentFolderID = '{0}' WHERE ID = '{1}'; COMMIT", toFolderID, id, principalID),
                    connection))
                {
                    if (cmd.ExecuteNonQuery() < 1)
                    {
                        throw new InventoryFolderNotStoredException(id);
                    }
                }
            }
            IncrementVersion(principalID, item.ParentFolderID);
            IncrementVersion(principalID, toFolderID);
        }

        private void IncrementVersion(UUID principalID, UUID folderID)
        {
            try
            {
                using (var connection = new MySqlConnection(m_ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new MySqlCommand("UPDATE " + m_InventoryFolderTable + " SET Version = Version + 1 WHERE OwnerID LIKE ?ownerid AND ID LIKE ?folderid", connection))
                    {
                        cmd.Parameters.AddParameter("?ownerid", principalID);
                        cmd.Parameters.AddParameter("?folderid", folderID);
                        if (cmd.ExecuteNonQuery() < 1)
                        {
                            throw new InventoryFolderNotStoredException(folderID);
                        }
                    }
                }
            }
            catch
            {
                /* nothing to do here */
            }
        }
    }
}
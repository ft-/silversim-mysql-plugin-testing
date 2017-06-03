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
using SilverSim.Types.Asset;
using SilverSim.Types.Inventory;
using System;
using System.Collections.Generic;

namespace SilverSim.Database.MySQL.Inventory
{
    public partial class MySQLInventoryService : IInventoryFolderServiceInterface
    {
        bool IInventoryFolderServiceInterface.TryGetValue(UUID key, out InventoryFolder folder)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM " + m_InventoryFolderTable + " WHERE ID LIKE ?folderid", connection))
                {
                    cmd.Parameters.AddParameter("?folderid", key);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        if (dbReader.Read())
                        {
                            folder = dbReader.ToFolder();
                            return true;
                        }
                    }
                }
            }

            folder = default(InventoryFolder);
            return false;
        }

        bool IInventoryFolderServiceInterface.ContainsKey(UUID key)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT ID FROM " + m_InventoryFolderTable + " WHERE ID LIKE ?folderid", connection))
                {
                    cmd.Parameters.AddParameter("?folderid", key);
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

        InventoryFolder IInventoryFolderServiceInterface.this[UUID key]
        {
            get
            {
                InventoryFolder folder;
                if(!Folder.TryGetValue(key, out folder))
                {
                    throw new InventoryFolderNotFoundException(key);
                }
                return folder;
            }
        }

        bool IInventoryFolderServiceInterface.TryGetValue(UUID principalID, UUID key, out InventoryFolder folder)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM " + m_InventoryFolderTable + " WHERE OwnerID LIKE ?ownerid AND ID LIKE ?folderid", connection))
                {
                    cmd.Parameters.AddParameter("?ownerid", principalID);
                    cmd.Parameters.AddParameter("?folderid", key);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        if (dbReader.Read())
                        {
                            folder = dbReader.ToFolder();
                            return true;
                        }
                    }
                }
            }

            folder = default(InventoryFolder);
            return false;
        }

        bool IInventoryFolderServiceInterface.ContainsKey(UUID principalID, UUID key)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT ID FROM " + m_InventoryFolderTable + " WHERE OwnerID LIKE ?ownerid AND ID LIKE ?folderid", connection))
                {
                    cmd.Parameters.AddParameter("?ownerid", principalID);
                    cmd.Parameters.AddParameter("?folderid", key);
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

        InventoryFolder IInventoryFolderServiceInterface.this[UUID principalID, UUID key]
        {
            get
            {
                InventoryFolder folder;
                if(!Folder.TryGetValue(principalID, key, out folder))
                {
                    throw new InventoryFolderNotFoundException(key);
                }
                return folder;
            }
        }

        bool IInventoryFolderServiceInterface.ContainsKey(UUID principalID, AssetType type)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                if (type == AssetType.RootFolder)
                {
                    using (var cmd = new MySqlCommand("SELECT ID FROM " + m_InventoryFolderTable + " WHERE OwnerID LIKE ?ownerid AND ParentFolderID = ?parentfolderid", connection))
                    {
                        cmd.Parameters.AddParameter("?ownerid", principalID);
                        cmd.Parameters.AddParameter("?parentfolderid", UUID.Zero);
                        using (MySqlDataReader dbReader = cmd.ExecuteReader())
                        {
                            if (dbReader.Read())
                            {
                                return true;
                            }
                        }
                    }
                }
                else
                {
                    using (var cmd = new MySqlCommand("SELECT ID FROM " + m_InventoryFolderTable + " WHERE OwnerID LIKE ?ownerid AND InventoryType = ?type", connection))
                    {
                        cmd.Parameters.AddParameter("?ownerid", principalID);
                        cmd.Parameters.AddParameter("?type", type);
                        using (MySqlDataReader dbReader = cmd.ExecuteReader())
                        {
                            if (dbReader.Read())
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        bool IInventoryFolderServiceInterface.TryGetValue(UUID principalID, AssetType type, out InventoryFolder folder)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                if (type == AssetType.RootFolder)
                {
                    using (var cmd = new MySqlCommand("SELECT * FROM " + m_InventoryFolderTable + " WHERE OwnerID LIKE ?ownerid AND ParentFolderID = ?parentfolderid", connection))
                    {
                        cmd.Parameters.AddParameter("?ownerid", principalID);
                        cmd.Parameters.AddParameter("?parentfolderid", UUID.Zero);
                        using (MySqlDataReader dbReader = cmd.ExecuteReader())
                        {
                            if (dbReader.Read())
                            {
                                folder = dbReader.ToFolder();
                                return true;
                            }
                        }
                    }
                }
                else
                {
                    using (var cmd = new MySqlCommand("SELECT * FROM " + m_InventoryFolderTable + " WHERE OwnerID LIKE ?ownerid AND InventoryType = ?type", connection))
                    {
                        cmd.Parameters.AddParameter("?ownerid", principalID);
                        cmd.Parameters.AddParameter("?type", type);
                        using (MySqlDataReader dbReader = cmd.ExecuteReader())
                        {
                            if (dbReader.Read())
                            {
                                folder = dbReader.ToFolder();
                                return true;
                            }
                        }
                    }
                }
            }

            folder = default(InventoryFolder);
            return false;
        }

        InventoryFolder IInventoryFolderServiceInterface.this[UUID principalID, AssetType type]
        {
            get
            {
                using (var connection = new MySqlConnection(m_ConnectionString))
                {
                    connection.Open();
                    if (type == AssetType.RootFolder)
                    {
                        using (var cmd = new MySqlCommand("SELECT * FROM " + m_InventoryFolderTable + " WHERE OwnerID LIKE ?ownerid AND ParentFolderID = ?parentfolderid", connection))
                        {
                            cmd.Parameters.AddParameter("?ownerid", principalID);
                            cmd.Parameters.AddParameter("?parentfolderid", UUID.Zero);
                            using (MySqlDataReader dbReader = cmd.ExecuteReader())
                            {
                                if (dbReader.Read())
                                {
                                    return dbReader.ToFolder();
                                }
                            }
                        }
                    }
                    else
                    {
                        using (var cmd = new MySqlCommand("SELECT * FROM " + m_InventoryFolderTable + " WHERE OwnerID LIKE ?ownerid AND InventoryType = ?type", connection))
                        {
                            cmd.Parameters.AddParameter("?ownerid", principalID);
                            cmd.Parameters.AddParameter("?type", type);
                            using (MySqlDataReader dbReader = cmd.ExecuteReader())
                            {
                                if (dbReader.Read())
                                {
                                    return dbReader.ToFolder();
                                }
                            }
                        }
                    }
                }

                throw new InventoryFolderTypeNotFoundException(type);
            }
        }

        List<InventoryFolder> IInventoryFolderServiceInterface.GetFolders(UUID principalID, UUID key)
        {
            var folders = new List<InventoryFolder>();
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM " + m_InventoryFolderTable + " WHERE OwnerID LIKE ?ownerid AND ParentFolderID LIKE ?folderid", connection))
                {
                    cmd.Parameters.AddParameter("?ownerid", principalID);
                    cmd.Parameters.AddParameter("?folderid", key);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while(dbReader.Read())
                        {
                            folders.Add(dbReader.ToFolder());
                        }
                    }
                }
            }

            return folders;
        }

        List<InventoryItem> IInventoryFolderServiceInterface.GetItems(UUID principalID, UUID key)
        {
            var items = new List<InventoryItem>();
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM " + m_InventoryItemTable + " WHERE OwnerID LIKE ?ownerid AND ParentFolderID LIKE ?folderid", connection))
                {
                    cmd.Parameters.AddParameter("?ownerid", principalID);
                    cmd.Parameters.AddParameter("?folderid", key);
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

        void IInventoryFolderServiceInterface.Add(InventoryFolder folder)
        {
            var newVals = new Dictionary<string, object>
            {
                ["ID"] = folder.ID,
                ["ParentFolderID"] = folder.ParentFolderID,
                ["OwnerID"] = folder.Owner.ID,
                ["Name"] = folder.Name,
                ["InventoryType"] = folder.InventoryType,
                ["Version"] = folder.Version
            };
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                try
                {
                    connection.InsertInto(m_InventoryFolderTable, newVals);
                }
                catch
                {
                    throw new InventoryFolderNotStoredException(folder.ID);
                }
            }

            if (folder.ParentFolderID != UUID.Zero)
            {
                IncrementVersionNoExcept(folder.Owner.ID, folder.ParentFolderID);
            }
        }

        void IInventoryFolderServiceInterface.Update(InventoryFolder folder)
        {
            var newVals = new Dictionary<string, object>
            {
                ["Version"] = folder.Version,
                ["Name"] = folder.Name
            };
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                try
                {
                    connection.UpdateSet(m_InventoryFolderTable, newVals, string.Format("OwnerID LIKE '{0}' AND ID LIKE '{1}'", folder.Owner.ID, folder.ID));
                }
                catch
                {
                    throw new InventoryFolderNotStoredException(folder.ID);
                }
            }
            IncrementVersionNoExcept(folder.Owner.ID, folder.ParentFolderID);
        }

        void IInventoryFolderServiceInterface.Move(UUID principalID, UUID folderID, UUID toFolderID)
        {
            InventoryFolder thisfolder = Folder[principalID, folderID];
            if(folderID == toFolderID)
            {
                throw new ArgumentException("folderID != toFolderID");
            }
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand(string.Format("BEGIN; IF EXISTS (SELECT NULL FROM " + m_InventoryFolderTable + " WHERE ID LIKE '{0}')" +
                    "UPDATE " + m_InventoryFolderTable + " SET ParentFolderID = '{0}' WHERE ID = '{1}'; COMMIT", toFolderID, folderID),
                    connection))
                {
                    if (cmd.ExecuteNonQuery() < 1)
                    {
                        throw new InventoryFolderNotStoredException(folderID);
                    }
                }
            }
            IncrementVersionNoExcept(principalID, toFolderID);
            IncrementVersionNoExcept(principalID, thisfolder.ParentFolderID);
        }

        #region Delete and Purge
        void IInventoryFolderServiceInterface.Delete(UUID principalID, UUID folderID)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                PurgeOrDelete(principalID, folderID, connection, true);
            }
        }

        void IInventoryFolderServiceInterface.Purge(UUID principalID, UUID folderID)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                PurgeOrDelete(principalID, folderID, connection, false);
            }
        }

        void IInventoryFolderServiceInterface.Purge(UUID folderID)
        {
            InventoryFolder folder = Folder[folderID];
            Folder.Purge(folder.Owner.ID, folderID);
        }

        private void PurgeOrDelete(UUID principalID, UUID folderID, MySqlConnection connection, bool deleteFolder)
        {
            List<UUID> folders;
            InventoryFolder thisfolder = Folder[principalID, folderID];

            connection.InsideTransaction(() =>
            {
                if (deleteFolder)
                {
                    folders = new List<UUID>
                    {
                        folderID
                    };
                }
                else
                {
                    folders = GetFolderIDs(principalID, folderID, connection);
                }

                for (int index = 0; index < folders.Count; ++index)
                {
                    foreach (UUID folder in GetFolderIDs(principalID, folders[index], connection))
                    {
                        if (!folders.Contains(folder))
                        {
                            folders.Insert(0, folder);
                        }
                    }
                }

                foreach (UUID folder in folders)
                {
                    using (var cmd = new MySqlCommand("DELETE FROM " + m_InventoryItemTable + " WHERE OwnerID LIKE ?ownerid AND ID LIKE ?folderid", connection))
                    {
                        cmd.Parameters.AddParameter("?ownerid", principalID);
                        cmd.Parameters.AddParameter("?folderid", folderID);
                        try
                        {
                            cmd.ExecuteNonQuery();
                        }
                        catch
                        {
                            /* nothing to do here */
                        }
                    }
                    using (var cmd = new MySqlCommand("DELETE FROM " + m_InventoryFolderTable + " WHERE OwnerID LIKE ?ownerid AND ParentFolderID LIKE ?folderid", connection))
                    {
                        cmd.Parameters.AddParameter("?ownerid", principalID);
                        cmd.Parameters.AddParameter("?folderid", folderID);
                        try
                        {
                            cmd.ExecuteNonQuery();
                        }
                        catch
                        {
                            /* nothing to do here */
                        }
                    }
                }

                using (var cmd = new MySqlCommand("UPDATE " + m_InventoryFolderTable + " SET Version = Version + 1 WHERE OwnerID LIKE ?ownerid AND ID LIKE ?folderid", connection))
                {
                    cmd.Parameters.AddParameter("?ownerid", principalID);
                    if (deleteFolder)
                    {
                        cmd.Parameters.AddParameter("?folderid", thisfolder.ParentFolderID);
                    }
                    else
                    {
                        cmd.Parameters.AddParameter("?folderid", folderID);
                    }
                }
            });
        }

        private List<UUID> GetFolderIDs(UUID principalID, UUID key, MySqlConnection connection)
        {
            var folders = new List<UUID>();
            using (var cmd = new MySqlCommand("SELECT ID FROM " + m_InventoryFolderTable + " WHERE OwnerID LIKE ?ownerid AND ParentFolderID LIKE ?folderid", connection))
            {
                cmd.Parameters.AddParameter("?ownerid", principalID);
                cmd.Parameters.AddParameter("?folderid", key);
                using (MySqlDataReader dbReader = cmd.ExecuteReader())
                {
                    while (dbReader.Read())
                    {
                        folders.Add(dbReader.GetUUID("ID"));
                    }
                }
            }

            return folders;
        }

        #endregion

        void IInventoryFolderServiceInterface.IncrementVersion(UUID principalID, UUID folderID)
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

        private void IncrementVersionNoExcept(UUID principalID, UUID folderID)
        {
            try
            {
                IncrementVersion(principalID, folderID);
            }
            catch
            {
                /* nothing to do here */
            }
        }

        List<UUID> IInventoryFolderServiceInterface.Delete(UUID principalID, List<UUID> folderIDs)
        {
            var deleted = new List<UUID>();
            foreach(UUID id in folderIDs)
            {
                try
                {
                    Folder.Delete(principalID, id);
                    deleted.Add(id);
                }
                catch
                {
                    /* nothing to do here */
                }
            }

            return deleted;
        }
    }
}
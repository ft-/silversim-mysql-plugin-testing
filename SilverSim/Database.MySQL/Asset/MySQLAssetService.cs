// SilverSim is distributed under the terms of the
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

using log4net;
using MySql.Data.MySqlClient;
using Nini.Config;
using SilverSim.Database.MySQL._Migration;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.ServiceInterfaces.Database;
using SilverSim.Threading;
using SilverSim.Types;
using SilverSim.Types.Asset;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;

namespace SilverSim.Database.MySQL.Asset
{
    [Description("MySQL Asset Backend")]
    [PluginName("Assets")]
    public sealed partial class MySQLAssetService : AssetServiceInterface, IDBServiceInterface, IPlugin, IAssetMetadataServiceInterface, IAssetDataServiceInterface, IAssetMigrationSourceInterface
    {
        private static readonly ILog m_Log = LogManager.GetLogger("MYSQL ASSET SERVICE");

        private readonly string m_ConnectionString;
        private readonly MySQLAssetReferencesService m_ReferencesService;
        private readonly RwLockedList<string> m_ConfigurationIssues;

        #region Constructor
        public MySQLAssetService(ConfigurationLoader loader, IConfig ownSection)
        {
            m_ConnectionString = MySQLUtilities.BuildConnectionString(ownSection, m_Log);
            m_ConfigurationIssues = loader.KnownConfigurationIssues;
            m_ReferencesService = new MySQLAssetReferencesService(this);
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* intentionally left empty */
        }
        #endregion

        public override bool IsSameServer(AssetServiceInterface other) =>
            other.GetType() == typeof(MySQLAssetService) &&
                (m_ConnectionString == ((MySQLAssetService)other).m_ConnectionString);

        #region Exists methods
        public override bool Exists(UUID key)
        {
            using(var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                bool needsUpdateAccessTime = false;
                using (var cmd = new MySqlCommand("SELECT id, access_time FROM assetrefs WHERE id = @id LIMIT 1", conn))
                {
                    cmd.Parameters.AddParameter("@id", key);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        if(!dbReader.Read())
                        {
                            return false;
                        }
                        needsUpdateAccessTime = DateTime.UtcNow - dbReader.GetDate("access_time") > TimeSpan.FromHours(1);
                    }
                }
                if (needsUpdateAccessTime)
                {
                    /* update access_time */
                    using (var cmd = new MySqlCommand("UPDATE assetrefs SET access_time = @access WHERE id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@access", Date.GetUnixTime());
                        cmd.Parameters.AddWithValue("@id", key);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
        }

        public override Dictionary<UUID, bool> Exists(List<UUID> assets)
        {
            var res = new Dictionary<UUID,bool>();
            if (assets.Count == 0)
            {
                return res;
            }

            foreach(UUID id in assets)
            {
                res[id] = false;
            }

            string ids = "'" + string.Join("','", assets) + "'";
            string sql = $"SELECT id, access_time FROM assetrefs WHERE id IN ({ids})";
            var updaterequired = new List<UUID>();

            using (var dbcon = new MySqlConnection(m_ConnectionString))
            {
                dbcon.Open();
                using (var cmd = new MySqlCommand(sql, dbcon))
                {
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            UUID id = dbReader.GetUUID("id");
                            res[id] = true;
                            if (DateTime.UtcNow - dbReader.GetDate("access_time") > TimeSpan.FromHours(1))
                            {
                                updaterequired.Add(id);
                            }
                        }
                    }
                }

                /* update access_time */
                if (updaterequired.Count != 0)
                {
                    ids = "'" + string.Join("','", updaterequired) + "'";
                    sql = $"UPDATE assetrefs SET access_time = @access WHERE id IN ({ids})";
                    using (var cmd = new MySqlCommand(sql, dbcon))
                    {
                        cmd.Parameters.AddWithValue("@access", Date.GetUnixTime());
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            return res;
        }
        #endregion

        #region Accessors
        public override AssetData this[UUID key]
        {
            get
            {
                AssetData asset;
                if(!TryGetValue(key, out asset))
                {
                    throw new AssetNotFoundException(key);
                }
                return asset;
            }
        }

        public override bool TryGetValue(UUID key, out AssetData asset)
        {
            asset = null;
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM assetrefs INNER JOIN assetdata ON assetrefs.hash = assetdata.hash AND assetrefs.assetType = assetdata.assetType WHERE id = @id LIMIT 1", conn))
                {
                    cmd.Parameters.AddParameter("@id", key);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        if (!dbReader.Read())
                        {
                            return false;
                        }
                        asset = new AssetData
                        {
                            ID = dbReader.GetUUID("id"),
                            Data = dbReader.GetBytes("data"),
                            Type = dbReader.GetEnum<AssetType>("assetType"),
                            Name = dbReader.GetString("name"),
                            CreateTime = dbReader.GetDate("create_time"),
                            AccessTime = dbReader.GetDate("access_time"),
                            Flags = dbReader.GetEnum<AssetFlags>("asset_flags"),
                            Temporary = dbReader.GetBool("temporary")
                        };
                    }
                }
                if (DateTime.UtcNow - asset.AccessTime > TimeSpan.FromHours(1))
                {
                    /* update access_time */
                    using (var cmd = new MySqlCommand("UPDATE assetrefs SET access_time = @access WHERE id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@access", Date.GetUnixTime());
                        cmd.Parameters.AddWithValue("@id", key);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
        }

        #endregion

        #region Metadata interface
        public override IAssetMetadataServiceInterface Metadata => this;

        AssetMetadata IAssetMetadataServiceInterface.this[UUID key]
        {
            get
            {
                AssetMetadata s;
                if (!Metadata.TryGetValue(key, out s))
                {
                    throw new AssetNotFoundException(key);
                }
                return s;
            }
        }

        bool IAssetMetadataServiceInterface.TryGetValue(UUID key, out AssetMetadata metadata)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM assetrefs WHERE id=@id LIMIT 1", conn))
                {
                    cmd.Parameters.AddParameter("@id", key);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        if (dbReader.Read())
                        {
                            metadata = new AssetMetadata
                            {
                                ID = dbReader.GetUUID("id"),
                                Type = dbReader.GetEnum<AssetType>("assetType"),
                                Name = dbReader.GetString("name"),
                                CreateTime = dbReader.GetDate("create_time"),
                                AccessTime = dbReader.GetDate("access_time"),
                                Flags = dbReader.GetEnum<AssetFlags>("asset_flags"),
                                Temporary = dbReader.GetBool("temporary")
                            };
                            return true;
                        }
                    }
                }
            }
            metadata = null;
            return false;
        }
        #endregion

        #region References interface
        public sealed class MySQLAssetReferencesService : AssetReferencesServiceInterface
        {
            private readonly MySQLAssetService m_AssetService;

            internal MySQLAssetReferencesService(MySQLAssetService assetService)
            {
                m_AssetService = assetService;
            }

            public override List<UUID> this[UUID key] => m_AssetService.GetAssetRefs(key);
        }

        internal List<UUID> GetAssetRefs(UUID key)
        {
            var references = new List<UUID>();
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                bool processed;
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT usesprocessed FROM assetrefs WHERE id = @id LIMIT 1", conn))
                {
                    cmd.Parameters.AddParameter("@id", key);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        processed = dbReader.Read() && dbReader.GetBool("usesprocessed");
                    }
                }

                AssetData data;
                if(processed)
                {
                    using (var cmd = new MySqlCommand("SELECT usesid FROM assetsinuse WHERE id = @id", conn))
                    {
                        cmd.Parameters.AddParameter("@id", key);
                        using (MySqlDataReader dbReader = cmd.ExecuteReader())
                        {
                            while(dbReader.Read())
                            {
                                references.Add(dbReader.GetUUID("usesid"));
                            }
                        }
                    }
                }
                else if(TryGetValue(key, out data))
                {
                    references = data.References;
                    references.Remove(UUID.Zero);
                    references.Remove(data.ID);
                }
                else
                {
                    throw new AssetNotFoundException(key);
                }

                return references;
            }
        }

        public override AssetReferencesServiceInterface References => m_ReferencesService;
        #endregion

        #region Data interface
        public override IAssetDataServiceInterface Data => this;

        Stream IAssetDataServiceInterface.this[UUID key]
        {
            get
            {
                Stream s;
                if (!Data.TryGetValue(key, out s))
                {
                    throw new AssetNotFoundException(key);
                }
                return s;
            }
        }

        bool IAssetDataServiceInterface.TryGetValue(UUID key, out Stream s)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT data FROM assetrefs INNER JOIN assetdata ON assetrefs.hash = assetdata.hash AND assetrefs.assetType = assetdata.assetType WHERE id=@id LIMIT 1", conn))
                {
                    cmd.Parameters.AddParameter("@id", key);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        if (dbReader.Read())
                        {
                            s = new MemoryStream(dbReader.GetBytes("data"));
                            return true;
                        }
                    }
                }
            }

            s = null;
            return false;
        }
        #endregion

        #region Store asset method
        public override void Store(AssetData asset)
        {
            using (var sha = SHA1.Create())
            {
                byte[] sha1data = sha.ComputeHash(asset.Data);

                using (var conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();

                    conn.InsideTransaction((transaction) =>
                    {
                        using (var cmd = new MySqlCommand(
                                "INSERT INTO assetdata (hash, assetType, data)" +
                                "VALUES(@hash, @assetType, @data) ON DUPLICATE KEY UPDATE assetType=assetType",
                                conn)
                        {
                            Transaction = transaction
                        })
                        {
                            using (cmd)
                            {
                                cmd.Parameters.AddParameter("@hash", sha1data);
                                cmd.Parameters.AddParameter("@assetType", asset.Type);
                                cmd.Parameters.AddParameter("@data", asset.Data);
                                if (cmd.ExecuteNonQuery() < 1)
                                {
                                    throw new AssetStoreFailedException(asset.ID);
                                }
                            }
                        }

                        using (var cmd = new MySqlCommand(
                                "INSERT INTO assetrefs (id, name, assetType, temporary, create_time, access_time, asset_flags, hash)" +
                                "VALUES(@id, @name, @assetType, @temporary, @create_time, @access_time, @asset_flags, @hash) ON " +
                                "DUPLICATE KEY UPDATE access_time=@access_time",
                                conn)
                        {
                            Transaction = transaction
                        })
                        {
                            string assetName = asset.Name;
                            if (asset.Name.Length > MAX_ASSET_NAME)
                            {
                                assetName = asset.Name.Substring(0, MAX_ASSET_NAME);
                                m_Log.WarnFormat("Name '{0}' for asset {1} truncated from {2} to {3} characters on add",
                                    asset.Name, asset.ID, asset.Name.Length, assetName.Length);
                            }

                            // create unix epoch time
                            ulong now = Date.GetUnixTime();
                            cmd.Parameters.AddParameter("@id", asset.ID);
                            cmd.Parameters.AddParameter("@name", assetName);
                            cmd.Parameters.AddParameter("@assetType", asset.Type);
                            cmd.Parameters.AddParameter("@temporary", asset.Temporary);
                            cmd.Parameters.AddParameter("@create_time", now);
                            cmd.Parameters.AddParameter("@access_time", now);
                            cmd.Parameters.AddParameter("@asset_flags", asset.Flags);
                            cmd.Parameters.AddParameter("@hash", sha1data);
                            if (1 > cmd.ExecuteNonQuery())
                            {
                                throw new AssetStoreFailedException(asset.ID);
                            }
                        }
                    });
                }
            }
            EnqueueAsset(asset.ID);
        }
        #endregion

        #region Delete asset method
        public override void Delete(UUID id)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("DELETE FROM assetrefs WHERE id=@id AND asset_flags <> 0", conn))
                {
                    cmd.Parameters.AddParameter("@id", id);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new MySqlCommand("DELETE FROM assetsinuse WHERE id=@id AND NOT EXISTS (SELECT NULL FROM assetrefs WHERE assetrefs.\"id\" = assetsinuse.\"id\")", conn))
                {
                    cmd.Parameters.AddParameter("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }
        #endregion

        #region DBServiceInterface
        public void VerifyConnection()
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                int maxallowedPacket = conn.GetMaxAllowedPacketSize();
                if (maxallowedPacket < 128 * 1024 * 1024)
                {
                    m_ConfigurationIssues.Add(string.Format("Please set max_allowed_packet = 128M in [mysqld] in MySQL/MariaDB. Found {0}", maxallowedPacket));
                }
            }
        }

        public void ProcessMigrations()
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                conn.MigrateTables(Migrations, m_Log);
            }
        }

        public List<UUID> GetAssetList(long start, long count)
        {
            var result = new List<UUID>();
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT id FROM assetrefs ORDER BY id LIMIT @start, @count", conn))
                {
                    cmd.Parameters.AddParameter("start", start);
                    cmd.Parameters.AddParameter("count", count);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(reader.GetUUID("id"));
                        }
                    }
                }
            }
            return result;
        }

        private static readonly IMigrationElement[] Migrations = new IMigrationElement[]
        {
            new SqlTable("assetdata") { IsDynamicRowFormat = true },
            new AddColumn<byte[]>("hash") { IsFixed = true, Cardinality = 20, IsNullAllowed = false },
            new AddColumn<AssetType>("assetType") { IsNullAllowed = false },
            new AddColumn<byte[]>("data") { IsLong = true },
            new PrimaryKeyInfo("hash", "assetType"),

            new SqlTable("assetrefs"),
            new AddColumn<UUID>("id") { IsNullAllowed = false, Default = UUID.Zero },
            new AddColumn<string>("name") { Cardinality = 64, IsNullAllowed = false, Default = string.Empty },
            new AddColumn<AssetType>("assetType") { IsNullAllowed = false },
            new AddColumn<bool>("temporary") { IsNullAllowed = false },
            new AddColumn<Date>("create_time") { IsNullAllowed = false },
            new AddColumn<Date>("access_time") { IsNullAllowed = false },
            new AddColumn<AssetFlags>("asset_flags") { IsNullAllowed = false },
            new AddColumn<UUI>("CreatorID") { IsNullAllowed = false, Default = UUID.Zero },
            new AddColumn<byte[]>("hash") { IsFixed = true, IsNullAllowed = false, Cardinality = 20 },
            new PrimaryKeyInfo("id"),
            new TableRevision(2),
            /* normally not executed but needed */
            new ChangeColumn<bool>("temporary") { IsNullAllowed = false },
            new ChangeColumn<AssetFlags>("asset_flags") { IsNullAllowed = false },
            new ChangeColumn<UUI>("CreatorID") { IsNullAllowed = false, Default = UUID.Zero },
            new TableRevision(3),
            new AddColumn<bool>("usesprocessed") { IsNullAllowed = false, Default = false },
            new TableRevision(4),
            new DropColumn("CreatorID"),
            new TableRevision(5),
            new NamedKeyInfo("assetType_index", "assetType"),
            new NamedKeyInfo("hash_index", "hash"),
            new NamedKeyInfo("usesprocessed_index", "usesprocessed"),

            new SqlTable("assetsinuse"),
            new AddColumn<UUID>("id") { IsNullAllowed = false },
            new AddColumn<UUID>("usesid") { IsNullAllowed = false },
            new PrimaryKeyInfo("id", "usesid"),
            new NamedKeyInfo("id", "id"),
            new NamedKeyInfo("usesid", "usesid")
        };
        #endregion

        private const int MAX_ASSET_NAME = 64;
    }
}

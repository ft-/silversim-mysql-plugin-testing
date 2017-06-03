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
using SilverSim.ServiceInterfaces.Database;
using SilverSim.ServiceInterfaces.Grid;
using SilverSim.ServiceInterfaces.ServerParam;
using SilverSim.Types;
using SilverSim.Types.Grid;
using System.Collections.Generic;
using System.ComponentModel;

namespace SilverSim.Database.MySQL.Grid
{
    #region Service Implementation
    [Description("MySQL Grid Backend")]
    [PluginName("Grid")]
    [ServerParam("DeleteOnUnregister", Type = ServerParamType.GlobalOnly, ParameterType = typeof(bool), DefaultValue = false)]
    [ServerParam("AllowDuplicateRegionNames", Type = ServerParamType.GlobalOnly, ParameterType = typeof(bool), DefaultValue = false)]
    public sealed class MySQLGridService : GridServiceInterface, IDBServiceInterface, IPlugin, IServerParamListener
    {
        private readonly string m_ConnectionString;
        private readonly string m_TableName;
        private static readonly ILog m_Log = LogManager.GetLogger("MYSQL GRID SERVICE");
        private bool m_IsDeleteOnUnregister;
        private bool m_AllowDuplicateRegionNames;
        private readonly bool m_UseRegionDefaultServices;
        private List<RegionDefaultFlagsServiceInterface> m_RegionDefaultServices;

        [ServerParam("DeleteOnUnregister")]
        public void DeleteOnUnregisterUpdated(UUID regionid, string value)
        {
            if(regionid == UUID.Zero)
            {
                m_IsDeleteOnUnregister = bool.Parse(value);
            }
        }

        [ServerParam("AllowDuplicateRegionNames")]
        public void AllowDuplicateRegionNamesUpdated(UUID regionid, string value)
        {
            if (regionid == UUID.Zero)
            {
                m_AllowDuplicateRegionNames = bool.Parse(value);
            }
        }

        #region Constructor
        public MySQLGridService(IConfig ownSection)
        {
            m_UseRegionDefaultServices = ownSection.GetBoolean("UseRegionDefaultServices", true);
            m_ConnectionString = MySQLUtilities.BuildConnectionString(ownSection, m_Log);
            m_TableName = ownSection.GetString("TableName", "regions");
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_RegionDefaultServices = loader.GetServicesByValue<RegionDefaultFlagsServiceInterface>();
        }
        #endregion

        public void VerifyConnection()
        {
            using(var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
            }
        }

        public void ProcessMigrations()
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                var migrations = new List<IMigrationElement>
                {
                    new SqlTable(m_TableName)
                };
                migrations.AddRange(Migrations);
                connection.MigrateTables(migrations.ToArray(), m_Log);
            }
        }

        #region Accessors
        public override RegionInfo this[UUID scopeID, UUID regionID]
        {
            get
            {
                RegionInfo rInfo;
                if(!TryGetValue(scopeID, regionID, out rInfo))
                {
                    throw new KeyNotFoundException();
                }
                return rInfo;
            }
        }

        public override bool TryGetValue(UUID scopeID, UUID regionID, out RegionInfo rInfo)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM `" + MySqlHelper.EscapeString(m_TableName) + "` WHERE uuid LIKE ?id AND ScopeID LIKE ?scopeid", connection))
                {
                    cmd.Parameters.AddParameter("?id", regionID);
                    cmd.Parameters.AddParameter("?scopeid", scopeID);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        if (dbReader.Read())
                        {
                            rInfo = ToRegionInfo(dbReader);
                            return true;
                        }
                    }
                }
            }

            rInfo = default(RegionInfo);
            return false;
        }

        public override bool ContainsKey(UUID scopeID, UUID regionID)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM `" + MySqlHelper.EscapeString(m_TableName) + "` WHERE uuid LIKE ?id AND ScopeID LIKE ?scopeid", connection))
                {
                    cmd.Parameters.AddParameter("?id", regionID);
                    cmd.Parameters.AddParameter("?scopeid", scopeID);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        return dbReader.Read();
                    }
                }
            }
        }

        public override RegionInfo this[UUID scopeID, uint gridX, uint gridY]
        {
            get
            {
                RegionInfo rInfo;
                if(!TryGetValue(scopeID, gridX, gridY, out rInfo))
                {
                    throw new KeyNotFoundException();
                }
                return rInfo;
            }
        }

        public override bool TryGetValue(UUID scopeID, uint gridX, uint gridY, out RegionInfo rInfo)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM `" + MySqlHelper.EscapeString(m_TableName) + "` WHERE locX <= ?x AND locY <= ?y AND locX + sizeX > ?x AND locY + sizeY > ?y AND ScopeID LIKE ?scopeid", connection))
                {
                    cmd.Parameters.AddParameter("?x", gridX);
                    cmd.Parameters.AddParameter("?y", gridY);
                    cmd.Parameters.AddParameter("?scopeid", scopeID);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        if (dbReader.Read())
                        {
                            rInfo = ToRegionInfo(dbReader);
                            return true;
                        }
                    }
                }
            }

            rInfo = default(RegionInfo);
            return false;
        }

        public override bool ContainsKey(UUID scopeID, uint gridX, uint gridY)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM `" + MySqlHelper.EscapeString(m_TableName) + "` WHERE locX <= ?x AND locY <= ?y AND locX + sizeX > ?x AND locY + sizeY > ?y AND ScopeID LIKE ?scopeid", connection))
                {
                    cmd.Parameters.AddParameter("?x", gridX);
                    cmd.Parameters.AddParameter("?y", gridY);
                    cmd.Parameters.AddParameter("?scopeid", scopeID);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        return dbReader.Read();
                    }
                }
            }
        }

        public override RegionInfo this[UUID scopeID, string regionName]
        {
            get
            {
                RegionInfo rInfo;
                if(!TryGetValue(scopeID, regionName, out rInfo))
                {
                    throw new KeyNotFoundException();
                }
                return rInfo;
            }
        }

        public override bool TryGetValue(UUID scopeID, string regionName, out RegionInfo rInfo)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM `" + MySqlHelper.EscapeString(m_TableName) + "` WHERE regionName LIKE ?name AND ScopeID LIKE ?scopeid", connection))
                {
                    cmd.Parameters.AddParameter("?name", regionName);
                    cmd.Parameters.AddParameter("?scopeid", scopeID);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        if (dbReader.Read())
                        {
                            rInfo = ToRegionInfo(dbReader);
                            return true;
                        }
                    }
                }
            }

            rInfo = default(RegionInfo);
            return false;
        }

        public override bool ContainsKey(UUID scopeID, string regionName)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM `" + MySqlHelper.EscapeString(m_TableName) + "` WHERE regionName LIKE ?name AND ScopeID LIKE ?scopeid", connection))
                {
                    cmd.Parameters.AddParameter("?name", regionName);
                    cmd.Parameters.AddParameter("?scopeid", scopeID);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        return dbReader.Read();
                    }
                }
            }
        }

        public override RegionInfo this[UUID regionID]
        {
            get
            {
                RegionInfo rInfo;
                if(!TryGetValue(regionID, out rInfo))
                {
                    throw new KeyNotFoundException();
                }
                return rInfo;
            }
        }

        public override bool TryGetValue(UUID regionID, out RegionInfo rInfo)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM `" + MySqlHelper.EscapeString(m_TableName) + "` WHERE uuid LIKE ?id", connection))
                {
                    cmd.Parameters.AddParameter("?id", regionID);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        if (dbReader.Read())
                        {
                            rInfo = ToRegionInfo(dbReader);
                            return true;
                        }
                    }
                }
            }

            rInfo = default(RegionInfo);
            return false;
        }

        public override bool ContainsKey(UUID regionID)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM `" + MySqlHelper.EscapeString(m_TableName) + "` WHERE uuid LIKE ?id", connection))
                {
                    cmd.Parameters.AddParameter("?id", regionID);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        return dbReader.Read();
                    }
                }
            }
        }
        #endregion

        #region dbData to RegionInfo
        private RegionInfo ToRegionInfo(MySqlDataReader dbReader) => new RegionInfo()
        {
            ID = dbReader.GetUUID("uuid"),
            Name = dbReader.GetString("regionName"),
            RegionSecret = dbReader.GetString("regionSecret"),
            ServerIP = dbReader.GetString("serverIP"),
            ServerPort = dbReader.GetUInt32("serverPort"),
            ServerURI = dbReader.GetString("serverURI"),
            Location = dbReader.GetGridVector("loc"),
            RegionMapTexture = dbReader.GetUUID("regionMapTexture"),
            ServerHttpPort = dbReader.GetUInt32("serverHttpPort"),
            Owner = dbReader.GetUUI("owner"),
            Access = dbReader.GetEnum<RegionAccess>("access"),
            ScopeID = dbReader.GetString("ScopeID"),
            Size = dbReader.GetGridVector("size"),
            Flags = dbReader.GetEnum<RegionFlags>("flags"),
            AuthenticatingToken = dbReader.GetString("AuthenticatingToken"),
            AuthenticatingPrincipal = dbReader.GetUUI("AuthenticatingPrincipalID"),
            ParcelMapTexture = dbReader.GetUUID("parcelMapTexture"),
            ProductName = dbReader.GetString("ProductName")
        };
        #endregion

        #region Region Registration
        public override void AddRegionFlags(UUID regionID, RegionFlags setflags)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("UPDATE `" + MySqlHelper.EscapeString(m_TableName) + "` SET flags = flags | ?flags WHERE uuid LIKE ?regionid", conn))
                {
                    cmd.Parameters.AddParameter("?regionid", regionID);
                    cmd.Parameters.AddParameter("?flags", setflags);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public override void RemoveRegionFlags(UUID regionID, RegionFlags removeflags)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("UPDATE `" + MySqlHelper.EscapeString(m_TableName) + "` SET flags = flags & ~?flags WHERE uuid LIKE ?regionid", conn))
                {
                    cmd.Parameters.AddParameter("?regionid", regionID);
                    cmd.Parameters.AddParameter("?flags", removeflags);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public override void RegisterRegion(RegionInfo regionInfo)
        {
            foreach (RegionDefaultFlagsServiceInterface service in m_RegionDefaultServices)
            {
                regionInfo.Flags |= service.GetRegionDefaultFlags(regionInfo.ID);
            }

            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();

                if(!m_AllowDuplicateRegionNames)
                {
                    using(var cmd = new MySqlCommand("SELECT uuid FROM `" + MySqlHelper.EscapeString(m_TableName) + "` WHERE ScopeID LIKE ?scopeid AND regionName LIKE ?name LIMIT 1", conn))
                    {
                        cmd.Parameters.AddParameter("?scopeid", regionInfo.ScopeID);
                        cmd.Parameters.AddParameter("?name", regionInfo.Name);
                        using(MySqlDataReader dbReader = cmd.ExecuteReader())
                        {
                            if (dbReader.Read() &&
                                dbReader.GetUUID("uuid") != regionInfo.ID)
                            {
                                throw new GridRegionUpdateFailedException("Duplicate region name");
                            }
                        }
                    }
                }

                /* we have to give checks for all intersection variants */
                using(var cmd = new MySqlCommand("SELECT uuid FROM `" + MySqlHelper.EscapeString(m_TableName) + "` WHERE (" +
                            "(locX >= ?minx AND locY >= ?miny AND locX < ?maxx AND locY < ?maxy) OR " +
                            "(locX + sizeX > ?minx AND locY+sizeY > ?miny AND locX + sizeX < ?maxx AND locY + sizeY < ?maxy)" +
                            ") AND uuid NOT LIKE ?regionid AND " +
                            "ScopeID LIKE ?scopeid LIMIT 1", conn))
                {
                    cmd.Parameters.AddParameter("?min", regionInfo.Location);
                    cmd.Parameters.AddParameter("?max", regionInfo.Location + regionInfo.Size);
                    cmd.Parameters.AddParameter("?regionid", regionInfo.ID);
                    cmd.Parameters.AddParameter("?scopeid", regionInfo.ScopeID);
                    using(MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        if (dbReader.Read() &&
                            dbReader.GetUUID("uuid") != regionInfo.ID)
                        {
                            throw new GridRegionUpdateFailedException("Overlapping regions");
                        }
                    }
                }

                var regionData = new Dictionary<string, object>
                {
                    ["uuid"] = regionInfo.ID,
                    ["regionName"] = regionInfo.Name,
                    ["loc"] = regionInfo.Location,
                    ["size"] = regionInfo.Size,
                    ["regionName"] = regionInfo.Name,
                    ["serverIP"] = regionInfo.ServerIP,
                    ["serverHttpPort"] = regionInfo.ServerHttpPort,
                    ["serverURI"] = regionInfo.ServerURI,
                    ["serverPort"] = regionInfo.ServerPort,
                    ["regionMapTexture"] = regionInfo.RegionMapTexture,
                    ["parcelMapTexture"] = regionInfo.ParcelMapTexture,
                    ["access"] = regionInfo.Access,
                    ["regionSecret"] = regionInfo.RegionSecret,
                    ["owner"] = regionInfo.Owner,
                    ["AuthenticatingToken"] = regionInfo.AuthenticatingToken,
                    ["AuthenticatingPrincipalID"] = regionInfo.AuthenticatingPrincipal,
                    ["flags"] = regionInfo.Flags,
                    ["ScopeID"] = regionInfo.ScopeID,
                    ["ProductName"] = regionInfo.ProductName
                };
                MySQLUtilities.ReplaceInto(conn, m_TableName, regionData);
            }
        }

        public override void UnregisterRegion(UUID scopeID, UUID regionID)
        {
            using(var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();

                if(m_IsDeleteOnUnregister)
                {
                    /* we handoff most stuff to mysql here */
                    /* first line deletes only when region is not persistent */
                    using(var cmd = new MySqlCommand("DELETE FROM `" + MySqlHelper.EscapeString(m_TableName) + "` WHERE ScopeID LIKE ?scopeid AND uuid LIKE ?regionid AND (flags & ?persistent) != 0", conn))
                    {
                        cmd.Parameters.AddParameter("?scopeid", scopeID);
                        cmd.Parameters.AddParameter("?regionid", regionID);
                        cmd.Parameters.AddParameter("?persistent", RegionFlags.Persistent);
                        cmd.ExecuteNonQuery();
                    }

                    /* second step is to set it offline when it is persistent */
                }

                using (var cmd = new MySqlCommand("UPDATE `" + MySqlHelper.EscapeString(m_TableName) + "` SET flags = flags - ?online, last_seen=?unixtime WHERE ScopeID LIKE ?scopeid AND uuid LIKE ?regionid AND (flags & ?online) != 0", conn))
                {
                    cmd.Parameters.AddParameter("?scopeid", scopeID);
                    cmd.Parameters.AddParameter("?regionid", regionID);
                    cmd.Parameters.AddParameter("?online", RegionFlags.RegionOnline);
                    cmd.Parameters.AddParameter("?unixtime", Date.Now);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public override void DeleteRegion(UUID scopeID, UUID regionID)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("DELETE FROM `" + MySqlHelper.EscapeString(m_TableName) + "` WHERE ScopeID LIKE ?scopeid AND uuid LIKE ?regionid", conn))
                {
                    cmd.Parameters.AddParameter("?scopeid", scopeID);
                    cmd.Parameters.AddParameter("?regionid", regionID);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        #endregion

        #region List accessors
        private List<RegionInfo> GetRegionsByFlag(UUID scopeID, RegionFlags flags)
        {
            var result = new List<RegionInfo>();

            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand(scopeID == UUID.Zero ?
                    "SELECT * FROM regions WHERE flags & ?flag != 0" :
                    "SELECT * FROM regions WHERE flags & ?flag != 0 AND ScopeID LIKE ?scopeid", connection))
                {
                    cmd.Parameters.AddParameter("?flag", flags);
                    if (scopeID != UUID.Zero)
                    {
                        cmd.Parameters.AddParameter("?scopeid", scopeID);
                    }
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            result.Add(ToRegionInfo(dbReader));
                        }
                    }
                }
            }

            return result;
        }

        public override List<RegionInfo> GetHyperlinks(UUID scopeID) =>
            GetRegionsByFlag(scopeID, RegionFlags.Hyperlink);

        public override List<RegionInfo> GetDefaultRegions(UUID scopeID) =>
            GetRegionsByFlag(scopeID, RegionFlags.DefaultRegion);

        public override List<RegionInfo> GetOnlineRegions(UUID scopeID) =>
            GetRegionsByFlag(scopeID, RegionFlags.RegionOnline);

        public override List<RegionInfo> GetOnlineRegions() =>
            GetRegionsByFlag(UUID.Zero, RegionFlags.RegionOnline);

        public override List<RegionInfo> GetFallbackRegions(UUID scopeID) =>
            GetRegionsByFlag(scopeID, RegionFlags.FallbackRegion);

        public override List<RegionInfo> GetDefaultHypergridRegions(UUID scopeID) =>
            GetRegionsByFlag(scopeID, RegionFlags.DefaultHGRegion);

        public override List<RegionInfo> GetRegionsByRange(UUID scopeID, GridVector min, GridVector max)
        {
            var result = new List<RegionInfo>();

            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM `" + MySqlHelper.EscapeString(m_TableName) + "` WHERE (" +
                        "(locX >= ?xmin AND locY >= ?ymin AND locX <= ?xmax AND locY <= ?ymax) OR " +
                        "(locX + sizeX >= ?xmin AND locY+sizeY >= ?ymin AND locX + sizeX <= ?xmax AND locY + sizeY <= ?ymax) OR " +
                        "(locX >= ?xmin AND locY >= ?ymin AND locX + sizeX > ?xmin AND locY + sizeY > ?ymin) OR " +
                        "(locX >= ?xmax AND locY >= ?ymax AND locX + sizeX > ?xmax AND locY + sizeY > ?ymax)" +
                        ") AND ScopeID LIKE ?scopeid", connection))
                {
                    cmd.Parameters.AddWithValue("?scopeid", scopeID.ToString());
                    cmd.Parameters.AddWithValue("?xmin", min.X);
                    cmd.Parameters.AddWithValue("?ymin", min.Y);
                    cmd.Parameters.AddWithValue("?xmax", max.X);
                    cmd.Parameters.AddWithValue("?ymax", max.Y);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            result.Add(ToRegionInfo(dbReader));
                        }
                    }
                }
            }

            return result;
        }

        public override List<RegionInfo> GetNeighbours(UUID scopeID, UUID regionID)
        {
            RegionInfo ri = this[scopeID, regionID];
            var result = new List<RegionInfo>();

            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM `" + MySqlHelper.EscapeString(m_TableName) + "` WHERE (" +
                                                            "((locX = ?maxX OR locX + sizeX = ?locX)  AND "+
                                                            "(locY <= ?maxY AND locY + sizeY >= ?locY))" +
                                                            " OR " +
                                                            "((locY = ?maxY OR locY + sizeY = ?locY) AND " +
                                                            "(locX <= ?maxX AND locX + sizeX >= ?locX))" +
                                                            ") AND " +
                                                            "ScopeID LIKE ?scopeid", connection))
                {
                    cmd.Parameters.AddWithValue("?scopeid", scopeID.ToString());
                    cmd.Parameters.AddWithValue("?locX", ri.Location.X);
                    cmd.Parameters.AddWithValue("?locY", ri.Location.Y);
                    cmd.Parameters.AddWithValue("?maxX", ri.Size.X + ri.Location.X);
                    cmd.Parameters.AddWithValue("?maxY", ri.Size.Y + ri.Location.Y);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            result.Add(ToRegionInfo(dbReader));
                        }
                    }
                }
            }

            return result;
        }

        public override List<RegionInfo> GetAllRegions(UUID scopeID)
        {
            var result = new List<RegionInfo>();

            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM `" + MySqlHelper.EscapeString(m_TableName) + "`", connection))
                {
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            result.Add(ToRegionInfo(dbReader));
                        }
                    }
                }
            }

            return result;
        }

        public override List<RegionInfo> SearchRegionsByName(UUID scopeID, string searchString)
        {
            var result = new List<RegionInfo>();

            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();

                using (var cmd = new MySqlCommand("SELECT * FROM `" + MySqlHelper.EscapeString(m_TableName) + "` WHERE ScopeID LIKE ?scopeid AND regionName LIKE '"+MySqlHelper.EscapeString(searchString)+"%'", connection))
                {
                    cmd.Parameters.AddWithValue("?scopeid", scopeID.ToString());
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            result.Add(ToRegionInfo(dbReader));
                        }
                    }
                }
            }

            return result;
        }

        public override Dictionary<string, string> GetGridExtraFeatures() =>
            new Dictionary<string, string>();

        #endregion

        private static readonly IMigrationElement[] Migrations = new IMigrationElement[]
        {
            /* no SqlTable here since we are adding it when processing migrations */
            new AddColumn<UUID>("uuid") { IsNullAllowed = false, Default = UUID.Zero },
            new AddColumn<string>("regionName") { Cardinality = 128, IsNullAllowed = false, Default = string.Empty },
            new AddColumn<string>("regionSecret") { Cardinality = 128, IsNullAllowed = false, Default = string.Empty },
            new AddColumn<string>("serverIP") { Cardinality = 64, IsNullAllowed = false, Default = string.Empty },
            new AddColumn<uint>("serverPort") { IsNullAllowed = false },
            new AddColumn<string>("serverURI") { Cardinality = 255, IsNullAllowed = false, Default = string.Empty },
            new AddColumn<GridVector>("loc") { IsNullAllowed = false, Default = GridVector.Zero },
            new AddColumn<UUID>("regionMapTexture") { IsNullAllowed = false, Default = UUID.Zero },
            new AddColumn<uint>("serverHttpPort") { IsNullAllowed = false },
            new AddColumn<UUI>("owner") { IsNullAllowed = false, Default = UUID.Zero },
            new AddColumn<uint>("access") { IsNullAllowed = false, Default = (uint)13 },
            new AddColumn<UUID>("ScopeID") { IsNullAllowed = false, Default = UUID.Zero },
            new AddColumn<GridVector>("Size") { IsNullAllowed = false, Default = GridVector.Zero },
            new AddColumn<uint>("flags") { IsNullAllowed = false, Default = (uint)0 },
            new AddColumn<Date>("last_seen") { IsNullAllowed = false , Default = Date.UnixTimeToDateTime(0) },
            new AddColumn<string>("AuthenticatingToken") { Cardinality = 255, IsNullAllowed = false, Default = string.Empty },
            new AddColumn<UUI>("AuthenticatingPrincipalID") { IsNullAllowed = false, Default = UUID.Zero },
            new AddColumn<UUID>("parcelMapTexture") { IsNullAllowed = false, Default = UUID.Zero },
            new PrimaryKeyInfo("uuid"),
            new NamedKeyInfo("regionName", "regionName"),
            new NamedKeyInfo("ScopeID", "ScopeID"),
            new NamedKeyInfo("flags", "flags"),
            new TableRevision(2),
            new AddColumn<string>("ProductName") { Cardinality = 255, IsNullAllowed = false, Default = "Mainland" },
            new TableRevision(3),
            /* only used as alter table when revision 2 table exists */
            new ChangeColumn<UUI>("AuthenticatingPrincipalID") { IsNullAllowed = false, Default = UUID.Zero },
        };
    }
    #endregion
}

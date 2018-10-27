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

using log4net;
using MySql.Data.MySqlClient;
using Nini.Config;
using SilverSim.Database.MySQL._Migration;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Database;
using SilverSim.ServiceInterfaces.Maptile;
using SilverSim.Types;
using SilverSim.Types.Maptile;
using System.Collections.Generic;
using System.ComponentModel;

namespace SilverSim.Database.MySQL.Maptile
{
    [Description("MySQL Maptile Backend")]
    [PluginName("Maptile")]
    public class MySQLMaptileService : MaptileServiceInterface, IPlugin, IDBServiceInterface
    {
        private readonly string m_ConnectionString;
        private static readonly ILog m_Log = LogManager.GetLogger("MYSQL MAPTILE SERVICE");

        public MySQLMaptileService(IConfig ownSection)
        {
            m_ConnectionString = MySQLUtilities.BuildConnectionString(ownSection, m_Log);
        }

        private static readonly IMigrationElement[] Migrations = new IMigrationElement[]
        {
            new SqlTable("maptiles"),
            new AddColumn<uint>("LocX") { IsNullAllowed = false },
            new AddColumn<uint>("LocY") { IsNullAllowed = false },
            new AddColumn<UUID>("ScopeID") { IsNullAllowed = false, Default = UUID.Zero },
            new AddColumn<Date>("LastUpdate") { IsNullAllowed = false },
            new AddColumn<string>("ContentType") { Cardinality = 255 },
            new AddColumn<int>("ZoomLevel") { IsNullAllowed = false, Default = 1 },
            new AddColumn<byte[]>("Data") { IsLong = true },
            new PrimaryKeyInfo("LocX", "LocY", "ZoomLevel"),
            new TableRevision(2),
            new PrimaryKeyInfo("LocX", "LocY", "ZoomLevel", "ScopeID"),
            new TableRevision(3),
            new PrimaryKeyInfo("LocX", "LocY", "ZoomLevel"),
            new DropColumn("ScopeID")
        };

        public void ProcessMigrations()
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                connection.MigrateTables(Migrations, m_Log);
            }
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* intentionally left empty */
        }

        public void VerifyConnection()
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
            }
        }

        public override bool TryGetValue(GridVector location, int zoomlevel, out MaptileData data)
        {
            data = default(MaptileData);
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM maptiles WHERE LocX = @locx AND LocY = @locy AND ZoomLevel = @zoomlevel LIMIT 1", connection))
                {
                    cmd.Parameters.AddParameter("@locx", location.X);
                    cmd.Parameters.AddParameter("@locy", location.Y);
                    cmd.Parameters.AddParameter("@zoomlevel", zoomlevel);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
                        {
                            data = new MaptileData();
                            data.Location.X = reader.GetUInt32("LocX");
                            data.Location.Y = reader.GetUInt32("LocY");
                            data.LastUpdate = reader.GetDate("LastUpdate");
                            data.ContentType = reader.GetString("ContentType");
                            data.ZoomLevel = reader.GetInt32("ZoomLevel");
                            data.Data = reader.GetBytes("Data");
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public override void Store(MaptileData data)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                var vals = new Dictionary<string, object>
                {
                    ["LocX"] = data.Location.X,
                    ["LocY"] = data.Location.Y,
                    ["LastUpdate"] = data.LastUpdate,
                    ["ContentType"] = data.ContentType,
                    ["ZoomLevel"] = data.ZoomLevel,
                    ["Data"] = data.Data
                };
                connection.ReplaceInto("maptiles", vals);
            }
        }

        public override bool Remove(GridVector location, int zoomlevel)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("DELETE FROM maptiles WHERE LocX = @locx AND LocY = @locy AND ZoomLevel = @zoomlevel", connection))
                {
                    cmd.Parameters.AddParameter("@locx", location.X);
                    cmd.Parameters.AddParameter("@locy", location.Y);
                    cmd.Parameters.AddParameter("@zoomlevel", zoomlevel);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public override List<MaptileInfo> GetUpdateTimes(GridVector minloc, GridVector maxloc, int zoomlevel)
        {
            var infos = new List<MaptileInfo>();

            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT LocX, LocY, LastUpdate FROM maptiles WHERE ZoomLevel = @zoomlevel AND locX >= @locxlow AND locY >= @locylow AND locX <= @locxhigh AND locY <= @locyhigh", connection))
                {
                    cmd.Parameters.AddParameter("@zoomlevel", zoomlevel);
                    cmd.Parameters.AddParameter("@locxlow", minloc.X);
                    cmd.Parameters.AddParameter("@locylow", minloc.Y);
                    cmd.Parameters.AddParameter("@locxhigh", maxloc.X);
                    cmd.Parameters.AddParameter("@locyhigh", maxloc.Y);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while(reader.Read())
                        {
                            var info = new MaptileInfo
                            {
                                Location = new GridVector { X = reader.GetUInt32("LocX"), Y = reader.GetUInt32("LocY") },
                                LastUpdate = reader.GetDate("LastUpdate"),
                                ZoomLevel = zoomlevel
                            };
                            infos.Add(info);
                        }
                    }
                }
            }
            return infos;
        }
    }
}

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
using SilverSim.Types;
using SilverSim.Types.Grid;
using System.Collections.Generic;
using System.ComponentModel;

namespace SilverSim.Database.MySQL.Grid
{
    [Description("MySQL RegionDefaultFlags Backend")]
    [PluginName("RegionDefaultFlags")]
    public class MySQLRegionDefaultFlagsService : RegionDefaultFlagsServiceInterface, IPlugin, IDBServiceInterface
    {
        private readonly string m_ConnectionString;
        private static readonly ILog m_Log = LogManager.GetLogger("MYSQL REGIONDEFAULTFLAGS SERVICE");

        public MySQLRegionDefaultFlagsService(IConfig ownSection)
        {
            m_ConnectionString = MySQLUtilities.BuildConnectionString(ownSection, m_Log);
        }

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

        public override RegionFlags GetRegionDefaultFlags(UUID regionId)
        {
            var flags = RegionFlags.None;
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT flags FROM regiondefaults WHERE uuid = @id", connection))
                {
                    cmd.Parameters.AddParameter("@id", regionId);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
                        {
                            flags = reader.GetEnum<RegionFlags>("flags");
                        }
                    }
                }
            }
            return flags;
        }

        public override void ChangeRegionDefaultFlags(UUID regionId, RegionFlags addFlags, RegionFlags removeFlags)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                connection.InsideTransaction((transaction) =>
                {
                    bool haveEntry = false;
                    using (var cmd = new MySqlCommand("SELECT * FROM regiondefaults WHERE uuid = @id", connection)
                    {
                        Transaction = transaction
                    })
                    {
                        cmd.Parameters.AddParameter("@id", regionId);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            haveEntry = reader.Read();
                        }
                    }

                    if (haveEntry)
                    {
                        using (var cmd = new MySqlCommand("UPDATE regiondefaults SET flags = (flags & @remove) | @add WHERE uuid = @id", connection)
                        {
                            Transaction = transaction
                        })
                        {
                            cmd.Parameters.AddParameter("@remove", ~removeFlags);
                            cmd.Parameters.AddParameter("@add", addFlags);
                            cmd.Parameters.AddParameter("@id", regionId);
                            cmd.ExecuteNonQuery();
                        }
                        using (var cmd = new MySqlCommand("DELETE FROM regiondefaults WHERE flags = 0 AND uuid = @id", connection)
                        {
                            Transaction = transaction
                        })
                        {
                            cmd.Parameters.AddParameter("@id", regionId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        var vals = new Dictionary<string, object>
                        {
                            ["uuid"] = regionId,
                            ["flags"] = addFlags
                        };
                        connection.InsertInto("regiondefaults", vals, transaction);
                    }
                });
            }
        }

        public override Dictionary<UUID, RegionFlags> GetAllRegionDefaultFlags()
        {
            var result = new Dictionary<UUID, RegionFlags>();
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM regiondefaults", connection))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while(reader.Read())
                        {
                            result.Add(reader.GetUUID("uuid"), reader.GetEnum<RegionFlags>("flags"));
                        }
                    }
                }
            }
            return result;
        }

        private static readonly IMigrationElement[] Migrations = new IMigrationElement[]
        {
            new SqlTable("regiondefaults"),
            new AddColumn<UUID>("uuid") { IsNullAllowed = false, Default = UUID.Zero },
            new AddColumn<RegionFlags>("flags") { IsNullAllowed = false, Default = RegionFlags.None },
            new PrimaryKeyInfo("uuid")
        };
    }
}

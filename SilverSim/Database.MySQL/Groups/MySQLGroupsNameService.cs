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
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.Types;
using System.Collections.Generic;
using System.ComponentModel;

namespace SilverSim.Database.MySQL.Groups
{
    [Description("MySQL GroupsName Backend")]
    [PluginName("GroupNames")]
    public sealed class MySQLGroupsNameService : GroupsNameServiceInterface, IDBServiceInterface, IPlugin
    {
        private readonly string m_ConnectionString;
        private static readonly ILog m_Log = LogManager.GetLogger("MYSQL GROUP NAMES SERVICE");

        #region Constructor
        public MySQLGroupsNameService(IConfig ownSection)
        {
            m_ConnectionString = MySQLUtilities.BuildConnectionString(ownSection, m_Log);
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* intentionally left empty */
        }
        #endregion

        #region Accessors
        public override UGI this[UUID groupID]
        {
            get
            {
                UGI ugi;
                if(!TryGetValue(groupID, out ugi))
                {
                    throw new KeyNotFoundException();
                }
                return ugi;
            }
        }

        public override bool TryGetValue(UUID groupID, out UGI ugi)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();

                using (var cmd = new MySqlCommand("SELECT * FROM groupnames WHERE GroupID LIKE '" + groupID.ToString() + "'", connection))
                {
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        if (dbReader.Read())
                        {
                            ugi = ToUGI(dbReader);
                            return true;
                        }
                    }
                }
            }
            ugi = default(UGI);
            return false;
        }

        private static UGI ToUGI(MySqlDataReader dbReader) =>
            new UGI(dbReader.GetUUID("GroupID"), dbReader.GetString("GroupName"), dbReader.GetUri("HomeURI"));

        public override List<UGI> GetGroupsByName(string groupName, int limit)
        {
            var groups = new List<UGI>();
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();

                using (var cmd = new MySqlCommand("SELECT * FROM groupnames WHERE GroupName LIKE ?groupName LIMIT ?limit", connection))
                {
                    cmd.Parameters.AddParameter("?groupName", groupName);
                    cmd.Parameters.AddParameter("?limit", limit);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while(dbReader.Read())
                        {
                            groups.Add(ToUGI(dbReader));
                        }
                    }
                }
            }
            return groups;
        }

        public override void Store(UGI group)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();

                using (var cmd = new MySqlCommand("REPLACE INTO groupnames (GroupID, HomeURI, GroupName) VALUES (?groupID, ?homeURI, ?groupName)", connection))
                {
                    cmd.Parameters.AddParameter("?groupID", group.ID);
                    cmd.Parameters.AddParameter("?homeURI", group.HomeURI);
                    cmd.Parameters.AddParameter("?groupName", group.GroupName);
                    cmd.ExecuteNonQuery();
                }
            }
        }
        #endregion

        public void VerifyConnection()
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
            }
        }

        public void ProcessMigrations()
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                connection.MigrateTables(Migrations, m_Log);
            }
        }

        private static readonly IMigrationElement[] Migrations = new IMigrationElement[]
        {
            new SqlTable("groupnames"),
            new AddColumn<UUID>("GroupID") { IsNullAllowed = false, Default = UUID.Zero },
            new AddColumn<string>("HomeURI") { Cardinality = 255, IsNullAllowed = false, Default = string.Empty },
            new AddColumn<string>("GroupName") { Cardinality = 255, IsNullAllowed = false, Default = string.Empty },
            new PrimaryKeyInfo("GroupID", "HomeURI"),
            new TableRevision(2),
            /* some corrections when revision 1 is found */
            new ChangeColumn<string>("HomeURI") { Cardinality = 255, IsNullAllowed = false, Default = string.Empty },
            new ChangeColumn<string>("GroupName") { Cardinality = 255, IsNullAllowed = false, Default = string.Empty },
        };
    }
}

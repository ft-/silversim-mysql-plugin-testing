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
using SilverSim.ServiceInterfaces.Estate;
using SilverSim.Types;
using SilverSim.Types.Estate;
using System.Collections.Generic;
using System.ComponentModel;

namespace SilverSim.Database.MySQL.Estate
{
    [Description("MySQL Estate Backend")]
    [PluginName("Estate")]
    public sealed partial class MySQLEstateService : EstateServiceInterface, IDBServiceInterface, IPlugin
    {
        private readonly string m_ConnectionString;
        private static readonly ILog m_Log = LogManager.GetLogger("MYSQL ESTATE SERVICE");

        #region Constructor
        public MySQLEstateService(IConfig ownSection)
        {
            m_ConnectionString = MySQLUtilities.BuildConnectionString(ownSection, m_Log);
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* intentionally left empty */
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
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                conn.MigrateTables(Migrations, m_Log);
            }
        }

        private static readonly IMigrationElement[] Migrations = new IMigrationElement[]
        {
            #region estate_regionmap
            new SqlTable("estate_regionmap"),
            new AddColumn<uint>("EstateID") { IsNullAllowed = false },
            new AddColumn<UUID>("RegionID") { Default = UUID.Zero, IsNullAllowed = false },
            new PrimaryKeyInfo("RegionID"),
            new NamedKeyInfo("EstateID", "EstateID"),
            #endregion

            #region estate_managers
            new SqlTable("estate_managers"),
            new AddColumn<uint>("EstateID") { IsNullAllowed = false },
            new AddColumn<UUID>("UserID") { IsNullAllowed = false, Default = UUID.Zero },
            new PrimaryKeyInfo(new string[] { "EstateID", "UserID" }),
            new NamedKeyInfo("UserID", "UserID"),
            new NamedKeyInfo("EstateID", "EstateID"),
            new TableRevision(2),
            new ChangeColumn<UUI>("UserID") { IsNullAllowed = false, Default = UUID.Zero },
            #endregion

            #region estate_groups
            new SqlTable("estate_groups"),
            new AddColumn<uint>("EstateID") { IsNullAllowed = false },
            new AddColumn<UUID>("GroupID") { IsNullAllowed = false, Default = UUID.Zero },
            new PrimaryKeyInfo(new string[] { "EstateID", "GroupID" }),
            new NamedKeyInfo("EstateID", "EstateID"),
            new NamedKeyInfo("GroupID", "GroupID"),
            #endregion

            #region estate_users
            new SqlTable("estate_users"),
            new AddColumn<uint>("EstateID") { IsNullAllowed = false },
            new AddColumn<UUI>("UserID") { IsNullAllowed = false, Default = UUID.Zero },
            new PrimaryKeyInfo(new string[] { "EstateID", "UserID" }),
            new NamedKeyInfo("EstateID", "EstateID"),
            new NamedKeyInfo("UserID", "UserID"),
            new TableRevision(2),
            /* following two entries are not produced as change lines when not finding a revision 1 table */
            new ChangeColumn<uint>("EstateID") { IsNullAllowed = false },
            new ChangeColumn<UUI>("UserID") { IsNullAllowed = false, Default = UUID.Zero },
            #endregion

            #region estate_bans
            new SqlTable("estate_bans"),
            new AddColumn<uint>("EstateID") { IsNullAllowed = false },
            new AddColumn<UUI>("UserID") { IsNullAllowed = false, Default = UUID.Zero },
            new PrimaryKeyInfo(new string[] { "EstateID", "UserID" }),
            new NamedKeyInfo("EstateID", "EstateID"),
            new NamedKeyInfo("UserID", "UserID"),
            new TableRevision(2),
            /* following two entries are not produced as change lines when not finding a revision 1 table */
            new ChangeColumn<uint>("EstateID") { IsNullAllowed = false },
            new ChangeColumn<UUI>("UserID") { IsNullAllowed = false, Default = UUID.Zero },
            #endregion

            #region estates
            new SqlTable("estates"),
            new AddColumn<uint>("ID") { IsNullAllowed = false },
            new AddColumn<string>("Name") { Cardinality = 64, IsNullAllowed = false },
            new AddColumn<UUI>("OwnerID") { IsNullAllowed = false, Default = UUID.Zero },
            new AddColumn<uint>("Flags") { IsNullAllowed = false, Default = (uint)0 },
            new AddColumn<int>("PricePerMeter") { IsNullAllowed = false, Default = 0 },
            new AddColumn<double>("BillableFactor") { IsNullAllowed = false, Default = (double)1 },
            new AddColumn<double>("SunPosition") { IsNullAllowed = false, Default = (double)0 },
            new AddColumn<string>("AbuseEmail") { Cardinality = 255, IsNullAllowed = false, Default = string.Empty },
            new PrimaryKeyInfo("ID"),
            new NamedKeyInfo("Name", "Name") { IsUnique = true },
            new NamedKeyInfo("Owner", "OwnerID"),
            new NamedKeyInfo("ID_Owner", "ID", "OwnerID"),
            new TableRevision(2),
            new AddColumn<UUID>("CovenantID") { IsNullAllowed = false, Default = UUID.Zero },
            new AddColumn<ulong>("CovenantTimestamp") { IsNullAllowed = false, Default = (ulong)0 },
            new TableRevision(3),
            new AddColumn<bool>("UseGlobalTime") { IsNullAllowed = false, Default = true },
            new TableRevision(4),
            new ChangeColumn<UUI>("Owner") { IsNullAllowed = false, Default = UUID.Zero, OldName = "OwnerID" },
            /* ^^ this is for compatibility our list generator actually skips this field when not finding the revision 3 table */
            new TableRevision(5),
            new AddColumn<uint>("ParentEstateID") {IsNullAllowed = false, Default = (uint)1 },
            #endregion

            #region estateexperiences
            new SqlTable("estateexperiences"),
            new AddColumn<uint>("EstateID") { IsNullAllowed = false },
            new AddColumn<UUID>("ExperienceID") { IsNullAllowed = false },
            new AddColumn<bool>("IsAllowed") { IsNullAllowed = false, Default = false },
            new AddColumn<bool>("IsTrusted") { IsNullAllowed = false, Default = false },
            new TableRevision(2),
            new DropColumn("IsTrusted"),
            #endregion

            #region estatetrustedexperiences
            new SqlTable("estatetrustedexperiences"),
            new AddColumn<uint>("EstateID") { IsNullAllowed = false },
            new AddColumn<UUID>("ExperienceID") { IsNullAllowed = false },
            #endregion
        };

        public override bool TryGetValue(uint estateID, out EstateInfo estateInfo)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM estates WHERE ID = @id", conn))
                {
                    cmd.Parameters.AddParameter("@id", estateID);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            estateInfo = reader.ToEstateInfo();
                            return true;
                        }
                    }
                }
            }
            estateInfo = default(EstateInfo);
            return false;
        }

        public override bool TryGetValue(string estateName, out EstateInfo estateInfo)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM estates WHERE Name = @name", conn))
                {
                    cmd.Parameters.AddParameter("@name", estateName);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            estateInfo = reader.ToEstateInfo();
                            return true;
                        }
                    }
                }
            }
            estateInfo = default(EstateInfo);
            return false;
        }

        public override bool ContainsKey(uint estateID)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT id FROM estates WHERE ID = @id", conn))
                {
                    cmd.Parameters.AddParameter("@id", estateID);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public override bool ContainsKey(string estateName)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT id FROM estates WHERE Name = @name", conn))
                {
                    cmd.Parameters.AddParameter("@name", estateName);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public override void Add(EstateInfo estateInfo)
        {
            var dict = new Dictionary<string, object>
            {
                ["ID"] = estateInfo.ID,
                ["Name"] = estateInfo.Name,
                ["Owner"] = estateInfo.Owner,
                ["Flags"] = estateInfo.Flags,
                ["PricePerMeter"] = estateInfo.PricePerMeter,
                ["BillableFactor"] = estateInfo.BillableFactor,
                ["SunPosition"] = estateInfo.SunPosition,
                ["AbuseEmail"] = estateInfo.AbuseEmail,
                ["CovenantID"] = estateInfo.CovenantID,
                ["CovenantTimestamp"] = estateInfo.CovenantTimestamp,
                ["UseGlobalTime"] = estateInfo.UseGlobalTime,
                ["ParentEstateID"] = estateInfo.ParentEstateID
            };
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                conn.InsertInto("estates", dict);
            }
        }

        public override void Update(EstateInfo value)
        {
            var dict = new Dictionary<string, object>
            {
                ["ID"] = value.ID,
                ["Name"] = value.Name,
                ["Owner"] = value.Owner,
                ["Flags"] = (uint)value.Flags,
                ["PricePerMeter"] = value.PricePerMeter,
                ["BillableFactor"] = value.BillableFactor,
                ["SunPosition"] = value.SunPosition,
                ["AbuseEmail"] = value.AbuseEmail,
                ["CovenantID"] = value.CovenantID,
                ["CovenantTimestamp"] = value.CovenantTimestamp,
                ["UseGlobalTime"] = value.UseGlobalTime,
                ["ParentEstateID"] = value.ParentEstateID
            };
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                conn.ReplaceInto("estates", dict);
            }
        }

        public static readonly string[] EstateRemoveTables =
        {
            "estate_regionmap",
            "estate_managers",
            "estate_groups",
            "estate_users",
            "estate_bans",
            "estateexperiences",
            "estatetrustedexperiences",
        };
        public override bool Remove(uint estateID)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                foreach (string table in EstateRemoveTables)
                {
                    using (var cmd = new MySqlCommand("DELETE FROM " + table + " WHERE EstateID = @id", conn))
                    {
                        cmd.Parameters.AddParameter("@id", estateID);
                        cmd.ExecuteNonQuery();
                    }
                }
                using (var cmd = new MySqlCommand("DELETE FROM estates WHERE ID = @id", conn))
                {
                    cmd.Parameters.AddParameter("@id", estateID);
                    return cmd.ExecuteNonQuery() == 1;
                }
            }
        }

        public override EstateInfo this[uint estateID]
        {
            get
            {
                using (var conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("SELECT * FROM estates WHERE ID = @id", conn))
                    {
                        cmd.Parameters.AddParameter("@id", estateID);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if(reader.Read())
                            {
                                return reader.ToEstateInfo();
                            }
                        }
                    }
                }
                throw new KeyNotFoundException();
            }
        }

        public override List<EstateInfo> All
        {
            get
            {
                var list = new List<EstateInfo>();

                using(var conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    using(var cmd = new MySqlCommand("SELECT * FROM estates", conn))
                    {
                        using(MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while(reader.Read())
                            {
                                list.Add(reader.ToEstateInfo());
                            }
                        }
                    }
                }
                return list;
            }
        }

        public override List<uint> AllIDs
        {
            get
            {
                var list = new List<uint>();

                using (var conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("SELECT ID FROM estates", conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add(reader.GetUInt32("ID"));
                            }
                        }
                    }
                }
                return list;
            }
        }

        public override IEstateManagerServiceInterface EstateManager => this;

        public override IEstateOwnerServiceInterface EstateOwner => this;

        public override IEstateAccessServiceInterface EstateAccess => this;

        public override IEstateBanServiceInterface EstateBans => this;

        public override IEstateGroupsServiceInterface EstateGroup => this;

        public override IEstateRegionMapServiceInterface RegionMap => this;

        public override IEstateExperienceServiceInterface Experiences => this;

        public override IEstateTrustedExperienceServiceInterface TrustedExperiences => this;
    }
}

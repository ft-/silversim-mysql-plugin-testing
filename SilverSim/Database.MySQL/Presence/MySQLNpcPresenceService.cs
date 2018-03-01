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
using SilverSim.ServiceInterfaces.Presence;
using SilverSim.Types;
using System.Collections.Generic;
using System.ComponentModel;

namespace SilverSim.Database.MySQL.Presence
{
    [Description("MySQL NpcPresence Backend")]
    [PluginName("NpcPresence")]
    public class MySQLNpcPresenceService : NpcPresenceServiceInterface, IDBServiceInterface, IPlugin
    {
        private readonly string m_ConnectionString;
        private static readonly ILog m_Log = LogManager.GetLogger("MYSQL PRESENCE SERVICE");

        #region Constructor
        public MySQLNpcPresenceService(IConfig ownSection)
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
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                connection.MigrateTables(Migrations, m_Log);
            }
        }

        public override void Store(NpcPresenceInfo presenceInfo)
        {
            var post = new Dictionary<string, object>
            {
                ["NpcID"] = presenceInfo.Npc.ID,
                ["FirstName"] = presenceInfo.Npc.FirstName,
                ["LastName"] = presenceInfo.Npc.LastName,
                ["Owner"] = presenceInfo.Owner,
                ["Group"] = presenceInfo.Group,
                ["Options"] = presenceInfo.Options,
                ["RegionID"] = presenceInfo.RegionID,
                ["Position"] = presenceInfo.Position,
                ["LookAt"] = presenceInfo.LookAt,
                ["SittingOnObjectID"] = presenceInfo.SittingOnObjectID
            };
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                try
                {
                    conn.ReplaceInto("npcpresence", post);
                }
                catch
                {
                    throw new PresenceUpdateFailedException();
                }
            }
        }

        public override void Remove(UUID scopeID, UUID npcID)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("DELETE FROM npcpresence WHERE NpcID = @userid", conn))
                {
                    cmd.Parameters.AddParameter("@userid", npcID);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static readonly IMigrationElement[] Migrations = new IMigrationElement[]
        {
            new SqlTable("npcpresence"),
            new AddColumn<UUID>("NpcID") { IsNullAllowed = false, Default = UUID.Zero },
            new AddColumn<string>("FirstName") { Cardinality = 31, IsNullAllowed = false, Default = string.Empty },
            new AddColumn<string>("LastName") { Cardinality = 31, IsNullAllowed = false, Default = string.Empty },
            new AddColumn<UUI>("Owner") { IsNullAllowed = false, Default = UUI.Unknown },
            new AddColumn<UGI>("Group") { IsNullAllowed = false, Default = UGI.Unknown },
            new AddColumn<NpcOptions>("Options") { IsNullAllowed = false, Default = NpcOptions.None },
            new AddColumn<UUID>("RegionID") { IsNullAllowed = false, Default = UUID.Zero },
            new AddColumn<Vector3>("Position") { IsNullAllowed = false, Default = Vector3.Zero },
            new AddColumn<Vector3>("LookAt") { IsNullAllowed = false, Default = Vector3.UnitX },
            new AddColumn<UUID>("SittingOnObjectID") { IsNullAllowed = false, Default = UUID.Zero },
            new PrimaryKeyInfo("NpcID"),
            new TableRevision(2),
            new NamedKeyInfo("FirstLast", "FirstName", "LastName"),
            new NamedKeyInfo("Region", "RegionID"),
            new NamedKeyInfo("FirstLastRegion", "FirstName", "LastName", "RegionID") { IsUnique = true }
        };

        public override bool ContainsKey(UUID npcid)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM npcpresence WHERE NpcID = @npcid LIMIT 1", conn))
                {
                    cmd.Parameters.AddParameter("@npcid", npcid);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        return reader.Read();
                    }
                }
            }
        }

        private NpcPresenceInfo ReaderToPresenceInfo(MySqlDataReader reader) => new NpcPresenceInfo
        {
            Npc = new UUI
            {
                ID = reader.GetUUID("NpcID"),
                FirstName = reader.GetString("FirstName"),
                LastName = reader.GetString("LastName")
            },
            Owner = reader.GetUUI("Owner"),
            Group = reader.GetUGI("Group"),
            Options = reader.GetEnum<NpcOptions>("Options"),
            RegionID = reader.GetUUID("RegionID"),
            Position = reader.GetVector3("Position"),
            LookAt = reader.GetVector3("LookAt"),
            SittingOnObjectID = reader.GetUUID("SittingOnObjectID")
        };

        public override bool TryGetValue(UUID regionID, string firstname, string lastname, out NpcPresenceInfo presence)
        {
            presence = default(NpcPresenceInfo);
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM npcpresence WHERE RegionID = @regionID AND FirstName = @first AND LastName = @last LIMIT 1", conn))
                {
                    cmd.Parameters.AddParameter("@regionID", regionID);
                    cmd.Parameters.AddParameter("@first", firstname);
                    cmd.Parameters.AddParameter("@last", lastname);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            presence = ReaderToPresenceInfo(reader);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public override bool TryGetValue(UUID npcid, out NpcPresenceInfo presence)
        {
            presence = default(NpcPresenceInfo);
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM npcpresence WHERE NpcID = @npcid LIMIT 1", conn))
                {
                    cmd.Parameters.AddParameter("@npcid", npcid);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            presence = ReaderToPresenceInfo(reader);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public override List<NpcPresenceInfo> this[UUID regionID]
        {
            get
            {
                var presences = new List<NpcPresenceInfo>();
                using (var conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("SELECT * FROM npcpresence WHERE RegionID = @regionID", conn))
                    {
                        cmd.Parameters.AddParameter("@regionID", regionID);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                presences.Add(ReaderToPresenceInfo(reader));
                            }
                        }
                    }
                }
                return presences;
            }
        }
    }
}
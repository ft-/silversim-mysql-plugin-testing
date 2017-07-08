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

using SilverSim.Scene.ServiceInterfaces.SimulationData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SilverSim.Types;
using SilverSim.Types.Parcel;
using MySql.Data.MySqlClient;

namespace SilverSim.Database.MySQL.SimulationData
{
    public class MySQLSimulationDataParcelExperienceListStorage : ISimulationDataParcelExperienceListStorageInterface
    {
        private readonly string m_ConnectionString;
        private readonly string m_TableName;

        public MySQLSimulationDataParcelExperienceListStorage(string connectionString, string tableName)
        {
            m_ConnectionString = connectionString;
            m_TableName = tableName;
        }

        public List<ParcelExperienceEntry> this[UUID regionID, UUID parcelID]
        {
            get
            {
                var result = new List<ParcelExperienceEntry>();
                using (var connection = new MySqlConnection(m_ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new MySqlCommand("SELECT * FROM " + m_TableName + " WHERE RegionID = @regionid AND ParcelID = @parcelid", connection))
                    {
                        cmd.Parameters.AddParameter("@regionid", regionID);
                        cmd.Parameters.AddParameter("@parcelid", parcelID);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var entry = new ParcelExperienceEntry()
                                {
                                    RegionID = reader.GetUUID("RegionID"),
                                    ParcelID = reader.GetUUID("ParcelID"),
                                    ExperienceID = reader.GetUUID("ExperienceID")
                                };
                                result.Add(entry);
                            }
                        }
                    }
                }
                return result;
            }
        }

        public bool this[UUID regionID, UUID parcelID, UUID experienceID]
        {
            get
            {
                using (var connection = new MySqlConnection(m_ConnectionString))
                {
                    connection.Open();
                    /* we use a specific implementation to reduce the result set here */
                    using (var cmd = new MySqlCommand("SELECT ExperienceID FROM " + m_TableName + " WHERE RegionID = @regionid AND ParcelID = @parcelid AND ExperienceID LIKE @experienceid", connection))
                    {
                        cmd.Parameters.AddParameter("@regionid", regionID);
                        cmd.Parameters.AddParameter("@parcelid", parcelID);
                        cmd.Parameters.AddParameter("@experienceid", experienceID);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            return reader.Read();
                        }
                    }
                }
            }
        }

        public bool Remove(UUID regionID, UUID parcelID)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("DELETE FROM " + m_TableName + " WHERE RegionID = @regionid AND ParcelID = @parcelid", connection))
                {
                    cmd.Parameters.AddParameter("@regionid", regionID);
                    cmd.Parameters.AddParameter("@parcelid", parcelID);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool Remove(UUID regionID, UUID parcelID, UUID experienceID)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("DELETE FROM " + m_TableName + " WHERE RegionID = @regionid AND ParcelID = @parcelid AND ExperienceID = @experienceid", connection))
                {
                    cmd.Parameters.AddParameter("@regionid", regionID);
                    cmd.Parameters.AddParameter("@parcelid", parcelID);
                    cmd.Parameters.AddParameter("@experienceid", experienceID);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool RemoveAllFromRegion(UUID regionID)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("DELETE FROM " + m_TableName + " WHERE RegionID = @regionid", connection))
                {
                    cmd.Parameters.AddParameter("@regionid", regionID);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public void Store(ParcelExperienceEntry entry)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                var data = new Dictionary<string, object>
                {
                    ["RegionID"] = entry.RegionID,
                    ["ParcelID"] = entry.ParcelID,
                    ["ExperienceID"] = entry.ExperienceID
                };
                connection.ReplaceInto(m_TableName, data);
            }
        }
    }
}

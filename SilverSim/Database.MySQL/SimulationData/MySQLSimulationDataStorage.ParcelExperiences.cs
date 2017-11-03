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
using SilverSim.Scene.ServiceInterfaces.SimulationData;
using SilverSim.Scene.Types.Scene;
using SilverSim.Types;
using SilverSim.Types.Parcel;
using System.Collections.Generic;

namespace SilverSim.Database.MySQL.SimulationData
{
    public sealed partial class MySQLSimulationDataStorage : ISimulationDataParcelExperienceListStorageInterface
    {
        List<ParcelExperienceEntry> IParcelExperienceList.this[UUID regionID, UUID parcelID]
        {
            get
            {
                var result = new List<ParcelExperienceEntry>();
                using (var connection = new MySqlConnection(m_ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new MySqlCommand("SELECT * FROM parcelexperiences WHERE RegionID = @regionid AND ParcelID = @parcelid", connection))
                    {
                        cmd.Parameters.AddParameter("@regionid", regionID);
                        cmd.Parameters.AddParameter("@parcelid", parcelID);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var entry = new ParcelExperienceEntry
                                {
                                    RegionID = reader.GetUUID("RegionID"),
                                    ParcelID = reader.GetUUID("ParcelID"),
                                    ExperienceID = reader.GetUUID("ExperienceID"),
                                    IsAllowed = reader.GetBool("IsAllowed")
                                };
                                result.Add(entry);
                            }
                        }
                    }
                }
                return result;
            }
        }

        ParcelExperienceEntry IParcelExperienceList.this[UUID regionID, UUID parcelID, UUID experienceID]
        {
            get
            {
                ParcelExperienceEntry exp;
                if(!Parcels.Experiences.TryGetValue(regionID, parcelID, experienceID, out exp))
                {
                    throw new KeyNotFoundException();
                }
                return exp;
            }
        }

        bool IParcelExperienceList.Remove(UUID regionID, UUID parcelID)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("DELETE FROM parcelexperiences WHERE RegionID = @regionid AND ParcelID = @parcelid", connection))
                {
                    cmd.Parameters.AddParameter("@regionid", regionID);
                    cmd.Parameters.AddParameter("@parcelid", parcelID);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        bool IParcelExperienceList.Remove(UUID regionID, UUID parcelID, UUID experienceID)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("DELETE FROM parcelexperiences WHERE RegionID = @regionid AND ParcelID = @parcelid AND ExperienceID = @experienceid", connection))
                {
                    cmd.Parameters.AddParameter("@regionid", regionID);
                    cmd.Parameters.AddParameter("@parcelid", parcelID);
                    cmd.Parameters.AddParameter("@experienceid", experienceID);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        bool ISimulationDataParcelExperienceListStorageInterface.RemoveAllFromRegion(UUID regionID)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("DELETE FROM parcelexperiences WHERE RegionID = @regionid", connection))
                {
                    cmd.Parameters.AddParameter("@regionid", regionID);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        void IParcelExperienceList.Store(ParcelExperienceEntry entry)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                var data = new Dictionary<string, object>
                {
                    ["RegionID"] = entry.RegionID,
                    ["ParcelID"] = entry.ParcelID,
                    ["ExperienceID"] = entry.ExperienceID,
                    ["IsAllowed"] = entry.IsAllowed
                };
                connection.ReplaceInto("parcelexperiences", data);
            }
        }

        bool IParcelExperienceList.TryGetValue(UUID regionID, UUID parcelID, UUID experienceID, out ParcelExperienceEntry entry)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                /* we use a specific implementation to reduce the result set here */
                using (var cmd = new MySqlCommand("SELECT IsAllowed FROM parcelexperiences WHERE RegionID = @regionid AND ParcelID = @parcelid AND ExperienceID LIKE @experienceid", connection))
                {
                    cmd.Parameters.AddParameter("@regionid", regionID);
                    cmd.Parameters.AddParameter("@parcelid", parcelID);
                    cmd.Parameters.AddParameter("@experienceid", experienceID);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
                        {
                            entry = new ParcelExperienceEntry
                            {
                                RegionID = regionID,
                                ParcelID = parcelID,
                                ExperienceID = experienceID,
                                IsAllowed = reader.GetBool("IsAllowed")
                            };
                            return true;
                        }
                    }
                }
                entry = default(ParcelExperienceEntry);
                return false;
            }
        }
    }
}

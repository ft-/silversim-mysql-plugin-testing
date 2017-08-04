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

using MySql.Data.MySqlClient;
using SilverSim.Scene.ServiceInterfaces.SimulationData;
using SilverSim.Scene.Types.Scene;
using SilverSim.Types;
using System.Collections.Generic;

namespace SilverSim.Database.MySQL.SimulationData
{
    public partial class MySQLSimulationDataStorage : ISimulationDataRegionSettingsStorageInterface
    {
        private RegionSettings ToRegionSettings(MySqlDataReader reader) => new RegionSettings()
        {
            BlockTerraform = reader.GetBool("BlockTerraform"),
            BlockFly = reader.GetBool("BlockFly"),
            AllowDamage = reader.GetBool("AllowDamage"),
            RestrictPushing = reader.GetBool("RestrictPushing"),
            AllowLandResell = reader.GetBool("AllowLandResell"),
            AllowLandJoinDivide = reader.GetBool("AllowLandJoinDivide"),
            BlockShowInSearch = reader.GetBool("BlockShowInSearch"),
            AgentLimit = reader.GetInt32("AgentLimit"),
            ObjectBonus = reader.GetDouble("ObjectBonus"),
            DisableScripts = reader.GetBool("DisableScripts"),
            DisableCollisions = reader.GetBool("DisableCollisions"),
            BlockFlyOver = reader.GetBool("BlockFlyOver"),
            Sandbox = reader.GetBool("Sandbox"),
            TerrainTexture1 = reader.GetUUID("TerrainTexture1"),
            TerrainTexture2 = reader.GetUUID("TerrainTexture2"),
            TerrainTexture3 = reader.GetUUID("TerrainTexture3"),
            TerrainTexture4 = reader.GetUUID("TerrainTexture4"),
            TelehubObject = reader.GetUUID("TelehubObject"),
            Elevation1NW = reader.GetDouble("Elevation1NW"),
            Elevation2NW = reader.GetDouble("Elevation2NW"),
            Elevation1NE = reader.GetDouble("Elevation1NE"),
            Elevation2NE = reader.GetDouble("Elevation2NE"),
            Elevation1SE = reader.GetDouble("Elevation1SE"),
            Elevation2SE = reader.GetDouble("Elevation2SE"),
            Elevation1SW = reader.GetDouble("Elevation1SW"),
            Elevation2SW = reader.GetDouble("Elevation2SW"),
            WaterHeight = reader.GetDouble("WaterHeight"),
            TerrainRaiseLimit = reader.GetDouble("TerrainRaiseLimit"),
            TerrainLowerLimit = reader.GetDouble("TerrainLowerLimit"),
            SunPosition = reader.GetDouble("SunPosition"),
            IsSunFixed = reader.GetBoolean("IsSunFixed"),
            UseEstateSun = reader.GetBool("UseEstateSun"),
            BlockDwell = reader.GetBool("BlockDwell"),
            ResetHomeOnTeleport = reader.GetBool("ResetHomeOnTeleport"),
            AllowLandmark = reader.GetBool("AllowLandmark"),
            AllowDirectTeleport = reader.GetBool("AllowDirectTeleport"),
            MaxBasePrims = reader.GetInt32("MaxBasePrims"),
            WalkableCoefficientsSerialization = reader.GetBytes("WalkableCoefficientsData")
        };

        RegionSettings ISimulationDataRegionSettingsStorageInterface.this[UUID regionID]
        {
            get
            {
                RegionSettings settings;
                if (!RegionSettings.TryGetValue(regionID, out settings))
                {
                    throw new KeyNotFoundException();
                }
                return settings;
            }
            set
            {
                using (var conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    var data = new Dictionary<string, object>
                    {
                        ["RegionID"] = regionID,
                        ["BlockTerraform"] = value.BlockTerraform,
                        ["BlockFly"] = value.BlockFly,
                        ["AllowDamage"] = value.AllowDamage,
                        ["RestrictPushing"] = value.RestrictPushing,
                        ["AllowLandResell"] = value.AllowLandResell,
                        ["AllowLandJoinDivide"] = value.AllowLandJoinDivide,
                        ["BlockShowInSearch"] = value.BlockShowInSearch,
                        ["AgentLimit"] = value.AgentLimit,
                        ["ObjectBonus"] = value.ObjectBonus,
                        ["DisableScripts"] = value.DisableScripts,
                        ["DisableCollisions"] = value.DisableCollisions,
                        ["BlockFlyOver"] = value.BlockFlyOver,
                        ["Sandbox"] = value.Sandbox,
                        ["TerrainTexture1"] = value.TerrainTexture1,
                        ["TerrainTexture2"] = value.TerrainTexture2,
                        ["TerrainTexture3"] = value.TerrainTexture3,
                        ["TerrainTexture4"] = value.TerrainTexture4,
                        ["TelehubObject"] = value.TelehubObject,
                        ["Elevation1NW"] = value.Elevation1NW,
                        ["Elevation2NW"] = value.Elevation2NW,
                        ["Elevation1NE"] = value.Elevation1NE,
                        ["Elevation2NE"] = value.Elevation2NE,
                        ["Elevation1SE"] = value.Elevation1SE,
                        ["Elevation2SE"] = value.Elevation2SE,
                        ["Elevation1SW"] = value.Elevation1SW,
                        ["Elevation2SW"] = value.Elevation2SW,
                        ["WaterHeight"] = value.WaterHeight,
                        ["TerrainRaiseLimit"] = value.TerrainRaiseLimit,
                        ["TerrainLowerLimit"] = value.TerrainLowerLimit,
                        ["SunPosition"] = value.SunPosition,
                        ["IsSunFixed"] = value.IsSunFixed,
                        ["UseEstateSun"] = value.UseEstateSun,
                        ["BlockDwell"] = value.BlockDwell,
                        ["ResetHomeOnTeleport"] = value.ResetHomeOnTeleport,
                        ["AllowLandmark"] = value.AllowLandmark,
                        ["AllowDirectTeleport"] = value.AllowDirectTeleport,
                        ["MaxBasePrims"] = value.MaxBasePrims,
                        ["WalkableCoefficientsData"] = value.WalkableCoefficientsSerialization
                    };
                    conn.ReplaceInto("regionsettings", data);
                }
            }
        }

        bool ISimulationDataRegionSettingsStorageInterface.TryGetValue(UUID regionID, out RegionSettings settings)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM regionsettings WHERE RegionID = @regionid", conn))
                {
                    cmd.Parameters.AddWithValue("@regionid", regionID);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            settings = ToRegionSettings(reader);
                            return true;
                        }
                    }
                }
            }
            settings = null;
            return false;
        }

        bool ISimulationDataRegionSettingsStorageInterface.ContainsKey(UUID regionID)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT RegionID FROM regionsettings WHERE RegionID = @regionid", conn))
                {
                    cmd.Parameters.AddParameter("@regionid", regionID);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        return reader.Read();
                    }
                }
            }
        }

        bool ISimulationDataRegionSettingsStorageInterface.Remove(UUID regionID)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("DELETE FROM regionsettings WHERE RegionID = @regionid", conn))
                {
                    cmd.Parameters.AddParameter("@regionid", regionID);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
    }
}

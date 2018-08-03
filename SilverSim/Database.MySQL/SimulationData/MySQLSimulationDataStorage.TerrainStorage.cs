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
using SilverSim.Types;
using SilverSim.Viewer.Messages.LayerData;
using System.Collections.Generic;

namespace SilverSim.Database.MySQL.SimulationData
{
    public partial class MySQLSimulationDataStorage : ISimulationDataTerrainStorageInterface
    {
        List<LayerPatch> ISimulationDataTerrainStorageInterface.this[UUID regionID]
        {
            get
            {
                var patches = new List<LayerPatch>();
                using (var connection = new MySqlConnection(m_ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new MySqlCommand("SELECT PatchID, TerrainData FROM terrains WHERE RegionID = @regionid", connection))
                    {
                        cmd.Parameters.AddParameter("@regionid", regionID);
                        cmd.CommandTimeout = 3600;
                        using (MySqlDataReader dbReader = cmd.ExecuteReader())
                        {
                            while (dbReader.Read())
                            {
                                var patch = new LayerPatch
                                {
                                    ExtendedPatchID = dbReader.GetUInt32("PatchID"),
                                    Serialization = dbReader.GetBytes("TerrainData")
                                };
                                patches.Add(patch);
                            }
                        }
                    }
                }
                return patches;
            }
        }

        bool ISimulationDataTerrainStorageInterface.TryGetDefault(UUID regionID, List<LayerPatch> list)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("SELECT PatchID, TerrainData FROM defaultterrains WHERE RegionID = @regionid", connection))
                {
                    cmd.Parameters.AddParameter("@regionid", regionID);
                    cmd.CommandTimeout = 3600;
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            var patch = new LayerPatch
                            {
                                ExtendedPatchID = dbReader.GetUInt32("PatchID"),
                                Serialization = dbReader.GetBytes("TerrainData")
                            };
                            list.Add(patch);
                        }
                    }
                }
            }
            return list.Count != 0;
        }
    }
}

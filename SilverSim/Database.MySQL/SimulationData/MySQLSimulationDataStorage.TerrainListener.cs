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
using SilverSim.ServiceInterfaces.Statistics;
using SilverSim.Threading;
using SilverSim.Types;
using SilverSim.Viewer.Messages.LayerData;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SilverSim.Database.MySQL.SimulationData
{
    public partial class MySQLSimulationDataStorage
    {
        readonly RwLockedList<MySQLTerrainListener> m_TerrainListenerThreads = new RwLockedList<MySQLTerrainListener>();
        public class MySQLTerrainListener : TerrainListener
        {
            readonly RwLockedList<MySQLTerrainListener> m_TerrainListenerThreads;
            readonly string m_ConnectionString;

            public MySQLTerrainListener(string connectionString, UUID regionID, RwLockedList<MySQLTerrainListener> terrainListenerThreads)
            {
                m_ConnectionString = connectionString;
                RegionID = regionID;
                m_TerrainListenerThreads = terrainListenerThreads;
            }

            public UUID RegionID { get; }

            public QueueStat GetStats()
            {
                int count = m_StorageTerrainRequestQueue.Count;
                return new QueueStat(count != 0 ? "PROCESSING" : "IDLE", count, (uint)m_ProcessedPatches);
            }

            int m_ProcessedPatches;

            protected override void StorageTerrainThread()
            {
                try
                {
                    m_TerrainListenerThreads.Add(this);
                    Thread.CurrentThread.Name = "Storage Terrain Thread: " + RegionID.ToString();

                    var knownSerialNumbers = new C5.TreeDictionary<uint, uint>();
                    string replaceIntoTerrain = string.Empty;
                    var updateRequests = new List<string>();

                    while (!m_StopStorageThread || m_StorageTerrainRequestQueue.Count != 0)
                    {
                        LayerPatch req;
                        try
                        {
                            req = m_StorageTerrainRequestQueue.Dequeue(1000);
                        }
                        catch
                        {
                            continue;
                        }

                        if (req == null)
                        {
                            using (var connection = new MySqlConnection(m_ConnectionString))
                            {
                                connection.Open();
                                connection.InsideTransaction((transaction) =>
                                {
                                    using (var cmd = new MySqlCommand("REPLACE INTO defaultterrains (RegionID, PatchID, TerrainData) SELECT RegionID, PatchID, TerrainData FROM terrains WHERE RegionID=@regionid", connection)
                                    {
                                        Transaction = transaction
                                    })
                                    {
                                        cmd.Parameters.AddParameter("@RegionID", RegionID);
                                        cmd.ExecuteNonQuery();
                                    }
                                });
                            }
                        }
                        else
                        {

                            uint serialNumber = req.Serial;

                            if (!knownSerialNumbers.Contains(req.ExtendedPatchID) || knownSerialNumbers[req.ExtendedPatchID] != req.Serial)
                            {
                                var data = new Dictionary<string, object>
                                {
                                    ["RegionID"] = RegionID,
                                    ["PatchID"] = req.ExtendedPatchID,
                                    ["TerrainData"] = req.Serialization
                                };
                                if (replaceIntoTerrain.Length == 0)
                                {
                                    replaceIntoTerrain = "REPLACE INTO terrains (" + MySQLUtilities.GenerateFieldNames(data) + ") VALUES ";
                                }
                                updateRequests.Add("(" + MySQLUtilities.GenerateValues(data) + ")");
                                knownSerialNumbers[req.ExtendedPatchID] = serialNumber;
                            }

                            if ((m_StorageTerrainRequestQueue.Count == 0 && updateRequests.Count > 0) || updateRequests.Count >= 256)
                            {
                                string elems = string.Join(",", updateRequests);
                                try
                                {
                                    using (var conn = new MySqlConnection(m_ConnectionString))
                                    {
                                        conn.Open();
                                        using (var cmd = new MySqlCommand(replaceIntoTerrain + elems, conn))
                                        {
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                    updateRequests.Clear();
                                    Interlocked.Increment(ref m_ProcessedPatches);
                                }
                                catch (Exception e)
                                {
                                    m_Log.Error("Terrain store failed", e);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    m_TerrainListenerThreads.Remove(this);
                }
            }
        }

        public override TerrainListener GetTerrainListener(UUID regionID) =>
            new MySQLTerrainListener(m_ConnectionString, regionID, m_TerrainListenerThreads);
    }
}

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
using SilverSim.Types;
using System.Collections.Generic;

namespace SilverSim.Database.MySQL.SimulationData
{
    public partial class MySQLSimulationDataStorage : ISimulationDataScriptStateStorageInterface
    {
        bool ISimulationDataScriptStateStorageInterface.TryGetValue(UUID regionID, UUID primID, UUID itemID, out byte[] state)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();

                using (var cmd = new MySqlCommand("SELECT ScriptState FROM scriptstates WHERE RegionID = @regionid AND PrimID = @primid AND ItemID = @itemid", connection))
                {
                    cmd.Parameters.AddParameter("@regionid", regionID);
                    cmd.Parameters.AddParameter("@primid", primID);
                    cmd.Parameters.AddParameter("@itemid", itemID);
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        if (dbReader.Read())
                        {
                            state = dbReader.GetBytes("ScriptState");
                            return true;
                        }
                    }
                }
            }
            state = null;
            return false;
        }

        byte[] ISimulationDataScriptStateStorageInterface.this[UUID regionID, UUID primID, UUID itemID]
        {
            get
            {
                byte[] state;
                if(!ScriptStates.TryGetValue(regionID, primID, itemID, out state))
                {
                    throw new KeyNotFoundException();
                }

                return state;
            }
            set
            {
                using(var connection = new MySqlConnection(m_ConnectionString))
                {
                    connection.Open();

                    var p = new Dictionary<string, object>
                    {
                        ["RegionID"] = regionID,
                        ["PrimID"] = primID,
                        ["ItemID"] = itemID,
                        ["ScriptState"] = value
                    };
                    MySQLUtilities.ReplaceInto(connection, "scriptstates", p);
                }
            }
        }

        bool ISimulationDataScriptStateStorageInterface.Remove(UUID regionID, UUID primID, UUID itemID)
        {
            using (var connection = new MySqlConnection(m_ConnectionString))
            {
                connection.Open();
                using (var cmd = new MySqlCommand("DELETE FROM scriptstates WHERE RegionID = @regionid AND PrimID = @primid AND ItemID = @itemid", connection))
                {
                    cmd.Parameters.AddParameter("@regionid", regionID);
                    cmd.Parameters.AddParameter("@primid", primID);
                    cmd.Parameters.AddParameter("@itemid", itemID);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
    }
}

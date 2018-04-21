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
using SilverSim.ServiceInterfaces.Estate;
using SilverSim.Types;
using System.Collections.Generic;

namespace SilverSim.Database.MySQL.Estate
{
    public partial class MySQLEstateService : IEstateOwnerServiceInterface
    {
        bool IEstateOwnerServiceInterface.TryGetValue(uint estateID, out UGUI uui)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT Owner FROM estates WHERE ID = @id LIMIT 1", conn))
                {
                    cmd.Parameters.AddParameter("@id", estateID);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            uui = reader.GetUGUI("Owner");
                            return true;
                        }
                    }
                }
            }
            uui = default(UGUI);
            return false;
        }

        List<uint> IEstateOwnerServiceInterface.this[UGUI owner]
        {
            get
            {
                var estates = new List<uint>();
                using (var conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("SELECT ID, Owner FROM estates WHERE Owner LIKE @agentid", conn))
                    {
                        cmd.Parameters.AddParameter("@id", owner.ID);
                        cmd.Parameters.AddParameter("@agentid", owner.ID.ToString() + "%");
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                UGUI uui = reader.GetUGUI("Owner");
                                if (uui.EqualsGrid(owner))
                                {
                                    estates.Add(reader.GetUInt32("ID"));
                                }
                            }
                            return estates;
                        }
                    }
                }
            }
        }

        UGUI IEstateOwnerServiceInterface.this[uint estateID]
        {
            get
            {
                UGUI uui;
                if(!EstateOwner.TryGetValue(estateID, out uui))
                {
                    throw new KeyNotFoundException();
                }
                return uui;
            }
            set
            {
                using (var conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("UPDATE estates SET Owner = @ownerid WHERE ID = @id", conn))
                    {
                        cmd.Parameters.AddParameter("@id", estateID);
                        cmd.Parameters.AddParameter("@ownerid", value);
                        if(cmd.ExecuteNonQuery() < 1)
                        {
                            throw new EstateUpdateFailedException();
                        }
                    }
                }
            }
        }
    }
}

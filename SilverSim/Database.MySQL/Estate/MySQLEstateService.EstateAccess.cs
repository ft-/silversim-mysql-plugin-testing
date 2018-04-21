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
    public partial class MySQLEstateService : IEstateAccessServiceInterface, IEstateAccessServiceListAccessInterface
    {
        List<UGUI> IEstateAccessServiceListAccessInterface.this[uint estateID]
        {
            get 
            {
                var estateusers = new List<UGUI>();
                using (var conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("SELECT UserID FROM estate_users WHERE EstateID = @estateid", conn))
                    {
                        cmd.Parameters.AddParameter("@estateid", estateID);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                estateusers.Add(reader.GetUGUI("UserID"));
                            }
                        }
                    }
                }
                return estateusers;
            }
        }

        bool IEstateAccessServiceInterface.this[uint estateID, UGUI agent]
        {
            get
            {
                using (var conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("SELECT UserID FROM estate_users WHERE EstateID = @estateid AND UserID LIKE @agentid", conn))
                    {
                        cmd.Parameters.AddParameter("@estateid", estateID);
                        cmd.Parameters.AddParameter("@agentid", agent.ID.ToString() + "%");
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while(reader.Read())
                            {
                                UGUI uui = reader.GetUGUI("UserID");
                                if(uui.EqualsGrid(agent))
                                {
                                    return true;
                                }
                            }
                            return false;
                        }
                    }
                }
            }
            set
            {
                string query = value ?
                    "REPLACE INTO estate_users (EstateID, UserID) VALUES (@estateid, @userid)" :
                    "DELETE FROM estate_users WHERE EstateID = @estateid AND UserID LIKE @userid";

                using (var conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddParameter("@estateid", estateID);
                        if (value)
                        {
                            cmd.Parameters.AddParameter("@userid", agent);
                        }
                        else
                        {
                            cmd.Parameters.AddParameter("@userid", agent.ID + "%");
                        }
                        if (cmd.ExecuteNonQuery() < 1 && value)
                        {
                            throw new EstateUpdateFailedException();
                        }
                    }
                }
            }
        }

        IEstateAccessServiceListAccessInterface IEstateAccessServiceInterface.All => this;
    }
}

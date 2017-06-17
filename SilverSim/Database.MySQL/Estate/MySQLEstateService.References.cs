using MySql.Data.MySqlClient;
using SilverSim.ServiceInterfaces.Purge;
using SilverSim.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SilverSim.Database.MySQL.Estate
{
    public sealed partial class MySQLEstateService : IAssetReferenceInfoServiceInterface
    {
        public void EnumerateUsedAssets(Action<UUID> action)
        {
            using (MySqlConnection conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand("SELECT DISTINCT CovenantID FROM estates", conn))
                {
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            UUID id = dbReader.GetUUID("CovenantID");
                            if (id != UUID.Zero)
                            {
                                action(id);
                            }
                        }
                    }
                }
            }
        }
    }
}

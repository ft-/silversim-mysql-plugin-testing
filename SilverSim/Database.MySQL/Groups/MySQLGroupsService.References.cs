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
using SilverSim.ServiceInterfaces.Purge;
using SilverSim.Types;
using System;

namespace SilverSim.Database.MySQL.Groups
{
    public sealed partial class MySQLGroupsService : IAssetReferenceInfoServiceInterface
    {
        public void EnumerateUsedAssets(Action<UUID> action)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT DISTINCT InsigniaID FROM groups", conn))
                {
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            UUID id = dbReader.GetUUID("InsigniaID");
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
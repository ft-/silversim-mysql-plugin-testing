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
using SilverSim.ServiceInterfaces.Experience;
using SilverSim.Types;
using System.Collections.Generic;

namespace SilverSim.Database.MySQL.Experience
{
    public sealed partial class MySQLExperienceService : ExperienceServiceInterface.IExperiencePermissionsInterface
    {
        Dictionary<UUID, bool> IExperiencePermissionsInterface.this[UGUI agent]
        {
            get
            {
                var result = new Dictionary<UUID, bool>();
                using (var conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("SELECT ExperienceID,User,IsAllowed FROM experienceusers WHERE User LIKE @user", conn))
                    {
                        cmd.Parameters.AddParameter("@user", agent.ID.ToString() + "%");
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                UGUI ret = reader.GetUGUI("User");
                                if (ret.EqualsGrid(agent))
                                {
                                    result.Add(reader.GetUUID("ExperienceID"), reader.GetBool("IsAllowed"));
                                }
                            }
                        }
                    }
                }

                return result;
            }
        }

        bool IExperiencePermissionsInterface.this[UUID experienceID, UGUI agent]
        {
            get
            {
                bool ret;
                if(!Permissions.TryGetValue(experienceID, agent, out ret))
                {
                    throw new KeyNotFoundException();
                }
                return ret;
            }

            set
            {
                var vals = new Dictionary<string, object>
                {
                    ["ExperienceID"] = experienceID,
                    ["User"] = agent,
                    ["IsAllowed"] = value
                };
                using (var conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    conn.ReplaceInto("experienceusers", vals);
                }
            }
        }

        bool IExperiencePermissionsInterface.Remove(UUID experienceID, UGUI agent)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("DELETE FROM experienceusers WHERE ExperienceID = @experienceid AND User LIKE @user", conn))
                {
                    cmd.Parameters.AddParameter("@experienceid", experienceID);
                    cmd.Parameters.AddParameter("@user", agent.ID.ToString() + "%");
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        bool IExperiencePermissionsInterface.TryGetValue(UUID experienceID, UGUI agent, out bool allowed)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT User,IsAllowed FROM experienceusers WHERE ExperienceID = @experienceid AND User LIKE @user", conn))
                {
                    cmd.Parameters.AddParameter("@experienceid", experienceID);
                    cmd.Parameters.AddParameter("@user", agent.ID.ToString() + "%");
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while(reader.Read())
                        {
                            UGUI ret = reader.GetUGUI("User");
                            if(ret.EqualsGrid(agent))
                            {
                                allowed = reader.GetBool("IsAllowed");
                                return true;
                            }
                        }
                    }
                }
            }

            allowed = false;
            return false;
        }
    }
}

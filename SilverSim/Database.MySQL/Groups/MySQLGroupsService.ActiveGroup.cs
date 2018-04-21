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
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.Types;

namespace SilverSim.Database.MySQL.Groups
{
    partial class MySQLGroupsService : GroupsServiceInterface.IGroupSelectInterface
    {
        UGI IGroupSelectInterface.this[UGUI requestingAgent, UGUI principalID]
        {
            get
            {
                using (var conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("SELECT ActiveGroupID FROM activegroup WHERE PrincipalID = @principalid LIMIT 1", conn))
                    {
                        cmd.Parameters.AddParameter("@principalid", principalID.ID);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if(reader.Read())
                            {
                                return new UGI(reader.GetUUID("ActiveGroupID"));
                            }
                        }
                    }
                }
                return UGI.Unknown;
            }

            set
            {
                if(Members.ContainsKey(requestingAgent, value, principalID))
                {
                    using (var conn = new MySqlConnection(m_ConnectionString))
                    {
                        conn.Open();
                        using (var cmd = new MySqlCommand("INSERT INTO activegroup (PrincipalID, ActiveGroupID) VALUES (@principalid, @activegroupid) ON DUPLICATE KEY UPDATE ActiveGroupID=@activegroupid", conn))
                        {
                            cmd.Parameters.AddParameter("@principalid", principalID.ID);
                            cmd.Parameters.AddParameter("@activegroupid", value.ID);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        /* get/set active role id */
        UUID IGroupSelectInterface.this[UGUI requestingAgent, UGI group, UGUI principal]
        {
            get
            {
                UUID id;
                if(!ActiveGroup.TryGetValue(requestingAgent, group, principal, out id))
                {
                    id = UUID.Zero;
                }
                return id;
            }

            set
            {
                using (var conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("UPDATE groupmemberships SET SelectedRoleID=@roleid WHERE PrincipalID = @principalid AND GroupID = @groupid", conn))
                    {
                        cmd.Parameters.AddParameter("@roleid", value);
                        cmd.Parameters.AddParameter("@groupid", group.ID);
                        cmd.Parameters.AddParameter("@principalid", principal.ID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        bool IGroupSelectInterface.TryGetValue(UGUI requestingAgent, UGUI principalID, out UGI ugi)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT ActiveGroupID FROM activegroup WHERE PrincipalID = @principalid LIMIT 1", conn))
                {
                    cmd.Parameters.AddParameter("@principalid", principalID.ID);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ugi = new UGI(reader.GetUUID("ActiveGroupID"));
                            return true;
                        }
                    }
                }
            }

            ugi = UGI.Unknown;
            return false;
        }

        bool IGroupSelectInterface.TryGetValue(UGUI requestingAgent, UGI group, UGUI principal, out UUID id)
        {
            id = UUID.Zero;
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT SelectedRoleID FROM groupmemberships WHERE PrincipalID = @principalid AND GroupID = @groupid LIMIT 1", conn))
                {
                    cmd.Parameters.AddParameter("@groupid", group.ID);
                    cmd.Parameters.AddParameter("@principalid", principal.ID);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
                        {
                            id = reader.GetUUID("SelectedRoleID");
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}

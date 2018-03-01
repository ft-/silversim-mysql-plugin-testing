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
using SilverSim.Types.Groups;
using System.Collections.Generic;

namespace SilverSim.Database.MySQL.Groups
{
    partial class MySQLGroupsService : GroupsServiceInterface.IGroupInvitesInterface
    {
        List<GroupInvite> IGroupInvitesInterface.this[UUI requestingAgent, UUI principal]
        {
            get
            {
                using (var conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("SELECT * from groupinvites WHERE PrincipalID = @principalid", conn))
                    {
                        cmd.Parameters.AddParameter("@principalid", principal.ID);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            var invites = new List<GroupInvite>();
                            while(reader.Read())
                            {
                                GroupInvite invite = reader.ToGroupInvite();
                                invite.Principal = ResolveName(invite.Principal);
                                invite.Group = ResolveName(requestingAgent, invite.Group);
                                invites.Add(invite);
                            }
                            return invites;
                        }
                    }
                }
            }
        }

        GroupInvite IGroupInvitesInterface.this[UUI requestingAgent, UUID groupInviteID]
        {
            get
            {
                GroupInvite invite;
                if(!Invites.TryGetValue(requestingAgent, groupInviteID, out invite))
                {
                    throw new KeyNotFoundException();
                }
                return invite;
            }
        }

        bool IGroupInvitesInterface.DoesSupportListGetters => true;

        List<GroupInvite> IGroupInvitesInterface.this[UUI requestingAgent, UGI group, UUID roleID, UUI principal]
        {
            get
            {
                using (var conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("SELECT * from groupinvites WHERE PrincipalID = @principalid AND GroupID = @groupid AND RoleID = @roleid", conn))
                    {
                        cmd.Parameters.AddParameter("@principalid", principal.ID);
                        cmd.Parameters.AddParameter("@roleid", roleID);
                        cmd.Parameters.AddParameter("@groupid", group.ID);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            var invites = new List<GroupInvite>();
                            while (reader.Read())
                            {
                                GroupInvite invite = reader.ToGroupInvite();
                                invite.Principal = ResolveName(invite.Principal);
                                invite.Group = ResolveName(requestingAgent, invite.Group);
                                invites.Add(invite);
                            }
                            return invites;
                        }
                    }
                }
            }
        }

        void IGroupInvitesInterface.Add(UUI requestingAgent, GroupInvite invite)
        {
            var vals = new Dictionary<string, object>
            {
                ["InviteID"] = invite.ID,
                ["GroupID"] = invite.Group.ID,
                ["RoleID"] = invite.RoleID,
                ["PrincipalID"] = invite.Principal.ID,
                ["Timestamp"] = invite.Timestamp
            };
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                conn.InsertInto("groupinvites", vals);
            }
        }

        bool IGroupInvitesInterface.ContainsKey(UUI requestingAgent, UUID groupInviteID)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT InviteID from groupinvites WHERE InviteID = @inviteid LIMIT 1", conn))
                {
                    cmd.Parameters.AddParameter("@inviteid", groupInviteID);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        return reader.Read();
                    }
                }
            }
        }

        void IGroupInvitesInterface.Delete(UUI requestingAgent, UUID inviteID)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("DELETE FROM groupinvites WHERE InviteID = @inviteid", conn))
                {
                    cmd.Parameters.AddParameter("@inviteid", inviteID);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        List<GroupInvite> IGroupInvitesInterface.GetByGroup(UUI requestingAgent, UGI group)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT * from groupinvites WHERE GroupID = @groupid", conn))
                {
                    cmd.Parameters.AddParameter("@groupid", group.ID);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        var invites = new List<GroupInvite>();
                        while (reader.Read())
                        {
                            GroupInvite invite = reader.ToGroupInvite();
                            invite.Principal = ResolveName(invite.Principal);
                            invite.Group = ResolveName(requestingAgent, invite.Group);
                            invites.Add(invite);
                        }
                        return invites;
                    }
                }
            }
        }

        bool IGroupInvitesInterface.TryGetValue(UUI requestingAgent, UUID groupInviteID, out GroupInvite ginvite)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT * from groupinvites WHERE InviteID = @inviteid LIMIT 1", conn))
                {
                    cmd.Parameters.AddParameter("@inviteid", groupInviteID);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ginvite = reader.ToGroupInvite();
                            ginvite.Principal = ResolveName(ginvite.Principal);
                            ginvite.Group = ResolveName(requestingAgent, ginvite.Group);
                            return true;
                        }
                    }
                }
            }
            ginvite = null;
            return false;
        }
    }
}

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
using System;
using System.Collections.Generic;

namespace SilverSim.Database.MySQL.Groups
{
    partial class MySQLGroupsService : GroupsServiceInterface.IGroupsInterface
    {
        GroupInfo IGroupsInterface.this[UUI requestingAgent, UGI group]
        {
            get
            {
                GroupInfo groupInfo;
                if (!Groups.TryGetValue(requestingAgent, group, out groupInfo))
                {
                    throw new KeyNotFoundException();
                }
                return groupInfo;
            }
        }

        GroupInfo IGroupsInterface.this[UUI requestingAgent, string groupName]
        {
            get
            {
                GroupInfo groupInfo;
                if (!Groups.TryGetValue(requestingAgent, groupName, out groupInfo))
                {
                    throw new KeyNotFoundException();
                }
                return groupInfo;
            }
        }

        UGI IGroupsInterface.this[UUI requestingAgent, UUID groupID]
        {
            get
            {
                UGI ugi;
                if(!Groups.TryGetValue(requestingAgent, groupID, out ugi))
                {
                    throw new KeyNotFoundException();
                }
                return ugi;
            }
        }

        bool IGroupsInterface.ContainsKey(UUI requestingAgent, string groupName)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT GroupID FROM groups WHERE `Name` = @groupname", conn))
                {
                    cmd.Parameters.AddParameter("@groupname", groupName);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        return reader.Read();
                    }
                }
            }
        }

        bool IGroupsInterface.ContainsKey(UUI requestingAgent, UGI group) => Groups.ContainsKey(requestingAgent, group.ID);

        bool IGroupsInterface.ContainsKey(UUI requestingAgent, UUID groupID)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT GroupID FROM groups WHERE GroupID = @groupid", conn))
                {
                    cmd.Parameters.AddParameter("@groupid", groupID);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        return reader.Read();
                    }
                }
            }
        }

        GroupInfo IGroupsInterface.Create(UUI requestingAgent, GroupInfo group)
        {
            var vals = new Dictionary<string, object>
            {
                ["GroupID"] = group.ID.ID,
                ["Location"] = group.ID.HomeURI != null ? group.ID.HomeURI.ToString() : string.Empty,
                ["Name"] = group.ID.GroupName,
                ["Charter"] = group.Charter,
                ["InsigniaID"] = group.InsigniaID,
                ["FounderID"] = group.Founder.ID,
                ["MembershipFee"] = group.MembershipFee,
                ["OpenEnrollment"] = group.IsOpenEnrollment,
                ["ShowInList"] = group.IsShownInList,
                ["AllowPublish"] = group.IsAllowPublish,
                ["MaturePublish"] = group.IsMaturePublish,
                ["OwnerRoleID"] = group.OwnerRoleID
            };
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                conn.InsertInto("groups", vals);
            }
            return group;
        }

        void IGroupsInterface.Delete(UUI requestingAgent, UGI group)
        {
            var tablenames = new string[] { "grouproles", "grouprolememberships", "groupnotices", "groupmemberships", "groupinvites", "groups" };
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                conn.InsideTransaction((transaction) =>
                {
                    using (var cmd = new MySqlCommand("DELETE FROM activegroup WHERE ActiveGroupID = @groupid", conn)
                    {
                        Transaction = transaction
                    })
                    {
                        cmd.Parameters.AddParameter("@groupid", group.ID);
                        cmd.ExecuteNonQuery();
                    }
                    foreach (string table in tablenames)
                    {
                        using (var cmd = new MySqlCommand("DELETE FROM " + table + " WHERE GroupID = @groupid", conn)
                        {
                            Transaction = transaction
                        })
                        {
                            cmd.Parameters.AddParameter("@groupid", group.ID);
                            cmd.ExecuteNonQuery();
                        }
                    }
                });
            }
        }

        List<DirGroupInfo> IGroupsInterface.GetGroupsByName(UUI requestingAgent, string query)
        {
            var groups = new List<DirGroupInfo>();
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT g.GroupID, g.`Name`, g.Location, " + GCountQuery + " FROM groups AS g WHERE g.Name LIKE @value", conn))
                {
                    cmd.Parameters.AddParameter("@value", "%" + query + "%");
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while(reader.Read())
                        {
                            DirGroupInfo info = new DirGroupInfo();
                            info.ID.ID = reader.GetUUID("GroupID");
                            info.ID.GroupName = reader.GetString("Name");
                            string uri = reader.GetString("Location");
                            if (!string.IsNullOrEmpty(uri))
                            {
                                info.ID.HomeURI = new Uri(uri, UriKind.Absolute);
                            }
                            info.MemberCount = reader.GetInt32("MemberCount");
                            groups.Add(info);
                        }
                    }
                }
            }
            return groups;
        }

        bool IGroupsInterface.TryGetValue(UUI requestingAgent, string groupName, out GroupInfo groupInfo)
        {
            groupInfo = null;
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT g.*, " + GCountQuery + " FROM groups AS g WHERE g.`Name` = @groupname", conn))
                {
                    cmd.Parameters.AddParameter("@groupname", groupName);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            groupInfo = reader.ToGroupInfo();
                            groupInfo.Founder = ResolveName(groupInfo.Founder);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        bool IGroupsInterface.TryGetValue(UUI requestingAgent, UGI group, out GroupInfo groupInfo)
        {
            groupInfo = null;
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT g.*, " + GCountQuery + " FROM groups AS g WHERE g.GroupID = @groupid", conn))
                {
                    cmd.Parameters.AddParameter("@groupid", group.ID);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            groupInfo = reader.ToGroupInfo();
                            groupInfo.Founder = ResolveName(groupInfo.Founder);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        bool IGroupsInterface.TryGetValue(UUI requestingAgent, UUID groupID, out UGI ugi)
        {
            ugi = default(UGI);
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT `Name`, Location FROM groups WHERE GroupID = @groupid", conn))
                {
                    cmd.Parameters.AddParameter("@groupid", groupID);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ugi = new UGI
                            {
                                ID = groupID
                            };
                            string uri = reader.GetString("Location");
                            if(!string.IsNullOrEmpty(uri))
                            {
                                ugi.HomeURI = new Uri(uri, UriKind.Absolute);
                            }
                            ugi.GroupName = reader.GetString("Name");
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        GroupInfo IGroupsInterface.Update(UUI requestingAgent, GroupInfo group)
        {
            var vals = new Dictionary<string, object>
            {
                ["Charter"] = group.Charter,
                ["InsigniaID"] = group.InsigniaID,
                ["FounderID"] = group.Founder.ID,
                ["MembershipFee"] = group.MembershipFee,
                ["OpenEnrollment"] = group.IsOpenEnrollment,
                ["ShowInList"] = group.IsShownInList,
                ["AllowPublish"] = group.IsAllowPublish,
                ["MaturePublish"] = group.IsMaturePublish,
                ["OwnerRoleID"] = group.OwnerRoleID
            };
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                conn.UpdateSet("groups", vals, "GroupID = \"" + group.ID.ID.ToString() + "\"");
            }
            return group;
        }
    }
}

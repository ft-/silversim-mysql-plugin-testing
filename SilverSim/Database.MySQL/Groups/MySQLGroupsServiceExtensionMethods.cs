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
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Groups;
using System;

namespace SilverSim.Database.MySQL.Groups
{
    public static class MySQLGroupsServiceExtensionMethods
    {
        public static GroupInfo ToGroupInfo(this MySqlDataReader reader, string memberCount = "MemberCount")
        {
            var info = new GroupInfo();
            info.ID.ID = reader.GetUUID("GroupID");
            string uri = reader.GetString("Location");
            if (!string.IsNullOrEmpty(uri))
            {
                info.ID.HomeURI = new Uri(uri, UriKind.Absolute);
            }
            info.ID.GroupName = reader.GetString("Name");
            info.Charter = reader.GetString("Charter");
            info.InsigniaID = reader.GetUUID("InsigniaID");
            info.Founder.ID = reader.GetUUID("FounderID");
            info.MembershipFee = reader.GetInt32("MembershipFee");
            info.IsOpenEnrollment = reader.GetBool("OpenEnrollment");
            info.IsShownInList = reader.GetBool("ShowInList");
            info.IsAllowPublish = reader.GetBool("AllowPublish");
            info.IsMaturePublish = reader.GetBool("MaturePublish");
            info.OwnerRoleID = reader.GetUUID("OwnerRoleID");
            info.MemberCount = reader.GetInt32(memberCount);
            info.RoleCount = reader.GetInt32("RoleCount");

            return info;
        }

        public static GroupRole ToGroupRole(this MySqlDataReader reader, string prefix = "")
        {
            var role = new GroupRole
            {
                Group = new UGI(reader.GetUUID("GroupID")),
                ID = reader.GetUUID("RoleID"),
                Name = reader.GetString(prefix + "Name"),
                Description = reader.GetString(prefix + "Description"),
                Title = reader.GetString(prefix + "Title"),
                Powers = reader.GetEnum<GroupPowers>(prefix + "Powers")
            };
            if (role.ID == UUID.Zero)
            {
                role.Members = reader.GetUInt32("GroupMembers");
            }
            else
            {
                role.Members = reader.GetUInt32("RoleMembers");
            }

            return role;
        }

        public static GroupMember ToGroupMember(this MySqlDataReader reader) => new GroupMember
        {
            Group = new UGI(reader.GetUUID("GroupID")),
            Principal = reader.GetUGUI("PrincipalID"),
            SelectedRoleID = reader.GetUUID("SelectedRoleID"),
            Contribution = reader.GetInt32("Contribution"),
            IsListInProfile = reader.GetBool("ListInProfile"),
            IsAcceptNotices = reader.GetBool("AcceptNotices"),
            AccessToken = reader.GetString("AccessToken")
        };

        public static GroupRolemember ToGroupRolemember(this MySqlDataReader reader) => new GroupRolemember
        {
            Group = new UGI(reader.GetUUID("GroupID")),
            RoleID = reader.GetUUID("RoleID"),
            Principal = reader.GetUGUI("PrincipalID"),
            Powers = reader.GetEnum<GroupPowers>("Powers")
        };

        public static GroupRolemember ToGroupRolememberEveryone(this MySqlDataReader reader, GroupPowers powers) => new GroupRolemember
        {
            Group = new UGI(reader.GetUUID("GroupID")),
            RoleID = UUID.Zero,
            Principal = reader.GetUGUI("PrincipalID"),
            Powers = powers
        };

        public static GroupRolemembership ToGroupRolemembership(this MySqlDataReader reader) => new GroupRolemembership
        {
            Group = new UGI(reader.GetUUID("GroupID")),
            RoleID = reader.GetUUID("RoleID"),
            Principal = reader.GetUGUI("PrincipalID"),
            Powers = reader.GetEnum<GroupPowers>("Powers"),
            GroupTitle = reader.GetString("Title")
        };

        public static GroupRolemembership ToGroupRolemembershipEveryone(this MySqlDataReader reader, GroupPowers powers) => new GroupRolemembership
        {
            Group = new UGI(reader.GetUUID("GroupID")),
            RoleID = UUID.Zero,
            Principal = reader.GetUGUI("PrincipalID"),
            Powers = powers
        };

        public static GroupInvite ToGroupInvite(this MySqlDataReader reader) => new GroupInvite
        {
            ID = reader.GetUUID("InviteID"),
            Group = new UGI(reader.GetUUID("GroupID")),
            RoleID = reader.GetUUID("RoleID"),
            Principal = reader.GetUGUI("PrincipalID"),
            Timestamp = reader.GetDate("Timestamp")
        };

        public static GroupNotice ToGroupNotice(this MySqlDataReader reader) => new GroupNotice
        {
            Group = new UGI(reader.GetUUID("GroupID")),
            ID = reader.GetUUID("NoticeID"),
            Timestamp = reader.GetDate("Timestamp"),
            FromName = reader.GetString("FromName"),
            Subject = reader.GetString("Subject"),
            Message = reader.GetString("Message"),
            HasAttachment = reader.GetBool("HasAttachment"),
            AttachmentType = reader.GetEnum<AssetType>("AttachmentType"),
            AttachmentName = reader.GetString("AttachmentName"),
            AttachmentItemID = reader.GetUUID("AttachmentItemID"),
            AttachmentOwner = reader.GetUGUI("AttachmentOwnerID")
        };
    }
}

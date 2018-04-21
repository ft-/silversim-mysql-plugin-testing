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
using SilverSim.Types;
using SilverSim.Types.GridUser;

namespace SilverSim.Database.MySQL.GridUser
{
    public static class MySQLGridUserExtensionMethods
    {
        public static GridUserInfo ToGridUser(this MySqlDataReader reader) => new GridUserInfo
        {
            User = reader.GetUGUI("ID"),
            HomeRegionID = reader.GetUUID("HomeRegionID"),
            HomeLookAt = reader.GetVector3("HomeLookAt"),
            HomePosition = reader.GetVector3("HomePosition"),
            LastRegionID = reader.GetUUID("LastRegionID"),
            LastLookAt = reader.GetVector3("LastLookAt"),
            LastPosition = reader.GetVector3("LastPosition"),
            IsOnline = reader.GetBool("IsOnline"),
            LastLogin = reader.GetDate("LastLogin"),
            LastLogout = reader.GetDate("LastLogout")
        };
    }
}

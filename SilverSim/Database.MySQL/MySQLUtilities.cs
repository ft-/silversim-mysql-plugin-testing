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

using log4net;
using MySql.Data.MySqlClient;
using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Scene.Types.SceneEnvironment;
using SilverSim.Types;
using SilverSim.Types.StructuredData.Llsd;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace SilverSim.Database.MySQL
{
    public static class MySQLUtilities
    {
        #region Connection String Creator
        public static string BuildConnectionString(IConfig config, ILog log)
        {
            var sb = new MySqlConnectionStringBuilder();
            if (!(config.Contains("Username") && config.Contains("Password") && config.Contains("Database")))
            {
                string configName = config.Name;
                if (!config.Contains("Username"))
                {
                    log.FatalFormat("[MYSQL CONFIG]: Parameter 'Username' missing in [{0}]", configName);
                }
                if (!config.Contains("Password"))
                {
                    log.FatalFormat("[MYSQL CONFIG]: Parameter 'Password' missing in [{0}]", configName);
                }
                if (!config.Contains("Database"))
                {
                    log.FatalFormat("[MYSQL CONFIG]: Parameter 'Database' missing in [{0}]", configName);
                }
                throw new ConfigurationLoader.ConfigurationErrorException();
            }

            if (config.Contains("Protocol"))
            {
                sb.ConnectionProtocol = (MySqlConnectionProtocol)Enum.Parse(typeof(MySqlConnectionProtocol), config.GetString("Protocol"));
            }

            if (config.Contains("SslMode"))
            {
                sb.SslMode = (MySqlSslMode)Enum.Parse(typeof(MySqlSslMode), config.GetString("SslMode"));
            }

            if (config.Contains("UseCompression"))
            {
                sb.UseCompression = config.GetBoolean("UseCompression");
            }

            if(config.Contains("PipeName"))
            {
                sb.PipeName = config.GetString("PipeName");
            }

            if (config.Contains("SharedMemoryName"))
            {
                sb.SharedMemoryName = config.GetString("SharedMemoryName");
            }

            if(config.Contains("Server"))
            {
                sb.Server = config.GetString("Server");
            }

            sb.UserID = config.GetString("Username");
            sb.Password = config.GetString("Password");
            sb.Database = config.GetString("Database");

            if(config.Contains("Port"))
            {
                sb.Port = (uint)config.GetInt("Port");
            }

            if(config.Contains("MaximumPoolsize"))
            {
                sb.MaximumPoolSize = (uint)config.GetInt("MaximumPoolsize");
            }

            return sb.ToString();
        }
        #endregion

        #region Exceptions
        [Serializable]
        public class MySQLInsertException : Exception
        {
            public MySQLInsertException()
            {
            }

            public MySQLInsertException(string msg)
                : base(msg)
            {
            }

            protected MySQLInsertException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }

            public MySQLInsertException(string msg, Exception innerException)
                : base(msg, innerException)
            {
            }
        }

        [Serializable]
        public class MySQLMigrationException : Exception
        {
            public MySQLMigrationException()
            {
            }

            public MySQLMigrationException(string msg)
                : base(msg)
            {
            }

            protected MySQLMigrationException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }

            public MySQLMigrationException(string msg, Exception innerException)
                : base(msg, innerException)
            {
            }
        }

        [Serializable]
        public class MySQLTransactionException : Exception
        {
            public MySQLTransactionException()
            {
            }

            public MySQLTransactionException(string msg)
                : base(msg)
            {
            }

            protected MySQLTransactionException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }

            public MySQLTransactionException(string msg, Exception inner)
                : base(msg, inner)
            {
            }
        }
        #endregion

        #region Transaction Helper
        public static void InsideTransaction(this MySqlConnection connection, Action del)
        {
            using (var cmd = new MySqlCommand("BEGIN", connection))
            {
                cmd.ExecuteNonQuery();
            }
            try
            {
                del();
            }
            catch(Exception e)
            {
                using (var cmd = new MySqlCommand("ROLLBACK", connection))
                {
                    cmd.ExecuteNonQuery();
                }
                throw new MySQLTransactionException("Transaction failed", e);
            }
            using (var cmd = new MySqlCommand("COMMIT", connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        public static T InsideTransaction<T>(this MySqlConnection connection, Func<T> del)
        {
            T res;
            using (var cmd = new MySqlCommand("BEGIN", connection))
            {
                cmd.ExecuteNonQuery();
            }
            try
            {
                res = del();
            }
            catch (Exception e)
            {
                using (var cmd = new MySqlCommand("ROLLBACK", connection))
                {
                    cmd.ExecuteNonQuery();
                }
                throw new MySQLTransactionException("Transaction failed", e);
            }
            using (var cmd = new MySqlCommand("COMMIT", connection))
            {
                cmd.ExecuteNonQuery();
            }
            return res;
        }
        #endregion

        public static int GetMaxAllowedPacketSize(this MySqlConnection conn)
        {
            using (var cmd = new MySqlCommand("SELECT @@max_allowed_packet", conn))
            {
                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    if(!reader.Read())
                    {
                        throw new InvalidOperationException();
                    }
                    return reader.GetInt32(0);
                }
            }
        }

        #region Push parameters
        public static void AddParameter(this MySqlParameterCollection mysqlparam, string key, object value)
        {
            var t = value?.GetType();
            if (t == typeof(Vector3))
            {
                var v = (Vector3)value;
                mysqlparam.AddWithValue(key + "X", v.X);
                mysqlparam.AddWithValue(key + "Y", v.Y);
                mysqlparam.AddWithValue(key + "Z", v.Z);
            }
            else if (t == typeof(GridVector))
            {
                var v = (GridVector)value;
                mysqlparam.AddWithValue(key + "X", v.X);
                mysqlparam.AddWithValue(key + "Y", v.Y);
            }
            else if (t == typeof(Quaternion))
            {
                var v = (Quaternion)value;
                mysqlparam.AddWithValue(key + "X", v.X);
                mysqlparam.AddWithValue(key + "Y", v.Y);
                mysqlparam.AddWithValue(key + "Z", v.Z);
                mysqlparam.AddWithValue(key + "W", v.W);
            }
            else if (t == typeof(Color))
            {
                var v = (Color)value;
                mysqlparam.AddWithValue(key + "Red", v.R);
                mysqlparam.AddWithValue(key + "Green", v.G);
                mysqlparam.AddWithValue(key + "Blue", v.B);
            }
            else if (t == typeof(ColorAlpha))
            {
                var v = (ColorAlpha)value;
                mysqlparam.AddWithValue(key + "Red", v.R);
                mysqlparam.AddWithValue(key + "Green", v.G);
                mysqlparam.AddWithValue(key + "Blue", v.B);
                mysqlparam.AddWithValue(key + "Alpha", v.A);
            }
            else if (t == typeof(EnvironmentController.WLVector2))
            {
                var vec = (EnvironmentController.WLVector2)value;
                mysqlparam.AddWithValue(key + "X", vec.X);
                mysqlparam.AddWithValue(key + "Y", vec.Y);
            }
            else if (t == typeof(EnvironmentController.WLVector4))
            {
                var vec = (EnvironmentController.WLVector4)value;
                mysqlparam.AddWithValue(key + "Red", vec.X);
                mysqlparam.AddWithValue(key + "Green", vec.Y);
                mysqlparam.AddWithValue(key + "Blue", vec.Z);
                mysqlparam.AddWithValue(key + "Value", vec.W);
            }
            else if (t == typeof(bool))
            {
                mysqlparam.AddWithValue(key, (bool)value ? 1 : 0);
            }
            else if (t == typeof(UUID) || t == typeof(UUI) || t == typeof(UGI) || t == typeof(Uri))
            {
                mysqlparam.AddWithValue(key, value.ToString());
            }
            else if(t == typeof(ParcelID))
            {
                UUID i = new UUID(((ParcelID)value).GetBytes(), 0);
                mysqlparam.AddWithValue(key, i.ToString());
            }
            else if (t == typeof(AnArray))
            {
                using (var stream = new MemoryStream())
                {
                    LlsdBinary.Serialize((AnArray)value, stream);
                    mysqlparam.AddWithValue(key, stream.ToArray());
                }
            }
            else if (t == typeof(Date))
            {
                mysqlparam.AddWithValue(key, ((Date)value).AsULong);
            }
            else if (t.IsEnum)
            {
                mysqlparam.AddWithValue(key, Convert.ChangeType(value, t.GetEnumUnderlyingType()));
            }
            else
            {
                mysqlparam.AddWithValue(key, value);
            }
        }

        private static void AddParameters(this MySqlParameterCollection mysqlparam, Dictionary<string, object> vals)
        {
            foreach (KeyValuePair<string, object> kvp in vals)
            {
                if (kvp.Value != null)
                {
                    AddParameter(mysqlparam, "@v_" + kvp.Key, kvp.Value);
                }
            }
        }
        #endregion

        #region Common REPLACE INTO/INSERT INTO helper
        public static void AnyInto(this MySqlConnection connection, string cmd, string tablename, Dictionary<string, object> vals)
        {
            var q = new List<string>();
            foreach (KeyValuePair<string, object> kvp in vals)
            {
                object value = kvp.Value;

                var t = value?.GetType();
                string key = kvp.Key;

                if (t == typeof(Vector3))
                {
                    q.Add(key + "X");
                    q.Add(key + "Y");
                    q.Add(key + "Z");
                }
                else if (t == typeof(GridVector) || t == typeof(EnvironmentController.WLVector2))
                {
                    q.Add(key + "X");
                    q.Add(key + "Y");
                }
                else if (t == typeof(Quaternion))
                {
                    q.Add(key + "X");
                    q.Add(key + "Y");
                    q.Add(key + "Z");
                    q.Add(key + "W");
                }
                else if (t == typeof(Color))
                {
                    q.Add(key + "Red");
                    q.Add(key + "Green");
                    q.Add(key + "Blue");
                }
                else if(t == typeof(EnvironmentController.WLVector4))
                {
                    q.Add(key + "Red");
                    q.Add(key + "Green");
                    q.Add(key + "Blue");
                    q.Add(key + "Value");
                }
                else if (t == typeof(ColorAlpha))
                {
                    q.Add(key + "Red");
                    q.Add(key + "Green");
                    q.Add(key + "Blue");
                    q.Add(key + "Alpha");
                }
                else if (value == null)
                {
                    /* skip */
                }
                else
                {
                    q.Add(key);
                }
            }

            var q1 = new StringBuilder();
            var q2 = new StringBuilder();
            q1.Append(cmd);
            q1.Append(" INTO `");
            q1.Append(tablename);
            q1.Append("` (");
            q2.Append(") VALUES (");
            bool first = true;
            foreach(string p in q)
            {
                if(!first)
                {
                    q1.Append(",");
                    q2.Append(",");
                }
                first = false;
                q1.Append("`");
                q1.Append(p);
                q1.Append("`");
                q2.Append("@v_");
                q2.Append(p);
            }
            q1.Append(q2);
            q1.Append(")");
            using (var command = new MySqlCommand(q1.ToString(), connection))
            {
                AddParameters(command.Parameters, vals);
                if (command.ExecuteNonQuery() < 1)
                {
                    throw new MySQLInsertException();
                }
            }
        }
        #endregion

        #region Generate values
        public static string GenerateFieldNames(Dictionary<string, object> vals)
        {
            var q = new List<string>();
            foreach (KeyValuePair<string, object> kvp in vals)
            {
                object value = kvp.Value;

                var t = value?.GetType();
                string key = kvp.Key;

                if (t == typeof(Vector3))
                {
                    q.Add(key + "X");
                    q.Add(key + "Y");
                    q.Add(key + "Z");
                }
                else if (t == typeof(GridVector) || t == typeof(EnvironmentController.WLVector2))
                {
                    q.Add(key + "X");
                    q.Add(key + "Y");
                }
                else if (t == typeof(Quaternion))
                {
                    q.Add(key + "X");
                    q.Add(key + "Y");
                    q.Add(key + "Z");
                    q.Add(key + "W");
                }
                else if (t == typeof(Color))
                {
                    q.Add(key + "Red");
                    q.Add(key + "Green");
                    q.Add(key + "Blue");
                }
                else if (t == typeof(EnvironmentController.WLVector4))
                {
                    q.Add(key + "Red");
                    q.Add(key + "Green");
                    q.Add(key + "Blue");
                    q.Add(key + "Value");
                }
                else if (t == typeof(ColorAlpha))
                {
                    q.Add(key + "Red");
                    q.Add(key + "Green");
                    q.Add(key + "Blue");
                    q.Add(key + "Alpha");
                }
                else
                {
                    q.Add(key);
                }
            }

            var q1 = new StringBuilder();
            foreach (string p in q)
            {
                if (q1.Length != 0)
                {
                    q1.Append(",");
                }
                q1.Append("`");
                q1.Append(p);
                q1.Append("`");
            }
            return q1.ToString();
        }

        public static string GenerateValues(Dictionary<string, object> vals)
        {
            var resvals = new List<string>();

            foreach (object value in vals.Values)
            {
                var t = value?.GetType();
                if (t == typeof(Vector3))
                {
                    var v = (Vector3)value;
                    resvals.Add(v.X.ToString(CultureInfo.InvariantCulture));
                    resvals.Add(v.Y.ToString(CultureInfo.InvariantCulture));
                    resvals.Add(v.Z.ToString(CultureInfo.InvariantCulture));
                }
                else if (t == typeof(GridVector))
                {
                    var v = (GridVector)value;
                    resvals.Add(v.X.ToString(CultureInfo.InvariantCulture));
                    resvals.Add(v.Y.ToString(CultureInfo.InvariantCulture));
                }
                else if (t == typeof(Quaternion))
                {
                    var v = (Quaternion)value;
                    resvals.Add(v.X.ToString(CultureInfo.InvariantCulture));
                    resvals.Add(v.Y.ToString(CultureInfo.InvariantCulture));
                    resvals.Add(v.Z.ToString(CultureInfo.InvariantCulture));
                    resvals.Add(v.W.ToString(CultureInfo.InvariantCulture));
                }
                else if (t == typeof(Color))
                {
                    var v = (Color)value;
                    resvals.Add(v.R.ToString(CultureInfo.InvariantCulture));
                    resvals.Add(v.G.ToString(CultureInfo.InvariantCulture));
                    resvals.Add(v.B.ToString(CultureInfo.InvariantCulture));
                }
                else if (t == typeof(ColorAlpha))
                {
                    var v = (ColorAlpha)value;
                    resvals.Add(v.R.ToString(CultureInfo.InvariantCulture));
                    resvals.Add(v.G.ToString(CultureInfo.InvariantCulture));
                    resvals.Add(v.B.ToString(CultureInfo.InvariantCulture));
                    resvals.Add(v.A.ToString(CultureInfo.InvariantCulture));
                }
                else if (t == typeof(EnvironmentController.WLVector2))
                {
                    var v = (EnvironmentController.WLVector2)value;
                    resvals.Add(v.X.ToString(CultureInfo.InvariantCulture));
                    resvals.Add(v.Y.ToString(CultureInfo.InvariantCulture));
                }
                else if (t == typeof(EnvironmentController.WLVector4))
                {
                    var v = (EnvironmentController.WLVector4)value;
                    resvals.Add(v.X.ToString(CultureInfo.InvariantCulture));
                    resvals.Add(v.Y.ToString(CultureInfo.InvariantCulture));
                    resvals.Add(v.Z.ToString(CultureInfo.InvariantCulture));
                    resvals.Add(v.W.ToString(CultureInfo.InvariantCulture));
                }
                else if (t == typeof(bool))
                {
                    resvals.Add((bool)value ? "1" : "0");
                }
                else if (t == typeof(UUID) || t == typeof(UUI) || t == typeof(UGI) || t == typeof(Uri) || t == typeof(string))
                {
                    resvals.Add("'" + MySqlHelper.EscapeString(value.ToString()) + "'");
                }
                else if (t == typeof(AnArray))
                {
                    using (var stream = new MemoryStream())
                    {
                        LlsdBinary.Serialize((AnArray)value, stream);
                        byte[] b = stream.ToArray();
                        resvals.Add(b.Length == 0 ? "''" : "0x" + b.ToHexString());
                    }
                }
                else if(t == typeof(byte[]))
                {
                    var b = (byte[])value;
                    resvals.Add(b.Length == 0 ? "''" : "0x" + b.ToHexString());
                }
                else if (t == typeof(Date))
                {
                    resvals.Add(((Date)value).AsULong.ToString());
                }
                else if (t == typeof(float))
                {
                    resvals.Add(((float)value).ToString(CultureInfo.InvariantCulture));
                }
                else if (t == typeof(double))
                {
                    resvals.Add(((double)value).ToString(CultureInfo.InvariantCulture));
                }
                else if (value == null)
                {
                    resvals.Add("NULL");
                }
                else if (t.IsEnum)
                {
                    resvals.Add(Convert.ChangeType(value, t.GetEnumUnderlyingType()).ToString());
                }
                else
                {
                    resvals.Add(value.ToString());
                }
            }
            return string.Join(",", resvals);
        }
        #endregion

        #region REPLACE INSERT INTO helper
        public static void ReplaceInto(this MySqlConnection connection, string tablename, Dictionary<string, object> vals)
        {
            connection.AnyInto("REPLACE", tablename, vals);
        }
        #endregion

        #region INSERT INTO helper
        public static void InsertInto(this MySqlConnection connection, string tablename, Dictionary<string, object> vals)
        {
            connection.AnyInto("INSERT", tablename, vals);
        }
        #endregion

        #region UPDATE SET helper
        private static List<string> UpdateSetFromVals(Dictionary<string, object> vals)
        {
            var updates = new List<string>();

            foreach (KeyValuePair<string, object> kvp in vals)
            {
                object value = kvp.Value;
                var t = value?.GetType();
                string key = kvp.Key;

                if (t == typeof(Vector3))
                {
                    updates.Add("`" + key + "X` = @v_" + key + "X");
                    updates.Add("`" + key + "Y` = @v_" + key + "Y");
                    updates.Add("`" + key + "Z` = @v_" + key + "Z");
                }
                else if (t == typeof(GridVector) || t == typeof(EnvironmentController.WLVector2))
                {
                    updates.Add("`" + key + "X` = @v_" + key + "X");
                    updates.Add("`" + key + "Y` = @v_" + key + "Y");
                }
                else if (t == typeof(Quaternion))
                {
                    updates.Add("`" + key + "X` = @v_" + key + "X");
                    updates.Add("`" + key + "Y` = @v_" + key + "Y");
                    updates.Add("`" + key + "Z` = @v_" + key + "Z");
                    updates.Add("`" + key + "W` = @v_" + key + "W");
                }
                else if (t == typeof(Color))
                {
                    updates.Add("`" + key + "Red` = @v_" + key + "Red");
                    updates.Add("`" + key + "Green` = @v_" + key + "Green");
                    updates.Add("`" + key + "Blue` = @v_" + key + "Blue");
                }
                else if (t == typeof(EnvironmentController.WLVector4))
                {
                    updates.Add("`" + key + "Red` = @v_" + key + "Red");
                    updates.Add("`" + key + "Green` = @v_" + key + "Green");
                    updates.Add("`" + key + "Blue` = @v_" + key + "Blue");
                    updates.Add("`" + key + "Value` = @v_" + key + "Value");
                }
                else if (t == typeof(ColorAlpha))
                {
                    updates.Add("`" + key + "Red` = @v_" + key + "Red");
                    updates.Add("`" + key + "Green` = @v_" + key + "Green");
                    updates.Add("`" + key + "Blue` = @v_" + key + "Blue");
                    updates.Add("`" + key + "Alpha` = @v_" + key + "Alpha");
                }
                else if (value == null)
                {
                    /* skip */
                }
                else
                {
                    updates.Add("`" + key + "` = @v_" + key);
                }
            }
            return updates;
        }

        public static void UpdateSet(this MySqlConnection connection, string tablename, Dictionary<string, object> vals, string where)
        {
            string q1 = "UPDATE " + tablename + " SET ";

            q1 += string.Join(",", UpdateSetFromVals(vals));

            using (var command = new MySqlCommand(q1 + " WHERE " + where, connection))
            {
                AddParameters(command.Parameters, vals);
                if (command.ExecuteNonQuery() < 1)
                {
                    throw new MySQLInsertException();
                }
            }
        }

        public static void UpdateSet(this MySqlConnection connection, string tablename, Dictionary<string, object> vals, Dictionary<string, object> where)
        {
            string q1 = "UPDATE " + tablename + " SET ";

            q1 += string.Join(",", UpdateSetFromVals(vals));

            var wherestr = new StringBuilder();
            foreach(KeyValuePair<string, object> w in where)
            {
                if(wherestr.Length != 0)
                {
                    wherestr.Append(" AND ");
                }
                wherestr.AppendFormat("{0} = @w_{0}", w.Key);
            }

            using (var command = new MySqlCommand(q1 + " WHERE " + wherestr, connection))
            {
                AddParameters(command.Parameters, vals);
                foreach(KeyValuePair<string, object> w in where)
                {
                    command.Parameters.AddWithValue("@w_" + w.Key, w.Value);
                }
                if (command.ExecuteNonQuery() < 1)
                {
                    throw new MySQLInsertException();
                }
            }
        }
        #endregion

        #region Data parsers
        public static EnvironmentController.WLVector4 GetWLVector4(this MySqlDataReader dbReader, string prefix) =>
            new EnvironmentController.WLVector4(
                (double)dbReader[prefix + "Red"],
                (double)dbReader[prefix + "Green"],
                (double)dbReader[prefix + "Blue"],
                (double)dbReader[prefix + "Value"]);

        public static T GetEnum<T>(this MySqlDataReader dbreader, string prefix)
        {
            var enumType = typeof(T).GetEnumUnderlyingType();
            object v = dbreader[prefix];
            return (T)Convert.ChangeType(v, enumType);
        }

        public static ParcelID GetParcelID(this MySqlDataReader dbReader, string prefix)
        {
            UUID id = GetUUID(dbReader, prefix);
            return new ParcelID(id.GetBytes(), 0);
        }

        public static UUID GetUUID(this MySqlDataReader dbReader, string prefix)
        {
            object v = dbReader[prefix];
            var t = v?.GetType();
            if(t == typeof(Guid))
            {
                return new UUID((Guid)v);
            }

            if(t == typeof(string))
            {
                return new UUID((string)v);
            }

            throw new InvalidCastException("GetUUID could not convert value for " + prefix);
        }

        public static UUI GetUUI(this MySqlDataReader dbReader, string prefix)
        {
            object v = dbReader[prefix];
            var t = v?.GetType();
            if (t == typeof(Guid))
            {
                return new UUI((Guid)v);
            }

            if (t == typeof(string))
            {
                return new UUI((string)v);
            }

            throw new InvalidCastException("GetUUI could not convert value for " + prefix);
        }

        public static UGI GetUGI(this MySqlDataReader dbReader, string prefix)
        {
            object v = dbReader[prefix];
            var t = v?.GetType();
            if (t == typeof(Guid))
            {
                return new UGI((Guid)v);
            }

            if (t == typeof(string))
            {
                return new UGI((string)v);
            }

            throw new InvalidCastException("GetUGI could not convert value for " + prefix);
        }

        public static Date GetDate(this MySqlDataReader dbReader, string prefix)
        {
            ulong v;
            if (!ulong.TryParse(dbReader[prefix].ToString(), out v))
            {
                throw new InvalidCastException("GetDate could not convert value for "+ prefix);
            }
            return Date.UnixTimeToDateTime(v);
        }

        public static EnvironmentController.WLVector2 GetWLVector2(this MySqlDataReader dbReader, string prefix) =>
            new EnvironmentController.WLVector2(
                (double)dbReader[prefix + "X"],
                (double)dbReader[prefix + "Y"]);

        public static Vector3 GetVector3(this MySqlDataReader dbReader, string prefix) =>
            new Vector3(
                (double)dbReader[prefix + "X"],
                (double)dbReader[prefix + "Y"],
                (double)dbReader[prefix + "Z"]);

        public static Quaternion GetQuaternion(this MySqlDataReader dbReader, string prefix) =>
            new Quaternion(
                (double)dbReader[prefix + "X"],
                (double)dbReader[prefix + "Y"],
                (double)dbReader[prefix + "Z"],
                (double)dbReader[prefix + "W"]);

        public static Color GetColor(this MySqlDataReader dbReader, string prefix) =>
            new Color(
                (double)dbReader[prefix + "Red"],
                (double)dbReader[prefix + "Green"],
                (double)dbReader[prefix + "Blue"]);

        public static ColorAlpha GetColorAlpha(this MySqlDataReader dbReader, string prefix) =>
            new ColorAlpha(
                (double)dbReader[prefix + "Red"],
                (double)dbReader[prefix + "Green"],
                (double)dbReader[prefix + "Blue"],
                (double)dbReader[prefix + "Alpha"]);

        public static bool GetBool(this MySqlDataReader dbReader, string prefix)
        {
            object o = dbReader[prefix];
            var t = o?.GetType();
            if(t == typeof(uint))
            {
                return (uint)o != 0;
            }
            else if(t == typeof(int))
            {
                return (int)o != 0;
            }
            else if(t == typeof(sbyte))
            {
                return (sbyte)o != 0;
            }
            else if (t == typeof(byte))
            {
                return (byte)o != 0;
            }
            else
            {
                throw new InvalidCastException("GetBoolean could not convert value for " + prefix + ": got type " + o.GetType().FullName);
            }
        }

        public static byte[] GetBytes(this MySqlDataReader dbReader, string prefix)
        {
            object o = dbReader[prefix];
            var t = o?.GetType();
            if(t == typeof(DBNull))
            {
                return new byte[0];
            }
            return (byte[])o;
        }

        public static Uri GetUri(this MySqlDataReader dbReader, string prefix)
        {
            object o = dbReader[prefix];
            var t = o?.GetType();
            if(t == typeof(DBNull))
            {
                return null;
            }
            var s = (string)o;
            if(s.Length == 0)
            {
                return null;
            }
            return new Uri(s);
        }

        public static GridVector GetGridVector(this MySqlDataReader dbReader, string prefix) =>
            new GridVector(dbReader.GetUInt32(prefix + "X"), dbReader.GetUInt32(prefix + "Y"));
        #endregion

        #region Migrations helper
        public static uint GetTableRevision(this MySqlConnection connection, string name)
        {
            using(var cmd = new MySqlCommand("SHOW TABLE STATUS WHERE name=@name", connection))
            {
                cmd.Parameters.AddWithValue("@name", name);
                using (MySqlDataReader dbReader = cmd.ExecuteReader())
                {
                    if (dbReader.Read())
                    {
                        uint u;
                        if(!uint.TryParse((string)dbReader["Comment"], out u))
                        {
                            throw new InvalidDataException("Comment is not a parseable number");
                        }
                        return u;
                    }
                }
            }
            return 0;
        }
        #endregion
    }
}

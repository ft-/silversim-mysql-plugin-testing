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
using System;
using System.Collections.Generic;
using System.Linq;
using MySQLMigrationException = SilverSim.Database.MySQL.MySQLUtilities.MySQLMigrationException;

namespace SilverSim.Database.MySQL._Migration
{
    public static class Migrator
    {
        static void ExecuteStatement(MySqlConnection conn, string command, ILog log)
        {
            try
            {
                using (var cmd = new MySqlCommand(command, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                log.Debug(command);
                throw;
            }
        }

        static void CreateTable(
            this MySqlConnection conn, 
            SqlTable table,
            ChangeEngine engine,
            PrimaryKeyInfo primaryKey,
            Dictionary<string, IColumnInfo> fields,
            Dictionary<string, NamedKeyInfo> tableKeys,
            uint tableRevision,
            ILog log)
        {
            log.InfoFormat("Creating table '{0}' at revision {1}", table.Name, tableRevision);
            var fieldSqls = new List<string>();
            foreach(IColumnInfo field in fields.Values)
            {
                fieldSqls.Add(field.FieldSql());
            }
            if(null != primaryKey)
            {
                fieldSqls.Add(primaryKey.FieldSql());
            }
            foreach(NamedKeyInfo key in tableKeys.Values)
            {
                fieldSqls.Add(key.FieldSql());
            }

            string cmd = "CREATE TABLE `" + MySqlHelper.EscapeString(table.Name) + "` (";
            cmd += string.Join(",", fieldSqls);
            cmd += ") COMMENT='" + tableRevision.ToString() + "' ENGINE=" + (engine != null ? engine.Engine : table.Engine);
            if(table.IsDynamicRowFormat)
            {
                cmd += " ROW_FORMAT=DYNAMIC";
            }
            cmd += " CHARACTER SET UTF8;";
            ExecuteStatement(conn, cmd, log);
        }

        public static void MigrateTables(this MySqlConnection conn, IMigrationElement[] processTable, ILog log)
        {
            var tableFields = new Dictionary<string, IColumnInfo>();
            PrimaryKeyInfo primaryKey = null;
            var tableKeys = new Dictionary<string, NamedKeyInfo>();
            SqlTable table = null;
            ChangeEngine selectedEngine = null;
            uint processingTableRevision = 0;
            uint currentAtRevision = 0;
            bool insideTransaction = false;

            if(processTable.Length == 0)
            {
                throw new MySQLMigrationException("Invalid MySQL migration");
            }

            if(null == processTable[0] as SqlTable)
            {
                throw new MySQLMigrationException("First entry must be table name");
            }

            foreach (IMigrationElement migration in processTable)
            {
                Type migrationType = migration.GetType();

                if (typeof(SqlTable) == migrationType)
                {
                    if(insideTransaction)
                    {
                        ExecuteStatement(conn, string.Format("ALTER TABLE {0} COMMENT='{1}';", table.Name, processingTableRevision), log);
                        ExecuteStatement(conn, "COMMIT", log);
                        insideTransaction = false;
                    }

                    if (null != table && 0 != processingTableRevision)
                    {
                        if(currentAtRevision == 0)
                        {
                            conn.CreateTable(
                                table,
                                selectedEngine,
                                primaryKey,
                                tableFields,
                                tableKeys,
                                processingTableRevision,
                                log);
                        }
                        tableKeys.Clear();
                        tableFields.Clear();
                        primaryKey = null;
                    }
                    table = (SqlTable)migration;
                    selectedEngine = null;
                    currentAtRevision = conn.GetTableRevision(table.Name);
                    processingTableRevision = 1;
                }
                else if (typeof(TableRevision) == migrationType)
                {
                    if (insideTransaction)
                    {
                        ExecuteStatement(conn, string.Format("ALTER TABLE {0} COMMENT='{1}';", table.Name, processingTableRevision), log);
                        ExecuteStatement(conn, "COMMIT", log);
                        insideTransaction = false;
                        if (currentAtRevision != 0)
                        {
                            currentAtRevision = processingTableRevision;
                        }
                    }

                    var rev = (TableRevision)migration;
                    if(rev.Revision != processingTableRevision + 1)
                    {
                        throw new MySQLMigrationException(string.Format("Invalid TableRevision entry. Expected {0}. Got {1}", processingTableRevision + 1, rev.Revision));
                    }

                    processingTableRevision = rev.Revision;

                    if (processingTableRevision - 1 == currentAtRevision && 0 != currentAtRevision)
                    {
                        ExecuteStatement(conn, "BEGIN", log);
                        insideTransaction = true;
                        log.InfoFormat("Migration table '{0}' to revision {1}", table.Name, processingTableRevision);
                    }
                }
                else if (processingTableRevision == 0 || table == null)
                {
                    if (table != null)
                    {
                        throw new MySQLMigrationException("Unexpected processing element for " + table.Name);
                    }
                    else
                    {
                        throw new MySQLMigrationException("Unexpected processing element");
                    }
                }
                else
                {
                    Type[] interfaces = migration.GetType().GetInterfaces();

                    if(interfaces.Contains(typeof(IAddColumn)))
                    {
                        var columnInfo = (IAddColumn)migration;
                        if(tableFields.ContainsKey(columnInfo.Name))
                        {
                            throw new ArgumentException("Column " + columnInfo.Name + " was added twice.");
                        }
                        tableFields.Add(columnInfo.Name, columnInfo);
                        if(insideTransaction)
                        {
                            ExecuteStatement(conn, columnInfo.Sql(table.Name), log);
                        }
                    }
                    else if(interfaces.Contains(typeof(IChangeColumn)))
                    {
                        var columnInfo = (IChangeColumn)migration;
                        IColumnInfo oldColumn;
                        if(!tableFields.TryGetValue(columnInfo.Name, out oldColumn))
                        {
                            throw new ArgumentException("Change column for " + columnInfo.Name + " has no preceeding AddColumn");
                        }
                        if(insideTransaction)
                        {
                            ExecuteStatement(conn, columnInfo.Sql(table.Name, oldColumn.FieldType), log);
                        }
                        tableFields[columnInfo.Name] = columnInfo;
                    }
                    else if(migrationType == typeof(DropColumn))
                    {
                        var columnInfo = (DropColumn)migration;
                        if (insideTransaction)
                        {
                            ExecuteStatement(conn, columnInfo.Sql(table.Name, tableFields[columnInfo.Name].FieldType), log);
                        }
                        tableFields.Remove(columnInfo.Name);
                    }
                    else if(migrationType == typeof(ChangeEngine))
                    {
                        var engineInfo = (ChangeEngine)migration;
                        if(insideTransaction)
                        {
                            ExecuteStatement(conn, engineInfo.Sql(table.Name), log);
                        }
                        selectedEngine = engineInfo;
                    }
                    else if(migrationType == typeof(PrimaryKeyInfo))
                    {
                        if(null != primaryKey && insideTransaction)
                        {
                            ExecuteStatement(conn, "ALTER TABLE `" + MySqlHelper.EscapeString(table.Name) + "` DROP PRIMARY KEY;", log);
                        }
                        primaryKey = (PrimaryKeyInfo)migration;
                        if (insideTransaction)
                        {
                            ExecuteStatement(conn, primaryKey.Sql(table.Name), log);
                        }
                    }
                    else if(migrationType == typeof(DropPrimaryKeyinfo))
                    {
                        if (null != primaryKey && insideTransaction)
                        {
                            ExecuteStatement(conn, ((DropPrimaryKeyinfo)migration).Sql(table.Name), log);
                        }
                        primaryKey = null;
                    }
                    else if(migrationType == typeof(NamedKeyInfo))
                    {
                        var namedKey = (NamedKeyInfo)migration;
                        tableKeys.Add(namedKey.Name, namedKey);
                        if (insideTransaction)
                        {
                            ExecuteStatement(conn, namedKey.Sql(table.Name), log);
                        }
                    }
                    else if(migrationType == typeof(DropNamedKeyInfo))
                    {
                        var namedKey = (DropNamedKeyInfo)migration;
                        tableKeys.Remove(namedKey.Name);
                        if (insideTransaction)
                        {
                            ExecuteStatement(conn, namedKey.Sql(table.Name), log);
                        }
                    }
                    else
                    {
                        throw new MySQLMigrationException("Invalid type " + migrationType.FullName + " in migration list");
                    }
                }
            }

            if (insideTransaction)
            {
                ExecuteStatement(conn, string.Format("ALTER TABLE {0} COMMENT='{1}';", table.Name, processingTableRevision), log);
                ExecuteStatement(conn, "COMMIT", log);
                if (currentAtRevision != 0)
                {
                    currentAtRevision = processingTableRevision;
                }
            }

            if (null != table && 0 != processingTableRevision && currentAtRevision == 0)
            {
                conn.CreateTable(
                    table,
                    selectedEngine,
                    primaryKey,
                    tableFields,
                    tableKeys,
                    processingTableRevision,
                    log);
            }
        }
    }
}

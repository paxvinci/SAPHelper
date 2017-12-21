using System;
using System.Data;
using System.IO;
using System.Data.SqlServerCe;
using System.Text;
namespace ManageCompositeRole
{
    class ManipulateSQLCE
    {
        public static string GetSQLTypeName(Type t)
        {

            if (t == typeof(SByte)) return "TinyInt";
            if (t == typeof(Int16)) return "SmallInt";
            if (t == typeof(Int32)) return "Int";
            if (t == typeof(Int64)) return "BigInt";
            if (t == typeof(Byte)) return "TinyInt";
            if (t == typeof(UInt16)) return "SmallInt";
            if (t == typeof(UInt32)) return "Int";
            if (t == typeof(UInt64)) return "BigInt";
            if (t == typeof(Single)) return "Real";
            if (t == typeof(Double)) return "Float";
            if (t == typeof(Decimal)) return "Money";
            if (t == typeof(Boolean)) return "Bit";
            if (t == typeof(Guid)) return "UniqueIdentifier";
            if (t == typeof(Byte[])) return "Image";
            if (t == typeof(string)) return "NText";
            if (t == typeof(DateTime)) return "DateTime";
            if (t == typeof(Char)) return "NChar";
            if (t == typeof(Byte[])) return "Binary";

            throw new Exception("Unable to get matching SQL CE type for type " + t.Name);
        }

        public static SqlDbType GetSQLType(Type t)
        {

            if (t == typeof(SByte)) return SqlDbType.TinyInt;
            if (t == typeof(Int16)) return SqlDbType.SmallInt;
            if (t == typeof(Int32)) return SqlDbType.Int;
            if (t == typeof(Int64)) return SqlDbType.BigInt;
            if (t == typeof(Byte)) return SqlDbType.TinyInt;
            if (t == typeof(UInt16)) return SqlDbType.SmallInt;
            if (t == typeof(UInt32)) return SqlDbType.Int;
            if (t == typeof(UInt64)) return SqlDbType.BigInt;
            if (t == typeof(Single)) return SqlDbType.Real;
            if (t == typeof(Double)) return SqlDbType.Float;
            if (t == typeof(Decimal)) return SqlDbType.Money;
            if (t == typeof(Boolean)) return SqlDbType.Bit;
            if (t == typeof(Guid)) return SqlDbType.UniqueIdentifier;
            if (t == typeof(Byte[])) return SqlDbType.Image;
            if (t == typeof(string)) return SqlDbType.NText;
            if (t == typeof(DateTime)) return SqlDbType.DateTime;
            if (t == typeof(Char)) return SqlDbType.NChar;
            if (t == typeof(Byte[])) return SqlDbType.Binary;

            throw new Exception("Unable to get matching SQL CE type for type " + t.Name);
        }

        // Store a dataset to the SQL CE.

        public static void storeDataToSQLCE(DataSet data, string dataFile)
        {
            if (File.Exists(dataFile))                              // Get rid of existing database file
                File.Delete(dataFile);

            SqlCeEngine en = new SqlCeEngine("Data Source = " + dataFile);
            // Create new database engine
            en.CreateDatabase();                                    // Create a new database

            SqlCeConnection con = new SqlCeConnection("Data Source = " + dataFile);
            // Create a new connection
            con.Open();                                             // Open this connection

            foreach (DataTable table in data.Tables)
            {             // Create tables one by one

                SqlCeCommand cmd = con.CreateCommand();             // Prepare Create table command

                StringBuilder command = new StringBuilder(1024);    // Assume we have pretty long command

                command.Append("Create Table \"");                  // Command string:
                command.Append(table.TableName);                  // Create Table "TableName" ("ColumnName" ColumnType, ... )
                command.Append("\" (");

                foreach (DataColumn c in table.Columns)
                {          // Add all columns

                    command.Append("\"");
                    command.Append(c.ColumnName);                  // Add column name
                    command.Append("\" ");
                    command.Append(GetSQLTypeName(c.DataType));    // And column SQL type

                    // Add special column features:
                    //
                    // Identity(_seed, _increment)  Autoincrement  Could not set this as column will be read only
                    // Primary Key                  Primary Key
                    // Default _value               Sets default value
                    // Unique                       For unique columns
                    // Not Null                     Null is not allowed


                    if (c.DefaultValue != DBNull.Value)
                    {         // Default value is set?
                        command.Append(String.Format(" Default '{0}'", c.DefaultValue));
                    }

                    if (!c.AllowDBNull)
                    {                         // Could not be null ?
                        command.Append(" Not Null");               // Mark it as Not Null
                    }

                    if (c.Unique)
                    {                               // Unique column ?
                        DataColumn[] pk = table.PrimaryKey;         // Get primry key column(s)

                        if (pk != null && pk.Length > 1)          // Only one column allowed as a primary key in SQL CE
                            throw new System.Exception("Only one column allowed as a primary key in SQL CE");

                        if (null != pk && pk.Length == 1 && pk[0] == c)
                        {

                            // Primary key ?
                            command.Append(" Primary Key");        // Mark it as such
                        }
                        else
                        {
                            command.Append(" Unique");             // Mark it as unique
                        }
                    }

                    command.Append(',');                           // Add separator
                }

                command.Replace(',', ')', command.Length - 1, 1);  // Replace last comma with ')'

                cmd.CommandText = command.ToString();               // Set command

                // Console.WriteLine ("Create command: \n{0}", cmd.CommandText);

                cmd.ExecuteNonQuery();                              // Do it - create a table

                cmd.Parameters.Clear();                             // Do some cleanup


                // At this point we have a database with an empty table ready to be populated...

                SqlCeDataAdapter da = new SqlCeDataAdapter();       // Prepare data adapter

                command.Remove(0, command.Length);                  // Clean up old command

                command.Append("Insert Into \"");                  // Insert command
                command.Append(table.TableName);
                command.Append("\" (");

                foreach (DataColumn column in table.Columns)
                {      // Add column names
                    command.Append('"');
                    command.Append(column.ColumnName);
                    command.Append("\",");                         // Separated by commas

                    cmd.Parameters.Add("@" + column.ColumnName,     // Add parameters to q query
                                        GetSQLType(column.DataType),
                                        column.MaxLength > 0 ?
column.MaxLength : 0,
                                        column.ColumnName);
                }

                command.Remove(command.Length - 1, 1);              // Remove last comma

                command.Append(") Values (");

                for (int i = table.Columns.Count; i-- > 0; )        // Add correct number 
                    command.Append("?,");                          // of queston marks

                command.Replace(',', ')', command.Length - 1, 1);  // Replace last comma with ')'

                cmd.CommandText = command.ToString();               // Set insert command

                da.InsertCommand = cmd;                             // Store our insert command


                da.Update(table);                                   // Update table in the SQL CE

                cmd.Parameters.Clear();                             // Do some cleanup

                //Verification
                Console.WriteLine("VERIFYING");
                #region VERIFICATION
                cmd = con.CreateCommand();             // Prepare Create table command
                string vcommand = String.Format("select COUNT(*) from {0}", table.TableName);
                cmd.CommandText = vcommand;               // Set command

                int count = (int)cmd.ExecuteScalar();                              // Do it - create a table
                if (count != table.Rows.Count)
                {
                    Console.WriteLine("We did not store Expected number of records");
                    Console.WriteLine("Expected was {0}", table.Rows.Count);
                    Console.WriteLine("Actual was {0}", count);
                    throw new Exception("Error in Verification");
                }
                #endregion
                Console.WriteLine("DONE WITH VERIFICATION");

            }

            con.Close();                                            // Close connection.
        }

        // Restore a dataset from the SQL CE

        public static DataSet restoreDataFromSQLCE(string dataFile)
        {
            DataSet data = new DataSet();                           // Create new dataset

            data.EnforceConstraints = false;

            SqlCeConnection con = new SqlCeConnection("Data Source = " + dataFile);
            // Create a new connection
            SqlCeDataReader rdr = null;
            try
            {
                con.Open();                                             // Open this connection

                // Now enumerate all user tables

                SqlCeCommand cmd = con.CreateCommand();                 // Prepare command

                cmd.CommandText = "Select table_type, table_name From Information_Schema.Tables";
                // Query table type and name
                rdr = cmd.ExecuteReader();              // Get data

                while (rdr.Read())                                       // Now read it
                {
                    if (rdr.GetString(0) == "TABLE")                  // Is it a user's table ?
                    {
                        string tableName = rdr.GetString(1);            // Get table name

                        SqlCeDataAdapter da = new SqlCeDataAdapter();   // Create data adapter to fill our DataSet

                        cmd.CommandType = CommandType.TableDirect;     // Use direct table access to load data

                        cmd.CommandText = tableName;                   // Set table name

                        da.SelectCommand = cmd;                         // Set command to be used with data adapter

                        DataTable table = new DataTable();             // Create DataTable to be filled

                        da.Fill(table);                                 // Fill table with data

                        table.TableName = tableName;                    // Rename table

                        data.Tables.Add(table);                         // Add table to the dataset
                    }
                }
                data.EnforceConstraints = true;
                return data;
            }
            finally
            {
                if (null != rdr)
                    rdr.Close();
                // We're done reading 
                if (null != con)
                    con.Close();
                // Close this connection
            }


        }

        public static void ShowErrors(SqlCeException e)
        {
            SqlCeErrorCollection errorCollection = e.Errors;

            StringBuilder bld = new StringBuilder();
            Exception inner = e.InnerException;

            foreach (SqlCeError err in errorCollection)
            {
                bld.Append("\n Error Code: " + err.HResult.ToString("X"));
                bld.Append("\n Message   : " + err.Message);
                bld.Append("\n Minor Err.: " + err.NativeError);
                bld.Append("\n Source    : " + err.Source);

                foreach (int numPar in err.NumericErrorParameters)
                {
                    if (0 != numPar) bld.Append("\n Num. Par. : " + numPar);
                }

                foreach (string errPar in err.ErrorParameters)
                {
                    if (String.Empty != errPar) bld.Append("\n Err. Par. : " + errPar);
                }

                Console.WriteLine(bld.ToString());
                bld.Remove(0, bld.Length);
            }
        }
    }
}
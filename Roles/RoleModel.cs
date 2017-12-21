using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlServerCe;

namespace ManageCompositeRole.Roles
{
    class RoleModel
    {
        private readonly String[] PRGN;

        private DataSet dsRoles;
        private ProxyParameter param;
        private SAPImportHelper helper;
        private SqlCeEngine db;
        private SqlCeConnection con;
        private SqlCeDataAdapter da;
        private const String fileSDF = "cache.sdf";

        public RoleModel(ProxyParameter param)
        {
            PRGN = new String[] { "AGR_DEFINE", "AGR_FLAGS", "AGR_AGRS", 
                                          "AGR_TCODES", "AGR_1250", "AGR_1251", 
                                          "AGR_1252", "AGR_1016", "AGR_USERS", 
                                          "AGR_TEXTS" };
            this.param = param;
            this.helper = new SAPImportHelper(this.param);
            db = new SqlCeEngine("Data Source=\"" + fileSDF + "\"");
            con = new SqlCeConnection(db.LocalConnectionString);
            
            dsRoles = new DataSet();
            da = new SqlCeDataAdapter();
            da.FillLoadOption = LoadOption.OverwriteChanges;
            da.Fill(dsRoles);
        }

        public DataSet RolesData
        {
            get { return dsRoles; }
        }
        

        private void ImportSAPTable(String tableName, DataTable options, String language)
        {
            // Define constant for insert into statement
            // Import (partial data of) table from SAP
            DataTable sapDataTable = helper.Import(tableName, options);

            // Define transaction to force checks of PK and indexes at least
            IEnumerable<String> arFields = from DataColumn c in sapDataTable.Columns
                                           select c.ColumnName;
            
            dsRoles.Tables.Add(sapDataTable);
        }

        public void Refresh()
        {
            con.Close();
            
            foreach (String prgn in PRGN)
            {
                ImportSAPTable(prgn, null, param.Language);
                //da = new SqlCeDataAdapter("select * from " + prgn, con);
                //if (!TableExists(con, prgn))
                //    using (SqlCeCommand cmd = con.CreateCommand())
                //    {
                //        cmd.CommandText = "CREATE TABLE " + prgn + "(MANDT NVARCHAR(4))";
                //        cmd.ExecuteNonQuery();
                //    }
                //da.MissingSchemaAction = MissingSchemaAction.Add;
                //da.MissingMappingAction = MissingMappingAction.Passthrough;
                //da.TableMappings.Add(prgn, prgn);
                //da.FillSchema(dsRoles, SchemaType.Source, prgn);
                //da.Update(dsRoles.Tables[prgn]);
            }
            ManipulateSQLCE.storeDataToSQLCE(this.dsRoles, "");
            
            con.Open();
        }

        private bool TableExists(SqlCeConnection connection, string tableName)
        {
            if (tableName == null) throw new ArgumentNullException("tableName");
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Invalid table name");
            if (connection == null) throw new ArgumentNullException("connection");
            if (connection.State != ConnectionState.Open)
            {
                throw new InvalidOperationException("TableExists requires an open and available Connection. The connection's current state is " + connection.State);
            }

            using (SqlCeCommand command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = "SELECT 1 FROM Information_Schema.Tables WHERE TABLE_NAME = @tableName";
                command.Parameters.AddWithValue("tableName", tableName);
                object result = command.ExecuteScalar();
                return result != null;
            }
        }
    }
}

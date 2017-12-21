using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Globalization;
using SAP.Middleware.Connector;
using System.Data;
using System.Threading;

namespace ManageCompositeRole
{
    class SAPTypeConverter : TypeConverter
    {
        // Overrides the ConvertTo method of TypeConverter.
        public override object ConvertTo(ITypeDescriptorContext context,
            CultureInfo culture,
            object value,
            Type destinationType)
        {
            if (destinationType == typeof(DateTime))
            {
                DateTime result;
                if (DateTime.TryParseExact(value.ToString().Trim(),
                    new String[] { "yyyyMMdd", "HHmmss" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal | DateTimeStyles.NoCurrentDateDefault,
                    out result))
                    return result;
                return result;
            }
            if (destinationType == typeof(Int32))
            {
                Int32 result;
                if (Int32.TryParse(value.ToString().Trim(), out result))
                    return result;
                return result;
            }
            if (destinationType == typeof(byte))
            {
                byte result;
                if (byte.TryParse(value.ToString().Trim(), out result))
                    return result;
                return result;
            }
            if (destinationType == typeof(TimeSpan))
            {
                DateTime result;
                if (DateTime.TryParseExact(value.ToString().Trim(),
                    new String[] { "yyyyMMdd", "HHmmss" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal | DateTimeStyles.NoCurrentDateDefault,
                    out result))
                    return result.TimeOfDay;
            }
            return base.ConvertTo(context, culture, value.ToString().Trim(),
                destinationType);
        }
    }

    public class SAPImportHelper
    {
        private ManageCompositeRole connect;
        private int ROWCOUNT = 30000;
        private int ROWSKIPS = 0;
        private int MAX_RETRY = 10;
        // equivalente al carattere 254 tabella ASCII
        private String delimiter = "" + (char)254;
        private IRfcFunction function;
        public event LogEventHandler OperationStatus;
        public event CompleteEventHandler CompleteStatus;

        private delegate DataTable ImportTable(String table);

        public SAPImportHelper(ProxyParameter param)
        {
            this.connect = new ManageCompositeRole();
            this.connect.Login(param);
        }

        protected virtual void OnLog(LogEventArgs e)
        {
            LogEventHandler handler = OperationStatus;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnComplete(LogEventArgs e)
        {
            CompleteEventHandler handler = CompleteStatus;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public DataTable Import(String tableName, DataTable filter)
        {
            DataTable dt = new DataTable();
            dt = InternalImportTable(tableName, filter);
            return dt;
        }


        private DataColumn[] InternalGetColumn(IRfcTable fields)
        {
            List<DataColumn> result = new List<DataColumn>();
            for (int i = 0; i < fields.Count; i++)
                result.Add(new DataColumn(fields[i].GetString("FIELDNAME"),
                    intTypeToType(fields[i].GetChar("TYPE"))));

            return result.ToArray();
        }

        private DataRow InternalGetRow(String row, DataTable dt)
        {
            DataRow drRow = dt.NewRow();
            String[] rowSplit = row.Split(new String[] { delimiter },
                StringSplitOptions.None);
            if (rowSplit.Length != dt.Columns.Count)
                throw new ApplicationException("FIELD COUNT EXCEPTION");

            for (int j = 0; j < rowSplit.Length; j++)
            {
                object value = new SAPTypeConverter().ConvertTo(null,
                    CultureInfo.InvariantCulture, rowSplit[j],
                    dt.Columns[j].DataType);
                drRow[dt.Columns[j]] = (value == null) ? Convert.DBNull : value;
            }
            return drRow;
        }


        private DataTable InternalImportTable(String table, DataTable filter)
        {
            String whereClause = "";
            int downloadedRows = ROWSKIPS = 0;

            if (filter != null)
            {
                for (int i = 0; i < filter.Rows.Count; i++)
                {
                    DataRow filterRow = filter.Rows[i];
                    if (String.IsNullOrEmpty(whereClause))
                        whereClause = (String)filterRow["FIELDNAME"] + " " +
                    (String)filterRow["OPTIONS"];
                    else
                        whereClause += " AND " + (String)filterRow["FIELDNAME"] +
                            " " + (String)filterRow["OPTIONS"];
                }
            }

            DataTable result = new DataTable(table);
            bool firstDownload = true;

            do
            {
                ROWSKIPS += downloadedRows;
                #region Retry
                bool fail = true;
                int retryCount = 0;
                while (fail)
                {
                    try
                    {
                        function = connect.Destination.Repository.
                            CreateFunction("RFC_READ_TABLE");

                        function.SetValue("DELIMITER", delimiter);
                        function.SetValue("ROWSKIPS", ROWSKIPS);
                        function.SetValue("ROWCOUNT", ROWCOUNT);
                        function.SetValue("QUERY_TABLE", table);
                        if (!String.IsNullOrEmpty(whereClause))
                        {
                            IRfcTable options = function.GetTable("OPTIONS");
                            options.Append();
                            options[0].SetValue("TEXT", whereClause);
                        }
                        function.Invoke(connect.Destination);
                        fail = false;
                    }
                    catch (Exception exc)
                    {
#if DEBUG
                        OnLog(new LogEventArgs("Errore durante la connessione, tentativo in corso: " + exc.Message));
#endif
                        Thread.Sleep(500);
                        retryCount++;
                        if (retryCount > MAX_RETRY)
                            throw new ApplicationException("MAX RETRY ATTEMPED");
                    }
                }
                #endregion
#if DEBUG
                OnLog(new LogEventArgs("Inizio download"));
#endif
                IRfcTable dataResult = function.GetTable("DATA");
                if (firstDownload)
                {
                    IRfcTable fields = function.GetTable("FIELDS");
                    result.Columns.AddRange(InternalGetColumn(fields));
                    firstDownload = false;
                }

                for (int i = 0; i < dataResult.Count; i++)
                {
                    String row = dataResult[i].GetString("WA");
                    result.Rows.Add(InternalGetRow(row, result));
                }
                downloadedRows = dataResult.Count;
            } while (ROWCOUNT <= downloadedRows);
            OnComplete(new LogEventArgs(table + " completata"));
            function = null;
            GC.Collect();
            return result;
        }

        private Type intTypeToType(char p)
        {
            switch (p)
            {
                case 'b': return typeof(Byte);
                case 'D': return typeof(DateTime);
                case 'T': return typeof(TimeSpan);
                case 'F': return typeof(float);
                case 'I':
                case 'P':
                case 's': return typeof(Int32);
                case 'h':
                case 'l':
                case 'r':
                case 'u':
                case 'v': return typeof(object);
                case 'C':
                case 'g':
                case 'N':
                case 'V':
                case 'X':
                case 'y': return typeof(String);
                default: return typeof(object);
            }
        }
    }
}

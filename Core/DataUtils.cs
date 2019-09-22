using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fetcher2.Core
{
    public static class DataUtils
    {
        public static ICollection<System.Data.DataRow> FindValue(this System.Data.DataTable table, System.Data.DataColumn col, object value)
        {
            var ret = new List<System.Data.DataRow>();
            foreach (System.Data.DataRow v in table.Rows)
            {
                var rowValue = v[col];
                if (value == null && rowValue == null) ret.Add(v);
                else if (value != null ? value.Equals(rowValue) : rowValue.Equals(value)) ret.Add(v);
            }
            return ret;
        }

        public static void Save(this System.Data.DataSet dataSet, string fileName = null, string tableName = null, IWin32Window owner = null)
        {
            if (string.IsNullOrEmpty(fileName))
                fileName = UI.FileIO.GetSaveFileName(owner, fileName ?? ".xls", "Comma Separated Values Files (*.csv)|*.csv|Microsoft Excel Files (*.xls)|*.xls|Microsoft Access Files (*.mdb)|*.mdb|All files (*.*)|*.*");
            if (fileName == null) return;
            string providerName = IntPtr.Size == 8 ? "Microsoft.ACE.OLEDB.12.0" : "Microsoft.Jet.OLEDB.4.0";
            string providerProds = null;
            string tableNameFormat = "{0}";
            string fileExtention = System.IO.Path.GetExtension(fileName).ToLower();
            var dataSource = fileName;
            switch (fileExtention)
            {
                case ".csv":
                    tableNameFormat = System.IO.Path.GetFileNameWithoutExtension(fileName) + "_{0}.csv";
                    dataSource = System.IO.Path.GetDirectoryName(fileName);
                    providerProds = "text;HDR=YES;FMT=Delimited";
                    break;
                case ".xls": providerProds = "Excel 8.0;HDR=YES"; break;
                case ".mdb": providerProds = "Access 8.0;HDR=YES"; break;
                default: throw new Exception("unknown file extention");
            }
            if (tableName == null) {
                using (var con = new System.Data.OleDb.OleDbConnection())
                {
                    con.ConnectionString = "Provider=" + providerName + ";Data Source=" + dataSource + ";Extended Properties='" + providerProds + "'";
                    con.Open();
                    foreach (System.Data.DataTable t in dataSet.Tables)
                    {
                        if (tableName != null && tableName != t.TableName) continue;
                        var tabeName = string.Format(tableNameFormat, t.TableName);
                        using (var command = new System.Data.OleDb.OleDbCommand(null, con))
                        {
                            string sqlCreate = string.Format("Create Table {0} (", tabeName);
                            string sqlInsert = string.Format("Insert Into {0} (", tabeName);
                            string sqlInsertValues = "Values(";
                            foreach (System.Data.DataColumn c in t.Columns)
                            {
                                var p = command.CreateParameter();
                                //p.ParameterName = c.ColumnName;
                                command.Parameters.Add(p);
                                string oleDbType = "Note";
                                if (c.DataType == typeof(int) | c.DataType == typeof(short) | c.DataType == typeof(byte)) oleDbType = "LONG";
                                sqlCreate += string.Format("[{0}] {1}, ", c.ColumnName, oleDbType);
                                sqlInsert += string.Format("[{0}], ", c.ColumnName);
                                sqlInsertValues += "?, ";
                            }
                            command.CommandText = sqlCreate.TrimEnd(' ', ',') + ")";
                            command.ExecuteNonQuery();
                            command.CommandText = sqlInsert.TrimEnd(' ', ',') + ")" + sqlInsertValues.TrimEnd(' ', ',') + ")";
                            foreach (System.Data.DataRow r in t.Rows)
                            {
                                for (int c =0; c < t.Columns.Count; c++) command.Parameters[c].Value = r[c];
                                command.ExecuteNonQuery();
                            }
                        }
                    }
                    con.Close();
                }
            }
        }
    }
}

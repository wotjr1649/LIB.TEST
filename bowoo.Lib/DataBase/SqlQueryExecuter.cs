using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;

using bowoo.Framework.common;

namespace bowoo.Lib.DataBase
{
    public class DBtility
    {
        static public SqlQueryExecuter BaseConnection
        {
            get { return bowoo.Lib.DataBase.SqlQueryExecuter.getInstance(); }
        }

    }

    public class SqlQueryExecuter
    {
        private static object _lock = new object();
        private readonly string strnolock = "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED ";

        public SqlQueryExecuter()
        {

        }

        public SqlQueryExecuter(string conn)
        {
            _connectionstring = conn;
        }

        private static SqlQueryExecuter _Instance;

        static public SqlQueryExecuter getInstance()
        {
            if (_Instance == null)
                _Instance = new SqlQueryExecuter();
            return _Instance;
        }


        static public SqlQueryExecuter getInstance(string conn)
        {
            if (_Instance == null)
                _Instance = new SqlQueryExecuter(conn);
            return _Instance;
        }

        private string connectionstring = null;
        private string _connectionstring
        {
            get
            {
                if (connectionstring == null)
                {
                    connectionstring = System.Configuration.ConfigurationManager.ConnectionStrings["defaultDatabase"].ToString();
                    TripleDESCrypto.CryptoUtil ENC = new TripleDESCrypto.CryptoUtil();
                    string sReplace = string.Empty;
                    string sConvertReplace = string.Empty;
                    string[] stResult = connectionstring.Split(';');
                    for (int i = 0; i < stResult.Length; i++)
                    {
                        if (stResult[i].Substring(0, 4).ToUpper() == "PASS")
                        {
                            sReplace = stResult[i].ToString().Replace("Password=", "");
                            sConvertReplace = sReplace;
                            //sConvertReplace = ENC.DecryptData(sReplace);
                        }
                    }
                    connectionstring = connectionstring.Replace(sReplace, sConvertReplace);
                }

                return connectionstring;
            }
            set
            {
                connectionstring = value;
            }
        }

        public SqlConnection Conn
        {
            get
            {
                return new SqlConnection(_connectionstring);
            }
        }

        public SqlParameter SetParameter(string Name, ParameterDirection Direction, int Size)
        {
            SqlParameter reVal = new SqlParameter();
            reVal.ParameterName = Name;
            reVal.Direction = Direction;
            reVal.Size = Size;
            return reVal;
        }



        private SqlParameter[] ParameterConvert(Dictionary<string, object> param)
        {
            SqlParameter[] spParem = null;
            if (param != null)
            {
                spParem = new SqlParameter[param.Keys.Count];
                int i = 0;
                foreach (string key in param.Keys)
                {
                    spParem[i] = new SqlParameter(key, param[key]);
                    i++;
                }
            }

            return spParem;
        }

        #region 자동 커넥션 메소드
        public int ExecuteNonQuery(string qry)
        {
            int ans = 0;
            SqlConnection scn = Conn;

            try
            {
                ans = SqlHelper.ExecuteNonQuery(scn, CommandType.Text, qry);
            }
            catch (Exception ex)
            {
                lib.Common.Log.LogFile.WriteError(ex, "Query: " + qry + "\n" + ex.Message + ex.StackTrace);

                if (scn.State != ConnectionState.Closed)
                {
                    scn.Close();
                }

                throw ex;
            }

            return ans;
        }

        public int ExecuteNonQuery(string sp, params object[] param)
        {
            int ans = 0;
            SqlConnection scn = Conn;
            try
            {
                ans = SqlHelper.ExecuteNonQuery(scn, sp, param);
            }
            catch (Exception ex)
            {
                string[] p = new string[param.Length];
                for (int i = 0; i < param.Length; i++)
                {
                    p[i] = param[i].ToString();
                }
                lib.Common.Log.LogFile.WriteError(ex, "Query: " + sp + string.Join(":", p) + "\n" + ex.Message + ex.StackTrace);

                if (scn.State != ConnectionState.Closed)
                {
                    scn.Close();
                }


                throw ex;
            }
            scn.Close();
            return ans;
        }

        public int ExecuteNonQuery(string qry, Dictionary<string, object> param)
        {
            return ExecuteNonQuery(null, qry, param, CommandType.Text);
        }

        public int ExecuteNonQuery(string qry, Dictionary<string, object> param, CommandType commandtype)
        {
            return ExecuteNonQuery(null, qry, param, commandtype);
        }

        public int ExecuteNonQuery(SqlTransaction tran, string qry)
        {
            return ExecuteNonQuery(tran, qry, null, CommandType.Text);
        }

        public int ExecuteNonQuery(SqlTransaction tran, string qry, Dictionary<string, object> param)
        {
            return ExecuteNonQuery(tran, qry, param, CommandType.Text);
        }

        public int ExecuteNonQuery(SqlTransaction tran, string commandtext, Dictionary<string, object> param, CommandType commandtype)
        {
            int ans = 0;
            SqlConnection scn = null;

            if (tran == null)
                scn = Conn;

            try
            {
                lock (_lock)
                {
                    if (tran == null)
                    {
                        ans = SqlHelper.ExecuteNonQuery(scn, commandtype, commandtext, ParameterConvert(param));
                    }
                    else
                        ans = SqlHelper.ExecuteNonQuery(tran, commandtype, commandtext, ParameterConvert(param));
                }
            }
            catch (Exception ex)
            {
                lib.Common.Log.LogFile.WriteError(ex, "Query: " + commandtext + "\n" + ex.Message + ex.StackTrace);

                if (scn != null)
                {
                    if (scn.State != ConnectionState.Closed)
                    {
                        scn.Close();
                    }
                }

                throw ex;
            }

            if (scn != null) scn.Close();

            return ans;
        }

        public int ExecuteNonQuery(SqlTransaction tran, string commandtext, Dictionary<string, object> param, CommandType commandtype, bool limitless)
        {
            int ans = 0;
            SqlConnection scn = null;

            if (tran == null)
                scn = Conn;

            try
            {
                lock (_lock)
                {
                    if (tran == null)
                    {
                        ans = SqlHelper.ExecuteNonQuery(scn, commandtype, commandtext, limitless, ParameterConvert(param));
                    }
                    else
                        ans = SqlHelper.ExecuteNonQuery(tran, commandtype, commandtext, limitless, ParameterConvert(param));
                }
            }
            catch (Exception ex)
            {
                lib.Common.Log.LogFile.WriteError(ex, "Query: " + commandtext + "\n" + ex.Message + ex.StackTrace);

                if (scn != null)
                {
                    if (scn.State != ConnectionState.Closed)
                    {
                        scn.Close();
                    }
                }

                throw ex;
            }

            if (scn != null) scn.Close();

            return ans;
        }

        public DataSet ExecuteDataSet(string qry)
        {
            return ExecuteDataSet(Conn, strnolock + qry, (Dictionary<string, object>)null, CommandType.Text);
        }

        public DataSet ExecuteDataSet(SqlConnection scn, string qry)
        {
            return ExecuteDataSet(scn, strnolock + qry, (Dictionary<string, object>)null, CommandType.Text);
        }


        public DataSet ExecuteDataSet(string sp, params object[] param)
        {
            DataSet ds = null;
            SqlConnection scn = Conn;
            try
            {
                ds = SqlHelper.ExecuteDataset(scn, sp, param);
            }
            catch (Exception ex)
            {
                lib.Common.Log.LogFile.WriteError(ex, "Query: " + sp + "\n" + ex.Message + ex.StackTrace);
                if (scn.State != ConnectionState.Closed)
                {
                    scn.Close();
                }

                throw ex;
            }
            scn.Close();
            return ds;
        }

        public DataSet ExecuteDataSet(string commandtext, Dictionary<string, object> param)
        {
            return ExecuteDataSet(strnolock + commandtext, param, CommandType.Text);
        }

        public DataSet ExecuteDataSet(string commandtext, Dictionary<string, object> param, CommandType commandType)
        {
            return ExecuteDataSet(Conn, commandtext, param, commandType);
        }

        public DataSet ExecuteDataSet(SqlTransaction tran, string commandtext, Dictionary<string, object> param, CommandType commandType)
        {
            DataSet ds = null;
            SqlConnection scn = null;

            if (tran == null)
                scn = Conn;

            try
            {
                lock (_lock)
                {
                    if (tran == null)
                        ds = SqlHelper.ExecuteDataset(scn, commandType, commandtext, ParameterConvert(param));
                    else
                        ds = SqlHelper.ExecuteDataset(tran, commandType, commandtext, ParameterConvert(param));
                }
            }
            catch (Exception ex)
            {
                lib.Common.Log.LogFile.WriteError(ex, "Query: " + commandtext + "\n" + ex.Message + ex.StackTrace);

                if (scn != null)
                {
                    if (scn.State != ConnectionState.Closed)
                    {
                        scn.Close();
                    }
                }

                throw ex;
            }

            if (scn != null) scn.Close();

            return ds;
        }

        public DataSet ExecuteDataSet(SqlTransaction tran, string commandtext, Dictionary<string, object> param, CommandType commandType, bool limitless)
        {
            DataSet ds = null;
            SqlConnection scn = null;

            if (tran == null)
                scn = Conn;

            try
            {
                lock (_lock)
                {
                    if (tran == null)
                        ds = SqlHelper.ExecuteDataset(scn, commandType, commandtext, limitless, ParameterConvert(param));
                    else
                        ds = SqlHelper.ExecuteDataset(tran, commandType, commandtext, limitless, ParameterConvert(param));
                }
            }
            catch (Exception ex)
            {
                lib.Common.Log.LogFile.WriteError(ex, "Query: " + commandtext + "\n" + ex.Message + ex.StackTrace);

                if (scn != null)
                {
                    if (scn.State != ConnectionState.Closed)
                    {
                        scn.Close();
                    }
                }

                throw ex;
            }

            if (scn != null) scn.Close();

            return ds;
        }

        public DataSet ExecuteDataSet(SqlConnection scn, string commandtext, Dictionary<string, object> param, CommandType commandType)
        {
            DataSet ds = null;

            try
            {
                //Console.WriteLine("==========================================================================");
                //Console.WriteLine($"DB 쿼리 전송 전 확인!!!\n{scn}\n{commandType}\n{commandtext}\n{ParameterConvert(param)}");
                //Console.WriteLine("==========================================================================");
                lock (_lock)
                {
                    ds = SqlHelper.ExecuteDataset(scn, commandType, commandtext, ParameterConvert(param));
                }
            }
            catch (Exception ex)
            {
                lib.Common.Log.LogFile.WriteError(ex, "Query: " + commandtext + "\n" + ex.Message + ex.StackTrace);

                if (scn != null)
                {
                    if (scn.State != ConnectionState.Closed)
                    {
                        scn.Close();
                    }
                }

                throw ex;
            }

            if (scn != null) scn.Close();


            //foreach (DataTable table in ds.Tables)
            //{
            //    Console.WriteLine($"== {table.TableName} ==");
            //    // 컬럼명 출력
            //    foreach (DataColumn col in table.Columns)
            //    {
            //        Console.Write($"{col.ColumnName}\t");
            //    }
            //    Console.WriteLine();

            //    // 데이터 출력
            //    foreach (DataRow row in table.Rows)
            //    {
            //        foreach (var item in row.ItemArray)
            //        {
            //            Console.Write($"{item}\t");
            //        }
            //        Console.WriteLine();
            //    }
            //    Console.WriteLine();
            //}

            return ds;
        }


        public DataSet ExecuteDataSetSqlParam(string sp, params SqlParameter[] param)
        {
            DataSet ds = null;
            SqlConnection scn = Conn;
            try
            {
                ds = SqlHelper.ExecuteDataset(scn, CommandType.StoredProcedure, sp, param);
            }
            catch (Exception ex)
            {
                lib.Common.Log.LogFile.WriteError(ex, "Query: " + sp + "\n" + ex.Message + ex.StackTrace);
                if (scn.State != ConnectionState.Closed)
                {
                    scn.Close();
                }

                throw ex;
            }
            scn.Close();
            return ds;
        }

        public object ExecuteScalar(string qry)
        {
            SqlConnection scn = Conn;
            try
            {
                return SqlHelper.ExecuteScalar(scn, CommandType.Text, strnolock + qry);
            }
            catch (Exception ex)
            {
                lib.Common.Log.LogFile.WriteError(ex, "Query: " + qry + "\n" + ex.Message + ex.StackTrace);
                if (scn.State != ConnectionState.Closed)
                {
                    scn.Close();
                }


                throw ex;
            }
        }

        public object ExecuteScalar(string sp, params object[] param)
        {
            SqlConnection scn = Conn;
            try
            {
                return SqlHelper.ExecuteScalar(scn, sp, param);
            }
            catch (Exception ex)
            {
                lib.Common.Log.LogFile.WriteError(ex, "Query: " + sp + "\n" + ex.Message + ex.StackTrace);
                if (scn.State != ConnectionState.Closed)
                {
                    scn.Close();
                }

                throw ex;
            }
        }

        public object ExecuteScalar(string commandtext, Dictionary<string, object> param)
        {
            return ExecuteScalar(commandtext, param, CommandType.Text);
        }

        public object ExecuteScalar(string commandtext, Dictionary<string, object> param, CommandType commandType)
        {
            return ExecuteScalar(null, commandtext, param, commandType);
        }

        public object ExecuteScalar(SqlTransaction tran, string commandtext, Dictionary<string, object> param, CommandType commandType)
        {
            SqlConnection scn = null;

            if (tran == null)
                scn = Conn;


            try
            {
                lock (_lock)
                {
                    object ans = null;

                    if (tran == null)
                    {
                        ans = SqlHelper.ExecuteScalar(scn, commandType, commandtext, ParameterConvert(param));
                        Conn.Close();
                    }
                    else
                    {
                        ans = SqlHelper.ExecuteScalar(tran, commandType, commandtext, ParameterConvert(param));
                    }
                    return ans;
                }
            }
            catch (Exception ex)
            {
                lib.Common.Log.LogFile.WriteError(ex, "Query: " + commandtext + "\n" + ex.Message + ex.StackTrace);

                if (tran == null)
                {
                    if (scn.State != ConnectionState.Closed)
                    {
                        scn.Close();
                    }
                }


                throw ex;
            }
        }

        public SqlDataReader ExecuteReader(string qry)
        {
            return ExecuteReader((SqlConnection)null, qry);
        }

        public SqlDataReader ExecuteReader(SqlConnection con, string qry)
        {
            try
            {
                if (con == null)
                {
                    return SqlHelper.ExecuteReader(_connectionstring, CommandType.Text, strnolock + qry);
                }
                else
                {
                    return SqlHelper.ExecuteReader(con, CommandType.Text, strnolock + qry);
                }
            }
            catch (Exception ex)
            {
                string error = "Query: " + qry + "\n" + ex.Message + ex.StackTrace;

                lib.Common.Log.LogFile.WriteError(ex, "Query: " + qry + "\n" + ex.Message + ex.StackTrace);

                if (con != null)
                {
                    if (con.State != ConnectionState.Closed)
                    {
                        con.Close();
                    }
                }

                throw new Exception(error);
            }
        }

        public SqlDataReader ExecuteReader(string qry, Dictionary<string, object> param)
        {
            return ExecuteReader(strnolock + qry, param, CommandType.Text);
        }

        public SqlDataReader ExecuteReader(string commandtext, Dictionary<string, object> param, CommandType commandType)
        {
            return ExecuteReader((SqlConnection)null, commandtext, param, commandType);
        }

        public SqlDataReader ExecuteReader(SqlConnection con, string commandtext, Dictionary<string, object> param, CommandType commandType)
        {
            try
            {
                lock (_lock)
                {
                    if (con == null)
                    {
                        return SqlHelper.ExecuteReader(_connectionstring, commandType, commandtext, ParameterConvert(param));
                    }
                    else
                    {
                        return SqlHelper.ExecuteReader(con, commandType, commandtext, ParameterConvert(param));
                    }
                }
            }
            catch (Exception ex)
            {
                lib.Common.Log.LogFile.WriteError(ex, "Query: " + commandtext + "\n" + ex.Message + ex.StackTrace);
                if (con != null)
                {
                    con.Close();
                }

                throw ex;
            }
        }

        public SqlDataReader ExecuteReader(SqlTransaction tran, string qry, Dictionary<string, object> param)
        {
            return ExecuteReader(tran, qry, param, CommandType.Text);
        }

        public SqlDataReader ExecuteReader(SqlTransaction tran, string qry)
        {
            return ExecuteReader(tran, qry, null, CommandType.Text);
        }

        public SqlDataReader ExecuteReader(SqlTransaction tran, string commandtext, Dictionary<string, object> param, CommandType commandType)
        {
            SqlDataReader sdr = null;
            SqlConnection scn = null;

            if (tran == null)
                scn = Conn;

            try
            {
                lock (_lock)
                {
                    if (tran == null)
                        sdr = SqlHelper.ExecuteReader(scn, commandType, commandtext, ParameterConvert(param));
                    else
                        sdr = SqlHelper.ExecuteReader(tran, commandType, commandtext, ParameterConvert(param));

                    return sdr;
                }
            }
            catch (Exception ex)
            {
                lib.Common.Log.LogFile.WriteError(ex, "Query: " + commandtext + "\n" + ex.Message + ex.StackTrace);

                if (scn != null)
                {
                    if (scn.State != ConnectionState.Closed)
                    {
                        scn.Close();
                    }
                }



                throw ex;
            }
        }



        public SqlDataReader ExecuteReader(string sp, params object[] param)
        {
            try
            {
                SqlDataReader sdr = SqlHelper.ExecuteReader(_connectionstring, sp, param);
                return sdr;
            }
            catch (Exception ex)
            {
                lib.Common.Log.LogFile.WriteError(ex, "Query: " + sp + "\n" + ex.Message + ex.StackTrace);
                throw ex;
            }
        }



        public void UpdateData(DataSet ds, string text, string tablename)
        {
            SqlConnection scn = Conn;
            try
            {
                SqlDataAdapter sda = new SqlDataAdapter(text, scn);
                SqlCommandBuilder scb = new SqlCommandBuilder(sda);
                SqlHelper.UpdateDataset(scb.GetInsertCommand(), scb.GetDeleteCommand(), scb.GetUpdateCommand(), ds, tablename);
            }
            catch (Exception ex)
            {
                lib.Common.Log.LogFile.WriteError(ex, "Query: " + text + "\n" + ex.Message + ex.StackTrace);

                if (scn.State != ConnectionState.Closed)
                {
                    scn.Close();
                }



                throw ex;
            }
        }


        public DataRow ExecuteRow(string qry)
        {
            return ExecuteRow(qry, null);
        }

        public DataRow ExecuteRow(string qry, Dictionary<string, object> param)
        {
            return ExecuteRow(null, strnolock + qry, param, CommandType.Text);
        }

        public DataRow ExecuteRow(SqlTransaction tran, string qry)
        {
            return ExecuteRow(tran, qry, null, CommandType.Text);
        }

        public DataTable ExecuteTable(SqlTransaction tran, string qry, Dictionary<string, object> param, CommandType commandType)
        {
            DataSet ds = ExecuteDataSet(tran, qry, param, commandType);
            if (ds.Tables.Count == 0) return null;
            if (ds.Tables[0].Rows.Count == 0) return null;
            return ds.Tables[0];
        }

        public DataRow ExecuteRow(SqlTransaction tran, string qry, Dictionary<string, object> param, CommandType commandType)
        {
            DataSet ds = ExecuteDataSet(tran, qry, param, commandType);
            if (ds.Tables.Count == 0) return null;
            if (ds.Tables[0].Rows.Count == 0) return null;
            return ds.Tables[0].Rows[0];
        }

        public Dictionary<string, object> GetOutputParms(string sp, object[] arParms)
        {
            try
            {
                SqlParameter[] storedParams = SqlHelperParameterCache.GetSpParameterSet(Conn, sp);

                for (int i = 0; i <= arParms.Length - 1; i++)
                    storedParams[i].Value = arParms[i];

                SqlConnection scn = Conn;
                SqlHelper.ExecuteNonQuery(scn, CommandType.StoredProcedure, sp, storedParams);

                Dictionary<string, object> rtn = new Dictionary<string, object>();

                for (int i = 0; i <= storedParams.Length - 1; i++)
                {
                    if (storedParams[i].Direction == ParameterDirection.InputOutput || storedParams[i].Direction == ParameterDirection.Output)
                    {
                        rtn.Add(storedParams[i].ParameterName.Substring(1).ToUpper(), storedParams[i].Value);
                    }
                }
                return rtn;
            }
            catch (Exception ex)
            {
                lib.Common.Log.LogFile.WriteError(ex, "Query: " + sp + "\n" + ex.Message + ex.StackTrace);
                throw ex;
            }
        }
        #endregion

    }
}

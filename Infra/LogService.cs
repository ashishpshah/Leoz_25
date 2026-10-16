using Microsoft.Data.SqlClient;
using System.Data;
//using MySql.Data.MySqlClient;

namespace Leoz_25.Infra
{
	public static class LogService
	{
		private static string prev_msg = "";

		private static void SetMSG(string msg) => prev_msg = msg;
		private static string GetMSG() => prev_msg;

		//public static void Log_User_Activity(string Form_Name, bool Record_Add, bool Record_Modify, bool Record_Delete, bool Record_View, string Remark, bool Is_Posted, string Log_Type)
		//{
		//	List<OracleParameter> oParams = new List<OracleParameter>();

		//	oParams.Add(new OracleParameter("P_USERNAME", OracleDbType.Int64) { Value = "" });
		//	oParams.Add(new OracleParameter("P_USER_ID", OracleDbType.Int64) { Value = Common.Get_Session_Int(SessionKey.USER_ID) });
		//	oParams.Add(new OracleParameter("P_PLANT_ID", OracleDbType.Int64) { Value = Common.Get_Session_Int(SessionKey.PLANT_ID) });
		//	oParams.Add(new OracleParameter("P_ROLE_ID", OracleDbType.Int64) { Value = Common.Get_Session_Int(SessionKey.ROLE_ID) });
		//	oParams.Add(new OracleParameter("P_MENU_ID", OracleDbType.Int64) { Value = Common.Get_Session_Int(SessionKey.MENU_ID) });
		//	oParams.Add(new OracleParameter("P_FORM_NAME", OracleDbType.Varchar2) { Value = Form_Name });
		//	oParams.Add(new OracleParameter("P_RECORD_ADD", OracleDbType.Varchar2) { Value = Record_Add });
		//	oParams.Add(new OracleParameter("P_RECORD_MODIFY", OracleDbType.Varchar2) { Value = Record_Modify });
		//	oParams.Add(new OracleParameter("P_RECORD_DELETE", OracleDbType.Varchar2) { Value = Record_Delete });
		//	oParams.Add(new OracleParameter("P_RECORD_VIEW", OracleDbType.Varchar2) { Value = Record_View });
		//	oParams.Add(new OracleParameter("P_REMARK", OracleDbType.Varchar2) { Value = Remark });
		//	oParams.Add(new OracleParameter("P_IS_POSTED", OracleDbType.Varchar2) { Value = Is_Posted ? "Y" : "N" });
		//	oParams.Add(new OracleParameter("P_LOG_TYPE", OracleDbType.Varchar2) { Value = Log_Type });

		//	var (IsSuccess, response, Id) = DataContext.ExecuteStoredProcedure("PC_QR_USER_ACTIVITY", oParams, false);

		//}

		public static void LogInsert(string action, string message, Exception ex = null)
		{
				try
				{
					var error = "";

					if (AppHttpContextAccessor.IsLogActive_Error == true && ex != null)
					{
						error = "Error : " + ex.Message.ToString() + Environment.NewLine;

						if (ex.InnerException != null)
						{
							try { error = error + " | InnerException: " + ex.InnerException.ToString().Substring(0, (ex.InnerException.ToString().Length > 1000 ? 1000 : ex.InnerException.ToString().Length)); } catch { error = error + "InnerException: " + ex.InnerException?.ToString(); }
						}

						if (ex.StackTrace != null)
						{
							try { error = error + " | StackTrace: " + ex.StackTrace.ToString().Substring(0, (ex.StackTrace.ToString().Length > 1000 ? 1000 : ex.StackTrace.ToString().Length)); } catch { error = error + "InnerException: " + ex.StackTrace?.ToString(); }
						}

						if (ex.Source != null)
						{
							try { error = error + " | Source: " + ex.Source.ToString().Substring(0, (ex.Source.ToString().Length > 1000 ? 1000 : ex.Source.ToString().Length)); } catch { error = error + "InnerException: " + ex.Source?.ToString(); }
						}

						if (ex.StackTrace == null && ex.Source == null)
						{
							try { error = error + " | Exception: " + ex.ToString().Substring(0, (ex.Source.ToString().Length > 3000 ? 3000 : ex.Source.ToString().Length)); } catch { error = error + "Exception: " + ex?.ToString(); }
						}
					}

					if (AppHttpContextAccessor.IsLogActive_Info == true && (action + " | " + message + " | " + error) != GetMSG())
					{
						//List<SqlParameter> oParams = new List<SqlParameter>();

						//oParams.Add(new SqlParameter("P_MESSAGE", MySqlDbType.LongText) { Value = action + " | " + message + " | " + error });

						//var dt = DataContext.ExecuteStoredProcedure_DataTable_SQL("PC_LOG_INSERT", oParams);

						DataTable dt = new DataTable();

						//if (AppHttpContextAccessor.IsCloudDBActive)
						//	try
						//	{
						//		List<OracleParameter> parameters = new List<OracleParameter>();

						//		parameters.Add(new OracleParameter("P_ERROR", OracleDbType.NVarchar2) { Value = action + " | " + message + " | " + error });

						//		using (OracleConnection con = new OracleConnection(DataContext._connectionString_Oracle))
						//		{
						//			using (OracleCommand cmd = con.CreateCommand())
						//			{
						//				con.Open();

						//				cmd.CommandType = CommandType.StoredProcedure;
						//				cmd.CommandText = "PC_INSERT_LOG";
						//				//cmd.DeriveParameters();

						//				if (parameters != null && parameters.Count > 0)
						//					cmd.Parameters.AddRange(parameters.ToArray());

						//				OracleDataAdapter oraAdapter = new OracleDataAdapter(cmd);

						//				OracleCommandBuilder oraBuilder = new OracleCommandBuilder(oraAdapter);

						//				oraAdapter.Fill(dt);
						//			}
						//		}
						//	}
						//	catch { }
						//else
						try
						{
							List<SqlParameter> oParams = new List<SqlParameter>();

							oParams.Add(new SqlParameter("P_MESSAGE", SqlDbType.Text) { Value = action + " | " + message + " | " + error });

							using (SqlConnection conn = new SqlConnection(DataContext_Command._connectionString))
							{
								using (SqlCommand cmd = new SqlCommand("PC_LOG_INSERT", conn))
								{
									cmd.CommandType = CommandType.StoredProcedure;

									if (oParams != null)
										foreach (SqlParameter param in oParams)
											cmd.Parameters.Add(param);

									SqlDataAdapter da = new SqlDataAdapter(cmd);

									da.Fill(dt);

								}
							}
						}
						catch { }

						SetMSG((action + " | " + message + " | " + error));
					}

				}
				catch (Exception) { }
		}
	}
}

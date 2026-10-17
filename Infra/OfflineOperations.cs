using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace Leoz_25.Infra
{
	/// <summary>
	/// PWA offline sync : a change saved offline is replayed by the service worker (wwwroot/sw.js) with a unique
	/// operation id in the header X-Leoz-Operation-Id. The first request runs the action as usual (same login, permission
	/// and validation checks as online) and its successful answer is kept; a repeated request with the same id
	/// (e.g. the answer was lost after the save) gets the kept answer instead of saving the record again.
	/// Requests without the header (normal online use) are not affected.
	/// </summary>
	public enum OfflineOperationState
	{
		Started,     // new operation : run the action
		Completed,   // already saved : return the kept answer
		InProgress,  // the same operation is running right now
		Unknown,     // started long ago and never completed (server stopped during the save) : result unknown
		OtherUser    // the operation id belongs to another user
	}

	public sealed class OfflineOperationBegin
	{
		public OfflineOperationState State { get; set; }
		public string? ResponseJson { get; set; }
	}

	public interface IOfflineOperationStore
	{
		OfflineOperationBegin TryBegin(Guid operationId, long userId, string path);
		void Complete(Guid operationId, string responseJson);
		void Abandon(Guid operationId);
	}

	/// <summary> dbo.OfflineOperation (Infra/Sql/009_OfflineOperation.sql). </summary>
	public sealed class SqlOfflineOperationStore : IOfflineOperationStore
	{
		private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);
		private const int KeepDays = 30;

		private readonly string _connectionString;
		private DateTime _nextCleanup = DateTime.MinValue;

		public SqlOfflineOperationStore(string connectionString) => _connectionString = connectionString;

		public OfflineOperationBegin TryBegin(Guid operationId, long userId, string path)
		{
			using var connection = Open();

			Cleanup(connection);

			try
			{
				using var insert = new SqlCommand("INSERT INTO [dbo].[OfflineOperation] ([OperationId], [UserId], [Path], [Status], [CreatedOn]) VALUES (@Id, @UserId, @Path, 0, SYSUTCDATETIME())", connection);
				insert.Parameters.AddWithValue("@Id", operationId);
				insert.Parameters.AddWithValue("@UserId", userId);
				insert.Parameters.AddWithValue("@Path", path.Length > 256 ? path.Substring(0, 256) : path);
				insert.ExecuteNonQuery();

				return new OfflineOperationBegin { State = OfflineOperationState.Started };
			}
			catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601) // primary key : already seen
			{
				using var select = new SqlCommand("SELECT [UserId], [Status], [ResponseJson], [CreatedOn] FROM [dbo].[OfflineOperation] WHERE [OperationId] = @Id", connection);
				select.Parameters.AddWithValue("@Id", operationId);

				using var reader = select.ExecuteReader();
				if (!reader.Read()) return new OfflineOperationBegin { State = OfflineOperationState.InProgress }; // removed meanwhile : try again later

				long ownerId = reader.GetInt64(0);
				byte status = reader.GetByte(1);
				string? response = reader.IsDBNull(2) ? null : reader.GetString(2);
				DateTime createdOn = reader.GetDateTime(3);

				if (ownerId != userId) return new OfflineOperationBegin { State = OfflineOperationState.OtherUser };
				if (status == 1) return new OfflineOperationBegin { State = OfflineOperationState.Completed, ResponseJson = response };

				return new OfflineOperationBegin { State = DateTime.UtcNow - createdOn > StaleAfter ? OfflineOperationState.Unknown : OfflineOperationState.InProgress };
			}
		}

		public void Complete(Guid operationId, string responseJson)
		{
			using var connection = Open();
			using var command = new SqlCommand("UPDATE [dbo].[OfflineOperation] SET [Status] = 1, [ResponseJson] = @Response, [CompletedOn] = SYSUTCDATETIME() WHERE [OperationId] = @Id", connection);
			command.Parameters.AddWithValue("@Id", operationId);
			command.Parameters.AddWithValue("@Response", responseJson);
			command.ExecuteNonQuery();
		}

		public void Abandon(Guid operationId)
		{
			using var connection = Open();
			using var command = new SqlCommand("DELETE FROM [dbo].[OfflineOperation] WHERE [OperationId] = @Id AND [Status] = 0", connection);
			command.Parameters.AddWithValue("@Id", operationId);
			command.ExecuteNonQuery();
		}

		private SqlConnection Open()
		{
			var connection = new SqlConnection(new SqlConnectionStringBuilder(_connectionString) { ConnectTimeout = 5 }.ConnectionString);
			connection.Open();
			return connection;
		}

		// old operations are no longer replayed by any device : keep the table small (at most once an hour)
		private void Cleanup(SqlConnection connection)
		{
			if (DateTime.UtcNow < _nextCleanup) return;
			_nextCleanup = DateTime.UtcNow.AddHours(1);

			try
			{
				using var command = new SqlCommand("DELETE FROM [dbo].[OfflineOperation] WHERE [CreatedOn] < DATEADD(DAY, -@Days, SYSUTCDATETIME())", connection);
				command.Parameters.AddWithValue("@Days", KeepDays);
				command.ExecuteNonQuery();
			}
			catch { }
		}
	}

	/// <summary>
	/// Global MVC filter. Runs after the controller's own OnActionExecuting (login / Remember Me / password-change checks),
	/// so a replayed change is only saved when the current server session may do it, exactly like online.
	/// </summary>
	public sealed class OfflineOperationFilter : IAsyncActionFilter
	{
		public const string HeaderName = "X-Leoz-Operation-Id";
		public const string SyncHeaderName = "X-Leoz-Sync";

		private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };

		private readonly IOfflineOperationStore _store;

		/// <summary> Logged-in user of the request (tests replace it). </summary>
		public Func<long> CurrentUserId { get; set; } = () => Common.LoggedUser_Id();

		public OfflineOperationFilter(IOfflineOperationStore store) => _store = store;

		public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
		{
			var request = context.HttpContext.Request;

			if (!HttpMethods.IsPost(request.Method)
				|| !Guid.TryParse(request.Headers[HeaderName].ToString(), out Guid operationId) || operationId == Guid.Empty)
			{
				await next();
				return;
			}

			long userId = CurrentUserId();

			// not logged in : the controller has already sent the login redirect, nothing to record
			if (userId <= 0)
			{
				await next();
				return;
			}

			OfflineOperationBegin begin;

			try { begin = _store.TryBegin(operationId, userId, request.Path.ToString()); }
			catch (Exception ex)
			{
				// table missing / database problem : save as usual (no duplicate protection for this request)
				LogService.LogInsert("OfflineOperationFilter", "Duplicate check not available : " + ex.Message, ex);
				await next();
				return;
			}

			switch (begin.State)
			{
				case OfflineOperationState.Completed:
					context.HttpContext.Response.Headers[SyncHeaderName] = "replayed";
					context.Result = new ContentResult { Content = begin.ResponseJson ?? Serialize(Answer(true, "Record saved successfully !")), ContentType = "application/json; charset=utf-8", StatusCode = StatusCodes.Status200OK };
					return;

				case OfflineOperationState.InProgress:
					context.HttpContext.Response.Headers[SyncHeaderName] = "in-progress";
					context.Result = JsonStatus(StatusCodes.Status409Conflict, "This change is still being saved. It will be checked again shortly.");
					return;

				case OfflineOperationState.Unknown:
					context.HttpContext.Response.Headers[SyncHeaderName] = "unknown";
					context.Result = JsonStatus(StatusCodes.Status409Conflict, "The server could not confirm whether this change was saved. Please check the list before sending it again.");
					return;

				case OfflineOperationState.OtherUser:
					context.HttpContext.Response.Headers[SyncHeaderName] = "other-user";
					context.Result = JsonStatus(StatusCodes.Status403Forbidden, "This change was saved by another user on this device and cannot be sent with your login.");
					return;
			}

			ActionExecutedContext executed;

			try { executed = await next(); }
			catch
			{
				Try(() => _store.Abandon(operationId));
				throw;
			}

			// keep only confirmed saves; anything else (validation, permission, error) may be sent again after it is fixed
			string? answer = executed.Exception == null && executed.Result is JsonResult json ? Serialize(json.Value) : null;

			if (answer != null && IsSuccess(answer)) Try(() => _store.Complete(operationId, answer));
			else Try(() => _store.Abandon(operationId));
		}

		public static bool IsSuccess(string json)
		{
			try
			{
				using var document = JsonDocument.Parse(json);
				return document.RootElement.ValueKind == JsonValueKind.Object
					&& document.RootElement.TryGetProperty("StatusCode", out JsonElement status)
					&& status.ValueKind == JsonValueKind.Number
					&& (status.GetInt32() == ResponseStatusCode.Success || status.GetInt32() == 10);
			}
			catch { return false; }
		}

		private static string Serialize(object? value) => JsonSerializer.Serialize(value, value?.GetType() ?? typeof(object), JsonOptions);

		private static object Answer(bool success, string message) => new { IsSuccess = success, StatusCode = success ? ResponseStatusCode.Success : ResponseStatusCode.Error, Message = message };

		private static ContentResult JsonStatus(int httpStatus, string message) =>
			new ContentResult { Content = Serialize(Answer(false, message)), ContentType = "application/json; charset=utf-8", StatusCode = httpStatus };

		private static void Try(Action action)
		{
			try { action(); }
			catch (Exception ex) { LogService.LogInsert("OfflineOperationFilter", ex.Message, ex); }
		}
	}
}

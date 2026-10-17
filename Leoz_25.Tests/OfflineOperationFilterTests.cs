using Leoz_25.Infra;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Leoz_25.Tests;

/// <summary>
/// OfflineOperationFilter : a change replayed by the PWA (header X-Leoz-Operation-Id) is saved only once,
/// a refused change can be sent again, and one user cannot replay another user's operation. In-memory store (no database).
/// </summary>
public class OfflineOperationFilterTests
{
	private const string Path = "/Admin/Project/Save";
	private static readonly string Success = "{\"IsSuccess\":true,\"StatusCode\":1,\"Message\":\"Record saved successfully ! \"}";

	/// <summary> Same rules as SqlOfflineOperationStore, in memory. </summary>
	private sealed class FakeStore : IOfflineOperationStore
	{
		public readonly Dictionary<Guid, (long UserId, byte Status, string? Response, DateTime CreatedOn)> Rows = new();

		public OfflineOperationBegin TryBegin(Guid operationId, long userId, string path)
		{
			if (!Rows.TryGetValue(operationId, out var row))
			{
				Rows[operationId] = (userId, 0, null, DateTime.UtcNow);
				return new OfflineOperationBegin { State = OfflineOperationState.Started };
			}

			if (row.UserId != userId) return new OfflineOperationBegin { State = OfflineOperationState.OtherUser };
			if (row.Status == 1) return new OfflineOperationBegin { State = OfflineOperationState.Completed, ResponseJson = row.Response };
			return new OfflineOperationBegin { State = DateTime.UtcNow - row.CreatedOn > TimeSpan.FromMinutes(5) ? OfflineOperationState.Unknown : OfflineOperationState.InProgress };
		}

		public void Complete(Guid operationId, string responseJson) => Rows[operationId] = (Rows[operationId].UserId, 1, responseJson, Rows[operationId].CreatedOn);

		public void Abandon(Guid operationId)
		{
			if (Rows.TryGetValue(operationId, out var row) && row.Status == 0) Rows.Remove(operationId);
		}
	}

	private sealed class Run
	{
		public int ActionCalls;
		public ActionExecutingContext Context = null!;
		public HttpContext Http = null!;
	}

	private static async Task<Run> Execute(OfflineOperationFilter filter, string? operationId, object? actionAnswer, string method = "POST")
	{
		var http = new DefaultHttpContext();
		http.Request.Method = method;
		http.Request.Path = Path;
		if (operationId != null) http.Request.Headers[OfflineOperationFilter.HeaderName] = operationId;

		var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
		var run = new Run { Http = http };
		run.Context = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());

		await filter.OnActionExecutionAsync(run.Context, () =>
		{
			run.ActionCalls++;
			return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()) { Result = new JsonResult(actionAnswer) });
		});

		return run;
	}

	private static object Answer(int statusCode, string message) => new { IsSuccess = statusCode == 1, StatusCode = statusCode, Message = message, RedirectURL = "/Admin/Project/Index" };

	private static OfflineOperationFilter Filter(FakeStore store, long userId = 42) => new(store) { CurrentUserId = () => userId };

	[Fact]
	public async Task Request_without_operation_id_is_not_touched()
	{
		var store = new FakeStore();

		var run = await Execute(Filter(store), null, Answer(1, "ok"));

		Assert.Equal(1, run.ActionCalls);
		Assert.Empty(store.Rows);
	}

	[Fact]
	public async Task Get_request_is_not_touched()
	{
		var store = new FakeStore();

		var run = await Execute(Filter(store), Guid.NewGuid().ToString(), Answer(1, "ok"), "GET");

		Assert.Equal(1, run.ActionCalls);
		Assert.Empty(store.Rows);
	}

	[Fact]
	public async Task First_send_saves_and_keeps_the_answer()
	{
		var store = new FakeStore();
		var id = Guid.NewGuid();

		var run = await Execute(Filter(store), id.ToString(), Answer(1, "Record saved successfully !"));

		Assert.Equal(1, run.ActionCalls);
		Assert.Null(run.Context.Result); // the action's own answer goes to the client
		Assert.Equal(1, store.Rows[id].Status);
		Assert.True(OfflineOperationFilter.IsSuccess(store.Rows[id].Response!));
	}

	[Fact]
	public async Task Answer_lost_then_sent_again_does_not_save_twice()
	{
		var store = new FakeStore();
		var filter = Filter(store);
		var id = Guid.NewGuid().ToString();

		var first = await Execute(filter, id, Answer(1, "Record saved successfully !"));
		var second = await Execute(filter, id, Answer(1, "Record saved successfully !"));
		var third = await Execute(filter, id, Answer(1, "Record saved successfully !"));

		Assert.Equal(1, first.ActionCalls);
		Assert.Equal(0, second.ActionCalls); // no second Project
		Assert.Equal(0, third.ActionCalls);

		var replayed = Assert.IsType<ContentResult>(second.Context.Result);
		Assert.Equal(200, replayed.StatusCode);
		Assert.True(OfflineOperationFilter.IsSuccess(replayed.Content!));
		Assert.Equal("replayed", second.Http.Response.Headers[OfflineOperationFilter.SyncHeaderName].ToString());
	}

	[Fact]
	public async Task Refused_change_can_be_sent_again_after_it_is_fixed()
	{
		var store = new FakeStore();
		var filter = Filter(store);
		var id = Guid.NewGuid().ToString();

		var refused = await Execute(filter, id, Answer(0, "You do not have access to perform this action."));
		Assert.Equal(1, refused.ActionCalls);
		Assert.Empty(store.Rows); // nothing kept : a refusal is not a save

		var retried = await Execute(filter, id, Answer(1, "Record saved successfully !"));
		Assert.Equal(1, retried.ActionCalls);
		Assert.Equal(1, store.Rows[Guid.Parse(id)].Status);
	}

	[Fact]
	public async Task Another_users_operation_is_refused()
	{
		var store = new FakeStore();
		var id = Guid.NewGuid().ToString();

		await Execute(Filter(store, userId: 42), id, Answer(1, "ok"));
		var other = await Execute(Filter(store, userId: 7), id, Answer(1, "ok"));

		Assert.Equal(0, other.ActionCalls);
		Assert.Equal(403, Assert.IsType<ContentResult>(other.Context.Result).StatusCode);
	}

	[Fact]
	public async Task Same_operation_running_now_is_not_run_twice()
	{
		var store = new FakeStore();
		var id = Guid.NewGuid();
		store.Rows[id] = (42, 0, null, DateTime.UtcNow);

		var run = await Execute(Filter(store), id.ToString(), Answer(1, "ok"));

		Assert.Equal(0, run.ActionCalls);
		Assert.Equal(409, Assert.IsType<ContentResult>(run.Context.Result).StatusCode);
		Assert.Equal("in-progress", run.Http.Response.Headers[OfflineOperationFilter.SyncHeaderName].ToString());
	}

	[Fact]
	public async Task Unconfirmed_old_operation_is_reported_as_unknown()
	{
		var store = new FakeStore();
		var id = Guid.NewGuid();
		store.Rows[id] = (42, 0, null, DateTime.UtcNow.AddMinutes(-30));

		var run = await Execute(Filter(store), id.ToString(), Answer(1, "ok"));

		Assert.Equal(0, run.ActionCalls);
		Assert.Equal("unknown", run.Http.Response.Headers[OfflineOperationFilter.SyncHeaderName].ToString());
	}

	[Fact]
	public async Task Not_logged_in_is_left_to_the_controller()
	{
		var store = new FakeStore();

		var run = await Execute(Filter(store, userId: -1), Guid.NewGuid().ToString(), Answer(1, "ok"));

		Assert.Equal(1, run.ActionCalls);
		Assert.Empty(store.Rows);
	}

	[Theory]
	[InlineData("{\"StatusCode\":1}", true)]
	[InlineData("{\"StatusCode\":10}", true)]
	[InlineData("{\"StatusCode\":0,\"Message\":\"x\"}", false)]
	[InlineData("{\"Message\":\"no status\"}", false)]
	[InlineData("[1,2]", false)]
	[InlineData("not json", false)]
	public void IsSuccess_reads_the_StatusCode(string json, bool expected) => Assert.Equal(expected, OfflineOperationFilter.IsSuccess(json));

	/// <summary>
	/// Real table (Infra/Sql/009_OfflineOperation.sql). Runs only when LEOZ_TEST_DB is set to a TEST database connection string.
	/// </summary>
	[Fact]
	public void Sql_store_saves_an_operation_only_once()
	{
		string? connectionString = Environment.GetEnvironmentVariable("LEOZ_TEST_DB");
		if (string.IsNullOrWhiteSpace(connectionString)) return; // no test database configured

		var store = new SqlOfflineOperationStore(connectionString);
		var id = Guid.NewGuid();

		Assert.Equal(OfflineOperationState.Started, store.TryBegin(id, 42, Path).State);
		Assert.Equal(OfflineOperationState.InProgress, store.TryBegin(id, 42, Path).State);
		Assert.Equal(OfflineOperationState.OtherUser, store.TryBegin(id, 7, Path).State);

		store.Complete(id, Success);
		var again = store.TryBegin(id, 42, Path);
		Assert.Equal(OfflineOperationState.Completed, again.State);
		Assert.Equal(Success, again.ResponseJson);

		var refused = Guid.NewGuid();
		Assert.Equal(OfflineOperationState.Started, store.TryBegin(refused, 42, Path).State);
		store.Abandon(refused);
		Assert.Equal(OfflineOperationState.Started, store.TryBegin(refused, 42, Path).State);
		store.Abandon(refused);
	}
}

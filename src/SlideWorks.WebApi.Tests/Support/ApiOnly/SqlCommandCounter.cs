using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SlideWorks.WebApi.Data;

namespace SlideWorks.WebApi.Tests.Support.ApiOnly;

// Counts the SQL commands the app runs, so a test can assert "one indexed read" rather than argue it from the query shape.
// A fixture opts in by overriding ApiTestFixture.ConfigureTestServices to call AddTo, and reads Count (or Texts) around the
// request it measures, after Reset. Every command counts, reads and writes, sync or async. It is shared by the fixture's
// app, so a test that counts calls Reset first and runs nothing else against the app meanwhile.
public sealed class SqlCommandCounter : DbCommandInterceptor
{
    private readonly Lock _gate = new();
    private readonly List<string> _texts = [];

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _texts.Count;
            }
        }
    }

    // The text of each command run since Reset, in order.
    public IReadOnlyList<string> Texts
    {
        get
        {
            lock (_gate)
            {
                return [.. _texts];
            }
        }
    }

    public void AddTo(IServiceCollection services)
        => services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(this));

    public void Reset()
    {
        lock (_gate)
        {
            _texts.Clear();
        }
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        => Counted(command, result);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Counted(command, result));

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        => Counted(command, result);

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Counted(command, result));

    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        => Counted(command, result);

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Counted(command, result));

    private T Counted<T>(DbCommand command, T result)
    {
        lock (_gate)
        {
            _texts.Add(command.CommandText);
        }
        return result;
    }
}

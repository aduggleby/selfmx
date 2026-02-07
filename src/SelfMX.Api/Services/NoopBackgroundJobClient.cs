using Hangfire;
using Hangfire.Common;
using Hangfire.States;

namespace SelfMX.Api.Services;

public class NoopBackgroundJobClient : IBackgroundJobClient
{
    public string? Create(Job job, IState state)
    {
        return Guid.NewGuid().ToString("N");
    }

    public bool ChangeState(string jobId, IState state, string? expectedState)
    {
        return true;
    }
}

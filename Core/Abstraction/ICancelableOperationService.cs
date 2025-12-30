using System;

namespace Core.Abstraction
{
    public interface ICancelableOperationService
    {
        Task<bool> ExecuteWithCancellationAsync(
            string title,
            string message,
            Func<CancellationToken, IProgress<double>, IProgress<string>, Task<bool>> operation,
            bool showProgress = false,
            bool showStatus = false);
    }
}

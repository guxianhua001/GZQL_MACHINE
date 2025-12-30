
// Interfaces/IProgressReporter.cs
namespace Interfaces
{
    public interface IProgressReporter
    {
        void Report(double progress, string statusMessage = null);
    }
}


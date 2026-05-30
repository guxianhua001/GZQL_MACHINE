
namespace Core.Abstraction
{
    public interface IProgressReporter
    {
        void Report(double progress, string statusMessage = null);
    }
}

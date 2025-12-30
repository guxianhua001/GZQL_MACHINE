

namespace Interfaces.Models
{
    public class ProgressReport
    {
        public int Percentage { get; set; }
        public string Message { get; set; }

        public ProgressReport(int percentage, string message)
        {
            Percentage = percentage;
            Message = message;
        }
    }
}


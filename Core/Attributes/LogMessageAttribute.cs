using System;

namespace Core.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class LogMessageAttribute : Attribute
    {
        public string CultureCode { get; }
        public string Message { get; }

        public LogMessageAttribute(string cultureCode, string message)
        {
            CultureCode = cultureCode ?? throw new ArgumentNullException(nameof(cultureCode));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }
    }
}

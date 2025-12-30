using System;
using System.Diagnostics;

namespace SmarterMotion
{
    class MyTraceListener : TraceListener
    {
        public override void Write(string message)
        {
            XLogger.Instance.WriteLine(message);
        }

        public override void WriteLine(string message)
        {
            XLogger.Instance.WriteLine(message);
        }

        public override void WriteLine(object o, string category)
        {
            string msg = "";
            if (string.IsNullOrWhiteSpace(category) == false)
            {
                msg = category + " : ";
            }
            if (o is Exception)
            {
                var ex = (Exception)o;
                msg += ex.Message + " => ";
                msg += ex.StackTrace;
            }
            else if (o != null)
            {
                msg = o.ToString();
            }
            WriteLine(msg);
        }


    }
}

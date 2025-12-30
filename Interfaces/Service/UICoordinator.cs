using System;
using System.Windows;

namespace Interfaces
{
    public class UICoordinator : IUICoordinator
    {
        public void RunOnUIThread(Action action)
        {
            if (Application.Current == null) return;

            if (Application.Current.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() => RunOnUIThread(action));
            }
        }
    }
}

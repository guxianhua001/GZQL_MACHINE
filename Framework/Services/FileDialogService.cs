using Framework.Dialogs;
using Microsoft.Win32;

namespace Framework.Services
{
    public class FileDialogService : IFileDialogService
    {
        public string ShowOpenFileDialog(string filter = null, string title = null, string initialDirectory = null)
        {
            var dlg = new OpenFileDialog
            {
                Filter = filter ?? "All files (*.*)|*.*",
                Title = title ?? "Open File"
            };
            if (!string.IsNullOrEmpty(initialDirectory) && System.IO.Directory.Exists(initialDirectory))
                dlg.InitialDirectory = initialDirectory;
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        public string ShowSaveFileDialog(string filter = null, string title = null, string defaultFileName = null)
        {
            var dlg = new SaveFileDialog
            {
                Filter = filter ?? "All files (*.*)|*.*",
                Title = title ?? "Save File",
                FileName = defaultFileName ?? string.Empty
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }
    }
}

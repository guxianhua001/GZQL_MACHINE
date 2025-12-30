using Microsoft.Win32;
using System.IO;

public class FileDialogService : IFileService
{
    public string OpenFileDialog(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string SaveFileDialog(string title, string defaultName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            FileName = defaultName,
            Filter = filter,
            OverwritePrompt = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}


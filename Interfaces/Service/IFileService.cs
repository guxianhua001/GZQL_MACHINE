


public interface IFileService
{
    string OpenFileDialog(string title, string filter);
    string SaveFileDialog(string title, string defaultName, string filter);
}


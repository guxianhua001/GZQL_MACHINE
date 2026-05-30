using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

class Program
{
    static void Main()
    {
        using var writer = new StreamWriter(@"c:\WorkFiles\GZQL_MACHINE\check_result.txt", false, System.Text.Encoding.UTF8);
        CheckFile(@"c:\WorkFiles\GZQL_MACHINE\MainApp\Languages\Strings.zh-CN.xaml", "zh-CN", writer);
        CheckFile(@"c:\WorkFiles\GZQL_MACHINE\MainApp\Languages\Strings.en-US.xaml", "en-US", writer);
        writer.Flush();
    }

    static void CheckFile(string path, string label, StreamWriter writer)
    {
        try
        {
            var doc = new XmlDocument();
            doc.Load(path);
            writer.WriteLine($"{label}: XML is valid");
        }
        catch (Exception ex)
        {
            writer.WriteLine($"{label}: XML INVALID - {ex.Message}");
        }

        try
        {
            var lines = File.ReadAllLines(path);
            var dict = new Dictionary<string, int>();
            var dupes = new List<string>();
            var regex = new Regex(@"x:Key=""([^""]+)""");

            for (int i = 0; i < lines.Length; i++)
            {
                var m = regex.Match(lines[i]);
                if (m.Success)
                {
                    var key = m.Groups[1].Value;
                    if (dict.TryGetValue(key, out int prevLine))
                    {
                        dupes.Add($"DUPE: '{key}' at lines {prevLine} and {i + 1}");
                    }
                    else
                    {
                        dict[key] = i + 1;
                    }
                }
            }

            writer.WriteLine($"=== {label}: {dict.Count} unique keys, {dupes.Count} duplicates ===");
            foreach (var d in dupes) writer.WriteLine(d);
        }
        catch (Exception ex)
        {
            writer.WriteLine($"Error reading {label}: {ex.Message}");
        }
    }
}

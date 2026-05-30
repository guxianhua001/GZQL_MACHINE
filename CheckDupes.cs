using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        CheckFile(@"c:\WorkFiles\GZQL_MACHINE\MainApp\Languages\Strings.zh-CN.xaml", "zh-CN");
        CheckFile(@"c:\WorkFiles\GZQL_MACHINE\MainApp\Languages\Strings.en-US.xaml", "en-US");
    }

    static void CheckFile(string path, string label)
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

        Console.WriteLine($"=== {label}: {dict.Count} unique keys, {dupes.Count} duplicates ===");
        foreach (var d in dupes) Console.WriteLine(d);
    }
}

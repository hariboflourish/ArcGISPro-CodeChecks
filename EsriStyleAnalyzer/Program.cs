using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

class Program
{
    static readonly StringBuilder Report = new();
    static int ErrorCount = 0;
    static int WarnCount = 0;

    static int Main(string[] args)
    {
        var rootDir = Directory.GetCurrentDirectory();
        var reportPath = Path.Combine(
            rootDir,
            $"EsriStyle_Report_{DateTime.Now:yyyyMM}.txt");

        LogInfo("Starting Esri Style XAML analysis");
        LogInfo($"Root directory: {rootDir}");
        LogInfo($"Report file: {reportPath}");
        LogInfo("");

        var xamlFiles = Directory.GetFiles(rootDir, "*.xaml", SearchOption.AllDirectories)
            .Where(f =>
                !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();

        LogInfo($"XAML files found: {xamlFiles.Count}");
	LogNewLine();

        foreach (var file in xamlFiles)
        {
            LogInfo($"Analyzing: {Path.GetRelativePath(rootDir, file)}");

            bool analyzed = false;

            try
            {
                AnalyzeWithXml(file);
                analyzed = true;
            }
            catch (Exception ex) when (ex is XmlException || ex is InvalidOperationException)
            {
                LogWarn("XAML contains ArcGIS-specific or design-time markup. Fallback to text scan.");
            }

            if (!analyzed)
            {
                AnalyzeWithTextScan(file);
            }

	    LogNewLine();
        }

        LogInfo($"Summary: Errors={ErrorCount}, Warnings={WarnCount}");
	if (ErrorCount == 0 && WarnCount == 0)
	{
		LogInfo("No errors or warnings found. Report file not generated");
	}
	else
	{

		// Write footer link to report
		Report.AppendLine("==========================");
		Report.AppendLine("Esri UI Style Guide Reference");
		Report.AppendLine("--------------------------");
		Report.AppendLine("For full details on Esri XAML control styles and usage, see:");
		Report.AppendLine("https://github.com/Esri/arcgis-pro-sdk/wiki/proguide-style-guide#");
		Report.AppendLine("==========================");
		Report.AppendLine();
		File.WriteAllText(reportPath, Report.ToString());
	}

        return ErrorCount > 0 ? 1 : 0;
    }

    /* ===============================
       XML ANALYSIS (PRIMARY)
       =============================== */

    static void AnalyzeWithXml(string file)
    {
        var doc = XDocument.Load(file, LoadOptions.SetLineInfo);

        foreach (var element in doc.Descendants())
        {
            var control = element.Name.LocalName;

            if (!AllTrackedControls.Contains(control))
                continue;

            var styleAttr = element.Attribute("Style");
            var line = GetLine(element);

            if (ErrorControls.Contains(control))
            {
                if (styleAttr == null || !IsEsriStyle(styleAttr.Value))
                {
                    LogError($"{control} missing required Esri style (line: {line})");
                }
            }
            else if (WarnControls.Contains(control))
            {
                if (styleAttr == null)
                {
                    LogWarn($"{control} does not use Esri style (optional) (line: {line})");
                }
                else if (!IsEsriStyle(styleAttr.Value))
                {
                    LogWarn($"{control} uses non-Esri style '{styleAttr.Value}' (line: {line})");
                }
            }
        }
    }

    /* ===============================
       TEXT SCAN FALLBACK
       =============================== */

    static void AnalyzeWithTextScan(string file)
    {
        var lines = File.ReadAllLines(file);

        for (int i = 0; i < lines.Length; i++)
        {
            var lineText = lines[i];
            var lineNo = i + 1;

            foreach (var control in ErrorControls)
            {
                if (lineText.Contains($"<{control}", StringComparison.OrdinalIgnoreCase))
                {
                    if (!lineText.Contains("Style=", StringComparison.OrdinalIgnoreCase))
                    {
                        LogError($"{control} missing required Esri style (line: {lineNo})");
                    }
                    break;
                }
            }

            foreach (var control in WarnControls)
            {
                if (lineText.Contains($"<{control}", StringComparison.OrdinalIgnoreCase))
                {
                    if (!lineText.Contains("Style=", StringComparison.OrdinalIgnoreCase))
                    {
                        LogWarn($"{control} does not use Esri style (optional) (line: {lineNo})");
                    }
                    break;
                }
            }
        }
    }

    /* ===============================
       LOGGING
       =============================== */

    static void LogNewLine()
    {
	    string message = "----------";
	    WriteConsole(ConsoleColor.Gray, message);
	    Report.AppendLine( message);
    }

    static void LogInfo(string message)
    {
        WriteConsole(ConsoleColor.Gray, "[INFO] " + message);
        Report.AppendLine("[INFO] " + message);
    }

    static void LogWarn(string message)
    {
        WarnCount++;
        WriteConsole(ConsoleColor.Yellow, "[WARN] " + message);
        Report.AppendLine("[WARN] " + message);
    }

    static void LogError(string message)
    {
        ErrorCount++;
        WriteConsole(ConsoleColor.Red, "[ERROR] " + message);
        Report.AppendLine("[ERROR] " + message);
    }

    static void WriteConsole(ConsoleColor color, string text)
    {
        var old = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = old;
    }

    /* ===============================
       HELPERS
       =============================== */

    static int GetLine(XElement element)
    {
        if (element is IXmlLineInfo li && li.HasLineInfo())
            return li.LineNumber;

        return -1;
    }

    static bool IsEsriStyle(string style)
    {
        if (string.IsNullOrWhiteSpace(style))
            return false;

        return style.Contains("Esri_", StringComparison.OrdinalIgnoreCase);
    }

    /* ===============================
       CONTROL GROUPS (VERIFIED)
       =============================== */

    static readonly HashSet<string> ErrorControls =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Button",
            "ToggleButton",
            "DataGrid",
            "Expander",
            "Image"
        };

    static readonly HashSet<string> WarnControls =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "CheckBox",
            "ListBox",
            "ListView",
            "TextBlock"
        };

    static readonly HashSet<string> AllTrackedControls =
        new(ErrorControls
            .Concat(WarnControls),
            StringComparer.OrdinalIgnoreCase);
}

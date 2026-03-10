using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using System.Text.RegularExpressions;

bool fixMode = args.Contains("--fix");
bool csvMode = args.Contains("--csv");

var errors =
    new Dictionary<string, Dictionary<string, List<(string Message, string? Suggestion)>>>();

var csvRows = new List<(string File,
                        string Category,
                        string OldName,
                        string SuggestName,
                        int LineNumber)>();

var rootDir = Directory.GetCurrentDirectory();
var timeStamp = DateTime.Now.ToString("yyyyMM");

var reportFilePath = Path.Combine(rootDir, $"NamingConvention_Report_{timeStamp}.txt");
var csvFilePath = Path.Combine(rootDir, $"NamingConvention_Report_{timeStamp}.csv");

var csFiles = Directory.GetFiles(rootDir, "*.cs", SearchOption.AllDirectories)
    .Where(f =>
        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
    .ToList();

/* ===============================
   ROSLYN SETUP
   =============================== */

var workspace = new AdhocWorkspace();

var projectInfo = ProjectInfo.Create(
    ProjectId.CreateNewId(),
    VersionStamp.Create(),
    "NamingAnalyzer",
    "NamingAnalyzer",
    LanguageNames.CSharp);

var mainProject = workspace.AddProject(projectInfo);

foreach (var file in csFiles)
{
    var text = SourceText.From(File.ReadAllText(file));
    mainProject = mainProject.AddDocument(
        Path.GetFileName(file),
        text,
        filePath: file).Project;
}

mainProject = mainProject.Solution.Projects.First();

/* ===============================
   ANALYSIS
   =============================== */

foreach (var document in mainProject.Documents)
{
    var root = await document.GetSyntaxRootAsync();
    var model = await document.GetSemanticModelAsync();
    if (root == null || model == null)
        continue;

    var filePath = document.FilePath!;
    var displayFile = Path.GetRelativePath(rootDir, filePath);

    /* ===== CLASS ===== */
    foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
    {
        var symbol = model.GetDeclaredSymbol(cls);
        if (symbol == null) continue;

        if (!IsPascalCase(symbol.Name))
        {
            var suggestion = ToPascalCase(symbol.Name);
            Report(filePath, "Class Naming",
                symbol.Name, "Class must be PascalCase", suggestion);

            AddCsv(displayFile, "Class Naming", symbol.Name, suggestion, cls);
        }

        var baseName = symbol.BaseType?.Name;

        if (baseName is "Button" or "Tool" or "DockPane")
        {
            if (!symbol.Name.EndsWith(baseName))
            {
                var suggestion = symbol.Name + baseName;

                Report(filePath, "UI Naming",
                    symbol.Name,
                    $"Class inheriting {baseName} must end with '{baseName}'",
                    suggestion);

                AddCsv(displayFile, "UI Naming", symbol.Name, suggestion, cls);
            }
        }

        if (baseName?.Contains("ViewModel") == true)
        {
            if (!symbol.Name.EndsWith("ViewModel"))
            {
                var suggestion = symbol.Name + "ViewModel";

                Report(filePath, "ViewModel Naming",
                    symbol.Name,
                    "ViewModel must end with 'ViewModel'",
                    suggestion);

                AddCsv(displayFile, "ViewModel Naming", symbol.Name, suggestion, cls);
            }
        }
    }

    /* ===== METHOD ===== */
    foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
    {
        var symbol = model.GetDeclaredSymbol(method);
        if (symbol == null || symbol.IsOverride) continue;

        if (!IsPascalCase(symbol.Name))
        {
            var suggestion = ToPascalCase(symbol.Name);
            Report(filePath, "Method Naming",
                symbol.Name, "Method must be PascalCase", suggestion);

            AddCsv(displayFile, "Method Naming", symbol.Name, suggestion, method);
        }

        bool isAsync = method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword));
        bool endsWithAsync = symbol.Name.EndsWith("Async");

        if (isAsync && !endsWithAsync)
        {
            var suggestion = symbol.Name + "Async";

            Report(filePath, "Async Naming",
                symbol.Name,
                "Async method must end with 'Async'",
                suggestion);

            AddCsv(displayFile, "Async Naming", symbol.Name, suggestion, method);
        }
    }

    /* ===== PROPERTY ===== */
    foreach (var prop in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
    {
        var symbol = model.GetDeclaredSymbol(prop);
        if (symbol == null || symbol.IsOverride) continue;

        if (!IsPascalCase(symbol.Name))
        {
            var suggestion = ToPascalCase(symbol.Name);

            Report(filePath, "Property Naming",
                symbol.Name, "Property must be PascalCase", suggestion);

            AddCsv(displayFile, "Property Naming", symbol.Name, suggestion, prop);
        }
    }

    /* ===== FIELD ===== */
    foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
    {
        foreach (var v in field.Declaration.Variables)
        {
            var symbol = model.GetDeclaredSymbol(v) as IFieldSymbol;
            if (symbol == null) continue;

            if (symbol.IsConst)
            {
                if (!IsAllCaps(symbol.Name))
                {
                    var suggestion = ToAllCaps(symbol.Name);

                    Report(filePath, "Global Constant Naming",
                        symbol.Name,
                        "Global constant must be ALL_CAPS",
                        suggestion);

                    AddCsv(displayFile, "Global Constant Naming",
                           symbol.Name, suggestion, v);
                }
                continue;
            }

            if (symbol.IsStatic && symbol.IsReadOnly)
            {
                if (!IsPascalCase(symbol.Name))
                {
                    var suggestion = ToPascalCase(symbol.Name);

                    Report(filePath, "Static Readonly Naming",
                        symbol.Name,
                        "static readonly field must be PascalCase",
                        suggestion);

                    AddCsv(displayFile, "Static Readonly Naming",
                           symbol.Name, suggestion, v);
                }
                continue;
            }

            if (!symbol.Name.StartsWith("_") ||
                !IsCamelCase(symbol.Name.TrimStart('_')))
            {
                var suggestion = "_" + ToCamelCase(symbol.Name);

                Report(filePath, "Field Naming",
                    symbol.Name,
                    "Field must be _camelCase",
                    suggestion);

                AddCsv(displayFile, "Field Naming",
                       symbol.Name, suggestion, v);
            }
        }
    }

    /* ===== LOCAL VARIABLE + COLLECTION ===== */
    foreach (var localDecl in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
    {
        foreach (var v in localDecl.Declaration.Variables)
        {
            var symbol = model.GetDeclaredSymbol(v);
            if (symbol == null) continue;

            if (localDecl.IsConst)
            {
                if (!IsPascalCase(symbol.Name))
                {
                    var suggestion = ToPascalCase(symbol.Name);

                    Report(filePath, "Local Constant Naming",
                        symbol.Name,
                        "Local constant must be PascalCase",
                        suggestion);

                    AddCsv(displayFile, "Local Constant Naming",
                           symbol.Name, suggestion, v);
                }
            }
            else
            {
                if (!IsCamelCase(symbol.Name))
                {
                    var suggestion = ToCamelCase(symbol.Name);

                    Report(filePath, "Local Variable Naming",
                        symbol.Name,
                        "Local variable must be camelCase",
                        suggestion);

                    AddCsv(displayFile, "Local Variable Naming",
                           symbol.Name, suggestion, v);
                }
            }
        }

        var typeInfo = model.GetTypeInfo(localDecl.Declaration.Type);
        if (typeInfo.Type is INamedTypeSymbol namedType &&
            namedType.IsGenericType)
        {
            var typeName = namedType.Name;

            if (typeName is "List" or "IEnumerable" or "ICollection" or "HashSet")
            {
                foreach (var v in localDecl.Declaration.Variables)
                {
                    var name = v.Identifier.Text;

                    if (!name.EndsWith("List") &&
                        !name.EndsWith("Collection") &&
                        !name.EndsWith("Items") &&
                        !name.EndsWith("Set"))
                    {
                        var suggestion = name + "List";

                        Report(filePath, "Collection Naming",
                            name,
                            "Collection variable should end with List / Collection / Items / Set",
                            suggestion);

                        AddCsv(displayFile, "Collection Naming",
                               name, suggestion, v);
                    }
                }
            }
        }
    }
}

/* ===============================
   OUTPUT TXT REPORT
   =============================== */

var report = new StringBuilder();
report.AppendLine("C# Naming Convention Analysis Report");
report.AppendLine($"Root Directory: {rootDir}");
report.AppendLine(new string('-', 60));
report.AppendLine();

foreach (var fileEntry in errors)
{
    var displayFile = Path.GetRelativePath(rootDir, fileEntry.Key);
    Console.WriteLine($"📄 {displayFile}");
    report.AppendLine($"File: {displayFile}");

    foreach (var type in fileEntry.Value)
    {
        Console.WriteLine($"  [{type.Key}]");
        report.AppendLine($"  [{type.Key}]");

        foreach (var err in type.Value)
        {
            Console.WriteLine($"    ❌ {err.Message}");
            report.AppendLine($"    - {err.Message}");

            if (err.Suggestion != null)
            {
                Console.WriteLine($"       💡 Suggested: {err.Suggestion}");
                report.AppendLine($"       Suggested: {err.Suggestion}");
            }
        }
        Console.WriteLine();
        report.AppendLine();
    }
}

if (errors.Count > 0)
{
    File.WriteAllText(reportFilePath, report.ToString());
    Console.WriteLine($"TXT report created: {reportFilePath}");
}
else
{
    Console.WriteLine("✅ No naming convention issues found!");
}

/* ===============================
   OUTPUT CSV REPORT
   =============================== */

if (csvMode && csvRows.Count > 0)
{
    var csvBuilder = new StringBuilder();
    csvBuilder.AppendLine("FileName,Category,OldName,SuggestName,LineNumber");

    foreach (var row in csvRows)
    {
        csvBuilder.AppendLine(
            $"{row.File},{row.Category},{row.OldName},{row.SuggestName},{row.LineNumber}");
    }

    File.WriteAllText(csvFilePath, csvBuilder.ToString());
    Console.WriteLine($"CSV report created: {csvFilePath}");
}

/* ===============================
   HELPERS
   =============================== */

void AddCsv(string file,
            string category,
            string oldName,
            string suggest,
            SyntaxNode node)
{
    if (!csvMode)
        return;

    var lineSpan = node.GetLocation().GetLineSpan();
    int lineNumber = lineSpan.StartLinePosition.Line + 1;

    csvRows.Add((file, category, oldName, suggest, lineNumber));
}

void Report(string file, string category, string name, string message, string? suggestion)
{
    if (!errors.ContainsKey(file))
        errors[file] = new Dictionary<string, List<(string, string?)>>();

    if (!errors[file].ContainsKey(category))
        errors[file][category] = new List<(string, string?)>();

    errors[file][category].Add(($"{name} → {message}", suggestion));
}

bool IsPascalCase(string s) =>
    !string.IsNullOrEmpty(s) &&
    char.IsUpper(s[0]) &&
    !s.Contains("_");

bool IsCamelCase(string s) =>
    !string.IsNullOrEmpty(s) &&
    char.IsLower(s[0]) &&
    !s.Contains("_");

bool IsAllCaps(string s) =>
    s.All(c => !char.IsLetter(c) || char.IsUpper(c));

string ToPascalCase(string input)
{
    var words = Regex.Split(input.Trim('_'), @"[_\W]+")
        .Where(w => !string.IsNullOrEmpty(w))
        .Select(w => char.ToUpper(w[0]) + w.Substring(1).ToLower());

    return string.Concat(words);
}

string ToCamelCase(string input)
{
    if (string.IsNullOrWhiteSpace(input))
        return input;

    var p = ToPascalCase(input);
    return string.IsNullOrEmpty(p) ? p : char.ToLower(p[0]) + p[1..];
}

string ToAllCaps(string input) =>
    Regex.Replace(input, @"([a-z])([A-Z])", "$1_$2").ToUpper();

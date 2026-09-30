// Claude Code hooks for this repo, as a single .NET 10 file-based app so every
// contributor can run it with nothing but the SDK (and it compiles only once):
//
//   dotnet run .claude/hooks/guard.cs            PreToolUse: enforces the module-boundary
//                                                rules from CLAUDE.md that the compiler can't
//   dotnet run .claude/hooks/guard.cs -- --stop  Stop: builds the solution if a .cs/.csproj
//                                                changed this turn, and hands errors back
//   dotnet run .claude/hooks/guard.cs -- --scan  Audit: checks every file under src/ and tests/
//
// PreToolUse exits 2 with the violations on stderr, which blocks the tool call
// and hands the explanation back to Claude. Only violations the edit would
// *introduce* are reported, so pre-existing issues never block unrelated work.

#:property PublishAot=false

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

var projectDir = Environment.GetEnvironmentVariable("CLAUDE_PROJECT_DIR") is { Length: > 0 } envDir
    ? envDir
    : Directory.GetCurrentDirectory();

if (args.Contains("--scan"))
{
    return Guard.Scan(projectDir);
}

JsonElement input;
try
{
    input = JsonDocument.Parse(Console.In.ReadToEnd()).RootElement;
}
catch (JsonException)
{
    return 0; // Never block on malformed hook input.
}

if (input.TryGetProperty("cwd", out var cwd) && Environment.GetEnvironmentVariable("CLAUDE_PROJECT_DIR") is null or "")
{
    projectDir = cwd.GetString() ?? projectDir;
}

if (args.Contains("--stop"))
{
    var stopHookActive = input.TryGetProperty("stop_hook_active", out var active) && active.ValueKind == JsonValueKind.True;
    return BuildCheck.Run(projectDir, stopHookActive);
}

if (!input.TryGetProperty("tool_name", out var toolNameElement) || !input.TryGetProperty("tool_input", out var toolInput))
{
    return 0;
}

var toolName = toolNameElement.GetString();

var violations = toolName switch
{
    "Bash" or "PowerShell" => Guard.CheckCommand(Str(toolInput, "command")),
    "Write" or "Edit" or "MultiEdit" => Guard.CheckEdit(projectDir, toolName, toolInput),
    _ => [],
};

if (violations.Count == 0)
{
    return 0;
}

Console.Error.WriteLine("Blocked by .claude/hooks/guard.cs (modular-monolith rules, see CLAUDE.md / DESIGN.md):");
foreach (var violation in violations)
{
    Console.Error.WriteLine($"  - {violation}");
}
return 2;

static string Str(JsonElement element, string name)
    => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;

static class Guard
{
    static readonly string[] Layers = ["Domain", "Application", "Infrastructure", "Contracts", "Endpoints"];

    static readonly Regex ModuleProjectPath = new(
        @"^src/Modules/(?<module>[^/]+)/\k<module>\.(?<layer>Domain|Application|Infrastructure|Contracts|Endpoints)/",
        RegexOptions.Compiled);

    static readonly Regex UsingDirective = new(
        @"^\s*(?:global\s+)?using\s+(?:static\s+)?(?:\w+\s*=\s*)?(?<ns>[A-Za-z_][\w.]*)\s*;",
        RegexOptions.Compiled | RegexOptions.Multiline);

    static readonly Regex ProjectReference = new(
        @"<ProjectReference\s+Include\s*=\s*""(?<path>[^""]+)""", RegexOptions.Compiled);

    static readonly Regex ReferencedProject = new(
        @"(?<module>\w+)\.(?<layer>Domain|Application|Infrastructure|Contracts|Endpoints)\.csproj$", RegexOptions.Compiled);

    static readonly Regex VersionedPackageReference = new(
        @"<PackageReference\b[^>]*\bVersion\s*=\s*""[^""]*""", RegexOptions.Compiled);

    static readonly Regex EfPackageReference = new(
        @"<PackageReference\s+Include\s*=\s*""(?<pkg>Microsoft\.EntityFrameworkCore[\w.]*|Npgsql[\w.]*)""", RegexOptions.Compiled);

    static readonly Regex SchemaLiteral = new(
        @"(?:HasDefaultSchema\s*\(\s*""(?<schema>\w+)""|ToTable\s*\([^)]*,\s*""(?<schema>\w+)""\s*\))", RegexOptions.Compiled);

    static readonly Regex MigrateCall = new(@"\.\s*Migrate(?:Async)?\s*\(", RegexOptions.Compiled);

    static readonly Regex BuildRelevantFile = new(@"\.(cs|csproj|props|targets|slnx)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<string> CheckEdit(string projectDir, string toolName, JsonElement toolInput)
    {
        var filePath = toolInput.TryGetProperty("file_path", out var fp) ? fp.GetString() ?? "" : "";
        if (filePath.Length == 0)
        {
            return [];
        }

        var relative = Path.GetRelativePath(projectDir, filePath).Replace('\\', '/');
        if (relative.StartsWith("..") || Path.IsPathRooted(relative) || relative.StartsWith(".claude/"))
        {
            return [];
        }

        // Compare what the edit adds against what it removes, so an edit is only
        // blocked for violations it introduces.
        var (added, removed) = toolName switch
        {
            "Write" => (Get(toolInput, "content"), File.Exists(filePath) ? File.ReadAllText(filePath) : ""),
            "Edit" => (Get(toolInput, "new_string"), Get(toolInput, "old_string")),
            _ => MultiEditText(toolInput),
        };

        var introduced = Check(projectDir, relative, added).Except(Check(projectDir, relative, removed)).ToList();

        if (introduced.Count == 0 && BuildRelevantFile.IsMatch(relative))
        {
            MarkBuildPending(projectDir);
        }

        return introduced;

        static string Get(JsonElement e, string name)
            => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
    }

    static (string Added, string Removed) MultiEditText(JsonElement toolInput)
    {
        if (!toolInput.TryGetProperty("edits", out var edits) || edits.ValueKind != JsonValueKind.Array)
        {
            return ("", "");
        }

        var added = string.Join('\n', edits.EnumerateArray().Select(e => e.TryGetProperty("new_string", out var v) ? v.GetString() : ""));
        var removed = string.Join('\n', edits.EnumerateArray().Select(e => e.TryGetProperty("old_string", out var v) ? v.GetString() : ""));
        return (added, removed);
    }

    // Tells the Stop hook (BuildCheck) that a build is due before Claude finishes.
    static void MarkBuildPending(string projectDir)
    {
        try
        {
            var marker = BuildCheck.MarkerPath(projectDir);
            Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
            File.WriteAllText(marker, DateTimeOffset.UtcNow.ToString("O"));
        }
        catch (IOException)
        {
            // Best effort only.
        }
    }

    public static List<string> CheckCommand(string command)
    {
        var violations = new List<string>();
        if (!Regex.IsMatch(command, @"\bdotnet\s+ef\s+(migrations|database|dbcontext)\b"))
        {
            return violations;
        }

        if (Regex.IsMatch(command, @"src[\\/]+Host\b"))
        {
            violations.Add("dotnet ef must target a module's *.Infrastructure project, never src/Host (DESIGN.md section 3).");
        }

        if (!Regex.IsMatch(command, @"--project\s") || !Regex.IsMatch(command, @"--startup-project\s"))
        {
            violations.Add("dotnet ef needs both --project and --startup-project set to the same src/Modules/{Name}/{Name}.Infrastructure path.");
        }

        if (Regex.IsMatch(command, @"\bmigrations\s+add\b") && !Regex.IsMatch(command, @"--output-dir\s+[""']?Persistence[\\/]Migrations"))
        {
            violations.Add("dotnet ef migrations add needs --output-dir Persistence/Migrations so the migration lands next to the DbContext.");
        }

        return violations;
    }

    public static List<string> Check(string projectDir, string relative, string text)
    {
        var violations = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return violations;
        }

        var modules = DiscoverModules(projectDir);
        var moduleMatch = ModuleProjectPath.Match(relative);
        var module = moduleMatch.Success ? moduleMatch.Groups["module"].Value : null;
        var layer = moduleMatch.Success ? moduleMatch.Groups["layer"].Value : null;
        var isHost = relative.StartsWith("src/Host/");
        var isBuildingBlocks = relative.StartsWith("src/BuildingBlocks/");
        var isSource = relative.StartsWith("src/");

        if (relative.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            if (VersionedPackageReference.IsMatch(text))
            {
                violations.Add($"{relative}: <PackageReference> must not carry a Version (Central Package Management). Set it in Directory.Packages.props.");
            }

            foreach (Match reference in ProjectReference.Matches(text))
            {
                var target = ReferencedProject.Match(reference.Groups["path"].Value.Replace('\\', '/'));
                if (!target.Success)
                {
                    continue;
                }

                var targetModule = target.Groups["module"].Value;
                var targetLayer = target.Groups["layer"].Value;

                if (isBuildingBlocks)
                {
                    violations.Add($"{relative}: BuildingBlocks must not reference any module ({targetModule}.{targetLayer}).");
                }
                else if (module is not null && targetModule != module && (targetLayer != "Contracts" || layer == "Domain"))
                {
                    violations.Add(layer == "Domain"
                        ? $"{relative}: {module}.Domain has zero references to other modules ({targetModule}.{targetLayer})."
                        : $"{relative}: {module} may only reference {targetModule}.Contracts, not {targetModule}.{targetLayer}.");
                }
                else if (module is not null && targetModule == module)
                {
                    var forbidden = layer switch
                    {
                        "Domain" => true,
                        "Contracts" => true,
                        "Application" => targetLayer is "Infrastructure" or "Endpoints",
                        _ => false,
                    };
                    if (forbidden)
                    {
                        violations.Add($"{relative}: {module}.{layer} must not reference {module}.{targetLayer}.");
                    }
                }
            }

            if (layer == "Application")
            {
                foreach (Match package in EfPackageReference.Matches(text))
                {
                    violations.Add($"{relative}: {module}.Application must not reference {package.Groups["pkg"].Value}; depend on I{module}DbContext instead.");
                }
            }

            return violations;
        }

        if (!relative.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || relative.Contains("/Migrations/") || !isSource)
        {
            return violations;
        }

        foreach (Match directive in UsingDirective.Matches(text))
        {
            var ns = directive.Groups["ns"].Value;
            var segments = ns.Split('.');
            var nsModule = segments.Length >= 2 && modules.Contains(segments[0]) && Layers.Contains(segments[1]) ? segments[0] : null;
            var nsLayer = nsModule is not null ? segments[1] : null;

            if (isBuildingBlocks && nsModule is not null)
            {
                violations.Add($"{relative}: BuildingBlocks must stay free of module code (using {ns}).");
            }
            else if (isHost && nsLayer is "Application" or "Domain" or "Infrastructure")
            {
                violations.Add($"{relative}: Host may only use a module's IModule (Endpoints) or Contracts, not {ns}.");
            }
            else if (module is not null && nsModule is not null && nsModule != module && nsLayer != "Contracts")
            {
                violations.Add($"{relative}: {module} may only use {nsModule}.Contracts, not {ns}.");
            }
            else if (module is not null)
            {
                var ownLayer = nsModule == module ? nsLayer : null;
                var reason = layer switch
                {
                    "Domain" when IsFrameworkNamespace(ns) => "Domain has zero framework dependencies",
                    "Domain" when nsModule is not null && nsModule != module => "Domain has zero references to other modules",
                    "Domain" when ownLayer is not null and not "Domain" => "Domain must not depend on other layers",
                    "Application" when ns.StartsWith("Microsoft.EntityFrameworkCore") || ns.StartsWith("Npgsql") => $"Application never references EF Core; use I{module}DbContext",
                    "Application" when ownLayer is "Infrastructure" or "Endpoints" => "Application must not depend on Infrastructure/Endpoints",
                    "Contracts" when ownLayer is not null and not "Contracts" => "Contracts is the public surface and must not expose internals",
                    _ => null,
                };
                if (reason is not null)
                {
                    violations.Add($"{relative}: {reason} (using {ns}).");
                }
            }
        }

        if (isHost && MigrateCall.IsMatch(text))
        {
            violations.Add($"{relative}: never call Database.Migrate()/MigrateAsync() at startup; ship scripts/migrations/*.sql instead (DESIGN.md section 3).");
        }

        if (Regex.IsMatch(text, @"\bTransactionScope\b"))
        {
            violations.Add($"{relative}: TransactionScope is not allowed; use the outbox or a saga for cross-module work (DESIGN.md section 4).");
        }

        if (layer == "Infrastructure")
        {
            foreach (Match schema in SchemaLiteral.Matches(text))
            {
                var value = schema.Groups["schema"].Value;
                if (modules.Any(m => m.Equals(value, StringComparison.OrdinalIgnoreCase) && m != module))
                {
                    violations.Add($"{relative}: {module}'s DbContext must not map into the '{value}' schema; each module owns exactly one schema.");
                }
            }
        }

        return violations;
    }

    static bool IsFrameworkNamespace(string ns)
        => ns.StartsWith("Microsoft.EntityFrameworkCore") || ns.StartsWith("Microsoft.AspNetCore")
           || ns.StartsWith("MediatR") || ns.StartsWith("FluentValidation") || ns.StartsWith("Npgsql");

    static HashSet<string> DiscoverModules(string projectDir)
    {
        var modulesDir = Path.Combine(projectDir, "src", "Modules");
        return Directory.Exists(modulesDir)
            ? Directory.GetDirectories(modulesDir).Select(Path.GetFileName).OfType<string>().ToHashSet()
            : [];
    }

    public static int Scan(string projectDir)
    {
        var files = new[] { "src", "tests" }
            .Select(dir => Path.Combine(projectDir, dir))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".csproj"))
            .Where(f => !Regex.IsMatch(f.Replace('\\', '/'), "/(bin|obj)/"));

        var violations = files
            .SelectMany(f => Check(projectDir, Path.GetRelativePath(projectDir, f).Replace('\\', '/'), File.ReadAllText(f)))
            .ToList();

        foreach (var violation in violations)
        {
            Console.WriteLine(violation);
        }

        Console.WriteLine(violations.Count == 0 ? "No module-boundary violations found." : $"{violations.Count} violation(s) found.");
        return violations.Count == 0 ? 0 : 1;
    }
}

static class BuildCheck
{
    public static string MarkerPath(string projectDir) => Path.Combine(projectDir, ".claude", "hooks", ".state", "build-pending");

    // Exit 2 keeps Claude working and feeds it the compiler errors. On the
    // retry (stop_hook_active) a second failure only warns the user, so a build
    // Claude can't fix never traps it in a loop.
    public static int Run(string projectDir, bool stopHookActive)
    {
        var marker = MarkerPath(projectDir);
        if (!File.Exists(marker))
        {
            return 0;
        }

        var startInfo = new ProcessStartInfo("dotnet", ["build", "OrderPoint.slnx", "-nologo", "-v:q", "-tl:off"])
        {
            WorkingDirectory = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(startInfo)!;
        var stderrTask = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd() + stderrTask.Result;
        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            File.Delete(marker);
            return 0;
        }

        var errors = output.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Contains(": error "))
            .Distinct()
            .Take(30)
            .ToList();
        var summary = errors.Count > 0 ? string.Join('\n', errors) : output.Trim();

        if (stopHookActive)
        {
            Console.WriteLine(JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["systemMessage"] = $"dotnet build still fails after Claude's changes:\n{summary}",
            }));
            return 0;
        }

        Console.Error.WriteLine("dotnet build failed after this turn's edits. Fix these before finishing:");
        Console.Error.WriteLine(summary);
        Console.Error.WriteLine("(MSB3021/MSB3027 file-lock errors usually mean src/Host is still running; say so instead of editing code.)");
        return 2;
    }
}

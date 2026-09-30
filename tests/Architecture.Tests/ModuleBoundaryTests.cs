using System.Reflection;
using System.Text.RegularExpressions;
using BuildingBlocks.Messaging;
using BuildingBlocks.Modules;

namespace Architecture.Tests;

/// <summary>
/// Compiled-assembly checks for the rules in CLAUDE.md / DESIGN.md. They
/// complement .claude/hooks/guard.cs (which checks source) and run in CI, so a
/// boundary violation fails the build no matter which editor introduced it.
/// </summary>
public sealed partial class ModuleBoundaryTests
{
    public static readonly string[] Modules = ["Identity", "Catalog", "Inventory", "Sales", "Notifications", "Reporting"];
    private static readonly string[] Layers = ["Domain", "Application", "Infrastructure", "Contracts", "Endpoints"];

    public static TheoryData<string> ModuleNames() => [.. Modules];

    private static Assembly Load(string module, string layer) => Assembly.Load($"{module}.{layer}");

    private static IEnumerable<string> ReferencedNames(Assembly assembly)
        => assembly.GetReferencedAssemblies().Select(a => a.Name!);

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_ReferencesOtherModules_OnlyThroughContracts(string module)
    {
        foreach (var layer in Layers)
        {
            var offending = ReferencedNames(Load(module, layer))
                .Where(name => Modules.Any(other => other != module && name.StartsWith($"{other}.")) && !name.EndsWith(".Contracts"))
                .ToList();

            Assert.True(offending.Count == 0, $"{module}.{layer} references {string.Join(", ", offending)}; only other modules' *.Contracts are allowed.");
        }
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Domain_HasNoFrameworkOrOtherModuleDependencies(string module)
    {
        var forbidden = ReferencedNames(Load(module, "Domain"))
            .Where(name => name.StartsWith("Microsoft.EntityFrameworkCore") || name.StartsWith("Npgsql")
                           || name.StartsWith("MediatR") || name.StartsWith("FluentValidation")
                           || Modules.Any(m => name.StartsWith($"{m}.")))
            .ToList();

        Assert.Empty(forbidden);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Application_DoesNotDependOnEfCoreOrItsOwnInfrastructure(string module)
    {
        var forbidden = ReferencedNames(Load(module, "Application"))
            .Where(name => name.StartsWith("Microsoft.EntityFrameworkCore") || name.StartsWith("Npgsql")
                           || name == "BuildingBlocks.Persistence"
                           || name == $"{module}.Infrastructure" || name == $"{module}.Endpoints")
            .ToList();

        Assert.Empty(forbidden);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Contracts_DependOnlyOnBuildingBlocks(string module)
    {
        var forbidden = ReferencedNames(Load(module, "Contracts"))
            .Where(name => Modules.Any(m => name.StartsWith($"{m}.")) || name.StartsWith("Microsoft.EntityFrameworkCore"))
            .ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void BuildingBlocks_KnowNothingAboutModules()
    {
        foreach (var assembly in new[] { Assembly.Load("BuildingBlocks"), Assembly.Load("BuildingBlocks.Persistence") })
        {
            Assert.DoesNotContain(ReferencedNames(assembly), name => Modules.Any(m => name.StartsWith($"{m}.")));
        }
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void IntegrationEvents_LiveInContracts_AndAreSealedRecords(string module)
    {
        foreach (var layer in Layers.Where(l => l != "Contracts"))
        {
            Assert.DoesNotContain(Load(module, layer).GetTypes(), t => t.IsClass && typeof(IIntegrationEvent).IsAssignableFrom(t));
        }

        foreach (var eventType in Load(module, "Contracts").GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t) && !t.IsInterface))
        {
            Assert.True(eventType.IsSealed, $"{eventType.Name} should be sealed.");
            Assert.NotNull(eventType.GetMethod("<Clone>$")); // compiler-generated for records
        }
    }

    [Fact]
    public void EveryModule_IsRegistered_AndOwnsCorrectlyNamedPermissions()
    {
        var modules = Modules
            .Select(m => (IModule)Activator.CreateInstance(Load(m, "Endpoints").GetType($"{m}.Endpoints.{m}Module", throwOnError: true)!)!)
            .ToList();

        var allNames = modules.SelectMany(m => m.Permissions).Select(p => p.Name).ToList();
        Assert.Equal(allNames.Count, allNames.Distinct().Count());

        foreach (var module in modules)
        {
            Assert.NotEmpty(module.Permissions);
            foreach (var permission in module.Permissions)
            {
                Assert.Matches(PermissionName(), permission.Name);
                Assert.StartsWith($"{module.Name.ToLowerInvariant()}:", permission.Name);
            }
        }
    }

    [GeneratedRegex("^[a-z]+:[a-z]+:[a-z]+$")]
    private static partial Regex PermissionName();
}

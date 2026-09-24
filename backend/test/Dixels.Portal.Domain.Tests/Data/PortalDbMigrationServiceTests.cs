using System;
using System.IO;
using Shouldly;
using Xunit;

namespace Dixels.Portal.Data;

/* The search starts from the migrator's working directory and walks up through its parents. */
public class PortalDbMigrationServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sln-search-" + Guid.NewGuid().ToString("N"));
    private readonly string _start;

    public PortalDbMigrationServiceTests()
    {
        _start = Path.Combine(_root, "src", "App.DbMigrator", "bin");
        Directory.CreateDirectory(_start);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData("App.sln")]
    [InlineData("App.slnx")]
    [InlineData("App.SLNX")]
    public void Finds_the_solution_folder_for_either_format(string solutionFile)
    {
        File.WriteAllText(Path.Combine(_root, solutionFile), "");

        PortalDbMigrationService.FindSolutionDirectory(_start).ShouldBe(_root);
    }

    [Fact]
    public void Ignores_files_that_only_start_with_a_solution_extension()
    {
        File.WriteAllText(Path.Combine(_root, "App.sln.DotSettings"), "");

        PortalDbMigrationService.FindSolutionDirectory(_start).ShouldNotBe(_root);
    }
}

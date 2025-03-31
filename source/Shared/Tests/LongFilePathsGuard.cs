// Copyright 2020 Energinet DataHub A/S
//
// Licensed under the Apache License, Version 2.0 (the "License2");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Reflection;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Shared.Tests;

/// <summary>
/// This test is here to ensure that Visual Studio can compile the code.
/// The reason being that Visual studio has a limit on the path lengths
/// (https://developercommunity.visualstudio.com/t/allow-building-running-and-debugging-a-net-applica/351628).
/// <remarks>
/// The path is dependent of the local location of the repository.
/// Hence it might fail locally but not in CI. If this happens, consider moving the repository to a shorter path.
/// If the max length needs to be altered, ensure that one of your Visual studio buddies can compile the code.
/// </remarks>
/// </summary>
public class LongFilePathsAreNotAllowed
{
    private static readonly int _maxPathLength = 270;
    private static readonly string _sourceFolder = "source";
    private static readonly string _assemblyPrefix = "Energinet.DataHub.";

    [Fact]
    public void Given_Path_When_CheckingPathFromDiskToClass_Then_PathMustBeLessThan270Characters()
    {
        var assembly = Assembly.GetAssembly(typeof(LongFilePathsAreNotAllowed))!;

        var types = assembly.GetTypes();

        string? diskToProjectPath = null;

        foreach (var type in types)
        {
            diskToProjectPath ??= GetDiskToProjectPath(type);

            var pathFromProjectToClass = GetPathFromProjectToClass(type);

            var fullPathForFile = Path.Combine(diskToProjectPath, pathFromProjectToClass);
            Assert.True(
                fullPathForFile.Length < _maxPathLength,
#pragma warning disable SA1118
                $"The file path: ${fullPathForFile} is too long\n"
                + $"It consists of {fullPathForFile.Length} characters but it should contain no more than {_maxPathLength}.\n"
                + $"Visual Studio can not handle it,"
                + $"please shorten the path and namespace: {pathFromProjectToClass}"
                + $"for the class: {type.Name}");
#pragma warning restore SA1118
        }
    }

    private static string GetDiskToProjectPath(Type type)
    {
        var fullPath = Path.GetFullPath(type.FullName!);

        var indexOfTestsPostfix = fullPath.IndexOf(_sourceFolder, StringComparison.Ordinal);
        return fullPath.Substring(0, indexOfTestsPostfix + _sourceFolder.Length);
    }

    private static string GetPathFromProjectToClass(Type type)
    {
        var projectToTypePath = type.Namespace != null
            ? type.Namespace.Substring(_assemblyPrefix.Length)
            : string.Empty;

        // We may have a method name in "type.Name", if so, it will start with "<".
        // These methods are not relevant for this test.
        if (type.Name.Contains("<"))
        {
            return projectToTypePath;
        }

        return Path.Combine(projectToTypePath, type.Name);
    }
}

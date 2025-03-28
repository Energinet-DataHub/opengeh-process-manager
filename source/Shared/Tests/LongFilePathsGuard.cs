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

public class LongFilePathsAreNotAllowed
{
    private static readonly int _maxPathLength = 270;
    private static readonly string _sourceFolder = "source";
    private static readonly string _assemblyPrefix = "Energinet.DataHub.";

    [Fact]
    public void Given_PathFromDiskToClass_When_CheckingLength_Then_MayConsistsOfLessThan270Characters()
    {
        var assembly = Assembly.GetAssembly(typeof(LongFilePathsAreNotAllowed))!;

        var types = assembly.GetTypes();

        string? diskToProjectPath = null;

        foreach (var type in types)
        {
            // returns C:\git\opengeh-process-manager\source
            diskToProjectPath ??= GetDiskToProjectPath(type);

            // returns ProcessManager.Orchestrations.Processes.BRS_045.MissingMeasurementsLogCalculation.V1.Activities.CalculationStep\CalculationStepGetJobRunStatusActivity_Brs_045_MissingMeasurementsLogCalculation_V1
            var pathFromProjectToClass = GetPathFromProjectToClass(type);

            // returns C:\git\opengeh-process-manager\source\ProcessManager.Orchestrations.Processes.BRS_045.MissingMeasurementsLogCalculation.V1.Activities.CalculationStep\CalculationStepGetJobRunStatusActivity_Brs_045_MissingMeasurementsLogCalculation_V1
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
        // The full path contains the bin, netX.0 and the like
        // e.g. C:\git\opengeh-process-manager\source\ProcessManager.Orchestrations.Tests\bin\Debug\net8.0\
        // Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.MissingMeasurementsLogCalculation.V1.Activities.CalculationStep.CalculationStepGetJobRunStatusActivity_Brs_045_MissingMeasurementsLogCalculation_V1
        var fullPath = Path.GetFullPath(type.FullName!);

        // returns C:\git\opengeh-process-manager\source
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

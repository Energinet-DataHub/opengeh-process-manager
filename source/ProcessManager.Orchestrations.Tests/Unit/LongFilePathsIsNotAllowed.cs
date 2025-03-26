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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit;

public class LongFilePathsIsNotAllowed
{
    private readonly int _maxPathLength = 270;
    private readonly string _testsPostfix = "source";
    private readonly string _classNamePrefix = "Energinet.DataHub.";

    [Fact]
    public void NamespaceMustContainLessThan270Characters()
    {
        var assembly = Assembly.GetAssembly(typeof(LongFilePathsIsNotAllowed))!;

        var types = assembly.GetTypes();

        string? diskToProjectPath = null;

        foreach (var type in types)
        {
            if (diskToProjectPath == null)
            {
                // This path contains the bin, netX.0 and the like
                // e.g. C:\git\opengeh-process-manager\source\ProcessManager.Orchestrations.Tests\bin\Debug\net8.0\
                // Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.MissingMeasurementsLogCalculation.V1.Activities.CalculationStep.CalculationStepGetJobRunStatusActivity_Brs_045_MissingMeasurementsLogCalculation_V1
                var fullPath = Path.GetFullPath(type.FullName!);

                // returns C:\\git\\opengeh-process-manager\\source
                var indexOfTestsPostfix = fullPath.IndexOf(_testsPostfix, StringComparison.Ordinal);
                diskToProjectPath = fullPath.Substring(0, indexOfTestsPostfix + _testsPostfix.Length);
            }

            var projectToTypePath = type.Namespace != null
                    ? type.Namespace.Substring(_classNamePrefix.Length)
                    : string.Empty;

            //returns ProcessManager.Orchestrations.Processes.BRS_045.MissingMeasurementsLogCalculation.V1.Activities.CalculationStep\CalculationStepGetJobRunStatusActivity_Brs_045_MissingMeasurementsLogCalculation_V1
            var pathFromProjectToClass = string.Empty;

            // We may have a method name in "type.Name", if so, it will start with "<".
            // These methods are not relevant for this test.
            if (type.Name.Contains("<"))
            {
                pathFromProjectToClass = projectToTypePath;
            }
            else
            {
                pathFromProjectToClass = Path.Combine(projectToTypePath, type.Name);
            }

            // returns C:\\git\\opengeh-process-manager\\source\\ProcessManager.Orchestrations.Processes.BRS_045.MissingMeasurementsLogCalculation.V1.Activities.CalculationStep\CalculationStepGetJobRunStatusActivity_Brs_045_MissingMeasurementsLogCalculation_V1
            var fullPathOfFile = Path.Combine(diskToProjectPath, pathFromProjectToClass);
            Assert.True(
                fullPathOfFile.Length < _maxPathLength,
#pragma warning disable SA1118
                $"The file path: ${fullPathOfFile} is to long\n"
                + $"It consists of {fullPathOfFile.Length} characters but it should contain no more than {_maxPathLength}.\n"
                + $"Visual Studio can not handle it, please refactor the namespace {pathFromProjectToClass}");
#pragma warning restore SA1118
        }
    }
}

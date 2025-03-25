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
using System.Runtime.CompilerServices;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit;

public class LongFileNamesTests
{
    private readonly int _maxPathLength = 260;
    private readonly string _testsPostfix = "source";
    private readonly string _classNamePrefix = "Energinet.DataHub.";

    [Fact]
    public void NamespaceMustContainLessThan256Characters()
    {
        var assembly = Assembly.GetAssembly(typeof(LongFileNamesTests))!;

        var classes = assembly.GetTypes();

        string? diskToProjectPath = null;

        foreach (var type in classes)
        {
            if (diskToProjectPath == null)
            {
                // This path contains the bin, netX.0 and the like, folders
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
                $"The file path: ${fullPathOfFile} is to long \n"
                + $"It consists of {fullPathOfFile.Length} characters but it should contain no more than {_maxPathLength}. \n"
                + $"Visual Studio can not handle it, please refactor the namespace {pathFromProjectToClass}");
#pragma warning restore SA1118
        }
    }
}

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

using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Extensions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeasurements.V1.Extensions;

public class InstantPatternWithOptionalSecondsTests
{
    [Theory]
    [InlineData("2025-03-17T11:13Z", true)] // Valid Cim format
    [InlineData("2025-03-17T11:13:48Z", true)] // Valid Ebix format
    [InlineData("2025-03-17T11:13:48.1955802Z", true)] // Valid Ebix format
    [InlineData("2025-03-17T11:13:48.1955802", false)]
    [InlineData("2025-03-17 11:13:48.1955802Z", false)]
    [InlineData("2025-03-17T11:13:48.1955802+00:00", false)]
    [InlineData("2025-03-31T13:43:48.8975122+02:00", false)]
    public void Parse_ShouldReturnExpectedResult(string dateTime, bool isValid)
    {
        // Act
        var result = InstantPatternWithOptionalSeconds.Parse(dateTime);

        // Assert
        if (isValid)
        {
            Assert.True(result.Success);
        }
        else
        {
            Assert.False(result.Success);
        }
    }
}

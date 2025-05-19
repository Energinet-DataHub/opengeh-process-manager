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

using Energinet.DataHub.Core.TestCommon.Xunit.Attributes;
using Energinet.DataHub.Core.TestCommon.Xunit.Orderers;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures;
using Energinet.DataHub.ProcessManager.SubsystemTests.Processes.Shared;
using NodaTime;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.SubsystemTests.Processes.BRS_045.MissingMeasurementsLogOnDemand.V1;

[TestCaseOrderer(
    ordererTypeName: TestCaseOrdererLocation.OrdererTypeName,
    ordererAssemblyName: TestCaseOrdererLocation.OrdererAssemblyName)]
[CollectionDefinition("Process Manger collection")]
public class MissingMeasurementsLogOnDemandCalculationScenario
    : CalculationScenario<MissingMeasurementsLogOnDemandCalculationScenarioState>,
        ICollectionFixture<ProcessManagerFixture>,
        IAsyncLifetime
{
    public MissingMeasurementsLogOnDemandCalculationScenario(
        ProcessManagerFixture fixture,
        ITestOutputHelper testOutputHelper,
        IClock clock)
        : base(fixture, testOutputHelper)
    {
        var periodStart = clock.GetCurrentInstant().ToDateTimeOffset();
        var periodEnd = periodStart.AddDays(1);
        var gridAreaCodes = new[] { "301" };

        Fixture.TestConfiguration = new MissingMeasurementsLogOnDemandCalculationScenarioState(
            startCommand: new StartMissingMeasurementsLogOnDemandCalculationCommandV1(
                Fixture.UserIdentity,
                new CalculationInputV1(periodStart, periodEnd, gridAreaCodes)));
    }

    [SubsystemFact]
    [ScenarioStep(5)]
    public void AndGiven_Start()
    {
        Assert.True(true);
    }
}

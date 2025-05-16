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

using Energinet.DataHub.Core.TestCommon.Xunit.Orderers;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_045.MissingMeasurementsLogCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.SubsystemTests.Processes.BRS_045.MissingMeasurementsLog.V1;

[TestCaseOrderer(
    ordererTypeName: TestCaseOrdererLocation.OrdererTypeName,
    ordererAssemblyName: TestCaseOrdererLocation.OrdererAssemblyName)]
public class MissingMeasurementsLogCalculationScenario
    : CalculationScenario<MissingMeasurementsLogCalculationScenarioState>, IClassFixture<ProcessManagerFixture<MissingMeasurementsLogCalculationScenarioState>>,
        IAsyncLifetime
{
    public MissingMeasurementsLogCalculationScenario(
        ProcessManagerFixture<MissingMeasurementsLogCalculationScenarioState> fixture,
        ITestOutputHelper testOutputHelper)
        : base(fixture, testOutputHelper)
    {
        Fixture.TestConfiguration = new MissingMeasurementsLogCalculationScenarioState(
            startCommand: new StartMissingMeasurementsLogCalculationCommandV1(Fixture.UserIdentity));
    }
}

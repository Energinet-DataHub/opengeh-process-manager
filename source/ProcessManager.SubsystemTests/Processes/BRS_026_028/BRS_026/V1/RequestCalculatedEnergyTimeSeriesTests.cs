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

using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_026.V1.Model;
using Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures;

namespace Energinet.DataHub.ProcessManager.SubsystemTests.Processes.BRS_026_028.BRS_026.V1;

public class RequestCalculatedEnergyTimeSeriesTests : IClassFixture<ProcessManagerFixture>
{
    private readonly ProcessManagerFixture _fixture;

    public RequestCalculatedEnergyTimeSeriesTests(ProcessManagerFixture fixture)
    {
        _fixture = fixture;
        _fixture.TestConfiguration = new RequestCalculatedEnergyTimeSeriesTestConfiguration();
    }

    public RequestCalculatedEnergyTimeSeriesTestConfiguration TestConfiguration =>
        _fixture.TestConfiguration as RequestCalculatedEnergyTimeSeriesTestConfiguration
        ?? throw new InvalidOperationException($"{nameof(_fixture.TestConfiguration)} is not {nameof(RequestCalculatedEnergyTimeSeriesTestConfiguration)}");

    [Fact]
    public void Given_RequestCalculatedEnergyTimeSeries()
    {
        TestConfiguration.Request = new RequestCalculatedEnergyTimeSeriesCommandV1(
            operatingIdentity: _fixture.EnergySupplierActorIdentity,
            inputParameter: new RequestCalculatedEnergyTimeSeriesInputV1(
                ActorMessageId: Guid.NewGuid().ToString(),
                TransactionId: Guid.NewGuid().ToString(),
                RequestedForActorNumber: _fixture.EnergySupplierActorIdentity.ActorNumber.Value,
                RequestedForActorRole: _fixture.EnergySupplierActorIdentity.ActorRole.Name,
                RequestedByActorNumber: _fixture.EnergySupplierActorIdentity.ActorNumber.Value,
                RequestedByActorRole: _fixture.EnergySupplierActorIdentity.ActorRole.Name,
                BusinessReason: BusinessReason.BalanceFixing.Name,
                PeriodStart: new DateTimeOffset(2025, 03, 07, 23, 00, 00, TimeSpan.Zero).ToString(),
                PeriodEnd: new DateTimeOffset(2025, 03, 09, 23, 00, 00, TimeSpan.Zero).ToString(),
                EnergySupplierNumber: _fixture.EnergySupplierActorIdentity.ActorNumber.Value,
                BalanceResponsibleNumber: null,
                GridAreas: ["804"],
                MeteringPointType: null,
                SettlementMethod: null,
                SettlementVersion: null),
            idempotencyKey: Guid.NewGuid().ToString());
    }

    [Fact]
    public async Task When_RequestIsReceived()
    {
        await _fixture.ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(
            TestConfiguration.Request,
            CancellationToken.None);
    }
}

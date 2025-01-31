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

using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using FluentAssertions;
using ActorNumber =
    Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.ActorNumber;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.V1.BusinessValidation.Rules;

public class MeteringPointValidationRuleTests
{
    private readonly MeteringPointValidationRule _sut = new();

    [Fact]
    public async Task Given_NoMeteringPointMasterData_When_Validate_Then_ValidationError()
    {
        var input = MeteredDataForMeteringPointMessageInputV1Builder.Build();

        var result = await _sut.ValidateAsync(new(input, []));

        result.Should()
            .NotBeEmpty()
            .And.Contain(
                ve => ve.ErrorCode == "E10"
                      && ve.Message == "Målepunktet findes ikke / The metering point does not exist");
    }

    [Fact]
    public async Task Given_MeteringPointMasterData_When_Validate_Then_NoValidationError()
    {
        var input = MeteredDataForMeteringPointMessageInputV1Builder.Build();

        var result = await _sut.ValidateAsync(
            new(
                input,
                [
                    new MeteringPointMasterData(
                        new MeteringPointId("id"),
                        new GridAreaCode("111"),
                        new ActorNumber("1111111111111"),
                        ConnectionState.Connected,
                        MeteringPointType.Production,
                        MeteringPointSubType.Physical,
                        MeasurementUnit.KilowattHour),
                ]));

        result.Should()
            .NotContain(
                ve => ve.ErrorCode == "E10"
                      && ve.Message == "Målepunktet findes ikke / The metering point does not exist");
    }
}

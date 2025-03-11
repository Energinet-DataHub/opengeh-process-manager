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
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;

public class ForwardMeteredDataBusinessValidatedDtoValidatorTests
{
    private readonly BusinessValidator<ForwardMeteredDataBusinessValidatedDto> _sut;

    public ForwardMeteredDataBusinessValidatedDtoValidatorTests()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddLogging();

        var orchestrationsAssembly = typeof(OrchestrationDescriptionBuilderV1).Assembly;
        var orchestrationsAbstractionsAssembly =
            typeof(ForwardMeteredDataBusinessValidatedDto).Assembly;
        services.AddBusinessValidation(assembliesToScan: [orchestrationsAssembly, orchestrationsAbstractionsAssembly]);

        var serviceProvider = services.BuildServiceProvider();

        _sut = serviceProvider
            .GetRequiredService<BusinessValidator<ForwardMeteredDataBusinessValidatedDto>>();
    }

    [Fact]
    public async Task Given_ValidForwardMeteredDataBusinessValidatedDto_When_Validate_Then_NoValidationError()
    {
        var input = MeteredDataForMeteringPointMessageInputV1Builder.Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: [
                    new MeteringPointMasterData(
                        MeteringPointId: new MeteringPointId(input.MeteringPointId!),
                        GridAreaCode: new GridAreaCode("804"),
                        GridAccessProvider: new ActorNumber(input.GridAccessProviderNumber),
                        ConnectionState: ConnectionState.Connected,
                        MeteringPointType: MeteringPointType.FromName(input.MeteringPointType!),
                        MeteringPointSubType: MeteringPointSubType.Physical,
                        MeasurementUnit: MeasurementUnit.FromName(input.MeasureUnit!)),
                ]));

        result.Should().BeEmpty();
    }

    [Fact(Skip = "'Metering point doesn't exists' validation is currently disabled")]
    public async Task Given_NoMasterData_When_Validate_Then_ValidationError()
    {
        var input = MeteredDataForMeteringPointMessageInputV1Builder.Build();

        var result = await _sut.ValidateAsync(
            new ForwardMeteredDataBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: []));

        result.Should()
            .NotBeEmpty()
            .And.Contain(
                ve => ve.ErrorCode == "E10"
                      && ve.Message == "Målepunktet findes ikke / The metering point does not exist");
    }
}

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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.Orchestration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_024.V1.BusinessValidation;

public class RequestYearlyMeasurementsBusinessValidationTests
{
    private readonly BusinessValidator<RequestYearlyMeasurementsBusinessValidatedDto> _sut;

    public RequestYearlyMeasurementsBusinessValidationTests()
    {
        IServiceCollection services = new ServiceCollection();

        var orchestrationsAssembly = typeof(OrchestrationDescriptionBuilder).Assembly;
        var orchestrationsAbstractionsAssembly =
            typeof(RequestYearlyMeasurementsBusinessValidatedDto).Assembly;
        services.AddBusinessValidation(assembliesToScan: [orchestrationsAssembly, orchestrationsAbstractionsAssembly]);

        var serviceProvider = services.BuildServiceProvider();
        _sut = serviceProvider
        .GetRequiredService<BusinessValidator<RequestYearlyMeasurementsBusinessValidatedDto>>();
    }

    [Fact]
    public async Task Given_ValidRequestYearlyMeasurementsBusinessValidatedDto_When_Validate_Then_NoValidationError()
    {
        var input = new RequestYearlyMeasurementsInputV1Builder()
            .Build();

        var meteringPointMasterData = new MeteringPointMasterDataBuilder()
            .BuildFromInput(input);

        var result = await _sut.ValidateAsync(
            new RequestYearlyMeasurementsBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: meteringPointMasterData));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_InvalidMeteringPointType_When_Validate_Then_ValidationError()
    {
        var input = new RequestYearlyMeasurementsInputV1Builder()
            .Build();

        var invalidMeteringPointType = MeteringPointType.Exchange;
        var meteringPointMasterData = new MeteringPointMasterDataBuilder()
            .BuildFromInput(
                input,
                meteringPointType: invalidMeteringPointType);

        var result = await _sut.ValidateAsync(
            new RequestYearlyMeasurementsBusinessValidatedDto(
                Input: input,
                MeteringPointMasterData: meteringPointMasterData));

        result.Should()
            .ContainSingle()
            .And.BeEquivalentTo(MeteringPointTypeValidationRule.WrongMeteringPointTypeError);
    }
}

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

using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;

public class Brs021_ForwardMeteredData_General_BusinessValidationDtoValidatorTests
{
    private readonly BusinessValidator<Brs021_ForwardMeteredData_General_BusinessValidationDto> _sut;

    public Brs021_ForwardMeteredData_General_BusinessValidationDtoValidatorTests()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddLogging();

        var orchestrationsAssembly = typeof(Orchestration_Brs_021_ForwardMeteredData_V1).Assembly;
        var orchestrationsAbstractionsAssembly =
            typeof(Brs021_ForwardMeteredData_General_BusinessValidationDto).Assembly;
        services.AddBusinessValidation(assembliesToScan: [orchestrationsAssembly, orchestrationsAbstractionsAssembly]);

        var serviceProvider = services.BuildServiceProvider();

        _sut = serviceProvider
            .GetRequiredService<BusinessValidator<Brs021_ForwardMeteredData_General_BusinessValidationDto>>();
    }

    [Fact]
    public async Task Given_Input_When_Validate_Then_NoValidationErrors()
    {
        var input = MeteredDataForMeteringPointMessageInputV1Builder.Build();

        var result = await _sut.ValidateAsync(new(input, []));

        result.Should().BeEmpty();
    }
}

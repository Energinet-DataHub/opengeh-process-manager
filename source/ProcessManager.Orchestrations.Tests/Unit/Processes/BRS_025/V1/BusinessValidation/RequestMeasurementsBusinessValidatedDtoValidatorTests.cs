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
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_025.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_025.V1.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_025.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_025.V1.Orchestration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NodaTime;
using NodaTime.Text;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_025.V1.BusinessValidation;

public class RequestMeasurementsBusinessValidatedDtoValidatorTests
{
    private readonly Mock<IClock> _clockMock = new();
    private readonly Mock<IOptions<ProcessManagerComponentsOptions>> _optionsMock = new();
    private readonly DateTimeZone _timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")!;

    private readonly BusinessValidator<RequestMeasurementsBusinessValidatedDto> _sut;

    public RequestMeasurementsBusinessValidatedDtoValidatorTests()
    {
        _clockMock.Setup(c => c.GetCurrentInstant())
            .Returns(Instant.FromUtc(2024, 11, 15, 16, 46, 43));

        var expectedOptions = new ProcessManagerComponentsOptions { AllowMockDependenciesForTests = false, };

        _optionsMock.Setup(o => o.Value).Returns(expectedOptions);

        IServiceCollection services = new ServiceCollection();

        services.AddLogging();
        services.AddTransient<DateTimeZone>(s => _timeZone);
        services.AddTransient<IClock>(s => _clockMock.Object);
        services.AddTransient<IOptions<ProcessManagerComponentsOptions>>(s => _optionsMock.Object);

        var orchestrationsAssembly = typeof(OrchestrationDescriptionBuilder).Assembly;
        var orchestrationsAbstractionsAssembly =
            typeof(RequestMeasurementsBusinessValidatedDto).Assembly;
        services.AddBusinessValidation(assembliesToScan: [orchestrationsAssembly, orchestrationsAbstractionsAssembly]);

        var serviceProvider = services.BuildServiceProvider();

        _sut = serviceProvider
            .GetRequiredService<BusinessValidator<RequestMeasurementsBusinessValidatedDto>>();
    }

    [Fact]
    public async Task Given_InvalidPeriod_When_ValidateAsync_Then_Error()
    {
        // Arrange
        var request = new RequestMeasurementsBusinessValidatedDto(
            new RequestMeasurementsInputV1(
                "1234",
                "1234",
                "1111111111111",
                "DDQ",
                "2222222222222",
                "2024-01-01T23:00:00Z",
                "2023-12-01T23:00:00Z"),
            []);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.Should().ContainSingle();
        result.Single()
            .Message.Should()
            .Be(
                PeriodValidationRule.StartDateAfterEndDate(
                    InstantPattern.General.Parse("2024-01-01T23:00:00Z").Value,
                    InstantPattern.General.Parse("2023-12-01T23:00:00Z").Value));

        result.Single().ErrorCode.Should().Be("E50");
    }
}

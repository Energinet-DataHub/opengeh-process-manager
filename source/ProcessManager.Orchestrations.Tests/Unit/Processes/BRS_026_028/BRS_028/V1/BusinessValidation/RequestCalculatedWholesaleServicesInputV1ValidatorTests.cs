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
using Energinet.DataHub.ProcessManager.Components.BusinessValidation.GridAreaOwner;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_026_028.BRS_028.V1.BusinessValidation;

public class RequestCalculatedWholesaleServicesInputV1ValidatorTests
{
    private readonly BusinessValidator<RequestCalculatedWholesaleServicesInputV1> _sut;

    public RequestCalculatedWholesaleServicesInputV1ValidatorTests()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddLogging();
        services.AddTransient<DateTimeZone>(s => DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")!);
        services.AddTransient<IClock>(s => SystemClock.Instance);

        var gridAreaOwnerClientMock = new Mock<IGridAreaOwnerClient>();
        services.AddScoped<IGridAreaOwnerClient>(_ => gridAreaOwnerClientMock.Object);

        var orchestrationsAssembly = typeof(Orchestration_Brs_028_V1).Assembly;
        var orchestrationsAbstractionsAssembly = typeof(RequestCalculatedWholesaleServicesInputV1).Assembly;
        services.AddBusinessValidation(assembliesToScan: [orchestrationsAssembly, orchestrationsAbstractionsAssembly]);

        var serviceProvider = services.BuildServiceProvider();

        _sut = serviceProvider.GetRequiredService<BusinessValidator<RequestCalculatedWholesaleServicesInputV1>>();
    }

    [Fact]
    public async Task Validate_WhenWholesaleServicesRequestIsValid_ReturnsSuccessValidation()
    {
        // Arrange
        var request = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(request);

        // Assert
        validationErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenPeriodStartIsTooOld_ReturnsUnsuccessfulValidation()
    {
        // Arrange
        var request = new RequestCalculatedWholesaleServicesInputV1Builder(forActorRole: ActorRole.EnergySupplier)
            .WithPeriod(
                periodStart: new LocalDate(2018, 5, 1)
                    .AtMidnight()
                    .InZoneStrictly(DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")!)
                    .ToInstant(),
                periodEnd: new LocalDate(2018, 6, 1)
                    .AtMidnight()
                    .InZoneStrictly(DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")!)
                    .ToInstant())
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(request);

        // Assert
        validationErrors.Should().ContainSingle()
            .Which.ErrorCode.Should().Be("E17");
    }

    [Fact]
    public async Task Validate_WhenPeriodStartAndPeriodEndAreInvalidFormat_ReturnsUnsuccessfulValidation()
    {
        // Arrange
        var now = SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset();

        var startToYearsAgo = now.AddYears(-2);
        var endToYearsAgo = now.AddYears(-2).AddMonths(1);

        var request = new RequestCalculatedWholesaleServicesInputV1Builder(forActorRole: ActorRole.EnergySupplier)
            .WithPeriod(
                periodStart: new LocalDateTime(startToYearsAgo.Year, startToYearsAgo.Month, 1, 17, 45, 12)
                    .InZoneStrictly(DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")!)
                    .ToInstant(),
                periodEnd: new LocalDateTime(endToYearsAgo.Year, endToYearsAgo.Month, 1, 8, 13, 56)
                    .InZoneStrictly(DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")!)
                    .ToInstant())
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(request);

        // Assert
        validationErrors.Where(x => x.ErrorCode == "D66").Should().HaveCount(2);
    }

    [Fact]
    public async Task Validate_WhenResolutionIsHourly_ReturnsUnsuccessfulValidation()
    {
        // Arrange
        var request = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithResolution("Hourly")
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(request);

        // Assert
        validationErrors.Should().ContainSingle()
            .Which.ErrorCode.Should().Be("D23");
    }

    [Fact]
    public async Task Validate_WhenChargeTypeIsToLong_ReturnsUnsuccessfulValidation()
    {
        // Arrange
        var chargeTypeInRequest = new RequestCalculatedWholesaleServicesInputV1.ChargeTypeInputV1(
            ChargeType: "123",
            ChargeCode: "ThisIsMoreThan10CharsLong");

        var request = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithChargeTypes([chargeTypeInRequest])
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(request);

        // Assert
        validationErrors.Should().ContainSingle()
            .Which.ErrorCode.Should().Be("D14");
    }

    [Fact]
    public async Task Validate_WhenSettlementVersionIsInvalid_ReturnsUnsuccessfulValidation()
    {
        // Arrange
        var request = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithBusinessReason(BusinessReason.Correction.Name)
            .WithSettlementVersion("invalid-settlement-version")
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(request);

        // Assert
        validationErrors.Should().ContainSingle()
            .Which.ErrorCode.Should().Be("E86");
    }
}

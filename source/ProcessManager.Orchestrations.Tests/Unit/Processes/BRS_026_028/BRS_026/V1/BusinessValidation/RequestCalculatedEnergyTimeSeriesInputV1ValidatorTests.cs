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

using Energinet.DataHub.ElectricityMarket.Integration;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation.GridAreaOwner;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_026.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_026.V1;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_026_028.BRS_026.V1.BusinessValidation;

public class RequestCalculatedEnergyTimeSeriesInputV1ValidatorTests
{
    private const string ValidActorNumber = "1111111111111";

    private readonly BusinessValidator<RequestCalculatedEnergyTimeSeriesInputV1> _sut;
    private readonly Mock<IClock> _clockMock;
    private readonly DateTimeZone _timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")!;

    public RequestCalculatedEnergyTimeSeriesInputV1ValidatorTests()
    {
        _clockMock = new Mock<IClock>();
        _clockMock.Setup(c => c.GetCurrentInstant()).Returns(Instant.FromUtc(2024, 11, 15, 16, 46, 43));

        IServiceCollection services = new ServiceCollection();

        services.AddLogging();
        services.AddTransient<DateTimeZone>(s => _timeZone);
        services.AddTransient<IClock>(s => _clockMock.Object);

        // IGridAreaOwnerClient mock must be added before AddBusinessValidation(), to override the default client registration
        var gridAreaOwnerClientMock = new Mock<IGridAreaOwnerClient>();
        services.AddScoped<IGridAreaOwnerClient>(_ => gridAreaOwnerClientMock.Object);

        var orchestrationsAssembly = typeof(Orchestration_Brs_026_V1).Assembly;
        var orchestrationsAbstractionsAssembly = typeof(RequestCalculatedEnergyTimeSeriesInputV1).Assembly;
        services.AddBusinessValidation(assembliesToScan: [orchestrationsAssembly, orchestrationsAbstractionsAssembly]);

        var serviceProvider = services.BuildServiceProvider();

        _sut = serviceProvider.GetRequiredService<BusinessValidator<RequestCalculatedEnergyTimeSeriesInputV1>>();
    }

    [Fact]
    public async Task Validate_WhenAggregatedTimeSeriesRequestIsValid_ReturnsSuccessValidation()
    {
        // Arrange
        var request = new RequestCalculatedEnergyTimeSeriesInputV1Builder(ActorRole.EnergySupplier)
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(request);

        // Assert
        validationErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenPeriodSizeIsInvalid_ReturnsUnsuccessfulValidation()
    {
        // Arrange
        var request = new RequestCalculatedEnergyTimeSeriesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(
                Instant.FromUtc(2022, 1, 1, 23, 0, 0),
                Instant.FromUtc(2022, 3, 2, 23, 0, 0))
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(request);

        // Assert
        validationErrors.Should()
            .ContainSingle()
            .Which.ErrorCode.Should()
            .Be("E17");
    }

    [Fact]
    public async Task
        Validate_WhenPeriodIsMoreThan3AndAHalfYearBackInTimeButPartOfCutOffMonth_ReturnsSuccessfulValidation()
    {
        // Prerequisite: The current time is NOT the start of a month.
        _clockMock.Setup(c => c.GetCurrentInstant()).Returns(Instant.FromUtc(2024, 11, 15, 16, 46, 43));

        // Arrange
        var periodStart = _clockMock.Object.GetCurrentInstant() // Assuming 2024-11-15 16:46:43 UTC
            .InZone(_timeZone) // 2024-11-15 17:46:43 CET
            .Date.PlusYears(-3) // 2021-11-15
            .PlusMonths(-6) // 2021-05-15
            .With(DateAdjusters.StartOfMonth) // 2021-05-01
            .AtMidnight() // 2021-05-01 00:00:00 UTC
            .InZoneStrictly(_timeZone) // 2021-05-01 00:00:00 CEST
            .ToInstant(); // 2021-04-30 22:00:00 UTC

        var periodEnd = _clockMock.Object.GetCurrentInstant() // Assuming 2024-11-15 16:46:43 UTC
            .InZone(_timeZone) // 2024-11-15 17:46:43 CET
            .Date.PlusYears(-3) // 2021-11-15
            .PlusMonths(-6) // 2021-05-15
            .With(DateAdjusters.StartOfMonth) // 2021-05-01
            .PlusDays(1) // 2021-05-02
            .AtMidnight() // 2021-05-02 00:00:00 UTC
            .InZoneStrictly(_timeZone) // 2021-05-02 00:00:00 CEST
            .ToInstant(); // 2021-05-01 22:00:00 UTC

        var request = new RequestCalculatedEnergyTimeSeriesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(periodStart, periodEnd)
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(request);

        // Assert
        validationErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task
        Validate_WhenPeriodOverlapsCutOffAt3AndAHalfYearBackInTime_ReturnsSuccessfulValidation()
    {
        // Prerequisite: The current time is NOT the start of a month.
        _clockMock.Setup(c => c.GetCurrentInstant()).Returns(Instant.FromUtc(2024, 11, 15, 16, 46, 43));

        // Arrange
        var periodStart = _clockMock.Object.GetCurrentInstant() // Assuming 2024-11-15 16:46:43 UTC
            .InZone(_timeZone) // 2024-11-15 17:46:43 CET
            .Date.PlusYears(-3) // 2021-11-15
            .PlusMonths(-6) // 2021-05-15
            .PlusDays(-1) // 2021-05-14
            .AtMidnight() // 2021-05-14 00:00:00 UTC
            .InZoneStrictly(_timeZone) // 2021-05-14 00:00:00 CEST
            .ToInstant(); // 2021-05-13 22:00:00 UTC

        var periodEnd = _clockMock.Object.GetCurrentInstant() // Assuming 2024-11-15 16:46:43 UTC
            .InZone(_timeZone) // 2024-11-15 17:46:43 CET
            .Date.PlusYears(-3) // 2021-11-15
            .PlusMonths(-6) // 2021-05-15
            .PlusDays(1) // 2021-05-16
            .AtMidnight() // 2021-05-16 00:00:00 UTC
            .InZoneStrictly(_timeZone) // 2021-05-16 00:00:00 CEST
            .ToInstant(); // 2021-05-15 22:00:00 UTC

        var request = new RequestCalculatedEnergyTimeSeriesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(periodStart, periodEnd)
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(request);

        // Assert
        validationErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WhenPeriodIsMoreThan3AndAHalfYearBackInTimeAndNotPartOfCutOffMonth_ReturnsUnsuccessfulValidation()
    {
        // Arrange
        _clockMock.Setup(c => c.GetCurrentInstant()).Returns(Instant.FromUtc(2024, 11, 15, 16, 46, 43));
        var periodStart = _clockMock.Object.GetCurrentInstant() // Assuming 2024-11-15 16:46:43 UTC
            .InZone(_timeZone) // 2024-11-15 17:46:43 CET
            .Date.PlusYears(-3) // 2021-11-15
            .PlusMonths(-7) // 2021-04-15
            .AtMidnight() // 2021-04-15 00:00:00 UTC
            .InZoneStrictly(_timeZone) // 2021-04-15 00:00:00 CEST
            .ToInstant(); // 2021-04-14 22:00:00 UTC

        var periodEnd = _clockMock.Object.GetCurrentInstant() // Assuming 2024-11-15 16:46:43 UTC
            .InZone(_timeZone) // 2024-11-15 17:46:43 CET
            .Date.PlusYears(-3) // 2021-11-15
            .PlusMonths(-7) // 2021-04-15
            .PlusDays(1) // 2021-04-16
            .AtMidnight() // 2021-04-16 00:00:00 UTC
            .InZoneStrictly(_timeZone) // 2021-04-16 00:00:00 CEST
            .ToInstant(); // 2021-04-15 22:00:00 UTC

        var request = new RequestCalculatedEnergyTimeSeriesInputV1Builder(ActorRole.EnergySupplier)
            .WithPeriod(periodStart, periodEnd)
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(request);

        // Assert
        validationErrors.Should()
            .ContainSingle()
            .Which.ErrorCode.Should()
            .Be("E17");
    }

    [Fact]
    public async Task Validate_WhenMeteringPointTypeIsInvalid_ReturnsUnsuccessfulValidation()
    {
        // Arrange
        var request = new RequestCalculatedEnergyTimeSeriesInputV1Builder(ActorRole.EnergySupplier)
            .WithMeteringPointType("invalid")
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(request);

        // Assert
        validationErrors.Should()
            .ContainSingle()
            .Which.ErrorCode.Should()
            .Be("D18");
    }

    [Fact]
    public async Task Validate_WhenEnergySupplierIdIsInvalid_ReturnsUnsuccessfulValidation()
    {
        // Arrange
        var request = new RequestCalculatedEnergyTimeSeriesInputV1Builder(ActorRole.EnergySupplier)
            .WithRequestedForActorNumber(ValidActorNumber)
            .WithEnergySupplierNumber("invalid-actor-number")
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(request);

        // Assert
        validationErrors.Should()
            .ContainSingle()
            .Which.ErrorCode.Should()
            .Be("E16");
    }

    [Fact]
    public async Task Validate_WhenSettlementMethodIsInvalid_ReturnsUnsuccessfulValidation()
    {
        // Arrange
        var request = new RequestCalculatedEnergyTimeSeriesInputV1Builder(ActorRole.EnergySupplier)
            .WithSettlementMethod("invalid-settlement-method")
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(request);

        // Assert
        validationErrors.Should()
            .ContainSingle()
            .Which.ErrorCode.Should()
            .Be("D15");
    }

    [Fact]
    public async Task Validate_WhenSettlementVersionIsInvalid_ReturnsUnsuccessfulValidation()
    {
        // Arrange
        var request = new RequestCalculatedEnergyTimeSeriesInputV1Builder(ActorRole.EnergySupplier)
            .WithBusinessReason(BusinessReason.Correction.Name)
            .WithSettlementVersion("invalid-settlement-version")
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(request);

        // Assert
        validationErrors.Should()
            .ContainSingle()
            .Which.ErrorCode.Should()
            .Be("E86");
    }

    [Fact]
    public async Task Validate_WhenConsumptionAndNoSettlementMethod_ReturnsUnsuccessfulValidation()
    {
        // Arrange
        var request = new RequestCalculatedEnergyTimeSeriesInputV1Builder(ActorRole.EnergySupplier)
            .WithMeteringPointType(MeteringPointType.Consumption.Name)
            .WithSettlementMethod(null)
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(request);

        // Assert
        validationErrors.Should()
            .ContainSingle()
            .Which.ErrorCode.Should()
            .Be("D11");
    }

    [Fact]
    public async Task Validate_WhenWholesaleFixingForBalanceResponsible_ReturnsUnsuccessfulValidation()
    {
        // Arrange
        var request = new RequestCalculatedEnergyTimeSeriesInputV1Builder(ActorRole.BalanceResponsibleParty)
            .WithBusinessReason(BusinessReason.WholesaleFixing.Name)
            .Build();

        // Act
        var validationErrors = await _sut.ValidateAsync(request);

        // Assert
        validationErrors.Should()
            .ContainSingle()
            .Which.ErrorCode.Should()
            .Be("D11");
    }
}

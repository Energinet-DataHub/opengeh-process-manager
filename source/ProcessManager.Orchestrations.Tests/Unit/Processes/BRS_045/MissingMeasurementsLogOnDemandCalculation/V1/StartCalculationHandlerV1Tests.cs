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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1;
using FluentAssertions;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1;

public class StartCalculationHandlerV1Tests
{
    public StartCalculationHandlerV1Tests()
    {
        OrchestrationInstanceId = new OrchestrationInstanceId(Guid.NewGuid());
        ManagerMock = new Mock<IStartOrchestrationInstanceCommands>();
        ManagerMock.Setup(mock => mock.StartNewOrchestrationInstanceAsync(
                It.IsAny<OperatingIdentity>(),
                It.IsAny<OrchestrationDescriptionUniqueName>(),
                It.IsAny<CalculationInputV1>(),
                It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync(OrchestrationInstanceId);
    }

    private Mock<IStartOrchestrationInstanceCommands> ManagerMock { get; }

    private OrchestrationInstanceId OrchestrationInstanceId { get; }

    [Fact]
    public async Task HandleCommand_WithValidParameters()
    {
        // Arrange
        var userIdentity = CreateUserIdentity();
        var offset = CreateDkOffset();

        var calculationInputV1 = new CalculationInputV1(
            new DateTimeOffset(2025, 1, 1, 23, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 1, 2, 23, 0, 0, TimeSpan.Zero),
            ["302"]);
        var command = new StartCalculationCommandV1(userIdentity, calculationInputV1);
        var sut = new StartCalculationHandlerV1(DateTimeZone.ForOffset(offset), ManagerMock.Object);

        // Act
        var actual = await sut.HandleAsync(command);

        // Assert
        actual.Should().Be(OrchestrationInstanceId.Value);
    }

    [Theory]
    [InlineData(2025, 1, 1, 23, 1, 2025, 1, 2, 23, 0, "301", 1)]
    [InlineData(2025, 1, 1, 23, 0, 2025, 1, 1, 22, 0, "301", 1)]
    [InlineData(2025, 1, 1, 22, 0, 2025, 1, 1, 23, 0, "301", 1)]
    [InlineData(2025, 7, 1, 22, 0, 2025, 7, 1, 22, 1, "301", 2)]
    [InlineData(2025, 1, 1, 23, 0, 2025, 1, 2, 23, 0, null, 1)]
    [InlineData(2025, 1, 1, 23, 0, 2025, 2, 2, 23, 0, "301", 1)]
    public async Task HandleCommand_WithInvalidParameters(
        int startYear,
        int startMonth,
        int startDay,
        int startHour,
        int startMinute,
        int endYear,
        int endMonth,
        int endDay,
        int endHour,
        int endMinute,
        string? gridAreaCode,
        int offset)
    {
        // Arrange
        var userIdentity = CreateUserIdentity();
        var offsetDk = CreateDkOffset();
        var gridAreaCodes = gridAreaCode != null ? [gridAreaCode] : new List<string>();
        var calculationInputV1 = new CalculationInputV1(
            new DateTimeOffset(startYear, startMonth, startDay, startHour, startMinute, 0, TimeSpan.FromHours(offset)),
            new DateTimeOffset(endYear, endMonth, endDay, endHour, endMinute, 0, TimeSpan.FromHours(offset)),
            gridAreaCodes);
        var command = new StartCalculationCommandV1(userIdentity, calculationInputV1);
        var sut = new StartCalculationHandlerV1(DateTimeZone.ForOffset(offsetDk), ManagerMock.Object);

        // Act
        var act = async () => await sut.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<Exception>("All cases should throw exception.");
    }

    private static Offset CreateDkOffset()
    {
        var offset = DateTimeZoneProviders.Tzdb
            .GetZoneOrNull("Europe/Copenhagen")!
            .GetUtcOffset(Instant.FromDateTimeUtc(DateTime.UtcNow));
        return offset;
    }

    private static UserIdentityDto CreateUserIdentity()
    {
        var userIdentity = new UserIdentityDto(
            UserId: Guid.NewGuid(),
            ActorNumber: ActorNumber.Create("1111111111111"),
            ActorRole: ActorRole.DataHubAdministrator);
        return userIdentity;
    }
}

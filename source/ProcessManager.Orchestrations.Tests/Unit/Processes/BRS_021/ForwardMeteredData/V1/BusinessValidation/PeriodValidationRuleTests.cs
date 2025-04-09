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

using Energinet.DataHub.ProcessManager.Components.BusinessValidation.Validators;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;
using FluentAssertions;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1.
    BusinessValidation;

public class PeriodValidationRuleTests
{
    [Fact]
    public async Task Given_StartDateIsNotParsable_When_ValidateAsync_Then_Error()
    {
        var sut = GetPeriodValidationRule(Instant.FromUtc(2025, 1, 1, 0, 0, 0));

        var result = await sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder()
                .WithStartDateTime("invalid").Build(),
                null,
                []));

        result.Should().ContainSingle().And.Contain(PeriodValidationRule.InvalidStartDate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    public async Task Given_EndDateIsNotParsable_When_ValidateAsync_Then_Error(string? endDate)
    {
        var sut = GetPeriodValidationRule(Instant.FromUtc(2025, 1, 1, 0, 0, 0));

        var result = await sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder().WithEndDateTime(endDate).Build(),
                null,
                []));

        result.Should().ContainSingle().And.Contain(PeriodValidationRule.InvalidEndDate);
    }

    [Fact]
    public async Task Given_StartDateIsTooOld_When_ValidateAsync_Then_Error()
    {
        var sut = GetPeriodValidationRule(Instant.FromUtc(2025, 1, 1, 0, 0, 0));

        var result = await sut.ValidateAsync(
            new(
                new ForwardMeteredDataInputV1Builder()
                    .WithStartDateTime("2021-11-30T23:00:00Z")
                    .WithEndDateTime("2021-12-31T23:00:00Z")
                    .Build(),
                null,
                []));

        result.Should().ContainSingle().And.Contain(PeriodValidationRule.StartDateIsTooOld);
    }

    private PeriodValidationRule GetPeriodValidationRule(Instant now)
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.GetCurrentInstant()).Returns(now);
        return new PeriodValidationRule(
            new PeriodValidator(DateTimeZoneProviders.Tzdb["Europe/Copenhagen"], clock.Object));
    }
}

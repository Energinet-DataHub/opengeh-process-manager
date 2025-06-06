﻿// Copyright 2020 Energinet DataHub A/S
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

using System.Reflection;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1.BusinessValidation;
using FluentAssertions;
using FluentAssertions.Execution;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_026_028.BRS_028.V1.BusinessValidation;

public class ResolutionValidationRuleTests
{
    private const string PropertyName = "aggregationSeries_Period.resolution";
    private static readonly ValidationError _notMonthlyResolution =
        new(
            $"{PropertyName} skal være 'P1M'/{PropertyName} must be 'P1M'",
            "D23");

    private readonly ResolutionValidationRule _sut;

    public ResolutionValidationRuleTests()
    {
        _sut = new ResolutionValidationRule();
    }

    public static IEnumerable<object?[]> GetMonthlyAndMissingResolution()
    {
        return new[]
        {
            new object[] { "Monthly" },
            new object[] { null! },
            new object[] { Resolution.Monthly.Name },
        };
    }

    public static IEnumerable<object?[]> GetInvalidResolutions()
    {
        var customResolutions = new[]
        {
            "NotMonthly",
            "P1M",
            "PT1M",
        }.ToArray();

        var allResolutions = GetAllResolutionsInDataHub();
        var invalidResolutions = allResolutions
            .Where(res => res != Resolution.Monthly.Name);

        var invalidResolutionsWithCustomResolutions = invalidResolutions.Concat(customResolutions)
            .Select(res => new object[] { res! });

        return invalidResolutionsWithCustomResolutions;
    }

    [Theory]
    [MemberData(nameof(GetMonthlyAndMissingResolution))]
    public async Task Validate_WhenResolutionIsValid_ReturnsNoErrors(string? allowedResolution)
    {
        // Arrange
        var request = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithResolution(allowedResolution)
            .Build();

        // Act
        var actual = await _sut.ValidateAsync(request);

        // Assert
        actual.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(GetInvalidResolutions))]
    public async Task Validate_WhenResolutionIsNotAllowed_ReturnsError(string rejectedResolution)
    {
        // Arrange
        var request = new RequestCalculatedWholesaleServicesInputV1Builder(ActorRole.EnergySupplier)
            .WithResolution(rejectedResolution)
            .Build();

        // Act
        var actual = await _sut.ValidateAsync(request);

        // Assert
        using var assertionScope = new AssertionScope();
        actual.Should().HaveCount(1);
        actual.First().Message.Should().BeSameAs(_notMonthlyResolution.Message);
        actual.First().ErrorCode.Should().BeSameAs(_notMonthlyResolution.ErrorCode);
    }

    private static IEnumerable<string?> GetAllResolutionsInDataHub()
    {
        var resolutionType = typeof(Resolution);
        return resolutionType
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .ToList()
            .Select(res => (Resolution)res.GetValue(null)!)
            .Select(res => res.Name);
    }
}

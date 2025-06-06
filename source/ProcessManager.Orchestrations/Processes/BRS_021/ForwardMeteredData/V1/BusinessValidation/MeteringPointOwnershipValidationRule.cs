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

using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.Extensions;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;

/// <summary>
/// Business validation rule for metering point validation
/// if the metering point does not exist, a business validation error is returned
/// </summary>
public class MeteringPointOwnershipValidationRule(IOptions<ProcessManagerComponentsOptions> options)
    : IBusinessValidationRule<ForwardMeteredDataBusinessValidatedDto>
{
    private readonly IOptions<ProcessManagerComponentsOptions> _options = options;

    public static IList<ValidationError> MeteringPointHasWrongOwnerError =>
    [
        new(
            Message: "Forkert ejer af målepunktet / wrong owner of metering point",
            ErrorCode: "D50"),
    ];

    private static IList<ValidationError> NoError => [];

    public Task<IList<ValidationError>> ValidateAsync(
        ForwardMeteredDataBusinessValidatedDto subject)
    {
        // The performance test uses non-existing metering points, so we must skip this validation
        if (_options.Value.AllowMockDependenciesForTests && IsTestInput(subject.Input))
            return Task.FromResult(NoError);

        // All the historical metering point master data, have the current grid access provider provided.
        var meteringPointMasterData = subject.MeteringPointMasterData.FirstOrDefault();

        if (meteringPointMasterData != null &&
            !meteringPointMasterData.CurrentGridAccessProvider.Value.Equals(subject.Input.GridAccessProviderNumber))
        {
            return Task.FromResult(MeteringPointHasWrongOwnerError);
        }

        return Task.FromResult(NoError);
    }

    private bool IsTestInput(ForwardMeteredDataInputV1 input)
    {
        if (input.MeteringPointId is null)
            return false;

        var isPerformanceTest = input.MeteringPointId.IsPerformanceTestUuid();
        var isTest = input.MeteringPointId.IsTestMeteringPointId();
        return isPerformanceTest || isTest;
    }
}

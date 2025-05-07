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

using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures.Extensions;

public static class ProcessManagerFixtureExtensions
{
    /// <summary>
    /// Wait for an orchestration instance to be returned by the ProcessManager http client. If step inputs are provided,
    /// then the orchestration instance must have a step instance with the given step sequence and state.
    /// <remarks>The lookup is based on the idempotency key of the test configuration request.</remarks>
    /// </summary>
    /// <param name="fixture"></param>
    /// <param name="idempotencyKey">Find an orchestration instance with the given idempotency key.</param>
    /// <param name="orchestrationInstanceState">If provided, then the orchestration instance have the given state.</param>
    /// <param name="stepSequence">If provided, then the orchestration instance must have a step instance with the given sequence number.</param>
    /// <param name="stepState">If provided, then the step should be in the given state (defaults to <see cref="StepInstanceLifecycleState.Terminated"/>).</param>
    public static async Task<(
        bool Success,
        OrchestrationInstanceTypedDto<TInputParameterDto>? OrchestrationInstance,
        StepInstanceDto? StepInstance)> WaitForOrchestrationInstanceByIdempotencyKeyAsync<TInputParameterDto, TConfiguration>(
            this ProcessManagerFixture<TConfiguration> fixture,
            string idempotencyKey,
            OrchestrationInstanceLifecycleState? orchestrationInstanceState = null,
            int? stepSequence = null,
            StepInstanceLifecycleState? stepState = null)
                where TInputParameterDto : class, IInputParameterDto
    {
        if (stepState != null && stepSequence == null)
            throw new ArgumentNullException(nameof(stepSequence), $"{nameof(stepSequence)} must be provided if {nameof(stepState)} is not null.");

        OrchestrationInstanceTypedDto<TInputParameterDto>? orchestrationInstance = null;
        StepInstanceDto? stepInstance = null;

        var success = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                orchestrationInstance = await fixture.ProcessManagerHttpClient
                    .GetOrchestrationInstanceByIdempotencyKeyAsync<TInputParameterDto>(
                        new GetOrchestrationInstanceByIdempotencyKeyQuery(
                            operatingIdentity: fixture.UserIdentity,
                            idempotencyKey: idempotencyKey),
                        CancellationToken.None);

                if (orchestrationInstance == null)
                    return false;

                if (stepSequence != null)
                {
                    stepInstance = orchestrationInstance.Steps
                        .SingleOrDefault(s => s.Sequence == stepSequence.Value);
                }

                if (orchestrationInstanceState != null && orchestrationInstance.Lifecycle.State != orchestrationInstanceState)
                    return false;

                // If step sequence is not provided, only check for orchestration instance existence
                if (stepSequence == null)
                    return true;

                return stepInstance != null
                    ? stepInstance.Lifecycle.State == (stepState ?? StepInstanceLifecycleState.Terminated)
                    : throw new ArgumentException($"Step instance for step sequence {stepSequence} not found", nameof(stepSequence));
            },
            timeLimit: TimeSpan.FromMinutes(1),
            delay: TimeSpan.FromSeconds(1));

        return (success, orchestrationInstance, stepInstance);
    }

    /// <summary>
    /// Wait for an orchestration instance to be returned by the ProcessManager http client. If step inputs are provided,
    /// then the orchestration instance must have a step instance with the given step sequence and state.
    /// </summary>
    /// <param name="fixture"></param>
    /// <param name="orchestrationInstanceId">Find an orchestration instance with the given id.</param>
    /// <param name="orchestrationInstanceState">If provided, then the orchestration instance have the given state.</param>
    /// <param name="stepSequence">If provided, then the orchestration instance must have a step instance with the given sequence number.</param>
    /// <param name="stepState">If provided, then the step should be in the given state (defaults to <see cref="StepInstanceLifecycleState.Terminated"/>).</param>
    public static async Task<(
        bool Success,
        OrchestrationInstanceTypedDto? OrchestrationInstance,
        StepInstanceDto? StepInstance)> WaitForOrchestrationInstanceByIdAsync<TConfiguration>(
            this ProcessManagerFixture<TConfiguration> fixture,
            Guid orchestrationInstanceId,
            OrchestrationInstanceLifecycleState? orchestrationInstanceState = null,
            int? stepSequence = null,
            StepInstanceLifecycleState? stepState = null)
    {
        if (stepState != null && stepSequence == null)
            throw new ArgumentNullException(nameof(stepSequence), $"{nameof(stepSequence)} must be provided if {nameof(stepState)} is not null.");

        OrchestrationInstanceTypedDto? orchestrationInstance = null;
        StepInstanceDto? stepInstance = null;

        var success = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                orchestrationInstance = await fixture.ProcessManagerHttpClient
                    .GetOrchestrationInstanceByIdAsync(
                        new GetOrchestrationInstanceByIdQuery(
                            operatingIdentity: fixture.UserIdentity,
                            id: orchestrationInstanceId),
                        CancellationToken.None);

                if (orchestrationInstance == null)
                    return false;

                if (stepSequence != null)
                {
                    stepInstance = orchestrationInstance.Steps
                        .SingleOrDefault(s => s.Sequence == stepSequence.Value);
                }

                if (orchestrationInstanceState != null && orchestrationInstance.Lifecycle.State != orchestrationInstanceState)
                    return false;

                // If step sequence is not provided, only check for orchestration instance existence
                if (stepSequence == null)
                    return true;

                return stepInstance != null
                    ? stepInstance.Lifecycle.State == (stepState ?? StepInstanceLifecycleState.Terminated)
                    : throw new ArgumentException($"Step instance for step sequence {stepSequence} not found", nameof(stepSequence));
            },
            timeLimit: TimeSpan.FromMinutes(1),
            delay: TimeSpan.FromSeconds(1));

        return (success, orchestrationInstance, stepInstance);
    }
}

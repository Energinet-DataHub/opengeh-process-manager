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

using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.SendMeasurements;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Client;

namespace Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;

public static class ProcessManagerClientExtensions
{
    private const int TimeLimitInSeconds = 60;

    public enum SendMeasurementsInstanceStep
    {
        BusinessValidation,
        ForwardToMeasurements,
        EnqueueActorMessages,
    }

    /// <summary>
    /// Wait for an orchestration instance, resolving true/false by using the given <paramref name="comparer"/> function.
    /// <remarks>Returns false if an orchestration instance isn't found in <see cref="TimeLimitInSeconds"/> seconds.</remarks>
    /// </summary>
    /// <param name="client"></param>
    /// <param name="idempotencyKey">The idempotency key of the orchestration instance, used to find the correct orchestration instance.</param>
    /// <param name="comparer">The comparer method used to determine whether the orchestration instance is in a correct state.</param>
    public static async Task<(bool Succes, OrchestrationInstanceTypedDto<TInput>? OrchestrationInstance)>
        TryWaitForOrchestrationInstance<TInput>(
            this IProcessManagerClient client,
            string idempotencyKey,
            Func<OrchestrationInstanceTypedDto<TInput>, bool> comparer)
                where TInput : class, IInputParameterDto
    {
        OrchestrationInstanceTypedDto<TInput>? orchestrationInstance = null;
        var success = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                orchestrationInstance = await client
                    .GetOrchestrationInstanceByIdempotencyKeyAsync<TInput>(
                        new GetOrchestrationInstanceByIdempotencyKeyQuery(
                            new UserIdentityDto(
                                UserId: Guid.NewGuid(),
                                ActorNumber: ActorNumber.Create("1234567891234"),
                                ActorRole: ActorRole.EnergySupplier),
                            idempotencyKey),
                        CancellationToken.None);

                if (orchestrationInstance is null)
                    return false;

                return comparer(orchestrationInstance);
            },
            timeLimit: TimeSpan.FromSeconds(TimeLimitInSeconds),
            delay: TimeSpan.FromSeconds(1));

        return (success, orchestrationInstance);
    }

    /// <summary>
    /// Wait for an orchestration instance, resolving true/false by using the given <paramref name="comparer"/> function.
    /// <remarks>Returns false if an orchestration instance isn't found in <see cref="TimeLimitInSeconds"/> seconds.</remarks>
    /// </summary>
    /// <param name="client"></param>
    /// <param name="orchestrationInstanceId">The id of the orchestration instance, used to find the correct orchestration instance.</param>
    /// <param name="comparer">The comparer method used to determine whether the orchestration instance is in a correct state.</param>
    public static async Task<(bool Success, OrchestrationInstanceTypedDto? OrchestrationInstance)> TryWaitForOrchestrationInstance(
            this IProcessManagerClient client,
            Guid orchestrationInstanceId,
            Func<OrchestrationInstanceTypedDto, bool> comparer)
    {
        OrchestrationInstanceTypedDto? orchestrationInstance = null;
        var success = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                orchestrationInstance = await client
                    .GetOrchestrationInstanceByIdAsync(
                        new GetOrchestrationInstanceByIdQuery(
                            operatingIdentity: new UserIdentityDto(
                                UserId: Guid.NewGuid(),
                                ActorNumber: ActorNumber.Create("1234567891234"),
                                ActorRole: ActorRole.EnergySupplier),
                            id: orchestrationInstanceId),
                        CancellationToken.None);

                if (orchestrationInstance is null)
                    return false;

                return comparer(orchestrationInstance);
            },
            timeLimit: TimeSpan.FromSeconds(TimeLimitInSeconds),
            delay: TimeSpan.FromSeconds(1));

        return (success, orchestrationInstance);
    }

    /// <summary>
    /// Wait for an orchestration instance to be terminated with the given <paramref name="terminationState"/>.
    /// <remarks>Returns false if not resolved in <see cref="TimeLimitInSeconds"/> seconds.</remarks>
    /// </summary>
    /// <param name="client"></param>
    /// <param name="idempotencyKey">The idempotency key of the orchestration instance, used to find the correct orchestration instance.</param>
    /// <param name="terminationState">The termination state the orchestration instance should be in.</param>
    public static Task<(bool Success, OrchestrationInstanceTypedDto<TInput>? OrchestrationInstance)> WaitForOrchestrationInstanceTerminated<TInput>(
        this IProcessManagerClient client,
        string idempotencyKey,
        OrchestrationInstanceTerminationState? terminationState = null)
        where TInput : class, IInputParameterDto
    {
        return client.TryWaitForOrchestrationInstance<TInput>(
            idempotencyKey,
            (orchestrationInstance) =>
                orchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleState.Terminated
                && (
                    terminationState is null
                    || orchestrationInstance.Lifecycle.TerminationState == terminationState));
    }

    /// <summary>
    /// Wait for an orchestration instance to be terminated with the given <paramref name="terminationState"/>.
    /// <remarks>Returns false if not resolved in <see cref="TimeLimitInSeconds"/> seconds.</remarks>
    /// </summary>
    /// <param name="client"></param>
    /// <param name="orchestrationInstanceId">The id of the orchestration instance, used to find the correct orchestration instance.</param>
    /// <param name="terminationState">The termination state the orchestration instance should be in.</param>
    public static Task<(bool Success, OrchestrationInstanceTypedDto? OrchestrationInstance)> WaitForOrchestrationInstanceTerminated(
        this IProcessManagerClient client,
        Guid orchestrationInstanceId,
        OrchestrationInstanceTerminationState? terminationState = null)
    {
        return client.TryWaitForOrchestrationInstance(
            orchestrationInstanceId,
            (orchestrationInstance) =>
                orchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleState.Terminated
                && (
                    terminationState is null
                    || orchestrationInstance.Lifecycle.TerminationState == terminationState));
    }

    /// <summary>
    /// Wait for an orchestration instance step to be in the <see cref="StepInstanceLifecycleState.Running"/> state.
    /// <remarks>Returns false if not resolved in <see cref="TimeLimitInSeconds"/> seconds.</remarks>
    /// </summary>
    /// <param name="client"></param>
    /// <param name="idempotencyKey">The idempotency key of the orchestration instance, used to find the correct orchestration instance.</param>
    /// <param name="stepSequence">The sequence number of the step that should be in the <see cref="StepInstanceLifecycleState.Running"/> state.</param>
    public static Task<(bool Success, OrchestrationInstanceTypedDto<TInput>? OrchestrationInstance)> WaitForStepToBeRunning<TInput>(
        this IProcessManagerClient client,
        string idempotencyKey,
        int stepSequence)
            where TInput : class, IInputParameterDto
    {
        return client.TryWaitForOrchestrationInstance<TInput>(
            idempotencyKey,
            (orchestrationInstance) =>
            {
                var enqueueActorMessagesStep = orchestrationInstance.Steps
                    .Single(s => s.Sequence == stepSequence);

                return enqueueActorMessagesStep.Lifecycle.State == StepInstanceLifecycleState.Running;
            });
    }

    /// <summary>
    /// Wait for an orchestration instance step to be in the <see cref="StepInstanceLifecycleState.Running"/> state.
    /// <remarks>Returns false if not resolved in <see cref="TimeLimitInSeconds"/> seconds.</remarks>
    /// </summary>
    /// <param name="client"></param>
    /// <param name="orchestrationInstanceId">The idempotency key of the orchestration instance, used to find the correct orchestration instance.</param>
    /// <param name="stepSequence">The sequence number of the step that should be in the <see cref="StepInstanceLifecycleState.Running"/> state.</param>
    public static Task<(bool Success, OrchestrationInstanceTypedDto? OrchestrationInstance)> WaitForStepToBeRunning<TInput>(
        this IProcessManagerClient client,
        Guid orchestrationInstanceId,
        int stepSequence)
        where TInput : class, IInputParameterDto
    {
        return client.TryWaitForOrchestrationInstance(
            orchestrationInstanceId: orchestrationInstanceId,
            comparer: orchestrationInstance =>
            {
                var enqueueActorMessagesStep = orchestrationInstance.Steps
                    .Single(s => s.Sequence == stepSequence);

                return enqueueActorMessagesStep.Lifecycle.State == StepInstanceLifecycleState.Running;
            });
    }

    /// <summary>
    /// Wait for a Send Measurements instance to be running (or terminated if <paramref name="mustBeTerminated"/> is true).
    /// <remarks>Returns false if not resolved in <see cref="TimeLimitInSeconds"/> seconds.</remarks>
    /// </summary>
    /// <param name="client"></param>
    /// <param name="idempotencyKey">The idempotency key used to find the correct instance.</param>
    /// <param name="mustBeTerminated">If true then the instance must be terminated.</param>
    public static async Task<(bool Success, SendMeasurementsInstanceDto? Instance)> WaitForSendMeasurementsInstanceAsync(
        this IProcessManagerClient client,
        string idempotencyKey,
        bool mustBeTerminated = false)
    {
        SendMeasurementsInstanceDto? instance = null;
        var success = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                instance = await client
                    .GetSendMeasurementsInstanceByIdempotencyKeyAsync(
                        new GetSendMeasurementsInstanceByIdempotencyKeyQuery(
                            new UserIdentityDto(
                                UserId: Guid.NewGuid(),
                                ActorNumber: ActorNumber.Create("1234567891234"),
                                ActorRole: ActorRole.EnergySupplier),
                            idempotencyKey),
                        CancellationToken.None);

                return mustBeTerminated
                    ? instance is { TerminatedAt: not null }
                    : instance is not null;
            },
            timeLimit: TimeSpan.FromSeconds(TimeLimitInSeconds),
            delay: TimeSpan.FromSeconds(1));

        return (success, instance);
    }

    /// <summary>
    /// Wait for a Send Measurements instance step to be running (or successful
    /// if <paramref name="stepMustBeSuccessful"/> is true).
    /// <remarks>Returns false if not resolved in <see cref="TimeLimitInSeconds"/> seconds.</remarks>
    /// </summary>
    /// <param name="client"></param>
    /// <param name="idempotencyKey">The idempotency key used to find the correct instance.</param>
    /// <param name="step">The step that should be running (or successful).</param>
    /// <param name="stepMustBeSuccessful">If true then the step must be completed successfully, else the step must be running.</param>
    public static async Task<(bool Success, SendMeasurementsInstanceDto? Instance)> WaitForSendMeasurementsInstanceStepAsync(
        this IProcessManagerClient client,
        string idempotencyKey,
        SendMeasurementsInstanceStep step,
        bool stepMustBeSuccessful = false)
    {
        SendMeasurementsInstanceDto? instance = null;
        var success = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                instance = await client
                    .GetSendMeasurementsInstanceByIdempotencyKeyAsync(
                        new GetSendMeasurementsInstanceByIdempotencyKeyQuery(
                            new UserIdentityDto(
                                UserId: Guid.NewGuid(),
                                ActorNumber: ActorNumber.Create("1234567891234"),
                                ActorRole: ActorRole.EnergySupplier),
                            idempotencyKey),
                        CancellationToken.None);

                if (instance is null)
                    return false;

                return stepMustBeSuccessful
                    ? step switch
                    {
                        SendMeasurementsInstanceStep.BusinessValidation => instance.BusinessValidationSucceededAt is not null,
                        SendMeasurementsInstanceStep.ForwardToMeasurements => instance.ReceivedFromMeasurementsAt is not null,
                        SendMeasurementsInstanceStep.EnqueueActorMessages => instance.ReceivedFromEnqueueActorMessagesAt is not null,
                        _ => throw new ArgumentOutOfRangeException(nameof(step), $"Unknown step: {step}."),
                    }
                    : step switch
                    {
                        SendMeasurementsInstanceStep.BusinessValidation => true,
                        SendMeasurementsInstanceStep.ForwardToMeasurements => instance.SentToMeasurementsAt is not null,
                        SendMeasurementsInstanceStep.EnqueueActorMessages => instance.SentToEnqueueActorMessagesAt is not null,
                        _ => throw new ArgumentOutOfRangeException(nameof(step), $"Unknown step: {step}."),
                    };
            },
            timeLimit: TimeSpan.FromSeconds(TimeLimitInSeconds),
            delay: TimeSpan.FromSeconds(1));

        return (success, instance);
    }
}

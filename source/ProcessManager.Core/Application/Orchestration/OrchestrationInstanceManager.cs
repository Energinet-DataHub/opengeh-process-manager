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

using Energinet.DataHub.ProcessManager.Core.Application.FeatureFlags;
using Energinet.DataHub.ProcessManager.Core.Application.Scheduling;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Core.Application.Orchestration;

/// <summary>
/// An manager that allows us to provide a framework for managing orchestration instances
/// using custom domain types.
/// </summary>
internal class OrchestrationInstanceManager(
    IClock clock,
    IOrchestrationInstanceExecutor executor,
    IOrchestrationRegisterQueries orchestrationRegister,
    IOrchestrationInstanceRepository repository,
    IFeatureFlagManager featureFlagManager,
    ILogger<OrchestrationInstanceManager> logger) :
        IStartOrchestrationInstanceCommands,
        IStartOrchestrationInstanceMessageCommands,
        IStartScheduledOrchestrationInstanceCommand,
        ICancelScheduledOrchestrationInstanceCommand,
        INotifyOrchestrationInstanceCommands
{
    private readonly IClock _clock = clock;
    private readonly IOrchestrationInstanceExecutor _executor = executor;
    private readonly IOrchestrationRegisterQueries _orchestrationRegister = orchestrationRegister;
    private readonly IOrchestrationInstanceRepository _repository = repository;
    private readonly IFeatureFlagManager _featureFlagManager = featureFlagManager;
    private readonly ILogger<OrchestrationInstanceManager> _logger = logger;

    /// <inheritdoc />
    public async Task<OrchestrationInstanceId> StartNewOrchestrationInstanceAsync(
        OperatingIdentity identity,
        OrchestrationDescriptionUniqueName uniqueName)
    {
        var orchestrationDescription = await GuardMatchingOrchestrationDescriptionAsync(uniqueName).ConfigureAwait(false);

        var orchestrationInstance = await CreateOrchestrationInstanceAsync(
            identity,
            orchestrationDescription).ConfigureAwait(false);

        await RequestStartOfOrchestrationInstanceIfPendingAsync(
            orchestrationDescription,
            orchestrationInstance).ConfigureAwait(false);

        return orchestrationInstance.Id;
    }

    /// <inheritdoc />
    public async Task<OrchestrationInstanceId> StartNewOrchestrationInstanceAsync<TParameter>(
        OperatingIdentity identity,
        OrchestrationDescriptionUniqueName uniqueName,
        TParameter inputParameter,
        IReadOnlyCollection<int> skipStepsBySequence)
            where TParameter : class
    {
        var orchestrationDescription = await GuardMatchingOrchestrationDescriptionWithInputAsync(
            uniqueName,
            inputParameter,
            skipStepsBySequence).ConfigureAwait(false);

        var orchestrationInstance = await CreateOrchestrationInstanceWithInputAsync(
            identity,
            orchestrationDescription,
            inputParameter,
            skipStepsBySequence).ConfigureAwait(false);

        await RequestStartOfOrchestrationInstanceIfPendingAsync(
            orchestrationDescription,
            orchestrationInstance).ConfigureAwait(false);

        return orchestrationInstance.Id;
    }

    /// <inheritdoc />
    public async Task<OrchestrationInstanceId> StartNewOrchestrationInstanceAsync<TParameter>(
        ActorIdentity identity,
        OrchestrationDescriptionUniqueName uniqueName,
        TParameter inputParameter,
        IReadOnlyCollection<int> skipStepsBySequence,
        IdempotencyKey idempotencyKey,
        ActorMessageId actorMessageId,
        TransactionId transactionId,
        MeteringPointId? meteringPointId)
            where TParameter : class
    {
        var orchestrationDescription = await GuardMatchingOrchestrationDescriptionWithInputAsync(
            uniqueName,
            inputParameter,
            skipStepsBySequence).ConfigureAwait(false);

        // Idempotency check
        var orchestrationInstance = await _repository.GetOrDefaultAsync(idempotencyKey).ConfigureAwait(false);
        orchestrationInstance ??= await CreateOrchestrationInstanceWithInputAsync(
                identity,
                orchestrationDescription,
                inputParameter,
                skipStepsBySequence,
                idempotencyKey: idempotencyKey,
                actorMessageId: actorMessageId,
                transactionId: transactionId,
                meteringPointId: meteringPointId)
            .ConfigureAwait(false);

        await RequestStartOfOrchestrationInstanceIfPendingAsync(
            orchestrationDescription,
            orchestrationInstance).ConfigureAwait(false);

        return orchestrationInstance.Id;
    }

    /// <inheritdoc />
    public async Task<OrchestrationInstanceId> ScheduleNewOrchestrationInstanceAsync(
        UserIdentity identity,
        OrchestrationDescriptionUniqueName uniqueName,
        Instant runAt)
    {
        var orchestrationDescription = await GuardMatchingOrchestrationDescriptionAsync(uniqueName).ConfigureAwait(false);

        if (orchestrationDescription.CanBeScheduled == false)
            throw new InvalidOperationException("Orchestration description cannot be scheduled.");

        var orchestrationInstance = await CreateOrchestrationInstanceAsync(
            identity,
            orchestrationDescription,
            runAt).ConfigureAwait(false);

        return orchestrationInstance.Id;
    }

    /// <inheritdoc />
    public async Task<OrchestrationInstanceId> ScheduleNewOrchestrationInstanceAsync<TParameter>(
        UserIdentity userIdentity,
        OrchestrationDescriptionUniqueName uniqueName,
        TParameter inputParameter,
        Instant runAt,
        IReadOnlyCollection<int> skipStepsBySequence)
            where TParameter : class
    {
        var orchestrationDescription = await GuardMatchingOrchestrationDescriptionWithInputAsync(
            uniqueName,
            inputParameter,
            skipStepsBySequence).ConfigureAwait(false);

        if (orchestrationDescription.CanBeScheduled == false)
            throw new InvalidOperationException("Orchestration description cannot be scheduled.");

        var orchestrationInstance = await CreateOrchestrationInstanceWithInputAsync(
            userIdentity,
            orchestrationDescription,
            inputParameter,
            skipStepsBySequence,
            runAt)
            .ConfigureAwait(false);

        return orchestrationInstance.Id;
    }

    /// <inheritdoc />
    public async Task StartScheduledOrchestrationInstanceAsync(OrchestrationInstanceId id)
    {
        var orchestrationInstance = await _repository.GetAsync(id).ConfigureAwait(false);
        if (!orchestrationInstance.Lifecycle.IsPendingForScheduledStart())
            throw new InvalidOperationException("Orchestration instance cannot be started.");

        var orchestrationDescription = await _orchestrationRegister.GetAsync(orchestrationInstance.OrchestrationDescriptionId).ConfigureAwait(false);
        if (!orchestrationDescription.IsEnabled)
            throw new InvalidOperationException("Orchestration instance is based on a disabled orchestration definition.");

        await RequestStartOfOrchestrationInstanceIfPendingAsync(orchestrationDescription, orchestrationInstance).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CancelScheduledOrchestrationInstanceAsync(UserIdentity userIdentity, OrchestrationInstanceId id)
    {
        var orchestrationInstance = await _repository.GetAsync(id).ConfigureAwait(false);
        if (!orchestrationInstance.Lifecycle.IsPendingForScheduledStart())
            throw new InvalidOperationException("Orchestration instance cannot be canceled.");

        // Transition lifecycle
        orchestrationInstance.Lifecycle.TransitionToUserCanceled(_clock, userIdentity);
        await _repository.UnitOfWork.CommitAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task NotifyOrchestrationInstanceAsync<TData>(OrchestrationInstanceId id, string eventName, TData? eventData)
        where TData : class
    {
        var orchestrationInstanceToNotify = await _repository.GetOrDefaultAsync(id).ConfigureAwait(false);

        if (orchestrationInstanceToNotify is null)
        {
            if (await _featureFlagManager.IsEnabledAsync(FeatureFlag.SilentMode).ConfigureAwait(false))
            {
                _logger.LogWarning(
                    $"Notifying orchestration instance with id '{id.Value}' and event name '{eventName}' failed.");
                return;
            }

            throw new InvalidOperationException($"Orchestration instance (Id={id.Value}) to notify was not found.");
        }

        var orchestrationDescription = await _orchestrationRegister
            .GetAsync(orchestrationInstanceToNotify.OrchestrationDescriptionId)
            .ConfigureAwait(false);

        if (orchestrationDescription is null)
        {
            if (await _featureFlagManager.IsEnabledAsync(FeatureFlag.SilentMode).ConfigureAwait(false))
            {
                _logger.LogWarning(
                    $"Unable to find orchestration description '{orchestrationInstanceToNotify.OrchestrationDescriptionId}' for orchestration instance '{id.Value}' and event name '{eventName}'.");

                return;
            }

            throw new InvalidOperationException(
                $"Orchestration description (Id={orchestrationInstanceToNotify.OrchestrationDescriptionId.Value}) was not found.");
        }

        if (!orchestrationDescription.IsDurableFunction)
        {
            return;
        }

        await _executor.NotifyOrchestrationInstanceAsync(id, eventName, eventData).ConfigureAwait(false);
    }

    /// <summary>
    /// Validate orchestration description is known and enabled.
    /// </summary>
    private async Task<OrchestrationDescription> GuardMatchingOrchestrationDescriptionAsync(
        OrchestrationDescriptionUniqueName uniqueName)
    {
        var orchestrationDescription = await _orchestrationRegister.GetOrDefaultAsync(uniqueName, isEnabled: true).ConfigureAwait(false);
        return orchestrationDescription == null
            ? throw new InvalidOperationException($"No enabled orchestration description matches UniqueName='{uniqueName}'.")
            : orchestrationDescription;
    }

    /// <summary>
    /// Validate orchestration description is known, enabled, and that paramter value is valid according to its parameter definition.
    /// </summary>
    private async Task<OrchestrationDescription> GuardMatchingOrchestrationDescriptionWithInputAsync<TParameter>(
        OrchestrationDescriptionUniqueName uniqueName,
        TParameter inputParameter,
        IReadOnlyCollection<int> skipStepsBySequence)
            where TParameter : class
    {
        var orchestrationDescription = await _orchestrationRegister.GetOrDefaultAsync(uniqueName, isEnabled: true).ConfigureAwait(false);
        if (orchestrationDescription == null)
            throw new InvalidOperationException($"No enabled orchestration description matches UniqueName='{uniqueName}'.");

        var isValidParameterValue = await orchestrationDescription.ParameterDefinition.IsValidParameterValueAsync(inputParameter).ConfigureAwait(false);
        if (isValidParameterValue == false)
        {
            throw new InvalidOperationException("Paramater value is not valid compared to registered parameter definition.")
            {
                Data =
                {
                    { "UniqueName", uniqueName },
                    { "InputParameter", inputParameter },
                    { "SerializedParameterDefinition", orchestrationDescription.ParameterDefinition.SerializedParameterDefinition },
                },
            };
        }

        foreach (var stepSequence in skipStepsBySequence)
        {
            var stepOrDefault = orchestrationDescription.Steps.FirstOrDefault(step => step.Sequence == stepSequence);
            if (stepOrDefault == null)
                throw new InvalidOperationException($"No step description matches the sequence '{stepSequence}'.");

            if (stepOrDefault.CanBeSkipped == false)
                throw new InvalidOperationException($"Step description with sequence '{stepSequence}' cannot be skipped.");
        }

        return orchestrationDescription;
    }

    private async Task<OrchestrationInstance> CreateOrchestrationInstanceAsync(
        OperatingIdentity identity,
        OrchestrationDescription orchestrationDescription,
        Instant? runAt = default)
    {
        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
            identity,
            orchestrationDescription,
            skipStepsBySequence: [],
            clock: _clock,
            runAt: runAt);

        await _repository.AddAsync(orchestrationInstance).ConfigureAwait(false);
        await _repository.UnitOfWork.CommitAsync().ConfigureAwait(false);

        return orchestrationInstance;
    }

    private async Task<OrchestrationInstance> CreateOrchestrationInstanceWithInputAsync<TParameter>(
        OperatingIdentity identity,
        OrchestrationDescription orchestrationDescription,
        TParameter inputParameter,
        IReadOnlyCollection<int> skipStepsBySequence,
        Instant? runAt = default,
        IdempotencyKey? idempotencyKey = default,
        ActorMessageId? actorMessageId = default,
        TransactionId? transactionId = default,
        MeteringPointId? meteringPointId = default)
            where TParameter : class
    {
        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
            identity,
            orchestrationDescription,
            skipStepsBySequence,
            _clock,
            runAt,
            idempotencyKey,
            actorMessageId,
            transactionId,
            meteringPointId);

        orchestrationInstance.ParameterValue.SetFromInstance(inputParameter);

        await _repository.AddAsync(orchestrationInstance).ConfigureAwait(false);
        await _repository.UnitOfWork.CommitAsync().ConfigureAwait(false);

        return orchestrationInstance;
    }

    private async Task RequestStartOfOrchestrationInstanceIfPendingAsync(
        OrchestrationDescription orchestrationDescription,
        OrchestrationInstance orchestrationInstance)
    {
        if (!orchestrationDescription.IsDurableFunction)
        {
            return;
        }

        if (orchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleState.Pending)
        {
            await _executor
                .StartNewOrchestrationInstanceAsync(
                    orchestrationDescription,
                    orchestrationInstance)
                .ConfigureAwait(false);

            orchestrationInstance.Lifecycle.TransitionToQueued(_clock);
            await _repository.UnitOfWork.CommitAsync().ConfigureAwait(false);
        }
    }
}

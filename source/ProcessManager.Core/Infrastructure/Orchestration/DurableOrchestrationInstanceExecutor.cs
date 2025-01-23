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

using DurableTask.Core.Exceptions;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Core.Infrastructure.Orchestration;

/// <summary>
/// An executor implementation that uses <see cref="IDurableClient"/> to start
/// Durable Functions orchestration instances.
/// </summary>
/// <param name="logger"></param>
/// <param name="durableClient">Must be a Durable Task Client that is connected to
/// the same Task Hub as the Durable Functions host containing orchestrations.</param>
internal class DurableOrchestrationInstanceExecutor(
    ILogger<DurableOrchestrationInstanceExecutor> logger,
    IDurableClient durableClient) :
        IOrchestrationInstanceExecutor
{
    private readonly ILogger _logger = logger;
    private readonly IDurableClient _durableClient = durableClient;

    /// <inheritdoc />
    public async Task<bool> StartNewOrchestrationInstanceAsync(
        OrchestrationDescription orchestrationDescription,
        OrchestrationInstance orchestrationInstance)
    {
        var instanceId = orchestrationInstance.Id.Value.ToString();

        var existingInstance = await _durableClient.GetStatusAsync(instanceId).ConfigureAwait(false);
        if (existingInstance == null)
        {
            try
            {
                await _durableClient
                    .StartNewAsync(
                        orchestratorFunctionName: orchestrationDescription.FunctionName,
                        instanceId,
                        input: orchestrationInstance.ParameterValue.SerializedParameterValue)
                    .ConfigureAwait(false);

                return true;
            }
            catch (OrchestrationAlreadyExistsException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Cannot start new Durable Function orchestration with ID {OrchestrationInstance}. An Orchestration instance with the ID already exists.",
                    orchestrationInstance);
            }
        }

        return false;
    }

    /// <inheritdoc />
    public Task NotifyOrchestrationInstanceAsync<TData>(OrchestrationInstanceId id, string eventName, TData? data)
        where TData : class
    {
        return _durableClient.RaiseEventAsync(
            instanceId: id.Value.ToString(),
            eventName: eventName,
            eventData: data);
    }
}

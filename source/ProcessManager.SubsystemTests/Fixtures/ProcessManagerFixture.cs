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

using System.Diagnostics.CodeAnalysis;
using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.Core.TestCommon.Diagnostics;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures;

public class ProcessManagerFixture<TConfiguration> : IAsyncLifetime
{
    private readonly Guid _subsystemTestUserId = Guid.Parse("00000000-0000-0000-0000-000000000999");

    private readonly ServiceProvider _services;

    public ProcessManagerFixture()
    {
        Logger = new TestDiagnosticsLogger();

        Configuration = new ProcessManagerSubsystemTestConfiguration();

        var serviceCollection = BuildServices();
        _services = serviceCollection.BuildServiceProvider();

        TestConfiguration = default;
    }

    public ProcessManagerSubsystemTestConfiguration Configuration { get; }

    public TestDiagnosticsLogger Logger { get; }

    [NotNull]
    public TConfiguration? TestConfiguration { get; set; }

    public IProcessManagerMessageClient ProcessManagerMessageClient => _services.GetRequiredService<IProcessManagerMessageClient>();

    public IProcessManagerClient ProcessManagerHttpClient => _services.GetRequiredService<IProcessManagerClient>();

    [NotNull]
    public ActorIdentityDto? EnergySupplierActorIdentity { get; private set; }

    [NotNull]
    public UserIdentityDto? UserIdentity { get; private set; }

    public async Task InitializeAsync()
    {
        EnergySupplierActorIdentity = new ActorIdentityDto(
            ActorNumber.Create(Configuration.EnergySupplierActorNumber),
            ActorRole.EnergySupplier);

        UserIdentity = new UserIdentityDto(
            UserId: _subsystemTestUserId,
            ActorNumber: EnergySupplierActorIdentity.ActorNumber,
            ActorRole: EnergySupplierActorIdentity.ActorRole);

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    public void SetTestOutputHelper(ITestOutputHelper? testOutputHelper)
    {
        Logger.TestOutputHelper = testOutputHelper;
    }

    // /// <summary>
    // /// Wait for an orchestration instance to be returned by the ProcessManager http client. If step inputs are provided,
    // /// then the orchestration instance must have a step instance with the given step sequence and state.
    // /// <remarks>The lookup is based on the idempotency key of the test configuration request.</remarks>
    // /// </summary>
    // /// <param name="idempotencyKey">Find an orchestration instance with the given idempotency key.</param>
    // /// <param name="orchestrationInstanceState">If provided, then the orchestration instance have the given state.</param>
    // /// <param name="stepSequence">If provided, then the orchestration instance must have a step instance with the given sequence number.</param>
    // /// <param name="stepState">If provided, then the step should be in the given state (defaults to <see cref="StepInstanceLifecycleState.Terminated"/>).</param>
    // public async Task<(
    //     bool Success,
    //     OrchestrationInstanceTypedDto<TInputParameterDto>? OrchestrationInstance,
    //     StepInstanceDto? StepInstance)> WaitForOrchestrationInstanceAsync<TInputParameterDto>(
    //         string idempotencyKey,
    //         OrchestrationInstanceLifecycleState? orchestrationInstanceState = null,
    //         int? stepSequence = null,
    //         StepInstanceLifecycleState? stepState = null)
    //             where TInputParameterDto : class, IInputParameterDto
    // {
    //     if (stepState != null && stepSequence == null)
    //         throw new ArgumentNullException(nameof(stepSequence), $"{nameof(stepSequence)} must be provided if {nameof(stepState)} is not null.");
    //
    //     OrchestrationInstanceTypedDto<TInputParameterDto>? orchestrationInstance = null;
    //     StepInstanceDto? stepInstance = null;
    //
    //     var success = await Awaiter.TryWaitUntilConditionAsync(
    //         async () =>
    //         {
    //             orchestrationInstance = await ProcessManagerHttpClient
    //                 .GetOrchestrationInstanceByIdempotencyKeyAsync<TInputParameterDto>(
    //                     new GetOrchestrationInstanceByIdempotencyKeyQuery(
    //                         operatingIdentity: UserIdentity,
    //                         idempotencyKey: idempotencyKey),
    //                     CancellationToken.None);
    //
    //             if (orchestrationInstance == null)
    //                 return false;
    //
    //             if (stepSequence != null)
    //             {
    //                 stepInstance = orchestrationInstance.Steps
    //                     .SingleOrDefault(s => s.Sequence == stepSequence.Value);
    //             }
    //
    //             if (orchestrationInstanceState != null && orchestrationInstance.Lifecycle.State != orchestrationInstanceState)
    //                 return false;
    //
    //             // If step sequence is not provided, only check for orchestration instance existence
    //             if (stepSequence == null)
    //                 return true;
    //
    //             return stepInstance != null
    //                 ? stepInstance.Lifecycle.State == (stepState ?? StepInstanceLifecycleState.Terminated)
    //                 : throw new ArgumentException($"Step instance for step sequence {stepSequence} not found", nameof(stepSequence));
    //         },
    //         timeLimit: TimeSpan.FromMinutes(1),
    //         delay: TimeSpan.FromSeconds(1));
    //
    //     return (success, orchestrationInstance, stepInstance);
    // }

    private IServiceCollection BuildServices()
    {
        var serviceCollection = new ServiceCollection();

        // TODO: Get settings from app settings
        serviceCollection.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
            // Message client options
            {
                $"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.StartTopicName)}",
                Configuration.ProcessManagerStartTopicName
            },
            {
                $"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.NotifyTopicName)}",
                Configuration.ProcessManagerNotifyTopicName
            },
            {
                $"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.Brs021ForwardMeteredDataStartTopicName)}",
                Configuration.ProcessManagerBrs021StartTopicName
            },
            {
                $"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.Brs021ForwardMeteredDataNotifyTopicName)}",
                Configuration.ProcessManagerBrs021NotifyTopicName
            },
            // HTTP client options
            {
                $"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.ApplicationIdUri)}",
                Configuration.ProcessManagerApplicationIdUri
            },
            {
                $"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}",
                Configuration.ProcessManagerGeneralApiBaseAddress
            },
            {
                $"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}",
                Configuration.ProcessManagerOrchestrationsApiBaseAddress
            },
        });

        serviceCollection.AddAzureClients(b => b.AddServiceBusClientWithNamespace(Configuration.ServiceBusNamespace));
        serviceCollection.AddProcessManagerMessageClient();
        serviceCollection.AddProcessManagerHttpClients();

        return serviceCollection;
    }
}

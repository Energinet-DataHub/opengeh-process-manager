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

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client.Authorization;
using Energinet.DataHub.ProcessManager.Client.Extensions;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;

namespace Energinet.DataHub.ProcessManager.Client;

/// <summary>
/// Implementation of a Process Manager client that is intended to be used from the BFF
/// to start, schedule, cancel and query orchestration instances.
/// Endpoints which contains 'custom' in their path are expected to have been implemented
/// in the application where the Durable Function Orchestration recides.
/// This convention allows us to implement a general client that can route request to
/// either the general API or the "orchestrations" API.
/// </summary>
internal class ProcessManagerClient : IProcessManagerClient
{
    private readonly HttpClient _generalApiHttpClient;
    private readonly HttpClient _orchestrationsApiHttpClient;
    private readonly IAuthorizationHeaderProvider _authorizationHeaderProvider;

    public ProcessManagerClient(IHttpClientFactory httpClientFactory, IAuthorizationHeaderProvider authorizationHeaderProvider)
    {
        _generalApiHttpClient = httpClientFactory.CreateClient(HttpClientNames.GeneralApi);
        _orchestrationsApiHttpClient = httpClientFactory.CreateClient(HttpClientNames.OrchestrationsApi);
        _authorizationHeaderProvider = authorizationHeaderProvider;
    }

    /// <inheritdoc/>
    public async Task<Guid> ScheduleNewOrchestrationInstanceAsync(
        ScheduleOrchestrationInstanceCommand command,
        CancellationToken cancellationToken)
    {
        var commandHasInput = command
            .GetType()
            .IsSubclassOfRawGeneric(typeof(ScheduleOrchestrationInstanceCommand<>));

        var commandUrlPath = commandHasInput
            ? $"/api/orchestrationinstance/command/schedule/custom/{command.OrchestrationDescriptionUniqueName.Name}/{command.OrchestrationDescriptionUniqueName.Version}"
            : $"/api/orchestrationinstance/command/schedule";

        using var request = await CreateAuthorizedPostRequestAsync(commandUrlPath, cancellationToken).ConfigureAwait(false);
        // Ensure we serialize using the derived type and not the base type; otherwise we won't serialize all properties.
        var json = JsonSerializer.Serialize(command, command.GetType());
        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var actualResponse = commandHasInput
            ? await _orchestrationsApiHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false)
            : await _generalApiHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        actualResponse.EnsureSuccessStatusCode();

        var orchestrationInstanceId = await actualResponse.Content
            .ReadFromJsonAsync<Guid>(cancellationToken)
            .ConfigureAwait(false);

        return orchestrationInstanceId;
    }

    /// <inheritdoc/>
    public async Task CancelScheduledOrchestrationInstanceAsync(
        CancelScheduledOrchestrationInstanceCommand command,
        CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedPostRequestAsync("/api/orchestrationinstance/command/cancel", cancellationToken).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(command);
        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var actualResponse = await _generalApiHttpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        actualResponse.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async Task<Guid> StartNewOrchestrationInstanceAsync(
        StartOrchestrationInstanceCommand<UserIdentityDto> command,
        CancellationToken cancellationToken)
    {
        var commandHasInput = command
            .GetType()
            .IsSubclassOfRawGeneric(typeof(StartOrchestrationInstanceCommand<,>));

        var commandUrlPath = commandHasInput
            ? $"/api/orchestrationinstance/command/start/custom/{command.OrchestrationDescriptionUniqueName.Name}/{command.OrchestrationDescriptionUniqueName.Version}"
            : $"/api/orchestrationinstance/command/start";

        using var request = await CreateAuthorizedPostRequestAsync(commandUrlPath, cancellationToken).ConfigureAwait(false);
        // Ensure we serialize using the derived type and not the base type; otherwise we won't serialize all properties.
        var json = JsonSerializer.Serialize(command, command.GetType());
        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var actualResponse = commandHasInput
            ? await _orchestrationsApiHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false)
            : await _generalApiHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        actualResponse.EnsureSuccessStatusCode();

        var orchestrationInstanceId = await actualResponse.Content
            .ReadFromJsonAsync<Guid>(cancellationToken)
            .ConfigureAwait(false);

        return orchestrationInstanceId;
    }

    /// <inheritdoc/>
    public async Task<OrchestrationInstanceTypedDto> GetOrchestrationInstanceByIdAsync(
        GetOrchestrationInstanceByIdQuery query,
        CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedPostRequestAsync("/api/orchestrationinstance/query/id", cancellationToken).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(query);
        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var actualResponse = await _generalApiHttpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        actualResponse.EnsureSuccessStatusCode();

        var orchestrationInstance = await actualResponse.Content
            .ReadFromJsonAsync<OrchestrationInstanceTypedDto>(cancellationToken)
            .ConfigureAwait(false);

        return orchestrationInstance!;
    }

    /// <inheritdoc/>
    public async Task<OrchestrationInstanceTypedDto<TInputParameterDto>> GetOrchestrationInstanceByIdAsync<TInputParameterDto>(
        GetOrchestrationInstanceByIdQuery query,
        CancellationToken cancellationToken)
            where TInputParameterDto : class, IInputParameterDto
    {
        using var request = await CreateAuthorizedPostRequestAsync("/api/orchestrationinstance/query/id", cancellationToken).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(query);
        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var actualResponse = await _generalApiHttpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        actualResponse.EnsureSuccessStatusCode();

        var orchestrationInstance = await actualResponse.Content
            .ReadFromJsonAsync<OrchestrationInstanceTypedDto<TInputParameterDto>>(cancellationToken)
            .ConfigureAwait(false);

        return orchestrationInstance!;
    }

    /// <inheritdoc/>
    public async Task<OrchestrationInstanceTypedDto<TInputParameterDto>?> GetOrchestrationInstanceByIdempotencyKeyAsync<TInputParameterDto>(
        GetOrchestrationInstanceByIdempotencyKeyQuery query,
        CancellationToken cancellationToken)
            where TInputParameterDto : class, IInputParameterDto
    {
        using var request = await CreateAuthorizedPostRequestAsync("/api/orchestrationinstance/query/idempotencykey", cancellationToken).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(query);
        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var actualResponse = await _generalApiHttpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        actualResponse.EnsureSuccessStatusCode();

        if (actualResponse.Content == null
            || actualResponse.Content.Headers.ContentLength == 0)
        {
            return null;
        }

        var orchestrationInstance = await actualResponse.Content
            .ReadFromJsonAsync<OrchestrationInstanceTypedDto<TInputParameterDto>?>(cancellationToken)
            .ConfigureAwait(false);

        return orchestrationInstance;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<OrchestrationInstanceTypedDto>> SearchOrchestrationInstancesByNameAsync(
        SearchOrchestrationInstancesByNameQuery query,
        CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedPostRequestAsync("/api/orchestrationinstance/query/name", cancellationToken).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(query);
        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var actualResponse = await _generalApiHttpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        actualResponse.EnsureSuccessStatusCode();

        var orchestrationInstances = await actualResponse.Content
            .ReadFromJsonAsync<IReadOnlyCollection<OrchestrationInstanceTypedDto>>(cancellationToken)
            .ConfigureAwait(false);

        return orchestrationInstances!;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<OrchestrationInstanceTypedDto<TInputParameterDto>>> SearchOrchestrationInstancesByNameAsync<TInputParameterDto>(
        SearchOrchestrationInstancesByNameQuery query,
        CancellationToken cancellationToken)
            where TInputParameterDto : class, IInputParameterDto
    {
        using var request = await CreateAuthorizedPostRequestAsync("/api/orchestrationinstance/query/name", cancellationToken).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(query);
        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var actualResponse = await _generalApiHttpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        actualResponse.EnsureSuccessStatusCode();

        var orchestrationInstances = await actualResponse.Content
            .ReadFromJsonAsync<IReadOnlyCollection<OrchestrationInstanceTypedDto<TInputParameterDto>>>(cancellationToken)
            .ConfigureAwait(false);

        return orchestrationInstances!;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<TItem>> SearchOrchestrationInstancesByCustomQueryAsync<TItem>(
        SearchOrchestrationInstancesByCustomQuery<TItem> query,
        CancellationToken cancellationToken)
            where TItem : class
    {
        using var request = await CreateAuthorizedPostRequestAsync($"/api/orchestrationinstance/query/custom/{query.QueryRouteName}", cancellationToken).ConfigureAwait(false);
        // Ensure we serialize using the derived type and not the base type; otherwise we won't serialize all properties.
        var json = JsonSerializer.Serialize(query, query.GetType());
        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var actualResponse = await _orchestrationsApiHttpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        actualResponse.EnsureSuccessStatusCode();

        var orchestrationInstances = await actualResponse.Content
            .ReadFromJsonAsync<IReadOnlyCollection<TItem>>(cancellationToken)
            .ConfigureAwait(false);

        return orchestrationInstances!;
    }

    private async Task<HttpRequestMessage> CreateAuthorizedPostRequestAsync(string requestUri, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            requestUri);

        request.Headers.Authorization = await _authorizationHeaderProvider.CreateAuthorizationHeader(cancellationToken).ConfigureAwait(false);

        return request;
    }
}

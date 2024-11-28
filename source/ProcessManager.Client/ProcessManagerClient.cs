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
using Energinet.DataHub.ProcessManager.Api.Model;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;

namespace Energinet.DataHub.ProcessManager.Client;

/// <inheritdoc/>
internal class ProcessManagerClient : IProcessManagerClient
{
    private readonly HttpClient _generalApiHttpClient;
    private readonly HttpClient _orchestrationsApiHttpClient;

    public ProcessManagerClient(IHttpClientFactory httpClientFactory)
    {
        _generalApiHttpClient = httpClientFactory.CreateClient(HttpClientNames.GeneralApi);
        _orchestrationsApiHttpClient = httpClientFactory.CreateClient(HttpClientNames.OrchestrationsApi);
    }

    /// <inheritdoc/>
    public async Task<Guid> ScheduleNewOrchestrationInstanceAsync<TInputParameterDto>(
        ScheduleOrchestrationInstanceCommand<TInputParameterDto> command,
        CancellationToken cancellationToken)
            where TInputParameterDto : IInputParameterDto
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/processmanager/orchestrationinstance/{command.OrchestrationDescriptionUniqueName.Name}/{command.OrchestrationDescriptionUniqueName.Version}");
        var json = JsonSerializer.Serialize(command);
        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var actualResponse = await _orchestrationsApiHttpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        actualResponse.EnsureSuccessStatusCode();

        var calculationId = await actualResponse.Content
            .ReadFromJsonAsync<Guid>(cancellationToken)
            .ConfigureAwait(false);

        return calculationId;
    }

    /// <inheritdoc/>
    public async Task CancelScheduledOrchestrationInstanceAsync(
        CancelScheduledOrchestrationInstanceCommand command,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/processmanager/orchestrationinstance/cancel");
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
    public async Task<OrchestrationInstanceTypedDto<TInputParameterDto>> GetOrchestrationInstanceByIdAsync<TInputParameterDto>(
        GetOrchestrationInstanceByIdQuery query,
        CancellationToken cancellationToken)
            where TInputParameterDto : IInputParameterDto
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/processmanager/orchestrationinstance/query/id");
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
    public async Task<IReadOnlyCollection<OrchestrationInstanceTypedDto<TInputParameterDto>>> SearchOrchestrationInstancesByNameAsync<TInputParameterDto>(
        SearchOrchestrationInstancesByNameQuery query,
        CancellationToken cancellationToken)
            where TInputParameterDto : IInputParameterDto
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/processmanager/orchestrationinstance/query/name");
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
}

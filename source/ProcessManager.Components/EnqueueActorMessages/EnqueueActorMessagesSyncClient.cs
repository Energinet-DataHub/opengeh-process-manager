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

using System.Text;
using System.Text.Json;
using Energinet.DataHub.ProcessManager.Components.Abstractions.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;

namespace Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;

internal class EnqueueActorMessagesSyncClient(
    IHttpClientFactory httpClientFactory) : IEnqueueActorMessagesSyncClient
{
    public const string EdiEndpointPrefix = "api/enqueue/";
    private readonly HttpClient _client = httpClientFactory.CreateClient(HttpClientNames.EdiEnqueueActorMessageClientName);

    /// <inheritdoc/>
    public async Task EnqueueAsync<TMessageData>(TMessageData data)
        where TMessageData : IEnqueueDataSyncDto
    {
        var enqueueUrl = $"{EdiEndpointPrefix}{data.Route}";

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            enqueueUrl);

        var json = JsonSerializer.Serialize(data, data.GetType());

        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        var response = await _client.SendAsync(request).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }
}

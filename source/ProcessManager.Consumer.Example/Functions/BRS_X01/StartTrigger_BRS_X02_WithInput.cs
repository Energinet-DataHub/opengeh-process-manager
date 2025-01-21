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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.InputExample.V1.Model;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Consumer.Example.Functions.BRS_X01;

public class StartTrigger_BRS_X01_InputExample(
    IProcessManagerClient httpClient)
{
    private readonly IProcessManagerClient _httpClient = httpClient;

    [Function(nameof(StartTrigger_BRS_X01_InputExample))]
    public async Task<Guid> Run(
        [HttpTrigger] HttpRequestMessage req)
    {
        var orchestrationInstanceId = await _httpClient.StartNewOrchestrationInstanceAsync(
                new StartInputExampleCommandV1(
                    new UserIdentityDto(
                        UserId: Guid.NewGuid(),
                        ActorId: Guid.NewGuid()),
                    new InputV1(ShouldSkipSkippableStep: false)),
                CancellationToken.None)
            .ConfigureAwait(false);

        return orchestrationInstanceId;
    }
}

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
using Energinet.DataHub.ProcessManager.Client;

namespace Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;

public static class ProcessManagerClientExtensions
{
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
                                ActorId: Guid.NewGuid()),
                            idempotencyKey),
                        CancellationToken.None);

                if (orchestrationInstance is null)
                    return false;

                return comparer(orchestrationInstance);
            },
            timeLimit: TimeSpan.FromSeconds(120),
            delay: TimeSpan.FromSeconds(3));

        return (success, orchestrationInstance);
    }
}

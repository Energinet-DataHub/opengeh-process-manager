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

namespace Energinet.DataHub.ProcessManager.Abstractions.Api.Model.SendMeasurements;

/// <summary>
/// Query for Send Measurements instance by idempotency key.
/// Must be JSON serializable.
/// </summary>
public sealed record GetSendMeasurementsInstanceByIdempotencyKeyQuery
    : OrchestrationInstanceRequest<UserIdentityDto>
{
    /// <summary>
    /// Construct query.
    /// </summary>
    /// <param name="operatingIdentity">Identity of the user executing the query.</param>
    /// <param name="idempotencyKey">Idempotency key of the Send Measurements instance.</param>
    public GetSendMeasurementsInstanceByIdempotencyKeyQuery(
        UserIdentityDto operatingIdentity,
        string idempotencyKey)
            : base(operatingIdentity)
    {
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>
    /// Idempotency key of the Send Measurements instance.
    /// </summary>
    public string IdempotencyKey { get; }
}

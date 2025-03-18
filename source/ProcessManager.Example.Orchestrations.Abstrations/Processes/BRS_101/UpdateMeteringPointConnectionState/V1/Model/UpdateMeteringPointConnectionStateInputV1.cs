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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Components.Abstractions.BusinessValidation;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_101.UpdateMeteringPointConnectionState.V1.Model;

/// <summary>
/// Input to start a BRS-101 Update MeteringPoint Connection State orchestration instance.
/// </summary>
/// <param name="RequestedByActorNumber">GLN or EIC of the Actor who made the request.</param>
/// <param name="RequestedByActorRole">The actor role of the Actor who made the request.</param>
/// <param name="ActorMessageId">The message id from the actor document that started the BRS process.</param>
/// <param name="TransactionId">The transaction id that started the BRS process, typically found in the actor message.</param>
/// <param name="MeteringPointId">The ID of the MeteringPoint to connect/disconnect.</param>
/// <param name="IsConnected"><see langword="True"/> if MeteringPoint should be connected; otherwise <see langword="false"/>.</param>
public record UpdateMeteringPointConnectionStateInputV1(
    string RequestedByActorNumber, // TODO: Can we somehow use the "indentity" that is already required by the command (?)
    string RequestedByActorRole, // TODO: Can we somehow use the "indentity" that is already required by the command (?)
    string ActorMessageId, // TODO: Could we model this in a "input" base (?) or use the one on the command base
    string TransactionId, // TODO: Could we model this in a "input" base (?) or use the one on the command base
    string MeteringPointId, // TODO: Could we model this in a "input" base (?) or use the one on the command base
    bool IsConnected)
        : IInputParameterDto, IBusinessValidatedDto;

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

using Energinet.DataHub.MarketParticipant.Authorization.Model;
using Energinet.DataHub.MarketParticipant.Authorization.Model.AccessValidationRequests;
using Energinet.DataHub.MarketParticipant.Authorization.Services;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using NodaTime.Extensions;

namespace Energinet.DataHub.ProcessManager.Components.Authorization.Model;

public class AuthorizationClient(
    AuthorizationRequestService authorizationRequestService)
    : IAuthorizationClient
{
    private readonly AuthorizationRequestService _authorizationRequestService = authorizationRequestService;

    public async Task<IReadOnlyCollection<AuthorizedPeriod>> GetAuthorizedPeriodsAsync(
        ActorNumber actorNumber,
        ActorRole actorRole,
        MeteringPointId meteringPointId,
        RequestedPeriod requestedPeriod)
    {
        var period = new AccessPeriod(
                MeteringPointId: meteringPointId.Value,
                FromDate: requestedPeriod.Start,
                ToDate: requestedPeriod.End);

        var authRequest = new MeasurementsAccessValidationRequest
        {
                MeteringPointId = meteringPointId.Value,
                ActorNumber = actorNumber.Value,
                MarketRole = MapActorRole(actorRole),
                RequestedPeriod = period,
        };

        Signature? signature = null;
        try
        {
            signature = await _authorizationRequestService.RequestSignatureAsync(authRequest).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            // TODO: Update this
            throw new InvalidOperationException(
                $"Failed to request authorization signature for actor {actorNumber} with role {actorRole} on metering point {meteringPointId}.",
                ex);
        }

        if (signature.AccessPeriods == null)
        {
            // TODO: Update this
            throw new InvalidOperationException("Access periods cannot be null in the signature.");
        }

        var periods = signature.AccessPeriods.Select(CreatePeriod);
        return periods.ToList().AsReadOnly();
    }

    private EicFunction MapActorRole(ActorRole role)
    {
        return role switch
        {
            var r when r == ActorRole.MeteringPointAdministrator => EicFunction.MeteringPointAdministrator,
            var r when r == ActorRole.EnergySupplier => EicFunction.EnergySupplier,
            var r when r == ActorRole.GridAccessProvider => EicFunction.GridAccessProvider,
            var r when r == ActorRole.MeteredDataAdministrator => EicFunction.MeteredDataAdministrator,
            var r when r == ActorRole.MeteredDataResponsible => EicFunction.MeteredDataResponsible,
            var r when r == ActorRole.BalanceResponsibleParty => EicFunction.BalanceResponsibleParty,
            var r when r == ActorRole.ImbalanceSettlementResponsible => EicFunction.ImbalanceSettlementResponsible,
            var r when r == ActorRole.SystemOperator => EicFunction.SystemOperator,
            var r when r == ActorRole.DanishEnergyAgency => EicFunction.DanishEnergyAgency,
            var r when r == ActorRole.Delegated => EicFunction.Delegated,
            var r when r == ActorRole.DataHubAdministrator => EicFunction.DataHubAdministrator,
            _ => throw new ArgumentOutOfRangeException(nameof(role), $"Unsupported actor role: {role}"),
        };
    }

    private AuthorizedPeriod CreatePeriod(AccessPeriod period)
    {
        return new AuthorizedPeriod(
            MeteringPointId: new MeteringPointId(period.MeteringPointId),
            Start: period.FromDate.ToInstant(),
            End: period.ToDate.ToInstant());
    }
}

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

using Energinet.DataHub.ProcessManager.Components.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028.V1.Model;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_026_028.BRS_028.V1;

public class RequestCalculatedWholesaleServicesInputV1Builder
{
    private const string ValidGlnNumber = "1111111111111";

    private readonly string _actorMessageId;
    private readonly string _transactionId;
    private string _requestedForActorNumber;
    private string _requestedForActorRole;
    private string _businessReason;
    private string? _resolution;
    private string _periodStart;
    private string? _periodEnd;
    private string? _energySupplierNumber;
    private string? _chargeOwnerNumber;
    private IReadOnlyCollection<string> _gridAreas;
    private string? _settlementVersion;
    private IReadOnlyCollection<RequestCalculatedWholesaleServicesInputV1.ChargeTypeInput>? _chargeTypes;

    /// <summary>
    /// Creates a new RequestCalculatedEnergyTimeSeriesInputV1Builder with default values
    /// </summary>
    public RequestCalculatedWholesaleServicesInputV1Builder(ActorRole forActorRole)
    {
        _actorMessageId = Guid.NewGuid().ToString();
        _transactionId = Guid.NewGuid().ToString();

        _requestedForActorNumber = ValidGlnNumber;
        _requestedForActorRole = forActorRole.Name;
        _businessReason = BusinessReason.BalanceFixing.Name;

        // Period from 1/2/2024 to 28/2/2024
        _periodStart = "2024-01-31T23:00:00Z";
        _periodEnd = "2024-02-29T23:00:00Z";

        _gridAreas = [];

        if (forActorRole == ActorRole.EnergySupplier)
            _energySupplierNumber = _requestedForActorNumber;
        else if (forActorRole == ActorRole.GridAccessProvider)
            _gridAreas = ["804"];
    }

    public RequestCalculatedWholesaleServicesInputV1Builder WithRequestedForActorNumber(string requestedForActorNumber)
    {
        _requestedForActorNumber = requestedForActorNumber;
        return this;
    }

    public RequestCalculatedWholesaleServicesInputV1Builder WithRequestedForActorRole(ActorRole requestedForActorRole)
    {
        _requestedForActorRole = requestedForActorRole.Name;
        return this;
    }

    public RequestCalculatedWholesaleServicesInputV1Builder WithRequestedForActorRole(string requestedForActorRole)
    {
        _requestedForActorRole = requestedForActorRole;
        return this;
    }

    public RequestCalculatedWholesaleServicesInputV1Builder WithBusinessReason(BusinessReason businessReason)
    {
        _businessReason = businessReason.Name;
        return this;
    }

    public RequestCalculatedWholesaleServicesInputV1Builder WithBusinessReason(string businessReason)
    {
        _businessReason = businessReason;
        return this;
    }

    public RequestCalculatedWholesaleServicesInputV1Builder WithResolution(string? resolution)
    {
        _resolution = resolution;
        return this;
    }

    public RequestCalculatedWholesaleServicesInputV1Builder WithPeriod(Instant periodStart, Instant? periodEnd)
    {
        _periodStart = periodStart.ToString();
        _periodEnd = periodEnd?.ToString();
        return this;
    }

    public RequestCalculatedWholesaleServicesInputV1Builder WithPeriod(string periodStart, string? periodEnd)
    {
        _periodStart = periodStart;
        _periodEnd = periodEnd;
        return this;
    }

    public RequestCalculatedWholesaleServicesInputV1Builder WithPeriodEnd(Instant? periodEnd)
    {
        _periodEnd = periodEnd?.ToString();
        return this;
    }

    public RequestCalculatedWholesaleServicesInputV1Builder WithEnergySupplierNumber(string? energySupplierNumber)
    {
        _energySupplierNumber = energySupplierNumber;
        return this;
    }

    public RequestCalculatedWholesaleServicesInputV1Builder WithBalanceResponsibleNumber(string? chargeOwnerNumber)
    {
        _chargeOwnerNumber = chargeOwnerNumber;
        return this;
    }

    public RequestCalculatedWholesaleServicesInputV1Builder WithGridArea(string? gridArea)
    {
        _gridAreas = !string.IsNullOrEmpty(gridArea)
            ? [gridArea]
            : [];

        return this;
    }

    public RequestCalculatedWholesaleServicesInputV1Builder WithGridAreas(IReadOnlyCollection<string> gridAreas)
    {
        _gridAreas = gridAreas;
        return this;
    }

    public RequestCalculatedWholesaleServicesInputV1Builder WithSettlementVersion(string? settlementVersion)
    {
        _settlementVersion = settlementVersion;
        return this;
    }

    public RequestCalculatedWholesaleServicesInputV1Builder WithChargeTypes(
        IReadOnlyCollection<RequestCalculatedWholesaleServicesInputV1.ChargeTypeInput>? chargeTypes)
    {
        _chargeTypes = chargeTypes;
        return this;
    }

    public RequestCalculatedWholesaleServicesInputV1Builder WithChargeType(
        RequestCalculatedWholesaleServicesInputV1.ChargeTypeInput? chargeType)
    {
        _chargeTypes = chargeType is not null
            ? [chargeType]
            : null;

        return this;
    }

    public RequestCalculatedWholesaleServicesInputV1 Build()
    {
        return new RequestCalculatedWholesaleServicesInputV1(
            ActorMessageId: _actorMessageId,
            TransactionId: _transactionId,
            RequestedForActorNumber: _requestedForActorNumber,
            RequestedForActorRole: _requestedForActorRole,
            RequestedByActorNumber: _requestedForActorNumber,
            RequestedByActorRole: _requestedForActorRole,
            BusinessReason: _businessReason,
            Resolution: _resolution,
            PeriodStart: _periodStart,
            PeriodEnd: _periodEnd,
            EnergySupplierNumber: _energySupplierNumber,
            ChargeOwnerNumber: _chargeOwnerNumber,
            GridAreas: _gridAreas,
            SettlementVersion: _settlementVersion,
            ChargeTypes: _chargeTypes);
    }
}

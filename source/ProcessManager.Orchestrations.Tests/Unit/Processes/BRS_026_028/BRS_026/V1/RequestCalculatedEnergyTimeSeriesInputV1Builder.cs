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

using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_026.V1.Model;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_026_028.BRS_026.V1;

public class RequestCalculatedEnergyTimeSeriesInputV1Builder
{
    private const string ValidGlnNumber = "1111111111111";

    private string _requestedForActorNumber;
    private string _requestedForActorRole;
    private string _businessReason;
    private string _periodStart;
    private string? _periodEnd;
    private string? _energySupplierNumber;
    private string? _balanceResponsibleNumber;
    private IReadOnlyCollection<string> _gridAreas;
    private string? _meteringPointType;
    private string? _settlementMethod;
    private string? _settlementVersion;

    /// <summary>
    /// Creates a new RequestCalculatedEnergyTimeSeriesInputV1Builder with default values
    /// </summary>
    public RequestCalculatedEnergyTimeSeriesInputV1Builder(ActorRole forActorRole)
    {
        _requestedForActorNumber = ValidGlnNumber;
        _requestedForActorRole = forActorRole.Name;
        _businessReason = BusinessReason.BalanceFixing.Name;

        // Period from 1/2/2024 to 5/2/2024
        _periodStart = "2024-01-31T23:00:00Z";
        _periodEnd = "2024-02-04T23:00:00Z";

        _gridAreas = [];

        if (forActorRole == ActorRole.EnergySupplier)
            _energySupplierNumber = _requestedForActorNumber;
        else if (forActorRole == ActorRole.BalanceResponsibleParty)
            _balanceResponsibleNumber = _requestedForActorNumber;
        else if (forActorRole == ActorRole.GridAccessProvider)
            _gridAreas = ["804"];
    }

    public RequestCalculatedEnergyTimeSeriesInputV1Builder WithRequestedForActorNumber(string requestedForActorNumber)
    {
        _requestedForActorNumber = requestedForActorNumber;
        return this;
    }

    public RequestCalculatedEnergyTimeSeriesInputV1Builder WithRequestedForActorRole(string requestedForActorRole)
    {
        _requestedForActorRole = requestedForActorRole;
        return this;
    }

    public RequestCalculatedEnergyTimeSeriesInputV1Builder WithBusinessReason(string businessReason)
    {
        _businessReason = businessReason;
        return this;
    }

    public RequestCalculatedEnergyTimeSeriesInputV1Builder WithPeriod(Instant periodStart, Instant? periodEnd)
    {
        _periodStart = periodStart.ToString();
        _periodEnd = periodEnd?.ToString();
        return this;
    }

    public RequestCalculatedEnergyTimeSeriesInputV1Builder WithPeriod(string periodStart, string? periodEnd)
    {
        _periodStart = periodStart;
        _periodEnd = periodEnd;
        return this;
    }

    public RequestCalculatedEnergyTimeSeriesInputV1Builder WithPeriodEnd(Instant? periodEnd)
    {
        _periodEnd = periodEnd?.ToString();
        return this;
    }

    public RequestCalculatedEnergyTimeSeriesInputV1Builder WithEnergySupplierNumber(string? energySupplierNumber)
    {
        _energySupplierNumber = energySupplierNumber;
        return this;
    }

    public RequestCalculatedEnergyTimeSeriesInputV1Builder WithBalanceResponsibleNumber(string? balanceResponsibleNumber)
    {
        _balanceResponsibleNumber = balanceResponsibleNumber;
        return this;
    }

    public RequestCalculatedEnergyTimeSeriesInputV1Builder WithGridArea(string? gridArea)
    {
        _gridAreas = !string.IsNullOrEmpty(gridArea)
            ? [gridArea]
            : [];

        return this;
    }

    public RequestCalculatedEnergyTimeSeriesInputV1Builder WithGridAreas(IReadOnlyCollection<string> gridAreas)
    {
        _gridAreas = gridAreas;
        return this;
    }

    public RequestCalculatedEnergyTimeSeriesInputV1Builder WithMeteringPointType(string? meteringPointType)
    {
        _meteringPointType = meteringPointType;
        return this;
    }

    public RequestCalculatedEnergyTimeSeriesInputV1Builder WithSettlementMethod(string? settlementMethod)
    {
        _settlementMethod = settlementMethod;
        return this;
    }

    public RequestCalculatedEnergyTimeSeriesInputV1Builder WithSettlementVersion(string? settlementVersion)
    {
        _settlementVersion = settlementVersion;
        return this;
    }

    public RequestCalculatedEnergyTimeSeriesInputV1 Build()
    {
        return new RequestCalculatedEnergyTimeSeriesInputV1(
            _requestedForActorNumber,
            _requestedForActorRole,
            _businessReason,
            _periodStart,
            _periodEnd,
            _energySupplierNumber,
            _balanceResponsibleNumber,
            _gridAreas,
            _meteringPointType,
            _settlementMethod,
            _settlementVersion);
    }
}

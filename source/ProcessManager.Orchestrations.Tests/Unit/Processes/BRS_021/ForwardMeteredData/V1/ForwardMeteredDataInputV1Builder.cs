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

using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1;

public class ForwardMeteredDataInputV1Builder
{
    private const string ActorNumber = "1234567890123";

    private string _actorMessageId = "MessageId";
    private string _transactionId = "TransactionId";
    private string _actorNumber = ActorNumber;
    private string _actorRole = ActorRole.GridAccessProvider.Name;
    private string _meteringPointId = "MeteringPointId";
    private string _meteringPointType = MeteringPointType.Production.Name;
    private string _productNumber = "ProductNumber";
    private string _measureUnit = MeasurementUnit.KilowattHour.Name;
    private string _registrationDateTime = "2024-12-31T23:00:00Z";
    private string _resolution = Resolution.QuarterHourly.Name;
    private string _startDateTime = "2024-12-31T23:00:00Z";
    private string? _endDateTime = "2025-12-31T23:15:00Z";
    private string _gridAccessProviderNumber = ActorNumber;
    private IReadOnlyCollection<ForwardMeteredDataInputV1.EnergyObservation> _energyObservations =
    [
        new("1", "1024", Quality.AsProvided.Name),
    ];

    public ForwardMeteredDataInputV1Builder WithActorMessageId(string actorMessageId)
    {
        _actorMessageId = actorMessageId;
        return this;
    }

    public ForwardMeteredDataInputV1Builder WithTransactionId(string transactionId)
    {
        _transactionId = transactionId;
        return this;
    }

    public ForwardMeteredDataInputV1Builder WithActorNumber(string actorNumber)
    {
        _actorNumber = actorNumber;
        return this;
    }

    public ForwardMeteredDataInputV1Builder WithActorRole(string actorRole)
    {
        _actorRole = actorRole;
        return this;
    }

    public ForwardMeteredDataInputV1Builder WithMeteringPointId(string meteringPointId)
    {
        _meteringPointId = meteringPointId;
        return this;
    }

    public ForwardMeteredDataInputV1Builder WithMeteringPointType(string meteringPointType)
    {
        _meteringPointType = meteringPointType;
        return this;
    }

    public ForwardMeteredDataInputV1Builder WithProductNumber(string productNumber)
    {
        _productNumber = productNumber;
        return this;
    }

    public ForwardMeteredDataInputV1Builder WithMeasureUnit(string measureUnit)
    {
        _measureUnit = measureUnit;
        return this;
    }

    public ForwardMeteredDataInputV1Builder WithRegistrationDateTime(string registrationDateTime)
    {
        _registrationDateTime = registrationDateTime;
        return this;
    }

    public ForwardMeteredDataInputV1Builder WithResolution(string resolution)
    {
        _resolution = resolution;
        return this;
    }

    public ForwardMeteredDataInputV1Builder WithStartDateTime(string startDateTime)
    {
        _startDateTime = startDateTime;
        return this;
    }

    public ForwardMeteredDataInputV1Builder WithEndDateTime(string? endDateTime)
    {
        _endDateTime = endDateTime;
        return this;
    }

    public ForwardMeteredDataInputV1Builder WithGridAccessProviderNumber(string gridAccessProviderNumber)
    {
        _gridAccessProviderNumber = gridAccessProviderNumber;
        return this;
    }

    public ForwardMeteredDataInputV1Builder WithEnergyObservations(
        IReadOnlyCollection<ForwardMeteredDataInputV1.EnergyObservation> energyObservations)
    {
        _energyObservations = energyObservations;
        return this;
    }

    public ForwardMeteredDataInputV1 Build()
    {
        return new ForwardMeteredDataInputV1(
            ActorMessageId: _actorMessageId,
            TransactionId: _transactionId,
            ActorNumber: _actorNumber,
            ActorRole: _actorRole,
            MeteringPointId: _meteringPointId,
            MeteringPointType: _meteringPointType,
            ProductNumber: _productNumber,
            MeasureUnit: _measureUnit,
            RegistrationDateTime: _registrationDateTime,
            Resolution: _resolution,
            StartDateTime: _startDateTime,
            EndDateTime: _endDateTime,
            GridAccessProviderNumber: _gridAccessProviderNumber,
            DelegatedGridAreaCodes: null,
            EnergyObservations: _energyObservations);
    }
}

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

using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Components.Datahub.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026.V1.BusinessValidation.Rules;

public class TimeSeriesTypeValidationRule : IBusinessValidationRule<RequestCalculatedEnergyTimeSeriesInputV1>
{
    private static readonly ValidationError _invalidTimeSeriesTypeForActor = new(
        "Den forespurgte tidsserie type kan ikke forespørges som en {PropertyName} / The requested time series type can not be requested as a {PropertyName}",
        "D11");

    private static IList<ValidationError> NoError => new List<ValidationError>();

    public Task<IList<ValidationError>> ValidateAsync(RequestCalculatedEnergyTimeSeriesInputV1 subject)
    {
        if (subject.RequestedForActorRole == ActorRole.MeteredDataResponsible.Name)
            return Task.FromResult(NoError);

        if (subject.MeteringPointType == MeteringPointType.Exchange.Name)
            return Task.FromResult(InvalidTimeSeriesTypeForActor(subject.RequestedForActorRole));

        if (subject.MeteringPointType == MeteringPointType.Consumption.Name && subject.SettlementMethod is null)
            return Task.FromResult(InvalidTimeSeriesTypeForActor(subject.RequestedForActorRole));

        return Task.FromResult(NoError);
    }

    private IList<ValidationError> InvalidTimeSeriesTypeForActor(string actorRole)
    {
        return new List<ValidationError> { _invalidTimeSeriesTypeForActor.WithPropertyName(actorRole) };
    }
}

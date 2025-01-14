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

using System.Globalization;
using Energinet.DataHub.ElectricityMarket.Integration;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Extensions;
using Microsoft.Azure.Functions.Worker;
using NodaTime;
using NodaTime.Text;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Activities;

internal sealed class GetMeteringPointMasterDataActivity_Brs_021_ForwardMeteredData_V1(
    IClock clock,
    IOrchestrationInstanceProgressRepository progressRepository,
    IElectricityMarketViews electricityMarketViews)
    : ProgressActivityBase(
        clock,
        progressRepository)
{
    private readonly IElectricityMarketViews _electricityMarketViews = electricityMarketViews;

    [Function(nameof(GetMeteringPointMasterDataActivity_Brs_021_ForwardMeteredData_V1))]
    public async Task<ActivityOutput> Run(
        [ActivityTrigger] ActivityInput activityInput)
    {
        if (activityInput.MeteringPointIdentification is null || activityInput.EndDateTime is null)
        {
            return new([]);
        }

        var id = new MeteringPointIdentification(activityInput.MeteringPointIdentification);
        var startDateTime = InstantPatternWithOptionalSeconds.Parse(activityInput.StartDateTime);
        var endDateTime = InstantPatternWithOptionalSeconds.Parse(activityInput.EndDateTime);

        if (!startDateTime.Success || !endDateTime.Success)
        {
            return new([]);
        }

        return new(
            await _electricityMarketViews
            .GetMeteringPointMasterDataChangesAsync(id, new Interval(startDateTime.Value, endDateTime.Value))
            .ToListAsync()
            .ConfigureAwait(false));
    }

    public sealed record ActivityInput(string? MeteringPointIdentification, string StartDateTime, string? EndDateTime);

    public sealed record ActivityOutput(IReadOnlyCollection<MeteringPointMasterData> MeteringPointMasterData);
}

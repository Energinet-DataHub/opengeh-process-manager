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

using Energinet.DataHub.ElectricityMarket.Integration;
using Energinet.DataHub.ElectricityMarket.Integration.Models.Common;
using Energinet.DataHub.ElectricityMarket.Integration.Models.GridAreas;
using Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData;
using Energinet.DataHub.ElectricityMarket.Integration.Models.ProcessDelegation;
using Energinet.DataHub.ProcessManager.Components.Extensions;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData;

public class ElectricityMarketViewsFactory(
    IOptions<ProcessManagerComponentsOptions> options,
    IElectricityMarketViews electricityMarketViews)
{
    private readonly IElectricityMarketViews _electricityMarketViews = electricityMarketViews;
    private readonly ProcessManagerComponentsOptions _options = options.Value;

    public IElectricityMarketViews Create(MeteringPointIdentification meteringPointId)
    {
        if (!_options.AllowMockDependenciesForTests)
            return _electricityMarketViews;

        return meteringPointId.Value.IsTestMeteringPointId()
            ? new ElectricityMarketViewsMock()
            : _electricityMarketViews;
    }

    /// <summary>
    /// An electricity market views mock implementation, that always returns a fixed data set.
    /// </summary>
    private class ElectricityMarketViewsMock : IElectricityMarketViews
    {
        public Task<IEnumerable<ElectricityMarket.Integration.Models.MasterData.MeteringPointMasterData>>
            GetMeteringPointMasterDataChangesAsync(MeteringPointIdentification meteringPointId, Interval period)
        {
            IEnumerable<ElectricityMarket.Integration.Models.MasterData.MeteringPointMasterData> mockData =
            [
                new ElectricityMarket.Integration.Models.MasterData.MeteringPointMasterData
                {
                    Identification = meteringPointId,
                    Type = MeteringPointType.Consumption,
                    SubType = MeteringPointSubType.Physical,
                    ConnectionState = ConnectionState.Connected,
                    ProductId = ProductId.EnergyActive,
                    Resolution = new Resolution("PT15M"),
                    Unit = MeasureUnit.kWh,
                    ParentIdentification = null,
                    EnergySupplier = "1234567890123",
                    GridAccessProvider = "1234567890123",
                    GridAreaCode = new GridAreaCode("001"),
                    NeighborGridAreaOwners = [],
                    ValidFrom = period.Start,
                    ValidTo = period.End,
                },
            ];

            return Task.FromResult(mockData);
        }

        public Task<ProcessDelegationDto?> GetProcessDelegationAsync(
            string actorNumber,
            EicFunction actorRole,
            string gridAreaCode,
            DelegatedProcess processType)
        {
            throw new NotImplementedException();
        }

        public Task<GridAreaOwnerDto?> GetGridAreaOwnerAsync(string gridAreaCode)
        {
            throw new NotImplementedException();
        }
    }
}

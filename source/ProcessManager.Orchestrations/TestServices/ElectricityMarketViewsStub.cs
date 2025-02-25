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

using System.Reflection;
using System.Runtime.CompilerServices;
using Energinet.DataHub.ElectricityMarket.Integration;
using Energinet.DataHub.ElectricityMarket.Integration.Models.Common;
using Energinet.DataHub.ElectricityMarket.Integration.Models.GridAreas;
using Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData;
using Energinet.DataHub.ElectricityMarket.Integration.Models.ProcessDelegation;
using NodaTime;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace Energinet.DataHub.ProcessManager.Orchestrations.TestServices;

// TODO (ID-283)
public sealed class ElectricityMarketViewsStub : IElectricityMarketViews
{
    public async Task<IEnumerable<MeteringPointMasterData>> GetMeteringPointMasterDataChangesAsync(
        MeteringPointIdentification meteringPointId,
        Interval period)
    {
        if (meteringPointId.Value == "NoMasterData")
        {
            return [];
        }

        var meteringPointMasterData =
            (MeteringPointMasterData)Activator.CreateInstance(typeof(MeteringPointMasterData))!;

        var resolution =
            (Resolution)RuntimeHelpers.GetUninitializedObject(typeof(Resolution));

        SetProperty(resolution, "Value", "PT1H");

        SetProperty(meteringPointMasterData, "Identification", new MeteringPointIdentification("StubId"));
        SetProperty(meteringPointMasterData, "GridAreaCode", new GridAreaCode("ZZZ"));
        SetProperty(meteringPointMasterData, "GridAccessProvider", "1111111111111");
        SetProperty(meteringPointMasterData, "ConnectionState", ConnectionState.Connected);
        SetProperty(meteringPointMasterData, "Type", MeteringPointType.Consumption);
        SetProperty(meteringPointMasterData, "SubType", MeteringPointSubType.Physical);
        SetProperty(meteringPointMasterData, "Resolution", resolution);
        SetProperty(meteringPointMasterData, "Unit", MeasureUnit.kWh);
        SetProperty(meteringPointMasterData, "ProductId", ProductId.FuelQuantity);

        return [meteringPointMasterData];
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

    public IAsyncEnumerable<MeteringPointEnergySupplier> GetMeteringPointEnergySuppliersAsync(
        MeteringPointIdentification meteringPointId,
        Interval period) => throw new NotImplementedException();

    private static void SetProperty(object obj, string propertyName, object value)
    {
        var property = obj.GetType()
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (property != null && property.CanWrite)
        {
            property.SetValue(obj, value);
        }
    }
}

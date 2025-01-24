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
using NodaTime;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace Energinet.DataHub.ProcessManager.Orchestrations.TestServices;

// TODO (ID-283)
public sealed class ElectricityMarketViewsStub : IElectricityMarketViews
{
    public async IAsyncEnumerable<MeteringPointMasterData> GetMeteringPointMasterDataChangesAsync(
        MeteringPointIdentification meteringPointId,
        Interval period)
    {
        if (meteringPointId.Value == "NoMasterData")
        {
            yield break;
        }

        var meteringPointMasterData =
            (MeteringPointMasterData)Activator.CreateInstance(typeof(MeteringPointMasterData))!;

        var resolution =
            (Resolution)RuntimeHelpers.GetUninitializedObject(typeof(Resolution));

        SetProperty(resolution, "Value", "PT1H");

        SetProperty(meteringPointMasterData, "Identification", new MeteringPointIdentification("StubId"));
        SetProperty(meteringPointMasterData, "GridAreaCode", new GridAreaCode("ZZZ"));
        SetProperty(meteringPointMasterData, "GridAccessProvider", new StubActorNumber("1111111111111"));
        SetProperty(meteringPointMasterData, "ConnectionState", ConnectionState.Connected);
        SetProperty(meteringPointMasterData, "Type", MeteringPointType.Consumption);
        SetProperty(meteringPointMasterData, "SubType", MeteringPointSubType.Physical);
        SetProperty(meteringPointMasterData, "Resolution", resolution);
        SetProperty(meteringPointMasterData, "Unit", MeasureUnit.kWh);
        SetProperty(meteringPointMasterData, "ProductId", ProductId.FuelQuantity);

        yield return meteringPointMasterData;
    }

    public async IAsyncEnumerable<MeteringPointEnergySupplier> GetMeteringPointEnergySuppliersAsync(
        MeteringPointIdentification meteringPointId,
        Interval period)
    {
        var meteringPointEnergySupplier =
            (MeteringPointEnergySupplier)Activator.CreateInstance(typeof(MeteringPointEnergySupplier))!;

        SetProperty(meteringPointEnergySupplier, "Identification", meteringPointId);
        SetProperty(meteringPointEnergySupplier, "EnergySupplier", new StubActorNumber("1111111111111"));
        SetProperty(meteringPointEnergySupplier, "StartDate", period.Start);
        SetProperty(meteringPointEnergySupplier, "EndDate", period.End);

        yield return meteringPointEnergySupplier;
    }

    private static void SetProperty(object obj, string propertyName, object value)
    {
        var property = obj.GetType()
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (property != null && property.CanWrite)
        {
            property.SetValue(obj, value);
        }
    }

    public sealed record StubActorNumber : ActorNumber
    {
        public StubActorNumber(string value)
            : base(value)
        {
        }
    }
}

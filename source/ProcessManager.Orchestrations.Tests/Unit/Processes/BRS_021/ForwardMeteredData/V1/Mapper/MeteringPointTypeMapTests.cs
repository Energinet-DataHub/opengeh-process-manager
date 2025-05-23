﻿// Copyright 2020 Energinet DataHub A/S
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

using Energinet.DataHub.ElectricityMarket.Integration.Models.MasterData;
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.ForwardMeteredData.V1.Mapper;

public class MeteringPointTypeMapTests
{
    [Fact]
    public void AllMasterDataMeteringPointType_Mapping_ShouldBeMappedCorrect()
    {
        var allMasterDataMeteringPointTypes = Enum.GetValues(typeof(MeteringPointType));

        foreach (MeteringPointType masterDataMeteringPointType in allMasterDataMeteringPointTypes)
        {
            var processManagerMeteringPointTypeResult = ElectricityMarketMasterDataMapper
                .MeteringPointTypeMap.Map(masterDataMeteringPointType);
            Assert.NotNull(processManagerMeteringPointTypeResult);
        }
    }
}

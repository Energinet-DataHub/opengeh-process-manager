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

using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData;
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.MeteringPointMasterData;

public sealed class AdditionalMeasurementsRecipientsProviderTests
{
    private static readonly MeteringPointId _validMeteringPointTest = new("570715000001542000");

    [Theory]
    [InlineData(ConstantAdditionalMeasurementsRecipientsProvider.AdditionalRecipientConstantSourceSelector.Empty)]
    [InlineData((ConstantAdditionalMeasurementsRecipientsProvider.AdditionalRecipientConstantSourceSelector)1337)]
    public async Task Given_InvalidEnum_When_GetRecipients_Then_AlwaysReturnEmptySet(ConstantAdditionalMeasurementsRecipientsProvider.AdditionalRecipientConstantSourceSelector selector)
    {
        // Arrange
        var sut = new ConstantAdditionalMeasurementsRecipientsProvider(selector);

        // Act
        var result = await sut.GetAdditionalRecipients(_validMeteringPointTest).ToListAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Given_TestSource_When_GetRecipients_Then_ReturnsRelevantData()
    {
        // Arrange
        var sut = new ConstantAdditionalMeasurementsRecipientsProvider(ConstantAdditionalMeasurementsRecipientsProvider.AdditionalRecipientConstantSourceSelector.Test);

        // Act
        var result = await sut.GetAdditionalRecipients(_validMeteringPointTest).ToListAsync();

        // Assert
        Assert.Single(result, actor => actor.Number.Value == "5790000432000" && actor.Role.Name == "SystemOperator");
    }
}

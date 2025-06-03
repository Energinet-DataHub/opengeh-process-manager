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

using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model;

namespace Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData;

public sealed class ConstantAdditionalMeasurementsRecipientsProvider : IAdditionalMeasurementsRecipientsProvider
{
    private static readonly Dictionary<MeteringPointId, List<Actor>> _developmentSource = new()
    {
        { new("571313101700011887"), [Actor.From("5790000432000", "SystemOperator")] },
    };

    private static readonly Dictionary<MeteringPointId, List<Actor>> _testSource = new()
    {
        { new("570715000001542000"), [Actor.From("5790000432000", "SystemOperator")] },
        { new("570715000001542001"), [Actor.From("5790000432000", "SystemOperator")] },
        { new("570715000001542002"), [Actor.From("5790000432000", "SystemOperator")] },
    };

    private static readonly Dictionary<MeteringPointId, List<Actor>> _preproductionSource = new();

    private static readonly Dictionary<MeteringPointId, List<Actor>> _productionSource = new();

    private readonly Dictionary<MeteringPointId, List<Actor>> _selectedSource;

    public ConstantAdditionalMeasurementsRecipientsProvider(AdditionalRecipientConstantSourceSelector sourceSelector)
    {
        _selectedSource = sourceSelector switch
        {
            AdditionalRecipientConstantSourceSelector.Development => _developmentSource,
            AdditionalRecipientConstantSourceSelector.Test => _testSource,
            AdditionalRecipientConstantSourceSelector.Preproduction => _preproductionSource,
            AdditionalRecipientConstantSourceSelector.Production => _productionSource,
            _ => [],
        };
    }

    public enum AdditionalRecipientConstantSourceSelector
    {
        Empty,
        Development,
        Test,
        Preproduction,
        Production,
    }

    public IAsyncEnumerable<Actor> GetAdditionalRecipients(MeteringPointId meteringPointId)
    {
        return (_selectedSource.TryGetValue(meteringPointId, out var recipients) ? recipients : []).ToAsyncEnumerable();
    }
}

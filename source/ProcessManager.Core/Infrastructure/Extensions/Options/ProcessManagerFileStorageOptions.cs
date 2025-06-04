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

using System.ComponentModel.DataAnnotations;

namespace Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Options;

public class ProcessManagerFileStorageOptions
{
    public const string SectionName = "ProcessManagerFileStorage";

    /// <summary>
    /// Do NOT rename this property. The "ServiceUri" property name is used by convention in .NET, when using the
    /// AzureClientFactoryBuilder.AddBlobServiceClient(configuration) method.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string ServiceUri { get; set; } = string.Empty;
}

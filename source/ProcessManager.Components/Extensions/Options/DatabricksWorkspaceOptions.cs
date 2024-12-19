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

namespace ProcessManager.Components.Extensions.Options;

public class DatabricksWorkspaceOptions
{
    public const string SectionName = "DatabricksWorkspace";

    /// <summary>
    /// Workspace base URL, which looks like https://adb-&lt;workspace-id&gt;.&lt;random-number&gt;.azuredatabricks.net
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The access token. To generate a token, see: https://github.com/Azure/azure-databricks-client?tab=readme-ov-file#requirements
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Token { get; set; } = string.Empty;
}

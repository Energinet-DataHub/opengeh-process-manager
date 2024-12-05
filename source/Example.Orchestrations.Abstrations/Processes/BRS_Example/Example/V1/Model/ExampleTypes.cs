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

namespace Energinet.DataHub.Example.Orchestrations.Abstractions.Processes.BRS_Example.Example.V1.Model;

/// <summary>
/// Defines the wholesale calculation types
/// </summary>
public enum ExampleTypes
{
    /// <summary>
    /// Balance fixing
    /// </summary>
    ExampleType1 = 0,

    /// <summary>
    /// Aggregation.
    /// </summary>
    ExampleType2 = 1,
}

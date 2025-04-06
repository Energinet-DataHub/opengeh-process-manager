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

namespace Energinet.DataHub.ProcessManager.Shared.Api.Json;

/// <summary>
/// Default serialization using 'OkObjectResult' doesn't perform Json Polymorphic correct if we
/// use the type directly; so we use an item container.
/// </summary>
/// <typeparam name="TItem">
/// The JSON polymorphic item type.
/// See https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism.
/// </typeparam>
/// <param name="Item">
/// The JSON polymorphic item instance.
/// </param>
internal record JsonPolymorphicItemContainer<TItem>(TItem? Item)
    where TItem : class;

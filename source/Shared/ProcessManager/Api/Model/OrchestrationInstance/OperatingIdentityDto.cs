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

using System.Text.Json.Serialization;

namespace Energinet.DataHub.ProcessManager.Api.Model.OrchestrationInstance;

/// <summary>
/// An identity performing an Process Manager operation.
///
/// We use https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(IOperatingIdentityDto), typeDiscriminator: "base")]
[JsonDerivedType(typeof(UserIdentityDto), typeDiscriminator: "user")]
[JsonDerivedType(typeof(ActorIdentityDto), typeDiscriminator: "actor")]
public interface IOperatingIdentityDto;
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

using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit.Attributes;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit.TraitDiscoverers;

/// <summary>
/// This class discovers all the xUnit tests that have applied
/// the <see cref="TestOrderAttribute"/> attribute. It then configures
/// xUnit 'traits' based on the parameter given to the attribute.
/// The trait is used by <see cref="TestOrderer"/> to order the tests.
/// </summary>
public class TestOrderDiscoverer : ITraitDiscoverer
{
    public const string TraitName = "Order";

    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        var constructorArgs = traitAttribute
            .GetConstructorArguments()
            .ToList();

#pragma warning disable CS8604 // Possible null reference argument.
        yield return new KeyValuePair<string, string>(TraitName, constructorArgs[0].ToString());
#pragma warning restore CS8604 // Possible null reference argument.
    }
}

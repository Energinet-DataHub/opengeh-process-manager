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

using System.IdentityModel.Tokens.Jwt;
using Azure.Identity;
using Energinet.DataHub.ProcessManager.Client.Authorization;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Client.Tests.Integration.Authorization;

public class AuthorizationHeaderProviderTests
{
    private const string ApplicationIdUriForTests = "https://management.azure.com";

    [Fact]
    public async Task Given_ApplicationIdUriForTests_When_CreateAuthorizationHeaderAsync_Then_ReturnedHeaderContainsBearerTokenWithExpectedAudience()
    {
        // Arrange
        var credential = new DefaultAzureCredential();
        var sut = new AuthorizationHeaderProvider(credential, ApplicationIdUriForTests);

        // Act
        var actual = await sut.CreateAuthorizationHeaderAsync(CancellationToken.None);

        // Assert
        actual.Should().NotBeNull();
        actual.Scheme.Should().Be("Bearer");

        var tokenhandler = new JwtSecurityTokenHandler();
        var token = tokenhandler.ReadJwtToken(actual.Parameter);
        token.Audiences.Should().Contain(ApplicationIdUriForTests);
    }

    [Fact]
    public async Task Given_ReusedSutInstance_When_CreateAuthorizationHeaderAsyncIsCalledMultipleTimes_Then_ReturnedHeaderContainsSameTokenBecauseTokenCacheIsUsed()
    {
        // Arrange
        var credential = new DefaultAzureCredential();
        var sut = new AuthorizationHeaderProvider(credential, ApplicationIdUriForTests);

        // Act
        var header01 = await sut.CreateAuthorizationHeaderAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(1));
        var header02 = await sut.CreateAuthorizationHeaderAsync(CancellationToken.None);

        // Assert
        header01.Parameter.Should().Be(header02.Parameter);
    }
}

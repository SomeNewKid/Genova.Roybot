// This file is part of the Genova project licensed under the GNU General Public License v3.0.
// See the LICENSE file in the project root for more information.

using FluentAssertions;

namespace Genova.Roybot.UnitTests;

public class RequiredTest
{
    [Fact]
    public void Required()
    {
        "At least one unit test is required by the GitHub Actions".Should().NotBeNullOrEmpty();
    }
}

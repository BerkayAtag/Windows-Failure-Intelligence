using WFI.Infrastructure;
using Xunit;
namespace WFI.Tests;
public class LdapGroupPolicyTests
{
    [Theory][InlineData("-2147483646")][InlineData("-2147483644")][InlineData("-2147483640")][InlineData("2147483650")]
    public void SecurityGroupGrantsOnlyItsConfiguredRole(string groupType)
    {
        Assert.Equal("Viewer",LdapAuthenticator.SecurityGroupRole(groupType,"Viewer"));
        Assert.Equal("Admin",LdapAuthenticator.SecurityGroupRole(groupType,"Admin"));
    }
    [Theory][InlineData("2")][InlineData("4")][InlineData("8")][InlineData(null)][InlineData("")][InlineData("malformed")][InlineData("4294967296")][InlineData("-4294967296")]
    public void DistributionMissingOrInvalidTypeCannotGrantAdmin(string? groupType)
        =>Assert.Null(LdapAuthenticator.SecurityGroupRole(groupType,"Admin"));
}

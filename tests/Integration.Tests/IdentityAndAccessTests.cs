using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace Integration.Tests;

[Collection(PlatformCollection.Name)]
public sealed partial class IdentityAndAccessTests(PlatformFixture fixture)
{
    [Fact]
    public async Task InvitedMember_AcceptsInvitation_AndGetsMemberPermissionsOnly()
    {
        var owner = (await fixture.RegisterTenantAsync()).Client;
        var memberEmail = $"member-{Guid.NewGuid():N}@example.com";

        (await owner.PostAsJsonAsync("/api/identity/users/invitations", new { Email = memberEmail, DisplayName = "Mia Member", Role = "Member" }))
            .EnsureSuccessStatusCode();

        var pending = await owner.GetAsync<List<UserView>>("/api/identity/users?status=Invited");
        Assert.Equal(memberEmail, Assert.Single(pending).Email);
        Assert.Equal("Owner", Assert.Single(await owner.GetAsync<List<UserView>>("/api/identity/users?role=Owner")).Role);
        Assert.Empty(await owner.GetAsync<List<UserView>>("/api/identity/users?role=Admin"));

        // The raw token only exists in the invitation email sent by Notifications.
        var notifications = await Api.EventuallyAsync(
            () => owner.GetAsync<Paged<NotificationView>>("/api/notifications"),
            p => p.Items.Any(n => n.Recipient == memberEmail),
            "the invitation email was sent");
        var email = notifications.Items.Single(n => n.Recipient == memberEmail);
        var token = Uri.UnescapeDataString(InvitationToken().Match(email.Body).Groups[1].Value);

        var session = await (await fixture.CreateClient().PostAsJsonAsync("/api/identity/invitations/accept", new { Token = token, Password = "a long enough password" }))
            .ReadAsync<AuthToken>();

        var member = fixture.WithToken(session.AccessToken);
        var me = await member.GetAsync<MeView>("/api/identity/me");
        Assert.Equal("Member", me.Role);
        Assert.Contains("catalog:product:read", me.Permissions);
        Assert.DoesNotContain("catalog:product:manage", me.Permissions);

        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/catalog/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await member.PostAsJsonAsync("/api/catalog/products", new { Sku = "X", Name = "X", Price = 1m })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/reporting/sales")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/reporting/sales/top-orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/reporting/sales/monthly")).StatusCode);

        // The invitation is single-use; the member can now sign in with their password.
        Assert.Equal(HttpStatusCode.NotFound,
            (await fixture.CreateClient().PostAsJsonAsync("/api/identity/invitations/accept", new { Token = token, Password = "another password" })).StatusCode);
        (await fixture.CreateClient().PostAsJsonAsync("/api/identity/auth/login", new { Email = memberEmail, Password = "a long enough password" }))
            .EnsureSuccessStatusCode();

        Assert.Empty(await owner.GetAsync<List<UserView>>("/api/identity/users?status=Invited"));

        // Only admins can disable users; once disabled, the member can no longer sign in.
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsync($"/api/identity/users/{me.UserId}/disable", null)).StatusCode);
        var disabled = await (await owner.PostAsync($"/api/identity/users/{me.UserId}/disable", null)).ReadAsync<UserView>();
        Assert.Equal("Disabled", disabled.Status);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.CreateClient().PostAsJsonAsync("/api/identity/auth/login", new { Email = memberEmail, Password = "a long enough password" })).StatusCode);

        // Re-enabling restores their access with the same password.
        var enabled = await (await owner.PostAsync($"/api/identity/users/{me.UserId}/enable", null)).ReadAsync<UserView>();
        Assert.Equal("Active", enabled.Status);
        (await fixture.CreateClient().PostAsJsonAsync("/api/identity/auth/login", new { Email = memberEmail, Password = "a long enough password" }))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DisablingAPendingInvitation_FreesItsSeat_ButYouCannotDisableYourself()
    {
        var owner = (await fixture.RegisterTenantAsync()).Client;

        async Task<HttpResponseMessage> Invite() => await owner.PostAsJsonAsync("/api/identity/users/invitations",
            new { Email = $"user-{Guid.NewGuid():N}@example.com", DisplayName = "Someone", Role = "Member" });

        // Free plan: 3 users including the owner.
        var first = await (await Invite()).ReadAsync<InvitationView>(HttpStatusCode.Accepted);
        Assert.Equal(HttpStatusCode.Accepted, (await Invite()).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Invite()).StatusCode);

        (await owner.PostAsync($"/api/identity/users/{first.UserId}/disable", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Accepted, (await Invite()).StatusCode);

        // A revoked invitation has no account behind it to re-enable.
        var reenable = await owner.PostAsync($"/api/identity/users/{first.UserId}/enable", null);
        Assert.Equal(HttpStatusCode.Conflict, reenable.StatusCode);
        Assert.Equal("Identity.InvitationNeverAccepted", await reenable.ErrorCodeAsync());

        var me = await owner.GetAsync<MeView>("/api/identity/me");
        var self = await owner.PostAsync($"/api/identity/users/{me.UserId}/disable", null);
        Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);
        Assert.Equal("Identity.CannotDisableSelf", await self.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsync($"/api/identity/users/{Guid.NewGuid()}/disable", null)).StatusCode);
    }

    [Fact]
    public async Task FreePlan_LimitsUsers_UntilTheOwnerUpgrades()
    {
        var owner = (await fixture.RegisterTenantAsync()).Client;

        async Task<HttpResponseMessage> Invite() => await owner.PostAsJsonAsync("/api/identity/users/invitations",
            new { Email = $"user-{Guid.NewGuid():N}@example.com", DisplayName = "Someone", Role = "Admin" });

        // Free plan: 3 users including the owner.
        Assert.Equal(HttpStatusCode.Accepted, (await Invite()).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await Invite()).StatusCode);

        var overLimit = await Invite();
        Assert.Equal(HttpStatusCode.Forbidden, overLimit.StatusCode);
        Assert.Equal("Identity.PlanLimitReached", await overLimit.ErrorCodeAsync());

        (await owner.PutAsJsonAsync("/api/identity/tenant/plan", new { Plan = "Pro" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Accepted, (await Invite()).StatusCode);

        var me = await owner.GetAsync<MeView>("/api/identity/me");
        Assert.Equal("Pro", me.Tenant.Plan);
        Assert.Equal(4, me.Tenant.UserCount);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401_WithoutRevealingWhetherTheAccountExists()
    {
        var tenant = await fixture.RegisterTenantAsync();
        var client = fixture.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/identity/auth/login", new { Email = tenant.OwnerEmail, Password = "wrong password!" });
        var unknownUser = await client.PostAsJsonAsync("/api/identity/auth/login", new { Email = "nobody@example.com", Password = "wrong password!" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        Assert.Equal(await wrongPassword.ErrorCodeAsync(), await unknownUser.ErrorCodeAsync());
    }

    [Fact]
    public async Task Platform_RejectsAnonymousAndInvalidRequests_WithProblemDetails()
    {
        var anonymous = fixture.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/sales/orders")).StatusCode);

        var invalid = await anonymous.PostAsJsonAsync("/api/identity/tenants/register",
            new { TenantName = "", OwnerEmail = "not-an-email", OwnerDisplayName = "x", Password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var body = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("OwnerEmail", body);
        Assert.Contains("Password", body);

        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health/ready")).StatusCode);
    }

    [GeneratedRegex(@"token=([A-Za-z0-9_\-%]+)")]
    private static partial Regex InvitationToken();
}

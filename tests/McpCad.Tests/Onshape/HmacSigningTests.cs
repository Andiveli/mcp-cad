using McpCad.Onshape;

namespace McpCad.Tests.Onshape;

public class HmacSigningTests
{
    private const string Secret = "this_is_a_test_secret_key_32bytes!!";

    [Fact]
    public void BuildCanonical_GetDocuments_MatchesSpecLayout()
    {
        var canonical = OnshapeClient.BuildCanonicalForTests(
            "GET",
            "/api/v6/documents",
            "0123456789abcdef0123456789abcdef",
            "Thu, 01 Jan 2026 00:00:00 GMT",
            "",
            "");

        canonical.Should().Be(
            "GET\n/api/v6/documents\n0123456789abcdef0123456789abcdef\nThu, 01 Jan 2026 00:00:00 GMT\n\n");
    }

    [Fact]
    public void BuildCanonical_PostWithBody_IncludesBodyInCanonical()
    {
        var body = new string('x', 240);
        var canonical = OnshapeClient.BuildCanonicalForTests(
            "POST",
            "/api/v6/partstudios/d/did/w/wid/e/eid/features",
            "nonce-post-1",
            "Thu, 01 Jan 2026 12:00:00 GMT",
            "application/json",
            body);

        canonical.Should().StartWith("POST\n/api/v6/partstudios/d/did/w/wid/e/eid/features\n");
        canonical.Should().EndWith(body);
        canonical.Split('\n').Should().HaveCount(6);
        canonical.Should().Contain("application/json");
    }

    [Fact]
    public void Sign_KnownCanonical_IsDeterministicBase64HmacSha256()
    {
        var canonical = OnshapeClient.BuildCanonicalForTests(
            "GET",
            "/api/v6/documents",
            "0123456789abcdef0123456789abcdef",
            "Thu, 01 Jan 2026 00:00:00 GMT",
            "",
            "");

        var sig1 = OnshapeClient.SignForTests(canonical, Secret);
        var sig2 = OnshapeClient.SignForTests(canonical, Secret);

        sig1.Should().Be(sig2);
        sig1.Should().NotBeNullOrWhiteSpace();
        Convert.FromBase64String(sig1).Should().HaveCount(32);
    }

    [Fact]
    public void Sign_EmptyBodyCanonical_DiffersFromMissingBodyLine()
    {
        var withEmpty = OnshapeClient.BuildCanonicalForTests("POST", "/api/v6/oauth/keys", "n", "d", "application/json", "");
        var sigEmpty = OnshapeClient.SignForTests(withEmpty, Secret);

        var withBody = OnshapeClient.BuildCanonicalForTests("POST", "/api/v6/oauth/keys", "n", "d", "application/json", "{}");
        var sigBody = OnshapeClient.SignForTests(withBody, Secret);

        sigEmpty.Should().NotBe(sigBody);
    }
}
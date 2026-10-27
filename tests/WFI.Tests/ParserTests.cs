using System.IO.Compression;
using System.Text;
using System.Xml;
using WFI.Core;
using WFI.Infrastructure;
using Xunit;

namespace WFI.Tests;
public class ParserTests
{
    private static AnalysisResult Analyze(string text, string file = "setupact.log") => new LogAnalyzer().Analyze(new MemoryStream(Encoding.UTF8.GetBytes(text)), file);
    [Fact] public void RollbackIsHypothesisWithEvidence() { var r = Analyze("started\nError 0xc1900101 phase FIRST_BOOT password=hunter2\n"); var f = Assert.Single(r.Findings); Assert.Equal("UPG-001", f.RuleId); Assert.Equal(2, f.Evidence[0].Line); Assert.Contains("tek başına", f.Explanation); Assert.DoesNotContain("hunter2", f.Evidence[0].Text); }
    [Fact] public void SuccessDoesNotProduceInventedFinding() { var r = Analyze("Setup completed with 0x00000000"); Assert.Empty(r.Findings); Assert.Contains("kanıtlamaz", r.Conclusion); }
    [Fact] public void CodePrefixDoesNotMatch() => Assert.Empty(Analyze("0xC1900101AB").Findings);
    [Fact] public void XmlDtdIsRejected() => Assert.Throws<XmlException>(() => Analyze("<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///secret'>]><x>&e;</x>", "x.xml"));
    [Fact] public void BrokenXmlIsRejected() => Assert.Throws<XmlException>(() => Analyze("<x>", "x.xml"));
    [Fact] public void NetlogonEventIsProviderQualified() { Assert.Single(Analyze("<Events><Event><System><Provider Name='NETLOGON'/><EventID>5719</EventID></System></Event></Events>", "system.xml").Findings); Assert.Empty(Analyze("<Event><System><Provider Name='Other'/><EventID>5719</EventID></System></Event>", "system.xml").Findings); }
    [Fact] public void BinaryIsRejected() => Assert.Throws<InputRejectedException>(() => Analyze("hello\0world"));
    [Fact] public void EmptyIsRejected() => Assert.Throws<InputRejectedException>(() => Analyze(""));
    [Fact] public void OversizedLineIsRejected() => Assert.Throws<InputRejectedException>(() => Analyze(new string('a', 32769)));
    [Fact] public void EvtxFailsExplicitly() => Assert.Throws<InputRejectedException>(() => Analyze("binary", "system.evtx"));
    [Theory][InlineData("../escape.log")][InlineData("C:/escape.log")][InlineData("/escape.log")][InlineData("nested.zip")]
    public void UnsafeArchiveIsRejected(string path) { using var zip = Zip(path, "hello"); Assert.Throws<InputRejectedException>(() => new LogAnalyzer().Analyze(zip, "logs.zip")); }
    [Fact] public void ArchiveCombinesEvidence() { using var zip = Zip("Panther/setuperr.log", "0xC1900101"); var r = new LogAnalyzer().Analyze(zip, "logs.zip"); Assert.Equal("Panther/setuperr.log", Assert.Single(r.Findings).Evidence[0].Source); }
    [Fact] public void ZipBombIsRejected() { using var zip = Zip("bomb.log", new string('a', 2 * 1024 * 1024)); Assert.Throws<InputRejectedException>(() => new LogAnalyzer().Analyze(zip, "logs.zip")); }
    [Fact] public void LdapFilterMetacharactersAreEscaped() => Assert.Equal(@"a\2a\28\29\5c\00", LdapAuthenticator.EscapeFilter("a*()\\\0"));
    [Theory][InlineData("user@evil.test")][InlineData("EXAMPLE\\user")][InlineData("a)(uid=*)")][InlineData("a@@example.invalid")][InlineData("")]
    public void ForeignOrAmbiguousIdentityRejected(string username) => Assert.Null(LdapAuthenticator.NormalizeUser(username, "example.invalid"));
    [Fact] public void DomainIsConfigurable() => Assert.Equal("alice@other.example", LdapAuthenticator.NormalizeUser("alice", "other.example"));
    [Fact] public void NoGroupsFailsClosed() => Assert.Throws<InvalidOperationException>(() => new LdapOptions { Server = "dc.example" }.Validate());
    [Fact] public void NestedEventsAreRejected() => Assert.Throws<InputRejectedException>(() => Analyze("<Event><Event/></Event>", "x.xml"));
    [Fact] public void ExcessiveXmlDepthIsRejected() => Assert.Throws<InputRejectedException>(() => Analyze(string.Concat(Enumerable.Repeat("<x>", 67)) + string.Concat(Enumerable.Repeat("</x>", 67)), "x.xml"));
    [Fact] public void CancelledInputStopsBeforeParsing() { using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<Event/>")); Assert.Throws<OperationCanceledException>(() => new LogAnalyzer().Analyze(stream, "x.xml", new CancellationToken(true))); }
    [Theory]
    [InlineData("Authorization: Bearer SYNTHETIC_SECRET")]
    [InlineData("Authorization: Basic SYNTHETIC_SECRET")]
    [InlineData("\"token\": \"SYNTHETIC_SECRET with spaces\"")]
    [InlineData("password='SYNTHETIC_SECRET with spaces'")]
    [InlineData("<secret>SYNTHETIC_SECRET</secret>")]
    public void StructuredCredentialsAreMasked(string input) { var result = Analyze("0xC1900101 " + input); Assert.DoesNotContain("SYNTHETIC_SECRET", Assert.Single(result.Findings).Evidence[0].Text); }
    [Fact] public void MaskingPrecedesEvidenceTruncation() { var text = "0xC1900101 password=\"" + new string('s', 1600) + "\""; Assert.DoesNotContain("ssss", Assert.Single(Analyze(text).Findings).Evidence[0].Text); }
    private static MemoryStream Zip(string name, string content) { var stream = new MemoryStream(); using (var z = new ZipArchive(stream, ZipArchiveMode.Create, true)) { using var w = new StreamWriter(z.CreateEntry(name).Open()); w.Write(content); } stream.Position = 0; return stream; }
}

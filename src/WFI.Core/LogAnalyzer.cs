using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace WFI.Core;

public sealed class LogAnalyzer
{
    public const long MaxUpload = 20 * 1024 * 1024;
    public const long MaxExpanded = 64 * 1024 * 1024;
    public static readonly string[] Extensions = [".log", ".txt", ".xml", ".zip"];
    // Intentionally mask the remainder of an authorization header, including its scheme.
    private static readonly Regex Authorization = new("(?i)\\b(?:proxy-)?authorization[\\\"']?\\s*[:=]\\s*[^\\r\\n<]+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Secret = new("(?i)\\b(password|pwd|token|access_token|refresh_token|secret)[\\\"']?\\s*[:=]\\s*(?:\\\"[^\\\"]*\\\"|'[^']*'|[^\\s<;]+)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex XmlSecret = new(@"(?is)<(password|pwd|token|authorization|secret)\b[^>]*>.*?</\1\s*>", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static string Redact(string value) => XmlSecret.Replace(Secret.Replace(Authorization.Replace(value, "Authorization=[MASKED]"), "$1=[MASKED]"), "<$1>[MASKED]</$1>");

    public AnalysisResult Analyze(Stream input, string filename, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        if (!Extensions.Contains(extension)) throw new InputRejectedException("Desteklenmeyen dosya. ZIP, XML, LOG veya TXT kullanın. EVTX için önce XML dışa aktarın.");
        var collected = new Dictionary<string, List<Evidence>>();
        int files = 0, lines = 0;
        void Process(Stream stream, string name, long length)
        {
            ct.ThrowIfCancellationRequested();
            if (length > MaxUpload) throw new InputRejectedException("Arşiv içindeki dosya 20 MB sınırını aşıyor.");
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true, 4096, leaveOpen: true);
            var content = reader.ReadToEnd();
            if (content.IndexOf('\0') >= 0) throw new InputRejectedException("İkili veya desteklenmeyen metin kodlaması.");
            if (Path.GetExtension(name).Equals(".xml", StringComparison.OrdinalIgnoreCase))
            {
                using var xml = XmlReader.Create(new StringReader(content), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxUpload });
                int eventDepth = -1, eventLine = 0, idDepth = -1;
                string? provider = null; var id = new StringBuilder();
                while (xml.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    if (xml.Depth > 64) throw new InputRejectedException("XML derinliği 64 sınırını aşıyor.");
                    if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Event")
                    {
                        if (eventDepth >= 0) throw new InputRejectedException("İç içe XML Event kabul edilmez.");
                        provider = null; id.Clear(); idDepth = -1;
                        eventLine = ((IXmlLineInfo)xml).LineNumber;
                        eventDepth = xml.IsEmptyElement ? -1 : xml.Depth;
                    }
                    else if (eventDepth >= 0 && xml.NodeType == XmlNodeType.Element && xml.Depth == eventDepth + 2)
                    {
                        if (xml.LocalName == "Provider") provider = xml.GetAttribute("Name");
                        if (xml.LocalName == "EventID" && !xml.IsEmptyElement) idDepth = xml.Depth;
                    }
                    else if (idDepth >= 0 && xml.NodeType == XmlNodeType.Text && xml.Depth == idDepth + 1)
                    {
                        if (id.Length + xml.Value.Length > 32) throw new InputRejectedException("Geçersiz XML EventID.");
                        id.Append(xml.Value);
                    }
                    if (xml.NodeType == XmlNodeType.EndElement && xml.Depth == idDepth) idDepth = -1;
                    if (xml.NodeType == XmlNodeType.EndElement && xml.Depth == eventDepth)
                    {
                        if (string.Equals(provider, "NETLOGON", StringComparison.OrdinalIgnoreCase) && id.ToString().Trim() == "5719")
                            Add("AD-001", name, eventLine, "NETLOGON EventID=5719: Domain controller bağlantısı kurulamadı.");
                        eventDepth = -1;
                    }
                }
            }
            using var lineReader = new StringReader(content);
            int localLine = 0;
            while (lineReader.ReadLine() is { } line)
            {
                ct.ThrowIfCancellationRequested();
                localLine++; lines++;
                if (line.Length > 32768) throw new InputRejectedException("Log satırı 32.768 karakter sınırını aşıyor.");
                foreach (var rule in Rules.All)
                    if (rule.Matcher.IsMatch(line)) Add(rule.Id, name, localLine, line);
            }
            files++;
        }
        void Add(string rule, string source, int line, string value)
        {
            if (!collected.TryGetValue(rule, out var evidence)) collected[rule] = evidence = [];
            if (evidence.Count < 30) { var safe = Redact(value); evidence.Add(new(Redact(source), line, safe.Length > 1500 ? safe[..1500] : safe)); }
        }
        if (extension == ".zip")
        {
            using var zip = new ZipArchive(input, ZipArchiveMode.Read, true);
            if (zip.Entries.Count > 100) throw new InputRejectedException("ZIP en fazla 100 girdi içerebilir.");
            long total = 0;
            foreach (var entry in zip.Entries)
            {
                var path = entry.FullName.Replace('\\', '/');
                if (path.StartsWith('/') || path.Contains(':') || path.Split('/').Contains("..")) throw new InputRejectedException("Güvenli olmayan ZIP yolu.");
                if (entry.Name.Length == 0) continue;
                if (!Extensions.Take(3).Contains(Path.GetExtension(path).ToLowerInvariant())) throw new InputRejectedException("ZIP yalnızca XML, LOG ve TXT içerebilir; iç içe ZIP kabul edilmez.");
                if (entry.Length > MaxExpanded - total || entry.Length > MaxUpload || (entry.Length > 1024 * 1024 && entry.Length / Math.Max(entry.CompressedLength, 1) > 200)) throw new InputRejectedException("ZIP açılım kotası aşıldı.");
                using var data = entry.Open();
                // Enforce actual bytes as well as the central-directory metadata.
                using var bounded = new MemoryStream();
                CopyBounded(data, bounded, Math.Min(MaxUpload, MaxExpanded - total), ct);
                total += bounded.Length;
                bounded.Position = 0;
                Process(bounded, path, bounded.Length);
            }
        }
        else
        {
            using var bounded = new MemoryStream();
            CopyBounded(input, bounded, MaxUpload, ct); bounded.Position = 0;
            Process(bounded, Path.GetFileName(filename), bounded.Length);
        }
        if (files == 0 || lines == 0) throw new InputRejectedException("Dosya boş veya analiz edilebilir log içermiyor.");
        var findings = Rules.All.Where(r => collected.ContainsKey(r.Id)).Select(r => new Finding(r.Id, Rules.Version, r.Category, "Warning", r.Title, r.Explanation, r.Checks, collected[r.Id].ToArray())).ToArray();
        return new(Rules.Version, findings.Length == 0 ? "Bilinen kurallarla eşleşme yok. Bu sonuç sistemin sağlıklı olduğunu kanıtlamaz." : "Olası nedenler bulundu. Kanıtlar ve önerilen doğrulama adımları birlikte değerlendirilmelidir.", findings, files, lines);
    }
    private static void CopyBounded(Stream source, Stream destination, long limit, CancellationToken ct)
    {
        var buffer = new byte[81920]; long total = 0; int n;
        while ((n = source.Read(buffer)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            total += n;
            if (total > limit) throw new InputRejectedException("Dosya boyutu kotası aşıldı.");
            destination.Write(buffer, 0, n);
        }
    }
}

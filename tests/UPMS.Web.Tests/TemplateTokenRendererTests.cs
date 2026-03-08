namespace UPMS.Web.Tests;

using System.IO.Compression;
using System.Text;
using UPMS.Data;
using UPMS.Web.Plugins.PowerPoint;
using UPMS.Web.Templates;

[TestFixture]
public class TemplateTokenRendererTests
{
    [Test]
    public void RenderText_AggregateNamespacedTokens_RenderSuccessfully()
    {
        var tickets = new List<Ticket>
        {
            CreateTicket("servicenow", "Acme", "INC1001", ("Number", "INC1001"), ("State", "Open"), ("Priority", "High"), ("Short Description", "Database outage")),
            CreateTicket("servicenow", "Acme", "INC1002", ("Number", "INC1002"), ("State", "Resolved"), ("Priority", "Medium"), ("Short Description", "Latency spike"))
        };

        var context = TemplateReportTokenBuilder.BuildContext("servicenow", "Acme", new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), tickets);
        var template = "Company={{meta.company}}|Count={{kpi.ticket_count}}|Graph={{graph.lifecycle.month_end.svg}}|Table={{table.kpis.html}}";

        var rendered = TemplateTokenRenderer.RenderText(template, context);

        Assert.That(rendered, Does.Contain("Company=Acme"));
        Assert.That(rendered, Does.Contain("Count=2"));
        Assert.That(rendered, Does.Contain("<svg"));
        Assert.That(rendered, Does.Contain("<table"));
    }

    [Test]
    public void RenderText_PerTicketSectionLoop_RendersFilteredTickets()
    {
        var tickets = new List<Ticket>
        {
            CreateTicket("servicenow", "Acme", "INC1001", ("Number", "INC1001"), ("Priority", "High"), ("Description", "Database cluster offline"), ("Short Description", "Database outage")),
            CreateTicket("servicenow", "Acme", "INC1002", ("Number", "INC1002"), ("Priority", "Low"), ("Description", "Minor UI issue"), ("Short Description", "UI issue"))
        };

        var context = TemplateReportTokenBuilder.BuildContext("servicenow", "Acme", new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), tickets);
        var template = "{{start per ticket Priority=High && Description~cluster}}{{ticket.Number}}|{{ticket.Short_Description}}|{{ticket.Description}}{{end per ticket}}";

        var rendered = TemplateTokenRenderer.RenderText(template, context);

        Assert.That(rendered, Is.EqualTo("INC1001|Database outage|Database cluster offline"));
    }

    [Test]
    public void RenderBytes_HtmlPageScopedLoop_InsertsPageBreaksBetweenTickets()
    {
        var tickets = new List<Ticket>
        {
            CreateTicket("servicenow", "Acme", "INC1001", ("Number", "INC1001"), ("State", "Open"), ("Description", "First description")),
            CreateTicket("servicenow", "Acme", "INC1002", ("Number", "INC1002"), ("State", "Open"), ("Description", "Second description"))
        };

        var context = TemplateReportTokenBuilder.BuildContext("servicenow", "Acme", new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), tickets);
        var template = "<html><body>{{start per ticket State=Open scope=page}}<section><h1>{{ticket.Number}}</h1><p>{{ticket.Description}}</p></section>{{end per ticket}}</body></html>";

        var renderedBytes = TemplateTokenRenderer.RenderBytes(Encoding.UTF8.GetBytes(template), ".html", context);
        var rendered = Encoding.UTF8.GetString(renderedBytes);

        Assert.That(rendered, Does.Contain("INC1001"));
        Assert.That(rendered, Does.Contain("INC1002"));
        Assert.That(rendered, Does.Contain("page-break-before:always"));
    }

    [Test]
    public void RenderBytes_PowerPointPageScopedLoop_DuplicatesSlidesPerTicket()
    {
        var tickets = new List<Ticket>
        {
            CreateTicket("servicenow", "Acme", "INC1001", ("Number", "INC1001"), ("Short Description", "Database outage")),
            CreateTicket("servicenow", "Acme", "INC1002", ("Number", "INC1002"), ("Short Description", "Network degradation"))
        };

        var context = TemplateReportTokenBuilder.BuildContext("servicenow", "Acme", new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), tickets);
        var templateBytes = CreatePowerPointTemplateWithPerTicketSlideLoop();

        var renderedBytes = TemplateTokenRenderer.RenderBytes(templateBytes, ".pptx", context);

        using MemoryStream stream = new(renderedBytes);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: false);

        var slidePaths = archive.Entries
            .Where(entry => entry.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase)
                         && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.FullName)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.That(slidePaths, Has.Count.EqualTo(3)); // cover slide + 2 ticket slides

        string slide2 = ReadText(archive.GetEntry("ppt/slides/slide2.xml")!);
        string slide3 = ReadText(archive.GetEntry("ppt/slides/slide3.xml")!);

        Assert.That(slide2, Does.Contain("INC1001"));
        Assert.That(slide2, Does.Contain("Database outage"));
        Assert.That(slide2, Does.Contain("Acme"));
        Assert.That(slide3, Does.Contain("INC1002"));
        Assert.That(slide3, Does.Contain("Network degradation"));
        Assert.That(slide3, Does.Contain("Acme"));
        Assert.That(slide2, Does.Not.Contain("start per ticket"));
        Assert.That(slide3, Does.Not.Contain("start per ticket"));
        Assert.That(slide2, Does.Not.Contain("ticket.Number"));
        Assert.That(slide3, Does.Not.Contain("ticket.Number"));
        Assert.That(slide2, Does.Not.Contain("meta.company"));
        Assert.That(slide3, Does.Not.Contain("meta.company"));
    }

    [Test]
    public void RenderBytes_DocxSplitRunTokensAndPageScopedLoop_RendersFilledPages()
    {
        var tickets = new List<Ticket>
        {
            CreateTicket("servicenow", "Acme", "INC1001", ("Number", "INC1001"), ("State", "Open"), ("Short Description", "Database outage")),
            CreateTicket("servicenow", "Acme", "INC1002", ("Number", "INC1002"), ("State", "Open"), ("Short Description", "Network degradation"))
        };

        var context = TemplateReportTokenBuilder.BuildContext("servicenow", "Acme", new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), tickets);
        var templateBytes = CreateDocxTemplateWithSplitRunLoop();

        var renderedBytes = TemplateTokenRenderer.RenderBytes(templateBytes, ".docx", context);

        using MemoryStream stream = new(renderedBytes);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: false);
        string documentXml = ReadText(archive.GetEntry("word/document.xml")!);

        Assert.That(documentXml, Does.Contain("Acme"));
        Assert.That(documentXml, Does.Contain("INC1001"));
        Assert.That(documentXml, Does.Contain("INC1002"));
        Assert.That(documentXml, Does.Contain("Database outage"));
        Assert.That(documentXml, Does.Contain("Network degradation"));
        Assert.That(documentXml, Does.Contain("w:br"));
        Assert.That(documentXml, Does.Not.Contain("start per ticket"));
        Assert.That(documentXml, Does.Not.Contain("ticket.Number"));
        Assert.That(documentXml, Does.Not.Contain("meta.company"));
    }

    private static Ticket CreateTicket(string itsmSource, string company, string number, params (string FieldName, string? Value)[] fields)
    {
        Dictionary<string, string?> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            values[field.FieldName] = field.Value;
        }

        if (!values.ContainsKey("Number"))
            values["Number"] = number;

        return new Ticket
        {
            TicketKey = TicketKeyFactory.Compose(itsmSource, company, number),
            CompanyName = company,
            ItsmSource = itsmSource,
            Fields = values,
            ObservedAt = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            SnapshotId = Guid.NewGuid(),
            SnapshotDate = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static byte[] CreatePowerPointTemplateWithPerTicketSlideLoop()
    {
        var baseBytes = PowerPointGenerator.Generate(
            "Acme",
            "servicenow",
            new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            "Default",
            []);

        using MemoryStream input = new(baseBytes);
        using ZipArchive source = new(input, ZipArchiveMode.Read, leaveOpen: false);
        using MemoryStream output = new();
        using (ZipArchive target = new(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                var targetEntry = target.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var sourceStream = entry.Open();
                using var targetStream = targetEntry.Open();

                if (string.Equals(entry.FullName, "ppt/slides/slide2.xml", StringComparison.OrdinalIgnoreCase))
                {
                    var templatedSlide = """
                        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                        <p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"
                               xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                               xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                          <p:cSld><p:spTree>
                            <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
                            <p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr>
                            <p:sp><p:nvSpPr><p:cNvPr id="2" name="LoopStart"/><p:cNvSpPr><a:spLocks noGrp="1"/></p:cNvSpPr><p:nvPr/></p:nvSpPr>
                              <p:spPr><a:xfrm><a:off x="457200" y="274638"/><a:ext cx="8229600" cy="457200"/></a:xfrm></p:spPr>
                              <p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:t>{{sta</a:t></a:r><a:r><a:t>rt per ticket scope=page}}</a:t></a:r></a:p></p:txBody></p:sp>
                            <p:sp><p:nvSpPr><p:cNvPr id="3" name="TicketTitle"/><p:cNvSpPr><a:spLocks noGrp="1"/></p:cNvSpPr><p:nvPr/></p:nvSpPr>
                              <p:spPr><a:xfrm><a:off x="457200" y="1066800"/><a:ext cx="8229600" cy="762000"/></a:xfrm></p:spPr>
                              <p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:t>Ticket </a:t></a:r><a:r><a:t>{{ticket.</a:t></a:r><a:r><a:t>Number}}</a:t></a:r></a:p></p:txBody></p:sp>
                            <p:sp><p:nvSpPr><p:cNvPr id="4" name="Company"/><p:cNvSpPr><a:spLocks noGrp="1"/></p:cNvSpPr><p:nvPr/></p:nvSpPr>
                              <p:spPr><a:xfrm><a:off x="457200" y="1828800"/><a:ext cx="8229600" cy="762000"/></a:xfrm></p:spPr>
                              <p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:t>Company </a:t></a:r><a:r><a:t>{{meta.co</a:t></a:r><a:r><a:t>mpany}}</a:t></a:r></a:p></p:txBody></p:sp>
                            <p:sp><p:nvSpPr><p:cNvPr id="5" name="Body"/><p:cNvSpPr><a:spLocks noGrp="1"/></p:cNvSpPr><p:nvPr/></p:nvSpPr>
                              <p:spPr><a:xfrm><a:off x="457200" y="2590800"/><a:ext cx="8229600" cy="1371600"/></a:xfrm></p:spPr>
                              <p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:t>{{ticket.Short_</a:t></a:r><a:r><a:t>Description}}</a:t></a:r></a:p></p:txBody></p:sp>
                            <p:sp><p:nvSpPr><p:cNvPr id="6" name="LoopEnd"/><p:cNvSpPr><a:spLocks noGrp="1"/></p:cNvSpPr><p:nvPr/></p:nvSpPr>
                              <p:spPr><a:xfrm><a:off x="457200" y="4114800"/><a:ext cx="8229600" cy="457200"/></a:xfrm></p:spPr>
                              <p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:t>{{end per </a:t></a:r><a:r><a:t>ticket}}</a:t></a:r></a:p></p:txBody></p:sp>
                          </p:spTree></p:cSld>
                        </p:sld>
                        """;

                    using StreamWriter writer = new(targetStream, new UTF8Encoding(false), leaveOpen: true);
                    writer.Write(templatedSlide);
                }
                else
                {
                    sourceStream.CopyTo(targetStream);
                }
            }
        }

        return output.ToArray();
    }

    private static byte[] CreateDocxTemplateWithSplitRunLoop()
    {
        const string contentTypes = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
            </Types>
            """;

        const string packageRels = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
            </Relationships>
            """;

        const string documentXml = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:body>
                <w:p>
                  <w:r><w:t>Company: </w:t></w:r>
                  <w:r><w:t>{{meta.</w:t></w:r>
                  <w:r><w:t>company}}</w:t></w:r>
                </w:p>
                <w:p>
                  <w:r><w:t>{{sta</w:t></w:r>
                  <w:r><w:t>rt per ticket State=Open scope=page}}</w:t></w:r>
                </w:p>
                <w:p>
                  <w:r><w:t>Ticket </w:t></w:r>
                  <w:r><w:t>{{ticket.</w:t></w:r>
                  <w:r><w:t>Number}}</w:t></w:r>
                </w:p>
                <w:p>
                  <w:r><w:t>Summary </w:t></w:r>
                  <w:r><w:t>{{ticket.Short_</w:t></w:r>
                  <w:r><w:t>Description}}</w:t></w:r>
                </w:p>
                <w:p>
                  <w:r><w:t>{{end per </w:t></w:r>
                  <w:r><w:t>ticket}}</w:t></w:r>
                </w:p>
                <w:sectPr>
                  <w:pgSz w:w="12240" w:h="15840"/>
                  <w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="720" w:footer="720" w:gutter="0"/>
                </w:sectPr>
              </w:body>
            </w:document>
            """;

        using MemoryStream output = new();
        using (ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteZipText(archive, "[Content_Types].xml", contentTypes);
            WriteZipText(archive, "_rels/.rels", packageRels);
            WriteZipText(archive, "word/document.xml", documentXml);
        }

        return output.ToArray();
    }

    private static void WriteZipText(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using StreamWriter writer = new(stream, new UTF8Encoding(false), leaveOpen: false);
        writer.Write(content);
    }

    private static string ReadText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        return reader.ReadToEnd();
    }
}

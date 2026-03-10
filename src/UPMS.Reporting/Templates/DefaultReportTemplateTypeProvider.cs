namespace UPMS.Reporting.Templates;

public sealed class DefaultReportTemplateTypeProvider : IReportTemplateTypeProvider
{
    private static readonly IReadOnlyList<ReportTemplateTypeDefinition> TemplateTypes =
    [
        new ReportTemplateTypeDefinition
        {
            TypeId = "html-document",
            DisplayName = "HTML document",
            Kind = ReportTemplateKind.Document,
            PrimaryExtension = ".html",
            Extensions = [".html", ".htm"],
            ContentType = "text/html",
            RenderingFamily = TemplateRenderingFamily.Text,
            SupportsInlineEdit = true,
            SortOrder = 10,
            Description = "Browser-friendly operations brief with live KPI cards and repeated ticket sections.",
            AuthoringGuidance = "Use semantic HTML and visible {{token}} text. For one section per ticket, wrap markup with {{start per ticket ...}} and {{end per ticket}}. Use scope=page when you want each repeated block separated by a page break in printable outputs.",
            StarterTemplateDisplayName = "Starter HTML - Operations Brief",
            StarterTemplateDescription = "HTML operations brief with KPI cards and one repeated ticket article per active ticket.",
            StarterTemplateResourceName = "UPMS.Reporting.StarterTemplates.html-document.html"
        },
        new ReportTemplateTypeDefinition
        {
            TypeId = "html-email",
            DisplayName = "HTML email",
            Kind = ReportTemplateKind.Email,
            PrimaryExtension = ".html",
            Extensions = [".html", ".htm"],
            ContentType = "text/html",
            RenderingFamily = TemplateRenderingFamily.Text,
            SupportsInlineEdit = true,
            SortOrder = 20,
            Description = "HTML email that auto-downloads as an .eml draft when generated in Auto mode.",
            AuthoringGuidance = "Keep email layouts simple and table-based where possible. Use visible HTML tokens and avoid external assets so the generated EML remains portable.",
            StarterTemplateDisplayName = "Starter HTML Email - Incident Update",
            StarterTemplateDescription = "HTML email update with summary KPIs and a repeated list of urgent tickets.",
            StarterTemplateResourceName = "UPMS.Reporting.StarterTemplates.html-email.html",
            DefaultSubjectTemplate = "UPMS {{meta.company}} incident update - {{meta.as_of_date}}"
        },
        new ReportTemplateTypeDefinition
        {
            TypeId = "eml-email",
            DisplayName = "EML draft",
            Kind = ReportTemplateKind.Email,
            PrimaryExtension = ".eml",
            Extensions = [".eml"],
            ContentType = "message/rfc822",
            RenderingFamily = TemplateRenderingFamily.Text,
            SupportsInlineEdit = true,
            SortOrder = 30,
            Description = "Raw RFC822 email draft for teams that want to author the full email shell themselves.",
            AuthoringGuidance = "Include standard headers such as Subject, MIME-Version, and Content-Type directly in the template. Tokens can be used in both headers and body content.",
            StarterTemplateDisplayName = "Starter EML - Executive Update",
            StarterTemplateDescription = "Ready-to-edit .eml draft with HTML body content and ticket loop markers.",
            StarterTemplateResourceName = "UPMS.Reporting.StarterTemplates.eml-email.eml",
            DefaultSubjectTemplate = "UPMS {{meta.company}} executive update - {{meta.as_of_date}}"
        },
        new ReportTemplateTypeDefinition
        {
            TypeId = "text-document",
            DisplayName = "Plain text document",
            Kind = ReportTemplateKind.Document,
            PrimaryExtension = ".txt",
            Extensions = [".txt"],
            ContentType = "text/plain",
            RenderingFamily = TemplateRenderingFamily.Text,
            SupportsInlineEdit = true,
            SortOrder = 40,
            Description = "Text-first summary for operational handovers, notes, and export-friendly previews.",
            AuthoringGuidance = "Keep loop markers on their own lines. scope=page inserts a form-feed style separation between repeated ticket sections when that layout is useful downstream.",
            StarterTemplateDisplayName = "Starter TXT - Operations Summary",
            StarterTemplateDescription = "Plain-text incident summary with one repeated page-style section per active ticket.",
            StarterTemplateResourceName = "UPMS.Reporting.StarterTemplates.text-document.txt"
        },
        new ReportTemplateTypeDefinition
        {
            TypeId = "csv-spreadsheet",
            DisplayName = "CSV spreadsheet",
            Kind = ReportTemplateKind.Spreadsheet,
            PrimaryExtension = ".csv",
            Extensions = [".csv"],
            ContentType = "text/csv",
            RenderingFamily = TemplateRenderingFamily.Text,
            SupportsInlineEdit = true,
            SortOrder = 50,
            Description = "Simple row-based export that can still use tokenised headers and per-ticket loops.",
            AuthoringGuidance = "Author CSV templates as plain text. Use one repeated line per ticket inside a per-ticket block so spreadsheet users can open the generated file directly in Excel or LibreOffice.",
            StarterTemplateDisplayName = "Starter CSV - Ticket Register",
            StarterTemplateDescription = "CSV export with summary rows followed by one repeated ticket row per active ticket.",
            StarterTemplateResourceName = "UPMS.Reporting.StarterTemplates.csv-spreadsheet.csv"
        },
        new ReportTemplateTypeDefinition
        {
            TypeId = "xml-generic",
            DisplayName = "XML payload",
            Kind = ReportTemplateKind.Generic,
            PrimaryExtension = ".xml",
            Extensions = [".xml"],
            ContentType = "application/xml",
            RenderingFamily = TemplateRenderingFamily.Text,
            SupportsInlineEdit = true,
            SortOrder = 60,
            Description = "Structured XML payload for downstream integrations, archives, or bespoke importers.",
            AuthoringGuidance = "Keep tokens in element text or attributes. When building repeated ticket nodes, ensure your loop markers surround well-formed XML fragments.",
            StarterTemplateDisplayName = "Starter XML - Ticket Payload",
            StarterTemplateDescription = "XML payload with report metadata and repeated <ticket> nodes for active tickets.",
            StarterTemplateResourceName = "UPMS.Reporting.StarterTemplates.xml-generic.xml"
        },
        new ReportTemplateTypeDefinition
        {
            TypeId = "word-document",
            DisplayName = "Word document",
            Kind = ReportTemplateKind.Document,
            PrimaryExtension = ".docx",
            Extensions = [".docx"],
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            RenderingFamily = TemplateRenderingFamily.Wordprocessing,
            SupportsInlineEdit = false,
            SortOrder = 70,
            Description = "Word document for polished operational summaries, briefings, and stakeholder packs.",
            AuthoringGuidance = "Keep tokens as visible text inside normal paragraphs. For one page per ticket, put {{start per ticket ... scope=page}} and {{end per ticket}} on their own paragraphs so the renderer can duplicate the page cleanly.",
            StarterTemplateDisplayName = "Starter Word - Operations Summary",
            StarterTemplateDescription = "DOCX summary page followed by one repeated page-style ticket section per active ticket.",
            StarterTemplateResourceName = "UPMS.Reporting.StarterTemplates.word-document.docx"
        },
        new ReportTemplateTypeDefinition
        {
            TypeId = "excel-workbook",
            DisplayName = "Excel workbook",
            Kind = ReportTemplateKind.Spreadsheet,
            PrimaryExtension = ".xlsx",
            Extensions = [".xlsx"],
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            RenderingFamily = TemplateRenderingFamily.Spreadsheet,
            SupportsInlineEdit = false,
            SortOrder = 80,
            Description = "Workbook-based reporting for KPI ledgers, exports, and spreadsheet-first consumers.",
            AuthoringGuidance = "Upload a real .xlsx workbook when you need formulas, formatting, or multiple sheets. Tokens can live in visible cells and shared strings. For larger ticket tables, pair the workbook with text-list or CSV-document tokens inside helper sheets.",
            StarterTemplateDisplayName = "Starter Excel - KPI Workbook",
            StarterTemplateDescription = "XLSX workbook with a summary sheet and helper sheet containing export-oriented tokens.",
            StarterTemplateResourceName = "UPMS.Reporting.StarterTemplates.excel-workbook.xlsx"
        },
        new ReportTemplateTypeDefinition
        {
            TypeId = "powerpoint-presentation",
            DisplayName = "PowerPoint presentation",
            Kind = ReportTemplateKind.Presentation,
            PrimaryExtension = ".pptx",
            Extensions = [".pptx"],
            ContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            RenderingFamily = TemplateRenderingFamily.Presentation,
            SupportsInlineEdit = false,
            SortOrder = 90,
            Description = "Slide deck for stakeholder briefings and review packs with optional one-slide-per-ticket loops.",
            AuthoringGuidance = "Keep slide-level loop markers on the slide that should be duplicated and keep them as visible text in a text box. scope=slide (or scope=page) is the most reliable pattern for one slide per ticket.",
            StarterTemplateDisplayName = "Starter PowerPoint - Ticket Deck",
            StarterTemplateDescription = "PPTX deck with a summary cover slide and one repeated slide per active ticket.",
            StarterTemplateResourceName = "UPMS.Reporting.StarterTemplates.powerpoint-presentation.pptx"
        }
    ];

    public IReadOnlyList<ReportTemplateTypeDefinition> GetTemplateTypes() => TemplateTypes;
}

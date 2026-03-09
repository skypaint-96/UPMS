import { FormEvent, useEffect, useState } from 'react';
import { Box, Button, Chip, Grid, Stack, TextField, Typography } from '@mui/material';
import { upmsApi } from '../api/client';
import { ReportTemplate, ReportTemplateKind } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { LoadingPanel } from '../components/LoadingPanel';
import { PageSection } from '../components/PageSection';
import { UpmsDataTable } from '../components/UpmsDataTable';

const templateKinds: ReportTemplateKind[] = ['Email', 'Document', 'Spreadsheet', 'Presentation', 'Generic'];
const tokenExamples = [
  '{{meta.company}}',
  '{{meta.itsm_source}}',
  '{{meta.as_of_date}}',
  '{{kpi.ticket_count}}',
  '{{table.kpis.html}}',
  '{{output.tickets.csv_document}}',
  '{{start per ticket Priority=High}}',
  '{{end per ticket}}',
];

export function ReportTemplatesPage() {
  const [templates, setTemplates] = useState<ReportTemplate[]>([]);
  const [displayName, setDisplayName] = useState('');
  const [kind, setKind] = useState<ReportTemplateKind>('Document');
  const [description, setDescription] = useState('');
  const [subjectTemplate, setSubjectTemplate] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string>();
  const [message, setMessage] = useState<string>();

  const loadTemplates = async () => {
    setLoading(true);
    try {
      const loaded = await upmsApi.getReportTemplates();
      setTemplates(loaded);
    } catch (err: any) {
      setError(err.message ?? 'Failed to load report templates.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadTemplates();
  }, []);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!displayName.trim() || !file) {
      return;
    }

    setSubmitting(true);
    setError(undefined);
    setMessage(undefined);

    const formData = new FormData();
    formData.append('displayName', displayName.trim());
    formData.append('kind', kind);
    formData.append('description', description.trim());
    formData.append('subjectTemplate', subjectTemplate.trim());
    formData.append('file', file);

    try {
      const uploaded = await upmsApi.uploadReportTemplate(formData);
      setDisplayName('');
      setKind('Document');
      setDescription('');
      setSubjectTemplate('');
      setFile(null);
      setMessage(`Uploaded template '${uploaded.displayName}'.`);
      await loadTemplates();
    } catch (err: any) {
      setError(err.message ?? 'Failed to upload report template.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <LoadingPanel label="Loading report templates" />;

  return (
    <>
      <ErrorAlert message={error} onClose={() => setError(undefined)} />
      <PageSection title="Report templates" description="Upload reusable tokenised templates for document, spreadsheet, presentation, email, or generic outputs.">
        {message ? (
          <Typography color="success.main" sx={{ mb: 2 }}>
            {message}
          </Typography>
        ) : null}
        <Grid container spacing={3}>
          <Grid item xs={12} lg={7}>
            <Box component="form" onSubmit={submit}>
              <Grid container spacing={2}>
                <Grid item xs={12} md={6}>
                  <TextField label="Display name" value={displayName} onChange={(e) => setDisplayName(e.target.value)} fullWidth required />
                </Grid>
                <Grid item xs={12} md={6}>
                  <TextField
                    select
                    SelectProps={{ native: true }}
                    InputLabelProps={{ shrink: true }}
                    label="Template kind"
                    value={kind}
                    onChange={(e) => setKind(e.target.value as ReportTemplateKind)}
                    fullWidth
                  >
                    {templateKinds.map((templateKind) => (
                      <option key={templateKind} value={templateKind}>
                        {templateKind}
                      </option>
                    ))}
                  </TextField>
                </Grid>
                <Grid item xs={12}>
                  <TextField label="Description" value={description} onChange={(e) => setDescription(e.target.value)} fullWidth multiline minRows={3} />
                </Grid>
                <Grid item xs={12}>
                  <TextField label="Email subject template" value={subjectTemplate} onChange={(e) => setSubjectTemplate(e.target.value)} fullWidth helperText="Optional. Used mainly for HTML email templates exported as .eml." />
                </Grid>
                <Grid item xs={12}>
                  <Button variant="outlined" component="label">
                    Choose template file
                    <input hidden type="file" accept=".html,.htm,.txt,.csv,.xml,.eml,.docx,.xlsx,.pptx" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
                  </Button>
                  <Typography sx={{ mt: 1 }} color="text.secondary">
                    {file ? `Selected file: ${file.name}` : 'Supported files: HTML, TXT, CSV, XML, EML, DOCX, XLSX, PPTX.'}
                  </Typography>
                </Grid>
              </Grid>
              <Button sx={{ mt: 2 }} type="submit" variant="contained" disabled={submitting || !displayName.trim() || !file}>
                Upload template
              </Button>
            </Box>
          </Grid>
          <Grid item xs={12} lg={5}>
            <Typography variant="h6" sx={{ mb: 1 }}>
              Example tokens
            </Typography>
            <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
              {tokenExamples.map((token) => (
                <Chip key={token} label={token} variant="outlined" />
              ))}
            </Stack>
            <Typography color="text.secondary" sx={{ mt: 2 }}>
              Use visible text tokens in DOCX and PPTX templates. Whole-page or whole-slide loops work best when start and end markers sit on their own lines.
            </Typography>
          </Grid>
        </Grid>
      </PageSection>

      <PageSection title="Uploaded templates" description="Newest templates appear first so you can confirm the latest upload.">
        <UpmsDataTable
          dataTestId="report-template-table"
          columns={[
            { key: 'displayName', label: 'Name' },
            { key: 'kind', label: 'Kind' },
            { key: 'extension', label: 'File Type' },
            { key: 'description', label: 'Description' },
            { key: 'uploadedAt', label: 'Uploaded' },
          ]}
          rows={templates
            .slice()
            .sort((left, right) => new Date(right.uploadedAt).getTime() - new Date(left.uploadedAt).getTime())
            .map((template) => ({
              id: template.id,
              displayName: template.displayName,
              kind: template.kind,
              extension: template.extension,
              description: template.description ?? '',
              uploadedAt: new Date(template.uploadedAt).toLocaleString(),
            }))}
        />
      </PageSection>
    </>
  );
}

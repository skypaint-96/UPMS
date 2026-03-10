import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  Grid,
  Link as MuiLink,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { Link } from 'react-router-dom';
import { upmsApi } from '../api/client';
import { ReportTemplate, ReportTemplateDetail, ReportTemplateKind, ReportTemplateType } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { LoadingPanel } from '../components/LoadingPanel';
import { PageSection } from '../components/PageSection';

const kindOrder: ReportTemplateKind[] = ['Document', 'Spreadsheet', 'Presentation', 'Email', 'Generic'];
const supportedFileAccept = '.html,.htm,.txt,.csv,.xml,.eml,.docx,.xlsx,.pptx';
const inlineTokenExamples = [
  '{{meta.company}}',
  '{{meta.itsm_source}}',
  '{{meta.as_of_date}}',
  '{{kpi.ticket_count}}',
  '{{table.kpis.html}}',
  '{{output.tickets.csv_document}}',
  '{{ticket.Number}}',
  '{{ticket.State}}',
];
const loopExamples = ['{{start per ticket State!=Closed}}', '{{start per ticket Priority=High scope=page}}', '{{end per ticket}}'];

type EditorMode = 'create' | 'edit';
type AuthoringMode = 'inline' | 'upload';

function compareTemplateTypes(left: ReportTemplateType, right: ReportTemplateType) {
  const leftOrder = kindOrder.indexOf(left.kind);
  const rightOrder = kindOrder.indexOf(right.kind);

  if (leftOrder !== rightOrder) {
    return leftOrder - rightOrder;
  }

  return left.displayName.localeCompare(right.displayName);
}

function compareTemplates(left: ReportTemplate, right: ReportTemplate) {
  const updated = new Date(right.updatedAt).getTime() - new Date(left.updatedAt).getTime();
  if (updated !== 0) {
    return updated;
  }

  return left.displayName.localeCompare(right.displayName);
}

function resolveType(templateTypes: ReportTemplateType[], typeId?: string | null) {
  return templateTypes.find((templateType) => templateType.typeId === typeId) ?? null;
}

export function ReportTemplatesPage() {
  const [templateTypes, setTemplateTypes] = useState<ReportTemplateType[]>([]);
  const [templates, setTemplates] = useState<ReportTemplate[]>([]);
  const [selectedTypeId, setSelectedTypeId] = useState('');
  const [selectedTemplateId, setSelectedTemplateId] = useState('');
  const [editorMode, setEditorMode] = useState<EditorMode>('create');
  const [authoringMode, setAuthoringMode] = useState<AuthoringMode>('inline');
  const [displayName, setDisplayName] = useState('');
  const [description, setDescription] = useState('');
  const [subjectTemplate, setSubjectTemplate] = useState('');
  const [textContent, setTextContent] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [editorDetail, setEditorDetail] = useState<ReportTemplateDetail | null>(null);
  const [kindFilter, setKindFilter] = useState<'All' | ReportTemplateKind>('All');
  const [searchText, setSearchText] = useState('');
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string>();
  const [message, setMessage] = useState<string>();

  const selectedType = useMemo(() => resolveType(templateTypes, selectedTypeId), [templateTypes, selectedTypeId]);

  const starterCount = useMemo(() => templates.filter((template) => template.isStarterTemplate).length, [templates]);
  const customCount = useMemo(() => templates.filter((template) => !template.isStarterTemplate).length, [templates]);
  const inlineTypeCount = useMemo(() => templateTypes.filter((templateType) => templateType.supportsInlineEdit).length, [templateTypes]);

  const filteredTemplates = useMemo(() => {
    return templates.filter((template) => {
      const matchesKind = kindFilter === 'All' || template.kind === kindFilter;
      if (!matchesKind) {
        return false;
      }

      const needle = searchText.trim().toLowerCase();
      if (!needle) {
        return true;
      }

      const haystack = [
        template.displayName,
        template.description ?? '',
        template.kind,
        template.extension,
        template.typeDisplayName ?? '',
        template.templateTypeId ?? '',
      ]
        .join(' ')
        .toLowerCase();

      return haystack.includes(needle);
    });
  }, [kindFilter, searchText, templates]);

  const loadLibrary = async (preferredTypeId?: string, preferredTemplateId?: string) => {
    const [loadedTypes, loadedTemplates] = await Promise.all([upmsApi.getReportTemplateTypes(), upmsApi.getReportTemplates()]);
    const sortedTypes = loadedTypes.slice().sort(compareTemplateTypes);
    const sortedTemplates = loadedTemplates.slice().sort(compareTemplates);

    setTemplateTypes(sortedTypes);
    setTemplates(sortedTemplates);
    setSelectedTypeId((current) => {
      if (preferredTypeId && sortedTypes.some((templateType) => templateType.typeId === preferredTypeId)) {
        return preferredTypeId;
      }

      if (current && sortedTypes.some((templateType) => templateType.typeId === current)) {
        return current;
      }

      return sortedTypes[0]?.typeId ?? '';
    });
    setSelectedTemplateId((current) => {
      if (preferredTemplateId && sortedTemplates.some((template) => template.id === preferredTemplateId)) {
        return preferredTemplateId;
      }

      if (current && sortedTemplates.some((template) => template.id === current)) {
        return current;
      }

      return sortedTemplates[0]?.id ?? '';
    });
  };

  const beginCreate = (typeId?: string) => {
    const resolvedType = resolveType(templateTypes, typeId) ?? selectedType ?? templateTypes[0] ?? null;

    setEditorMode('create');
    setEditorDetail(null);
    setSelectedTemplateId('');
    setSelectedTypeId(resolvedType?.typeId ?? '');
    setAuthoringMode(resolvedType?.supportsInlineEdit ? 'inline' : 'upload');
    setDisplayName('');
    setDescription('');
    setSubjectTemplate(resolvedType?.defaultSubjectTemplate ?? '');
    setTextContent('');
    setFile(null);
  };

  const beginEdit = async (templateId: string) => {
    setDetailLoading(true);
    setError(undefined);
    setMessage(undefined);

    try {
      const detail = await upmsApi.getReportTemplate(templateId);
      const libraryRow = templates.find((template) => template.id === detail.id);
      const resolvedTypeId = detail.templateTypeId ?? libraryRow?.templateTypeId ?? '';

      setEditorMode('edit');
      setEditorDetail(detail);
      setSelectedTemplateId(detail.id);
      setSelectedTypeId(resolvedTypeId);
      setAuthoringMode(detail.supportsInlineEdit ? 'inline' : 'upload');
      setDisplayName(detail.displayName);
      setDescription(detail.description ?? '');
      setSubjectTemplate(detail.subjectTemplate ?? '');
      setTextContent(detail.editableTextContent ?? '');
      setFile(null);
    } catch (err: any) {
      setError(err.message ?? 'Failed to load template detail.');
    } finally {
      setDetailLoading(false);
    }
  };

  useEffect(() => {
    loadLibrary()
      .catch((err: any) => setError(err.message ?? 'Failed to load report template workspace.'))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (selectedType && !selectedType.supportsInlineEdit && authoringMode !== 'upload') {
      setAuthoringMode('upload');
    }
  }, [authoringMode, selectedType]);

  useEffect(() => {
    if (editorMode !== 'create' || !selectedType) {
      return;
    }

    if (!subjectTemplate.trim() && selectedType.defaultSubjectTemplate) {
      setSubjectTemplate(selectedType.defaultSubjectTemplate);
    }
  }, [editorMode, selectedTypeId]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();

    if (!selectedType) {
      setError('Choose a supported template type before saving.');
      return;
    }

    if (!displayName.trim()) {
      setError('Display name is required.');
      return;
    }

    const requiresFile = editorMode === 'create' && (!selectedType.supportsInlineEdit || authoringMode === 'upload');
    if (requiresFile && !file) {
      setError(`Upload a ${selectedType.primaryExtension} template file to create this template.`);
      return;
    }

    setSubmitting(true);
    setError(undefined);
    setMessage(undefined);

    const formData = new FormData();
    formData.append('displayName', displayName.trim());
    formData.append('templateTypeId', selectedType.typeId);

    if (description.trim()) {
      formData.append('description', description.trim());
    }

    if (subjectTemplate.trim()) {
      formData.append('subjectTemplate', subjectTemplate.trim());
    }

    if (selectedType.supportsInlineEdit && authoringMode === 'inline') {
      formData.append('textContent', textContent);
    }

    if (file) {
      formData.append('file', file);
    }

    try {
      const saved = editorMode === 'create'
        ? await upmsApi.uploadReportTemplate(formData)
        : await upmsApi.updateReportTemplate(editorDetail!.id, formData);

      await loadLibrary(saved.templateTypeId ?? selectedType.typeId, saved.id);
      await beginEdit(saved.id);
      setMessage(editorMode === 'create' ? `Created template '${saved.displayName}'.` : `Updated template '${saved.displayName}'.`);
    } catch (err: any) {
      setError(err.message ?? 'Failed to save report template.');
    } finally {
      setSubmitting(false);
    }
  };

  const deleteTemplate = async (template: ReportTemplate) => {
    const confirmed = window.confirm(`Delete template '${template.displayName}'?`);
    if (!confirmed) {
      return;
    }

    setSubmitting(true);
    setError(undefined);
    setMessage(undefined);

    try {
      await upmsApi.deleteReportTemplate(template.id);
      await loadLibrary(template.templateTypeId ?? selectedTypeId);
      if (editorDetail?.id === template.id) {
        beginCreate(template.templateTypeId ?? selectedTypeId);
      }
      setMessage(`Deleted template '${template.displayName}'.`);
    } catch (err: any) {
      setError(err.message ?? 'Failed to delete report template.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <LoadingPanel label="Loading report template workspace" />;

  return (
    <>
      <ErrorAlert message={error} onClose={() => setError(undefined)} />

      <PageSection
        title="Supported template types"
        description="The report system now revolves around a registry of supported template types. Fresh environments seed a starter example for each type, and more types can be added in code without reworking the template library UX."
      >
        {message ? <Alert severity="success" sx={{ mb: 3 }}>{message}</Alert> : null}
        <Alert severity="info" sx={{ mb: 3 }}>
          Use this workspace to curate the template library, then open the{' '}
          <MuiLink component={Link} to="/reports" underline="hover">
            reports page
          </MuiLink>{' '}
          to generate outputs directly from those templates.
        </Alert>

        <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap" sx={{ mb: 3 }}>
          <Chip label={`${templateTypes.length} supported types`} variant="outlined" />
          <Chip label={`${starterCount} starter templates`} variant="outlined" />
          <Chip label={`${customCount} custom templates`} variant="outlined" />
          <Chip label={`${inlineTypeCount} inline-edit types`} variant="outlined" />
        </Stack>

        <Grid container spacing={2}>
          {templateTypes.map((templateType) => {
            const selected = templateType.typeId === selectedTypeId;

            return (
              <Grid item xs={12} md={6} lg={4} key={templateType.typeId}>
                <Paper
                  variant="outlined"
                  sx={{
                    p: 2,
                    height: '100%',
                    borderColor: selected ? 'primary.main' : undefined,
                  }}
                >
                  <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap" sx={{ mb: 1.5 }}>
                    <Chip label={templateType.kind} size="small" />
                    <Chip label={templateType.primaryExtension} size="small" variant="outlined" />
                    <Chip label={templateType.supportsInlineEdit ? 'Inline edit' : 'Upload only'} size="small" color={templateType.supportsInlineEdit ? 'success' : 'default'} />
                    <Chip label="Starter seeded" size="small" color="info" variant="outlined" />
                  </Stack>
                  <Typography variant="h6" sx={{ mb: 1 }}>
                    {templateType.displayName}
                  </Typography>
                  <Typography color="text.secondary" sx={{ mb: 2 }}>
                    {templateType.description ?? 'No description provided for this template type.'}
                  </Typography>
                  <Typography variant="body2" sx={{ mb: 2 }}>
                    Extensions: {templateType.extensions.join(', ')}
                  </Typography>
                  <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                    {templateType.starterTemplateDisplayName ?? 'Starter template'}: {templateType.starterTemplateDescription ?? 'Included automatically on fresh state.'}
                  </Typography>
                  <Button variant={selected ? 'contained' : 'outlined'} onClick={() => beginCreate(templateType.typeId)}>
                    Use this type
                  </Button>
                </Paper>
              </Grid>
            );
          })}
        </Grid>
      </PageSection>

      <PageSection
        title={editorMode === 'create' ? 'Create template' : 'Edit template'}
        description="Upload a file or author inline text for the supported text-based formats. Saved templates are stored in the shared library and become immediately available for report generation."
      >
        <Grid container spacing={3}>
          <Grid item xs={12} lg={7}>
            <Box component="form" onSubmit={submit}>
              <Grid container spacing={2}>
                <Grid item xs={12} md={6}>
                  <TextField
                    label="Display name"
                    value={displayName}
                    onChange={(event) => setDisplayName(event.target.value)}
                    fullWidth
                    required
                    helperText="Use a library-friendly name that report runners can select easily."
                  />
                </Grid>
                <Grid item xs={12} md={6}>
                  <TextField
                    select
                    SelectProps={{ native: true }}
                    InputLabelProps={{ shrink: true }}
                    label="Template type"
                    value={selectedTypeId}
                    onChange={(event) => {
                      const nextTypeId = event.target.value;
                      const nextType = resolveType(templateTypes, nextTypeId);
                      setSelectedTypeId(nextTypeId);
                      setFile(null);
                      if (nextType) {
                        if (!nextType.supportsInlineEdit) {
                          setAuthoringMode('upload');
                        } else if (editorMode === 'create') {
                          setAuthoringMode('inline');
                        }
                      }
                    }}
                    fullWidth
                    helperText="Changing type without replacing the file only works when the extension remains compatible."
                  >
                    <option value="">Select a type</option>
                    {kindOrder.map((kind) => {
                      const rows = templateTypes.filter((templateType) => templateType.kind === kind);
                      if (rows.length === 0) {
                        return null;
                      }

                      return (
                        <optgroup key={kind} label={kind}>
                          {rows.map((templateType) => (
                            <option key={templateType.typeId} value={templateType.typeId}>
                              {templateType.displayName} ({templateType.primaryExtension})
                            </option>
                          ))}
                        </optgroup>
                      );
                    })}
                  </TextField>
                </Grid>
                <Grid item xs={12}>
                  <TextField
                    label="Description"
                    value={description}
                    onChange={(event) => setDescription(event.target.value)}
                    fullWidth
                    multiline
                    minRows={3}
                    helperText="Summarise when this template should be used and what audience it serves."
                  />
                </Grid>

                {selectedType?.kind === 'Email' || subjectTemplate ? (
                  <Grid item xs={12}>
                    <TextField
                      label="Subject template"
                      value={subjectTemplate}
                      onChange={(event) => setSubjectTemplate(event.target.value)}
                      fullWidth
                      helperText="Optional. Used when HTML or EML templates generate an email draft."
                    />
                  </Grid>
                ) : null}

                {selectedType?.supportsInlineEdit ? (
                  <Grid item xs={12} md={6}>
                    <TextField
                      select
                      SelectProps={{ native: true }}
                      InputLabelProps={{ shrink: true }}
                      label="Authoring mode"
                      value={authoringMode}
                      onChange={(event) => setAuthoringMode(event.target.value as AuthoringMode)}
                      fullWidth
                      helperText="Inline text is quickest for HTML, TXT, CSV, XML, and EML templates."
                    >
                      <option value="inline">Author inline</option>
                      <option value="upload">Upload file</option>
                    </TextField>
                  </Grid>
                ) : null}

                {selectedType?.supportsInlineEdit && authoringMode === 'inline' ? (
                  <Grid item xs={12}>
                    <TextField
                      label="Template body"
                      value={textContent}
                      onChange={(event) => setTextContent(event.target.value)}
                      fullWidth
                      multiline
                      minRows={16}
                      placeholder="Write your template with visible {{token}} markers and optional per-ticket loop blocks."
                      helperText={`Stored as ${selectedType.primaryExtension} text. Reports generated from this template will use the tokenised template runner automatically.`}
                    />
                  </Grid>
                ) : (
                  <Grid item xs={12}>
                    <Button variant="outlined" component="label">
                      {editorMode === 'create' ? 'Choose template file' : 'Choose replacement file'}
                      <input
                        hidden
                        type="file"
                        accept={selectedType ? selectedType.extensions.join(',') : supportedFileAccept}
                        onChange={(event) => setFile(event.target.files?.[0] ?? null)}
                      />
                    </Button>
                    <Typography sx={{ mt: 1 }} color="text.secondary">
                      {file
                        ? `Selected file: ${file.name}`
                        : editorMode === 'edit'
                          ? 'Leave this empty to keep the current file and update metadata only.'
                          : `Supported extensions for this type: ${selectedType?.extensions.join(', ') ?? supportedFileAccept}`}
                    </Typography>
                  </Grid>
                )}
              </Grid>

              <Stack direction="row" spacing={1.5} useFlexGap flexWrap="wrap" sx={{ mt: 3 }}>
                <Button type="submit" variant="contained" disabled={submitting || !selectedTypeId || !displayName.trim()}>
                  {editorMode === 'create' ? 'Save template' : 'Save changes'}
                </Button>
                <Button variant="outlined" onClick={() => beginCreate(selectedTypeId)} disabled={submitting}>
                  Create new instead
                </Button>
                {editorDetail ? (
                  <Button component="a" href={upmsApi.getReportTemplateDownloadUrl(editorDetail.id)} variant="outlined">
                    Export current file
                  </Button>
                ) : null}
                {editorDetail ? (
                  <Button color="error" variant="outlined" onClick={() => void deleteTemplate(editorDetail)} disabled={submitting}>
                    Delete template
                  </Button>
                ) : null}
              </Stack>
            </Box>
          </Grid>

          <Grid item xs={12} lg={5}>
            <Stack spacing={2}>
              <Paper variant="outlined" sx={{ p: 2 }}>
                <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap" sx={{ mb: 1.5 }}>
                  {selectedType ? <Chip label={selectedType.kind} size="small" /> : null}
                  {selectedType ? <Chip label={selectedType.primaryExtension} size="small" variant="outlined" /> : null}
                  {selectedType ? <Chip label={selectedType.supportsInlineEdit ? 'Inline edit supported' : 'Binary upload required'} size="small" color={selectedType.supportsInlineEdit ? 'success' : 'default'} /> : null}
                </Stack>
                <Typography variant="h6" sx={{ mb: 1 }}>
                  {selectedType?.displayName ?? 'Choose a template type'}
                </Typography>
                <Typography color="text.secondary" sx={{ mb: 2 }}>
                  {selectedType?.description ?? 'Select a type to see authoring guidance, starter metadata, and save options.'}
                </Typography>
                {selectedType?.starterTemplateDisplayName ? (
                  <Typography variant="body2" sx={{ mb: 1 }}>
                    Starter example: {selectedType.starterTemplateDisplayName}
                  </Typography>
                ) : null}
                {selectedType?.authoringGuidance ? (
                  <Typography variant="body2" color="text.secondary">
                    {selectedType.authoringGuidance}
                  </Typography>
                ) : null}
              </Paper>

              <Paper variant="outlined" sx={{ p: 2 }}>
                <Typography variant="h6" sx={{ mb: 1 }}>
                  Token patterns
                </Typography>
                <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap" sx={{ mb: 2 }}>
                  {inlineTokenExamples.map((token) => (
                    <Chip key={token} label={token} size="small" variant="outlined" />
                  ))}
                </Stack>
                <Typography variant="subtitle2" sx={{ mb: 1 }}>
                  Loop markers
                </Typography>
                <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
                  {loopExamples.map((token) => (
                    <Chip key={token} label={token} size="small" variant="outlined" />
                  ))}
                </Stack>
              </Paper>

              <Paper variant="outlined" sx={{ p: 2 }}>
                <Typography variant="h6" sx={{ mb: 1 }}>
                  Storage and generation flow
                </Typography>
                <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                  Every saved template is stored in the shared report-template library. The report runner reads directly from this library, so there is no separate report-definition catalogue to maintain.
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {detailLoading
                    ? 'Loading template detail…'
                    : editorDetail
                      ? `Currently editing ${editorDetail.displayName}. Last updated ${new Date(editorDetail.updatedAt).toLocaleString()} by ${editorDetail.updatedBy ?? editorDetail.uploadedBy}.`
                      : 'Choose a type to create a new template or edit one from the library below.'}
                </Typography>
              </Paper>
            </Stack>
          </Grid>
        </Grid>
      </PageSection>

      <PageSection title="Template library" description="Starter and custom templates are all managed in one place. Edit, export, or delete them here; generate with them from the reports page.">
        <Grid container spacing={2} sx={{ mb: 2 }}>
          <Grid item xs={12} md={7}>
            <TextField
              label="Search templates"
              value={searchText}
              onChange={(event) => setSearchText(event.target.value)}
              fullWidth
              helperText="Search by name, kind, extension, type, or description."
            />
          </Grid>
          <Grid item xs={12} md={5}>
            <TextField
              select
              SelectProps={{ native: true }}
              InputLabelProps={{ shrink: true }}
              label="Filter by kind"
              value={kindFilter}
              onChange={(event) => setKindFilter(event.target.value as 'All' | ReportTemplateKind)}
              fullWidth
            >
              <option value="All">All kinds</option>
              {kindOrder.map((kind) => (
                <option key={kind} value={kind}>
                  {kind}
                </option>
              ))}
            </TextField>
          </Grid>
        </Grid>

        <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap" sx={{ mb: 3 }}>
          <Chip label={`${filteredTemplates.length} shown`} variant="outlined" />
          <Chip label={`${starterCount} starter`} variant="outlined" />
          <Chip label={`${customCount} custom`} variant="outlined" />
        </Stack>

        {filteredTemplates.length === 0 ? (
          <Alert severity="warning">No templates match the current filters.</Alert>
        ) : (
          <Grid container spacing={2}>
            {filteredTemplates.map((template) => (
              <Grid item xs={12} md={6} lg={4} key={template.id}>
                <Paper
                  variant="outlined"
                  sx={{
                    p: 2,
                    height: '100%',
                    borderColor: selectedTemplateId === template.id ? 'primary.main' : undefined,
                  }}
                >
                  <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap" sx={{ mb: 1.5 }}>
                    <Chip label={template.kind} size="small" />
                    <Chip label={template.typeDisplayName ?? template.extension} size="small" variant="outlined" />
                    <Chip label={template.extension} size="small" variant="outlined" />
                    {template.isStarterTemplate ? <Chip label="Starter" size="small" color="success" /> : <Chip label="Custom" size="small" color="primary" variant="outlined" />}
                  </Stack>
                  <Typography variant="h6" sx={{ mb: 1 }}>
                    {template.displayName}
                  </Typography>
                  <Typography color="text.secondary" sx={{ mb: 2 }}>
                    {template.description ?? 'No description has been added for this template yet.'}
                  </Typography>
                  <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                    Updated {new Date(template.updatedAt).toLocaleString()} by {template.updatedBy ?? template.uploadedBy}
                  </Typography>
                  <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
                    <Button variant="contained" onClick={() => void beginEdit(template.id)} disabled={detailLoading || submitting}>
                      Edit
                    </Button>
                    <Button component="a" href={upmsApi.getReportTemplateDownloadUrl(template.id)} variant="outlined">
                      Export
                    </Button>
                    <Button variant="outlined" color="error" onClick={() => void deleteTemplate(template)} disabled={submitting}>
                      Delete
                    </Button>
                  </Stack>
                </Paper>
              </Grid>
            ))}
          </Grid>
        )}
      </PageSection>
    </>
  );
}

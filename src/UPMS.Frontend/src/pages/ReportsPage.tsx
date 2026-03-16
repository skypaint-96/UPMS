import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Alert, Box, Button, Chip, Grid, Link as MuiLink, Paper, Stack, TextField, Typography } from '@mui/material';
import { Link, useNavigate } from 'react-router-dom';
import { upmsApi } from '../api/client';
import { DistributionList, ItsmSourceSummary, ReportParameter, ReportPlugin, ReportTemplate, ReportTemplateScope } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { LoadingPanel } from '../components/LoadingPanel';
import { PageSection } from '../components/PageSection';

const kindOrder = ['Document', 'Spreadsheet', 'Presentation', 'Email', 'Generic'] as const;
const contextParameterKeys = new Set(['itsm_source', 'company']);

function compareTemplates(left: ReportTemplate, right: ReportTemplate) {
  return new Date(right.updatedAt).getTime() - new Date(left.updatedAt).getTime();
}

function formatScope(scope?: ReportTemplateScope | null) {
  if (!scope || scope.isGlobal) {
    return 'Global: available for all ITSM sources and companies.';
  }

  const parts: string[] = [];

  if (scope.itsmSources.length > 0) {
    parts.push(`Sources: ${scope.itsmSources.join(', ')}`);
  }

  if (scope.companies.length > 0) {
    parts.push(`Companies: ${scope.companies.join(', ')}`);
  }

  if (scope.itsmSourceCompanies.length > 0) {
    parts.push(`Pairs: ${scope.itsmSourceCompanies.map((pair) => `${pair.itsmSource} + ${pair.company}`).join('; ')}`);
  }

  return parts.join(' • ');
}

export function ReportsPage() {
  const navigate = useNavigate();
  const [runner, setRunner] = useState<ReportPlugin | null>(null);
  const [templates, setTemplates] = useState<ReportTemplate[]>([]);
  const [sources, setSources] = useState<ItsmSourceSummary[]>([]);
  const [availableDistributionLists, setAvailableDistributionLists] = useState<DistributionList[]>([]);
  const [selectedDistributionListIds, setSelectedDistributionListIds] = useState<string[]>([]);
  const [distributionListsLoading, setDistributionListsLoading] = useState(false);
  const [selectedTemplateId, setSelectedTemplateId] = useState('');
  const [parameters, setParameters] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [templatesLoading, setTemplatesLoading] = useState(false);
  const [error, setError] = useState<string>();
  const [submitting, setSubmitting] = useState(false);

  const selectedItsmSource = (parameters.itsm_source ?? '').trim();
  const selectedCompany = (parameters.company ?? '').trim();
  const templateContextReady = selectedItsmSource.length > 0 && selectedCompany.length > 0;

  useEffect(() => {
    Promise.all([upmsApi.getReportPlugins(), upmsApi.getItsmSources()])
      .then(([loadedPlugins, loadedSources]) => {
        const selectedRunner = loadedPlugins.find((plugin) => plugin.pluginId === 'tokenised-template-report') ?? loadedPlugins[0] ?? null;
        setRunner(selectedRunner);
        setSources(loadedSources);
        if (loadedSources.length === 1) {
          setParameters((current) => ({ ...current, itsm_source: current.itsm_source ?? loadedSources[0].name }));
        }
      })
      .catch((err: any) => setError(err.message ?? 'Failed to load reporting options.'))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    let cancelled = false;

    if (!templateContextReady) {
      setTemplates([]);
      setSelectedTemplateId('');
      setTemplatesLoading(false);
      return () => {
        cancelled = true;
      };
    }

    setTemplatesLoading(true);
    setError(undefined);

    const timer = window.setTimeout(() => {
      upmsApi.getReportTemplates({ itsmSource: selectedItsmSource, company: selectedCompany })
        .then((loadedTemplates) => {
          if (cancelled) {
            return;
          }

          const sortedTemplates = loadedTemplates.slice().sort(compareTemplates);
          setTemplates(sortedTemplates);
          setSelectedTemplateId((current) => {
            if (current && sortedTemplates.some((template) => template.id === current)) {
              return current;
            }

            return sortedTemplates[0]?.id ?? '';
          });
        })
        .catch((err: any) => {
          if (cancelled) {
            return;
          }

          setTemplates([]);
          setSelectedTemplateId('');
          setError(err.message ?? 'Failed to load report templates for the selected context.');
        })
        .finally(() => {
          if (!cancelled) {
            setTemplatesLoading(false);
          }
        });
    }, 250);

    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [selectedItsmSource, selectedCompany, templateContextReady]);

  const selectedTemplate = useMemo(
    () => templates.find((template) => template.id === selectedTemplateId) ?? null,
    [templates, selectedTemplateId],
  );

  const starterCount = useMemo(() => templates.filter((template) => template.isStarterTemplate).length, [templates]);

  const runnerParameters = useMemo(
    () => (runner?.parameters ?? []).filter((parameter) => parameter.key !== 'template_id'),
    [runner],
  );

  const contextParameters = useMemo(
    () => runnerParameters.filter((parameter) => contextParameterKeys.has(parameter.key)),
    [runnerParameters],
  );

  const generationParameters = useMemo(
    () => runnerParameters.filter((parameter) => !contextParameterKeys.has(parameter.key)),
    [runnerParameters],
  );

  const companyValue = (parameters.company ?? '').trim();

  useEffect(() => {
    if (!selectedTemplate) {
      return;
    }

    setParameters((current) => {
      const next = { ...current, template_id: selectedTemplate.id };
      const defaultOutputMode = selectedTemplate.kind === 'Email' ? 'Auto' : selectedTemplate.extension === '.html' ? 'Preview HTML' : 'Auto';

      if (selectedDistributionListIds.length > 0) {
        next.output_mode = 'Download Filled File';
      } else if (!next.output_mode) {
        next.output_mode = defaultOutputMode;
      }

      return next;
    });
  }, [selectedTemplate, selectedDistributionListIds.length]);

  useEffect(() => {
    const normalizedCompany = companyValue;
    if (!normalizedCompany) {
      setAvailableDistributionLists([]);
      setSelectedDistributionListIds([]);
      return;
    }

    setDistributionListsLoading(true);
    upmsApi
      .getDistributionLists({ company: normalizedCompany })
      .then((lists) => {
        const activeLists = lists.filter((list) => list.isActive && list.activeRecipientCount > 0);
        setAvailableDistributionLists(activeLists);
        setSelectedDistributionListIds((current) => current.filter((id) => activeLists.some((list) => list.id === id)));
      })
      .catch((err: any) => setError(err.message ?? 'Failed to load distribution lists.'))
      .finally(() => setDistributionListsLoading(false));
  }, [companyValue]);

  useEffect(() => {
    if (selectedDistributionListIds.length === 0) {
      return;
    }

    setParameters((current) => {
      if (current.output_mode === 'Download Filled File') {
        return current;
      }

      return {
        ...current,
        output_mode: 'Download Filled File',
      };
    });
  }, [selectedDistributionListIds]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!runner || !selectedTemplate) {
      return;
    }

    setSubmitting(true);
    setError(undefined);
    try {
      const job = await upmsApi.queueReport({
        pluginId: runner.pluginId,
        parameters: {
          ...parameters,
          template_id: selectedTemplate.id,
          ...(selectedDistributionListIds.length > 0 ? { output_mode: 'Download Filled File' } : {}),
        },
        distributionListIds: selectedDistributionListIds.length > 0 ? selectedDistributionListIds : undefined,
      });
      navigate(`/jobs?jobId=${encodeURIComponent(job.id)}`);
    } catch (err: any) {
      setError(err.message ?? 'Failed to queue report job.');
    } finally {
      setSubmitting(false);
    }
  };

  const renderParameterField = (parameter: ReportParameter) => {
    if (parameter.type === 'ItsmSource') {
      return (
        <TextField
          select
          SelectProps={{ native: true }}
          InputLabelProps={{ shrink: true }}
          label={parameter.displayName}
          value={parameters[parameter.key] ?? ''}
          onChange={(event) => setParameters((current) => ({ ...current, [parameter.key]: event.target.value }))}
          required={parameter.isRequired}
          fullWidth
          helperText={parameter.description ?? 'Choose the ITSM source to query.'}
        >
          <option value="">Select a source</option>
          {sources.map((source) => (
            <option key={source.name} value={source.name}>
              {source.displayLabel}
            </option>
          ))}
        </TextField>
      );
    }

    if (parameter.type === 'MultiSelect') {
      return (
        <TextField
          label={parameter.displayName}
          value={parameters[parameter.key] ?? ''}
          onChange={(event) => setParameters((current) => ({ ...current, [parameter.key]: event.target.value }))}
          placeholder="Comma separate values"
          required={parameter.isRequired}
          fullWidth
          multiline
          minRows={3}
          helperText={parameter.description ?? `Suggested values: ${parameter.options.join(', ')}`}
        />
      );
    }

    if (parameter.options && parameter.options.length > 0) {
      return (
        <TextField
          select
          SelectProps={{ native: true }}
          InputLabelProps={{ shrink: true }}
          label={parameter.displayName}
          value={parameters[parameter.key] ?? ''}
          onChange={(event) => setParameters((current) => ({ ...current, [parameter.key]: event.target.value }))}
          required={parameter.isRequired}
          fullWidth
          helperText={parameter.description ?? parameter.type}
        >
          <option value="">Select an option</option>
          {parameter.options.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </TextField>
      );
    }

    if (parameter.type === 'TextArea') {
      return (
        <TextField
          label={parameter.displayName}
          value={parameters[parameter.key] ?? ''}
          onChange={(event) => setParameters((current) => ({ ...current, [parameter.key]: event.target.value }))}
          placeholder={parameter.placeholder ?? undefined}
          required={parameter.isRequired}
          fullWidth
          multiline
          minRows={4}
          helperText={parameter.description ?? parameter.type}
        />
      );
    }

    if (parameter.type === 'Date') {
      return (
        <TextField
          label={parameter.displayName}
          type="date"
          InputLabelProps={{ shrink: true }}
          value={parameters[parameter.key] ?? ''}
          onChange={(event) => setParameters((current) => ({ ...current, [parameter.key]: event.target.value }))}
          required={parameter.isRequired}
          fullWidth
          helperText={parameter.description ?? parameter.type}
        />
      );
    }

    return (
      <TextField
        label={parameter.displayName}
        value={parameters[parameter.key] ?? ''}
        onChange={(event) => setParameters((current) => ({ ...current, [parameter.key]: event.target.value }))}
        placeholder={parameter.placeholder ?? undefined}
        required={parameter.isRequired}
        fullWidth
        helperText={parameter.description ?? parameter.type}
      />
    );
  };

  const toggleDistributionList = (listId: string) => {
    setSelectedDistributionListIds((current) =>
      current.includes(listId) ? current.filter((id) => id !== listId) : [...current, listId],
    );
  };

  if (loading) return <LoadingPanel label="Loading report templates" />;

  return (
    <>
      <ErrorAlert message={error} onClose={() => setError(undefined)} />
      <PageSection title="Reports" description="Generate reports from reusable templates. The worker fills the selected template with live ticket data and stores the output on the jobs page.">
        <Alert severity="info" sx={{ mb: 3 }}>
          Fresh installs include starter templates for every supported type. Create and curate the library from the{' '}
          <MuiLink component={Link} to="/report-templates" underline="hover">
            report templates workspace
          </MuiLink>
          .
        </Alert>

        {!runner ? (
          <Alert severity="error">The template report runner is not registered.</Alert>
        ) : (
          <Grid container spacing={3}>
            <Grid item xs={12} lg={5}>
              <Typography variant="h6" sx={{ mb: 1 }}>
                Report context
              </Typography>
              <Typography color="text.secondary" sx={{ mb: 2 }}>
                Choose the ITSM source and company first. Template availability is filtered server-side for that context.
              </Typography>

              <Grid container spacing={2} sx={{ mb: 2 }}>
                {contextParameters.map((parameter) => (
                  <Grid item xs={12} md={parameter.key === 'company' ? 12 : 6} key={parameter.key}>
                    {renderParameterField(parameter)}
                  </Grid>
                ))}
              </Grid>

              {!templateContextReady ? (
                <Alert severity="info">Choose an ITSM source and company to load matching templates.</Alert>
              ) : templatesLoading ? (
                <Alert severity="info">Loading templates for the selected source and company…</Alert>
              ) : templates.length === 0 ? (
                <Alert severity="warning">
                  No templates are available for ITSM source '{selectedItsmSource}' and company '{selectedCompany}'.
                </Alert>
              ) : (
                <>
                  <TextField
                    select
                    SelectProps={{ native: true }}
                    InputLabelProps={{ shrink: true }}
                    label="Report template"
                    value={selectedTemplateId}
                    onChange={(event) => setSelectedTemplateId(event.target.value)}
                    fullWidth
                    sx={{ mb: 2 }}
                  >
                    <option value="">Select a template</option>
                    {kindOrder.map((kind) => {
                      const rows = templates.filter((template) => template.kind === kind);
                      if (rows.length === 0) {
                        return null;
                      }

                      return (
                        <optgroup key={kind} label={kind}>
                          {rows.map((template) => (
                            <option key={template.id} value={template.id}>
                              {template.displayName}
                              {template.isStarterTemplate ? ' — starter' : ''}
                            </option>
                          ))}
                        </optgroup>
                      );
                    })}
                  </TextField>

                  {selectedTemplate ? (
                    <Paper variant="outlined" sx={{ p: 2 }}>
                      <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap" sx={{ mb: 2 }}>
                        <Chip label={selectedTemplate.kind} size="small" />
                        <Chip label={selectedTemplate.typeDisplayName ?? selectedTemplate.extension} size="small" />
                        <Chip label={selectedTemplate.extension} size="small" variant="outlined" />
                        {selectedTemplate.isStarterTemplate ? <Chip label="Starter" size="small" color="success" /> : null}
                        <Chip label={selectedTemplate.scope?.isGlobal ? 'Global' : 'Scoped'} size="small" color={selectedTemplate.scope?.isGlobal ? 'info' : 'warning'} variant="outlined" />
                      </Stack>
                      <Typography variant="h6" sx={{ mb: 1 }}>
                        {selectedTemplate.displayName}
                      </Typography>
                      <Typography color="text.secondary" sx={{ mb: 2 }}>
                        {selectedTemplate.description ?? 'No description has been provided for this template yet.'}
                      </Typography>
                      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                        {formatScope(selectedTemplate.scope)}
                      </Typography>
                      <Typography variant="body2" color="text.secondary">
                        Updated {new Date(selectedTemplate.updatedAt).toLocaleString()} by {selectedTemplate.updatedBy ?? selectedTemplate.uploadedBy}
                      </Typography>
                    </Paper>
                  ) : null}

                  <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap" sx={{ mt: 2 }}>
                    <Chip label={`${templates.length} templates`} variant="outlined" />
                    <Chip label={`${starterCount} starters`} variant="outlined" />
                  </Stack>
                </>
              )}
            </Grid>

            <Grid item xs={12} lg={7}>
              <Box component="form" onSubmit={submit}>
                <Typography variant="h6" sx={{ mb: 1 }}>
                  Generation inputs
                </Typography>
                <Typography color="text.secondary" sx={{ mb: 2 }}>
                  {runner.description}
                </Typography>
                <Grid container spacing={2} sx={{ mb: 2 }}>
                  {generationParameters.map((parameter) => (
                    <Grid item xs={12} md={parameter.type === 'TextArea' || parameter.type === 'MultiSelect' ? 12 : 6} key={parameter.key}>
                      {renderParameterField(parameter)}
                    </Grid>
                  ))}
                </Grid>

                <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
                  <Typography variant="subtitle1" sx={{ mb: 1 }}>
                    Delivery lists
                  </Typography>
                  <Typography color="text.secondary" sx={{ mb: 2 }}>
                    Rendering and sending are separate. Choose one or more company lists to queue delivery after the report artifact is generated.
                  </Typography>
                  {!companyValue ? (
                    <Alert severity="info">
                      Enter a company value above to load that company&apos;s available distribution lists. Manage lists from the{' '}
                      <MuiLink component={Link} to="/distribution-lists" underline="hover">
                        Distribution Lists
                      </MuiLink>{' '}
                      page.
                    </Alert>
                  ) : distributionListsLoading ? (
                    <LoadingPanel label="Loading distribution lists" />
                  ) : availableDistributionLists.length === 0 ? (
                    <Alert severity="warning">
                      No active distribution lists were found for {companyValue}. You can still queue the report without delivery, or create a list in the{' '}
                      <MuiLink component={Link} to="/distribution-lists" underline="hover">
                        Distribution Lists
                      </MuiLink>{' '}
                      page.
                    </Alert>
                  ) : (
                    <>
                      <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap" sx={{ mb: 1.5 }}>
                        {availableDistributionLists.map((list) => {
                          const selected = selectedDistributionListIds.includes(list.id);
                          return (
                            <Chip
                              key={list.id}
                              label={`${list.name} (${list.activeRecipientCount})`}
                              color={selected ? 'primary' : 'default'}
                              variant={selected ? 'filled' : 'outlined'}
                              onClick={() => toggleDistributionList(list.id)}
                              clickable
                            />
                          );
                        })}
                      </Stack>
                      {selectedDistributionListIds.length > 0 ? (
                        <Alert severity="info" sx={{ mt: 1 }}>
                          {selectedDistributionListIds.length} distribution list{selectedDistributionListIds.length === 1 ? '' : 's'} selected. The worker will render first, store a single artifact, then queue separate delivery jobs without re-rendering the report.
                        </Alert>
                      ) : null}
                    </>
                  )}
                </Paper>

                <Button type="submit" variant="contained" disabled={submitting || templatesLoading || !templateContextReady || !selectedTemplateId}>
                  Queue report job
                </Button>
              </Box>
            </Grid>
          </Grid>
        )}
      </PageSection>
    </>
  );
}

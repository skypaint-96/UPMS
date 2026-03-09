import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Box, Button, Grid, Link as MuiLink, TextField, Typography } from '@mui/material';
import { Link, useNavigate } from 'react-router-dom';
import { upmsApi } from '../api/client';
import { ReportPlugin } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { LoadingPanel } from '../components/LoadingPanel';
import { PageSection } from '../components/PageSection';

export function ReportsPage() {
  const navigate = useNavigate();
  const [plugins, setPlugins] = useState<ReportPlugin[]>([]);
  const [selectedPluginId, setSelectedPluginId] = useState('');
  const [parameters, setParameters] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    upmsApi
      .getReportPlugins()
      .then((loaded) => {
        setPlugins(loaded);
        if (loaded.length > 0) {
          setSelectedPluginId(loaded[0].pluginId);
        }
      })
      .catch((err) => setError(err.message ?? 'Failed to load report plugins.'))
      .finally(() => setLoading(false));
  }, []);

  const selectedPlugin = useMemo(
    () => plugins.find((plugin) => plugin.pluginId === selectedPluginId) ?? null,
    [plugins, selectedPluginId],
  );

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!selectedPluginId) {
      return;
    }

    setSubmitting(true);
    setError(undefined);
    try {
      const job = await upmsApi.queueReport({ pluginId: selectedPluginId, parameters });
      navigate(`/jobs?jobId=${encodeURIComponent(job.id)}`);
    } catch (err: any) {
      setError(err.message ?? 'Failed to queue report job.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <LoadingPanel label="Loading report plugins" />;

  return (
    <>
      <ErrorAlert message={error} onClose={() => setError(undefined)} />
      <PageSection title="Reports" description="Run user-facing reports through the worker and collect the output from the jobs page.">
        <Typography color="text.secondary" sx={{ mb: 2 }}>
          Reusable template upload is available from the power user menu in the top-right settings drawer.
          {' '}
          <MuiLink component={Link} to="/report-templates" underline="hover">
            Open report templates.
          </MuiLink>
        </Typography>
        <Box component="form" onSubmit={submit}>
          <TextField
            select
            SelectProps={{ native: true }}
            InputLabelProps={{ shrink: true }}
            label="Report plugin"
            value={selectedPluginId}
            onChange={(e) => setSelectedPluginId(e.target.value)}
            fullWidth
            sx={{ mb: 2 }}
          >
            {plugins.map((plugin) => (
              <option key={plugin.pluginId} value={plugin.pluginId}>
                {plugin.displayName}
              </option>
            ))}
          </TextField>
          <Typography sx={{ mb: 2 }}>{selectedPlugin?.description}</Typography>
          <Grid container spacing={2} sx={{ mb: 2 }}>
            {selectedPlugin?.parameters.map((parameter) => (
              <Grid item xs={12} md={6} key={parameter.key}>
                {parameter.options && parameter.options.length > 0 ? (
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
                ) : (
                  <TextField
                    label={parameter.displayName}
                    value={parameters[parameter.key] ?? ''}
                    onChange={(event) => setParameters((current) => ({ ...current, [parameter.key]: event.target.value }))}
                    placeholder={parameter.placeholder ?? undefined}
                    required={parameter.isRequired}
                    fullWidth
                    helperText={parameter.description ?? parameter.type}
                  />
                )}
              </Grid>
            ))}
          </Grid>
          <Button type="submit" variant="contained" disabled={submitting || !selectedPluginId}>
            Queue report job
          </Button>
        </Box>
      </PageSection>
    </>
  );
}

import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  Box,
  Button,
  Checkbox,
  Chip,
  FormControlLabel,
  Grid,
  Link as MuiLink,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { upmsApi } from '../api/client';
import { FileSharePollingSettings, FileSharePollingSource, ItsmSourceSummary } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { LoadingPanel } from '../components/LoadingPanel';
import { PageSection } from '../components/PageSection';
import { UpmsDataTable } from '../components/UpmsDataTable';

type SourceFormState = {
  id?: string;
  name: string;
  enabled: boolean;
  watchedPath: string;
  filePatternsText: string;
  archivePath: string;
  errorPath: string;
  itsmSource: string;
  pollIntervalSeconds: string;
  maxFilesPerCycle: string;
  stableFileAgeSeconds: string;
};

function createEmptyForm(settings?: FileSharePollingSettings | null, defaultItsmSource?: string) {
  return {
    name: '',
    enabled: true,
    watchedPath: '',
    filePatternsText: '*.csv\n*.json',
    archivePath: '',
    errorPath: '',
    itsmSource: defaultItsmSource ?? '',
    pollIntervalSeconds: String(settings?.defaultPollIntervalSeconds ?? 300),
    maxFilesPerCycle: '',
    stableFileAgeSeconds: String(settings?.defaultStableFileAgeSeconds ?? 30),
  } satisfies SourceFormState;
}

function formatTimestamp(value?: string | null) {
  return value ? new Date(value).toLocaleString() : '—';
}

function parsePatterns(value: string) {
  return value
    .split(/[\n,]+/)
    .map((entry) => entry.trim())
    .filter(Boolean);
}

function toNullableNumber(value: string) {
  const trimmed = value.trim();
  if (!trimmed) {
    return null;
  }

  const parsed = Number.parseInt(trimmed, 10);
  return Number.isFinite(parsed) ? parsed : null;
}

function buildStatusChip(source: FileSharePollingSource) {
  if (source.currentJobId) {
    return <Chip size="small" color="warning" label="Queued / running" />;
  }

  if (source.lastError) {
    return <Chip size="small" color="error" label="Needs attention" />;
  }

  if (source.lastSucceededAt) {
    return <Chip size="small" color="success" label="Healthy" />;
  }

  return <Chip size="small" label={source.enabled ? 'Pending first run' : 'Disabled'} variant="outlined" />;
}

export function FileSharePollingSourcesPage() {
  const navigate = useNavigate();
  const [settings, setSettings] = useState<FileSharePollingSettings | null>(null);
  const [sources, setSources] = useState<FileSharePollingSource[]>([]);
  const [itsmSources, setItsmSources] = useState<ItsmSourceSummary[]>([]);
  const [form, setForm] = useState<SourceFormState>(createEmptyForm());
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string>();

  const canManageSources = settings?.allowUserManagedSources ?? true;

  const loadData = async () => {
    setLoading(true);
    setError(undefined);

    try {
      const [loadedSettings, loadedSources, loadedItsmSources] = await Promise.all([
        upmsApi.getFileSharePollingSettings(),
        upmsApi.getFileSharePollingSources(),
        upmsApi.getItsmSources(),
      ]);

      setSettings(loadedSettings);
      setSources(loadedSources);
      setItsmSources(loadedItsmSources);
      setForm((current) =>
        current.id || current.name || current.watchedPath || current.archivePath || current.errorPath
          ? current
          : createEmptyForm(loadedSettings, loadedItsmSources[0]?.name ?? ''),
      );
    } catch (err: any) {
      setError(err.message ?? 'Failed to load file share polling sources.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const resetForm = () => {
    setForm(createEmptyForm(settings, itsmSources[0]?.name ?? ''));
  };

  const formTitle = form.id ? 'Edit polling source' : 'Add polling source';
  const allowedWatchedRootsLabel = useMemo(
    () => (settings?.allowedWatchedRoots?.length ? settings.allowedWatchedRoots.join(', ') : 'Any watched path allowed by the worker account'),
    [settings],
  );
  const allowedArchiveRootsLabel = useMemo(
    () => (settings?.allowedArchiveRoots?.length ? settings.allowedArchiveRoots.join(', ') : 'Any archive path allowed by the worker account'),
    [settings],
  );
  const allowedErrorRootsLabel = useMemo(
    () => (settings?.allowedErrorRoots?.length ? settings.allowedErrorRoots.join(', ') : 'Any quarantine path allowed by the worker account'),
    [settings],
  );

  const saveSource = async (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setError(undefined);

    try {
      const payload = {
        name: form.name.trim(),
        enabled: form.enabled,
        watchedPath: form.watchedPath.trim(),
        filePatterns: parsePatterns(form.filePatternsText),
        archivePath: form.archivePath.trim(),
        errorPath: form.errorPath.trim(),
        itsmSource: form.itsmSource,
        pollIntervalSeconds: toNullableNumber(form.pollIntervalSeconds),
        maxFilesPerCycle: toNullableNumber(form.maxFilesPerCycle),
        stableFileAgeSeconds: toNullableNumber(form.stableFileAgeSeconds),
      };

      if (form.id) {
        await upmsApi.updateFileSharePollingSource(form.id, payload);
      } else {
        await upmsApi.createFileSharePollingSource(payload);
      }

      await loadData();
      resetForm();
    } catch (err: any) {
      setError(err.message ?? 'Failed to save file share polling source.');
    } finally {
      setSaving(false);
    }
  };

  const editSource = (source: FileSharePollingSource) => {
    setForm({
      id: source.id,
      name: source.name,
      enabled: source.enabled,
      watchedPath: source.watchedPath,
      filePatternsText: source.filePatterns.join('\n'),
      archivePath: source.archivePath,
      errorPath: source.errorPath,
      itsmSource: source.itsmSource,
      pollIntervalSeconds: String(source.pollIntervalSeconds),
      maxFilesPerCycle: source.maxFilesPerCycle == null ? '' : String(source.maxFilesPerCycle),
      stableFileAgeSeconds: String(source.stableFileAgeSeconds),
    });
  };

  const runNow = async (source: FileSharePollingSource) => {
    setError(undefined);

    try {
      const queued = await upmsApi.runFileSharePollingSource(source.id);
      navigate(`/jobs?jobId=${encodeURIComponent(queued.job.id)}`);
    } catch (err: any) {
      setError(err.message ?? `Failed to queue poll job for '${source.name}'.`);
    }
  };

  const deleteSource = async (source: FileSharePollingSource) => {
    if (!window.confirm(`Delete polling source '${source.name}'?`)) {
      return;
    }

    setError(undefined);

    try {
      await upmsApi.deleteFileSharePollingSource(source.id);
      await loadData();
      if (form.id === source.id) {
        resetForm();
      }
    } catch (err: any) {
      setError(err.message ?? `Failed to delete polling source '${source.name}'.`);
    }
  };

  if (loading) {
    return <LoadingPanel label="Loading file share polling" />;
  }

  return (
    <>
      <ErrorAlert message={error} onClose={() => setError(undefined)} />
      <PageSection
        title="File share polling"
        description="Let privileged users define watched locations that the worker can scan and then hand off into the existing snapshot-ingest job pipeline."
      >
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} justifyContent="space-between" sx={{ mb: 2 }}>
          <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
            <Chip color={settings?.enabled ? 'success' : 'default'} label={settings?.enabled ? 'Feature enabled' : 'Feature disabled'} />
            <Chip
              variant="outlined"
              label={canManageSources ? 'User-managed sources enabled' : 'User-managed sources disabled'}
            />
          </Stack>
          <Button variant="outlined" onClick={loadData}>
            Refresh sources
          </Button>
        </Stack>

        <Typography color="text.secondary" sx={{ mb: 1 }}>
          Watched roots: {allowedWatchedRootsLabel}
        </Typography>
        <Typography color="text.secondary" sx={{ mb: 1 }}>
          Archive roots: {allowedArchiveRootsLabel}
        </Typography>
        <Typography color="text.secondary" sx={{ mb: 3 }}>
          Error roots: {allowedErrorRootsLabel}
        </Typography>

        <Box component="form" onSubmit={saveSource} sx={{ mb: 3 }}>
          <Typography variant="h6" sx={{ mb: 1 }}>
            {formTitle}
          </Typography>
          <Typography color="text.secondary" sx={{ mb: 2 }}>
            Each source polls one watched location, quarantines problematic files, and submits accepted files into the same worker ingest queue as manual uploads.
          </Typography>
          <Grid container spacing={2}>
            <Grid item xs={12} md={4}>
              <TextField label="Display name" value={form.name} onChange={(e) => setForm((current) => ({ ...current, name: e.target.value }))} fullWidth required />
            </Grid>
            <Grid item xs={12} md={4}>
              <TextField
                select
                label="ITSM source"
                value={form.itsmSource}
                onChange={(e) => setForm((current) => ({ ...current, itsmSource: e.target.value }))}
                SelectProps={{ native: true }}
                InputLabelProps={{ shrink: true }}
                fullWidth
                required
              >
                {itsmSources.map((source) => (
                  <option key={source.name} value={source.name}>
                    {source.displayLabel}
                  </option>
                ))}
              </TextField>
            </Grid>
            <Grid item xs={12} md={4}>
              <FormControlLabel
                control={<Checkbox checked={form.enabled} onChange={(e) => setForm((current) => ({ ...current, enabled: e.target.checked }))} />}
                label="Enabled"
                sx={{ mt: 1 }}
              />
            </Grid>
            <Grid item xs={12} md={4}>
              <TextField label="Watched path" value={form.watchedPath} onChange={(e) => setForm((current) => ({ ...current, watchedPath: e.target.value }))} fullWidth required />
            </Grid>
            <Grid item xs={12} md={4}>
              <TextField label="Archive path" value={form.archivePath} onChange={(e) => setForm((current) => ({ ...current, archivePath: e.target.value }))} fullWidth required />
            </Grid>
            <Grid item xs={12} md={4}>
              <TextField label="Error / quarantine path" value={form.errorPath} onChange={(e) => setForm((current) => ({ ...current, errorPath: e.target.value }))} fullWidth required />
            </Grid>
            <Grid item xs={12} md={4}>
              <TextField
                label="Poll interval seconds"
                type="number"
                value={form.pollIntervalSeconds}
                onChange={(e) => setForm((current) => ({ ...current, pollIntervalSeconds: e.target.value }))}
                inputProps={{ min: settings?.minPollIntervalSeconds ?? 1, max: settings?.maxPollIntervalSeconds ?? undefined }}
                fullWidth
              />
            </Grid>
            <Grid item xs={12} md={4}>
              <TextField
                label="Stable file age seconds"
                type="number"
                value={form.stableFileAgeSeconds}
                onChange={(e) => setForm((current) => ({ ...current, stableFileAgeSeconds: e.target.value }))}
                inputProps={{ min: 0 }}
                fullWidth
              />
            </Grid>
            <Grid item xs={12} md={4}>
              <TextField
                label="Max files per cycle"
                type="number"
                value={form.maxFilesPerCycle}
                onChange={(e) => setForm((current) => ({ ...current, maxFilesPerCycle: e.target.value }))}
                inputProps={{ min: 1, max: settings?.maxFilesPerCycleCap ?? undefined }}
                fullWidth
              />
            </Grid>
            <Grid item xs={12}>
              <TextField
                label="File patterns"
                value={form.filePatternsText}
                onChange={(e) => setForm((current) => ({ ...current, filePatternsText: e.target.value }))}
                helperText="One glob pattern per line or comma-separated, for example *.csv or *.json."
                multiline
                minRows={3}
                fullWidth
              />
            </Grid>
          </Grid>

          <Stack direction="row" spacing={1} sx={{ mt: 2 }}>
            <Button type="submit" variant="contained" disabled={!canManageSources || saving || !settings?.enabled}>
              {form.id ? 'Save changes' : 'Add polling source'}
            </Button>
            <Button type="button" variant="text" onClick={resetForm} disabled={saving}>
              Clear form
            </Button>
          </Stack>
        </Box>

        <UpmsDataTable
          columns={[
            { key: 'name', label: 'Source' },
            { key: 'status', label: 'Status', sortable: false },
            { key: 'itsmSource', label: 'ITSM Source' },
            { key: 'watchedPath', label: 'Watched path' },
            { key: 'nextPollDueAt', label: 'Next due', getSortValue: (row) => String(row.nextPollDueAtSort ?? '') },
            { key: 'lastRun', label: 'Last completed', getSortValue: (row) => String(row.lastRunSort ?? '') },
            { key: 'currentJob', label: 'Current job', sortable: false },
            { key: 'actions', label: 'Actions', sortable: false },
          ]}
          rows={sources.map((source) => ({
            id: source.id,
            name: (
              <Stack spacing={0.5}>
                <Typography fontWeight={600}>{source.name}</Typography>
                <Typography variant="body2" color="text.secondary">
                  {source.filePatterns.join(', ')}
                </Typography>
                {source.lastError ? (
                  <Typography variant="body2" color="error.main">
                    {source.lastError}
                  </Typography>
                ) : null}
              </Stack>
            ),
            status: buildStatusChip(source),
            itsmSource: source.itsmSource,
            watchedPath: source.watchedPath,
            nextPollDueAt: formatTimestamp(source.nextPollDueAt),
            nextPollDueAtSort: source.nextPollDueAt ?? '',
            lastRun: formatTimestamp(source.lastRunCompletedAt),
            lastRunSort: source.lastRunCompletedAt ?? '',
            currentJob: source.currentJobId ? (
              <MuiLink component="button" type="button" onClick={() => navigate(`/jobs?jobId=${encodeURIComponent(source.currentJobId!)}`)}>
                {source.currentJobId.slice(0, 8)}
              </MuiLink>
            ) : source.lastJobId ? (
              <MuiLink component="button" type="button" onClick={() => navigate(`/jobs?jobId=${encodeURIComponent(source.lastJobId!)}`)}>
                {source.lastJobId.slice(0, 8)}
              </MuiLink>
            ) : (
              '—'
            ),
            actions: (
              <Stack direction={{ xs: 'column', md: 'row' }} spacing={1}>
                <Button size="small" variant="text" onClick={() => editSource(source)} disabled={!canManageSources}>
                  Edit
                </Button>
                <Button size="small" variant="text" onClick={() => runNow(source)} disabled={!settings?.enabled}>
                  Run now
                </Button>
                <Button size="small" color="error" variant="text" onClick={() => deleteSource(source)} disabled={!canManageSources}>
                  Delete
                </Button>
              </Stack>
            ),
          }))}
        />
      </PageSection>
    </>
  );
}

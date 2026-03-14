import { ChangeEvent, FormEvent, useEffect, useMemo, useState } from 'react';
import { Box, Button, Chip, Grid, Stack, TextField, Typography } from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { upmsApi } from '../api/client';
import { ItsmSourceSummary } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { LoadingPanel } from '../components/LoadingPanel';
import { PageSection } from '../components/PageSection';
import { UpmsDataTable } from '../components/UpmsDataTable';

type SnapshotDateSource = 'default' | 'filename' | 'manual';

type SnapshotUploadSelection = {
  id: string;
  file: File;
  snapshotDate: string;
  dateSource: SnapshotDateSource;
};

function buildSelectionId(file: File) {
  return `${file.name}:${file.size}:${file.lastModified}`;
}

function isValidCalendarDate(year: number, month: number, day: number) {
  const candidate = new Date(Date.UTC(year, month - 1, day));
  return candidate.getUTCFullYear() === year && candidate.getUTCMonth() === month - 1 && candidate.getUTCDate() === day;
}

function toIsoDate(yearText: string, monthText: string, dayText: string) {
  const year = Number.parseInt(yearText, 10);
  const month = Number.parseInt(monthText, 10);
  const day = Number.parseInt(dayText, 10);
  if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day)) {
    return null;
  }

  return isValidCalendarDate(year, month, day)
    ? `${yearText.padStart(4, '0')}-${monthText.padStart(2, '0')}-${dayText.padStart(2, '0')}`
    : null;
}

function inferSnapshotDateFromFileName(fileName: string) {
  const separatedMatch = fileName.match(/((?:19|20)\d{2})[-_.](\d{2})[-_.](\d{2})/);
  if (separatedMatch) {
    const [, year, month, day] = separatedMatch;
    return toIsoDate(year, month, day);
  }

  const compactMatch = fileName.match(/((?:19|20)\d{2})(\d{2})(\d{2})/);
  if (compactMatch) {
    const [, year, month, day] = compactMatch;
    return toIsoDate(year, month, day);
  }

  return null;
}

function formatFileSize(size: number) {
  if (size < 1024) {
    return `${size} B`;
  }

  if (size < 1024 * 1024) {
    return `${(size / 1024).toFixed(1)} KB`;
  }

  return `${(size / (1024 * 1024)).toFixed(1)} MB`;
}

function createSelection(file: File, defaultSnapshotDate: string): SnapshotUploadSelection {
  const inferredDate = inferSnapshotDateFromFileName(file.name);
  return {
    id: buildSelectionId(file),
    file,
    snapshotDate: inferredDate ?? defaultSnapshotDate,
    dateSource: inferredDate ? 'filename' : 'default',
  };
}

export function UploadPage() {
  const navigate = useNavigate();
  const [sources, setSources] = useState<ItsmSourceSummary[]>([]);
  const [itsmSource, setItsmSource] = useState('');
  const [snapshotDate, setSnapshotDate] = useState(new Date().toISOString().slice(0, 10));
  const [files, setFiles] = useState<SnapshotUploadSelection[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string>();

  useEffect(() => {
    upmsApi
      .getItsmSources()
      .then((loaded) => {
        setSources(loaded);
        if (loaded.length > 0) {
          setItsmSource(loaded[0].name);
        }
      })
      .catch((err) => setError(err.message ?? 'Failed to load ITSM sources.'))
      .finally(() => setLoading(false));
  }, []);

  const bulkMode = files.length > 1;
  const submitLabel = useMemo(() => {
    if (files.length <= 1) {
      return 'Queue ingest job';
    }

    return `Queue ${files.length} ingest jobs`;
  }, [files.length]);

  const addFiles = (event: ChangeEvent<HTMLInputElement>) => {
    const selectedFiles = Array.from(event.target.files ?? []);
    if (selectedFiles.length === 0) {
      return;
    }

    setFiles((current) => {
      const existingIds = new Set(current.map((entry) => entry.id));
      const next = [...current];

      for (const file of selectedFiles) {
        const selection = createSelection(file, snapshotDate);
        if (existingIds.has(selection.id)) {
          continue;
        }

        next.push(selection);
        existingIds.add(selection.id);
      }

      return next;
    });

    event.target.value = '';
  };

  const updateFileDate = (id: string, value: string) => {
    setFiles((current) =>
      current.map((entry) =>
        entry.id === id
          ? {
              ...entry,
              snapshotDate: value,
              dateSource: 'manual',
            }
          : entry,
      ),
    );
  };

  const applyDefaultDateToAll = () => {
    setFiles((current) => current.map((entry) => ({ ...entry, snapshotDate, dateSource: 'default' })));
  };

  const removeFile = (id: string) => {
    setFiles((current) => current.filter((entry) => entry.id !== id));
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (files.length === 0 || !itsmSource) {
      return;
    }

    setSubmitting(true);
    setError(undefined);

    try {
      if (files.length === 1) {
        const formData = new FormData();
        formData.append('itsmSource', itsmSource);
        formData.append('snapshotDate', files[0].snapshotDate);
        formData.append('file', files[0].file);

        const job = await upmsApi.queueSnapshotIngest(formData);
        navigate(`/jobs?jobId=${encodeURIComponent(job.id)}`);
        return;
      }

      const formData = new FormData();
      formData.append('itsmSource', itsmSource);
      files.forEach((entry) => {
        formData.append('snapshotDates', entry.snapshotDate);
        formData.append('files', entry.file);
      });

      const submission = await upmsApi.queueBulkSnapshotIngest(formData);
      const jobIds = submission.jobs.map((job) => job.id).join(',');
      navigate(`/jobs?jobIds=${encodeURIComponent(jobIds)}`);
    } catch (err: any) {
      setError(err.message ?? 'Failed to queue snapshot ingest.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <LoadingPanel label="Loading upload form" />;

  return (
    <>
      <ErrorAlert message={error} onClose={() => setError(undefined)} />
      <PageSection
        title="Upload snapshots"
        description="Queue one or many CSV or JSON snapshots for worker-based ingestion. Every selected file uses the same ITSM source, and you can edit the date for each file before queueing."
      >
        <Box component="form" onSubmit={submit}>
          <Grid container spacing={2} sx={{ mb: 2 }}>
            <Grid item xs={12} md={4}>
              <TextField
                select
                SelectProps={{ native: true }}
                InputLabelProps={{ shrink: true }}
                label="ITSM Source"
                value={itsmSource}
                onChange={(e) => setItsmSource(e.target.value)}
                fullWidth
              >
                {sources.map((source) => (
                  <option key={source.name} value={source.name}>
                    {source.displayLabel}
                  </option>
                ))}
              </TextField>
            </Grid>
            <Grid item xs={12} md={4}>
              <TextField
                label="Default snapshot date"
                type="date"
                value={snapshotDate}
                onChange={(e) => setSnapshotDate(e.target.value)}
                fullWidth
                InputLabelProps={{ shrink: true }}
              />
            </Grid>
            <Grid item xs={12} md={4}>
              <Button variant="outlined" component="label" type="button" fullWidth sx={{ height: '100%' }}>
                Add files
                <input hidden type="file" accept=".csv,.json" multiple onChange={addFiles} />
              </Button>
            </Grid>
          </Grid>

          <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} justifyContent="space-between" sx={{ mb: 2 }}>
            <Typography>
              {files.length === 0
                ? 'Choose one or more CSV or JSON snapshot exports.'
                : `${files.length} file${files.length === 1 ? '' : 's'} selected${bulkMode ? ' for bulk upload' : ''}.`}
            </Typography>
            <Stack direction="row" spacing={1}>
              <Button variant="text" type="button" onClick={applyDefaultDateToAll} disabled={files.length === 0}>
                Apply date to all
              </Button>
              <Button variant="text" type="button" onClick={() => setFiles([])} disabled={files.length === 0}>
                Clear selection
              </Button>
            </Stack>
          </Stack>

          {files.length > 0 ? (
            <UpmsDataTable
              columns={[
                { key: 'fileName', label: 'File', sortable: false },
                { key: 'dateSource', label: 'Date Source', sortable: false },
                { key: 'snapshotDate', label: 'Snapshot Date', sortable: false },
                { key: 'actions', label: 'Actions', sortable: false },
              ]}
              rows={files.map((entry) => ({
                id: entry.id,
                fileName: (
                  <Box>
                    <Typography variant="body2">{entry.file.name}</Typography>
                    <Typography variant="caption" color="text.secondary">
                      {formatFileSize(entry.file.size)}
                    </Typography>
                  </Box>
                ),
                dateSource: (
                  <Chip
                    label={entry.dateSource === 'filename' ? 'From file name' : entry.dateSource === 'manual' ? 'Manual' : 'Default'}
                    size="small"
                  />
                ),
                snapshotDate: (
                  <TextField
                    type="date"
                    size="small"
                    value={entry.snapshotDate}
                    onChange={(e) => updateFileDate(entry.id, e.target.value)}
                    InputLabelProps={{ shrink: true }}
                  />
                ),
                actions: (
                  <Button variant="text" type="button" onClick={() => removeFile(entry.id)}>
                    Remove
                  </Button>
                ),
              }))}
            />
          ) : null}

          <Typography variant="body2" color="text.secondary" sx={{ mt: 2, mb: 2 }}>
            Snapshot dates are auto-filled from filenames when they include YYYY-MM-DD, YYYY_MM_DD, or YYYYMMDD. Otherwise the default date is used.
          </Typography>

          <Button variant="contained" type="submit" disabled={!itsmSource || files.length === 0 || files.some((entry) => !entry.snapshotDate) || submitting}>
            {submitting ? 'Queueing...' : submitLabel}
          </Button>
        </Box>
      </PageSection>
    </>
  );
}

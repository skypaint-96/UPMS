import { FormEvent, useEffect, useState } from 'react';
import { Box, Button, Grid, TextField, Typography } from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { upmsApi } from '../api/client';
import { ItsmSourceSummary } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { LoadingPanel } from '../components/LoadingPanel';
import { PageSection } from '../components/PageSection';

export function UploadPage() {
  const navigate = useNavigate();
  const [sources, setSources] = useState<ItsmSourceSummary[]>([]);
  const [itsmSource, setItsmSource] = useState('');
  const [snapshotDate, setSnapshotDate] = useState(new Date().toISOString().slice(0, 10));
  const [file, setFile] = useState<File | null>(null);
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

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!file || !itsmSource) {
      return;
    }

    setSubmitting(true);
    setError(undefined);

    const formData = new FormData();
    formData.append('itsmSource', itsmSource);
    formData.append('snapshotDate', snapshotDate);
    formData.append('file', file);

    try {
      const job = await upmsApi.queueSnapshotIngest(formData);
      navigate(`/jobs?jobId=${encodeURIComponent(job.id)}`);
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
      <PageSection title="Upload snapshot" description="Queue a CSV or JSON snapshot for worker-based ingestion.">
        <Box component="form" onSubmit={submit}>
          <Grid container spacing={2} sx={{ mb: 2 }}>
            <Grid item xs={12} md={4}>
              <TextField select SelectProps={{ native: true }} label="ITSM Source" value={itsmSource} onChange={(e) => setItsmSource(e.target.value)} fullWidth>
                {sources.map((source) => (
                  <option key={source.name} value={source.name}>
                    {source.displayLabel}
                  </option>
                ))}
              </TextField>
            </Grid>
            <Grid item xs={12} md={4}>
              <TextField label="Snapshot date" type="date" value={snapshotDate} onChange={(e) => setSnapshotDate(e.target.value)} fullWidth InputLabelProps={{ shrink: true }} />
            </Grid>
            <Grid item xs={12} md={4}>
              <Button variant="outlined" component="label" fullWidth sx={{ height: '100%' }}>
                Select file
                <input hidden type="file" accept=".csv,.json" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
              </Button>
            </Grid>
          </Grid>
          <Typography sx={{ mb: 2 }}>{file ? `Selected file: ${file.name}` : 'Choose a CSV or JSON snapshot export.'}</Typography>
          <Button variant="contained" type="submit" disabled={!itsmSource || !file || submitting}>
            Queue ingest job
          </Button>
        </Box>
      </PageSection>
    </>
  );
}

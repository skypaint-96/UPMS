import { useEffect, useMemo, useState } from 'react';
import { Box, Button, Chip, Link as MuiLink, Stack, Typography } from '@mui/material';
import { useLocation } from 'react-router-dom';
import { upmsApi } from '../api/client';
import { BackgroundJob } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { LoadingPanel } from '../components/LoadingPanel';
import { PageSection } from '../components/PageSection';
import { UpmsDataTable } from '../components/UpmsDataTable';

export function JobsPage() {
  const location = useLocation();
  const query = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const highlightedJobId = query.get('jobId');
  const trackedJobIds = useMemo(() => {
    const ids = (query.get('jobIds') ?? '')
      .split(',')
      .map((value) => value.trim())
      .filter(Boolean);

    if (highlightedJobId && !ids.includes(highlightedJobId)) {
      ids.unshift(highlightedJobId);
    }

    return ids;
  }, [highlightedJobId, query]);

  const [jobs, setJobs] = useState<BackgroundJob[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();

  const loadJobs = () => {
    setLoading(true);
    upmsApi
      .getJobs()
      .then(setJobs)
      .catch((err) => setError(err.message ?? 'Failed to load jobs.'))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadJobs();
    const intervalId = window.setInterval(loadJobs, 5000);
    return () => window.clearInterval(intervalId);
  }, []);

  const trackedJobs = trackedJobIds
    .map((id) => jobs.find((job) => job.id === id) ?? null)
    .filter((job): job is BackgroundJob => Boolean(job));
  const selectedJob = jobs.find((job) => job.id === highlightedJobId) ?? null;
  let parsedResult: any = null;
  try {
    parsedResult = selectedJob?.resultJson ? JSON.parse(selectedJob.resultJson) : null;
  } catch {
    parsedResult = null;
  }

  return (
    <>
      <ErrorAlert message={error} onClose={() => setError(undefined)} />
      <PageSection title="Background jobs" description="Monitor worker-processed ingest and report execution jobs.">
        {trackedJobIds.length > 0 ? (
          <Box sx={{ mb: 2, p: 2, border: 1, borderColor: 'divider', borderRadius: 2 }}>
            <Typography variant="subtitle1" sx={{ mb: 0.5 }}>
              Latest submitted jobs
            </Typography>
            <Typography color="text.secondary" sx={{ mb: 1.5 }}>
              Tracking {trackedJobs.length} of {trackedJobIds.length} job{trackedJobIds.length === 1 ? '' : 's'} from your latest snapshot upload.
            </Typography>
            <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
              {trackedJobs.map((job) => (
                <Chip key={job.id} label={`${job.id.slice(0, 8)} • ${job.status}`} size="small" />
              ))}
              {trackedJobs.length < trackedJobIds.length ? (
                <Chip label={`${trackedJobIds.length - trackedJobs.length} job${trackedJobIds.length - trackedJobs.length === 1 ? '' : 's'} not visible yet`} size="small" variant="outlined" />
              ) : null}
            </Stack>
          </Box>
        ) : null}
        <Box sx={{ mb: 2 }}>
          <Button variant="outlined" onClick={loadJobs}>
            Refresh jobs
          </Button>
        </Box>
        {loading ? (
          <LoadingPanel label="Loading jobs" />
        ) : (
          <UpmsDataTable
            columns={[
              { key: 'jobType', label: 'Job Type' },
              { key: 'status', label: 'Status' },
              { key: 'createdAt', label: 'Created', getSortValue: (row) => String(row.createdAtSortValue ?? '') },
              { key: 'download', label: 'Output', sortable: false },
            ]}
            rows={jobs.map((job) => ({
              id: job.id,
              jobType: job.jobType,
              status: job.status,
              createdAt: new Date(job.createdAt).toLocaleString(),
              createdAtSortValue: job.createdAt,
              download: job.downloadUrl ? (
                <MuiLink href={job.downloadUrl}>Download {job.outputFileName ?? 'artifact'}</MuiLink>
              ) : (
                job.outputFileName ?? ''
              ),
            }))}
          />
        )}
        {selectedJob && parsedResult?.htmlContent ? (
          <Box sx={{ mt: 3 }}>
            <Typography variant="h6" sx={{ mb: 1 }}>
              Job preview
            </Typography>
            <div dangerouslySetInnerHTML={{ __html: parsedResult.htmlContent }} />
          </Box>
        ) : null}
      </PageSection>
    </>
  );
}

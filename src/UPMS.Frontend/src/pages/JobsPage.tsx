import { useEffect, useMemo, useState } from 'react';
import { Box, Button, Link as MuiLink, Typography } from '@mui/material';
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

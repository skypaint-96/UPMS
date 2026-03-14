import { useEffect, useMemo, useState } from 'react';
import { Alert, Box, Button, Chip, Link as MuiLink, Stack, Typography } from '@mui/material';
import { Link as RouterLink, useLocation } from 'react-router-dom';
import { upmsApi } from '../api/client';
import { BackgroundJob, ReportDelivery } from '../api/types';
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
  const [deliveries, setDeliveries] = useState<ReportDelivery[]>([]);
  const [loading, setLoading] = useState(true);
  const [deliveriesLoading, setDeliveriesLoading] = useState(false);
  const [retryingDeliveryId, setRetryingDeliveryId] = useState<string | null>(null);
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

  useEffect(() => {
    if (!selectedJob || selectedJob.jobType !== 'report-execution') {
      setDeliveries([]);
      return;
    }

    setDeliveriesLoading(true);
    upmsApi
      .getReportDeliveries({ reportJobId: selectedJob.id, take: 100 })
      .then(setDeliveries)
      .catch((err) => setError(err.message ?? 'Failed to load report deliveries.'))
      .finally(() => setDeliveriesLoading(false));
  }, [selectedJob?.id, selectedJob?.jobType]);

  let parsedResult: any = null;
  try {
    parsedResult = selectedJob?.resultJson ? JSON.parse(selectedJob.resultJson) : null;
  } catch {
    parsedResult = null;
  }

  const retryDelivery = async (deliveryId: string) => {
    setRetryingDeliveryId(deliveryId);
    setError(undefined);
    try {
      await upmsApi.retryReportDelivery(deliveryId);
      if (selectedJob) {
        const rows = await upmsApi.getReportDeliveries({ reportJobId: selectedJob.id, take: 100 });
        setDeliveries(rows);
      }
      loadJobs();
    } catch (err: any) {
      setError(err.message ?? 'Failed to retry report delivery.');
    } finally {
      setRetryingDeliveryId(null);
    }
  };

  return (
    <>
      <ErrorAlert message={error} onClose={() => setError(undefined)} />
      <PageSection title="Background jobs" description="Monitor worker-processed ingest, report execution, and report delivery jobs.">
        {trackedJobIds.length > 0 ? (
          <Box sx={{ mb: 2, p: 2, border: 1, borderColor: 'divider', borderRadius: 2 }}>
            <Typography variant="subtitle1" sx={{ mb: 0.5 }}>
              Latest submitted jobs
            </Typography>
            <Typography color="text.secondary" sx={{ mb: 1.5 }}>
              Tracking {trackedJobs.length} of {trackedJobIds.length} job{trackedJobIds.length === 1 ? '' : 's'} from your latest activity.
            </Typography>
            <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
              {trackedJobs.map((job) => (
                <Chip key={job.id} label={`${job.id.slice(0, 8)} • ${job.status}`} size="small" />
              ))}
              {trackedJobs.length < trackedJobIds.length ? (
                <Chip
                  label={`${trackedJobIds.length - trackedJobs.length} job${trackedJobIds.length - trackedJobs.length === 1 ? '' : 's'} not visible yet`}
                  size="small"
                  variant="outlined"
                />
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

        {selectedJob ? (
          <Box sx={{ mt: 3 }}>
            <Typography variant="h6" sx={{ mb: 1 }}>
              Selected job
            </Typography>
            <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap" sx={{ mb: 1 }}>
              <Chip label={selectedJob.jobType} size="small" />
              <Chip
                label={selectedJob.status}
                size="small"
                color={selectedJob.status.toLowerCase() === 'succeeded' ? 'success' : selectedJob.status.toLowerCase() === 'failed' ? 'error' : 'default'}
              />
              {selectedJob.outputFileName ? <Chip label={selectedJob.outputFileName} size="small" variant="outlined" /> : null}
            </Stack>
            <Typography color="text.secondary">
              Created {new Date(selectedJob.createdAt).toLocaleString()}
              {selectedJob.requestedBy ? ` by ${selectedJob.requestedBy}` : ''}.
            </Typography>
          </Box>
        ) : null}

        {selectedJob?.jobType === 'report-execution' ? (
          <Box sx={{ mt: 3 }}>
            <Typography variant="h6" sx={{ mb: 1 }}>
              Delivery activity
            </Typography>
            <Typography color="text.secondary" sx={{ mb: 2 }}>
              Each row below is a separate outbound delivery record created after the report artifact was rendered. Failed deliveries can be retried without re-rendering the report.
            </Typography>
            {deliveriesLoading ? (
              <LoadingPanel label="Loading delivery records" />
            ) : deliveries.length === 0 ? (
              <Alert severity="info">
                No delivery records were found for this report job. That usually means the job was queued without any distribution lists.
              </Alert>
            ) : (
              <UpmsDataTable
                columns={[
                  { key: 'distributionListName', label: 'Distribution list' },
                  { key: 'status', label: 'Status' },
                  { key: 'recipientCount', label: 'Recipients', type: 'number' },
                  { key: 'attemptCount', label: 'Attempts', type: 'number' },
                  { key: 'lastErrorMessage', label: 'Last error', sortable: false },
                  { key: 'actions', label: 'Actions', sortable: false },
                ]}
                rows={deliveries.map((delivery) => ({
                  id: delivery.id,
                  distributionListName: delivery.distributionListName,
                  status: delivery.status,
                  recipientCount: delivery.recipientCount,
                  attemptCount: delivery.attemptCount,
                  lastErrorMessage: delivery.lastErrorMessage ?? '—',
                  actions:
                    delivery.status.toLowerCase() === 'failed' ? (
                      <Button
                        size="small"
                        variant="outlined"
                        disabled={retryingDeliveryId === delivery.id}
                        onClick={() => void retryDelivery(delivery.id)}
                      >
                        Retry
                      </Button>
                    ) : (
                      '—'
                    ),
                }))}
              />
            )}
            <Typography color="text.secondary" sx={{ mt: 1.5 }}>
              Need to edit recipients? Open the <MuiLink component={RouterLink} to="/distribution-lists">Distribution Lists</MuiLink> page.
            </Typography>
          </Box>
        ) : null}

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

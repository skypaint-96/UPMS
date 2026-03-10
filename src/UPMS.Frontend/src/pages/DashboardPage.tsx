import { useEffect, useMemo, useState } from 'react';
import { Grid, Paper, Typography } from '@mui/material';
import { upmsApi } from '../api/client';
import { Snapshot, ItsmSourceSummary } from '../api/types';
import { LoadingPanel } from '../components/LoadingPanel';
import { ErrorAlert } from '../components/ErrorAlert';

export function DashboardPage() {
  const [snapshots, setSnapshots] = useState<Snapshot[]>([]);
  const [sources, setSources] = useState<ItsmSourceSummary[]>([]);
  const [error, setError] = useState<string>();
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([upmsApi.getSnapshots(), upmsApi.getItsmSources()])
      .then(([loadedSnapshots, loadedSources]) => {
        setSnapshots(loadedSnapshots);
        setSources(loadedSources);
      })
      .catch((err) => setError(err.message ?? 'Failed to load dashboard data.'))
      .finally(() => setLoading(false));
  }, []);

  const cards = useMemo(
    () => [
      { label: 'ITSM sources', value: sources.length },
      { label: 'Snapshots', value: snapshots.length },
      { label: 'Latest snapshot', value: snapshots[0] ? new Date(snapshots[0].snapshotDate).toLocaleString() : 'None' },
    ],
    [snapshots, sources.length],
  );

  if (loading) return <LoadingPanel label="Loading dashboard" />;

  return (
    <>
      <ErrorAlert message={error} onClose={() => setError(undefined)} />
      <Typography variant="h4" sx={{ mb: 3 }}>
        Unified Problem Management System
      </Typography>
      <Grid container spacing={2} sx={{ mb: 3 }}>
        {cards.map((card) => (
          <Grid item xs={12} md={4} key={card.label}>
            <Paper sx={{ p: 3 }}>
              <Typography variant="overline">{card.label}</Typography>
              <Typography variant="h5">{card.value}</Typography>
            </Paper>
          </Grid>
        ))}
      </Grid>
      <Paper sx={{ p: 3 }}>
        <Typography variant="h6" sx={{ mb: 2 }}>
          Recent snapshots
        </Typography>
        {snapshots.slice(0, 5).map((snapshot) => (
          <Typography key={snapshot.id} sx={{ mb: 1 }}>
            {snapshot.itsmSource} — {new Date(snapshot.snapshotDate).toLocaleString()} — uploaded by {snapshot.uploadedBy}
          </Typography>
        ))}
      </Paper>
    </>
  );
}

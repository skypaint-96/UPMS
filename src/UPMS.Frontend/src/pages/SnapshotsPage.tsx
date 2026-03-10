import { useEffect, useState } from 'react';
import { Box, TextField } from '@mui/material';
import { upmsApi } from '../api/client';
import { Snapshot } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { LoadingPanel } from '../components/LoadingPanel';
import { PageSection } from '../components/PageSection';
import { UpmsDataTable } from '../components/UpmsDataTable';

export function SnapshotsPage() {
  const [snapshots, setSnapshots] = useState<Snapshot[]>([]);
  const [itsmSource, setItsmSource] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();

  useEffect(() => {
    setLoading(true);
    upmsApi
      .getSnapshots(itsmSource ? { itsmSource } : undefined)
      .then(setSnapshots)
      .catch((err) => setError(err.message ?? 'Failed to load snapshots.'))
      .finally(() => setLoading(false));
  }, [itsmSource]);

  return (
    <>
      <ErrorAlert message={error} onClose={() => setError(undefined)} />
      <PageSection title="Snapshots" description="Browse uploaded point-in-time data sets by ITSM source.">
        <Box sx={{ mb: 2 }}>
          <TextField label="ITSM source filter" value={itsmSource} onChange={(e) => setItsmSource(e.target.value)} />
        </Box>
        {loading ? (
          <LoadingPanel label="Loading snapshots" />
        ) : (
          <UpmsDataTable
            dataTestId="snapshots-table"
            columns={[
              { key: 'itsmSource', label: 'ITSM Source' },
              { key: 'snapshotDate', label: 'Snapshot Date' },
              { key: 'uploadedBy', label: 'Uploaded By' },
            ]}
            rows={snapshots.map((snapshot) => ({
              id: snapshot.id,
              itsmSource: snapshot.itsmSource,
              snapshotDate: new Date(snapshot.snapshotDate).toLocaleString(),
              uploadedBy: snapshot.uploadedBy,
            }))}
          />
        )}
      </PageSection>
    </>
  );
}

import { useEffect, useMemo, useState } from 'react';
import { Autocomplete, Box, TextField } from '@mui/material';
import { upmsApi } from '../api/client';
import { ItsmSourceSummary, Snapshot } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { LoadingPanel } from '../components/LoadingPanel';
import { PageSection } from '../components/PageSection';
import { UpmsDataTable } from '../components/UpmsDataTable';
import { getUniqueSortedStrings } from '../utils/ticketFields';

export function SnapshotsPage() {
  const [snapshots, setSnapshots] = useState<Snapshot[]>([]);
  const [sources, setSources] = useState<ItsmSourceSummary[]>([]);
  const [itsmSource, setItsmSource] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();

  useEffect(() => {
    upmsApi.getItsmSources().then(setSources).catch(() => undefined);
  }, []);

  useEffect(() => {
    setLoading(true);
    upmsApi
      .getSnapshots(itsmSource ? { itsmSource } : undefined)
      .then(setSnapshots)
      .catch((err) => setError(err.message ?? 'Failed to load snapshots.'))
      .finally(() => setLoading(false));
  }, [itsmSource]);

  const sourceSuggestions = useMemo(() => getUniqueSortedStrings(sources.map((source) => source.name)), [sources]);

  return (
    <>
      <ErrorAlert message={error} onClose={() => setError(undefined)} />
      <PageSection title="Snapshots" description="Browse uploaded point-in-time data sets by ITSM source.">
        <Box sx={{ mb: 2, maxWidth: 420 }}>
          <Autocomplete
            freeSolo
            options={sourceSuggestions}
            inputValue={itsmSource}
            onInputChange={(_, newValue) => setItsmSource(newValue)}
            onChange={(_, newValue) => setItsmSource(typeof newValue === 'string' ? newValue : newValue ?? '')}
            renderInput={(params) => (
              <TextField
                {...params}
                label="ITSM source filter"
                helperText={sourceSuggestions.length > 0 ? 'Configured sources are suggested as you type.' : 'Suggestions appear once sources have loaded.'}
              />
            )}
          />
        </Box>
        {loading ? (
          <LoadingPanel label="Loading snapshots" />
        ) : (
          <UpmsDataTable
            dataTestId="snapshots-table"
            columns={[
              { key: 'itsmSource', label: 'ITSM Source' },
              { key: 'snapshotDate', label: 'Snapshot Date', getSortValue: (row) => String(row.snapshotDateSortValue ?? '') },
              { key: 'uploadedBy', label: 'Uploaded By' },
            ]}
            rows={snapshots.map((snapshot) => ({
              id: snapshot.id,
              itsmSource: snapshot.itsmSource,
              snapshotDate: new Date(snapshot.snapshotDate).toLocaleString(),
              snapshotDateSortValue: snapshot.snapshotDate,
              uploadedBy: snapshot.uploadedBy,
            }))}
          />
        )}
      </PageSection>
    </>
  );
}

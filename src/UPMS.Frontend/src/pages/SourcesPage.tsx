import { useEffect, useState } from 'react';
import { Box, TextField } from '@mui/material';
import { upmsApi } from '../api/client';
import { ItsmSourceDefinition, ItsmSourceSummary } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { LoadingPanel } from '../components/LoadingPanel';
import { PageSection } from '../components/PageSection';
import { UpmsDataTable } from '../components/UpmsDataTable';

export function SourcesPage() {
  const [sources, setSources] = useState<ItsmSourceSummary[]>([]);
  const [selectedSource, setSelectedSource] = useState('');
  const [definition, setDefinition] = useState<ItsmSourceDefinition | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();

  useEffect(() => {
    upmsApi
      .getItsmSources()
      .then((loaded) => {
        setSources(loaded);
        if (loaded.length > 0) {
          setSelectedSource(loaded[0].name);
        }
      })
      .catch((err) => setError(err.message ?? 'Failed to load ITSM sources.'))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (!selectedSource) {
      setDefinition(null);
      return;
    }

    upmsApi.getItsmSource(selectedSource).then(setDefinition).catch((err) => setError(err.message ?? 'Failed to load source detail.'));
  }, [selectedSource]);

  if (loading) return <LoadingPanel label="Loading ITSM sources" />;

  return (
    <>
      <ErrorAlert message={error} onClose={() => setError(undefined)} />
      <PageSection title="ITSM sources" description="View current source definitions and canonical field mappings.">
        <Box sx={{ mb: 3 }}>
          <TextField select SelectProps={{ native: true }} label="Source" value={selectedSource} onChange={(e) => setSelectedSource(e.target.value)}>
            {sources.map((source) => (
              <option key={source.name} value={source.name}>
                {source.displayLabel}
              </option>
            ))}
          </TextField>
        </Box>
        {definition ? (
          <UpmsDataTable
            columns={[
              { key: 'sourceFieldName', label: 'Source Field' },
              { key: 'canonicalFieldName', label: 'Canonical Field' },
              { key: 'required', label: 'Required' },
            ]}
            rows={definition.mappings.map((mapping) => ({
              id: `${mapping.itsmSource}:${mapping.sourceFieldName}`,
              sourceFieldName: mapping.sourceFieldName,
              canonicalFieldName: mapping.canonicalFieldName,
              required: mapping.isRequired ? 'Yes' : 'No',
            }))}
          />
        ) : null}
      </PageSection>
    </>
  );
}

import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  Box,
  Button,
  Checkbox,
  FormControlLabel,
  Grid,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { upmsApi } from '../api/client';
import { CanonicalField, ItsmSourceDefinition, ItsmSourceSummary } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { LoadingPanel } from '../components/LoadingPanel';
import { PageSection } from '../components/PageSection';
import { UpmsDataTable } from '../components/UpmsDataTable';
import { parseItsmSourceImport } from '../utils/itsmSourceImport';

export function SourcesPage() {
  const [sources, setSources] = useState<ItsmSourceSummary[]>([]);
  const [canonicalFields, setCanonicalFields] = useState<CanonicalField[]>([]);
  const [selectedSource, setSelectedSource] = useState('');
  const [definition, setDefinition] = useState<ItsmSourceDefinition | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [message, setMessage] = useState<string>();
  const [newSourceName, setNewSourceName] = useState('');
  const [newSourceLabel, setNewSourceLabel] = useState('');
  const [mappingFieldName, setMappingFieldName] = useState('');
  const [mappingCanonicalField, setMappingCanonicalField] = useState('');
  const [mappingRequired, setMappingRequired] = useState(false);
  const [importName, setImportName] = useState('');
  const [importLabel, setImportLabel] = useState('');
  const [importFile, setImportFile] = useState<File | null>(null);

  const loadPage = async (preferredSource?: string) => {
    setLoading(true);
    try {
      const [loadedSources, loadedCanonicalFields] = await Promise.all([upmsApi.getItsmSources(), upmsApi.getCanonicalFields()]);
      setSources(loadedSources);
      setCanonicalFields(loadedCanonicalFields);

      const resolvedSource = preferredSource && loadedSources.some((source) => source.name === preferredSource)
        ? preferredSource
        : selectedSource && loadedSources.some((source) => source.name === selectedSource)
          ? selectedSource
          : loadedSources[0]?.name ?? '';

      setSelectedSource(resolvedSource);
      if (resolvedSource) {
        const detail = await upmsApi.getItsmSource(resolvedSource);
        setDefinition(detail);
      } else {
        setDefinition(null);
      }
    } catch (err: any) {
      setError(err.message ?? 'Failed to load ITSM source data.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadPage();
  }, []);

  useEffect(() => {
    if (!selectedSource) {
      setDefinition(null);
      return;
    }

    upmsApi.getItsmSource(selectedSource).then(setDefinition).catch((err) => setError(err.message ?? 'Failed to load source detail.'));
  }, [selectedSource]);

  const canonicalOptions = useMemo(
    () => canonicalFields.map((field) => field.name).sort((left, right) => left.localeCompare(right)),
    [canonicalFields],
  );

  const createSource = async (event: FormEvent) => {
    event.preventDefault();
    if (!newSourceName.trim() || !newSourceLabel.trim()) return;

    setBusy(true);
    setError(undefined);
    setMessage(undefined);
    try {
      const created = await upmsApi.createItsmSource({
        name: newSourceName.trim(),
        displayLabel: newSourceLabel.trim(),
      });

      setNewSourceName('');
      setNewSourceLabel('');
      setMessage(`Created source '${created.displayLabel}'.`);
      await loadPage(created.name);
    } catch (err: any) {
      setError(err.message ?? 'Failed to create ITSM source.');
    } finally {
      setBusy(false);
    }
  };

  const saveMapping = async (event: FormEvent) => {
    event.preventDefault();
    if (!selectedSource || !mappingFieldName.trim() || !mappingCanonicalField.trim()) return;

    setBusy(true);
    setError(undefined);
    setMessage(undefined);
    try {
      await upmsApi.upsertItsmMapping(selectedSource, mappingFieldName.trim(), {
        canonicalFieldName: mappingCanonicalField.trim(),
        isRequired: mappingRequired,
      });

      setMappingFieldName('');
      setMappingCanonicalField('');
      setMappingRequired(false);
      setMessage('Saved field mapping.');
      const detail = await upmsApi.getItsmSource(selectedSource);
      setDefinition(detail);
    } catch (err: any) {
      setError(err.message ?? 'Failed to save field mapping.');
    } finally {
      setBusy(false);
    }
  };

  const deleteMapping = async (sourceFieldName: string) => {
    if (!selectedSource) return;

    setBusy(true);
    setError(undefined);
    setMessage(undefined);
    try {
      await upmsApi.deleteItsmMapping(selectedSource, sourceFieldName);
      setMessage(`Deleted mapping '${sourceFieldName}'.`);
      const detail = await upmsApi.getItsmSource(selectedSource);
      setDefinition(detail);
    } catch (err: any) {
      setError(err.message ?? 'Failed to delete field mapping.');
    } finally {
      setBusy(false);
    }
  };

  const deleteSource = async () => {
    if (!selectedSource) return;

    setBusy(true);
    setError(undefined);
    setMessage(undefined);
    try {
      await upmsApi.deleteItsmSource(selectedSource);
      setMessage(`Deleted source '${selectedSource}'.`);
      await loadPage();
    } catch (err: any) {
      setError(err.message ?? 'Failed to delete ITSM source.');
    } finally {
      setBusy(false);
    }
  };

  const importSource = async (event: FormEvent) => {
    event.preventDefault();
    if (!importFile) return;

    setBusy(true);
    setError(undefined);
    setMessage(undefined);

    try {
      const fileText = await importFile.text();
      const imported = parseItsmSourceImport(importFile.name, fileText);
      const resolvedName = (imported.name ?? importName).trim();
      const resolvedLabel = (imported.displayLabel ?? importLabel).trim() || resolvedName;

      if (!resolvedName) {
        throw new Error('A source name is required. Supply it in the file or in the import form.');
      }

      const existing = sources.find((source) => source.name === resolvedName);
      if (!existing) {
        await upmsApi.createItsmSource({ name: resolvedName, displayLabel: resolvedLabel });
      }

      for (const mapping of imported.mappings) {
        await upmsApi.upsertItsmMapping(resolvedName, mapping.sourceFieldName, {
          canonicalFieldName: mapping.canonicalFieldName,
          isRequired: mapping.isRequired,
        });
      }

      setImportName('');
      setImportLabel('');
      setImportFile(null);
      setMessage(`Imported ${imported.mappings.length} mappings into '${resolvedName}'.`);
      await loadPage(resolvedName);
    } catch (err: any) {
      setError(err.message ?? 'Failed to import ITSM source definition.');
    } finally {
      setBusy(false);
    }
  };

  if (loading) return <LoadingPanel label="Loading ITSM sources" />;

  return (
    <>
      <ErrorAlert message={error} onClose={() => setError(undefined)} />
      <PageSection title="ITSM sources" description="Create sources, import field mappings, and maintain canonical field alignment.">
        {message ? (
          <Typography color="success.main" sx={{ mb: 2 }}>
            {message}
          </Typography>
        ) : null}

        <Grid container spacing={3}>
          <Grid item xs={12} lg={6}>
            <Stack spacing={3}>
              <Box component="form" onSubmit={createSource}>
                <Typography variant="h6" sx={{ mb: 1 }}>
                  Create source
                </Typography>
                <Grid container spacing={2}>
                  <Grid item xs={12} md={6}>
                    <TextField label="Source name" value={newSourceName} onChange={(e) => setNewSourceName(e.target.value)} fullWidth helperText="Use a stable slug such as service-now or jira." />
                  </Grid>
                  <Grid item xs={12} md={6}>
                    <TextField label="Display label" value={newSourceLabel} onChange={(e) => setNewSourceLabel(e.target.value)} fullWidth />
                  </Grid>
                </Grid>
                <Button sx={{ mt: 2 }} type="submit" variant="contained" disabled={busy || !newSourceName.trim() || !newSourceLabel.trim()}>
                  Create source
                </Button>
              </Box>

              <Box component="form" onSubmit={importSource}>
                <Typography variant="h6" sx={{ mb: 1 }}>
                  Import source definition
                </Typography>
                <Typography color="text.secondary" sx={{ mb: 2 }}>
                  Import mappings from JSON or CSV. Supported columns: sourceFieldName, canonicalFieldName, isRequired.
                </Typography>
                <Grid container spacing={2}>
                  <Grid item xs={12} md={6}>
                    <TextField label="Source name override" value={importName} onChange={(e) => setImportName(e.target.value)} fullWidth />
                  </Grid>
                  <Grid item xs={12} md={6}>
                    <TextField label="Display label override" value={importLabel} onChange={(e) => setImportLabel(e.target.value)} fullWidth />
                  </Grid>
                  <Grid item xs={12}>
                    <Button variant="outlined" component="label">
                      Choose mapping file
                      <input hidden type="file" accept=".json,.csv" onChange={(e) => setImportFile(e.target.files?.[0] ?? null)} />
                    </Button>
                    <Typography sx={{ mt: 1 }} color="text.secondary">
                      {importFile ? `Selected file: ${importFile.name}` : 'No file selected yet.'}
                    </Typography>
                  </Grid>
                </Grid>
                <Button sx={{ mt: 2 }} type="submit" variant="contained" disabled={busy || !importFile}>
                  Import definition
                </Button>
              </Box>
            </Stack>
          </Grid>

          <Grid item xs={12} lg={6}>
            <Stack spacing={3}>
              <Box>
                <Typography variant="h6" sx={{ mb: 1 }}>
                  Manage source mappings
                </Typography>
                <TextField
                  select
                  SelectProps={{ native: true }}
                  InputLabelProps={{ shrink: true }}
                  label="Source"
                  value={selectedSource}
                  onChange={(e) => setSelectedSource(e.target.value)}
                  fullWidth
                >
                  <option value="">Select a source</option>
                  {sources.map((source) => (
                    <option key={source.name} value={source.name}>
                      {source.displayLabel}
                    </option>
                  ))}
                </TextField>
                <Box sx={{ mt: 1 }}>
                  <Button type="button" onClick={deleteSource} disabled={!selectedSource || busy} color="error">
                    Delete selected source
                  </Button>
                </Box>
              </Box>

              <Box component="form" onSubmit={saveMapping}>
                <Grid container spacing={2}>
                  <Grid item xs={12} md={6}>
                    <TextField label="Source field name" value={mappingFieldName} onChange={(e) => setMappingFieldName(e.target.value)} fullWidth />
                  </Grid>
                  <Grid item xs={12} md={6}>
                    <TextField
                      select
                      SelectProps={{ native: true }}
                      InputLabelProps={{ shrink: true }}
                      label="Canonical field"
                      value={mappingCanonicalField}
                      onChange={(e) => setMappingCanonicalField(e.target.value)}
                      fullWidth
                    >
                      <option value="">Select a canonical field</option>
                      {canonicalOptions.map((option) => (
                        <option key={option} value={option}>
                          {option}
                        </option>
                      ))}
                    </TextField>
                  </Grid>
                </Grid>
                <FormControlLabel
                  sx={{ mt: 1 }}
                  control={<Checkbox checked={mappingRequired} onChange={(e) => setMappingRequired(e.target.checked)} />}
                  label="Required for ingest"
                />
                <Box>
                  <Button type="submit" variant="contained" disabled={busy || !selectedSource || !mappingFieldName.trim() || !mappingCanonicalField.trim()}>
                    Save mapping
                  </Button>
                </Box>
              </Box>
            </Stack>
          </Grid>
        </Grid>
      </PageSection>

      <PageSection title="Current mappings" description="Review field mappings for the selected ITSM source.">
        {definition ? (
          <UpmsDataTable
            columns={[
              { key: 'sourceFieldName', label: 'Source Field' },
              { key: 'canonicalFieldName', label: 'Canonical Field' },
              { key: 'required', label: 'Required' },
              { key: 'actions', label: 'Actions' },
            ]}
            rows={definition.mappings.map((mapping) => ({
              id: `${mapping.itsmSource}:${mapping.sourceFieldName}`,
              sourceFieldName: mapping.sourceFieldName,
              canonicalFieldName: mapping.canonicalFieldName,
              required: mapping.isRequired ? 'Yes' : 'No',
              actions: (
                <Button size="small" onClick={() => deleteMapping(mapping.sourceFieldName)}>
                  Delete
                </Button>
              ),
            }))}
          />
        ) : (
          <Typography color="text.secondary">No source selected yet.</Typography>
        )}
      </PageSection>
    </>
  );
}

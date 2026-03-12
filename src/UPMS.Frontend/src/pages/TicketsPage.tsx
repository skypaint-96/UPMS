import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Autocomplete, Box, Button, Chip, Grid, Link as MuiLink, Stack, TextField } from '@mui/material';
import { Link } from 'react-router-dom';
import { upmsApi } from '../api/client';
import { ItsmSourceDefinition, ItsmSourceSummary, Ticket } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { LoadingPanel } from '../components/LoadingPanel';
import { PageSection } from '../components/PageSection';
import { UpmsDataTable } from '../components/UpmsDataTable';
import { getTicketFieldOptions, getTicketFieldValue, getTicketFieldValueSuggestions, getTicketNumber, getTicketState, getUniqueSortedStrings } from '../utils/ticketFields';

function toIsoDateTime(localValue: string) {
  const parsed = new Date(localValue);
  return Number.isNaN(parsed.getTime()) ? undefined : parsed.toISOString();
}

function normalizeSelection(values: string[]) {
  return getUniqueSortedStrings(values);
}

function isBaseTicketField(fieldName: string) {
  const normalized = fieldName.trim().toLowerCase();
  return normalized === 'state' || normalized === 'status' || normalized === 'number' || normalized === 'ticket_number';
}

export function TicketsPage() {
  const [sources, setSources] = useState<ItsmSourceSummary[]>([]);
  const [definition, setDefinition] = useState<ItsmSourceDefinition | null>(null);
  const [itsmSource, setItsmSource] = useState('');
  const [company, setCompany] = useState('');
  const [asOf, setAsOf] = useState(new Date().toISOString().slice(0, 16));
  const [fieldName, setFieldName] = useState('');
  const [fieldValue, setFieldValue] = useState('');
  const [visibleFieldColumns, setVisibleFieldColumns] = useState<string[]>([]);
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [suggestionTickets, setSuggestionTickets] = useState<Ticket[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();

  useEffect(() => {
    let cancelled = false;

    upmsApi
      .getItsmSources()
      .then((loaded) => {
        if (cancelled) {
          return;
        }

        setSources(loaded);
        if (loaded.length > 0) {
          setItsmSource((current) => current || loaded[0].name);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setError(err.message ?? 'Failed to load ITSM sources.');
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    const effectiveAsOf = toIsoDateTime(asOf);
    if (!itsmSource || !effectiveAsOf) {
      setDefinition(null);
      setSuggestionTickets([]);
      return;
    }

    let cancelled = false;

    Promise.all([
      upmsApi.getItsmSource(itsmSource).catch(() => null),
      upmsApi.getTickets({ itsmSource, asOf: effectiveAsOf }).catch(() => []),
    ]).then(([loadedDefinition, loadedTickets]) => {
      if (cancelled) {
        return;
      }

      setDefinition(loadedDefinition);
      setSuggestionTickets(loadedTickets);
    });

    return () => {
      cancelled = true;
    };
  }, [asOf, itsmSource]);

  const companySuggestions = useMemo(() => getUniqueSortedStrings(suggestionTickets.map((ticket) => ticket.companyName)), [suggestionTickets]);

  const fieldOptions = useMemo(
    () => getTicketFieldOptions(suggestionTickets, definition?.mappings ?? []),
    [definition?.mappings, suggestionTickets],
  );

  const columnOptions = useMemo(() => fieldOptions.filter((field) => !isBaseTicketField(field)), [fieldOptions]);

  const valueSuggestionScope = useMemo(() => {
    const normalizedCompany = company.trim().toLowerCase();
    if (!normalizedCompany) {
      return suggestionTickets;
    }

    return suggestionTickets.filter((ticket) => ticket.companyName.toLowerCase().includes(normalizedCompany));
  }, [company, suggestionTickets]);

  const fieldValueSuggestions = useMemo(
    () => getTicketFieldValueSuggestions(valueSuggestionScope, fieldName),
    [fieldName, valueSuggestionScope],
  );

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError(undefined);

    const effectiveAsOf = toIsoDateTime(asOf);
    if (!effectiveAsOf) {
      setError('Please choose a valid "As of" date and time.');
      return;
    }

    const normalizedFieldName = fieldName.trim();
    const normalizedFieldValue = fieldValue.trim();

    if ((normalizedFieldName && !normalizedFieldValue) || (!normalizedFieldName && normalizedFieldValue)) {
      setError('Enter both a field name and a field value to apply the extra ticket filter.');
      return;
    }

    setLoading(true);
    try {
      const loaded = await upmsApi.getTickets({
        itsmSource,
        company: company.trim() || undefined,
        asOf: effectiveAsOf,
        fieldName: normalizedFieldName || undefined,
        fieldValue: normalizedFieldValue || undefined,
      });
      setTickets(loaded);
    } catch (err: any) {
      setError(err.message ?? 'Failed to load tickets.');
    } finally {
      setLoading(false);
    }
  };

  const ticketColumns = useMemo(
    () => [
      {
        key: 'ticketKey',
        label: 'Ticket',
        getSortValue: (row: Record<string, unknown>) => String(row.ticketSortValue ?? ''),
      },
      { key: 'companyName', label: 'Company' },
      { key: 'state', label: 'State' },
      {
        key: 'snapshotDate',
        label: 'Snapshot Date',
        getSortValue: (row: Record<string, unknown>) => String(row.snapshotDateSortValue ?? ''),
      },
      ...visibleFieldColumns.map((field) => ({
        key: `field:${field}`,
        label: field,
      })),
    ],
    [visibleFieldColumns],
  );

  const ticketRows = useMemo(
    () =>
      tickets.map((ticket) => {
        const ticketNumber = getTicketNumber(ticket);
        const query = new URLSearchParams({ itsmSource: ticket.itsmSource, asOf }).toString();
        return {
          id: `${ticket.snapshotId}:${ticket.companyName}:${ticket.ticketKey}`,
          ticketKey: (
            <MuiLink component={Link} to={`/tickets/${encodeURIComponent(ticket.companyName)}/${encodeURIComponent(ticket.ticketKey)}?${query}`}>
              {ticketNumber}
            </MuiLink>
          ),
          ticketSortValue: ticketNumber,
          companyName: ticket.companyName,
          state: getTicketState(ticket) || '-',
          snapshotDate: new Date(ticket.snapshotDate).toLocaleString(),
          snapshotDateSortValue: ticket.snapshotDate,
          ...Object.fromEntries(
            visibleFieldColumns.map((field) => [`field:${field}`, getTicketFieldValue(ticket, field) || '-']),
          ),
        };
      }),
    [asOf, tickets, visibleFieldColumns],
  );

  return (
    <>
      <ErrorAlert message={error} onClose={() => setError(undefined)} />
      <PageSection title="Tickets" description="Query point-in-time ticket state by source, company, timestamp, and optional field filters.">
        <Box component="form" onSubmit={submit} sx={{ mb: 3 }}>
          <Grid container spacing={2}>
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
                <option value="">Select a source</option>
                {sources.map((source) => (
                  <option key={source.name} value={source.name}>
                    {source.displayLabel}
                  </option>
                ))}
              </TextField>
            </Grid>
            <Grid item xs={12} md={4}>
              <Autocomplete
                freeSolo
                options={companySuggestions}
                inputValue={company}
                onInputChange={(_, newValue) => setCompany(newValue)}
                onChange={(_, newValue) => setCompany(typeof newValue === 'string' ? newValue : newValue ?? '')}
                renderInput={(params) => (
                  <TextField
                    {...params}
                    label="Company (optional)"
                    fullWidth
                    helperText={companySuggestions.length > 0 ? 'Suggestions come from the selected source and timestamp.' : 'Start with an ITSM source to load company suggestions.'}
                  />
                )}
              />
            </Grid>
            <Grid item xs={12} md={4}>
              <TextField label="As of" type="datetime-local" value={asOf} onChange={(e) => setAsOf(e.target.value)} fullWidth InputLabelProps={{ shrink: true }} />
            </Grid>
            <Grid item xs={12} md={6}>
              <Autocomplete
                freeSolo
                options={fieldOptions}
                inputValue={fieldName}
                onInputChange={(_, newValue) => {
                  setFieldName(newValue);
                  setFieldValue('');
                }}
                onChange={(_, newValue) => {
                  setFieldName(typeof newValue === 'string' ? newValue : newValue ?? '');
                  setFieldValue('');
                }}
                renderInput={(params) => (
                  <TextField
                    {...params}
                    label="Field filter name (optional)"
                    fullWidth
                    helperText={fieldOptions.length > 0 ? 'Choose a ticket field to narrow the result set.' : 'Field suggestions load from the selected source.'}
                  />
                )}
              />
            </Grid>
            <Grid item xs={12} md={6}>
              <Autocomplete
                freeSolo
                options={fieldValueSuggestions}
                inputValue={fieldValue}
                onInputChange={(_, newValue) => setFieldValue(newValue)}
                onChange={(_, newValue) => setFieldValue(typeof newValue === 'string' ? newValue : newValue ?? '')}
                renderInput={(params) => (
                  <TextField
                    {...params}
                    label="Field filter value (optional)"
                    fullWidth
                    helperText={fieldName ? 'Suggestions narrow as you pick a field and company.' : 'Pick a field name first to see likely values.'}
                  />
                )}
              />
            </Grid>
            <Grid item xs={12}>
              <Autocomplete
                multiple
                freeSolo
                filterSelectedOptions
                options={columnOptions}
                value={visibleFieldColumns}
                onChange={(_, newValue) => setVisibleFieldColumns(normalizeSelection(newValue.map((value) => String(value))))}
                renderInput={(params) => (
                  <TextField
                    {...params}
                    label="Additional ticket columns"
                    helperText="Add ticket fields from the underlying data to the results table below."
                    fullWidth
                  />
                )}
              />
            </Grid>
          </Grid>
          <Stack direction="row" spacing={1} sx={{ mt: 2, mb: 2, flexWrap: 'wrap', rowGap: 1 }}>
            <Chip label={`${tickets.length} result${tickets.length === 1 ? '' : 's'}`} variant="outlined" />
            <Chip label={`${visibleFieldColumns.length} extra column${visibleFieldColumns.length === 1 ? '' : 's'}`} variant="outlined" />
          </Stack>
          <Button sx={{ mt: 1 }} variant="contained" type="submit" disabled={!itsmSource || loading}>
            Search tickets
          </Button>
        </Box>
        {loading ? (
          <LoadingPanel label="Loading tickets" />
        ) : (
          <UpmsDataTable dataTestId="tickets-table" columns={ticketColumns} rows={ticketRows} />
        )}
      </PageSection>
    </>
  );
}

import { FormEvent, useEffect, useState } from 'react';
import { Box, Button, Grid, Link as MuiLink, TextField } from '@mui/material';
import { Link } from 'react-router-dom';
import { upmsApi } from '../api/client';
import { ItsmSourceSummary, Ticket } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { LoadingPanel } from '../components/LoadingPanel';
import { PageSection } from '../components/PageSection';
import { UpmsDataTable } from '../components/UpmsDataTable';

export function TicketsPage() {
  const [sources, setSources] = useState<ItsmSourceSummary[]>([]);
  const [itsmSource, setItsmSource] = useState('');
  const [company, setCompany] = useState('');
  const [asOf, setAsOf] = useState(new Date().toISOString().slice(0, 16));
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();

  useEffect(() => {
    upmsApi.getItsmSources().then((loaded) => {
      setSources(loaded);
      if (loaded.length > 0) {
        setItsmSource(loaded[0].name);
      }
    });
  }, []);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setLoading(true);
    setError(undefined);
    try {
      const loaded = await upmsApi.getTickets({
        itsmSource,
        company: company || undefined,
        asOf: new Date(asOf).toISOString(),
      });
      setTickets(loaded);
    } catch (err: any) {
      setError(err.message ?? 'Failed to load tickets.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <>
      <ErrorAlert message={error} onClose={() => setError(undefined)} />
      <PageSection title="Tickets" description="Query point-in-time ticket state by source, company, and timestamp.">
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
              <TextField label="Company (optional)" value={company} onChange={(e) => setCompany(e.target.value)} fullWidth />
            </Grid>
            <Grid item xs={12} md={4}>
              <TextField label="As of" type="datetime-local" value={asOf} onChange={(e) => setAsOf(e.target.value)} fullWidth InputLabelProps={{ shrink: true }} />
            </Grid>
          </Grid>
          <Button sx={{ mt: 2 }} variant="contained" type="submit" disabled={!itsmSource || loading}>
            Search tickets
          </Button>
        </Box>
        {loading ? (
          <LoadingPanel label="Loading tickets" />
        ) : (
          <UpmsDataTable
            dataTestId="tickets-table"
            columns={[
              { key: 'ticketKey', label: 'Ticket' },
              { key: 'companyName', label: 'Company' },
              { key: 'state', label: 'State' },
              { key: 'snapshotDate', label: 'Snapshot Date' },
            ]}
            rows={tickets.map((ticket) => ({
              id: ticket.ticketKey,
              ticketKey: (
                <MuiLink component={Link} to={`/tickets/${encodeURIComponent(ticket.companyName)}/${encodeURIComponent(ticket.ticketKey)}?itsmSource=${encodeURIComponent(ticket.itsmSource)}`}>
                  {ticket.fields['Number'] ?? ticket.ticketKey}
                </MuiLink>
              ),
              companyName: ticket.companyName,
              state: ticket.fields['State'] ?? ticket.fields['state'] ?? '',
              snapshotDate: new Date(ticket.snapshotDate).toLocaleString(),
            }))}
          />
        )}
      </PageSection>
    </>
  );
}

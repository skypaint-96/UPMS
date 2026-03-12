import { useEffect, useMemo, useState } from 'react';
import { Grid, Paper, Typography } from '@mui/material';
import { useLocation, useParams } from 'react-router-dom';
import { upmsApi } from '../api/client';
import { FieldChange, Ticket } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { LoadingPanel } from '../components/LoadingPanel';
import { UpmsDataTable } from '../components/UpmsDataTable';

export function TicketDetailPage() {
  const params = useParams();
  const location = useLocation();
  const query = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const itsmSource = query.get('itsmSource') ?? undefined;
  const asOf = query.get('asOf') ?? undefined;
  const [ticket, setTicket] = useState<Ticket | null>(null);
  const [history, setHistory] = useState<FieldChange[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();

  useEffect(() => {
    if (!params.company || !params.ticketKey) {
      return;
    }

    setLoading(true);
    Promise.all([
      upmsApi.getTicket(params.company, params.ticketKey, itsmSource, asOf),
      upmsApi.getTicketHistory(params.company, params.ticketKey),
    ])
      .then(([loadedTicket, loadedHistory]) => {
        setTicket(loadedTicket);
        setHistory(loadedHistory);
      })
      .catch((err) => setError(err.message ?? 'Failed to load ticket.'))
      .finally(() => setLoading(false));
  }, [asOf, itsmSource, params.company, params.ticketKey]);

  if (loading) return <LoadingPanel label="Loading ticket" />;
  if (!ticket) return <Typography>Ticket not found.</Typography>;

  const fieldRows = Object.entries(ticket.fields).map(([key, value]) => ({ id: key, field: key, value: value ?? '' }));

  return (
    <>
      <ErrorAlert message={error} onClose={() => setError(undefined)} />
      <Typography variant="h4" sx={{ mb: 2 }}>
        {ticket.fields['Number'] ?? ticket.ticketKey}
      </Typography>
      <Grid container spacing={2}>
        <Grid item xs={12} md={5}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" sx={{ mb: 2 }}>
              Current field state
            </Typography>
            <UpmsDataTable columns={[{ key: 'field', label: 'Field' }, { key: 'value', label: 'Value' }]} rows={fieldRows} />
          </Paper>
        </Grid>
        <Grid item xs={12} md={7}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" sx={{ mb: 2 }}>
              Change history
            </Typography>
            <UpmsDataTable
              columns={[
                { key: 'observedAt', label: 'Observed At', getSortValue: (row) => String(row.observedAtSortValue ?? '') },
                { key: 'field', label: 'Field' },
                { key: 'value', label: 'Value' },
              ]}
              rows={history.map((change) => ({
                id: change.id,
                observedAt: new Date(change.observedAt).toLocaleString(),
                observedAtSortValue: change.observedAt,
                field: change.displayFieldName,
                value: change.fieldValue ?? '',
              }))}
            />
          </Paper>
        </Grid>
      </Grid>
    </>
  );
}

import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Box, Button, Chip, Grid, Paper, Stack, TextField, Typography } from '@mui/material';
import { upmsApi } from '../api/client';
import { ItsmSourceSummary, ProblemRequestDetail, ProblemRequestSummary, ProblemRequestStatus } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { LoadingPanel } from '../components/LoadingPanel';
import { PageSection } from '../components/PageSection';
import { UpmsDataTable } from '../components/UpmsDataTable';

const problemRequestStatuses: ProblemRequestStatus[] = [
  'New',
  'Under Review',
  'Accepted',
  'Rejected',
  'Converted / Linked',
];

const problemRequestTriageStatuses: ProblemRequestStatus[] = [
  'New',
  'Under Review',
  'Accepted',
  'Rejected',
];

const emptyCreateForm = {
  requesterName: '',
  requesterEmail: '',
  requesterTeam: '',
  companyName: '',
  itsmSource: '',
  title: '',
  description: '',
  justification: '',
  assignee: '',
};

function blankToUndefined(value: string) {
  const trimmed = value.trim();
  return trimmed ? trimmed : undefined;
}

function displayValue(value?: string | null) {
  return value && value.trim() ? value : '—';
}

function getErrorMessage(error: any, fallback: string) {
  const validationErrors = error?.response?.data?.errors;
  if (validationErrors && typeof validationErrors === 'object') {
    const firstEntry = Object.values(validationErrors).flat()[0];
    if (typeof firstEntry === 'string' && firstEntry.trim()) {
      return firstEntry;
    }
  }

  const apiError = error?.response?.data?.error;
  if (typeof apiError === 'string' && apiError.trim()) {
    return apiError;
  }

  return error?.message ?? fallback;
}

export function ProblemRequestsPage() {
  const [sources, setSources] = useState<ItsmSourceSummary[]>([]);
  const [requests, setRequests] = useState<ProblemRequestSummary[]>([]);
  const [selectedRequestId, setSelectedRequestId] = useState('');
  const [selectedRequest, setSelectedRequest] = useState<ProblemRequestDetail | null>(null);
  const [statusFilter, setStatusFilter] = useState('');
  const [createForm, setCreateForm] = useState(emptyCreateForm);
  const [triageForm, setTriageForm] = useState({ status: 'New', assignee: '', decisionReason: '' });
  const [commentText, setCommentText] = useState('');
  const [commentAuthor, setCommentAuthor] = useState('');
  const [linkForm, setLinkForm] = useState({
    problemReference: '',
    problemItsmSource: '',
    problemCompanyName: '',
    problemTicketKey: '',
    comment: '',
  });
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);
  const [error, setError] = useState<string>();
  const [message, setMessage] = useState<string>();

  const isSelectedRequestConverted = selectedRequest?.status === 'Converted / Linked';
  const isSelectedRequestRejected = selectedRequest?.status === 'Rejected';
  const triageStatusOptions = isSelectedRequestConverted ? problemRequestStatuses : problemRequestTriageStatuses;

  useEffect(() => {
    let cancelled = false;
    setLoading(true);

    Promise.all([
      upmsApi.getItsmSources().catch(() => []),
      upmsApi.getProblemRequests({ status: blankToUndefined(statusFilter) }),
    ])
      .then(([loadedSources, loadedRequests]) => {
        if (cancelled) {
          return;
        }

        setSources(loadedSources);
        setRequests(loadedRequests);
        setSelectedRequestId((current) => {
          if (current && loadedRequests.some((request) => request.id === current)) {
            return current;
          }

          return loadedRequests[0]?.id ?? '';
        });

        if (loadedRequests.length === 0) {
          setSelectedRequest(null);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setError(getErrorMessage(err, 'Failed to load problem requests.'));
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [refreshKey, statusFilter]);

  useEffect(() => {
    if (!selectedRequestId) {
      setSelectedRequest(null);
      return;
    }

    let cancelled = false;
    setDetailLoading(true);

    upmsApi
      .getProblemRequest(selectedRequestId)
      .then((loaded) => {
        if (cancelled) {
          return;
        }

        setSelectedRequest(loaded);
        setTriageForm({
          status: loaded.status,
          assignee: loaded.assignee ?? '',
          decisionReason: loaded.decisionReason ?? '',
        });
        setLinkForm({
          problemReference: loaded.problemReference ?? '',
          problemItsmSource: loaded.problemItsmSource ?? loaded.itsmSource ?? '',
          problemCompanyName: loaded.problemCompanyName ?? loaded.companyName ?? '',
          problemTicketKey: loaded.problemTicketKey ?? '',
          comment: '',
        });
        setCommentText('');
        setCommentAuthor('');
      })
      .catch((err) => {
        if (!cancelled) {
          setError(getErrorMessage(err, 'Failed to load problem request details.'));
        }
      })
      .finally(() => {
        if (!cancelled) {
          setDetailLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [refreshKey, selectedRequestId]);

  const requestRows = useMemo(
    () =>
      requests.map((request) => ({
        id: request.id,
        title: (
          <Button
            type="button"
            variant="text"
            onClick={() => setSelectedRequestId(request.id)}
            sx={{ justifyContent: 'flex-start', minWidth: 0, p: 0, textTransform: 'none' }}
          >
            {request.title}
          </Button>
        ),
        requester: request.requesterTeam ? `${request.requesterName} (${request.requesterTeam})` : request.requesterName,
        companyName: displayValue(request.companyName),
        itsmSource: displayValue(request.itsmSource),
        status: <Chip size="small" label={request.status} />,
        assignee: displayValue(request.assignee),
        problemReference: displayValue(request.problemReference),
        updatedAt: new Date(request.updatedAt).toLocaleString(),
        updatedAtSortValue: request.updatedAt,
      })),
    [requests],
  );

  const createRequest = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(undefined);
    setMessage(undefined);

    try {
      const created = await upmsApi.createProblemRequest({
        requesterName: createForm.requesterName.trim(),
        requesterEmail: blankToUndefined(createForm.requesterEmail),
        requesterTeam: blankToUndefined(createForm.requesterTeam),
        companyName: blankToUndefined(createForm.companyName),
        itsmSource: blankToUndefined(createForm.itsmSource),
        title: createForm.title.trim(),
        description: createForm.description.trim(),
        justification: createForm.justification.trim(),
        assignee: blankToUndefined(createForm.assignee),
      });

      setCreateForm(emptyCreateForm);
      setSelectedRequestId(created.id);
      setMessage('Problem request submitted.');
      setRefreshKey((current) => current + 1);
    } catch (err: any) {
      setError(getErrorMessage(err, 'Failed to create problem request.'));
    } finally {
      setBusy(false);
    }
  };

  const saveTriage = async (event: FormEvent) => {
    event.preventDefault();
    if (!selectedRequest) {
      return;
    }

    setBusy(true);
    setError(undefined);
    setMessage(undefined);

    try {
      await upmsApi.updateProblemRequest(selectedRequest.id, {
        status: triageForm.status,
        assignee: triageForm.assignee,
        decisionReason: triageForm.decisionReason,
      });

      setMessage('Problem request updated.');
      setRefreshKey((current) => current + 1);
    } catch (err: any) {
      setError(getErrorMessage(err, 'Failed to update problem request.'));
    } finally {
      setBusy(false);
    }
  };

  const addComment = async (event: FormEvent) => {
    event.preventDefault();
    if (!selectedRequest) {
      return;
    }

    setBusy(true);
    setError(undefined);
    setMessage(undefined);

    try {
      await upmsApi.addProblemRequestComment(selectedRequest.id, {
        commentText: commentText.trim(),
        authorName: blankToUndefined(commentAuthor),
      });

      setCommentText('');
      setCommentAuthor('');
      setMessage('Comment added.');
      setRefreshKey((current) => current + 1);
    } catch (err: any) {
      setError(getErrorMessage(err, 'Failed to add comment.'));
    } finally {
      setBusy(false);
    }
  };

  const linkRequest = async (event: FormEvent) => {
    event.preventDefault();
    if (!selectedRequest) {
      return;
    }

    setBusy(true);
    setError(undefined);
    setMessage(undefined);

    try {
      await upmsApi.linkProblemRequest(selectedRequest.id, {
        problemReference: blankToUndefined(linkForm.problemReference),
        problemItsmSource: blankToUndefined(linkForm.problemItsmSource),
        problemCompanyName: blankToUndefined(linkForm.problemCompanyName),
        problemTicketKey: blankToUndefined(linkForm.problemTicketKey),
        comment: blankToUndefined(linkForm.comment),
      });

      setLinkForm((current) => ({ ...current, comment: '' }));
      setMessage('Problem request linked to a problem reference.');
      setRefreshKey((current) => current + 1);
    } catch (err: any) {
      setError(getErrorMessage(err, 'Failed to link problem request.'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <ErrorAlert message={error} onClose={() => setError(undefined)} />

      <PageSection
        title="Problem requests"
        description="Submit a lightweight intake request when a team needs a problem raised or reviewed, then track triage and link the request to the real problem record."
      >
        {message ? (
          <Typography color="success.main" sx={{ mb: 2 }}>
            {message}
          </Typography>
        ) : null}

        <Box component="form" onSubmit={createRequest}>
          <Grid container spacing={2}>
            <Grid item xs={12} md={4}>
              <TextField label="Requester name" value={createForm.requesterName} onChange={(event) => setCreateForm((current) => ({ ...current, requesterName: event.target.value }))} fullWidth />
            </Grid>
            <Grid item xs={12} md={4}>
              <TextField label="Requester email" value={createForm.requesterEmail} onChange={(event) => setCreateForm((current) => ({ ...current, requesterEmail: event.target.value }))} fullWidth />
            </Grid>
            <Grid item xs={12} md={4}>
              <TextField label="Requester team" value={createForm.requesterTeam} onChange={(event) => setCreateForm((current) => ({ ...current, requesterTeam: event.target.value }))} fullWidth />
            </Grid>
            <Grid item xs={12} md={4}>
              <TextField label="Company (optional)" value={createForm.companyName} onChange={(event) => setCreateForm((current) => ({ ...current, companyName: event.target.value }))} fullWidth />
            </Grid>
            <Grid item xs={12} md={4}>
              <TextField
                select
                SelectProps={{ native: true }}
                InputLabelProps={{ shrink: true }}
                label="ITSM source (optional)"
                value={createForm.itsmSource}
                onChange={(event) => setCreateForm((current) => ({ ...current, itsmSource: event.target.value }))}
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
              <TextField label="Suggested owner (optional)" value={createForm.assignee} onChange={(event) => setCreateForm((current) => ({ ...current, assignee: event.target.value }))} fullWidth />
            </Grid>
            <Grid item xs={12}>
              <TextField label="Title" value={createForm.title} onChange={(event) => setCreateForm((current) => ({ ...current, title: event.target.value }))} fullWidth />
            </Grid>
            <Grid item xs={12} md={6}>
              <TextField
                label="Description"
                value={createForm.description}
                onChange={(event) => setCreateForm((current) => ({ ...current, description: event.target.value }))}
                fullWidth
                multiline
                minRows={4}
                helperText="Describe the issue pattern, impact, and any key evidence already known."
              />
            </Grid>
            <Grid item xs={12} md={6}>
              <TextField
                label="Reason / justification"
                value={createForm.justification}
                onChange={(event) => setCreateForm((current) => ({ ...current, justification: event.target.value }))}
                fullWidth
                multiline
                minRows={4}
                helperText="Explain why this should be reviewed as a problem request rather than left as a ticket-only issue."
              />
            </Grid>
          </Grid>

          <Button
            sx={{ mt: 2 }}
            type="submit"
            variant="contained"
            disabled={busy || !createForm.requesterName.trim() || !createForm.title.trim() || !createForm.description.trim() || !createForm.justification.trim()}
          >
            Submit request
          </Button>
        </Box>
      </PageSection>

      <PageSection title="Request queue" description="Browse the triage queue, filter by status, and open a request to review it in more detail.">
        <Grid container spacing={2} sx={{ mb: 2 }}>
          <Grid item xs={12} md={4}>
            <TextField
              select
              SelectProps={{ native: true }}
              InputLabelProps={{ shrink: true }}
              label="Status filter"
              value={statusFilter}
              onChange={(event) => setStatusFilter(event.target.value)}
              fullWidth
            >
              <option value="">All statuses</option>
              {problemRequestStatuses.map((status) => (
                <option key={status} value={status}>
                  {status}
                </option>
              ))}
            </TextField>
          </Grid>
        </Grid>

        {loading ? <LoadingPanel label="Loading problem requests" /> : null}
        {!loading ? (
          <UpmsDataTable
            columns={[
              { key: 'title', label: 'Title' },
              { key: 'requester', label: 'Requester' },
              { key: 'companyName', label: 'Company' },
              { key: 'itsmSource', label: 'ITSM Source' },
              { key: 'status', label: 'Status' },
              { key: 'assignee', label: 'Assignee' },
              { key: 'problemReference', label: 'Problem Ref' },
              { key: 'updatedAt', label: 'Updated', getSortValue: (row) => String(row.updatedAtSortValue ?? '') },
            ]}
            rows={requestRows}
            dataTestId="problem-request-table"
          />
        ) : null}
      </PageSection>

      <PageSection title="Selected request" description="Review the intake details, record triage decisions, add notes, and link the request to a problem reference.">
        {detailLoading ? <LoadingPanel label="Loading problem request details" /> : null}
        {!detailLoading && !selectedRequest ? (
          <Typography color="text.secondary">Select a request from the queue to start triage.</Typography>
        ) : null}

        {!detailLoading && selectedRequest ? (
          <Grid container spacing={3}>
            <Grid item xs={12} lg={7}>
              <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
                <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} justifyContent="space-between" alignItems={{ xs: 'flex-start', md: 'center' }}>
                  <Box>
                    <Typography variant="h6">{selectedRequest.title}</Typography>
                    <Typography color="text.secondary">Request ID: {selectedRequest.id}</Typography>
                  </Box>
                  <Chip label={selectedRequest.status} />
                </Stack>

                <Grid container spacing={2} sx={{ mt: 1 }}>
                  <Grid item xs={12} md={6}>
                    <Typography variant="subtitle2">Requester</Typography>
                    <Typography>{selectedRequest.requesterName}</Typography>
                    <Typography color="text.secondary">{displayValue(selectedRequest.requesterEmail)}</Typography>
                    <Typography color="text.secondary">{displayValue(selectedRequest.requesterTeam)}</Typography>
                  </Grid>
                  <Grid item xs={12} md={6}>
                    <Typography variant="subtitle2">Context</Typography>
                    <Typography>Company: {displayValue(selectedRequest.companyName)}</Typography>
                    <Typography>ITSM source: {displayValue(selectedRequest.itsmSource)}</Typography>
                    <Typography>Created: {new Date(selectedRequest.createdAt).toLocaleString()}</Typography>
                    <Typography>Updated: {new Date(selectedRequest.updatedAt).toLocaleString()}</Typography>
                  </Grid>
                  <Grid item xs={12}>
                    <Typography variant="subtitle2">Description</Typography>
                    <Typography sx={{ whiteSpace: 'pre-wrap' }}>{selectedRequest.description}</Typography>
                  </Grid>
                  <Grid item xs={12}>
                    <Typography variant="subtitle2">Reason / justification</Typography>
                    <Typography sx={{ whiteSpace: 'pre-wrap' }}>{selectedRequest.justification}</Typography>
                  </Grid>
                  <Grid item xs={12}>
                    <Typography variant="subtitle2">Linked problem reference</Typography>
                    <Typography>{displayValue(selectedRequest.problemReference)}</Typography>
                    {selectedRequest.problemItsmSource || selectedRequest.problemCompanyName || selectedRequest.problemTicketKey ? (
                      <Typography color="text.secondary">
                        {displayValue(selectedRequest.problemItsmSource)} / {displayValue(selectedRequest.problemCompanyName)} / {displayValue(selectedRequest.problemTicketKey)}
                      </Typography>
                    ) : null}
                  </Grid>
                </Grid>
              </Paper>

              <Paper variant="outlined" sx={{ p: 2 }}>
                <Typography variant="h6" sx={{ mb: 2 }}>
                  Notes / comments
                </Typography>

                {selectedRequest.comments.length === 0 ? (
                  <Typography color="text.secondary" sx={{ mb: 2 }}>
                    No comments recorded yet.
                  </Typography>
                ) : (
                  <Stack spacing={2} sx={{ mb: 3 }}>
                    {selectedRequest.comments.map((comment) => (
                      <Box key={comment.id} sx={{ border: 1, borderColor: 'divider', borderRadius: 1, p: 2 }}>
                        <Typography variant="subtitle2">{displayValue(comment.authorName)}</Typography>
                        <Typography color="text.secondary" sx={{ mb: 1 }}>
                          {new Date(comment.createdAt).toLocaleString()}
                        </Typography>
                        <Typography sx={{ whiteSpace: 'pre-wrap' }}>{comment.commentText}</Typography>
                      </Box>
                    ))}
                  </Stack>
                )}

                <Box component="form" onSubmit={addComment}>
                  <Grid container spacing={2}>
                    <Grid item xs={12} md={4}>
                      <TextField label="Comment author (optional)" value={commentAuthor} onChange={(event) => setCommentAuthor(event.target.value)} fullWidth />
                    </Grid>
                    <Grid item xs={12} md={8}>
                      <TextField
                        label="Comment"
                        value={commentText}
                        onChange={(event) => setCommentText(event.target.value)}
                        fullWidth
                        multiline
                        minRows={3}
                      />
                    </Grid>
                  </Grid>
                  <Button sx={{ mt: 2 }} type="submit" variant="outlined" disabled={busy || !commentText.trim()}>
                    Add comment
                  </Button>
                </Box>
              </Paper>
            </Grid>

            <Grid item xs={12} lg={5}>
              <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
                <Typography variant="h6" sx={{ mb: 2 }}>
                  Triage
                </Typography>

                <Box component="form" onSubmit={saveTriage}>
                  <Stack spacing={2}>
                    <TextField
                      select
                      SelectProps={{ native: true }}
                      InputLabelProps={{ shrink: true }}
                      label="Status"
                      value={triageForm.status}
                      onChange={(event) => setTriageForm((current) => ({ ...current, status: event.target.value }))}
                      fullWidth
                    >
                      {triageStatusOptions.map((status) => (
                        <option key={status} value={status}>
                          {status}
                        </option>
                      ))}
                    </TextField>
                    <TextField label="Assignee / owner" value={triageForm.assignee} onChange={(event) => setTriageForm((current) => ({ ...current, assignee: event.target.value }))} fullWidth />
                    <TextField
                      label="Decision note"
                      value={triageForm.decisionReason}
                      onChange={(event) => setTriageForm((current) => ({ ...current, decisionReason: event.target.value }))}
                      fullWidth
                      multiline
                      minRows={4}
                      helperText="Capture the acceptance or rejection rationale when helpful."
                    />
                  </Stack>
                  {isSelectedRequestConverted ? (
                    <Typography color="text.secondary" variant="body2">
                      Converted / linked requests are treated as final in v1. Use the link panel to adjust the linked problem reference if needed.
                    </Typography>
                  ) : null}
                  <Button sx={{ mt: 2 }} type="submit" variant="contained" disabled={busy || isSelectedRequestConverted}>
                    Save triage
                  </Button>
                </Box>
              </Paper>

              <Paper variant="outlined" sx={{ p: 2 }}>
                <Typography variant="h6" sx={{ mb: 2 }}>
                  Link to problem reference
                </Typography>

                <Box component="form" onSubmit={linkRequest}>
                  <Stack spacing={2}>
                    <TextField
                      label="Problem reference"
                      value={linkForm.problemReference}
                      onChange={(event) => setLinkForm((current) => ({ ...current, problemReference: event.target.value }))}
                      fullWidth
                      helperText="Use the real problem ID, ticket number, or other reference you want the request linked to."
                    />
                    <TextField
                      select
                      SelectProps={{ native: true }}
                      InputLabelProps={{ shrink: true }}
                      label="Problem ITSM source"
                      value={linkForm.problemItsmSource}
                      onChange={(event) => setLinkForm((current) => ({ ...current, problemItsmSource: event.target.value }))}
                      fullWidth
                    >
                      <option value="">Select a source</option>
                      {sources.map((source) => (
                        <option key={source.name} value={source.name}>
                          {source.displayLabel}
                        </option>
                      ))}
                    </TextField>
                    <TextField label="Problem company" value={linkForm.problemCompanyName} onChange={(event) => setLinkForm((current) => ({ ...current, problemCompanyName: event.target.value }))} fullWidth />
                    <TextField label="Problem ticket key / number" value={linkForm.problemTicketKey} onChange={(event) => setLinkForm((current) => ({ ...current, problemTicketKey: event.target.value }))} fullWidth />
                    <TextField
                      label="Link note (optional)"
                      value={linkForm.comment}
                      onChange={(event) => setLinkForm((current) => ({ ...current, comment: event.target.value }))}
                      fullWidth
                      multiline
                      minRows={3}
                    />
                  </Stack>
                  <Button
                    sx={{ mt: 2 }}
                    type="submit"
                    variant="contained"
                    disabled={busy || isSelectedRequestRejected || (!linkForm.problemReference.trim() && !linkForm.problemTicketKey.trim())}
                  >
                    {selectedRequest.problemReference ? 'Update linked problem' : 'Link to problem'}
                  </Button>
                  {isSelectedRequestRejected ? (
                    <Typography color="text.secondary" variant="body2" sx={{ mt: 1 }}>
                      Rejected requests must be moved back under review or accepted before they can be linked.
                    </Typography>
                  ) : null}
                </Box>
              </Paper>
            </Grid>
          </Grid>
        ) : null}
      </PageSection>
    </>
  );
}

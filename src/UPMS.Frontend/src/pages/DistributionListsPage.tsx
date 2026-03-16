import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Alert, Box, Button, Chip, Grid, Paper, Stack, TextField, Typography } from '@mui/material';
import { Link } from 'react-router-dom';
import { upmsApi } from '../api/client';
import { CompanyProfile, DistributionList, ReportDelivery } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { LoadingPanel } from '../components/LoadingPanel';
import { PageSection } from '../components/PageSection';
import { UpmsDataTable } from '../components/UpmsDataTable';

type FormState = {
  companyName: string;
  name: string;
  description: string;
  isActive: boolean;
  recipientsText: string;
};

const initialFormState: FormState = {
  companyName: '',
  name: '',
  description: '',
  isActive: true,
  recipientsText: '',
};

function parseRecipients(text: string) {
  return text
    .split(/\r?\n|,|;/)
    .map((value) => value.trim())
    .filter(Boolean)
    .map((endpoint, index) => ({
      channel: 'email',
      endpoint,
      isActive: true,
      sortOrder: index,
    }));
}

function buildRecipientsText(list: DistributionList) {
  return list.recipients.map((recipient) => recipient.endpoint).join('\n');
}

export function DistributionListsPage() {
  const [companyFilter, setCompanyFilter] = useState('');
  const [companies, setCompanies] = useState<CompanyProfile[]>([]);
  const [lists, setLists] = useState<DistributionList[]>([]);
  const [deliveries, setDeliveries] = useState<ReportDelivery[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string>();
  const [editingId, setEditingId] = useState<string | null>(null);
  const [formState, setFormState] = useState<FormState>(initialFormState);

  const loadData = async (company?: string) => {
    setLoading(true);
    setError(undefined);
    try {
      const [loadedCompanies, loadedLists, loadedDeliveries] = await Promise.all([
        upmsApi.getCompanyProfiles(),
        upmsApi.getDistributionLists(company ? { company } : undefined),
        upmsApi.getReportDeliveries(company ? { company, take: 50 } : { take: 50 }),
      ]);

      setCompanies(loadedCompanies);
      setLists(loadedLists);
      setDeliveries(loadedDeliveries);
    } catch (err: any) {
      setError(err.message ?? 'Failed to load distribution lists.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadData(companyFilter || undefined);
  }, [companyFilter]);

  const companyOptions = useMemo(
    () => companies.slice().sort((left, right) => left.displayName.localeCompare(right.displayName, undefined, { sensitivity: 'base' })),
    [companies],
  );

  const groupedLists = useMemo(() => {
    const groups = new Map<string, DistributionList[]>();
    for (const list of lists) {
      const key = list.companyName;
      const bucket = groups.get(key) ?? [];
      bucket.push(list);
      groups.set(key, bucket);
    }

    return [...groups.entries()]
      .map(([companyName, companyLists]) => [companyName, companyLists.slice().sort((left, right) => left.name.localeCompare(right.name, undefined, { sensitivity: 'base' }))] as const)
      .sort((left, right) => left[0].localeCompare(right[0], undefined, { sensitivity: 'base' }));
  }, [lists]);

  const resetForm = () => {
    setEditingId(null);
    setFormState(initialFormState);
  };

  const beginEdit = (list: DistributionList) => {
    setEditingId(list.id);
    setFormState({
      companyName: list.companyName,
      name: list.name,
      description: list.description ?? '',
      isActive: list.isActive,
      recipientsText: buildRecipientsText(list),
    });
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setError(undefined);

    try {
      const payload = {
        companyName: formState.companyName,
        name: formState.name,
        description: formState.description || undefined,
        isActive: formState.isActive,
        recipients: parseRecipients(formState.recipientsText),
      };

      if (editingId) {
        await upmsApi.updateDistributionList(editingId, payload);
      } else {
        await upmsApi.createDistributionList(payload);
      }

      resetForm();
      await loadData(companyFilter || undefined);
    } catch (err: any) {
      setError(err.message ?? 'Failed to save distribution list.');
    } finally {
      setSaving(false);
    }
  };

  const removeList = async (list: DistributionList) => {
    setError(undefined);
    try {
      await upmsApi.deleteDistributionList(list.id);
      if (editingId === list.id) {
        resetForm();
      }
      await loadData(companyFilter || undefined);
    } catch (err: any) {
      setError(err.message ?? 'Failed to delete distribution list.');
    }
  };

  return (
    <>
      <ErrorAlert message={error} onClose={() => setError(undefined)} />
      <PageSection
        title="Distribution lists"
        description="Manage company-scoped recipient lists for outbound report delivery. Report rendering stays separate from delivery; the worker renders first and then queues list-specific sends."
      >
        <Alert severity="info" sx={{ mb: 3 }}>
          This release focuses on email delivery only. The backend model keeps channel and endpoint metadata so future non-email targets can be added without changing report templates.
        </Alert>

        <Grid container spacing={3}>
          <Grid item xs={12} lg={7}>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 2 }}>
              <TextField
                select
                SelectProps={{ native: true }}
                InputLabelProps={{ shrink: true }}
                label="Company filter"
                value={companyFilter}
                onChange={(event) => setCompanyFilter(event.target.value)}
                fullWidth
              >
                <option value="">All companies</option>
                {companyOptions.map((company) => (
                  <option key={company.id} value={company.displayName}>
                    {company.displayName}
                  </option>
                ))}
              </TextField>
              <Button variant="outlined" onClick={() => void loadData(companyFilter || undefined)}>
                Refresh
              </Button>
            </Stack>

            {loading ? (
              <LoadingPanel label="Loading distribution lists" />
            ) : groupedLists.length === 0 ? (
              <Alert severity="warning">
                No distribution lists exist yet. Create one on the right, then select it from the <Link to="/reports">Reports</Link> page when queuing a report.
              </Alert>
            ) : (
              <Stack spacing={2}>
                {groupedLists.map(([companyName, companyLists]) => (
                  <Paper key={companyName} variant="outlined" sx={{ p: 2 }}>
                    <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap" sx={{ mb: 1.5 }}>
                      <Typography variant="h6" sx={{ mr: 'auto' }}>
                        {companyName}
                      </Typography>
                      <Chip label={`${companyLists.length} list${companyLists.length === 1 ? '' : 's'}`} size="small" variant="outlined" />
                    </Stack>

                    <Stack spacing={1.5}>
                      {companyLists.map((list) => (
                        <Paper key={list.id} variant="outlined" sx={{ p: 2, borderColor: editingId === list.id ? 'primary.main' : 'divider' }}>
                          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} useFlexGap flexWrap="wrap" sx={{ mb: 1 }}>
                            <Typography variant="subtitle1" sx={{ mr: 'auto' }}>
                              {list.name}
                            </Typography>
                            <Chip label={list.isActive ? 'Active' : 'Inactive'} size="small" color={list.isActive ? 'success' : 'default'} />
                            <Chip label={`${list.activeRecipientCount} active recipient${list.activeRecipientCount === 1 ? '' : 's'}`} size="small" variant="outlined" />
                          </Stack>
                          {list.description ? (
                            <Typography color="text.secondary" sx={{ mb: 1 }}>
                              {list.description}
                            </Typography>
                          ) : null}
                          <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', mb: 1 }}>
                            {list.recipients.length > 0
                              ? list.recipients.map((recipient) => recipient.endpoint).join(', ')
                              : 'No recipients configured yet.'}
                          </Typography>
                          <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
                            Updated {new Date(list.updatedAt).toLocaleString()} by {list.updatedBy ?? list.createdBy}
                          </Typography>
                          <Stack direction="row" spacing={1}>
                            <Button size="small" variant="outlined" onClick={() => beginEdit(list)}>
                              Edit
                            </Button>
                            <Button size="small" color="error" onClick={() => void removeList(list)}>
                              Delete
                            </Button>
                          </Stack>
                        </Paper>
                      ))}
                    </Stack>
                  </Paper>
                ))}
              </Stack>
            )}
          </Grid>

          <Grid item xs={12} lg={5}>
            <Paper component="form" variant="outlined" sx={{ p: 2 }} onSubmit={submit}>
              <Typography variant="h6" sx={{ mb: 1 }}>
                {editingId ? 'Edit distribution list' : 'Create distribution list'}
              </Typography>
              <Typography color="text.secondary" sx={{ mb: 2 }}>
                Use the same company value you enter on the Reports page so the correct lists appear for report delivery selection.
              </Typography>
              <Stack spacing={2}>
                <TextField
                  label="Company"
                  value={formState.companyName}
                  onChange={(event) => setFormState((current) => ({ ...current, companyName: event.target.value }))}
                  required
                  fullWidth
                />
                <TextField
                  label="List name"
                  value={formState.name}
                  onChange={(event) => setFormState((current) => ({ ...current, name: event.target.value }))}
                  required
                  fullWidth
                />
                <TextField
                  label="Description"
                  value={formState.description}
                  onChange={(event) => setFormState((current) => ({ ...current, description: event.target.value }))}
                  fullWidth
                  multiline
                  minRows={2}
                />
                <TextField
                  select
                  SelectProps={{ native: true }}
                  InputLabelProps={{ shrink: true }}
                  label="List status"
                  value={formState.isActive ? 'active' : 'inactive'}
                  onChange={(event) => setFormState((current) => ({ ...current, isActive: event.target.value === 'active' }))}
                  fullWidth
                >
                  <option value="active">Active</option>
                  <option value="inactive">Inactive</option>
                </TextField>
                <TextField
                  label="Recipient email addresses"
                  value={formState.recipientsText}
                  onChange={(event) => setFormState((current) => ({ ...current, recipientsText: event.target.value }))}
                  placeholder={`one.email@example.com\ntwo.email@example.com`}
                  fullWidth
                  multiline
                  minRows={8}
                  helperText="One email address per line. This first version keeps the UI email-focused while the backend preserves channel metadata for future endpoints."
                />
                <Stack direction="row" spacing={1}>
                  <Button type="submit" variant="contained" disabled={saving}>
                    {editingId ? 'Save changes' : 'Create list'}
                  </Button>
                  <Button type="button" variant="outlined" onClick={resetForm} disabled={saving}>
                    Clear
                  </Button>
                </Stack>
              </Stack>
            </Paper>
          </Grid>
        </Grid>

        <Box sx={{ mt: 4 }}>
          <Typography variant="h6" sx={{ mb: 1 }}>
            Recent delivery history
          </Typography>
          <Typography color="text.secondary" sx={{ mb: 2 }}>
            Review the latest per-list outcomes here. For a specific report execution, the <Link to="/jobs">Jobs</Link> page also shows linked delivery records when a render job is selected.
          </Typography>
          <UpmsDataTable
            columns={[
              { key: 'companyName', label: 'Company' },
              { key: 'distributionListName', label: 'List' },
              { key: 'status', label: 'Status' },
              { key: 'recipientCount', label: 'Recipients', type: 'number' },
              { key: 'attemptCount', label: 'Attempts', type: 'number' },
              { key: 'artifactFileName', label: 'Artifact' },
              { key: 'completedAt', label: 'Completed', getSortValue: (row) => String(row.completedAtSortValue ?? '') },
            ]}
            rows={deliveries.map((delivery) => ({
              id: delivery.id,
              companyName: delivery.companyName,
              distributionListName: delivery.distributionListName,
              status: delivery.status,
              recipientCount: delivery.recipientCount,
              attemptCount: delivery.attemptCount,
              artifactFileName: delivery.artifactFileName ?? '',
              completedAt: delivery.completedAt ? new Date(delivery.completedAt).toLocaleString() : '—',
              completedAtSortValue: delivery.completedAt ?? delivery.createdAt,
            }))}
          />
        </Box>
      </PageSection>
    </>
  );
}

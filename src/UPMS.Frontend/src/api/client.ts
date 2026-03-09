import axios from 'axios';
import {
  BackgroundJob,
  CanonicalField,
  FieldChange,
  ItsmSourceDefinition,
  ItsmSourceSummary,
  ReportPlugin,
  Snapshot,
  Ticket,
  IngestResult,
} from './types';

const api = axios.create({
  baseURL: import.meta.env.VITE_UPMS_API_BASE_URL ?? '/api/v1',
});

export const upmsApi = {
  async getCanonicalFields() {
    const { data } = await api.get<CanonicalField[]>('/canonical-fields');
    return data;
  },
  async getItsmSources() {
    const { data } = await api.get<ItsmSourceSummary[]>('/itsm-sources');
    return data;
  },
  async getItsmSource(name: string) {
    const { data } = await api.get<ItsmSourceDefinition>(`/itsm-sources/${encodeURIComponent(name)}`);
    return data;
  },
  async getSnapshots(params?: { itsmSource?: string; company?: string }) {
    const { data } = await api.get<Snapshot[]>('/snapshots', { params });
    return data;
  },
  async getSnapshotTickets(id: string) {
    const { data } = await api.get<Ticket[]>(`/snapshots/${id}/tickets`);
    return data;
  },
  async getTickets(params: { itsmSource: string; company?: string; asOf?: string; fieldName?: string; fieldValue?: string }) {
    const { data } = await api.get<Ticket[]>('/tickets', { params });
    return data;
  },
  async getTicket(company: string, ticketKey: string, itsmSource?: string, asOf?: string) {
    const { data } = await api.get<Ticket>(`/tickets/${encodeURIComponent(company)}/${encodeURIComponent(ticketKey)}`, {
      params: { itsmSource, asOf },
    });
    return data;
  },
  async getTicketHistory(company: string, ticketKey: string) {
    const { data } = await api.get<FieldChange[]>(`/tickets/${encodeURIComponent(company)}/${encodeURIComponent(ticketKey)}/history`);
    return data;
  },
  async getReportPlugins() {
    const { data } = await api.get<ReportPlugin[]>('/reports/plugins');
    return data;
  },
  async queueReport(payload: { pluginId: string; parameters: Record<string, string> }) {
    const { data } = await api.post<BackgroundJob>('/jobs/report-execution', payload);
    return data;
  },
  async queueSnapshotIngest(formData: FormData) {
    const { data } = await api.post<BackgroundJob>('/jobs/snapshot-ingest', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return data;
  },
  async ingestSnapshotSync(formData: FormData) {
    const { data } = await api.post<IngestResult>('/snapshots/ingest', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return data;
  },
  async getJobs() {
    const { data } = await api.get<BackgroundJob[]>('/jobs');
    return data;
  },
  async getJob(id: string) {
    const { data } = await api.get<BackgroundJob>(`/jobs/${id}`);
    return data;
  },
};

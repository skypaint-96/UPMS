import axios from 'axios';
import {
  BackgroundJob,
  BulkJobSubmission,
  CanonicalField,
  FieldChange,
  FileSharePollingRunResponse,
  FileSharePollingSettings,
  FileSharePollingSource,
  ItsmSourceDefinition,
  ItsmSourceSummary,
  ReportPlugin,
  ReportTemplate,
  ReportTemplateDetail,
  ReportTemplateType,
  Snapshot,
  Ticket,
  IngestResult,
} from './types';

const apiBaseUrl = import.meta.env.VITE_UPMS_API_BASE_URL ?? '/api/v1';

const api = axios.create({
  baseURL: apiBaseUrl,
});

function buildApiUrl(path: string) {
  return `${apiBaseUrl.replace(/\/$/, '')}${path.startsWith('/') ? path : `/${path}`}`;
}

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
  async createItsmSource(payload: { name: string; displayLabel: string }) {
    const { data } = await api.post<ItsmSourceSummary>('/itsm-sources', payload);
    return data;
  },
  async deleteItsmSource(name: string) {
    await api.delete(`/itsm-sources/${encodeURIComponent(name)}`);
  },
  async upsertItsmMapping(sourceName: string, sourceFieldName: string, payload: { canonicalFieldName: string; isRequired: boolean }) {
    await api.put(`/itsm-sources/${encodeURIComponent(sourceName)}/mappings/${encodeURIComponent(sourceFieldName)}`, payload);
  },
  async deleteItsmMapping(sourceName: string, sourceFieldName: string) {
    await api.delete(`/itsm-sources/${encodeURIComponent(sourceName)}/mappings/${encodeURIComponent(sourceFieldName)}`);
  },
  async getFileSharePollingSettings() {
    const { data } = await api.get<FileSharePollingSettings>('/file-share-polling/settings');
    return data;
  },
  async getFileSharePollingSources() {
    const { data } = await api.get<FileSharePollingSource[]>('/file-share-polling-sources');
    return data;
  },
  async createFileSharePollingSource(payload: {
    name: string;
    enabled: boolean;
    watchedPath: string;
    filePatterns: string[];
    archivePath: string;
    errorPath: string;
    itsmSource: string;
    pollIntervalSeconds?: number | null;
    maxFilesPerCycle?: number | null;
    stableFileAgeSeconds?: number | null;
  }) {
    const { data } = await api.post<FileSharePollingSource>('/file-share-polling-sources', payload);
    return data;
  },
  async updateFileSharePollingSource(id: string, payload: {
    name: string;
    enabled: boolean;
    watchedPath: string;
    filePatterns: string[];
    archivePath: string;
    errorPath: string;
    itsmSource: string;
    pollIntervalSeconds?: number | null;
    maxFilesPerCycle?: number | null;
    stableFileAgeSeconds?: number | null;
  }) {
    const { data } = await api.put<FileSharePollingSource>(`/file-share-polling-sources/${encodeURIComponent(id)}`, payload);
    return data;
  },
  async deleteFileSharePollingSource(id: string) {
    await api.delete(`/file-share-polling-sources/${encodeURIComponent(id)}`);
  },
  async runFileSharePollingSource(id: string) {
    const { data } = await api.post<FileSharePollingRunResponse>(`/file-share-polling-sources/${encodeURIComponent(id)}/run`);
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
  async getReportTemplateTypes() {
    const { data } = await api.get<ReportTemplateType[]>('/report-template-types');
    return data;
  },
  async getReportTemplates() {
    const { data } = await api.get<ReportTemplate[]>('/report-templates');
    return data;
  },
  async getReportTemplate(id: string) {
    const { data } = await api.get<ReportTemplateDetail>(`/report-templates/${encodeURIComponent(id)}`);
    return data;
  },
  async uploadReportTemplate(formData: FormData) {
    const { data } = await api.post<ReportTemplate>('/report-templates', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return data;
  },
  async updateReportTemplate(id: string, formData: FormData) {
    const { data } = await api.put<ReportTemplate>(`/report-templates/${encodeURIComponent(id)}`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return data;
  },
  async deleteReportTemplate(id: string) {
    await api.delete(`/report-templates/${encodeURIComponent(id)}`);
  },
  getReportTemplateDownloadUrl(id: string) {
    return buildApiUrl(`/report-templates/${encodeURIComponent(id)}/download`);
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
  async queueBulkSnapshotIngest(formData: FormData) {
    const { data } = await api.post<BulkJobSubmission>('/jobs/snapshot-ingest/bulk', formData, {
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

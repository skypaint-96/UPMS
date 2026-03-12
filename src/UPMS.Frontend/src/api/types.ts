export type CanonicalField = {
  name: string;
  dataType: string;
  isSystemRequired: boolean;
};

export type ItsmSourceSummary = {
  id: number;
  name: string;
  displayLabel: string;
};

export type ItsmFieldMapping = {
  itsmSource: string;
  sourceFieldName: string;
  canonicalFieldName: string;
  isRequired: boolean;
};

export type ItsmSourceDefinition = ItsmSourceSummary & {
  mappings: ItsmFieldMapping[];
};

export type Snapshot = {
  id: string;
  itsmSource: string;
  snapshotDate: string;
  uploadedBy: string;
  uploadedAt: string;
  uploadMetadata?: string | null;
};

export type Ticket = {
  ticketKey: string;
  companyName: string;
  itsmSource: string;
  observedAt: string;
  snapshotId: string;
  snapshotDate: string;
  fields: Record<string, string | null>;
};

export type FieldChange = {
  id: number;
  companyName: string;
  ticketKey: string;
  fieldName: string;
  canonicalFieldName?: string | null;
  displayFieldName: string;
  fieldValue?: string | null;
  observedAt: string;
  snapshotId: string;
  registeredDataType?: string | null;
  isValueValid?: boolean | null;
};

export type ReportParameter = {
  key: string;
  displayName: string;
  type: string;
  isRequired: boolean;
  description?: string | null;
  placeholder?: string | null;
  canonicalFieldName?: string | null;
  options: string[];
};

export type ReportPlugin = {
  pluginId: string;
  displayName: string;
  description: string;
  parameters: ReportParameter[];
};

export type ReportTemplateKind = 'Email' | 'Document' | 'Spreadsheet' | 'Presentation' | 'Generic';

export type ReportTemplateType = {
  typeId: string;
  displayName: string;
  kind: ReportTemplateKind;
  primaryExtension: string;
  extensions: string[];
  contentType: string;
  supportsInlineEdit: boolean;
  isTextLike: boolean;
  isOoxmlPackage: boolean;
  description?: string | null;
  authoringGuidance?: string | null;
  starterTemplateDisplayName?: string | null;
  starterTemplateDescription?: string | null;
  defaultSubjectTemplate?: string | null;
};

export type ReportTemplate = {
  id: string;
  displayName: string;
  kind: ReportTemplateKind;
  description?: string | null;
  subjectTemplate?: string | null;
  fileName: string;
  extension: string;
  contentType: string;
  uploadedAt: string;
  uploadedBy: string;
  updatedAt: string;
  updatedBy?: string | null;
  templateTypeId?: string | null;
  typeDisplayName?: string | null;
  supportsInlineEdit: boolean;
  isStarterTemplate: boolean;
};

export type ReportTemplateDetail = ReportTemplate & {
  authoringGuidance?: string | null;
  editableTextContent?: string | null;
};

export type BackgroundJob = {
  id: string;
  jobType: string;
  status: string;
  requestedBy?: string | null;
  createdAt: string;
  startedAt?: string | null;
  completedAt?: string | null;
  errorMessage?: string | null;
  resultJson?: string | null;
  outputFileName?: string | null;
  outputContentType?: string | null;
  downloadUrl?: string | null;
};

export type BulkJobSubmission = {
  queuedCount: number;
  jobs: BackgroundJob[];
};

export type IngestResult = {
  success: boolean;
  snapshotId: string;
  ticketsIngested: number;
  fieldChangesRecorded: number;
  errorMessage?: string | null;
  warnings: string[];
};

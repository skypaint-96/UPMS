-- Migration 003: ITSM field name canonical mapping table
-- Maps source-specific field names to canonical names used throughout UPMS.
-- When the same semantic field is named differently across ITSM sources,
-- this table normalises it to a single canonical name.

CREATE TABLE IF NOT EXISTS itsm_field_mapping (
    itsm_source         VARCHAR(100)    NOT NULL,
    source_field_name   VARCHAR(255)    NOT NULL,
    canonical_field_name VARCHAR(255)   NOT NULL,
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    PRIMARY KEY (itsm_source, source_field_name)
);

-- Index for bulk lookups by ITSM source
CREATE INDEX IF NOT EXISTS idx_itsm_field_mapping_source 
    ON itsm_field_mapping (itsm_source);

-- Seed: ServiceNow canonical field mappings
INSERT INTO itsm_field_mapping (itsm_source, source_field_name, canonical_field_name) VALUES
    ('servicenow', 'incident_state',    'Status'),
    ('servicenow', 'assigned_to',       'Assignee'),
    ('servicenow', 'short_description', 'Summary'),
    ('servicenow', 'description',       'Description'),
    ('servicenow', 'priority',          'Priority'),
    ('servicenow', 'category',          'Category'),
    ('servicenow', 'sys_created_on',    'CreatedAt'),
    ('servicenow', 'sys_updated_on',    'UpdatedAt'),
    ('servicenow', 'caller_id',         'Reporter'),
    ('servicenow', 'close_notes',       'ResolutionNotes')
ON CONFLICT (itsm_source, source_field_name) DO UPDATE 
    SET canonical_field_name = EXCLUDED.canonical_field_name,
        updated_at = NOW();

-- Seed: Jira canonical field mappings
INSERT INTO itsm_field_mapping (itsm_source, source_field_name, canonical_field_name) VALUES
    ('jira', 'status',       'Status'),
    ('jira', 'assignee',     'Assignee'),
    ('jira', 'summary',      'Summary'),
    ('jira', 'description',  'Description'),
    ('jira', 'priority',     'Priority'),
    ('jira', 'issuetype',    'Category'),
    ('jira', 'created',      'CreatedAt'),
    ('jira', 'updated',      'UpdatedAt'),
    ('jira', 'reporter',     'Reporter'),
    ('jira', 'resolution',   'ResolutionNotes')
ON CONFLICT (itsm_source, source_field_name) DO UPDATE 
    SET canonical_field_name = EXCLUDED.canonical_field_name,
        updated_at = NOW();

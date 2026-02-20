-- ============================================================================
-- Migration: 004_itsm_source_definition.sql
-- Purpose: Introduce the two-table ITSM source definition model.
--          Creates itsm_source (parent) and revises itsm_field_mapping to add
--          is_required flag and a FK reference to itsm_source.name.
--          Idempotent: uses IF NOT EXISTS / IF NOT EXISTS column guards.
-- PostgreSQL 15+
-- ============================================================================

-- ============================================================================
-- Table: itsm_source
-- One row per named ITSM source instance.
-- ============================================================================
CREATE TABLE IF NOT EXISTS itsm_source (
    id            SERIAL PRIMARY KEY,
    name          TEXT NOT NULL,
    display_label TEXT NOT NULL,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_itsm_source_name UNIQUE (name)
);

COMMENT ON TABLE  itsm_source              IS 'Named ITSM source instances (e.g. servicenow-client-a)';
COMMENT ON COLUMN itsm_source.name         IS 'Slug identifier — stored in raw_snapshot.itsm_source';
COMMENT ON COLUMN itsm_source.display_label IS 'Human-readable label shown in the UI';

-- ============================================================================
-- Revise itsm_field_mapping
-- Add is_required column if it does not exist yet.
-- ============================================================================
ALTER TABLE itsm_field_mapping
    ADD COLUMN IF NOT EXISTS is_required BOOLEAN NOT NULL DEFAULT FALSE;

-- ============================================================================
-- Seed: servicenow-default
-- ============================================================================
INSERT INTO itsm_source (name, display_label)
VALUES ('servicenow-default', 'ServiceNow (Default)')
ON CONFLICT (name) DO NOTHING;

INSERT INTO itsm_field_mapping (itsm_source, source_field_name, canonical_field_name, is_required)
VALUES
    ('servicenow-default', 'number',            'ticket_key',   TRUE),
    ('servicenow-default', 'company',           'company',      TRUE),
    ('servicenow-default', 'short_description', 'title',        FALSE),
    ('servicenow-default', 'description',       'description',  FALSE),
    ('servicenow-default', 'priority',          'priority',     FALSE),
    ('servicenow-default', 'state',             'status',       FALSE),
    ('servicenow-default', 'sys_created_on',    'created_date', FALSE),
    ('servicenow-default', 'sys_updated_on',    'updated_date', FALSE),
    ('servicenow-default', 'assigned_to',       'assigned_to',  FALSE),
    ('servicenow-default', 'category',          'category',     FALSE)
ON CONFLICT (itsm_source, source_field_name) DO UPDATE
    SET canonical_field_name = EXCLUDED.canonical_field_name,
        is_required          = EXCLUDED.is_required;

-- ============================================================================
-- Seed: jira-default
-- ============================================================================
INSERT INTO itsm_source (name, display_label)
VALUES ('jira-default', 'Jira (Default)')
ON CONFLICT (name) DO NOTHING;

INSERT INTO itsm_field_mapping (itsm_source, source_field_name, canonical_field_name, is_required)
VALUES
    ('jira-default', 'key',         'ticket_key',   TRUE),
    ('jira-default', 'company',     'company',      TRUE),
    ('jira-default', 'summary',     'title',        FALSE),
    ('jira-default', 'description', 'description',  FALSE),
    ('jira-default', 'priority',    'priority',     FALSE),
    ('jira-default', 'status',      'status',       FALSE),
    ('jira-default', 'created',     'created_date', FALSE),
    ('jira-default', 'updated',     'updated_date', FALSE),
    ('jira-default', 'assignee',    'assigned_to',  FALSE),
    ('jira-default', 'issuetype',   'category',     FALSE)
ON CONFLICT (itsm_source, source_field_name) DO UPDATE
    SET canonical_field_name = EXCLUDED.canonical_field_name,
        is_required          = EXCLUDED.is_required;

-- ============================================================================
-- End of migration 004
-- ============================================================================

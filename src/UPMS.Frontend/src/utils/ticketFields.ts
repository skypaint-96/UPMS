import { ItsmFieldMapping, Ticket } from '../api/types';

function normalizeText(value: string | null | undefined) {
  return (value ?? '').trim();
}

function caseInsensitiveCompare(left: string, right: string) {
  return left.localeCompare(right, undefined, { sensitivity: 'base' });
}

export function getUniqueSortedStrings(values: Array<string | null | undefined>) {
  const distinct = new Map<string, string>();

  values.forEach((value) => {
    const normalized = normalizeText(value);
    if (!normalized) {
      return;
    }

    const key = normalized.toLowerCase();
    if (!distinct.has(key)) {
      distinct.set(key, normalized);
    }
  });

  return Array.from(distinct.values()).sort(caseInsensitiveCompare);
}

export function getTicketFieldValue(ticket: Ticket, fieldName: string) {
  const normalizedFieldName = normalizeText(fieldName);
  if (!normalizedFieldName) {
    return '';
  }

  if (ticket.fields[normalizedFieldName] != null) {
    return String(ticket.fields[normalizedFieldName] ?? '');
  }

  const matchedKey = Object.keys(ticket.fields).find((key) => key.toLowerCase() === normalizedFieldName.toLowerCase());
  return matchedKey ? String(ticket.fields[matchedKey] ?? '') : '';
}

export function getTicketNumber(ticket: Ticket) {
  return getTicketFieldValue(ticket, 'Number') || getTicketFieldValue(ticket, 'ticket_number') || ticket.ticketKey;
}

export function getTicketState(ticket: Ticket) {
  return getTicketFieldValue(ticket, 'State') || getTicketFieldValue(ticket, 'status') || getTicketFieldValue(ticket, 'state');
}

export function getTicketFieldOptions(tickets: Ticket[], mappings: ItsmFieldMapping[] = []) {
  const preferredFieldNames = ['State', 'status'];
  const preferredKeys = new Set(preferredFieldNames.map((name) => name.toLowerCase()));
  const distinct = new Map<string, string>();

  const addOption = (value: string | null | undefined) => {
    const normalized = normalizeText(value);
    if (!normalized) {
      return;
    }

    const key = normalized.toLowerCase();
    if (!distinct.has(key)) {
      distinct.set(key, normalized);
    }
  };

  preferredFieldNames.forEach(addOption);
  mappings.forEach((mapping) => {
    addOption(mapping.canonicalFieldName);
    addOption(mapping.sourceFieldName);
  });
  tickets.forEach((ticket) => {
    Object.keys(ticket.fields).forEach(addOption);
  });

  const allOptions = Array.from(distinct.values());
  const preferred = allOptions.filter((value) => preferredKeys.has(value.toLowerCase())).sort(caseInsensitiveCompare);
  const rest = allOptions.filter((value) => !preferredKeys.has(value.toLowerCase())).sort(caseInsensitiveCompare);

  return [...preferred, ...rest];
}

export function getTicketFieldValueSuggestions(tickets: Ticket[], fieldName: string) {
  const normalizedFieldName = normalizeText(fieldName);
  if (!normalizedFieldName) {
    return [];
  }

  return getUniqueSortedStrings(tickets.map((ticket) => getTicketFieldValue(ticket, normalizedFieldName)));
}

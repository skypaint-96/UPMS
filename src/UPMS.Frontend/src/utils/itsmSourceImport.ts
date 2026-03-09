export type ImportedItsmMapping = {
  sourceFieldName: string;
  canonicalFieldName: string;
  isRequired: boolean;
};

export type ImportedItsmSourceDefinition = {
  name?: string;
  displayLabel?: string;
  mappings: ImportedItsmMapping[];
};

function normalizeKey(value: string) {
  return value.replace(/[^a-z0-9]/gi, '').toLowerCase();
}

function coerceBoolean(value: unknown) {
  if (typeof value === 'boolean') return value;
  if (typeof value === 'number') return value !== 0;
  const text = String(value ?? '').trim().toLowerCase();
  return text === 'true' || text === '1' || text === 'yes' || text === 'y' || text === 'required';
}

function mapRow(row: Record<string, unknown>): ImportedItsmMapping | null {
  const lookup = new Map<string, unknown>();
  Object.entries(row).forEach(([key, value]) => lookup.set(normalizeKey(key), value));

  const sourceFieldName = String(
    lookup.get('sourcefieldname') ?? lookup.get('sourcefield') ?? lookup.get('fieldname') ?? lookup.get('field') ?? '',
  ).trim();
  const canonicalFieldName = String(
    lookup.get('canonicalfieldname') ?? lookup.get('canonicalfield') ?? lookup.get('canonical') ?? lookup.get('targetfield') ?? '',
  ).trim();
  const isRequired = coerceBoolean(lookup.get('isrequired') ?? lookup.get('required') ?? false);

  if (!sourceFieldName || !canonicalFieldName) {
    return null;
  }

  return {
    sourceFieldName,
    canonicalFieldName,
    isRequired,
  };
}

function splitCsvLine(line: string) {
  const cells: string[] = [];
  let current = '';
  let inQuotes = false;

  for (let i = 0; i < line.length; i += 1) {
    const char = line[i];
    const next = line[i + 1];

    if (char === '"') {
      if (inQuotes && next === '"') {
        current += '"';
        i += 1;
      } else {
        inQuotes = !inQuotes;
      }
      continue;
    }

    if (char === ',' && !inQuotes) {
      cells.push(current.trim());
      current = '';
      continue;
    }

    current += char;
  }

  cells.push(current.trim());
  return cells;
}

function parseCsv(text: string): ImportedItsmSourceDefinition {
  const lines = text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0);

  if (lines.length < 2) {
    throw new Error('The CSV file must include a header row and at least one mapping row.');
  }

  const headers = splitCsvLine(lines[0]);
  const mappings = lines
    .slice(1)
    .map((line) => {
      const values = splitCsvLine(line);
      const row: Record<string, string> = {};
      headers.forEach((header, index) => {
        row[header] = values[index] ?? '';
      });
      return mapRow(row);
    })
    .filter((row): row is ImportedItsmMapping => row !== null);

  if (mappings.length === 0) {
    throw new Error('No usable mappings were found in the CSV file.');
  }

  return { mappings };
}

function parseJson(text: string): ImportedItsmSourceDefinition {
  const value = JSON.parse(text) as unknown;

  if (Array.isArray(value)) {
    const mappings = value.map((item) => mapRow((item ?? {}) as Record<string, unknown>)).filter((row): row is ImportedItsmMapping => row !== null);
    if (mappings.length === 0) {
      throw new Error('The JSON array did not contain any usable mappings.');
    }

    return { mappings };
  }

  if (!value || typeof value !== 'object') {
    throw new Error('The JSON file must contain either an array of mappings or an object with a mappings array.');
  }

  const objectValue = value as Record<string, unknown>;
  const mappings = Array.isArray(objectValue.mappings)
    ? objectValue.mappings.map((item) => mapRow((item ?? {}) as Record<string, unknown>)).filter((row): row is ImportedItsmMapping => row !== null)
    : [];

  if (mappings.length === 0) {
    throw new Error('The JSON file did not contain any usable mappings.');
  }

  return {
    name: String(objectValue.name ?? objectValue.sourceName ?? objectValue.itsmSource ?? '').trim() || undefined,
    displayLabel: String(objectValue.displayLabel ?? objectValue.displayName ?? objectValue.label ?? '').trim() || undefined,
    mappings,
  };
}

export function parseItsmSourceImport(fileName: string, text: string): ImportedItsmSourceDefinition {
  if (fileName.toLowerCase().endsWith('.json')) {
    return parseJson(text);
  }

  if (fileName.toLowerCase().endsWith('.csv')) {
    return parseCsv(text);
  }

  try {
    return parseJson(text);
  } catch {
    return parseCsv(text);
  }
}

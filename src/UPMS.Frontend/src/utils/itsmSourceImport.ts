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

function readSourceFieldName(lookup: Map<string, unknown>) {
  return String(
    lookup.get('sourcefieldname')
      ?? lookup.get('sourcefield')
      ?? lookup.get('sourcecolumn')
      ?? lookup.get('sourcecolumnname')
      ?? lookup.get('csvcolumn')
      ?? lookup.get('fieldname')
      ?? lookup.get('rawfieldname')
      ?? lookup.get('field')
      ?? '',
  ).trim();
}

function readCanonicalFieldName(lookup: Map<string, unknown>) {
  return String(
    lookup.get('canonicalfieldname')
      ?? lookup.get('canonicalfield')
      ?? lookup.get('canonicalname')
      ?? lookup.get('displayfieldname')
      ?? lookup.get('displayname')
      ?? lookup.get('normalizedfieldname')
      ?? lookup.get('canonical')
      ?? lookup.get('targetfield')
      ?? '',
  ).trim();
}

function mapRow(row: Record<string, unknown>): ImportedItsmMapping | null {
  const lookup = new Map<string, unknown>();
  Object.entries(row).forEach(([key, value]) => lookup.set(normalizeKey(key), value));

  const sourceFieldName = readSourceFieldName(lookup);
  const canonicalFieldName = readCanonicalFieldName(lookup);
  const isRequired = coerceBoolean(
    lookup.get('isrequired')
      ?? lookup.get('required')
      ?? lookup.get('requiredfield')
      ?? lookup.get('mandatory')
      ?? false,
  );

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

function getObjectValue(objectValue: Record<string, unknown>, ...keys: string[]) {
  for (const key of keys) {
    if (key in objectValue) {
      return objectValue[key];
    }
  }
  return undefined;
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
  const sourceObject = (getObjectValue(objectValue, 'source', 'Source') ?? {}) as Record<string, unknown>;
  const rawMappings = getObjectValue(objectValue, 'mappings', 'Mappings', 'fieldMappings', 'FieldMappings', 'mappingRows', 'MappingRows');
  const mappings = Array.isArray(rawMappings)
    ? rawMappings.map((item) => mapRow((item ?? {}) as Record<string, unknown>)).filter((row): row is ImportedItsmMapping => row !== null)
    : [];

  if (mappings.length === 0) {
    throw new Error('The JSON file did not contain any usable mappings.');
  }

  return {
    name: String(
      getObjectValue(objectValue, 'name', 'Name', 'sourceName', 'SourceName', 'itsmSource', 'ItsmSource')
        ?? getObjectValue(sourceObject, 'name', 'Name')
        ?? '',
    ).trim() || undefined,
    displayLabel: String(
      getObjectValue(objectValue, 'displayLabel', 'DisplayLabel', 'displayName', 'DisplayName', 'label', 'Label')
        ?? getObjectValue(sourceObject, 'displayLabel', 'DisplayLabel', 'displayName', 'DisplayName', 'label', 'Label')
        ?? '',
    ).trim() || undefined,
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

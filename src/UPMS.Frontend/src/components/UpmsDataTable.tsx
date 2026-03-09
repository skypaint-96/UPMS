import { CGIDataTable } from 'eds-react-app';

export type UpmsTableColumn = {
  key: string;
  label: string;
  type?: string;
};

export function UpmsDataTable({
  columns,
  rows,
  dataTestId,
}: {
  columns: UpmsTableColumn[];
  rows: Record<string, unknown>[];
  dataTestId?: string;
}) {
  return (
    <CGIDataTable
      columns={columns.map((column) => ({
        key: column.key,
        label: column.label,
        type: column.type ?? 'string',
      }))}
      rows={rows}
      showHeaderCheckbox={false}
      tableIndex={0}
      showPagination={true}
      onSelectedItemsChange={() => undefined}
      dataTestId={dataTestId}
    />
  );
}

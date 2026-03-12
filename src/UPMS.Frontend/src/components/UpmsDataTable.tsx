import { ReactNode, isValidElement, useEffect, useMemo, useState } from 'react';
import {
  Box,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TableSortLabel,
  Typography,
} from '@mui/material';

export type UpmsTableColumn = {
  key: string;
  label: string;
  type?: string;
  alignment?: 'left' | 'right' | 'center';
  sortable?: boolean;
  getSortValue?: (row: Record<string, unknown>) => string | number | null | undefined;
};

type SortDirection = 'asc' | 'desc';

function getDefaultAlignment(column: UpmsTableColumn): 'left' | 'right' | 'center' {
  if (column.alignment) {
    return column.alignment;
  }

  if (column.type === 'number') {
    return 'right';
  }

  if (column.type === 'icon') {
    return 'center';
  }

  return 'left';
}

function normalizeSortValue(value: unknown) {
  if (value == null) {
    return '';
  }

  if (typeof value === 'number') {
    return value;
  }

  if (typeof value === 'string') {
    return value;
  }

  if (value instanceof Date) {
    return value.getTime();
  }

  if (typeof value === 'boolean') {
    return value ? 1 : 0;
  }

  if (isValidElement(value)) {
    return '';
  }

  return String(value);
}

function compareValues(left: string | number, right: string | number) {
  if (typeof left === 'number' && typeof right === 'number') {
    return left - right;
  }

  return String(left).localeCompare(String(right), undefined, { numeric: true, sensitivity: 'base' });
}

function renderCellValue(value: unknown): ReactNode {
  if (value == null) {
    return '';
  }

  if (typeof value === 'boolean') {
    return value ? 'True' : 'False';
  }

  return value as ReactNode;
}

export function UpmsDataTable({
  columns,
  rows,
  dataTestId,
}: {
  columns: UpmsTableColumn[];
  rows: Record<string, unknown>[];
  dataTestId?: string;
}) {
  const [sortConfig, setSortConfig] = useState<{ key: string; direction: SortDirection } | null>(null);
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(10);

  useEffect(() => {
    const maxPage = Math.max(0, Math.ceil(rows.length / rowsPerPage) - 1);
    if (page > maxPage) {
      setPage(maxPage);
    }
  }, [page, rows.length, rowsPerPage]);

  const sortedRows = useMemo(() => {
    if (!sortConfig) {
      return rows;
    }

    const targetColumn = columns.find((column) => column.key === sortConfig.key);
    if (!targetColumn) {
      return rows;
    }

    const direction = sortConfig.direction === 'asc' ? 1 : -1;

    return [...rows].sort((leftRow, rightRow) => {
      const leftValue = normalizeSortValue(targetColumn.getSortValue ? targetColumn.getSortValue(leftRow) : leftRow[targetColumn.key]);
      const rightValue = normalizeSortValue(targetColumn.getSortValue ? targetColumn.getSortValue(rightRow) : rightRow[targetColumn.key]);
      return compareValues(leftValue, rightValue) * direction;
    });
  }, [columns, rows, sortConfig]);

  const pagedRows = useMemo(() => {
    const start = page * rowsPerPage;
    return sortedRows.slice(start, start + rowsPerPage);
  }, [page, rowsPerPage, sortedRows]);

  const handleSort = (column: UpmsTableColumn) => {
    if (column.sortable === false) {
      return;
    }

    setSortConfig((current) => {
      if (!current || current.key !== column.key) {
        return { key: column.key, direction: 'asc' };
      }

      return {
        key: column.key,
        direction: current.direction === 'asc' ? 'desc' : 'asc',
      };
    });
  };

  return (
    <Box>
      <TableContainer component={Paper} variant="outlined">
        <Table data-testid={dataTestId} size="small">
          <TableHead>
            <TableRow>
              {columns.map((column) => {
                const isActive = sortConfig?.key === column.key;
                const alignment = getDefaultAlignment(column);
                return (
                  <TableCell key={column.key} align={alignment}>
                    {column.sortable === false ? (
                      <Typography variant="subtitle2">{column.label}</Typography>
                    ) : (
                      <TableSortLabel
                        active={isActive}
                        direction={isActive ? sortConfig?.direction ?? 'asc' : 'asc'}
                        onClick={() => handleSort(column)}
                      >
                        {column.label}
                      </TableSortLabel>
                    )}
                  </TableCell>
                );
              })}
            </TableRow>
          </TableHead>
          <TableBody>
            {pagedRows.length === 0 ? (
              <TableRow>
                <TableCell colSpan={Math.max(columns.length, 1)}>
                  <Typography color="text.secondary">No rows to display.</Typography>
                </TableCell>
              </TableRow>
            ) : (
              pagedRows.map((row, index) => {
                const rowId = row.id ?? index;
                return (
                  <TableRow key={String(rowId)} hover>
                    {columns.map((column) => (
                      <TableCell key={column.key} align={getDefaultAlignment(column)}>
                        {renderCellValue(row[column.key])}
                      </TableCell>
                    ))}
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>
      {rows.length > 10 ? (
        <TablePagination
          component="div"
          count={rows.length}
          page={page}
          onPageChange={(_, newPage) => setPage(newPage)}
          rowsPerPage={rowsPerPage}
          onRowsPerPageChange={(event) => {
            setRowsPerPage(Number.parseInt(event.target.value, 10));
            setPage(0);
          }}
          rowsPerPageOptions={[10, 50, 100]}
          labelRowsPerPage="Items per page:"
        />
      ) : null}
    </Box>
  );
}

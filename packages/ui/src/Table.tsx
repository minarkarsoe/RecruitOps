import React from 'react';

export interface TableProps<T = any> extends React.TableHTMLAttributes<HTMLTableElement> {
  headers?: string[];
  data?: T[];
  renderRow?: (item: T, index: number) => React.ReactNode;
  children?: React.ReactNode;
  dense?: boolean;
}

export function Table<T = any>({
  headers,
  data,
  renderRow,
  children,
  dense = false,
  className = '',
  ...props
}: TableProps<T>) {
  if (headers && data && renderRow) {
    return (
      <div className="w-full overflow-x-auto border border-line-200 rounded-md bg-surface-0">
        <table className={`w-full text-left text-sm text-ink-900 border-collapse ${className}`} {...props}>
          <TableHeader>
            <TableRow hoverable={false}>
              {headers.map((header, idx) => (
                <TableHead key={idx} dense={dense}>
                  {header}
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {data.length === 0 ? (
              <TableRow hoverable={false}>
                <TableCell colSpan={headers.length} className="text-center text-ink-400 py-8">
                  No data available
                </TableCell>
              </TableRow>
            ) : (
              data.map((item, idx) => renderRow(item, idx))
            )}
          </TableBody>
        </table>
      </div>
    );
  }

  return (
    <div className="w-full overflow-x-auto border border-line-200 rounded-md bg-surface-0">
      <table className={`w-full text-left text-sm text-ink-900 border-collapse ${className}`} {...props}>
        {children}
      </table>
    </div>
  );
}

export function TableHeader({
  children,
  className = '',
  ...props
}: React.HTMLAttributes<HTMLTableSectionElement>) {
  return (
    <thead className={`bg-surface-50 border-b border-line-200 ${className}`} {...props}>
      {children}
    </thead>
  );
}

export function TableBody({
  children,
  className = '',
  ...props
}: React.HTMLAttributes<HTMLTableSectionElement>) {
  return (
    <tbody className={`divide-y divide-line-200 bg-surface-0 ${className}`} {...props}>
      {children}
    </tbody>
  );
}

export function TableFooter({
  children,
  className = '',
  ...props
}: React.HTMLAttributes<HTMLTableSectionElement>) {
  return (
    <tfoot className={`bg-surface-50 border-t border-line-200 font-medium ${className}`} {...props}>
      {children}
    </tfoot>
  );
}

export interface TableRowProps extends React.HTMLAttributes<HTMLTableRowElement> {
  selected?: boolean;
  hoverable?: boolean;
}

export function TableRow({
  children,
  selected = false,
  hoverable = true,
  className = '',
  ...props
}: TableRowProps) {
  return (
    <tr
      className={`border-b border-line-200 transition-colors ${
        selected ? 'bg-primary-100/50' : hoverable ? 'hover:bg-surface-50' : ''
      } ${className}`}
      {...props}
    >
      {children}
    </tr>
  );
}

export interface TableHeadProps extends React.ThHTMLAttributes<HTMLTableCellElement> {
  dense?: boolean;
}

export function TableHead({
  children,
  dense = false,
  className = '',
  ...props
}: TableHeadProps) {
  return (
    <th
      className={`font-semibold uppercase tracking-wider text-[11px] text-ink-600 align-middle ${
        dense ? 'px-3 py-2' : 'px-4 py-3'
      } ${className}`}
      {...props}
    >
      {children}
    </th>
  );
}

export interface TableCellProps extends React.TdHTMLAttributes<HTMLTableCellElement> {
  dense?: boolean;
}

export function TableCell({
  children,
  dense = false,
  className = '',
  ...props
}: TableCellProps) {
  return (
    <td
      className={`align-middle text-sm text-ink-900 ${
        dense ? 'px-3 py-2.5' : 'px-4 py-3.5'
      } ${className}`}
      {...props}
    >
      {children}
    </td>
  );
}

export function TableCaption({
  children,
  className = '',
  ...props
}: React.HTMLAttributes<HTMLTableCaptionElement>) {
  return (
    <caption className={`mt-4 text-xs text-ink-400 ${className}`} {...props}>
      {children}
    </caption>
  );
}

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
      <div className="w-full overflow-x-auto overflow-hidden rounded-lg border border-line bg-white">
        <table className={`w-full border-collapse text-left text-base text-ink-900 ${className}`} {...props}>
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
                <TableCell colSpan={headers.length} className="py-8 text-center text-ink-500">
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
    <div className="w-full overflow-x-auto overflow-hidden rounded-lg border border-line bg-white">
      <table className={`w-full border-collapse text-left text-base text-ink-900 ${className}`} {...props}>
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
    <thead className={`bg-canvas border-b border-line ${className}`} {...props}>
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
    <tbody className={`bg-white ${className}`} {...props}>
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
    <tfoot className={`bg-canvas border-t border-line font-medium ${className}`} {...props}>
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
      className={`border-b border-line transition-colors ${
        selected ? 'bg-brand-50/60' : hoverable ? 'hover:bg-canvas' : ''
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
      className={`align-middle text-sm font-medium text-ink-600 ${
        dense ? 'px-3 py-2' : 'px-4 py-2.5'
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
      className={`align-middle text-base text-ink-900 ${
        dense ? 'px-3 py-2' : 'px-4 py-3'
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
    <caption className={`mt-4 text-sm text-ink-500 ${className}`} {...props}>
      {children}
    </caption>
  );
}

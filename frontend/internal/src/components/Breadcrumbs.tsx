import { Link, useLocation } from 'react-router-dom';

export interface BreadcrumbItem {
  label: string;
  path?: string;
}

const ROUTE_LABELS: Record<string, string> = {
  requisitions: 'Requisitions',
  new: 'New',
  jobpostings: 'Job Postings',
  interviews: 'Interviews',
  inbox: 'Inbox',
  jdtemplates: 'JD Templates',
  scorecardtemplates: 'Scorecard Templates',
  approvalchains: 'Approval Chains',
  departments: 'Departments',
  users: 'Users',
  roles: 'Role Builder',
  edit: 'Edit',
};

export function getBreadcrumbsForPath(pathname: string): BreadcrumbItem[] {
  const segments = pathname.split('/').filter(Boolean);

  if (segments.length === 0) {
    return [{ label: 'Dashboard', path: '/' }];
  }

  const items: BreadcrumbItem[] = [{ label: 'Home', path: '/' }];
  let currentPath = '';

  for (let i = 0; i < segments.length; i++) {
    const seg = segments[i];
    currentPath += `/${seg}`;

    let label = ROUTE_LABELS[seg.toLowerCase()];

    if (!label) {
      // Dynamic ID segments fallback
      const prevSeg = segments[i - 1]?.toLowerCase();
      if (prevSeg === 'requisitions') {
        label = 'Requisition Details';
      } else if (prevSeg === 'jobpostings') {
        label = 'Posting Details';
      } else if (prevSeg === 'interviews') {
        label = 'Interview Round';
      } else {
        label = seg.replace(/[-_]/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());
      }
    } else if (seg.toLowerCase() === 'new') {
      const prevSeg = segments[i - 1]?.toLowerCase();
      if (prevSeg === 'requisitions') {
        label = 'New Requisition';
      } else if (prevSeg === 'jobpostings') {
        label = 'New Job Posting';
      } else {
        label = 'New';
      }
    }

    items.push({
      label,
      path: i === segments.length - 1 ? undefined : currentPath,
    });
  }

  return items;
}

export function Breadcrumbs() {
  const location = useLocation();
  const breadcrumbs = getBreadcrumbsForPath(location.pathname);

  return (
    <nav aria-label="Breadcrumb" className="flex items-center text-xs font-medium text-ink-500">
      <ol className="flex items-center space-x-1.5 flex-wrap">
        {breadcrumbs.map((item, index) => {
          const isLast = index === breadcrumbs.length - 1;

          return (
            <li key={index} className="flex items-center space-x-1.5">
              {index > 0 && (
                <svg
                  className="h-3.5 w-3.5 text-ink-300 shrink-0"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                  strokeWidth="2"
                  aria-hidden="true"
                >
                  <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
                </svg>
              )}
              {isLast || !item.path ? (
                <span
                  className="font-semibold text-ink-900 truncate max-w-[200px]"
                  aria-current={isLast ? 'page' : undefined}
                >
                  {item.label}
                </span>
              ) : (
                <Link
                  to={item.path}
                  className="text-ink-600 hover:text-primary-600 transition-colors truncate max-w-[150px]"
                >
                  {item.label}
                </Link>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

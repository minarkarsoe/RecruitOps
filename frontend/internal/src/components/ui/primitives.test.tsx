import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import {
  Sheet,
  Badge,
  Table,
  CommandPalette,
  Dialog,
  Tabs,
  Skeleton,
  Input,
  Select,
} from './index';

describe('UI Primitives Library (Milestone 1)', () => {
  describe('Sheet / Drawer', () => {
    it('renders title and content when open', () => {
      render(
        <Sheet isOpen={true} onClose={() => {}} title="Test Drawer Title">
          <div>Drawer Content Body</div>
        </Sheet>
      );
      expect(screen.getByText('Test Drawer Title')).toBeInTheDocument();
      expect(screen.getByText('Drawer Content Body')).toBeInTheDocument();
    });

    it('does not render when closed', () => {
      render(
        <Sheet isOpen={false} onClose={() => {}} title="Test Drawer Title">
          <div>Drawer Content Body</div>
        </Sheet>
      );
      expect(screen.queryByText('Test Drawer Title')).not.toBeInTheDocument();
    });

    it('triggers onClose when close button is clicked', () => {
      const handleClose = vi.fn();
      render(
        <Sheet isOpen={true} onClose={handleClose} title="Test Drawer Title">
          <div>Drawer Content Body</div>
        </Sheet>
      );
      const closeBtn = screen.getByLabelText('Close panel');
      fireEvent.click(closeBtn);
      expect(handleClose).toHaveBeenCalledTimes(1);
    });
  });

  describe('Badge', () => {
    it('renders with default variant', () => {
      render(<Badge>Default Badge</Badge>);
      expect(screen.getByText('Default Badge')).toBeInTheDocument();
    });

    it('renders client tier gold badge with crown icon', () => {
      const { container } = render(<Badge variant="gold">Gold Tier</Badge>);
      expect(screen.getByText('Gold Tier')).toBeInTheDocument();
      expect(container.querySelector('svg')).toBeInTheDocument();
    });

    it('renders cyan and teal brand variants', () => {
      render(<Badge variant="cyan">Cyan Badge</Badge>);
      render(<Badge variant="teal">Teal Badge</Badge>);
      expect(screen.getByText('Cyan Badge')).toBeInTheDocument();
      expect(screen.getByText('Teal Badge')).toBeInTheDocument();
    });
  });

  describe('Table', () => {
    it('renders high-density table from headers and data props', () => {
      const headers = ['Name', 'Role', 'Status'];
      const data = [{ id: 1, name: 'Alice', role: 'Engineer', status: 'Approved' }];
      render(
        <Table
          headers={headers}
          data={data}
          renderRow={(item) => (
            <tr key={item.id}>
              <td>{item.name}</td>
              <td>{item.role}</td>
              <td>{item.status}</td>
            </tr>
          )}
        />
      );

      expect(screen.getByText('Name')).toBeInTheDocument();
      expect(screen.getByText('Alice')).toBeInTheDocument();
      expect(screen.getByText('Engineer')).toBeInTheDocument();
    });
  });

  describe('CommandPalette', () => {
    it('renders search input and commands when open', () => {
      render(
        <CommandPalette
          isOpen={true}
          onClose={() => {}}
          placeholder="Search commands..."
        />
      );
      expect(screen.getByPlaceholderText('Search commands...')).toBeInTheDocument();
      expect(screen.getByText('Requisitions')).toBeInTheDocument();
    });

    it('filters commands when typing in search input', () => {
      render(
        <CommandPalette
          isOpen={true}
          onClose={() => {}}
          placeholder="Search commands..."
        />
      );
      const input = screen.getByPlaceholderText('Search commands...');
      fireEvent.change(input, { target: { value: 'Pipeline' } });
      expect(screen.getByText('Candidate Pipeline')).toBeInTheDocument();
      expect(screen.queryByText('Requisitions')).not.toBeInTheDocument();
    });

    it('triggers onSelectRoute when command is clicked', () => {
      const handleSelectRoute = vi.fn();
      render(
        <CommandPalette
          isOpen={true}
          onClose={() => {}}
          onSelectRoute={handleSelectRoute}
          placeholder="Search commands..."
        />
      );
      const item = screen.getByText('Requisitions');
      fireEvent.click(item);
      expect(handleSelectRoute).toHaveBeenCalledWith('/requisitions');
    });
  });

  describe('Dialog', () => {
    it('renders title and modal body when open', () => {
      render(
        <Dialog isOpen={true} onClose={() => {}} title="Confirm Action">
          <div>Are you sure you want to proceed?</div>
        </Dialog>
      );
      expect(screen.getByText('Confirm Action')).toBeInTheDocument();
      expect(screen.getByText('Are you sure you want to proceed?')).toBeInTheDocument();
    });

    it('does not render when closed', () => {
      render(
        <Dialog isOpen={false} onClose={() => {}} title="Confirm Action">
          <div>Are you sure?</div>
        </Dialog>
      );
      expect(screen.queryByText('Confirm Action')).not.toBeInTheDocument();
    });
  });

  describe('Tabs', () => {
    it('renders tabs and handles tab change', () => {
      const handleChange = vi.fn();
      const tabs = [
        { id: 'overview', label: 'Overview', count: 5 },
        { id: 'notes', label: 'Notes', count: 2 },
      ];
      render(<Tabs tabs={tabs} activeTab="overview" onChange={handleChange} />);

      expect(screen.getByText('Overview')).toBeInTheDocument();
      expect(screen.getByText('5')).toBeInTheDocument();

      const notesTab = screen.getByText('Notes');
      fireEvent.click(notesTab);
      expect(handleChange).toHaveBeenCalledWith('notes');
    });
  });

  describe('Skeleton', () => {
    it('renders loading skeleton placeholder', () => {
      const { container } = render(<Skeleton width={100} height={20} />);
      const skeleton = container.firstChild as HTMLElement;
      expect(skeleton).toHaveClass('animate-pulse');
    });
  });

  describe('Input', () => {
    it('renders label and handles typing', () => {
      const handleChange = vi.fn();
      render(
        <Input
          label="Email Address"
          placeholder="user@example.com"
          onChange={handleChange}
        />
      );

      expect(screen.getByLabelText('Email Address')).toBeInTheDocument();
      const input = screen.getByPlaceholderText('user@example.com');
      fireEvent.change(input, { target: { value: 'test@recruitops.io' } });
      expect(handleChange).toHaveBeenCalled();
    });

    it('displays error message when provided', () => {
      render(<Input label="Username" error="Username is required" />);
      expect(screen.getByText('Username is required')).toBeInTheDocument();
    });
  });

  describe('Select', () => {
    it('renders label and options', () => {
      const options = [
        { value: 'eng', label: 'Engineering' },
        { value: 'product', label: 'Product' },
      ];
      render(<Select label="Department" options={options} />);

      expect(screen.getByLabelText('Department')).toBeInTheDocument();
      expect(screen.getByText('Engineering')).toBeInTheDocument();
      expect(screen.getByText('Product')).toBeInTheDocument();
    });

    it('displays error message when provided', () => {
      render(<Select label="Role" error="Please select a role" options={[]} />);
      expect(screen.getByText('Please select a role')).toBeInTheDocument();
    });
  });
});

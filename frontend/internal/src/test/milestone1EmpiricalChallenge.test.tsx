import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
  Sheet,
  SheetHeader,
  SheetTitle,
  SheetDescription,
  SheetBody,
  SheetFooter,
  Badge,
  Table,
  TableHeader,
  TableBody,
  TableFooter,
  TableRow,
  TableHead,
  TableCell,
  TableCaption,
  CommandPalette,
  CommandItem,
  Dialog,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogBody,
  DialogFooter,
  Tabs,
  TabsList,
  TabsTrigger,
  TabsContent,
  SkeletonText,
  SkeletonAvatar,
  SkeletonRow,
  SkeletonCard,
  Input,
  Select,
} from '../components/ui';

describe('Milestone 1 Empirical Challenge Suite (UI Primitives)', () => {
  beforeEach(() => {
    document.body.style.overflow = '';
  });

  describe('1. Sheet / Drawer Primitive', () => {
    it('handles ESC key press to close sheet when open', () => {
      const handleClose = vi.fn();
      render(
        <Sheet isOpen={true} onClose={handleClose} title="Drawer Title">
          <div>Content</div>
        </Sheet>
      );
      fireEvent.keyDown(window, { key: 'Escape' });
      expect(handleClose).toHaveBeenCalledTimes(1);
    });

    it('does not trigger onClose on ESC key press when sheet is closed', () => {
      const handleClose = vi.fn();
      render(
        <Sheet isOpen={false} onClose={handleClose} title="Drawer Title">
          <div>Content</div>
        </Sheet>
      );
      fireEvent.keyDown(window, { key: 'Escape' });
      expect(handleClose).not.toHaveBeenCalled();
    });

    it('locks body scroll when open and unlocks body scroll on unmount', () => {
      const { unmount } = render(
        <Sheet isOpen={true} onClose={() => {}} title="Test Sheet">
          <div>Body</div>
        </Sheet>
      );
      expect(document.body.style.overflow).toBe('hidden');
      unmount();
      expect(document.body.style.overflow).toBe('');
    });

    it('triggers onClose when backdrop is clicked', () => {
      const handleClose = vi.fn();
      render(
        <Sheet isOpen={true} onClose={handleClose} title="Backdrop Test">
          <div>Body</div>
        </Sheet>
      );
      const backdrop = screen.getByTestId('sheet-backdrop');
      fireEvent.click(backdrop);
      expect(handleClose).toHaveBeenCalledTimes(1);
    });

    it('renders compound subcomponents correctly', () => {
      render(
        <Sheet isOpen={true} onClose={() => {}}>
          <SheetHeader>
            <SheetTitle>Compound Header</SheetTitle>
            <SheetDescription>Subtext</SheetDescription>
          </SheetHeader>
          <SheetBody>Compound Body</SheetBody>
          <SheetFooter>
            <button>Save</button>
          </SheetFooter>
        </Sheet>
      );

      expect(screen.getByText('Compound Header')).toBeInTheDocument();
      expect(screen.getByText('Subtext')).toBeInTheDocument();
      expect(screen.getByText('Compound Body')).toBeInTheDocument();
      expect(screen.getByText('Save')).toBeInTheDocument();
    });
  });

  describe('2. Dialog Primitive', () => {
    it('handles ESC key press to close dialog when open', () => {
      const handleClose = vi.fn();
      render(
        <Dialog isOpen={true} onClose={handleClose} title="Modal Dialog">
          <div>Dialog Content</div>
        </Dialog>
      );
      fireEvent.keyDown(window, { key: 'Escape' });
      expect(handleClose).toHaveBeenCalledTimes(1);
    });

    it('locks body scroll on open and restores overflow on unmount', () => {
      const { unmount } = render(
        <Dialog isOpen={true} onClose={() => {}} title="Scroll Test">
          <div>Body</div>
        </Dialog>
      );
      expect(document.body.style.overflow).toBe('hidden');
      unmount();
      expect(document.body.style.overflow).toBe('');
    });

    it('triggers onClose on backdrop click', () => {
      const handleClose = vi.fn();
      render(
        <Dialog isOpen={true} onClose={handleClose} title="Backdrop Click">
          <div>Body</div>
        </Dialog>
      );
      const backdrop = screen.getByTestId('dialog-backdrop');
      fireEvent.click(backdrop);
      expect(handleClose).toHaveBeenCalledTimes(1);
    });

    it('renders compound Dialog subcomponents', () => {
      render(
        <Dialog isOpen={true} onClose={() => {}}>
          <DialogHeader>
            <DialogTitle>Dialog Title</DialogTitle>
            <DialogDescription>Dialog Desc</DialogDescription>
          </DialogHeader>
          <DialogBody>Dialog Body Text</DialogBody>
          <DialogFooter>
            <button>Confirm</button>
          </DialogFooter>
        </Dialog>
      );

      expect(screen.getByText('Dialog Title')).toBeInTheDocument();
      expect(screen.getByText('Dialog Desc')).toBeInTheDocument();
      expect(screen.getByText('Dialog Body Text')).toBeInTheDocument();
      expect(screen.getByText('Confirm')).toBeInTheDocument();
    });
  });

  describe('3. CommandPalette Primitive', () => {
    const customItems: CommandItem[] = [
      { id: 'item-1', title: 'First Action', category: 'Actions', path: '/action-1' },
      { id: 'item-2', title: 'Second Action', category: 'Actions', onSelect: vi.fn() },
      { id: 'item-3', title: 'Third Feature', category: 'Features', path: '/feature-3' },
    ];

    it('handles keyboard navigation (ArrowDown, ArrowUp, Enter)', () => {
      const handleRoute = vi.fn();
      render(
        <CommandPalette
          isOpen={true}
          onClose={() => {}}
          onSelectRoute={handleRoute}
          items={customItems}
        />
      );

      // Default selected item is index 0 (First Action)
      fireEvent.keyDown(window, { key: 'ArrowDown' }); // index -> 1 (Second Action)
      fireEvent.keyDown(window, { key: 'Enter' }); // execute item-2 onSelect
      expect(customItems[1].onSelect).toHaveBeenCalledTimes(1);
    });

    it('wraps around index with ArrowUp from top item', () => {
      const handleRoute = vi.fn();
      render(
        <CommandPalette
          isOpen={true}
          onClose={() => {}}
          onSelectRoute={handleRoute}
          items={customItems}
        />
      );

      // Press ArrowUp from 0 -> should wrap to index 2 (Third Feature)
      fireEvent.keyDown(window, { key: 'ArrowUp' });
      fireEvent.keyDown(window, { key: 'Enter' });
      expect(handleRoute).toHaveBeenCalledWith('/feature-3');
    });

    it('shows empty state when no items match search query', () => {
      render(<CommandPalette isOpen={true} onClose={() => {}} items={customItems} />);
      const input = screen.getByPlaceholderText('Type a command or search...');
      fireEvent.change(input, { target: { value: 'NonexistentSearchQuery123' } });

      expect(screen.getByText(/No matching commands or routes found/)).toBeInTheDocument();
    });

    it('clears search query when clear button is clicked', () => {
      render(<CommandPalette isOpen={true} onClose={() => {}} items={customItems} />);
      const input = screen.getByPlaceholderText('Type a command or search...') as HTMLInputElement;
      fireEvent.change(input, { target: { value: 'First' } });
      expect(input.value).toBe('First');

      const clearBtn = screen.getByText('Clear');
      fireEvent.click(clearBtn);
      expect(input.value).toBe('');
    });

    it('closes palette on ESC key press', () => {
      const handleClose = vi.fn();
      render(<CommandPalette isOpen={true} onClose={handleClose} items={customItems} />);
      fireEvent.keyDown(window, { key: 'Escape' });
      expect(handleClose).toHaveBeenCalledTimes(1);
    });
  });

  describe('4. Tabs Primitive', () => {
    it('renders prop-driven tabs and disables clicking on disabled tabs', () => {
      const handleChange = vi.fn();
      const tabsList = [
        { id: 'tab1', label: 'Tab 1', count: 10 },
        { id: 'tab2', label: 'Tab 2', disabled: true },
      ];

      render(<Tabs tabs={tabsList} activeTab="tab1" onChange={handleChange} />);
      expect(screen.getByText('Tab 1')).toBeInTheDocument();
      expect(screen.getByText('10')).toBeInTheDocument();

      const disabledTab = screen.getByText('Tab 2');
      fireEvent.click(disabledTab);
      expect(handleChange).not.toHaveBeenCalled();
    });

    it('renders compound tabs with TabsList, TabsTrigger, and TabsContent', () => {
      const handleValueChange = vi.fn();
      render(
        <Tabs value="summary" onValueChange={handleValueChange}>
          <TabsList>
            <TabsTrigger value="summary">Summary</TabsTrigger>
            <TabsTrigger value="details">Details</TabsTrigger>
          </TabsList>
          <TabsContent value="summary">Summary View Content</TabsContent>
          <TabsContent value="details">Details View Content</TabsContent>
        </Tabs>
      );

      expect(screen.getByText('Summary View Content')).toBeInTheDocument();
      expect(screen.queryByText('Details View Content')).not.toBeInTheDocument();

      fireEvent.click(screen.getByText('Details'));
      expect(handleValueChange).toHaveBeenCalledWith('details');
    });
  });

  describe('5. Table Primitive', () => {
    it('renders empty table state when data array is empty', () => {
      render(<Table headers={['ID', 'Name']} data={[]} renderRow={() => <tr />} />);
      expect(screen.getByText('No data available')).toBeInTheDocument();
    });

    it('renders compound Table subcomponents with dense mode', () => {
      render(
        <Table>
          <TableHeader>
            <TableRow hoverable={false}>
              <TableHead dense>Header A</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow selected>
              <TableCell dense>Data Cell A</TableCell>
            </TableRow>
          </TableBody>
          <TableFooter>
            <TableRow>
              <TableCell>Total: 1</TableCell>
            </TableRow>
          </TableFooter>
          <TableCaption>Sample Caption</TableCaption>
        </Table>
      );

      expect(screen.getByText('Header A')).toBeInTheDocument();
      expect(screen.getByText('Data Cell A')).toBeInTheDocument();
      expect(screen.getByText('Total: 1')).toBeInTheDocument();
      expect(screen.getByText('Sample Caption')).toBeInTheDocument();
    });
  });

  describe('6. Badge Primitive', () => {
    it('supports all tier and status variants', () => {
      const variants = [
        'default',
        'primary',
        'secondary',
        'cyan',
        'teal',
        'zinc',
        'success',
        'warning',
        'danger',
        'info',
        'gold',
        'silver',
        'bronze',
      ] as const;

      variants.forEach((v) => {
        const { unmount } = render(<Badge variant={v}>{v} badge</Badge>);
        expect(screen.getByText(`${v} badge`)).toBeInTheDocument();
        unmount();
      });
    });

    it('allows custom icon override on gold badge', () => {
      const customIcon = <span data-testid="custom-icon">★</span>;
      render(
        <Badge variant="gold" icon={customIcon}>
          Gold Custom
        </Badge>
      );

      expect(screen.getByTestId('custom-icon')).toBeInTheDocument();
    });
  });

  describe('7. Skeleton Primitive', () => {
    it('renders text, avatar, row, and card skeletons', () => {
      const { container } = render(
        <div>
          <SkeletonText lines={4} />
          <SkeletonAvatar size={48} />
          <SkeletonRow columns={3} />
          <SkeletonCard />
        </div>
      );

      const pulses = container.querySelectorAll('.animate-pulse');
      expect(pulses.length).toBeGreaterThan(5);
    });
  });

  describe('8. Input & Select Primitives', () => {
    it('renders input with icons and helper text', () => {
      render(
        <Input
          label="Search"
          helperText="Enter name to filter"
          leftIcon={<span data-testid="left-icon">🔍</span>}
          rightIcon={<span data-testid="right-icon">✖</span>}
        />
      );

      expect(screen.getByLabelText('Search')).toBeInTheDocument();
      expect(screen.getByText('Enter name to filter')).toBeInTheDocument();
      expect(screen.getByTestId('left-icon')).toBeInTheDocument();
      expect(screen.getByTestId('right-icon')).toBeInTheDocument();
    });

    it('renders select with placeholder option and option items', () => {
      render(
        <Select
          label="Department Select"
          placeholder="Choose department..."
          options={[
            { value: 'hr', label: 'Human Resources' },
            { value: 'eng', label: 'Engineering', disabled: true },
          ]}
        />
      );

      expect(screen.getByLabelText('Department Select')).toBeInTheDocument();
      expect(screen.getByText('Choose department...')).toBeInTheDocument();
      expect(screen.getByText('Human Resources')).toBeInTheDocument();
    });
  });
});

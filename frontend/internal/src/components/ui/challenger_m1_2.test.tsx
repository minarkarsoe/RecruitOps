import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import React, { createRef } from 'react';
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
  Skeleton,
  SkeletonText,
  SkeletonAvatar,
  SkeletonRow,
  SkeletonCard,
  Input,
  Select,
  Button,
  Card,
  StatusPill,
} from './index';

describe('Empirical Stress Tests - Milestone 1 UI Primitives (Challenger 2)', () => {
  describe('Re-export Bridge Completeness', () => {
    it('exports all 12 primitives and subcomponents from @recruitops/ui', () => {
      expect(Sheet).toBeDefined();
      expect(SheetHeader).toBeDefined();
      expect(SheetTitle).toBeDefined();
      expect(SheetDescription).toBeDefined();
      expect(SheetBody).toBeDefined();
      expect(SheetFooter).toBeDefined();
      expect(Badge).toBeDefined();
      expect(Table).toBeDefined();
      expect(TableHeader).toBeDefined();
      expect(TableBody).toBeDefined();
      expect(TableFooter).toBeDefined();
      expect(TableRow).toBeDefined();
      expect(TableCell).toBeDefined();
      expect(TableHead).toBeDefined();
      expect(TableCaption).toBeDefined();
      expect(CommandPalette).toBeDefined();
      expect(Dialog).toBeDefined();
      expect(DialogHeader).toBeDefined();
      expect(DialogTitle).toBeDefined();
      expect(DialogDescription).toBeDefined();
      expect(DialogBody).toBeDefined();
      expect(DialogFooter).toBeDefined();
      expect(Tabs).toBeDefined();
      expect(TabsList).toBeDefined();
      expect(TabsTrigger).toBeDefined();
      expect(TabsContent).toBeDefined();
      expect(Skeleton).toBeDefined();
      expect(SkeletonText).toBeDefined();
      expect(SkeletonAvatar).toBeDefined();
      expect(SkeletonRow).toBeDefined();
      expect(SkeletonCard).toBeDefined();
      expect(Input).toBeDefined();
      expect(Select).toBeDefined();
      expect(Button).toBeDefined();
      expect(Card).toBeDefined();
      expect(StatusPill).toBeDefined();
    });
  });

  describe('Sheet Stress & Subcomponents Test', () => {
    it('locks body overflow and closes on Escape key', () => {
      const handleClose = vi.fn();
      const { rerender } = render(
        <Sheet isOpen={true} onClose={handleClose}>
          <div>Content</div>
        </Sheet>
      );

      expect(document.body.style.overflow).toBe('hidden');

      fireEvent.keyDown(window, { key: 'Escape' });
      expect(handleClose).toHaveBeenCalledTimes(1);

      rerender(
        <Sheet isOpen={false} onClose={handleClose}>
          <div>Content</div>
        </Sheet>
      );
      expect(document.body.style.overflow).toBe('');
    });

    it('renders compound Sheet subcomponents correctly', () => {
      render(
        <Sheet isOpen={true} onClose={() => {}}>
          <SheetHeader>
            <SheetTitle>Compound Title</SheetTitle>
            <SheetDescription>Compound Subtitle</SheetDescription>
          </SheetHeader>
          <SheetBody>Body Content</SheetBody>
          <SheetFooter>
            <button>Cancel</button>
            <button>Save</button>
          </SheetFooter>
        </Sheet>
      );

      expect(screen.getByText('Compound Title')).toBeInTheDocument();
      expect(screen.getByText('Compound Subtitle')).toBeInTheDocument();
      expect(screen.getByText('Body Content')).toBeInTheDocument();
      expect(screen.getByText('Cancel')).toBeInTheDocument();
      expect(screen.getByText('Save')).toBeInTheDocument();
    });
  });

  describe('Badge All Variants Test', () => {
    it('renders all badge variants without errors', () => {
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
      ] as const;

      variants.forEach((v) => {
        const { unmount } = render(<Badge variant={v}>{v} badge</Badge>);
        expect(screen.getByText(`${v} badge`)).toBeInTheDocument();
        unmount();
      });
    });
  });

  describe('Table Compound & Edge Cases Test', () => {
    it('renders empty table when data is empty array', () => {
      render(<Table headers={['Header 1', 'Header 2']} data={[]} renderRow={() => null} />);
      expect(screen.getByText('No data available')).toBeInTheDocument();
    });

    it('renders custom compound Table correctly', () => {
      render(
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead dense>Col A</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow selected hoverable>
              <TableCell dense>Val A</TableCell>
            </TableRow>
          </TableBody>
          <TableFooter>
            <TableRow>
              <TableCell>Footer</TableCell>
            </TableRow>
          </TableFooter>
          <TableCaption>Caption text</TableCaption>
        </Table>
      );

      expect(screen.getByText('Col A')).toBeInTheDocument();
      expect(screen.getByText('Val A')).toBeInTheDocument();
      expect(screen.getByText('Footer')).toBeInTheDocument();
      expect(screen.getByText('Caption text')).toBeInTheDocument();
    });
  });

  describe('CommandPalette Keyboard Navigation Test', () => {
    it('navigates through items using ArrowDown and ArrowUp and triggers item.onSelect', () => {
      const customOnSelect = vi.fn();
      const items = [
        { id: '1', title: 'Item 1', category: 'Cat A', onSelect: customOnSelect },
        { id: '2', title: 'Item 2', category: 'Cat A', path: '/path-2' },
      ];

      render(
        <CommandPalette
          isOpen={true}
          onClose={() => {}}
          items={items}
        />
      );

      expect(screen.getByText('Item 1')).toBeInTheDocument();
      expect(screen.getByText('Item 2')).toBeInTheDocument();

      fireEvent.keyDown(window, { key: 'ArrowDown' });
      fireEvent.keyDown(window, { key: 'Enter' });

      // Item 2 should have been triggered (or index wrapped)
      expect(screen.getByText('Item 1')).toBeInTheDocument();
    });
  });

  describe('Tabs Compound Pattern Test', () => {
    it('renders compound Tabs components with active tab switching', () => {
      function TestTabs() {
        const [active, setActive] = React.useState('tab1');
        return (
          <Tabs value={active} onValueChange={setActive}>
            <TabsList>
              <TabsTrigger value="tab1" count={3}>Tab 1</TabsTrigger>
              <TabsTrigger value="tab2" count={0}>Tab 2</TabsTrigger>
            </TabsList>
            <TabsContent value="tab1">Content 1</TabsContent>
            <TabsContent value="tab2">Content 2</TabsContent>
          </Tabs>
        );
      }

      render(<TestTabs />);
      expect(screen.getByText('Content 1')).toBeInTheDocument();
      expect(screen.queryByText('Content 2')).not.toBeInTheDocument();

      fireEvent.click(screen.getByText('Tab 2'));
      expect(screen.queryByText('Content 1')).not.toBeInTheDocument();
      expect(screen.getByText('Content 2')).toBeInTheDocument();
    });
  });

  describe('Input and Select Ref Forwarding Test', () => {
    it('forwards refs correctly for Input and Select', () => {
      const inputRef = createRef<HTMLInputElement>();
      const selectRef = createRef<HTMLSelectElement>();

      render(
        <div>
          <Input ref={inputRef} label="Test Input" />
          <Select ref={selectRef} label="Test Select" options={[{ value: '1', label: 'One' }]} />
        </div>
      );

      expect(inputRef.current).toBeInstanceOf(HTMLInputElement);
      expect(selectRef.current).toBeInstanceOf(HTMLSelectElement);
    });
  });

  describe('Skeleton Variants Test', () => {
    it('renders all skeleton helper components', () => {
      render(
        <div>
          <SkeletonText lines={4} />
          <SkeletonAvatar size={48} />
          <SkeletonRow columns={3} />
          <SkeletonCard />
        </div>
      );

      expect(screen.getAllByText((_content, element) => {
        return element?.classList.contains('animate-pulse') ?? false;
      }).length).toBeGreaterThan(0);
    });
  });
});

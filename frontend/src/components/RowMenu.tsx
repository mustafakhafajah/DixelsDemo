import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'

export interface RowMenuItem {
  label: string
  onClick: () => void
}

/* The ⋯ actions on a table row. The list opens over the page rather than inside the table, so a scrolling
 * table never clips it, and it opens upwards when there is no room below (e.g. on the last row). */
export function RowMenu({ items }: { items: RowMenuItem[] }) {
  /* Nothing the user may do on this row: no ⋯ button at all. */
  if (!items.length) return null

  return (
    <DropdownMenu modal={false}>
      <DropdownMenuTrigger asChild>
        <button type="button" className="iconbtn" title="Actions" aria-label="Actions">⋯</button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" collisionPadding={8} className="tw:min-w-[150px]">
        {items.map((it) => (
          <DropdownMenuItem key={it.label} onSelect={it.onClick}>{it.label}</DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

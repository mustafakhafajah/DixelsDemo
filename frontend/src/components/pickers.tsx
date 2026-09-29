import { useState, type CSSProperties } from 'react'
import { CalendarIcon } from 'lucide-react'

import { Calendar } from '@/components/ui/calendar'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { cn } from '@/lib/utils'
import { dayAt, dayKey, pad } from '../lib/dateUtils'

/* App-level wrappers around the shadcn/ui pieces, so forms keep passing plain strings ("2026-09-27", "09:30"). */

export type DropdownOption = { value: string; label: string; disabled?: boolean }

/* Radix Select cannot hold an empty value, so "" (e.g. "All buildings") travels under this key. */
const EMPTY = '__empty__'
const enc = (v: string) => (v === '' ? EMPTY : v)
const dec = (v: string) => (v === EMPTY ? '' : v)

export function Dropdown({ id, value, onChange, options, placeholder = 'Select…', disabled, size, className, style, 'aria-label': ariaLabel }: {
  id?: string
  value: string
  onChange: (value: string) => void
  options: DropdownOption[]
  placeholder?: string
  disabled?: boolean
  size?: 'sm' | 'default'
  className?: string
  style?: CSSProperties
  'aria-label'?: string
}) {
  /* A value that is not in the list (e.g. not chosen yet) shows the placeholder. */
  const known = options.some((o) => o.value === value)
  return (
    <Select value={known ? enc(value) : ''} onValueChange={(v) => onChange(dec(v))} disabled={disabled}>
      <SelectTrigger id={id} size={size} className={className} style={style} aria-label={ariaLabel}>
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {options.map((o) => <SelectItem key={o.value} value={enc(o.value)} disabled={o.disabled}>{o.label}</SelectItem>)}
      </SelectContent>
    </Select>
  )
}

const DATE_LABEL = new Intl.DateTimeFormat('en-GB', { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' })

/* value/min/max are UTC day keys ("YYYY-MM-DD"), the same format the app already uses everywhere. */
export function DatePicker({ id, value, onChange, min, max, placeholder = 'Pick a date', disabled, className, style, 'aria-label': ariaLabel }: {
  id?: string
  value: string
  onChange: (value: string) => void
  min?: string
  max?: string
  placeholder?: string
  disabled?: boolean
  className?: string
  style?: CSSProperties
  'aria-label'?: string
}) {
  const [open, setOpen] = useState(false)
  const selected = value ? dayAt(value) : undefined
  const hidden = [
    ...(min ? [{ before: dayAt(min) }] : []),
    ...(max ? [{ after: dayAt(max) }] : []),
  ]
  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button type="button" id={id} disabled={disabled} aria-label={ariaLabel} style={style}
          className={cn(
            'tw:flex tw:h-[39px] tw:w-full tw:items-center tw:gap-2 tw:rounded-[9px] tw:border tw:border-solid tw:border-input tw:bg-background tw:px-3 tw:text-left tw:font-[inherit] tw:text-sm tw:text-foreground tw:whitespace-nowrap tw:shadow-[0_1px_2px_rgba(24,24,27,.04)] tw:outline-none tw:cursor-pointer',
            'tw:focus-visible:border-ring tw:data-[state=open]:border-ring tw:disabled:cursor-not-allowed tw:disabled:opacity-50 tw:disabled:bg-muted',
            !value && 'tw:text-muted-foreground',
            className,
          )}>
          <CalendarIcon className="tw:size-4 tw:shrink-0 tw:opacity-60" />
          <span className="tw:truncate">{selected ? DATE_LABEL.format(selected) : placeholder}</span>
        </button>
      </PopoverTrigger>
      <PopoverContent className="tw:w-auto tw:p-3" align="start">
        <Calendar
          mode="single"
          timeZone="UTC"
          weekStartsOn={1}
          selected={selected}
          defaultMonth={selected ?? (min ? dayAt(min) : undefined)}
          disabled={hidden}
          onSelect={(d) => { if (d) { onChange(dayKey(d)); setOpen(false) } }}
        />
      </PopoverContent>
    </Popover>
  )
}

/* One list of times ("HH:MM") in steps of minuteStep (15 by default): 00:00, 00:15 … 23:45.
 * min ("HH:MM") leaves every earlier time out of the list, so it cannot be picked at all.
 * The time already chosen always stays in the list (even an off-step one such as 10:10),
 * so the field never shows as blank. */
export function TimePicker({ id, value, onChange, min, minuteStep = 15, disabled, className, 'aria-label': ariaLabel = 'Time' }: {
  id?: string
  value: string
  onChange: (value: string) => void
  min?: string | null
  minuteStep?: number
  disabled?: boolean
  className?: string
  'aria-label'?: string
}) {
  const toMin = (t: string) => { const [h, m] = t.split(':').map(Number); return h * 60 + m }
  const floor = min ? toMin(min) : 0
  const times: string[] = []
  for (let t = 0; t < 1440; t += minuteStep) {
    if (t >= floor) times.push(`${pad(Math.floor(t / 60))}:${pad(t % 60)}`)
  }
  if (value && !times.includes(value)) { times.push(value); times.sort() }
  return (
    <Dropdown id={id} value={value} placeholder="--:--" disabled={disabled} aria-label={ariaLabel} className={className}
      options={times.map((t) => ({ value: t, label: t }))} onChange={onChange} />
  )
}

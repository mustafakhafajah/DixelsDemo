import { lifecycleOf, type EstateStatus, type ScheduleItem } from '../api/types'

export function StatusPill({ item }: { item: ScheduleItem }) {
  const s = lifecycleOf(item)
  if (s === 'cancelled') return <span className="pill pill-cancelled"><span className="dot" />Cancelled</span>
  if (s === 'ended') return <span className="pill pill-ended"><span className="dot" />Ended</span>
  if (s === 'in_progress') return <span className="pill pill-inprog"><span className="dot" />In progress</span>
  return (
    <span className="pill pill-confirmed">
      <span className="dot" />
      {item.kind === 'maintenance' ? 'Scheduled' : 'Confirmed'}
    </span>
  )
}

export function EstatePill({ status }: { status: EstateStatus }) {
  return status === 'Active'
    ? <span className="pill pill-confirmed"><span className="dot" />Active</span>
    : <span className="pill pill-inactive"><span className="dot" />Inactive</span>
}

export function ErrorLine({ error }: { error: { code: string; message: string } | null | undefined }) {
  if (!error) return null
  return (
    <p className="err">
      <span className="errcode">{error.code}</span>
      <span className="msg">{error.message}</span>
    </p>
  )
}

/* The "*" after a required field's label; pair it with className="lbl req" on the label. */
export function RequiredMark() {
  return <span className="req-mark" aria-hidden="true">*</span>
}

export function Loading({ label = 'Loading…' }: { label?: string }) {
  return <p style={{ padding: '24px 16px', textAlign: 'center', fontSize: 12.5, color: 'var(--slate)', margin: 0 }}>{label}</p>
}

export const shortId = (id: string, prefix = 'BK') => `${prefix}-${id.slice(0, 8).toUpperCase()}`

export const plural =(n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`

export function initials(name: string): string {
  const parts = name.replace(/[^\p{L}\s.]/gu, ' ').split(/[\s.]+/).filter(Boolean)
  if (!parts.length) return '?'
  return (parts.length === 1 ? parts[0].slice(0, 2) : parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
}

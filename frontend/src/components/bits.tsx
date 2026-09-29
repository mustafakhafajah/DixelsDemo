import { errorText } from '../api/client'
import { lifecycleOf, type ScheduleItem } from '../api/types'

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

/* reason: shown as a tooltip, e.g. why a ticked space is still blocked by its building. */
export function BookablePill({ bookable, reason }: { bookable: boolean; reason?: string | null }) {
  return bookable
    ? <span className="pill pill-confirmed"><span className="dot" />Bookable</span>
    : <span className="pill pill-inactive" title={reason ?? undefined}><span className="dot" />Not bookable</span>
}

/* id: lets the field point at its message (aria-describedby). */
export function ErrorLine({ error, id }: { error: { code: string; message: string } | null | undefined; id?: string }) {
  if (!error) return null
  return (
    /* Only the human sentence is shown; the code stays in the data for logic, not on screen. */
    <p className="err" data-code={error.code} id={id} role="alert">
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

export const plural =(n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`

export function initials(name: string): string {
  const parts = name.replace(/[^\p{L}\s.]/gu, ' ').split(/[\s.]+/).filter(Boolean)
  if (!parts.length) return '?'
  return (parts.length === 1 ? parts[0].slice(0, 2) : parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
}

/* Shown instead of "Loading…" when a request failed, so the screen never waits forever. */
export function LoadError({ what, error, onRetry }: { what: string; error: unknown; onRetry: () => void }) {
  return (
    <div className="load-error" role="alert">
      <p className="load-error-title">Couldn't load {what}.</p>
      <p className="load-error-msg">{errorText(error).message}</p>
      <button type="button" className="btn btn-sm" onClick={onRetry}>Try again</button>
    </div>
  )
}

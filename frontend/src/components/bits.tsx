import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { errorText } from '../api/client'
import { lifecycleOf, type ScheduleItem } from '../api/types'

export function StatusPill({ item }: { item: ScheduleItem }) {
  const { t } = useTranslation()
  const s = lifecycleOf(item)
  if (s === 'cancelled') return <span className="pill pill-cancelled"><span className="dot" />{t('status.cancelled')}</span>
  if (s === 'ended') return <span className="pill pill-ended"><span className="dot" />{t('status.ended')}</span>
  if (s === 'in_progress') return <span className="pill pill-inprog"><span className="dot" />{t('status.inProgress')}</span>
  return (
    <span className="pill pill-confirmed">
      <span className="dot" />
      {item.kind === 'maintenance' ? t('status.scheduled') : t('status.confirmed')}
    </span>
  )
}

/* reason: shown as a tooltip, e.g. why a ticked space is still blocked by its building. */
export function BookablePill({ bookable, reason }: { bookable: boolean; reason?: string | null }) {
  const { t } = useTranslation()
  return bookable
    ? <span className="pill pill-confirmed"><span className="dot" />{t('status.bookable')}</span>
    : <span className="pill pill-inactive" title={reason ?? undefined}><span className="dot" />{t('status.notBookable')}</span>
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

/* A form field: required makes a bold label with a "*" after the text (styles in appShell.css); error is shown right under the input. */
export function Field({ id, label, required, error, children }: { id: string; label: string; required?: boolean; error?: { code: string; message: string }; children: ReactNode }) {
  return (
    <div>
      <label className={`lbl${required ? ' req' : ''}`} htmlFor={id}>{label}{required && <RequiredMark />}</label>
      {children}
      <ErrorLine id={`${id}-error`} error={error} />
    </div>
  )
}

/* "* Required field", at the top of a form that has required fields. */
export function RequiredNote() {
  const { t } = useTranslation()
  return <p className="req-note"><RequiredMark /> {t('common.requiredField')}</p>
}

export function Loading({ label }: { label?: string }) {
  const { t } = useTranslation()
  return <p style={{ padding: '24px 16px', textAlign: 'center', fontSize: 12.5, color: 'var(--slate)', margin: 0 }}>{label ?? t('common.loading')}</p>
}

/* Shown instead of "Loading…" when a request failed, so the screen never waits forever.
 * what: the thing that failed, e.g. t('load.buildings') ("the buildings"). */
export function LoadError({ what, error, onRetry }: { what: string; error: unknown; onRetry: () => void }) {
  const { t } = useTranslation()
  return (
    <div className="load-error" role="alert">
      <p className="load-error-title">{t('load.failed', { what })}</p>
      <p className="load-error-msg">{errorText(error).message}</p>
      <button type="button" className="btn btn-sm" onClick={onRetry}>{t('common.tryAgain')}</button>
    </div>
  )
}

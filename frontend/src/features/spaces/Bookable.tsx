import { useState } from 'react'
import { Trans, useTranslation } from 'react-i18next'
import { errorText } from '../../api/client'
import { useSetBookable, useUpcomingCount, type EstateKind, type EstateScope } from '../../api/hooks'
import { CancelMessageFields, type CancelMessageValue } from '../../components/CancelMessageFields'
import { bulkCancelDefaults } from '../../components/cancelMessage'
import { Modal } from '../../components/Sheet'
import { modals, type BookableTarget } from '../../state/modalStore'
import { toast } from '../../state/toastStore'
import { scopeTypeOf, useCancelUpcomingAfterSave } from './useCancelUpcomingAfterSave'

/* What happens to bookings that already exist when something stops being bookable: they are kept,
 * and the admin can choose to cancel them, with the email everyone on them gets. Shown in the forms and in the
 * "Make not bookable" dialog. message is null until edited, showing the ready-made text for label. */
function UpcomingWarning({ scope, label, cancel, onCancelChange, message, onMessageChange }: {
  scope: EstateScope
  label: string
  cancel: boolean
  onCancelChange: (v: boolean) => void
  message: CancelMessageValue | null
  onMessageChange: (v: CancelMessageValue) => void
}) {
  const { t } = useTranslation()
  const count = useUpcomingCount(scope)
  if (count.isLoading) return <p className="muted-box">{t('bookable.checking')}</p>
  const n = count.data ?? 0
  if (!n) return <p className="muted-box">{t('bookable.nobodyAffected')}</p>
  return (
    <div className="muted-box" style={{ borderColor: 'var(--rust-line)', background: 'var(--rust-soft)', color: 'var(--ink)' }}>
      <Trans i18nKey="bookable.upcomingKept" count={n} components={{ b: <strong /> }} />
      <label style={{ display: 'flex', gap: 8, alignItems: 'center', marginTop: 8, fontWeight: 600, cursor: 'pointer' }}>
        <input type="checkbox" checked={cancel} onChange={(e) => onCancelChange(e.target.checked)} />
        {t('bookable.alsoCancel', { count: n })}
      </label>
      {cancel && (
        <div style={{ marginTop: 10 }}>
          <CancelMessageFields idPrefix={`cancel-${scope.scopeId}`} value={message ?? bulkCancelDefaults(label)} onChange={onMessageChange} />
        </div>
      )}
    </div>
  )
}

/* The "Bookable" tick in the building, floor and space forms. When an existing, bookable item is
 * unticked, it warns about the bookings already made there. label names the item in the cancellation email. */
export function BookableField({ idPrefix, kind, label, value, onChange, existingId, wasBookable, cancelUpcoming, onCancelUpcomingChange, cancelMessage, onCancelMessageChange }: {
  idPrefix: string
  kind: EstateKind
  label: string
  value: boolean
  onChange: (v: boolean) => void
  existingId?: string
  wasBookable?: boolean
  cancelUpcoming: boolean
  onCancelUpcomingChange: (v: boolean) => void
  cancelMessage: CancelMessageValue | null
  onCancelMessageChange: (v: CancelMessageValue) => void
}) {
  const { t } = useTranslation()
  const turningOff = !!existingId && !!wasBookable && !value
  return (
    <div>
      <label htmlFor={`${idPrefix}-bookable`} style={{ display: 'flex', gap: 9, alignItems: 'flex-start', cursor: 'pointer' }}>
        <input type="checkbox" id={`${idPrefix}-bookable`} checked={value} onChange={(e) => onChange(e.target.checked)} style={{ marginTop: 3 }} />
        <span>
          <span style={{ fontWeight: 600 }}>{t('status.bookable')}</span>
          <span style={{ display: 'block', fontSize: 12, color: 'var(--slate)' }}>
            {value ? t('bookable.peopleCanBook') : `${t('bookable.noNewBookings')} ${t(`bookable.blockedToo.${kind}`)}`}
          </span>
        </span>
      </label>
      {turningOff && (
        <div style={{ marginTop: 10 }}>
          <UpcomingWarning scope={{ scopeType: scopeTypeOf(kind), scopeId: existingId! }} label={label}
            cancel={cancelUpcoming} onCancelChange={onCancelUpcomingChange} message={cancelMessage} onMessageChange={onCancelMessageChange} />
        </div>
      )}
    </div>
  )
}

/* The row menu's "Make not bookable": the same warning and choice as the forms, as a small dialog. */
export function NotBookableModal({ target }: { target: BookableTarget }) {
  const { t } = useTranslation()
  const setBookable = useSetBookable()
  const cancelAfter = useCancelUpcomingAfterSave()
  const [cancel, setCancel] = useState(false)
  const [message, setMessage] = useState<CancelMessageValue | null>(null)

  const confirm = async () => {
    try {
      await setBookable.mutateAsync({ kind: target.kind, id: target.id, isBookable: false })
      if (cancel) await cancelAfter(target.kind, target.id, target.label, message)
      toast('warn', t('bookable.toast.notBookable', { name: target.label }), t(`bookable.cantTake.${target.kind}`))
      modals.close()
    } catch (e) {
      const { code, message } = errorText(e)
      toast('err', t('common.requestRejected'), message, code)
    }
  }

  return (
    <Modal width={440} title={t('estate.makeNotBookable')} subtitle={target.label} onClose={modals.close}
      footer={(
        <>
          <button type="button" className="btn" onClick={modals.close}>{t('bookable.keepBookable')}</button>
          <button type="button" className="btn btn-primary" disabled={setBookable.isPending} onClick={confirm}>{t('estate.makeNotBookable')}</button>
        </>
      )}>
      <p style={{ fontSize: 13 }}>{t('bookable.nobodyWillBook')} {t(`bookable.blockedToo.${target.kind}`)}</p>
      <UpcomingWarning scope={{ scopeType: scopeTypeOf(target.kind), scopeId: target.id }} label={target.label}
        cancel={cancel} onCancelChange={setCancel} message={message} onMessageChange={setMessage} />
    </Modal>
  )
}

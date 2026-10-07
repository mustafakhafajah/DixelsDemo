import { useTranslation } from 'react-i18next'

/* What the cancellation email says, as the fields hold it. */
export interface CancelMessageValue {
  subject: string
  message: string
}

/* The subject and message emailed when bookings are cancelled, like the note Teams lets you add when cancelling a
 * meeting. The caller fills in the default text, so a plain "cancel" still sends a sensible email. */
export function CancelMessageFields({ idPrefix, value, onChange }: {
  idPrefix: string
  value: CancelMessageValue
  onChange: (v: CancelMessageValue) => void
}) {
  const { t } = useTranslation()
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      <div>
        <label className="lbl" htmlFor={`${idPrefix}-subject`}>{t('cancelMessage.subject')}</label>
        <input id={`${idPrefix}-subject`} className="inp" dir="auto" maxLength={200} value={value.subject}
          onChange={(e) => onChange({ ...value, subject: e.target.value })} />
      </div>
      <div>
        <label className="lbl" htmlFor={`${idPrefix}-message`}>{t('cancelMessage.message')}</label>
        <textarea id={`${idPrefix}-message`} className="inp" dir="auto" rows={3} maxLength={2000} style={{ resize: 'vertical' }}
          aria-describedby={`${idPrefix}-hint`} value={value.message} onChange={(e) => onChange({ ...value, message: e.target.value })} />
        <p id={`${idPrefix}-hint`} style={{ fontSize: 11.5, color: 'var(--slate)', margin: '4px 0 0' }}>{t('cancelMessage.hint')}</p>
      </div>
    </div>
  )
}

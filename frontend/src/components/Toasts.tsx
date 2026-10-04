import { useTranslation } from 'react-i18next'
import { useToastStore } from '../state/toastStore'

export function Toasts() {
  const { t } = useTranslation()
  const { toasts, dismiss } = useToastStore()
  return (
    <div className="toasts">
      {toasts.map((toast) => (
        <div key={toast.id} className={`toast ${toast.kind}`} role="status">
          <div style={{ display: 'flex', gap: 9, alignItems: 'flex-start' }}>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ display: 'flex', gap: 7, alignItems: 'center', flexWrap: 'wrap' }}>
                <strong style={{ fontSize: 13 }}>{toast.title}</strong>
              </div>
              {toast.message && <p style={{ fontSize: 12.5, color: 'var(--slate)', margin: '4px 0 0', lineHeight: 1.45 }}>{toast.message}</p>}
            </div>
            <button type="button" className="iconbtn" onClick={() => dismiss(toast.id)} aria-label={t('common.dismiss')}>×</button>
          </div>
        </div>
      ))}
    </div>
  )
}

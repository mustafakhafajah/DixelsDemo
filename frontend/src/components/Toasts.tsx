import { useToastStore } from '../state/toastStore'

export function Toasts() {
  const { toasts, dismiss } = useToastStore()
  return (
    <div className="toasts">
      {toasts.map((t) => (
        <div key={t.id} className={`toast ${t.kind}`} role="status">
          <div style={{ display: 'flex', gap: 9, alignItems: 'flex-start' }}>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ display: 'flex', gap: 7, alignItems: 'center', flexWrap: 'wrap' }}>
                <strong style={{ fontSize: 13 }}>{t.title}</strong>
                {t.code && <span className="errcode">{t.code}</span>}
              </div>
              {t.message && <p style={{ fontSize: 12.5, color: 'var(--slate)', margin: '4px 0 0', lineHeight: 1.45 }}>{t.message}</p>}
            </div>
            <button type="button" className="iconbtn" onClick={() => dismiss(t.id)} aria-label="Dismiss">×</button>
          </div>
        </div>
      ))}
    </div>
  )
}

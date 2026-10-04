import type { ReactNode } from 'react'
import './EmptyState.css'

export type EmptyArt = 'building' | 'floor' | 'space' | 'spaceType' | 'user' | 'room'

/* An empty list: a picture, what is going on, what to do, and at most one button - the type only allows one,
 * so a page can't squeeze two actions in. Nothing yet -> "Add ..."; filters match nothing -> "Clear filters". */
export function EmptyState({ art, title, text, action }: {
  art: EmptyArt
  title: string
  text: string
  action?: { label: string; onClick: () => void }
}) {
  return (
    <div className="empty-state" role="status">
      <EmptyArtwork art={art} />
      <h2 className="empty-state-title">{title}</h2>
      <p className="empty-state-text">{text}</p>
      {action && <button type="button" className="btn btn-primary" onClick={action.onClick}>{action.label}</button>}
    </div>
  )
}

/* Drawn only with the theme's colours, so it follows dark mode; nearly symmetric, so it reads the same in Arabic. */
const C = {
  back: 'var(--surface-3)',
  card: 'var(--surface)',
  line: 'var(--line)',
  shape: 'var(--line-strong)',
  soft: 'var(--accent-soft)',
  accent: 'var(--accent)',
}

/* Each page's subject, drawn on the front card (its centre is at 110, 82). */
const SUBJECT: Record<EmptyArt, ReactNode> = {
  building: (
    <g>
      <rect x="90" y="52" width="40" height="56" rx="3" fill={C.soft} stroke={C.shape} strokeWidth="2" />
      {[60, 74, 88].map((y) => [97, 115].map((x) => <rect key={`${x}-${y}`} x={x} y={y} width="8" height="7" rx="1.5" fill={C.shape} />))}
      <rect x="105" y="96" width="10" height="12" rx="1.5" fill={C.accent} />
    </g>
  ),
  floor: (
    <g fill="none" stroke={C.shape} strokeWidth="2">
      <rect x="72" y="54" width="76" height="54" rx="3" fill={C.soft} />
      <path d="M72 80h34M106 54v54M106 70h42" />
      <rect x="114" y="78" width="26" height="22" rx="2" fill={C.accent} stroke="none" opacity=".85" />
    </g>
  ),
  space: (
    <g>
      <rect x="96" y="54" width="28" height="19" rx="2.5" fill={C.soft} stroke={C.shape} strokeWidth="2" />
      <rect x="107" y="73" width="6" height="6" fill={C.shape} />
      <rect x="78" y="79" width="64" height="7" rx="2" fill={C.shape} />
      <path d="M84 86v20M136 86v20" stroke={C.shape} strokeWidth="3" strokeLinecap="round" />
      <circle cx="110" cy="99" r="5" fill={C.accent} />
    </g>
  ),
  spaceType: (
    <g>
      <path d="M86 58h28l22 22-28 28-22-22z" fill={C.soft} stroke={C.shape} strokeWidth="2" strokeLinejoin="round" />
      <circle cx="99" cy="71" r="5" fill={C.accent} />
      <path d="M104 92l12-12M110 98l10-10" stroke={C.shape} strokeWidth="2.4" strokeLinecap="round" />
    </g>
  ),
  user: (
    <g>
      <circle cx="110" cy="68" r="13" fill={C.soft} stroke={C.shape} strokeWidth="2" />
      <path d="M84 108c3-14 13-21 26-21s23 7 26 21" fill={C.soft} stroke={C.shape} strokeWidth="2" strokeLinecap="round" />
      <circle cx="133" cy="60" r="5" fill={C.accent} />
    </g>
  ),
  room: (
    <g>
      <rect x="78" y="54" width="56" height="48" rx="4" fill={C.soft} stroke={C.shape} strokeWidth="2" />
      <path d="M78 66h56M90 50v8M122 50v8" stroke={C.shape} strokeWidth="2" strokeLinecap="round" />
      {[74, 84].map((y) => [88, 100, 112].map((x) => <rect key={`${x}-${y}`} x={x} y={y} width="7" height="6" rx="1.5" fill={C.shape} />))}
      <circle cx="132" cy="98" r="11" fill={C.card} stroke={C.accent} strokeWidth="3" />
      <path d="M140 106l8 8" stroke={C.accent} strokeWidth="3.5" strokeLinecap="round" />
    </g>
  ),
}

function EmptyArtwork({ art }: { art: EmptyArt }) {
  return (
    <svg className="empty-state-art" viewBox="0 0 220 150" aria-hidden="true" focusable="false">
      <rect x="44" y="20" width="124" height="92" rx="14" fill={C.back} transform="rotate(-11 106 66)" />
      <g className="empty-state-card">
        <rect x="48" y="36" width="124" height="92" rx="14" fill={C.card} stroke={C.line} />
      </g>
      {SUBJECT[art]}
    </svg>
  )
}

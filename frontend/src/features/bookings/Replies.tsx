import { CheckIcon, CircleHelpIcon, ClockIcon, XIcon, type LucideIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import type { AttendeeResponse } from '../../api/types'

/* Each answer's mark and colour, as Teams shows a meeting's tracking: a tick, a question mark, a cross, or waiting. */
const LOOK: Record<AttendeeResponse, { Icon: LucideIcon; color: string; pill: string }> = {
  accepted: { Icon: CheckIcon, color: 'var(--accent)', pill: 'pill-confirmed' },
  tentative: { Icon: CircleHelpIcon, color: 'var(--copper)', pill: 'pill-inprog' },
  declined: { Icon: XIcon, color: 'var(--rust)', pill: 'pill-inactive' },
  none: { Icon: ClockIcon, color: 'var(--slate-2)', pill: 'pill-ended' },
}

/* Decorative: the answer is always written next to it. */
export function ResponseIcon({ response, size = 14 }: { response: AttendeeResponse; size?: number }) {
  const { Icon, color } = LOOK[response]
  return <Icon size={size} color={color} aria-hidden="true" style={{ flexShrink: 0 }} />
}

/* Your own answer to an invitation, as a small pill (the day list). */
export function ResponsePill({ response }: { response: AttendeeResponse }) {
  const { t } = useTranslation()
  return (
    <span className={`pill ${LOOK[response].pill}`}>
      <ResponseIcon response={response} size={12} />
      {response === 'none' ? t('detail.reply.notAnsweredShort') : t(`detail.response.${response}`)}
    </span>
  )
}

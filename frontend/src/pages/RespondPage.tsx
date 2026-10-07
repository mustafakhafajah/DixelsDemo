import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useParams, useSearchParams } from 'react-router-dom'
import { apiRequest, errorText } from '../api/client'
import { parseUtc, formatDate, hm } from '../lib/dateUtils'
import { localRange } from '../lib/closedDays'
import type { BookingInvitation, Reply } from '../api/types'
import { ResponseIcon } from '../features/bookings/Replies'
import '../app/appShell.css'
import './RespondPage.css'

/* The words an invitation's buttons put in the link. */
const ANSWERS = new Map<string, Reply>([['accept', 'accepted'], ['tentative', 'tentative'], ['decline', 'declined']])

/* Where an outside guest lands from an invitation's Accept / Maybe / Decline button: no sign-in, the private secret in
 * the address is the key. The answer from the link is recorded once, then the page shows the booking (in the guest's
 * own time) and their answer, read-only like in the portal: the email's buttons change it. */
export function RespondPage() {
  const { t } = useTranslation()
  const { token = '' } = useParams()
  const [params, setParams] = useSearchParams()
  const [invitation, setInvitation] = useState<BookingInvitation | null>(null)
  const [error, setError] = useState<string | null>(null)
  const answer = ANSWERS.get(params.get('answer') ?? '')
  const wholeSeries = params.get('series') === '1'
  const started = useRef(false)

  useEffect(() => {
    if (started.current) return
    started.current = true
    const path = `/api/app/booking-invitations/${encodeURIComponent(token)}`
    const request = answer
      ? apiRequest<BookingInvitation>(undefined, 'PUT', `${path}/response`, { response: answer, wholeSeries })
      : apiRequest<BookingInvitation>(undefined, 'GET', path)
    request.then(setInvitation, (e: unknown) => setError(errorText(e).message))
    /* A reload or a shared address then only shows the answer, it doesn't send it again. */
    if (answer) setParams({}, { replace: true })
  }, [token, answer, wholeSeries, setParams])

  const start = invitation ? parseUtc(invitation.startUtc) : null
  const end = invitation ? parseUtc(invitation.endUtc) : null
  const buildingClock = invitation && start && end ? localRange(start, end, invitation.timeZone) : null

  return (
    <main className="respond-page">
      <div className="respond-card" role="status" aria-live="polite">
        <p className="respond-brand">Dixels</p>
        {error ? (
          <>
            <h1>{t('respond.problem')}</h1>
            <p className="respond-muted">{error}</p>
          </>
        ) : !invitation || !start || !end ? (
          <p className="respond-muted">{t('respond.loading')}</p>
        ) : (
          <>
            <h1>
              <ResponseIcon response={invitation.response} size={20} />
              {invitation.response === 'none' ? t('respond.notAnswered') : t('respond.yourAnswer', { answer: t(`detail.response.${invitation.response}`) })}
            </h1>
            <p className="respond-muted">
              {t('respond.invitedBy', { name: invitation.ownerName })}{invitation.response !== 'none' && ` ${t('respond.told', { name: invitation.ownerName })}`}
            </p>
            <dl className="respond-kv">
              <dt>{t('common.space')}</dt>
              <dd>
                <bdi>{invitation.spaceName}</bdi>
                {invitation.buildingName && <span className="respond-muted"> · <bdi>{invitation.buildingName}</bdi>{invitation.floorName && <> · <bdi>{t('common.floorName', { name: invitation.floorName })}</bdi></>}</span>}
              </dd>
              <dt>{t('common.date')}</dt>
              <dd>{formatDate(start, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })}</dd>
              <dt>{t('respond.time')}</dt>
              <dd>
                <span className="mono">{hm(start)}–{hm(end)}</span> <span className="respond-muted">{t('respond.yourTime')}</span>
                {buildingClock && <div className="respond-muted">= {buildingClock}</div>}
              </dd>
            </dl>
            {invitation.lifecycle === 'cancelled' ? <p className="respond-note">{t('respond.cancelled')}</p>
              : invitation.lifecycle === 'ended' ? <p className="respond-note">{t('respond.ended')}</p>
                : <p className="respond-note">{t('respond.howToChange')}</p>}
          </>
        )}
      </div>
    </main>
  )
}

export default RespondPage

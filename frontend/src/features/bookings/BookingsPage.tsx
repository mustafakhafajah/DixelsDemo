import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import type { Reply } from '../../api/types'
import { LoadError, Loading } from '../../components/bits'
import { modals } from '../../state/modalStore'
import { ScheduleCalendar } from './schedule/ScheduleCalendar'
import { ScheduleToolbar } from './schedule/ScheduleToolbar'
import { useScheduleData } from './schedule/useScheduleData'

/* The answers an invitation email's links carry. "refuse" is what older emails say for Decline. */
const EMAIL_REPLIES = new Map<string, Reply>([['accept', 'accepted'], ['tentative', 'tentative'], ['decline', 'declined'], ['refuse', 'declined']])

/* The links in an invitation email: ?booking=<id> opens that booking, and &respond=accept|tentative|decline also
 * records that answer at once, as clicking it in a Teams email does (the email is the only place to answer).
 * &series=1: the invitation was for a repeating booking, so the answer covers its later dates too.
 * The address is tidied up afterwards. */
function useEmailLink() {
  const [params, setParams] = useSearchParams()
  const bookingId = params.get('booking')
  const respond = EMAIL_REPLIES.get(params.get('respond') ?? '')
  const series = params.get('series') === '1'
  useEffect(() => {
    if (!bookingId) return
    modals.detail('booking', bookingId, respond ? { respond, ...(series ? { respondSeries: true as const } : {}) } : {})
    setParams((p) => { p.delete('booking'); p.delete('respond'); p.delete('series'); return p }, { replace: true })
  }, [bookingId, respond, series, setParams])
}

export function BookingsPage() {
  const { t } = useTranslation()
  const data = useScheduleData('my')
  useEmailLink()
  return (
    <section>
      <div className="card" style={{ marginBottom: 16, overflow: 'hidden' }}>
        <ScheduleToolbar id="my" spaces={data.spaces} />
      </div>
      <div className="card" style={{ marginBottom: 16, overflow: 'hidden' }}>
        {data.error ? <LoadError what={t('load.yourBookings')} error={data.error} onRetry={data.retry} />
          : data.loading ? <Loading label={t('schedule.loadingBookings')} />
          : <ScheduleCalendar id="my" data={data} />}
      </div>
    </section>
  )
}

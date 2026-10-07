import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { LoadError, Loading } from '../../components/bits'
import { modals } from '../../state/modalStore'
import { ScheduleCalendar } from './schedule/ScheduleCalendar'
import { ScheduleToolbar } from './schedule/ScheduleToolbar'
import { useScheduleData } from './schedule/useScheduleData'

/* The links in an invitation email: ?booking=<id> opens that booking (Accept: nothing else to do), and
 * &respond=refuse also asks straight away whether to leave it. The address is tidied up afterwards. */
function useEmailLink() {
  const [params, setParams] = useSearchParams()
  const bookingId = params.get('booking')
  const refuse = params.get('respond') === 'refuse'
  useEffect(() => {
    if (!bookingId) return
    modals.detail('booking', bookingId, refuse)
    setParams((p) => { p.delete('booking'); p.delete('respond'); return p }, { replace: true })
  }, [bookingId, refuse, setParams])
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

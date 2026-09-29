import { LoadError, Loading } from '../../components/bits'
import { ScheduleCalendar } from './schedule/ScheduleCalendar'
import { ScheduleToolbar } from './schedule/ScheduleToolbar'
import { useScheduleData } from './schedule/useScheduleData'

export function BookingsPage() {
  const data = useScheduleData('my')
  return (
    <section>
      <div className="card" style={{ marginBottom: 16, overflow: 'hidden' }}>
        <ScheduleToolbar id="my" spaces={data.spaces} />
      </div>
      <div className="card" style={{ marginBottom: 16, overflow: 'hidden' }}>
        {data.error ? <LoadError what="your bookings" error={data.error} onRetry={data.retry} />
          : data.loading ? <Loading label="Loading bookings…" />
          : <ScheduleCalendar id="my" data={data} />}
      </div>
    </section>
  )
}

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
        <div className="card-head">
          <div>
            <h2 className="card-title">Schedule</h2>
            <p className="card-sub">{data.loading ? 'Loading bookings…' : 'Click a day to see full detail.'}</p>
          </div>
        </div>
        <ScheduleCalendar id="my" data={data} />
      </div>
    </section>
  )
}

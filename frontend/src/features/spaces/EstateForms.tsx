import { useState, type ReactNode } from 'react'
import { errorText } from '../../api/client'
import { useBuildings, useFloors, useSaveBuilding, useSaveFloor, useSaveSpace } from '../../api/hooks'
import { SPACE_TYPE_LABELS, type Building, type EstateStatus, type Floor, type Space, type SpaceType } from '../../api/types'
import { ErrorLine, shortId } from '../../components/bits'
import { Modal } from '../../components/Sheet'
import { modals } from '../../state/modalStore'
import { toast } from '../../state/toastStore'

type FieldError = { code: string; message: string } | null

const numOrNull = (v: string) => (v.trim() === '' ? null : Number(v))
const numText = (v: number | null | undefined) => (v == null ? '' : String(v))

function Footer({ onSave, label, busy }: { onSave: () => void; label: string; busy: boolean }) {
  return (
    <>
      <button type="button" className="btn" onClick={modals.close}>Discard</button>
      <button type="button" className="btn btn-primary" disabled={busy} onClick={onSave}>{label}</button>
    </>
  )
}

function Field({ id, label, children }: { id: string; label: string; children: ReactNode }) {
  return <div><label className="lbl" htmlFor={id}>{label}</label>{children}</div>
}

/* Open/close/min/max overrides, with "Inherits X" placeholders from the parent level. */
function OverrideFields({ prefix, values, onChange, inherits }: {
  prefix: string
  values: [string, string, string, string]
  onChange: (v: [string, string, string, string]) => void
  inherits: [number, number, number, number] | null
}) {
  const set = (i: number, v: string) => { const next = [...values] as [string, string, string, string]; next[i] = v; onChange(next) }
  const ph = (i: number) => (inherits ? `Inherits ${inherits[i]}` : '')
  return (
    <>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Field id={`${prefix}-open`} label="Open hour override">
          <input type="number" id={`${prefix}-open`} className="inp mono" min={0} max={23} value={values[0]} placeholder={ph(0)} onChange={(e) => set(0, e.target.value)} />
        </Field>
        <Field id={`${prefix}-close`} label="Close hour override">
          <input type="number" id={`${prefix}-close`} className="inp mono" min={1} max={24} value={values[1]} placeholder={ph(1)} onChange={(e) => set(1, e.target.value)} />
        </Field>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Field id={`${prefix}-min`} label="Min duration override (minutes)">
          <input type="number" id={`${prefix}-min`} className="inp mono" min={5} value={values[2]} placeholder={ph(2)} onChange={(e) => set(2, e.target.value)} />
        </Field>
        <Field id={`${prefix}-max`} label="Max duration override (hours)">
          <input type="number" id={`${prefix}-max`} className="inp mono" min={1} value={values[3]} placeholder={ph(3)} onChange={(e) => set(3, e.target.value)} />
        </Field>
      </div>
    </>
  )
}

/* ─── Building ─── */

export function BuildingFormModal({ editing }: { editing: Building | null }) {
  const save = useSaveBuilding()
  const [name, setName] = useState(editing?.name ?? '')
  const [tz, setTz] = useState(editing?.timeZone ?? 'UTC')
  const [status, setStatus] = useState<EstateStatus>(editing?.status ?? 'Active')
  const [open, setOpen] = useState(String(editing?.openHour ?? 8))
  const [close, setClose] = useState(String(editing?.closeHour ?? 20))
  const [min, setMin] = useState(String(editing?.minBookingMinutes ?? 15))
  const [max, setMax] = useState(String(editing?.maxBookingHours ?? 8))
  const [holidays, setHolidays] = useState<string[]>(editing?.holidays ?? [])
  const [holidayInput, setHolidayInput] = useState('')
  const [error, setError] = useState<FieldError>(null)

  const submit = () => {
    const o = Number(open), c = Number(close), mi = Number(min), ma = Number(max)
    if (!name.trim()) return setError({ code: 'validation.missing_field', message: 'Give the building a name.' })
    if (c <= o) return setError({ code: 'validation.invalid_hours', message: 'Close hour must be after open hour.' })
    if (mi < 5) return setError({ code: 'validation.invalid_hours', message: 'Minimum duration must be at least 5 minutes.' })
    if (ma < 1) return setError({ code: 'validation.invalid_hours', message: 'Maximum duration must be at least 1 hour.' })
    save.mutateAsync({
      id: editing?.id,
      body: { name: name.trim(), timeZone: tz.trim() || 'UTC', status, openHour: o, closeHour: c, minBookingMinutes: mi, maxBookingHours: ma, holidays },
    }).then(() => { toast('ok', editing ? 'Building updated' : 'Building added', name.trim()); modals.close() }, (e) => setError(errorText(e)))
  }

  const addHoliday = () => {
    if (holidayInput && !holidays.includes(holidayInput)) setHolidays([...holidays, holidayInput].sort())
    setHolidayInput('')
  }

  return (
    <Modal title={editing ? 'Edit building' : 'Add a building'} subtitle="One record is one physical building." onClose={modals.close}
      footer={<Footer onSave={submit} label="Save building" busy={save.isPending} />}>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 130px', gap: 12 }}>
        <Field id="bld-name" label="Name">
          <input id="bld-name" className="inp" placeholder="e.g. Riverside Studio" value={name} autoFocus onChange={(e) => { setName(e.target.value); setError(null) }} />
        </Field>
        <Field id="bld-status" label="Status">
          <select id="bld-status" className="inp" value={status} onChange={(e) => setStatus(e.target.value as EstateStatus)}>
            <option value="Active">Active</option><option value="Inactive">Inactive</option>
          </select>
        </Field>
      </div>
      <Field id="bld-tz" label="Local timezone">
        <input id="bld-tz" className="inp mono" placeholder="UTC" value={tz} onChange={(e) => setTz(e.target.value)} />
      </Field>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Field id="bld-open" label="Open hour (UTC)"><input type="number" id="bld-open" className="inp mono" min={0} max={23} value={open} onChange={(e) => setOpen(e.target.value)} /></Field>
        <Field id="bld-close" label="Close hour (UTC)"><input type="number" id="bld-close" className="inp mono" min={1} max={24} value={close} onChange={(e) => setClose(e.target.value)} /></Field>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Field id="bld-min" label="Min duration (minutes)"><input type="number" id="bld-min" className="inp mono" min={5} step={5} value={min} onChange={(e) => setMin(e.target.value)} /></Field>
        <Field id="bld-max" label="Max duration (hours)"><input type="number" id="bld-max" className="inp mono" min={1} step={1} value={max} onChange={(e) => setMax(e.target.value)} /></Field>
      </div>
      <div>
        <label className="lbl" htmlFor="bld-holiday">Holidays</label>
        <div style={{ display: 'flex', gap: 8 }}>
          <input type="date" id="bld-holiday" className="inp mono" style={{ flex: 1 }} value={holidayInput} onChange={(e) => setHolidayInput(e.target.value)} />
          <button type="button" className="btn btn-sm" onClick={addHoliday}>Add</button>
        </div>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginTop: 8 }}>
          {holidays.length ? holidays.map((d) => (
            <span key={d} className="tag mono">
              {d} <button type="button" className="iconbtn" style={{ fontSize: 13, padding: '0 0 0 4px' }} onClick={() => setHolidays(holidays.filter((x) => x !== d))} aria-label={`Remove ${d}`}>×</button>
            </span>
          )) : <span style={{ fontSize: 11.5, color: 'var(--slate)' }}>No holidays yet.</span>}
        </div>
      </div>
      <ErrorLine error={error} />
    </Modal>
  )
}

/* ─── Floor ─── */

export function FloorFormModal({ editing }: { editing: Floor | null }) {
  const buildings = useBuildings().data ?? []
  const save = useSaveFloor()
  const [buildingId, setBuildingId] = useState(editing?.buildingId ?? '')
  const [name, setName] = useState(editing?.name ?? '')
  const [status, setStatus] = useState<EstateStatus>(editing?.status ?? 'Active')
  const [ov, setOv] = useState<[string, string, string, string]>([
    numText(editing?.openHourOverride), numText(editing?.closeHourOverride),
    numText(editing?.minBookingMinutesOverride), numText(editing?.maxBookingHoursOverride),
  ])
  const [error, setError] = useState<FieldError>(null)
  const effBuildingId = buildingId || buildings[0]?.id || ''
  const b = buildings.find((x) => x.id === effBuildingId)

  const submit = () => {
    if (!b) return setError({ code: 'validation.missing_field', message: 'Pick a building first.' })
    if (!name.trim()) return setError({ code: 'validation.missing_field', message: 'Type a floor name or number.' })
    const [o, c, mi, ma] = ov.map(numOrNull)
    if (o != null && o < b.openHour) return setError({ code: 'validation.narrowing_violation', message: `Floor cannot open earlier (${o}) than the building (${b.openHour}).` })
    if (c != null && c > b.closeHour) return setError({ code: 'validation.narrowing_violation', message: `Floor cannot close later (${c}) than the building (${b.closeHour}).` })
    if ((c ?? b.closeHour) <= (o ?? b.openHour)) return setError({ code: 'validation.invalid_hours', message: 'Close hour must be after open hour.' })
    if (mi != null && mi < b.minBookingMinutes) return setError({ code: 'validation.narrowing_violation', message: `Floor's minimum duration cannot be less than the building's (${b.minBookingMinutes}m).` })
    if (ma != null && ma > b.maxBookingHours) return setError({ code: 'validation.narrowing_violation', message: `Floor's maximum duration cannot exceed the building's (${b.maxBookingHours}h).` })
    save.mutateAsync({
      id: editing?.id,
      body: { buildingId: b.id, name: name.trim(), status, openHourOverride: o, closeHourOverride: c, minBookingMinutesOverride: mi, maxBookingHoursOverride: ma },
    }).then(() => { toast('ok', editing ? 'Floor updated' : 'Floor added', `Floor ${name.trim()} in ${b.name}.`); modals.close() }, (e) => setError(errorText(e)))
  }

  return (
    <Modal width={460} title={editing ? 'Edit floor' : 'Add a floor'} subtitle="Inherits its building's hours and duration unless overridden below."
      onClose={modals.close} footer={<Footer onSave={submit} label="Save floor" busy={save.isPending} />}>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Field id="flr-building" label="Building">
          <select id="flr-building" className="inp" value={effBuildingId} disabled={!!editing} onChange={(e) => setBuildingId(e.target.value)}>
            {buildings.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
          </select>
        </Field>
        <Field id="flr-name" label="Floor">
          <input id="flr-name" className="inp mono" placeholder="e.g. 5" value={name} autoFocus onChange={(e) => { setName(e.target.value); setError(null) }} />
        </Field>
      </div>
      <Field id="flr-status" label="Status">
        <select id="flr-status" className="inp" value={status} onChange={(e) => setStatus(e.target.value as EstateStatus)}>
          <option value="Active">Active</option><option value="Inactive">Inactive</option>
        </select>
      </Field>
      <OverrideFields prefix="flr" values={ov} onChange={setOv}
        inherits={b ? [b.openHour, b.closeHour, b.minBookingMinutes, b.maxBookingHours] : null} />
      <ErrorLine error={error} />
    </Modal>
  )
}

/* ─── Space ─── */

export function SpaceFormModal({ editing }: { editing: Space | null }) {
  const buildings = useBuildings().data ?? []
  const floors = useFloors().data ?? []
  const save = useSaveSpace()
  const [name, setName] = useState(editing?.name ?? '')
  const [type, setType] = useState<SpaceType>(editing?.type ?? 'MeetingRoom')
  const [capacity, setCapacity] = useState(String(editing?.capacity ?? 0))
  const [status, setStatus] = useState<EstateStatus>(editing?.status ?? 'Active')
  const [buildingId, setBuildingId] = useState(editing?.buildingId ?? '')
  const [floorId, setFloorId] = useState(editing?.floorId ?? '')
  const [tz, setTz] = useState<string | null>(editing?.timeZone ?? null)
  const [note, setNote] = useState(editing?.note ?? '')
  const [ov, setOv] = useState<[string, string, string, string]>([
    numText(editing?.openHourOverride), numText(editing?.closeHourOverride),
    numText(editing?.minBookingMinutesOverride), numText(editing?.maxBookingHoursOverride),
  ])
  const [error, setError] = useState<FieldError>(null)

  const effBuildingId = buildingId || buildings[0]?.id || ''
  const b = buildings.find((x) => x.id === effBuildingId)
  const bFloors = floors.filter((f) => f.buildingId === effBuildingId)
  const effFloorId = bFloors.some((f) => f.id === floorId) ? floorId : bFloors[0]?.id ?? ''
  const f = bFloors.find((x) => x.id === effFloorId)
  const bounds = b ? [
    f?.openHourOverride ?? b.openHour, f?.closeHourOverride ?? b.closeHour,
    f?.minBookingMinutesOverride ?? b.minBookingMinutes, f?.maxBookingHoursOverride ?? b.maxBookingHours,
  ] as [number, number, number, number] : null
  const effTz = tz ?? b?.timeZone ?? 'UTC'

  const submit = () => {
    if (!name.trim()) return setError({ code: 'validation.missing_field', message: 'Give the space a name.' })
    if (!b) return setError({ code: 'validation.missing_field', message: 'Pick a building. Add one first if the list is empty.' })
    if (!f) return setError({ code: 'validation.missing_field', message: `${b.name} has no floors yet. Add a floor to it first.` })
    const [o, c, mi, ma] = ov.map(numOrNull)
    const [bo, bc, bmi, bma] = bounds!
    if (o != null && o < bo) return setError({ code: 'validation.narrowing_violation', message: `Space cannot open earlier (${o}) than its floor (${bo}).` })
    if (c != null && c > bc) return setError({ code: 'validation.narrowing_violation', message: `Space cannot close later (${c}) than its floor (${bc}).` })
    if ((c ?? bc) <= (o ?? bo)) return setError({ code: 'validation.invalid_hours', message: 'Close hour must be after open hour.' })
    if (mi != null && mi < bmi) return setError({ code: 'validation.narrowing_violation', message: `Space's minimum duration cannot be less than its floor's (${bmi}m).` })
    if (ma != null && ma > bma) return setError({ code: 'validation.narrowing_violation', message: `Space's maximum duration cannot exceed its floor's (${bma}h).` })
    save.mutateAsync({
      id: editing?.id,
      body: {
        name: name.trim(), type, status, buildingId: b.id, floorId: f.id, timeZone: effTz.trim() || 'UTC',
        capacity: Math.max(0, Number(capacity) || 0), note: note.trim(),
        openHourOverride: o, closeHourOverride: c, minBookingMinutesOverride: mi, maxBookingHoursOverride: ma,
      },
    }).then(() => {
      toast('ok', editing ? 'Space updated' : 'Space added',
        editing ? `${name.trim()} in ${b.name}, floor ${f.name}.` : `${name.trim()} is ${status === 'Active' ? 'bookable now' : 'saved as inactive'}.`)
      modals.close()
    }, (e) => setError(errorText(e)))
  }

  return (
    <Modal title={editing ? 'Edit space' : 'Add a space'}
      subtitle={editing ? `${shortId(editing.id, 'SP')} · changes apply to new bookings straight away` : 'One record is one physical unit.'}
      onClose={modals.close} footer={<Footer onSave={submit} label={editing ? 'Save changes' : 'Add space'} busy={save.isPending} />}>
      <Field id="sp-name" label="Name">
        <input id="sp-name" className="inp" placeholder="e.g. Conference Room D" value={name} autoFocus onChange={(e) => { setName(e.target.value); setError(null) }} />
      </Field>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 100px 1fr', gap: 12 }}>
        <Field id="sp-type" label="Type">
          <select id="sp-type" className="inp" value={type} onChange={(e) => setType(e.target.value as SpaceType)}>
            {(Object.keys(SPACE_TYPE_LABELS) as SpaceType[]).map((t) => <option key={t} value={t}>{SPACE_TYPE_LABELS[t]}</option>)}
          </select>
        </Field>
        <Field id="sp-capacity" label="Capacity">
          <input type="number" id="sp-capacity" className="inp mono" min={0} value={capacity} onChange={(e) => setCapacity(e.target.value)} />
        </Field>
        <Field id="sp-status" label="Status">
          <select id="sp-status" className="inp" value={status} onChange={(e) => setStatus(e.target.value as EstateStatus)}>
            <option value="Active">Active — bookable</option><option value="Inactive">Inactive — not bookable</option>
          </select>
        </Field>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 110px 1fr', gap: 12 }}>
        <Field id="sp-building" label="Building">
          <select id="sp-building" className="inp" value={effBuildingId} onChange={(e) => { setBuildingId(e.target.value); setFloorId(''); if (!editing) setTz(null) }}>
            {buildings.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
          </select>
        </Field>
        <Field id="sp-floor" label="Floor">
          <select id="sp-floor" className="inp" value={effFloorId} onChange={(e) => setFloorId(e.target.value)}>
            {bFloors.length ? bFloors.map((x) => <option key={x.id} value={x.id}>{x.name}</option>) : <option value="">—</option>}
          </select>
        </Field>
        <Field id="sp-tz" label="Local timezone">
          <input id="sp-tz" className="inp mono" value={effTz} onChange={(e) => setTz(e.target.value)} />
        </Field>
      </div>
      <OverrideFields prefix="sp" values={ov} onChange={setOv} inherits={bounds} />
      <Field id="sp-note" label="Description">
        <input id="sp-note" className="inp" placeholder="Seats 8 · whiteboard wall" value={note} onChange={(e) => setNote(e.target.value)} />
      </Field>
      <ErrorLine error={error} />
    </Modal>
  )
}

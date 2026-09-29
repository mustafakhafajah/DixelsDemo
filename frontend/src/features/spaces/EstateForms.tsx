import { useState, type ReactNode } from 'react'
import { useBuildings, useFloors, useSaveBuilding, useSaveFloor, useSaveSpace, useSaveSpaceType, useSpaceTypes } from '../../api/hooks'
import type { Building, Floor, Space, SpaceType } from '../../api/types'
import { ErrorLine, plural, RequiredMark } from '../../components/bits'
import { DatePicker, Dropdown } from '../../components/pickers'
import { Modal } from '../../components/Sheet'
import { isValidTimeZone, WEEKDAYS } from '../../lib/closedDays'
import { useFieldErrors, type FieldError, type FieldErrors } from '../../lib/useFieldErrors'
import { modals } from '../../state/modalStore'
import { BookableField, useCancelUpcomingAfterSave } from './Bookable'
import { toast } from '../../state/toastStore'

const numOrNull = (v: string) => (v.trim() === '' ? null : Number(v))
const numText = (v: number | null | undefined) => (v == null ? '' : String(v))
const err = (message: string, code = 'validation.invalid_hours'): FieldError => ({ code, message })
const missing = (message: string): FieldError => ({ code: 'validation.missing_field', message })

function Footer({ onSave, label, busy }: { onSave: () => void; label: string; busy: boolean }) {
  return (
    <>
      <button type="button" className="btn" onClick={modals.close}>Discard</button>
      <button type="button" className="btn btn-primary" disabled={busy} onClick={onSave}>{label}</button>
    </>
  )
}

/* required: bold label with a "*" after the text (styles in appShell.css). error: shown right under the input. */
function Field({ id, label, required, error, children }: { id: string; label: string; required?: boolean; error?: FieldError; children: ReactNode }) {
  return (
    <div>
      <label className={`lbl${required ? ' req' : ''}`} htmlFor={id}>{label}{required && <RequiredMark />}</label>
      {children}
      <ErrorLine id={`${id}-error`} error={error} />
    </div>
  )
}

type Overrides = [string, string, string, string]
const OVERRIDE_KEYS = ['open', 'close', 'min', 'max'] as const
const OVERRIDE_SERVER_FIELDS = ['openHourOverride', 'closeHourOverride', 'minBookingMinutesOverride', 'maxBookingHoursOverride']
const overrideServerFields = (prefix: string) =>
  Object.fromEntries(OVERRIDE_SERVER_FIELDS.map((f, i) => [f, `${prefix}-${OVERRIDE_KEYS[i]}`]))

/* The same checks the server makes on a floor's or space's overrides: each may only narrow its parent
 * (subject "Floor" or "Space", parent e.g. "the building" / "its floor"), and close stays after open. */
function checkOverrides(prefix: string, ov: Overrides, bounds: [number, number, number, number], subject: string, parent: string, parentPossessive: string): FieldErrors {
  const [o, c, mi, ma] = ov.map(numOrNull)
  const [bo, bc, bmi, bma] = bounds
  const out: FieldErrors = {}
  if (o != null && o < bo) out[`${prefix}-open`] = err(`${subject} cannot open earlier (${o}) than ${parent} (${bo}).`, 'validation.narrowing_violation')
  if (c != null && c > bc) out[`${prefix}-close`] = err(`${subject} cannot close later (${c}) than ${parent} (${bc}).`, 'validation.narrowing_violation')
  if ((c ?? bc) <= (o ?? bo)) out[c != null || o == null ? `${prefix}-close` : `${prefix}-open`] ??= err('Close hour must be after open hour.')
  if (mi != null && mi < bmi) out[`${prefix}-min`] = err(`${subject}'s minimum duration cannot be less than ${parentPossessive} (${bmi}m).`, 'validation.narrowing_violation')
  if (ma != null && ma > bma) out[`${prefix}-max`] = err(`${subject}'s maximum duration cannot exceed ${parentPossessive} (${bma}h).`, 'validation.narrowing_violation')
  return out
}

/* Open/close/min/max overrides. An override may only narrow what the parent allows (the same rule
 * the server enforces), so each field shows the range it may take, e.g. "min 8 · max 19".
 * Ranges react to the other fields: open must stay before close, min length within max length. */
function OverrideFields({ prefix, values, onChange, inherits, parent, errors }: {
  prefix: string
  values: Overrides
  onChange: (v: Overrides) => void
  inherits: [number, number, number, number] | null
  /* "building" or "floor", for the hint. */
  parent: string
  errors: FieldErrors
}) {
  const set = (i: number, v: string) => { const next = [...values] as Overrides; next[i] = v; onChange(next) }
  const [o, c, mi, ma] = values.map(numOrNull)
  const ranges = inherits ? (() => {
    const [pOpen, pClose, pMin, pMax] = inherits
    return [
      [pOpen, (c ?? pClose) - 1],
      [(o ?? pOpen) + 1, pClose],
      [pMin, (ma ?? pMax) * 60],
      [Math.max(1, Math.ceil((mi ?? pMin) / 60)), pMax],
    ] as [number, number][]
  })() : null
  const ph = (i: number) => (ranges ? `min ${ranges[i][0]} · max ${ranges[i][1]}` : '')
  const field = (i: number, label: string) => {
    const id = `${prefix}-${OVERRIDE_KEYS[i]}`
    return (
      <Field id={id} label={label} error={errors[id]}>
        <input type="number" id={id} className="inp mono" value={values[i]} placeholder={ph(i)}
          min={ranges?.[i][0]} max={ranges?.[i][1]} onChange={(e) => set(i, e.target.value)} />
      </Field>
    )
  }
  return (
    <>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        {field(0, 'Open hour override')}
        {field(1, 'Close hour override')}
      </div>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        {field(2, 'Min duration override (minutes)')}
        {field(3, 'Max duration override (hours)')}
      </div>
      {inherits && (
        <p style={{ fontSize: 11.5, color: 'var(--slate)', marginTop: -6 }}>
          Leave a field empty to use the {parent}'s value: open {inherits[0]}:00–{inherits[1]}:00, bookings {inherits[2]} min to {inherits[3]} h.
        </p>
      )}
    </>
  )
}

/* A whole number within [lo, hi], or the message saying what is wrong with it. */
function checkNumber(value: string, label: string, lo: number, hi: number, unit = ''): FieldError | undefined {
  if (value.trim() === '') return missing(`Enter the ${label}.`)
  const n = Number(value)
  if (!Number.isInteger(n) || n < lo || n > hi) return err(`The ${label} must be a whole number from ${lo} to ${hi}${unit}.`)
  return undefined
}

/* ─── Building ─── */

const BUILDING_SERVER_FIELDS = {
  name: 'bld-name', timeZone: 'bld-tz', openHour: 'bld-open', closeHour: 'bld-close',
  minBookingMinutes: 'bld-min', maxBookingHours: 'bld-max', closedWeekdays: 'bld-weekdays', holidays: 'bld-holiday',
}

export function BuildingFormModal({ editing }: { editing: Building | null }) {
  const save = useSaveBuilding()
  const fields = useFieldErrors()
  const { errors } = fields
  const [name, setName] = useState(editing?.name ?? '')
  const [tz, setTz] = useState(editing?.timeZone ?? 'UTC')
  const [isBookable, setIsBookable] = useState(editing?.isBookable ?? true)
  const [cancelUpcoming, setCancelUpcoming] = useState(false)
  const cancelAfter = useCancelUpcomingAfterSave()
  const [open, setOpen] = useState(String(editing?.openHour ?? 8))
  const [close, setClose] = useState(String(editing?.closeHour ?? 20))
  const [min, setMin] = useState(String(editing?.minBookingMinutes ?? 15))
  const [max, setMax] = useState(String(editing?.maxBookingHours ?? 8))
  const [holidays, setHolidays] = useState<string[]>(editing?.holidays ?? [])
  const [holidayInput, setHolidayInput] = useState('')
  const [closedWeekdays, setClosedWeekdays] = useState<number[]>(editing?.closedWeekdays ?? [])
  const tzValid = isValidTimeZone(tz.trim() || 'UTC')
  /* The time zone is checked as it is typed, so its message shows before saving too. */
  const tzError = errors['bld-tz'] ?? (tzValid ? undefined
    : err(`"${tz.trim()}" is not a known time zone. Use a name like Europe/Warsaw or Asia/Dubai.`, 'validation.time_zone_unknown'))

  const submit = () => {
    const o = Number(open), c = Number(close), mi = Number(min), ma = Number(max)
    const found: FieldErrors = {
      'bld-name': name.trim() ? undefined : missing('Give the building a name.'),
      'bld-tz': tzError,
      'bld-open': checkNumber(open, 'open hour', 0, 23),
      'bld-close': checkNumber(close, 'close hour', 1, 24),
      'bld-min': checkNumber(min, 'minimum duration', 5, 1440, ' minutes'),
      'bld-max': checkNumber(max, 'maximum duration', 1, 24, ' hours'),
    }
    if (!found['bld-open'] && !found['bld-close'] && c <= o) found['bld-close'] = err('Close hour must be after open hour.')
    if (fields.show(found)) return
    save.mutateAsync({
      id: editing?.id,
      body: { name: name.trim(), timeZone: tz.trim() || 'UTC', isBookable, openHour: o, closeHour: c, minBookingMinutes: mi, maxBookingHours: ma, holidays, closedWeekdays },
    }).then(async () => {
      if (editing && cancelUpcoming && !isBookable) await cancelAfter('building', editing.id, name.trim())
      toast('ok', editing ? 'Building updated' : 'Building added', name.trim())
      modals.close()
    }, (e) => fields.fromServer(e, BUILDING_SERVER_FIELDS))
  }

  const addHoliday = () => {
    if (!holidayInput) return fields.show({ ...errors, 'bld-holiday': missing('Pick a date, then press Add.') })
    if (!holidays.includes(holidayInput)) setHolidays([...holidays, holidayInput].sort())
    setHolidayInput('')
    fields.clear('bld-holiday')
  }

  return (
    <Modal title={editing ? 'Edit building' : 'Add a building'} subtitle="One record is one physical building." onClose={modals.close}
      footer={<Footer onSave={submit} label="Save building" busy={save.isPending} />}>
      <p className="req-note"><RequiredMark /> Required field</p>
      <Field id="bld-name" label="Name" required error={errors['bld-name']}>
        <input id="bld-name" className="inp" placeholder="e.g. Riverside Studio" value={name} autoFocus onChange={(e) => { setName(e.target.value); fields.clear('bld-name') }} />
      </Field>
      <Field id="bld-tz" label="Local timezone" required error={tzError}>
        <input id="bld-tz" className="inp mono" placeholder="UTC" value={tz} onChange={(e) => { setTz(e.target.value); fields.clear('bld-tz') }} />
        {!tzError && <p className="card-sub" style={{ marginTop: 5 }}>Holidays and closed days follow this time zone, e.g. Europe/Warsaw or Asia/Dubai.</p>}
      </Field>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Field id="bld-open" label="Open hour (UTC)" required error={errors['bld-open']}>
          <input type="number" id="bld-open" className="inp mono" min={0} max={23} value={open} onChange={(e) => { setOpen(e.target.value); fields.clear('bld-open', 'bld-close') }} />
        </Field>
        <Field id="bld-close" label="Close hour (UTC)" required error={errors['bld-close']}>
          <input type="number" id="bld-close" className="inp mono" min={1} max={24} value={close} onChange={(e) => { setClose(e.target.value); fields.clear('bld-open', 'bld-close') }} />
        </Field>
      </div>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Field id="bld-min" label="Min duration (minutes)" required error={errors['bld-min']}>
          <input type="number" id="bld-min" className="inp mono" min={5} step={5} value={min} onChange={(e) => { setMin(e.target.value); fields.clear('bld-min') }} />
        </Field>
        <Field id="bld-max" label="Max duration (hours)" required error={errors['bld-max']}>
          <input type="number" id="bld-max" className="inp mono" min={1} step={1} value={max} onChange={(e) => { setMax(e.target.value); fields.clear('bld-max') }} />
        </Field>
      </div>
      <div>
        <label className="lbl">Closed every week</label>
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }} role="group" aria-label="Closed every week">
          {WEEKDAYS.map(([d, label]) => (
            <button key={d} type="button" className={`dur-chip${closedWeekdays.includes(d) ? ' active' : ''}`} aria-pressed={closedWeekdays.includes(d)}
              onClick={() => { setClosedWeekdays(closedWeekdays.includes(d) ? closedWeekdays.filter((x) => x !== d) : [...closedWeekdays, d].sort()); fields.clear('bld-weekdays') }}>
              {label}
            </button>
          ))}
        </div>
        {errors['bld-weekdays']
          ? <ErrorLine id="bld-weekdays-error" error={errors['bld-weekdays']} />
          : <p className="card-sub" style={{ marginTop: 6 }}>Nobody can book any space in this building on these days, every week.</p>}
      </div>
      <div>
        <label className="lbl" htmlFor="bld-holiday">Holidays</label>
        <div style={{ display: 'flex', gap: 8 }}>
          <div style={{ flex: 1 }}><DatePicker id="bld-holiday" value={holidayInput} onChange={(v) => { setHolidayInput(v); fields.clear('bld-holiday') }} /></div>
          <button type="button" className="btn btn-sm" onClick={addHoliday}>Add</button>
        </div>
        <ErrorLine id="bld-holiday-error" error={errors['bld-holiday']} />
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginTop: 8 }}>
          {holidays.length ? holidays.map((d) => (
            <span key={d} className="tag mono">
              {d} <button type="button" className="iconbtn" style={{ fontSize: 13, padding: '0 0 0 4px' }} onClick={() => setHolidays(holidays.filter((x) => x !== d))} aria-label={`Remove ${d}`}>×</button>
            </span>
          )) : <span style={{ fontSize: 11.5, color: 'var(--slate)' }}>No holidays yet.</span>}
        </div>
      </div>
      <BookableField idPrefix="bld" kind="building" value={isBookable} onChange={setIsBookable}
        existingId={editing?.id} wasBookable={editing?.isBookable} cancelUpcoming={cancelUpcoming} onCancelUpcomingChange={setCancelUpcoming} />
    </Modal>
  )
}

/* ─── Floor ─── */

const FLOOR_SERVER_FIELDS = { buildingId: 'flr-building', name: 'flr-name', ...overrideServerFields('flr') }

export function FloorFormModal({ editing }: { editing: Floor | null }) {
  const buildings = useBuildings().data ?? []
  const save = useSaveFloor()
  const fields = useFieldErrors()
  const { errors } = fields
  const [buildingId, setBuildingId] = useState(editing?.buildingId ?? '')
  const [name, setName] = useState(editing?.name ?? '')
  const [isBookable, setIsBookable] = useState(editing?.isBookable ?? true)
  const [cancelUpcoming, setCancelUpcoming] = useState(false)
  const cancelAfter = useCancelUpcomingAfterSave()
  const [ov, setOv] = useState<Overrides>([
    numText(editing?.openHourOverride), numText(editing?.closeHourOverride),
    numText(editing?.minBookingMinutesOverride), numText(editing?.maxBookingHoursOverride),
  ])
  const effBuildingId = buildingId || buildings[0]?.id || ''
  const b = buildings.find((x) => x.id === effBuildingId)

  const submit = () => {
    const found: FieldErrors = {
      'flr-building': b ? undefined : missing('Pick a building first.'),
      'flr-name': name.trim() ? undefined : missing('Type a floor name or number.'),
      ...(b ? checkOverrides('flr', ov, [b.openHour, b.closeHour, b.minBookingMinutes, b.maxBookingHours], 'Floor', 'the building', "the building's") : {}),
    }
    if (fields.show(found) || !b) return
    const [o, c, mi, ma] = ov.map(numOrNull)
    save.mutateAsync({
      id: editing?.id,
      body: { buildingId: b.id, name: name.trim(), isBookable, openHourOverride: o, closeHourOverride: c, minBookingMinutesOverride: mi, maxBookingHoursOverride: ma },
    }).then(async () => {
      if (editing && cancelUpcoming && !isBookable) await cancelAfter('floor', editing.id, `${b.name} · Floor ${name.trim()}`)
      toast('ok', editing ? 'Floor updated' : 'Floor added', `Floor ${name.trim()} in ${b.name}.`)
      modals.close()
    }, (e) => fields.fromServer(e, FLOOR_SERVER_FIELDS))
  }

  return (
    <Modal width={460} title={editing ? 'Edit floor' : 'Add a floor'} subtitle="Inherits its building's hours and duration unless overridden below."
      onClose={modals.close} footer={<Footer onSave={submit} label="Save floor" busy={save.isPending} />}>
      <p className="req-note"><RequiredMark /> Required field</p>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Field id="flr-building" label="Building" required error={errors['flr-building']}>
          <Dropdown id="flr-building" value={effBuildingId} disabled={!!editing}
            onChange={(v) => { setBuildingId(v); fields.clear('flr-building', 'flr-open', 'flr-close', 'flr-min', 'flr-max') }}
            options={buildings.map((x) => ({ value: x.id, label: x.name }))} />
        </Field>
        <Field id="flr-name" label="Floor" required error={errors['flr-name']}>
          <input id="flr-name" className="inp mono" placeholder="e.g. 5" value={name} autoFocus onChange={(e) => { setName(e.target.value); fields.clear('flr-name') }} />
        </Field>
      </div>
      <OverrideFields prefix="flr" parent="building" values={ov} errors={errors}
        onChange={(v) => { setOv(v); fields.clear('flr-open', 'flr-close', 'flr-min', 'flr-max') }}
        inherits={b ? [b.openHour, b.closeHour, b.minBookingMinutes, b.maxBookingHours] : null} />
      <BookableField idPrefix="flr" kind="floor" value={isBookable} onChange={setIsBookable}
        existingId={editing?.id} wasBookable={editing?.isBookable} cancelUpcoming={cancelUpcoming} onCancelUpcomingChange={setCancelUpcoming} />
    </Modal>
  )
}

/* ─── Space ─── */

const SPACE_SERVER_FIELDS = {
  name: 'sp-name', typeId: 'sp-type', capacity: 'sp-capacity', buildingId: 'sp-building', floorId: 'sp-floor', note: 'sp-note',
  ...overrideServerFields('sp'),
}
const SPACE_OVERRIDE_IDS = ['sp-open', 'sp-close', 'sp-min', 'sp-max']

export function SpaceFormModal({ editing }: { editing: Space | null }) {
  const buildings = useBuildings().data ?? []
  const floors = useFloors().data ?? []
  const types = useSpaceTypes().data ?? []
  const save = useSaveSpace()
  const fields = useFieldErrors()
  const { errors } = fields
  const [name, setName] = useState(editing?.name ?? '')
  const [typeId, setTypeId] = useState(editing?.typeId ?? '')
  const [capacity, setCapacity] = useState(String(editing?.capacity ?? 0))
  const [isBookable, setIsBookable] = useState(editing?.isBookable ?? true)
  const [cancelUpcoming, setCancelUpcoming] = useState(false)
  const cancelAfter = useCancelUpcomingAfterSave()
  const [buildingId, setBuildingId] = useState(editing?.buildingId ?? '')
  const [floorId, setFloorId] = useState(editing?.floorId ?? '')
  const [note, setNote] = useState(editing?.note ?? '')
  const [ov, setOv] = useState<Overrides>([
    numText(editing?.openHourOverride), numText(editing?.closeHourOverride),
    numText(editing?.minBookingMinutesOverride), numText(editing?.maxBookingHoursOverride),
  ])

  /* Nothing is pre-picked: choose the building, then one of its floors. */
  const b = buildings.find((x) => x.id === buildingId)
  const bFloors = b ? floors.filter((f) => f.buildingId === b.id) : []
  const effFloorId = bFloors.some((f) => f.id === floorId) ? floorId : ''
  const f = bFloors.find((x) => x.id === effFloorId)
  const bounds = b ? [
    f?.openHourOverride ?? b.openHour, f?.closeHourOverride ?? b.closeHour,
    f?.minBookingMinutesOverride ?? b.minBookingMinutes, f?.maxBookingHoursOverride ?? b.maxBookingHours,
  ] as [number, number, number, number] : null
  const effTypeId = typeId || types[0]?.id || ''

  const submit = () => {
    const cap = capacity.trim() === '' ? 0 : Number(capacity)
    const found: FieldErrors = {
      'sp-name': name.trim() ? undefined : missing('Give the space a name.'),
      'sp-type': effTypeId ? undefined : err('Pick a space type. Add one on the Space types page first.', 'validation.invalid_space_type'),
      'sp-capacity': Number.isInteger(cap) && cap >= 0 && cap <= 999 ? undefined : err('Capacity must be a whole number from 0 to 999.', 'validation.invalid_request'),
      'sp-building': b ? undefined : missing('Pick a building. Add one first if the list is empty.'),
      'sp-floor': !b ? undefined : !bFloors.length ? missing(`${b.name} has no floors yet. Add a floor to it first.`) : f ? undefined : missing('Pick a floor.'),
      'sp-note': note.trim().length > 500 ? err('Keep the description to 500 characters.', 'validation.invalid_request') : undefined,
      ...(bounds && f ? checkOverrides('sp', ov, bounds, 'Space', 'its floor', "its floor's") : {}),
    }
    if (fields.show(found) || !b || !f) return
    const [o, c, mi, ma] = ov.map(numOrNull)
    save.mutateAsync({
      id: editing?.id,
      body: {
        name: name.trim(), typeId: effTypeId, isBookable, buildingId: b.id, floorId: f.id,
        capacity: cap, note: note.trim(),
        openHourOverride: o, closeHourOverride: c, minBookingMinutesOverride: mi, maxBookingHoursOverride: ma,
      },
    }).then(async () => {
      if (editing && cancelUpcoming && !isBookable) await cancelAfter('space', editing.id, name.trim())
      toast('ok', editing ? 'Space updated' : 'Space added',
        editing ? `${name.trim()} in ${b.name}, floor ${f.name}.` : `${name.trim()} is ${isBookable ? 'bookable now' : 'saved as not bookable'}.`)
      modals.close()
    }, (e) => fields.fromServer(e, SPACE_SERVER_FIELDS))
  }

  return (
    <Modal title={editing ? 'Edit space' : 'Add a space'}
      subtitle={editing ? 'Changes apply to new bookings straight away.' : 'One record is one physical unit.'}
      onClose={modals.close} footer={<Footer onSave={submit} label={editing ? 'Save changes' : 'Add space'} busy={save.isPending} />}>
      <p className="req-note"><RequiredMark /> Required field</p>
      <Field id="sp-name" label="Name" required error={errors['sp-name']}>
        <input id="sp-name" className="inp" placeholder="e.g. Conference Room D" value={name} autoFocus onChange={(e) => { setName(e.target.value); fields.clear('sp-name') }} />
      </Field>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 120px', gap: 12 }}>
        <Field id="sp-type" label="Type" required error={errors['sp-type']}>
          <Dropdown id="sp-type" value={effTypeId} onChange={(v) => { setTypeId(v); fields.clear('sp-type') }} disabled={!types.length}
            placeholder={types.length ? 'Choose a type' : 'No types yet'}
            options={types.map((t) => ({ value: t.id, label: t.name }))} />
        </Field>
        <Field id="sp-capacity" label="Capacity" error={errors['sp-capacity']}>
          <input type="number" id="sp-capacity" className="inp mono" min={0} max={999} value={capacity} onChange={(e) => { setCapacity(e.target.value); fields.clear('sp-capacity') }} />
        </Field>
      </div>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 110px 1fr', gap: 12 }}>
        <Field id="sp-building" label="Building" required error={errors['sp-building']}>
          <Dropdown id="sp-building" value={buildingId} placeholder="Choose a building"
            onChange={(v) => { setBuildingId(v); setFloorId(''); fields.clear('sp-building', 'sp-floor', ...SPACE_OVERRIDE_IDS) }}
            options={buildings.map((x) => ({ value: x.id, label: x.name }))} />
        </Field>
        <Field id="sp-floor" label="Floor" required error={errors['sp-floor']}>
          <Dropdown id="sp-floor" value={effFloorId} onChange={(v) => { setFloorId(v); fields.clear('sp-floor', ...SPACE_OVERRIDE_IDS) }}
            disabled={!b || !bFloors.length} placeholder={!b ? 'Building first' : bFloors.length ? 'Choose' : 'No floors'}
            options={bFloors.map((x) => ({ value: x.id, label: x.name }))} />
        </Field>
        {/* A space always uses its building's time zone; it is changed on the building. */}
        <Field id="sp-tz" label="Time zone (building)">
          <input id="sp-tz" className="inp mono" value={b?.timeZone ?? ''} readOnly disabled title="Set on the building" />
        </Field>
      </div>
      <OverrideFields prefix="sp" parent="floor" values={ov} errors={errors} inherits={bounds}
        onChange={(v) => { setOv(v); fields.clear(...SPACE_OVERRIDE_IDS) }} />
      <Field id="sp-note" label="Description" error={errors['sp-note']}>
        <input id="sp-note" className="inp" placeholder="Seats 8 · whiteboard wall" value={note} maxLength={500} onChange={(e) => { setNote(e.target.value); fields.clear('sp-note') }} />
      </Field>
      <BookableField idPrefix="sp" kind="space" value={isBookable} onChange={setIsBookable}
        existingId={editing?.id} wasBookable={editing?.isBookable} cancelUpcoming={cancelUpcoming} onCancelUpcomingChange={setCancelUpcoming} />
      {/* The space's own tick can be on while its floor or building is off: say so. */}
      {isBookable && b && (!b.isBookable || (f && !f.isBookable)) && (
        <p className="muted-box">
          Still not bookable while {!b.isBookable ? `${b.name} is not bookable` : `floor ${f!.name} is not bookable`}.
        </p>
      )}
    </Modal>
  )
}

/* ─── Space type ─── */

export function SpaceTypeFormModal({ editing }: { editing: SpaceType | null }) {
  const save = useSaveSpaceType()
  const fields = useFieldErrors()
  const [name, setName] = useState(editing?.name ?? '')

  const submit = () => {
    if (fields.show({ 'st-name': name.trim() ? undefined : missing('Give the space type a name.') })) return
    save.mutateAsync({ id: editing?.id, name: name.trim() }).then(() => {
      toast('ok', editing ? 'Space type renamed' : 'Space type added',
        editing ? `${editing.name} is now ${name.trim()}.` : `${name.trim()} can now be picked for any space.`)
      modals.close()
    }, (e) => fields.fromServer(e, { name: 'st-name' }))
  }

  const used = editing ? `Used by ${plural(editing.spaceCount, 'space')}; they all show the new name.` : 'A kind of space, like "Phone booth" or "Lab".'
  return (
    <Modal width={420} title={editing ? 'Edit space type' : 'Add a space type'} subtitle={used}
      onClose={modals.close} footer={<Footer onSave={submit} label={editing ? 'Save changes' : 'Add type'} busy={save.isPending} />}>
      <p className="req-note"><RequiredMark /> Required field</p>
      <Field id="st-name" label="Name" required error={fields.errors['st-name']}>
        <input id="st-name" className="inp" placeholder="e.g. Phone booth" value={name} autoFocus maxLength={64}
          onChange={(e) => { setName(e.target.value); fields.clear('st-name') }}
          onKeyDown={(e) => { if (e.key === 'Enter') submit() }} />
      </Field>
    </Modal>
  )
}

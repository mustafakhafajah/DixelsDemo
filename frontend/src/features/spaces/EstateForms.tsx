import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import i18n from 'i18next'
import { useBuildings, useFloors, useSaveBuilding, useSaveFloor, useSaveSpace, useSaveSpaceType, useSpaceTypes } from '../../api/hooks'
import type { Building, Floor, Space, SpaceType } from '../../api/types'
import { ErrorLine, Field, RequiredNote } from '../../components/bits'
import { DatePicker, Dropdown } from '../../components/pickers'
import { Modal } from '../../components/Sheet'
import { isValidTimeZone, WEEKDAYS } from '../../lib/closedDays'
import { weekdayName } from '../../lib/dateUtils'
import { useFieldErrors, type FieldError, type FieldErrors } from '../../lib/useFieldErrors'
import { modals } from '../../state/modalStore'
import { BookableField, useCancelUpcomingAfterSave } from './Bookable'
import { LocalizedInput } from './LocalizedInput'
import { checkTranslations, draftsOf, englishOf, NOTE_MAX, scriptProblem, toTranslations, translationServerFields } from './translationDrafts'
import { toast } from '../../state/toastStore'

const numOrNull = (v: string) => (v.trim() === '' ? null : Number(v))
const numText = (v: number | null | undefined) => (v == null ? '' : String(v))
const err = (message: string, code = 'validation.invalid_hours'): FieldError => ({ code, message })
const missing = (message: string): FieldError => ({ code: 'validation.missing_field', message })

function Footer({ onSave, label, busy }: { onSave: () => void; label: string; busy: boolean }) {
  const { t } = useTranslation()
  return (
    <>
      <button type="button" className="btn" onClick={modals.close}>{t('common.discard')}</button>
      <button type="button" className="btn btn-primary" disabled={busy} onClick={onSave}>{label}</button>
    </>
  )
}

type Overrides = [string, string, string, string]
const OVERRIDE_KEYS = ['open', 'close', 'min', 'max'] as const
const OVERRIDE_SERVER_FIELDS = ['openHourOverride', 'closeHourOverride', 'minBookingMinutesOverride', 'maxBookingHoursOverride']
const overrideServerFields = (prefix: string) =>
  Object.fromEntries(OVERRIDE_SERVER_FIELDS.map((f, i) => [f, `${prefix}-${OVERRIDE_KEYS[i]}`]))

/* The same checks the server makes on a floor's or space's overrides: each may only narrow its parent
 * (a floor's building, a space's floor), and close stays after open. */
function checkOverrides(prefix: string, ov: Overrides, bounds: [number, number, number, number], kind: 'floor' | 'space'): FieldErrors {
  const [o, c, mi, ma] = ov.map(numOrNull)
  const [bo, bc, bmi, bma] = bounds
  const out: FieldErrors = {}
  if (o != null && o < bo) out[`${prefix}-open`] = err(i18n.t(`forms.override.openEarlier.${kind}`, { value: o, limit: bo }), 'validation.narrowing_violation')
  if (c != null && c > bc) out[`${prefix}-close`] = err(i18n.t(`forms.override.closeLater.${kind}`, { value: c, limit: bc }), 'validation.narrowing_violation')
  if ((c ?? bc) <= (o ?? bo)) out[c != null || o == null ? `${prefix}-close` : `${prefix}-open`] ??= err(i18n.t('forms.closeAfterOpen'))
  if (mi != null && mi < bmi) out[`${prefix}-min`] = err(i18n.t(`forms.override.minBelow.${kind}`, { limit: bmi }), 'validation.narrowing_violation')
  if (ma != null && ma > bma) out[`${prefix}-max`] = err(i18n.t(`forms.override.maxAbove.${kind}`, { limit: bma }), 'validation.narrowing_violation')
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
  /* Whose values an empty field takes, for the hint. */
  parent: 'building' | 'floor'
  errors: FieldErrors
}) {
  const { t } = useTranslation()
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
  const ph = (i: number) => (ranges ? t('forms.override.range', { min: ranges[i][0], max: ranges[i][1] }) : '')
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
        {field(0, t('forms.override.open'))}
        {field(1, t('forms.override.close'))}
      </div>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        {field(2, t('forms.override.min'))}
        {field(3, t('forms.override.max'))}
      </div>
      {inherits && (
        <p style={{ fontSize: 11.5, color: 'var(--slate)', marginTop: -6 }}>
          {t(`forms.override.inherit.${parent}`, { open: inherits[0], close: inherits[1], min: inherits[2], max: inherits[3] })}
        </p>
      )}
    </>
  )
}

/* A whole number within [lo, hi], or the message saying what is wrong with it. */
function checkNumber(value: string, field: 'open' | 'close' | 'min' | 'max', lo: number, hi: number): FieldError | undefined {
  if (value.trim() === '') return missing(i18n.t(`forms.number.enter.${field}`))
  const n = Number(value)
  if (!Number.isInteger(n) || n < lo || n > hi) return err(i18n.t(`forms.number.range.${field}`, { lo, hi }))
  return undefined
}

/* ─── Building ─── */

const BUILDING_SERVER_FIELDS = {
  name: 'bld-name', timeZone: 'bld-tz', openHour: 'bld-open', closeHour: 'bld-close',
  minBookingMinutes: 'bld-min', maxBookingHours: 'bld-max', closedWeekdays: 'bld-weekdays', holidays: 'bld-holiday',
}

export function BuildingFormModal({ editing }: { editing: Building | null }) {
  const { t } = useTranslation()
  const save = useSaveBuilding()
  const fields = useFieldErrors()
  const { errors } = fields
  const [name, setName] = useState(englishOf(editing?.translations)?.name ?? editing?.name ?? '')
  const [drafts, setDrafts] = useState(draftsOf(editing?.translations))
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
    : err(t('forms.building.tzUnknown', { tz: tz.trim() }), 'validation.time_zone_unknown'))

  const submit = () => {
    const o = Number(open), c = Number(close), mi = Number(min), ma = Number(max)
    const found: FieldErrors = {
      'bld-name': name.trim() ? scriptProblem('en', name) : missing(t('forms.building.nameMissing')),
      'bld-tz': tzError,
      'bld-open': checkNumber(open, 'open', 0, 23),
      'bld-close': checkNumber(close, 'close', 1, 24),
      'bld-min': checkNumber(min, 'min', 5, 1440),
      'bld-max': checkNumber(max, 'max', 1, 24),
      ...checkTranslations('bld', drafts),
    }
    if (!found['bld-open'] && !found['bld-close'] && c <= o) found['bld-close'] = err(t('forms.closeAfterOpen'))
    if (fields.show(found)) return
    save.mutateAsync({
      id: editing?.id,
      body: { name: name.trim(), translations: toTranslations(drafts), timeZone: tz.trim() || 'UTC', isBookable, openHour: o, closeHour: c, minBookingMinutes: mi, maxBookingHours: ma, holidays, closedWeekdays },
    }).then(async () => {
      if (editing && cancelUpcoming && !isBookable) await cancelAfter('building', editing.id, name.trim())
      toast('ok', editing ? t('forms.building.updated') : t('forms.building.added'), name.trim())
      modals.close()
    }, (e) => fields.fromServer(e, { ...BUILDING_SERVER_FIELDS, ...translationServerFields('bld') }))
  }

  const addHoliday = () => {
    if (!holidayInput) return fields.show({ ...errors, 'bld-holiday': missing(t('forms.building.holidayMissing')) })
    if (!holidays.includes(holidayInput)) setHolidays([...holidays, holidayInput].sort())
    setHolidayInput('')
    fields.clear('bld-holiday')
  }

  return (
    <Modal title={editing ? t('forms.building.editTitle') : t('forms.building.addTitle')} subtitle={t('forms.building.subtitle')} onClose={modals.close}
      footer={<Footer onSave={submit} label={t('forms.building.save')} busy={save.isPending} />}>
      <RequiredNote />
      <LocalizedInput prefix="bld" field="name" id="bld-name" label={t('forms.name')} required english={name} onEnglish={setName}
        drafts={drafts} onDrafts={setDrafts} errors={errors} attempt={fields.attempt} clear={fields.clear} placeholder={t('forms.building.namePlaceholder')} autoFocus />
      <Field id="bld-tz" label={t('forms.building.timeZone')} required error={tzError}>
        <input id="bld-tz" className="inp mono" dir="ltr" placeholder="UTC" value={tz} onChange={(e) => { setTz(e.target.value); fields.clear('bld-tz') }} />
        {!tzError && <p className="card-sub" style={{ marginTop: 5 }}>{t('forms.building.tzHint')}</p>}
      </Field>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Field id="bld-open" label={t('forms.building.openHour')} required error={errors['bld-open']}>
          <input type="number" id="bld-open" className="inp mono" min={0} max={23} value={open} onChange={(e) => { setOpen(e.target.value); fields.clear('bld-open', 'bld-close') }} />
        </Field>
        <Field id="bld-close" label={t('forms.building.closeHour')} required error={errors['bld-close']}>
          <input type="number" id="bld-close" className="inp mono" min={1} max={24} value={close} onChange={(e) => { setClose(e.target.value); fields.clear('bld-open', 'bld-close') }} />
        </Field>
      </div>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Field id="bld-min" label={t('forms.building.minDuration')} required error={errors['bld-min']}>
          <input type="number" id="bld-min" className="inp mono" min={5} step={5} value={min} onChange={(e) => { setMin(e.target.value); fields.clear('bld-min') }} />
        </Field>
        <Field id="bld-max" label={t('forms.building.maxDuration')} required error={errors['bld-max']}>
          <input type="number" id="bld-max" className="inp mono" min={1} step={1} value={max} onChange={(e) => { setMax(e.target.value); fields.clear('bld-max') }} />
        </Field>
      </div>
      <div>
        <label className="lbl">{t('forms.building.closedWeekly')}</label>
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }} role="group" aria-label={t('forms.building.closedWeekly')}>
          {WEEKDAYS.map((d) => (
            <button key={d} type="button" className={`dur-chip${closedWeekdays.includes(d) ? ' active' : ''}`} aria-pressed={closedWeekdays.includes(d)}
              onClick={() => { setClosedWeekdays(closedWeekdays.includes(d) ? closedWeekdays.filter((x) => x !== d) : [...closedWeekdays, d].sort()); fields.clear('bld-weekdays') }}>
              {weekdayName(d, 'short')}
            </button>
          ))}
        </div>
        {errors['bld-weekdays']
          ? <ErrorLine id="bld-weekdays-error" error={errors['bld-weekdays']} />
          : <p className="card-sub" style={{ marginTop: 6 }}>{t('forms.building.closedWeeklyHint')}</p>}
      </div>
      <div>
        <label className="lbl" htmlFor="bld-holiday">{t('forms.building.holidays')}</label>
        <div style={{ display: 'flex', gap: 8 }}>
          <div style={{ flex: 1 }}><DatePicker id="bld-holiday" value={holidayInput} onChange={(v) => { setHolidayInput(v); fields.clear('bld-holiday') }} /></div>
          <button type="button" className="btn btn-sm" onClick={addHoliday}>{t('common.add')}</button>
        </div>
        <ErrorLine id="bld-holiday-error" error={errors['bld-holiday']} />
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginTop: 8 }}>
          {holidays.length ? holidays.map((d) => (
            <span key={d} className="tag mono">
              {d} <button type="button" className="iconbtn" style={{ fontSize: 13, padding: 0, paddingInlineStart: 4 }} onClick={() => setHolidays(holidays.filter((x) => x !== d))} aria-label={t('forms.building.removeHoliday', { date: d })}>×</button>
            </span>
          )) : <span style={{ fontSize: 11.5, color: 'var(--slate)' }}>{t('forms.building.noHolidays')}</span>}
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
  const { t } = useTranslation()
  const buildings = useBuildings().data ?? []
  const save = useSaveFloor()
  const fields = useFieldErrors()
  const { errors } = fields
  const [buildingId, setBuildingId] = useState(editing?.buildingId ?? '')
  const [name, setName] = useState(englishOf(editing?.translations)?.name ?? editing?.name ?? '')
  const [drafts, setDrafts] = useState(draftsOf(editing?.translations))
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
      'flr-building': b ? undefined : missing(t('forms.floor.buildingMissing')),
      'flr-name': name.trim() ? scriptProblem('en', name) : missing(t('forms.floor.nameMissing')),
      ...checkTranslations('flr', drafts),
      ...(b ? checkOverrides('flr', ov, [b.openHour, b.closeHour, b.minBookingMinutes, b.maxBookingHours], 'floor') : {}),
    }
    if (fields.show(found) || !b) return
    const [o, c, mi, ma] = ov.map(numOrNull)
    save.mutateAsync({
      id: editing?.id,
      body: { buildingId: b.id, name: name.trim(), translations: toTranslations(drafts), isBookable, openHourOverride: o, closeHourOverride: c, minBookingMinutesOverride: mi, maxBookingHoursOverride: ma },
    }).then(async () => {
      if (editing && cancelUpcoming && !isBookable) await cancelAfter('floor', editing.id, t('common.floorIn', { building: b.name, floor: name.trim() }))
      toast('ok', editing ? t('forms.floor.updated') : t('forms.floor.added'), t('forms.floor.savedMessage', { floor: name.trim(), building: b.name }))
      modals.close()
    }, (e) => fields.fromServer(e, { ...FLOOR_SERVER_FIELDS, ...translationServerFields('flr') }))
  }

  return (
    <Modal width={460} title={editing ? t('forms.floor.editTitle') : t('forms.floor.addTitle')} subtitle={t('forms.floor.subtitle')}
      onClose={modals.close} footer={<Footer onSave={submit} label={t('forms.floor.save')} busy={save.isPending} />}>
      <RequiredNote />
      <Field id="flr-building" label={t('common.building')} required error={errors['flr-building']}>
        <Dropdown id="flr-building" value={effBuildingId} disabled={!!editing}
          onChange={(v) => { setBuildingId(v); fields.clear('flr-building', 'flr-open', 'flr-close', 'flr-min', 'flr-max') }}
          options={buildings.map((x) => ({ value: x.id, label: x.name }))} />
      </Field>
      <LocalizedInput prefix="flr" field="name" id="flr-name" label={t('forms.floor.name')} required english={name} onEnglish={setName}
        drafts={drafts} onDrafts={setDrafts} errors={errors} attempt={fields.attempt} clear={fields.clear} placeholder={t('forms.floor.namePlaceholder')} className="inp mono" autoFocus />
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
  const { t } = useTranslation()
  const buildings = useBuildings().data ?? []
  const types = useSpaceTypes().data ?? []
  const save = useSaveSpace()
  const fields = useFieldErrors()
  const { errors } = fields
  const [name, setName] = useState(englishOf(editing?.translations)?.name ?? editing?.name ?? '')
  const [drafts, setDrafts] = useState(draftsOf(editing?.translations))
  const [typeId, setTypeId] = useState(editing?.typeId ?? '')
  const [capacity, setCapacity] = useState(String(editing?.capacity ?? 0))
  const [isBookable, setIsBookable] = useState(editing?.isBookable ?? true)
  const [cancelUpcoming, setCancelUpcoming] = useState(false)
  const cancelAfter = useCancelUpcomingAfterSave()
  const [buildingId, setBuildingId] = useState(editing?.buildingId ?? '')
  const [floorId, setFloorId] = useState(editing?.floorId ?? '')
  /* The chosen building's floors, asked of the server. */
  const floors = useFloors(buildingId, !!buildingId).data ?? []
  const [note, setNote] = useState((editing ? englishOf(editing.translations)?.note ?? editing.note : null) ?? '')
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
      'sp-name': name.trim() ? scriptProblem('en', name) : missing(t('forms.space.nameMissing')),
      'sp-type': effTypeId ? undefined : err(t('forms.space.typeMissing'), 'validation.invalid_space_type'),
      'sp-capacity': Number.isInteger(cap) && cap >= 0 && cap <= 999 ? undefined : err(t('forms.space.capacityRange'), 'validation.invalid_request'),
      'sp-building': b ? undefined : missing(t('forms.space.buildingMissing')),
      'sp-floor': !b ? undefined : !bFloors.length ? missing(t('forms.space.buildingHasNoFloors', { name: b.name })) : f ? undefined : missing(t('forms.space.floorMissing')),
      'sp-note': note.trim().length > 500 ? err(t('forms.space.noteTooLong'), 'validation.invalid_request') : scriptProblem('en', note),
      ...checkTranslations('sp', drafts, true),
      ...(bounds && f ? checkOverrides('sp', ov, bounds, 'space') : {}),
    }
    if (fields.show(found) || !b || !f) return
    const [o, c, mi, ma] = ov.map(numOrNull)
    save.mutateAsync({
      id: editing?.id,
      body: {
        name: name.trim(), typeId: effTypeId, isBookable, buildingId: b.id, floorId: f.id,
        capacity: cap, note: note.trim(), translations: toTranslations(drafts, true),
        openHourOverride: o, closeHourOverride: c, minBookingMinutesOverride: mi, maxBookingHoursOverride: ma,
      },
    }).then(async () => {
      if (editing && cancelUpcoming && !isBookable) await cancelAfter('space', editing.id, name.trim())
      toast('ok', editing ? t('forms.space.updated') : t('forms.space.added'),
        editing ? t('forms.space.updatedMessage', { name: name.trim(), building: b.name, floor: f.name })
          : isBookable ? t('forms.space.addedBookable', { name: name.trim() }) : t('forms.space.addedNotBookable', { name: name.trim() }))
      modals.close()
    }, (e) => fields.fromServer(e, { ...SPACE_SERVER_FIELDS, ...translationServerFields('sp') }))
  }

  return (
    <Modal title={editing ? t('forms.space.editTitle') : t('forms.space.addTitle')}
      subtitle={editing ? t('forms.space.editSubtitle') : t('forms.space.addSubtitle')}
      onClose={modals.close} footer={<Footer onSave={submit} label={editing ? t('common.saveChanges') : t('forms.space.save')} busy={save.isPending} />}>
      <RequiredNote />
      <LocalizedInput prefix="sp" field="name" id="sp-name" label={t('forms.name')} required english={name} onEnglish={setName}
        drafts={drafts} onDrafts={setDrafts} errors={errors} attempt={fields.attempt} clear={fields.clear} placeholder={t('forms.space.namePlaceholder')} autoFocus />
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 120px', gap: 12 }}>
        <Field id="sp-type" label={t('common.type')} required error={errors['sp-type']}>
          <Dropdown id="sp-type" value={effTypeId} onChange={(v) => { setTypeId(v); fields.clear('sp-type') }} disabled={!types.length}
            placeholder={types.length ? t('forms.space.chooseType') : t('forms.space.noTypes')}
            options={types.map((st) => ({ value: st.id, label: st.name }))} />
        </Field>
        <Field id="sp-capacity" label={t('common.capacity')} error={errors['sp-capacity']}>
          <input type="number" id="sp-capacity" className="inp mono" min={0} max={999} value={capacity} onChange={(e) => { setCapacity(e.target.value); fields.clear('sp-capacity') }} />
        </Field>
      </div>
      <div className="form-cols" style={{ display: 'grid', gridTemplateColumns: '1fr 110px 1fr', gap: 12 }}>
        <Field id="sp-building" label={t('common.building')} required error={errors['sp-building']}>
          <Dropdown id="sp-building" value={buildingId} placeholder={t('common.chooseBuilding')}
            onChange={(v) => { setBuildingId(v); setFloorId(''); fields.clear('sp-building', 'sp-floor', ...SPACE_OVERRIDE_IDS) }}
            options={buildings.map((x) => ({ value: x.id, label: x.name }))} />
        </Field>
        <Field id="sp-floor" label={t('common.floor')} required error={errors['sp-floor']}>
          <Dropdown id="sp-floor" value={effFloorId} onChange={(v) => { setFloorId(v); fields.clear('sp-floor', ...SPACE_OVERRIDE_IDS) }}
            disabled={!b || !bFloors.length} placeholder={!b ? t('common.buildingFirst') : bFloors.length ? t('forms.space.choose') : t('forms.space.noFloors')}
            options={bFloors.map((x) => ({ value: x.id, label: x.name }))} />
        </Field>
        {/* A space always uses its building's time zone; it is changed on the building. */}
        <Field id="sp-tz" label={t('forms.space.timeZone')}>
          <input id="sp-tz" className="inp mono" dir="ltr" value={b?.timeZone ?? ''} readOnly disabled title={t('forms.space.timeZoneTitle')} />
        </Field>
      </div>
      <OverrideFields prefix="sp" parent="floor" values={ov} errors={errors} inherits={bounds}
        onChange={(v) => { setOv(v); fields.clear(...SPACE_OVERRIDE_IDS) }} />
      <LocalizedInput prefix="sp" field="note" id="sp-note" label={t('forms.description')} english={note} onEnglish={setNote}
        drafts={drafts} onDrafts={setDrafts} errors={errors} attempt={fields.attempt} clear={fields.clear} placeholder={t('forms.space.notePlaceholder')} maxLength={NOTE_MAX} />
      <BookableField idPrefix="sp" kind="space" value={isBookable} onChange={setIsBookable}
        existingId={editing?.id} wasBookable={editing?.isBookable} cancelUpcoming={cancelUpcoming} onCancelUpcomingChange={setCancelUpcoming} />
      {/* The space's own tick can be on while its floor or building is off: say so. */}
      {isBookable && b && (!b.isBookable || (f && !f.isBookable)) && (
        <p className="muted-box">
          {!b.isBookable ? t('forms.space.blockedByBuilding', { name: b.name }) : t('forms.space.blockedByFloor', { floor: f!.name })}
        </p>
      )}
    </Modal>
  )
}

/* ─── Space type ─── */

export function SpaceTypeFormModal({ editing }: { editing: SpaceType | null }) {
  const { t } = useTranslation()
  const save = useSaveSpaceType()
  const fields = useFieldErrors()
  const [name, setName] = useState(englishOf(editing?.translations)?.name ?? editing?.name ?? '')
  const [drafts, setDrafts] = useState(draftsOf(editing?.translations))

  const submit = () => {
    if (fields.show({ 'st-name': name.trim() ? scriptProblem('en', name) : missing(t('forms.spaceType.nameMissing')), ...checkTranslations('st', drafts) })) return
    save.mutateAsync({ id: editing?.id, name: name.trim(), translations: toTranslations(drafts) }).then(() => {
      toast('ok', editing ? t('forms.spaceType.renamed') : t('forms.spaceType.added'),
        editing ? t('forms.spaceType.renamedMessage', { from: editing.name, to: name.trim() }) : t('forms.spaceType.addedMessage', { name: name.trim() }))
      modals.close()
    }, (e) => fields.fromServer(e, { name: 'st-name', ...translationServerFields('st') }))
  }

  const used = editing ? t('forms.spaceType.usedBy', { count: editing.spaceCount }) : t('forms.spaceType.addSubtitle')
  return (
    <Modal width={420} title={editing ? t('forms.spaceType.editTitle') : t('forms.spaceType.addTitle')} subtitle={used}
      onClose={modals.close} footer={<Footer onSave={submit} label={editing ? t('common.saveChanges') : t('forms.spaceType.save')} busy={save.isPending} />}>
      <RequiredNote />
      <LocalizedInput prefix="st" field="name" id="st-name" label={t('forms.name')} required english={name} onEnglish={setName}
        drafts={drafts} onDrafts={setDrafts} errors={fields.errors} attempt={fields.attempt} clear={fields.clear} placeholder={t('forms.spaceType.namePlaceholder')}
        maxLength={64} autoFocus onEnter={submit} />
    </Modal>
  )
}

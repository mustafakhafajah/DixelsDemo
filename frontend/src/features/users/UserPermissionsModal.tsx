import { useEffect, useMemo, useState } from 'react'
import { errorText } from '../../api/client'
import { LoadError, Loading } from '../../components/bits'
import { Modal } from '../../components/Sheet'
import { toast } from '../../state/toastStore'
import { useSaveUserPermissions, useUserPermissions, type UserPermission } from './api'

/* Section titles for the top-level permissions; anything else uses the server's display name. */
const SECTION_TITLES: Record<string, string> = {
  'Portal.Bookings': 'Bookings',
  'Portal.Buildings': 'Buildings',
  'Portal.Floors': 'Floors',
  'Portal.Spaces': 'Spaces',
  'Portal.SpaceTypes': 'Space types',
  'Portal.Maintenance': 'Blocked time',
  'Portal.Users': 'Users',
}

const ESTATE_LABELS = (root: string): Record<string, string> => ({
  [root]: 'See', [`${root}.Create`]: 'Add', [`${root}.Edit`]: 'Edit', [`${root}.Delete`]: 'Delete',
})

/* Friendly, plain-words labels for the permissions the portal defines. */
const LABELS: Record<string, string> = {
  'Portal.Bookings': 'See bookings',
  'Portal.Bookings.Create': 'New booking',
  'Portal.Bookings.Edit': 'Reschedule / end early',
  'Portal.Bookings.Delete': 'Cancel',
  'Portal.Bookings.ManageAll': "Manage everyone's bookings",
  ...ESTATE_LABELS('Portal.Buildings'),
  ...ESTATE_LABELS('Portal.Floors'),
  ...ESTATE_LABELS('Portal.Spaces'),
  ...ESTATE_LABELS('Portal.SpaceTypes'),
  'Portal.Maintenance': 'See blocked time',
  'Portal.Maintenance.Create': 'Block time',
  'Portal.Maintenance.Edit': 'Edit blocked time',
  'Portal.Maintenance.Delete': 'Unblock',
  'Portal.Users': 'See user directory',
  'Portal.Users.Create': 'Add users',
  'Portal.Users.Edit': 'Edit users',
  'Portal.Users.ManagePermissions': 'Edit permissions',
}

const SOURCE_TAGS: Record<UserPermission['source'], string | null> = {
  role: 'from role',
  user: 'added for this user',
  blocked: 'turned off for this user',
  none: null,
}

interface Section { root: UserPermission; children: UserPermission[] }

/* One section per top-level permission, its children under it, in the server's order.
 * A permission whose parent isn't in the list becomes a section of its own. */
function toSections(items: UserPermission[]): Section[] {
  const names = new Set(items.map((p) => p.name))
  const sections: Section[] = []
  const byRoot = new Map<string, Section>()
  const rootOf = (p: UserPermission): string => {
    let cur = p
    while (cur.parentName && names.has(cur.parentName)) cur = items.find((x) => x.name === cur.parentName)!
    return cur.name
  }
  items.forEach((p) => {
    if (!p.parentName || !names.has(p.parentName)) {
      const s = { root: p, children: [] }
      sections.push(s)
      byRoot.set(p.name, s)
    }
  })
  items.forEach((p) => {
    if (p.parentName && names.has(p.parentName)) byRoot.get(rootOf(p))?.children.push(p)
  })
  return sections
}

/* readOnly: the signed-in user's own row, whose permissions can be looked at but not changed here. */
export function UserPermissionsModal({ userId, userName, readOnly, onClose }: {
  userId: string
  userName: string
  readOnly?: boolean
  onClose: () => void
}) {
  const q = useUserPermissions(userId)
  const save = useSaveUserPermissions()
  /* name -> isGranted as shown on screen; starts as the server's answer. */
  const [draft, setDraft] = useState<Record<string, boolean>>({})
  const loaded = useMemo(() => Object.fromEntries((q.data ?? []).map((p) => [p.name, p.isGranted])), [q.data])
  useEffect(() => { setDraft(loaded) }, [loaded])

  const items = q.data ?? []
  const sections = useMemo(() => toSections(items), [items])
  const byName = useMemo(() => new Map(items.map((p) => [p.name, p])), [items])
  const on = (name: string) => draft[name] ?? loaded[name] ?? false
  /* A child is only possible while every permission above it is on. */
  const parentOn = (p: UserPermission): boolean => {
    let parent = p.parentName ? byName.get(p.parentName) : undefined
    while (parent) {
      if (!on(parent.name)) return false
      parent = parent.parentName ? byName.get(parent.parentName) : undefined
    }
    return true
  }

  const descendants = (name: string): string[] =>
    items.filter((p) => p.parentName === name).flatMap((p) => [p.name, ...descendants(p.name)])

  /* Turning a parent off turns its children off too; turning it back on restores what they were. */
  const toggle = (p: UserPermission, value: boolean) => setDraft((prev) => {
    const next = { ...prev, [p.name]: value }
    descendants(p.name).forEach((n) => { next[n] = value ? loaded[n] ?? false : false })
    return next
  })

  const changes = items.filter((p) => on(p.name) !== p.isGranted).map((p) => ({ name: p.name, isGranted: on(p.name) }))

  const submit = () => {
    if (!changes.length) { onClose(); return }
    save.mutateAsync({ id: userId, permissions: changes }).then(() => {
      toast('ok', 'Permissions saved', `${userName}'s access has been updated.`)
      onClose()
    }, (e) => { const { code, message } = errorText(e); toast('err', 'Request rejected', message, code) })
  }

  const row = (p: UserPermission, child: boolean) => {
    const disabled = readOnly || (child && !parentOn(p))
    const changed = on(p.name) !== p.isGranted
    const tag = changed ? 'not saved yet' : SOURCE_TAGS[p.source]
    const id = `perm-${p.name}`
    return (
      <label key={p.name} htmlFor={id} className={`perm-row${child ? ' child' : ''}${disabled ? ' disabled' : ''}`}>
        <input id={id} type="checkbox" checked={on(p.name)} disabled={disabled}
          onChange={(e) => toggle(p, e.target.checked)} />
        <span className="perm-label">{LABELS[p.name] ?? p.displayName}</span>
        {tag && <span className={`perm-tag${changed ? ' changed' : p.source === 'blocked' ? ' blocked' : ''}`}>{tag}</span>}
      </label>
    )
  }

  return (
    <Modal width={520} title={`Permissions — ${userName}`}
      subtitle={readOnly ? 'Your own permissions. Another admin can change them.' : 'Turn a feature off to hide it from this user. Changes apply straight away.'}
      onClose={onClose} footer={readOnly ? <button type="button" className="btn" onClick={onClose}>Close</button> : (
        <>
          <button type="button" className="btn" onClick={onClose}>Discard</button>
          <button type="button" className="btn btn-primary" disabled={save.isPending || !q.data} onClick={submit}>Save permissions</button>
        </>
      )}>
      <div className="user-dir">
        {q.isError ? <LoadError what="the permissions" error={q.error} onRetry={() => q.refetch()} /> : !q.data ? <Loading /> : !sections.length ? (
          <p className="empty-note">No permissions to show.</p>
        ) : sections.map((s) => (
          <section key={s.root.name} className="perm-section">
            <h3 className="perm-title">{SECTION_TITLES[s.root.name] ?? s.root.displayName}</h3>
            {row(s.root, false)}
            {s.children.map((c) => row(c, true))}
          </section>
        ))}
      </div>
    </Modal>
  )
}

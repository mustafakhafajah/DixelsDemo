import { useState } from 'react'
import { errorText } from '../../api/client'
import { Dropdown } from '../../components/pickers'
import { Modal } from '../../components/Sheet'
import { toast } from '../../state/toastStore'
import { roleLabel, useSetUserRole, useUserRoles, type UserDirectoryItem } from './api'

/* Gives the user one role (their only one from then on). Per-user permission changes are dropped by the server. */
export function ChangeRoleModal({ user, onClose }: { user: UserDirectoryItem; onClose: () => void }) {
  const roles = useUserRoles()
  const setRole = useSetUserRole()
  const name = user.name || user.userName
  const [role, setPicked] = useState(user.role ?? '')
  const changed = !!role && role.toLowerCase() !== (user.role ?? '').toLowerCase()

  const save = () => {
    setRole.mutateAsync({ id: user.id, role }).then(() => {
      toast('ok', 'Role changed', `${name} is now ${roleLabel(role, roles.data)}.`)
      onClose()
    }, (e) => { const { code, message } = errorText(e); toast('err', 'Request rejected', message, code) })
  }

  return (
    <Modal width={420} title={`Change role — ${name}`} onClose={onClose} footer={
      <>
        <button type="button" className="btn" onClick={onClose}>Discard</button>
        <button type="button" className="btn btn-primary" disabled={!changed || setRole.isPending} onClick={save}>Save role</button>
      </>
    }>
      <div>
        <label className="lbl" htmlFor="cr-role">Role</label>
        <Dropdown id="cr-role" value={role} placeholder={roles.isLoading ? 'Loading…' : 'Select…'} onChange={setPicked}
          options={(roles.data ?? []).map((r) => ({ value: r.name, label: r.displayName }))} />
        <p style={{ fontSize: 11.5, color: 'var(--slate)', margin: '5px 0 0' }}>Their permissions go back to what the new role gives.</p>
      </div>
    </Modal>
  )
}

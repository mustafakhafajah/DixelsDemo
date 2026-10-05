import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { CameraIcon, TrashIcon } from 'lucide-react'
import { ApiError, errorText } from '../../api/client'
import { ErrorLine } from '../../components/bits'
import { shrinkToJpeg } from '../../lib/image'
import type { FieldError } from '../../lib/useFieldErrors'
import { toast } from '../../state/toastStore'
import { useRemoveMyPicture, useSetMyPicture, type MyProfile } from './api'
import { checkPictureFile, PICTURE_TYPES } from './validation'

/* The field the server ties picture refusals to (ProfilePictureAppService.PictureField). */
const SERVER_FIELD = 'picture'
const ERROR_ID = 'profile-photo-error'

/* Upload / Change and Remove for the profile picture, in the header under the name. A problem with the chosen file
 * shows under the buttons at once; the file is shrunk in the browser before it is sent, and a new picture replaces
 * the old one. */
export function ProfilePhoto({ me }: { me: MyProfile }) {
  const { t } = useTranslation()
  const set = useSetMyPicture()
  const remove = useRemoveMyPicture()
  const input = useRef<HTMLInputElement>(null)
  const [error, setError] = useState<FieldError>()
  const [preparing, setPreparing] = useState(false)
  const hasPicture = !!me.pictureVersion
  const busy = preparing || set.isPending || remove.isPending

  /* A refusal about the picture goes under it; anything else is a notice. */
  const fail = (e: unknown) => {
    const mine = e instanceof ApiError ? e.fieldErrors.find((f) => f.field === SERVER_FIELD) : undefined
    if (mine) setError({ code: mine.code, message: mine.message })
    else { const { code, message } = errorText(e); toast('err', t('common.requestRejected'), message, code) }
  }

  const choose = async (file: File | undefined) => {
    /* So choosing the same file again still counts as a choice. */
    if (input.current) input.current.value = ''
    if (!file) return
    const problem = checkPictureFile(file)
    setError(problem)
    if (problem) return

    setPreparing(true)
    let small: Blob
    try {
      small = await shrinkToJpeg(file)
    } catch {
      setError({ code: 'validation.picture_not_image', message: t('profile.photo.unreadable') })
      return
    } finally {
      setPreparing(false)
    }
    set.mutateAsync(small).then(() => toast('ok', hasPicture ? t('profile.photo.changed') : t('profile.photo.added')), fail)
  }

  const drop = () => {
    setError(undefined)
    remove.mutateAsync().then(() => toast('ok', t('profile.photo.removed')), fail)
  }

  return (
    <div className="profile-photo">
      <div className="profile-photo-actions">
        <input ref={input} type="file" accept={PICTURE_TYPES.join(',')} hidden aria-describedby={ERROR_ID}
          onChange={(e) => { void choose(e.target.files?.[0]) }} />
        <button type="button" className="btn btn-sm" disabled={busy} onClick={() => input.current?.click()}>
          <CameraIcon aria-hidden="true" />{busy && !remove.isPending ? t('profile.photo.saving') : hasPicture ? t('profile.photo.change') : t('profile.photo.upload')}
        </button>
        {hasPicture && (
          <button type="button" className="btn btn-sm btn-danger" disabled={busy} onClick={drop}>
            <TrashIcon aria-hidden="true" />{t('profile.photo.remove')}
          </button>
        )}
      </div>
      <ErrorLine id={ERROR_ID} error={error} />
    </div>
  )
}

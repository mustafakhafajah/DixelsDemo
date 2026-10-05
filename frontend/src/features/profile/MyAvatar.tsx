import { initials } from '../../components/bits'
import { useMyPicture, useMyProfile } from './api'
import './MyAvatar.css'

/* The signed-in person's avatar: their picture when they have one, otherwise their initials, in whatever circle
 * className draws (the sidebar's, the account menu's, the profile header's). Decorative: their name is beside it. */
export function MyAvatar({ name, className }: { name: string; className: string }) {
  const version = useMyProfile().data?.pictureVersion
  const picture = useMyPicture(version).data
  return (
    <span className={`${className} my-avatar`} aria-hidden="true">
      {picture ? <img src={picture} alt="" /> : initials(name)}
    </span>
  )
}

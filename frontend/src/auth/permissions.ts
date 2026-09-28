/* Permission names as the ABP backend defines them (PortalPermissions). */
const group = <T extends string>(root: string, children: T[]) =>
  Object.fromEntries([['Default', root], ...children.map((c) => [c, `${root}.${c}`])]) as { Default: string } & Record<T, string>

const crud = (root: string) => group(root, ['Create', 'Edit', 'Delete'])

export const P = {
  Buildings: crud('Portal.Buildings'),
  Floors: crud('Portal.Floors'),
  Spaces: crud('Portal.Spaces'),
  SpaceTypes: crud('Portal.SpaceTypes'),
  /* Create = block time, Delete = unblock it. */
  Maintenance: crud('Portal.Maintenance'),
  /* Default = see bookings, Create = book, Edit = reschedule / end early, Delete = cancel,
   * ManageAll = everyone's bookings rather than only your own. */
  Bookings: group('Portal.Bookings', ['Create', 'Edit', 'Delete', 'ManageAll']),
  Users: group('Portal.Users', ['Create', 'Edit', 'ManagePermissions']),
}

/* Every employee holds these; used until the server's real grants have loaded, so nothing flickers. */
export const DEFAULT_EMPLOYEE_POLICIES = new Set([
  P.Buildings.Default, P.Floors.Default, P.Spaces.Default, P.SpaceTypes.Default, P.Maintenance.Default,
  P.Bookings.Default, P.Bookings.Create, P.Bookings.Edit, P.Bookings.Delete,
])

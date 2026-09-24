export type DemoRole = 'booking_user' | 'administrator'

export interface DemoAccount {
  id: string
  name: string
  email: string
  password: string
  role: DemoRole
  initials: string
}

export const DEMO_ACCOUNTS: DemoAccount[] = [
  {
    id: 'u-001',
    name: 'J. Tran',
    email: 'j.tran@company.com',
    password: 'demo-password',
    role: 'booking_user',
    initials: 'JT',
  },
  {
    id: 'u-900',
    name: 'A. Okafor',
    email: 'a.okafor@company.com',
    password: 'demo-password',
    role: 'administrator',
    initials: 'AO',
  },
]

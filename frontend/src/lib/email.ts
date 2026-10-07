/* Only a shape check (something@something.something, no spaces); the server has the final word. */
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export const isEmail = (text: string) => EMAIL.test(text.trim())

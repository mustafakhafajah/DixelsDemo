/* A picture as a data: address an <img> can show. Nothing to release afterwards, unlike an object URL; fine for
 * small pictures such as a 512 px profile picture. */
export function blobToDataUrl(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(reader.result as string)
    reader.onerror = () => reject(reader.error ?? new Error('The picture could not be read.'))
    reader.readAsDataURL(blob)
  })
}

/* A picture made smaller in the browser before it is sent: at most maxSide pixels on its longer side, as a JPEG.
 * This keeps uploads small whatever the camera took, and drops the photo's metadata (location, device). Transparent
 * parts become white, since JPEG has no transparency. Rejects when the file can't be read as an image. */
export async function shrinkToJpeg(file: Blob, maxSide = 512, quality = 0.9): Promise<Blob> {
  /* imageOrientation: a phone photo is turned the way it was taken. */
  const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' })
  try {
    const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height))
    const canvas = document.createElement('canvas')
    canvas.width = Math.max(1, Math.round(bitmap.width * scale))
    canvas.height = Math.max(1, Math.round(bitmap.height * scale))
    const context = canvas.getContext('2d')
    if (!context) throw new Error('Canvas is not available.')
    context.fillStyle = '#ffffff'
    context.fillRect(0, 0, canvas.width, canvas.height)
    context.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
    return await new Promise<Blob>((resolve, reject) =>
      canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('The picture could not be encoded.'))), 'image/jpeg', quality))
  } finally {
    bitmap.close()
  }
}

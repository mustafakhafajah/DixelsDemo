import { useEffect, useRef, type RefObject } from 'react'
import fragmentSource from './universe.glsl?raw'

/* One triangle that covers the whole canvas; the fragment shader draws everything. */
const VERTEX_SOURCE = 'attribute vec2 a_pos; void main() { gl_Position = vec4(a_pos, 0.0, 1.0); }'

const rgb = (hex: string) => [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255) as [number, number, number]
/* The brand panel's colours (LoginPage.css --bp-*). */
const COLORS = { u_bg: rgb('#0d0d10'), u_busy: rgb('#b994bf'), u_ready: rgb('#7ee0a1'), u_struct: rgb('#6e3a7a') }

/* Sharper than this adds little to a moving scene and costs a lot of GPU on big high-density screens. */
const MAX_DPR = 1.5
/* The scene's outer orbit has radius 1.15; these leave a little air around it inside the anchor. */
const FIT_WIDTH = 2.33
const FIT_HEIGHT = 1.4
/* How fast the scene catches up with the pointer (per second); lower is floatier. */
const FOLLOW = 4
/* Start a few seconds in, so the first frame already has light trails in flight. */
const START_TIME = 8

function compile(gl: WebGLRenderingContext, type: number, source: string) {
  const shader = gl.createShader(type)
  if (!shader) return null
  gl.shaderSource(shader, source)
  gl.compileShader(shader)
  if (gl.getShaderParameter(shader, gl.COMPILE_STATUS)) return shader
  console.warn('Universe shader did not compile:', gl.getShaderInfoLog(shader))
  gl.deleteShader(shader)
  return null
}

function link(gl: WebGLRenderingContext) {
  const vertex = compile(gl, gl.VERTEX_SHADER, VERTEX_SOURCE)
  const fragment = compile(gl, gl.FRAGMENT_SHADER, fragmentSource)
  if (!vertex || !fragment) return null
  const program = gl.createProgram()
  if (!program) return null
  gl.attachShader(program, vertex)
  gl.attachShader(program, fragment)
  gl.linkProgram(program)
  gl.deleteShader(vertex)
  gl.deleteShader(fragment)
  if (gl.getProgramParameter(program, gl.LINK_STATUS)) return program
  console.warn('Universe shader did not link:', gl.getProgramInfoLog(program))
  gl.deleteProgram(program)
  return null
}

/* The live backdrop of the sign-in page's brand panel. It fills its parent; the system is centred on and sized to
 * `anchor` (the scene area between the headline and the principles). The pointer anywhere over the canvas turns
 * and tilts it, even through the text on top; away from it, the scene drifts back to a resting view. Without
 * WebGL the canvas hides and the panel's own dark background shows. With reduced motion the scene holds still
 * and only follows the pointer. */
export function UniverseCanvas({ anchor }: { anchor: RefObject<HTMLElement | null> }) {
  const canvasRef = useRef<HTMLCanvasElement>(null)

  useEffect(() => {
    const canvas = canvasRef.current
    const gl = canvas?.getContext('webgl', { alpha: false, antialias: false, powerPreference: 'low-power' })
    const program = gl ? link(gl) : null
    if (!canvas || !gl || !program) {
      if (canvas) canvas.hidden = true
      return
    }

    const buffer = gl.createBuffer()
    gl.bindBuffer(gl.ARRAY_BUFFER, buffer)
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 3, -1, -1, 3]), gl.STATIC_DRAW)
    gl.useProgram(program)
    const position = gl.getAttribLocation(program, 'a_pos')
    gl.enableVertexAttribArray(position)
    gl.vertexAttribPointer(position, 2, gl.FLOAT, false, 0, 0)
    for (const [name, value] of Object.entries(COLORS)) gl.uniform3fv(gl.getUniformLocation(program, name), value)
    const u = {
      res: gl.getUniformLocation(program, 'u_res'),
      time: gl.getUniformLocation(program, 'u_time'),
      mouse: gl.getUniformLocation(program, 'u_mouse'),
      center: gl.getUniformLocation(program, 'u_center'),
      scale: gl.getUniformLocation(program, 'u_scale'),
      px: gl.getUniformLocation(program, 'u_px'),
    }

    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)')
    /* Everything below is in device pixels with y up, as the shader sees it. */
    let dpr = 1
    let center = { x: 0, y: 0 }
    let rest = { x: 0, y: 0 }
    let scale = 1
    let pointer: { x: number; y: number } | null = null
    const mouse = { x: 0, y: 0 }
    let placed = false

    const measure = () => {
      dpr = Math.min(window.devicePixelRatio || 1, MAX_DPR)
      const box = canvas.getBoundingClientRect()
      canvas.width = Math.max(1, Math.round(box.width * dpr))
      canvas.height = Math.max(1, Math.round(box.height * dpr))
      const area = anchor.current?.getBoundingClientRect()
      if (area && area.width > 0 && area.height > 0) {
        center = { x: (area.left + area.width / 2 - box.left) * dpr, y: (box.bottom - area.top - area.height / 2) * dpr }
        scale = Math.min(area.width / FIT_WIDTH, area.height / FIT_HEIGHT) * dpr
      } else {
        center = { x: canvas.width / 2, y: canvas.height * 0.47 }
        scale = Math.min(canvas.width / 2.6, canvas.height / 2.9)
      }
      /* Resting view: a little right of and below the hub, as in the design. */
      rest = { x: center.x + scale * 0.53, y: center.y - scale * 0.145 }
      if (!placed) {
        Object.assign(mouse, rest)
        placed = true
      }
    }

    const onPointerMove = (e: PointerEvent) => {
      const box = canvas.getBoundingClientRect()
      const inside = e.clientX >= box.left && e.clientX <= box.right && e.clientY >= box.top && e.clientY <= box.bottom
      pointer = inside ? { x: (e.clientX - box.left) * dpr, y: (box.bottom - e.clientY) * dpr } : null
    }
    const onPointerGone = () => { pointer = null }

    let frame = 0
    let last = performance.now()
    let time = START_TIME
    const draw = (now: number) => {
      frame = requestAnimationFrame(draw)
      const dt = Math.min(0.05, (now - last) / 1000)
      last = now
      if (!reducedMotion.matches) time += dt
      const target = pointer ?? rest
      const ease = 1 - Math.exp(-dt * FOLLOW)
      mouse.x += (target.x - mouse.x) * ease
      mouse.y += (target.y - mouse.y) * ease
      gl.viewport(0, 0, canvas.width, canvas.height)
      gl.uniform2f(u.res, canvas.width, canvas.height)
      gl.uniform1f(u.time, time)
      gl.uniform2f(u.mouse, mouse.x, mouse.y)
      gl.uniform2f(u.center, center.x, center.y)
      gl.uniform1f(u.scale, scale)
      gl.uniform1f(u.px, dpr)
      gl.drawArrays(gl.TRIANGLES, 0, 3)
    }
    const start = () => {
      if (frame) return
      last = performance.now()
      frame = requestAnimationFrame(draw)
    }
    const stop = () => {
      cancelAnimationFrame(frame)
      frame = 0
    }

    measure()
    /* Only animate while on screen: narrow windows hide the brand panel entirely. */
    const visibility = new IntersectionObserver(([entry]) => (entry.isIntersecting ? start() : stop()))
    visibility.observe(canvas)
    const sizes = new ResizeObserver(measure)
    sizes.observe(canvas)
    if (anchor.current) sizes.observe(anchor.current)
    const onContextLost = (e: Event) => {
      e.preventDefault()
      stop()
      canvas.hidden = true
    }
    canvas.addEventListener('webglcontextlost', onContextLost)
    window.addEventListener('pointermove', onPointerMove, { passive: true })
    window.addEventListener('blur', onPointerGone)
    document.documentElement.addEventListener('pointerleave', onPointerGone)

    return () => {
      stop()
      visibility.disconnect()
      sizes.disconnect()
      canvas.removeEventListener('webglcontextlost', onContextLost)
      window.removeEventListener('pointermove', onPointerMove)
      window.removeEventListener('blur', onPointerGone)
      document.documentElement.removeEventListener('pointerleave', onPointerGone)
      gl.deleteBuffer(buffer)
      gl.deleteProgram(program)
    }
  }, [anchor])

  return <canvas ref={canvasRef} className="universe-canvas" aria-hidden="true" />
}

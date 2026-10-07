/* The sign-in page's "resource universe" (pen.dev frame "Sign in — Resource Universe"): the organization as a glowing
 * hub with four orbits of shared things around it, one shape per kind. Items flip between ready (green) and in
 * progress (lilac), light trails run from the hub to them, and the pointer turns and tilts the whole system.
 * u_mouse and every size are in device pixels; u_px is the device pixel ratio, so lines and stars keep their
 * on-screen size on high-density screens. */
precision highp float;

uniform vec2 u_res;
uniform float u_time;
uniform vec2 u_mouse;
uniform vec2 u_center;
uniform float u_scale;
uniform float u_px;
uniform vec3 u_bg;
uniform vec3 u_busy;
uniform vec3 u_ready;
uniform vec3 u_struct;

float hash(float n) {
  return fract(sin(n) * 43758.5453);
}

float hash2(vec2 p) {
  return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float segDist(vec2 p, vec2 a, vec2 b) {
  vec2 pa = p - a;
  vec2 ba = b - a;
  float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
  return length(pa - ba * h);
}

/* 0 dot (people), 1 diamond (assets), 2 square (projects), 3 ring (data). */
float shapeSD(vec2 q, int kind, float r) {
  if (kind == 0) {
    return length(q) - r;
  }
  if (kind == 1) {
    return (abs(q.x) + abs(q.y)) * 0.7071 - r * 0.78;
  }
  if (kind == 2) {
    vec2 d = abs(q) - vec2(r * 0.72);
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - r * 0.14;
  }
  return abs(length(q) - r * 0.78) - r * 0.26;
}

void main() {
  vec2 fc = gl_FragCoord.xy;
  vec2 m = u_mouse;
  vec2 mn = m / u_res - 0.5;
  float unit = 1.0 / u_scale;
  float lw = unit * u_px;
  vec2 p = (fc - u_center) * unit;
  vec2 pm = (m - u_center) * unit;
  float t = u_time;

  float ang = t * 0.02 + mn.x * 1.3;
  float tilt = clamp(0.4 + mn.y * 0.45, 0.2, 0.85);

  vec3 col = u_bg;

  vec2 nb = p * vec2(0.75, 1.5);
  col += u_struct * 0.5 * exp(-dot(nb, nb) * 0.9);
  vec2 nb2 = p - vec2(0.95, 0.75);
  col += u_busy * 0.05 * exp(-dot(nb2, nb2) * 1.8);

  for (int L = 0; L < 3; L++) {
    float fl = float(L);
    float sz = (20.0 + fl * 16.0) * u_px;
    vec2 sp = (fc + mn * (20.0 + fl * 44.0) * u_px) / sz;
    vec2 id = floor(sp);
    float h = hash2(id + fl * 31.0);
    if (h > 0.88) {
      vec2 o = 0.2 + 0.6 * vec2(hash2(id + 1.7), hash2(id + 4.3));
      float d = length(fract(sp) - o) * sz / u_px;
      float tw = 0.5 + 0.5 * sin(t * (0.7 + h * 2.0) + h * 40.0);
      col += mix(u_busy, vec3(1.0), 0.55) * smoothstep(1.3 + fl * 0.3, 0.0, d) * (0.2 + 0.5 * tw) * (0.4 + fl * 0.3);
    }
  }

  vec2 q = vec2(p.x, p.y / tilt);
  float lq = length(q);
  float kq = mix(1.0 / tilt, 1.0, (q.x * q.x) / (dot(q, q) + 1e-5));

  for (int r = 0; r < 4; r++) {
    float R = 0.34 + float(r) * 0.27;
    float line = smoothstep(lw * kq * 1.3, 0.0, abs(lq - R));
    float front = q.y < 0.0 ? 1.0 : 0.55;
    col += u_busy * line * 0.16 * front;
  }

  float wave = fract(t * 0.18);
  col += u_ready * smoothstep(lw * kq * 2.5, 0.0, abs(lq - wave * 1.35)) * 0.35 * (1.0 - wave);

  float dh = length(p);
  float pulse = 0.5 + 0.5 * sin(t * 1.6);
  col += u_busy * exp(-dh * dh * (140.0 - pulse * 40.0)) * 1.1;
  col += mix(u_busy, vec3(1.0), 0.6) * smoothstep(0.024, 0.016, dh);

  for (int r = 0; r < 4; r++) {
    float fr = float(r);
    int N = 5 + r * 3;
    float R = 0.34 + fr * 0.27;
    float spd = 0.11 / (1.0 + fr * 0.55);
    for (int i = 0; i < 14; i++) {
      if (i >= N) break;
      float fi = float(i);
      float seed = hash(fr * 31.7 + fi * 7.13 + 1.0);
      float a = fi / float(N) * 6.28318 + t * spd + ang + seed * 0.45;
      vec2 w = vec2(cos(a), sin(a)) * R;
      vec2 s = vec2(w.x, w.y * tilt);
      float depth = 1.0 - w.y / 1.15 * 0.32;
      float st = hash(seed * 91.0 + floor(t * 0.12 + seed * 7.0));
      vec3 c = st > 0.45 ? u_busy : u_ready;

      vec2 ds = s - pm;
      float near = exp(-dot(ds, ds) * 16.0);
      float size = (0.015 + 0.003 * fr) * depth * (1.0 + near * 0.8);

      float sd = shapeSD(p - s, r, size);
      float fill = smoothstep(lw * 1.2, 0.0, sd);
      float glow = exp(-max(sd, 0.0) / (lw * (5.0 + near * 9.0)));
      col += c * (fill * (0.8 + near * 0.5) + glow * (0.2 + near * 0.35));

      float bt = fract(t * 0.08 + seed * 3.7);
      if (bt < 0.16) {
        float k = bt / 0.16;
        float d = segDist(p, vec2(0.0), s);
        float along = clamp(dot(p, s) / dot(s, s), 0.0, 1.0);
        float head = exp(-pow((along - k) * 7.0, 2.0));
        col += c * smoothstep(lw * 1.6, 0.0, d) * (0.18 * (1.0 - k) + 0.9 * head);
      }

      if (near > 0.22) {
        col += u_ready * smoothstep(lw * 1.2, 0.0, segDist(p, pm, s)) * 0.45 * near;
      }
    }
  }

  float dmr = length(p - pm);
  col += u_ready * smoothstep(lw * 1.3, 0.0, abs(dmr - 0.075)) * 0.55;
  col += u_ready * 0.08 * exp(-dmr * dmr * 30.0);

  vec2 uv = fc / u_res;
  col *= 0.72 + 0.28 * smoothstep(0.0, 0.3, uv.y);
  col *= 0.85 + 0.15 * smoothstep(1.0, 0.75, uv.y);

  gl_FragColor = vec4(col, 1.0);
}

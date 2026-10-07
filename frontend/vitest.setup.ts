/* Tests run on a fixed clock so they read the same on every machine: Amman is UTC+3 all year, like a viewer in
 * Jordan. The app shows every time on the viewer's own clock, so the zone matters to the expected values. */
process.env.TZ = 'Asia/Amman'

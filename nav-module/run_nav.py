"""Usage: py run_nav.py [path/to/jez_ctx_20m_grid.json]  -> writes routes.json + nav_demo.html (open by double-click)."""
import sys, os, json, random, base64, io, math, nav
from PIL import Image
here = os.path.dirname(os.path.abspath(__file__))
cands = [sys.argv[1]] if len(sys.argv) > 1 else [os.path.join(here, "..", "data", "jez_ctx_20m_grid.json"), os.path.join(here, "jez_ctx_20m_grid.json")]
g = nav.hazards(nav.load_grid(next(p for p in cands if os.path.exists(p))))
R, C, M = g["rows"], g["cols"], nav.CONFIG["MAX_SLOPE_DEG"]
rnd = random.Random(7); found = []
for _ in range(40):                                    # search pairs ~3-6 km apart where the two modes differ most
    a = (rnd.randrange(R), rnd.randrange(C)); ang = rnd.uniform(0, 6.28); n = rnd.randint(40, 75)
    b = (round(a[0] + n * math.sin(ang)), round(a[1] + n * math.cos(ang)))
    if not (0 <= b[0] < R and 0 <= b[1] < C): continue
    s, f = nav.astar(g, a, b, "shortest"), nav.astar(g, a, b, "safest")
    if s and f:
        js, jf = nav.route_json(g, s, "shortest"), nav.route_json(g, f, "safest")
        found.append((js["maxSlopeDeg"] - jf["maxSlopeDeg"], dict(start=a, goal=b, shortest=js, safest=jf)))
found.sort(key=lambda t: -t[0]); presets = []
for _, p in found:
    if all(math.dist(p["start"], q["start"]) > 25 for q in presets): presets.append(p)
    if len(presets) == 3: break
out = dict(config=nav.CONFIG, source=g["meta"]["source"], doi=g["meta"]["doi"], cellMeters=g["cell"], gridCols=C, gridRows=R,
           frame="x=m east of west edge, y=m north of south edge, z=elevation m (Mars CTX DTM, no vertical exaggeration)",
           geotiffOriginEastingM=g["meta"]["geotiff_origin_easting_m"], geotiffOriginNorthingM=g["meta"]["geotiff_origin_northing_m"],
           presets=presets)
json.dump(out, open(os.path.join(here, "routes.json"), "w"), separators=(",", ":"))
img = Image.new("RGB", (C, R))                          # hazard map: green safe -> red risky, dark = impassable
for r in range(R):
    for c in range(C):
        k = g["risk"][r][c]; img.putpixel((c, r), (25, 25, 25) if g["slope"][r][c] > M else (int(255 * min(1, 2 * k)), int(200 * min(1, 2 * (1 - k))), 60))
buf = io.BytesIO(); img.save(buf, "PNG"); uri = "data:image/png;base64," + base64.b64encode(buf.getvalue()).decode()
slim = dict(cfg=nav.CONFIG, src=out["source"], doi=out["doi"], cell=g["cell"], presets=[
    {k: {kk: v[kk] for kk in ("distanceMeters", "etaMinutes", "maxSlopeDeg", "meanRisk", "path", "waypoints")} for k, v in p.items() if k in ("shortest", "safest")} for p in presets])
html = open(os.path.join(here, "demo_template.html")).read().replace("__IMG__", uri).replace("__DATA__", json.dumps(slim, separators=(",", ":")))
open(os.path.join(here, "nav_demo.html"), "w", encoding="utf-8").write(html)
for i, p in enumerate(presets, 1):
    print(f"preset {i}: shortest {p['shortest']['distanceMeters']} m / max {p['shortest']['maxSlopeDeg']} deg | safest {p['safest']['distanceMeters']} m / max {p['safest']['maxSlopeDeg']} deg")

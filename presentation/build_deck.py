import os, html
D = os.path.dirname(os.path.abspath(__file__))

def img(rel_path):
    return rel_path

slides = [
 dict(t="ORBITX: Mars Sentinel", sub="Toward 'Google Earth for Mars' — Web + 3D + Unity 6 + Unreal 5.8",
      b=["Team ORBITX · NASA Space Apps Challenge 2026 (orbitX00)",
         "Anchor: Jezero Crater, Mars (18.4447°N, 77.4508°E)",
         "Synchronized WebGL twin + Unity 6 game + Unreal 5.8 + Blender 5.2"], im=img("../showcase/blender_3d_night_starfield.png"), title=True),
 dict(t="The Problem & Verified NASA Stack", sub="Earth is 15–22 light-minutes away. No GPS. Dust, radiation, steep slopes.",
      b=["MRO HiRISE 4 m DTM (1,132,096 vertices, SHA-256 verified) + CTX 20 m + MOLA",
         "USGS SIM 3292: 44 geologic units with slope & trafficability hazards",
         "Perseverance (559 legs, 22 tubes), Ingenuity (72 flights), MEDA, RAD, DONKI"], im=img("../showcase/hero_mars_planet_and_sun_orbit.png"),
      note="Honest limit: orbital DEM resolution cannot resolve meter-scale boulders. Panels separate measured NASA data vs. simulation."),
 dict(t="CTX A* Routing, Solar & RF Viewshed", sub="Shared nav-module running live in WebGL, Unity 6 & UE 5.8",
      b=["8-connected A* over 399×378 CTX grid: Shortest 5,367 m (13.1°) vs. Safest 6,483 m (8.1°)",
         "Mars orbital solar & shadow model (Ls 0°–355°, 75-step raymarch) + RF LOS viewshed",
         "Automated Outpost Site Check (Slope ≤ 10°, Roughness ≤ 2 m, Daylight ≥ 50%)"], im=img("../showcase/nav_module_ctx_rf.png"),
      note="Slope thresholds and risk weights are configurable operational parameters."),
 dict(t="5-Chapter Story & 3D Hazard X-Ray", sub="Sol 1042: Echoes of Jezero — Bilingual EN/AR Live Comms",
      b=["Live 3D Hazard/Solar/RF terrain projection + Omni-Probe PIXEL/RIMFAX scanner",
         "Unity 6 URP 17.6 Render Graph [X] world-space subsurface radar wave",
         "Full bilingual Arabic RTL + English campaign synchronized across Web & Game"], im=img("../showcase/unity_game_xray_story.png")),
 dict(t="Unity 6 Playable Survival Game", sub="Live MarsSentinel.exe — 0.38g EVA, Rover & Space Weather",
      b=["Beer-Lambert dust optical depth τ (0.45 → 2.80) + live Suit O2, Power, Radiation & RF",
         "Authentic NASA Perseverance GLB with 6-point suspension, [R] A* Autopilot & Co-op EVA-02",
         "5-Chapter Story Director with DONKI Dust Storm Triage & Mars Ascent Rocket Ignition"], im=img("../showcase/unity_game_live.png"),
      note="Atmospheric and solar values are parameterized by NASA MEDA/RAD/DONKI datasets."),
 dict(t="Unreal Engine 5.8 Lumen & Nanite", sub="MarsSentinelUE.uproject — DX12 SM6 Next-Gen Module",
      b=["Lumen Global Illumination, Nanite Virtualized Geometry & Martian SkyAtmosphere",
         "Physically calibrated Mars gravity (-372.076 cm/s²) + C++ EVA & CTX A* subsystems",
         "Automated Blender 5.2 → UE 5.8 Interchange pipeline with 10 real NASA assets"], im=img("../showcase/unreal_ue5_jezero_lumen.png"),
      note="Open unreal_project/MarsSentinelUE.uproject directly in UE 5.8 to explore JezeroCrater_Hero."),
 dict(t="Verified, Open Source & Scalable", sub="github.com/youssef50505/ORBITX-NASA-SPACE-APP",
      b=["Zero-CDN WebGL twin + Unity 6 .exe + Unreal 5.8 + Blender 5.2 + nav-module",
         "121/121 workspace integrity checks · 236 automated tests passing (0 failures)",
         "Built-in WebM recorder (● Rec 28s) + Arabic RTL + [N] 28 s / [P] 60 s Unity directors"], im=img("../showcase/web_arabic_rtl.png"), last=True),
]

CSS = """
@page{size:1920px 1080px;margin:0}
*{box-sizing:border-box}
html,body{margin:0;padding:0;background:#05060d;font-family:'Segoe UI',Arial,sans-serif;color:#f3ece6}
.s{width:1920px;height:1080px;position:relative;overflow:hidden;page-break-after:always;break-after:page;
 background:radial-gradient(ellipse at 85% 10%,#3a1a12 0%,#120b14 45%,#05060d 100%);padding:70px 90px}
.s:last-child{page-break-after:auto;break-after:auto}
.s::before{content:'';position:absolute;inset:0;background-image:radial-gradient(#fff8 1px,transparent 1.5px),radial-gradient(#fff5 1px,transparent 1.5px);background-size:190px 170px,97px 131px;background-position:0 0,40px 60px;opacity:.35}
.in{position:relative;z-index:1;height:100%;display:flex;gap:60px}
.tx{flex:1;display:flex;flex-direction:column;justify-content:center}
h1{font-size:84px;margin:0 0 18px;color:#ff8a55;line-height:1.05}
.title h1{font-size:104px}
.sub{font-size:38px;color:#ffd7bf;margin-bottom:44px;line-height:1.25}
ul{list-style:none;padding:0;margin:0}
li{font-size:40px;line-height:1.3;margin-bottom:26px;padding-left:44px;position:relative}
li::before{content:'';position:absolute;left:0;top:18px;width:18px;height:18px;border-radius:50%;background:#e2552d}
.note{margin-top:20px;font-size:28px;color:#c9a99a;font-style:italic}
.im{flex:1.15;display:flex;align-items:center}
.im img{width:100%;border-radius:18px;border:2px solid #e2552d88;box-shadow:0 0 60px #e2552d55}
.num{position:absolute;right:50px;bottom:30px;font-size:26px;color:#8a7a78;z-index:2}
.brand{position:absolute;left:90px;bottom:30px;font-size:24px;color:#8a7a78;letter-spacing:4px;z-index:2}
.ai{font-size:26px;color:#c9a99a;margin-top:16px}
.link{font-size:44px;color:#ffb48a;font-weight:700}
"""

out = ["<!DOCTYPE html><html lang='en'><head><meta charset='utf-8'><title>ORBITX Mars Sentinel</title><style>", CSS, "</style></head><body>"]
for n, s in enumerate(slides, 1):
    e = html.escape
    lis = "".join(f"<li>{e(x)}</li>" for x in s["b"])
    note = f"<div class='note'>{e(s['note'])}</div>" if s.get("note") else ""
    extra = ""
    if s.get("last"):
        extra = ("<div class='ai'>AI use: AI coding assistants helped with code, docs and this deck; "
                 "data, metrics and tests were verified by the team. Data: NASA / USGS public datasets.</div>")
    sub = e(s["sub"])
    if s.get("last"):
        sub = f"<span class='link'>https://github.com/youssef50505/ORBITX-NASA-SPACE-APP</span>"
    imh = f"<div class='im'><img src='{s['im']}'></div>" if s["im"] else ""
    cls = "s title" if s.get("title") else "s"
    out.append(f"<section class='{cls}'><div class='in'><div class='tx'><h1>{e(s['t'])}</h1><div class='sub'>{sub}</div><ul>{lis}</ul>{note}{extra}</div>{imh}</div><div class='brand'>ORBITX · MARS SENTINEL</div><div class='num'>{n} / 7</div></section>")
out.append("</body></html>")
open(os.path.join(D, "ORBITX_Deck.html"), "w", encoding="utf-8").write("\n".join(out))
print("ok")

import os, html
D = os.path.dirname(os.path.abspath(__file__))
S = os.path.join(D, "shots")
FB = ["web_modern_simple_ui.png", "web_modern_omni_open.png"]

def img(name, i=0):
    f = name if os.path.exists(os.path.join(S, name)) else FB[i % 2]
    return f"shots/{f}"

slides = [
 dict(t="ORBITX: Mars Sentinel", sub="Interplanetary Survival Guide: Martian Map",
      b=["Team ORBITX · NASA Space Apps Challenge 2026",
         "Anchor: Jezero Crater, Mars (18.4447°N, 77.4508°E)",
         "3D digital twin: Unity 6 desktop + live Three.js web twin"], im=img("01_hero.png", 0), title=True),
 dict(t="The Problem & the Data Stack", sub="Earth is 15–22 light-minutes away. No GPS. Dust, radiation, steep slopes.",
      b=["MRO HiRISE DTM (1,132,096 vertices, SHA-256 verified) + MOLA",
         "USGS SIM 3292: 44 geologic units with trafficability",
         "Perseverance (559 legs, 22 tubes), Ingenuity (72 flights), Curiosity, MEDA, RAD, DONKI"], im=None,
      note="Honest limit: terrain resolution cannot resolve individual rocks. Panels tag measured data vs. simulation."),
 dict(t="A* Multi-Objective Traverse", sub="Three routes from Octavia E. Butler Landing",
      b=["Safest 88.4/100: 1,159.95 m, max slope 6.49°",
         "Shortest 64.1/100: 1,113.50 m, 7.08°  ·  Science 82.7/100: 1,203.21 m",
         "[H] toggles layers to cut visual clutter"], im=img("02_routes.png", 0),
      note="Slope limits and risk weights are demo assumptions."),
 dict(t="Omni-Probe X-Ray Scanner", sub="Inspect terrain beyond fixed map pins",
      b=["Click terrain, outcrops, meteorites (Cacao, Lebanon), Belva Crater",
         "Shows lat/lon, slope, thermal inertia, USGS unit",
         "PIXL chemistry + 3-layer RIMFAX radar profile (0–15 m)"], im=img("03_omni.png", 1)),
 dict(t="Space Weather & EVA Survival Radius", sub="Beer-Lambert: I = 586.2 · exp(−τ / sin(elevation))",
      b=["Dust optical depth τ from 0.45 (nominal) to 2.80 (global storm)",
         "Point-of-No-Return EVA radius contracts with visibility and suit O2",
         "Storm triage: GCR 0.67 mSv/day and DONKI SEP alerts"], im=img("04_orbit.png", 0),
      note="Atmospheric and solar values are a simulation parameterized by MEDA/RAD data."),
 dict(t="Closed-Loop Base, Rover & Drones", sub="Unified Command Deck (MARS-SENTINEL-v10)",
      b=["Hydroponics + aeroponics: 98.4% water recycling, 2.84 kg/Sol O2",
         "ROVER-01 manual drive or A* autopilot",
         "3-drone recon fleet DR-01..03"], im=img("05_rover.png", 1),
      note="Rover speeds and base figures are demo assumptions."),
 dict(t="Verified, Open Source, Ready", sub="github.com/youssef50505/ORBITX-NASA-SPACE-APP",
      b=["Unity 6 desktop build + zero-build WebGL twin + Blender 5.2 pipeline",
         "120/120 integrity checks · 208/208 automated tests passing",
         "Built-in demo directors: [N] 28 s, [P] 60 s"], im=img("01_hero.png", 0), last=True),
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

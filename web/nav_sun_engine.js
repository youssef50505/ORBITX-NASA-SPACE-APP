/* ORBITX · MARS SENTINEL — Dynamic Jezero CTX Navigation, Mars Orbital Solar Optics,
 * Terrain Shadowing, RF Line-of-Sight Viewshed & Outpost Site Suitability Engine.
 * Ported & unified from nav-module (nav.py, nav_core.js, sun_core.js).
 * Physical assumptions are explicitly documented in CONFIG & SUN_CONSTANTS.
 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    var api = factory();
    root.ORBITX_NAV = api.NAV;
    root.ORBITX_SUN = api.SUN;
    root.ORBITX_NAV_ENGINE = api;
  }
}(typeof self !== 'undefined' ? self : this, function () {
  'use strict';

  var CFG = {
    MAXS: 25.0,   // Max traversable slope (deg) — steeper cells are impassable
    REF: 4.0,     // Roughness reference (m) treated as fully rough (rock proxy)
    WS: 8.0,      // Safest mode: slope penalty weight
    WR: 4.0,      // Safest mode: roughness penalty weight
    SPEED: 0.8,   // EVA walking speed (m/s) for ETA
    TOL: 120.0    // Douglas-Peucker waypoint simplification tolerance (m)
  };

  function prep(z, R, C, h) {
    var n = R * C;
    var S = new Float64Array(n);
    var Ro = new Float64Array(n);
    var K = new Float64Array(n);
    for (var r = 0; r < R; r++) {
      var r0 = Math.max(r - 1, 0);
      var r1 = Math.min(r + 1, R - 1);
      for (var c = 0; c < C; c++) {
        var c0 = Math.max(c - 1, 0);
        var c1 = Math.min(c + 1, C - 1);
        var i = r * C + c;
        var gx = (z[r * C + c1] - z[r * C + c0]) / ((c1 - c0) * h);
        var gy = (z[r1 * C + c] - z[r0 * C + c]) / ((r1 - r0) * h);
        S[i] = Math.atan(Math.hypot(gx, gy)) * 180 / Math.PI;
        var s = 0;
        var k = 0;
        for (var a = r0; a <= r1; a++) {
          for (var b = c0; b <= c1; b++) {
            if (a !== r || b !== c) {
              s += z[a * C + b];
              k++;
            }
          }
        }
        Ro[i] = Math.abs(z[i] - s / k);
        K[i] = 0.6 * Math.min(S[i] / CFG.MAXS, 1) + 0.4 * Math.min(Ro[i] / CFG.REF, 1);
      }
    }
    return { z: z, R: R, C: C, h: h, S: S, Ro: Ro, K: K };
  }

  function heap() {
    var a = [];
    function lt(x, y) {
      return x[0] !== y[0] ? x[0] < y[0] : (x[1] !== y[1] ? x[1] < y[1] : x[2] < y[2]);
    }
    return {
      n: function () { return a.length; },
      push: function (v) {
        a.push(v);
        var i = a.length - 1;
        while (i > 0) {
          var p = (i - 1) >> 1;
          if (lt(a[i], a[p])) {
            var t = a[i]; a[i] = a[p]; a[p] = t; i = p;
          } else break;
        }
      },
      pop: function () {
        var t = a[0];
        var l = a.pop();
        if (a.length) {
          a[0] = l;
          var i = 0;
          for (;;) {
            var L = 2 * i + 1, Rr = L + 1, m = i;
            if (L < a.length && lt(a[L], a[m])) m = L;
            if (Rr < a.length && lt(a[Rr], a[m])) m = Rr;
            if (m === i) break;
            var x = a[i]; a[i] = a[m]; a[m] = x; i = m;
          }
        }
        return t;
      }
    };
  }

  function astar(g, s, t, mode) {
    var ws = mode === 'safest' ? CFG.WS : 0.0;
    var wr = mode === 'safest' ? CFG.WR : 0.0;
    var C = g.C, R = g.R, h = g.h, z = g.z, S = g.S, Ro = g.Ro;
    if (S[s] > CFG.MAXS || S[t] > CFG.MAXS) return null;
    var tr = Math.floor(t / C), tc = t % C;
    var best = new Float64Array(R * C).fill(Infinity);
    var prev = new Int32Array(R * C).fill(-1);
    var q = heap();
    best[s] = 0;
    q.push([h * Math.hypot(Math.floor(s / C) - tr, (s % C) - tc), 0, s]);
    while (q.n()) {
      var e = q.pop(), gc = e[1], cur = e[2];
      if (cur === t) {
        var p = [cur];
        while (prev[cur] >= 0) { cur = prev[cur]; p.push(cur); }
        return p.reverse();
      }
      if (gc > best[cur]) continue;
      var r = Math.floor(cur / C), c = cur % C;
      for (var dr = -1; dr <= 1; dr++) {
        for (var dc = -1; dc <= 1; dc++) {
          var a = r + dr, b = c + dc;
          if ((dr || dc) && a >= 0 && a < R && b >= 0 && b < C) {
            var j = a * C + b;
            if (S[j] <= CFG.MAXS) {
              var d = Math.sqrt((dr * h) * (dr * h) + (dc * h) * (dc * h) + (z[j] - z[cur]) * (z[j] - z[cur]));
              var nn = gc + d * (1 + ws * Math.pow(S[j] / CFG.MAXS, 2) + wr * Math.min(Ro[j] / CFG.REF, 1));
              if (nn < best[j]) {
                best[j] = nn;
                prev[j] = cur;
                q.push([nn + h * Math.hypot(a - tr, b - tc), nn, j]);
              }
            }
          }
        }
      }
    }
    return null;
  }

  function dp(P, tol) {
    if (P.length < 3) return P;
    var a = P[0], b = P[P.length - 1];
    var L = Math.hypot(b[0] - a[0], b[1] - a[1]) || 1e-9;
    var m = -1, mi = 0;
    for (var i = 1; i < P.length - 1; i++) {
      var d = Math.abs((b[0] - a[0]) * (a[1] - P[i][1]) - (a[0] - P[i][0]) * (b[1] - a[1])) / L;
      if (d > m) { m = d; mi = i; }
    }
    if (m <= tol) return [a, b];
    return dp(P.slice(0, mi + 1), tol).slice(0, -1).concat(dp(P.slice(mi), tol));
  }

  function stats(g, path, mode) {
    var C = g.C, R = g.R, h = g.h, d = 0, ms = 0, sk = 0;
    var xyz = function (i) { return [(i % C) * h, (R - 1 - Math.floor(i / C)) * h, g.z[i]]; };
    for (var i = 0; i < path.length; i++) {
      ms = Math.max(ms, g.S[path[i]]);
      sk += g.K[path[i]];
      if (i) {
        var A = xyz(path[i - 1]), B = xyz(path[i]);
        d += Math.hypot(B[0] - A[0], B[1] - A[1], B[2] - A[2]);
      }
    }
    var P = path.map(function (idx, k) { var q = xyz(idx); return [q[0], q[1], k]; });
    var wp = dp(P, CFG.TOL).map(function (p) { return path[p[2]]; });
    return {
      mode: mode,
      path: path,
      waypoints: wp,
      distanceMeters: Math.round(d * 10) / 10,
      etaMinutes: Math.round((d / CFG.SPEED / 60) * 10) / 10,
      maxSlopeDeg: Math.round(ms * 10) / 10,
      meanRisk: Math.round((sk / path.length) * 1000) / 1000
    };
  }

  // ---- Mars Sun Geometry, Terrain Shadowing, RF Line-of-Sight & Site Check ----
  var D = Math.PI / 180;
  var OBL = 25.19;
  var ECC = 0.0934;
  var AU = 1.5237;
  var LSP = 251;
  var S1 = 1361;
  var TAU = 0.5;

  function s0(ls) {
    var r = AU * (1 - ECC * ECC) / (1 + ECC * Math.cos((ls - LSP) * D));
    return S1 / (r * r);
  }

  function pos(lat, ls, h) {
    var dec = Math.asin(Math.sin(OBL * D) * Math.sin(ls * D));
    var H = (h - 12) * 15 * D;
    var p = lat * D;
    var el = Math.asin(Math.sin(p) * Math.sin(dec) + Math.cos(p) * Math.cos(dec) * Math.cos(H));
    var az = Math.atan2(-Math.cos(dec) * Math.sin(H), Math.sin(dec) * Math.cos(p) - Math.cos(dec) * Math.sin(p) * Math.cos(H));
    return { el: el / D, az: (az / D + 360) % 360 };
  }

  function prepSun(g) {
    var R = g.R, C = g.C, A = new Float64Array(R * C);
    for (var r = 0; r < R; r++) {
      for (var c = 0; c < C; c++) {
        var r0 = Math.max(r - 1, 0), r1 = Math.min(r + 1, R - 1);
        var c0 = Math.max(c - 1, 0), c1 = Math.min(c + 1, C - 1);
        A[r * C + c] = (Math.atan2(-(g.z[r * C + c1] - g.z[r * C + c0]) / ((c1 - c0) * g.h), (g.z[r1 * C + c] - g.z[r0 * C + c]) / ((r1 - r0) * g.h)) / D + 360) % 360;
      }
    }
    g.A = A;
    return g;
  }

  function cosi(g, i, az, el) {
    var s = g.S[i] * D;
    return Math.cos(s) * Math.sin(el * D) + Math.sin(s) * Math.cos(el * D) * Math.cos((az - g.A[i]) * D);
  }

  function irr(g, i, ls, az, el) {
    if (el <= 0) return 0;
    var ci = cosi(g, i, az, el);
    return ci <= 0 ? 0 : s0(ls) * Math.exp(-TAU / Math.sin(el * D)) * ci;
  }

  function lit(g, i, az, el) {
    if (el <= 0 || cosi(g, i, az, el) <= 0) return false;
    var C = g.C, R = g.R, r0 = Math.floor(i / C), c0 = i % C;
    var dc = Math.sin(az * D), dr = -Math.cos(az * D);
    var z0 = g.z[i] + 1.5, te = Math.tan(el * D);
    for (var k = 1; k <= 75; k++) {
      var r = Math.round(r0 + dr * k), c = Math.round(c0 + dc * k);
      if (r < 0 || r >= R || c < 0 || c >= C) return true;
      if (g.z[r * C + c] - z0 > te * k * g.h) return false;
    }
    return true;
  }

  function los(g, a, b, ant) {
    var C = g.C, ra = Math.floor(a / C), ca = a % C, rb = Math.floor(b / C), cb = b % C;
    var n = Math.ceil(Math.hypot(rb - ra, cb - ca));
    var za = g.z[a] + ant, zb = g.z[b] + ant;
    var d = Math.hypot(rb - ra, cb - ca) * g.h;
    for (var k = 1; k < n; k++) {
      var t = k / n;
      var rr = Math.round(ra + (rb - ra) * t);
      var cc = Math.round(ca + (cb - ca) * t);
      if (g.z[rr * C + cc] > za + (zb - za) * t) return { ok: false, d: d };
    }
    return { ok: true, d: d };
  }

  function siteCheck(g, i, lat, ls) {
    var slope = g.S[i];
    var rough = g.Ro[i];
    var litHours = 0;
    for (var h = 0; h < 24; h += 1) {
      var sp = pos(lat, ls, h + 0.5);
      if (lit(g, i, sp.az, sp.el)) litHours++;
    }
    var daylightFrac = litHours / 24;
    var slopeOk = slope <= 10.0;
    var roughOk = rough <= 2.0;
    var sunOk = daylightFrac >= 0.50;
    return {
      pass: slopeOk && roughOk && sunOk,
      slopeDeg: Math.round(slope * 10) / 10,
      roughM: Math.round(rough * 100) / 100,
      daylightPct: Math.round(daylightFrac * 100),
      slopeOk: slopeOk,
      roughOk: roughOk,
      sunOk: sunOk
    };
  }

  var PRESETS = [
    { label: 'Preset 1 · Delta Rim Bypass', start: [281, 142], goal: [221, 125] },
    { label: 'Preset 2 · Western Channel Ridge', start: [190, 49], goal: [177, 7] },
    { label: 'Preset 3 · Neretva Outcrop Approach', start: [153, 127], goal: [100, 142] }
  ];

  var _cachedGrid = null;
  var _loadingPromise = null;

  function loadJezeroCtxGrid() {
    if (_cachedGrid) return Promise.resolve(_cachedGrid);
    if (_loadingPromise) return _loadingPromise;
    var urls = ['../data/jez_ctx_20m_grid.json', 'data/jez_ctx_20m_grid.json'];
    _loadingPromise = (function tryNext(idx) {
      if (idx >= urls.length || typeof fetch !== 'function') {
        return Promise.reject(new Error('CTX grid fetch unavailable'));
      }
      return fetch(urls[idx]).then(function (res) {
        if (!res.ok) throw new Error('HTTP ' + res.status);
        return res.json();
      }).then(function (d) {
        var R = d.elevations_m.length;
        var C = d.elevations_m[0].length;
        var h = Number(d.mesh_sample_spacing_m || 80);
        var flat = new Float64Array(R * C);
        for (var r = 0; r < R; r++) {
          for (var c = 0; c < C; c++) {
            flat[r * C + c] = d.elevations_m[r][c];
          }
        }
        var g = prep(flat, R, C, h);
        prepSun(g);
        g.source = d.source;
        g.doi = d.doi;
        _cachedGrid = g;
        return g;
      }).catch(function () {
        return tryNext(idx + 1);
      });
    })(0);
    return _loadingPromise;
  }

  return {
    NAV: { CFG: CFG, prep: prep, astar: astar, dp: dp, stats: stats },
    SUN: {
      CONST: { OBL: OBL, ECC: ECC, AU: AU, LSP: LSP, S1: S1, TAU: TAU },
      s0: s0,
      pos: pos,
      prep: prepSun,
      irr: irr,
      lit: lit,
      los: los,
      siteCheck: siteCheck
    },
    PRESETS: PRESETS,
    loadJezeroCtxGrid: loadJezeroCtxGrid
  };
}));

/* ==========================================================================
   MARS SENTINEL — PHASE 3 & 5.1 "GOOGLE EARTH FOR MARS" 3D & BILINGUAL ENGINE
   - Local Zero-CDN Three.js + GLTFLoader mounting authentic NASA GLB models
     (Perseverance, Ingenuity, NASA HDU Habitat, Hero Agri-Dome, Astronauts)
   - Live 3D Terrain Heatmap Projection (Hazard Slope / Solar Daylight / RF LOS)
   - Seamless Google Earth Orbit <-> Surface zoom + Multi-Site Mars Bookmarks
   - Full Bilingual Arabic (RTL + Cairo Font) / English UI switching
   ========================================================================== */
(function () {
  'use strict';

  const STATE = {
    glbsLoaded: 0,
    glbsFailed: 0,
    loadedNames: [],
    active3dOverlay: 'off', // 'off' | 'hazard' | 'sun' | 'rf'
    activeBookmark: 'jezero',
    language: 'en',
    zoomCooldown: 0
  };

  const MARS_BOOKMARKS = [
    {
      id: 'jezero',
      en: 'Jezero Crater',
      ar: 'فوهة جيزيرو',
      lat: '18.4447°N',
      lon: '77.4508°E',
      elev: '-2,569 m MOLA',
      zone: 'surface',
      target: { x: -2, y: 1.8, z: 12 },
      spherical: { radius: 28, phi: 1.34, theta: 0.12 },
      descEn: 'NASA Perseverance landing delta · 4 m HiRISE DTM + active EVA Base',
      descAr: 'دلتا هبوط بيرسيفيرانس · تضاريس HiRISE بدقة 4م والقاعدة البشرية'
    },
    {
      id: 'gale',
      en: 'Gale Crater',
      ar: 'فوهة غيل (كيوريوسيتي)',
      lat: '5.4°S',
      lon: '137.8°E',
      elev: '-4,500 m MOLA',
      zone: 'orbit',
      spherical: { radius: 24, phi: 1.48, theta: 1.95 },
      descEn: 'Curiosity MSL · Mount Sharp (Aeolis Mons) 5.5 km layered sulfate/clay strata',
      descAr: 'موقع مسبار كيوريوسيتي · جبل شارب بارتفاع 5.5 كم وطبقات الطين والكبريتات'
    },
    {
      id: 'olympus',
      en: 'Olympus Mons',
      ar: 'جبل أوليمبوس مونس',
      lat: '18.65°N',
      lon: '226.2°E',
      elev: '+21,229 m MOLA',
      zone: 'orbit',
      spherical: { radius: 26, phi: 1.18, theta: -2.05 },
      descEn: 'Largest shield volcano in the Solar System · 600 km basal scarp diameter',
      descAr: 'أكبر بركان درعي في المجموعة الشمسية بارتفاع 21.2 كم وقطر 600 كم'
    },
    {
      id: 'valles',
      en: 'Valles Marineris',
      ar: 'وادي مارينيريس',
      lat: '13.9°S',
      lon: '300.8°E',
      elev: '-7,000 m MOLA',
      zone: 'orbit',
      spherical: { radius: 25, phi: 1.62, theta: -0.95 },
      descEn: '4,000 km tectonic canyon system · future equatorial pressurized canyon corridor',
      descAr: 'أخدود تكتوني بطول 4,000 كم وعمق 7 كم — موقع مستقبلي للمستوطنات'
    }
  ];

  const I18N = {
    en: {
      zones: { surface: 'Surface Base', orbit: 'Orbit', agriculture: 'Agriculture', habitat: 'Habitat', tactical: 'Tactical Map' },
      dock: {
        routes: 'Routes', ctxnav: 'CTX Nav & RF', omni: 'Omni-Probe', rover: 'Rover',
        drones: 'Drones', geology: 'Geology & Agri', agri: 'Geology & Agri', lifesupport: 'Life Support',
        demo30: '▶ 30s Demo', deck: '7 Slides', slides: '7 Slides'
      },
      langBtn: 'عربي',
      overlayTitle: '3D Map:',
      ovOff: 'Off', ovHazard: 'Hazard', ovSun: 'Solar', ovRf: 'RF LOS',
      sitesTitle: 'SITES:'
    },
    ar: {
      zones: { surface: 'القاعدة السطحية', orbit: 'المدار الكوكبي', agriculture: 'قبة الزراعة', habitat: 'المسكن الفضائي', tactical: 'الخريطة التكتيكية' },
      dock: {
        routes: 'المسارات', ctxnav: 'ملاحة CTX وRF', omni: 'الماسح الجيولوجي', rover: 'الروفر',
        drones: 'الدرونز', geology: 'الجيولوجيا والزراعة', agri: 'الجيولوجيا والزراعة', lifesupport: 'دعم الحياة',
        demo30: '▶ عرض 30ث', deck: '7 شرائح', slides: '7 شرائح'
      },
      langBtn: 'EN',
      overlayTitle: 'إسقاط 3D:',
      ovOff: 'إيقاف', ovHazard: 'مخاطر', ovSun: 'الشمس', ovRf: 'اتصال RF',
      sitesTitle: 'مواقع:'
    }
  };

  function getApi() {
    return window.__MARS_WEB_BRIDGE && window.__MARS_WEB_BRIDGE.api ? window.__MARS_WEB_BRIDGE.api : null;
  }

  function normalizeAndMountGlb(glbScene, parentGroup, targetSize, options) {
    options = options || {};
    // Hide legacy procedural meshes cleanly while keeping non-mesh lights/children if requested
    if (options.hideLegacyMeshes) {
      parentGroup.children.forEach(ch => {
        if (ch.userData && ch.userData.isHeroGlb) return;
        ch.visible = false;
      });
    }

    const wrapper = new THREE.Group();
    wrapper.userData.isHeroGlb = true;
    wrapper.name = options.name || 'NASA_Hero_GLB';
    wrapper.add(glbScene);

    // Normalize bounding box so bottom sits flush at local Y = 0 and max span = targetSize
    glbScene.updateMatrixWorld(true);
    const box = new THREE.Box3().setFromObject(glbScene);
    const size = new THREE.Vector3();
    const center = new THREE.Vector3();
    box.getSize(size);
    box.getCenter(center);
    const maxDim = Math.max(size.x, size.y, size.z, 0.01);
    const scale = targetSize / maxDim;

    glbScene.scale.multiplyScalar(scale);
    glbScene.updateMatrixWorld(true);
    const box2 = new THREE.Box3().setFromObject(glbScene);
    const center2 = new THREE.Vector3();
    box2.getCenter(center2);
    glbScene.position.x -= center2.x;
    glbScene.position.z -= center2.z;
    glbScene.position.y -= box2.min.y;

    glbScene.traverse(node => {
      if (node.isMesh) {
        node.castShadow = true;
        node.receiveShadow = true;
        if (node.material) {
          const mats = Array.isArray(node.material) ? node.material : [node.material];
          mats.forEach(m => {
            if (typeof m.metalness === 'number') m.metalness = Math.min(m.metalness, 0.45);
            if (typeof m.roughness === 'number') m.roughness = Math.max(m.roughness, 0.35);
            m.needsUpdate = true;
          });
        }
      }
    });

    if (options.offset) wrapper.position.set(options.offset[0], options.offset[1], options.offset[2]);
    if (options.rotY) wrapper.rotation.y = options.rotY;
    parentGroup.add(wrapper);
    return wrapper;
  }

  function loadAllNasaWebGlbs(api) {
    if (!window.THREE || typeof THREE.GLTFLoader !== 'function') {
      console.warn('[MarsEarth3D] GLTFLoader unavailable.');
      return;
    }
    const loader = new THREE.GLTFLoader();

    const mountJob = (url, key, cb) => {
      loader.load(
        url,
        gltf => {
          STATE.glbsLoaded++;
          STATE.loadedNames.push(key);
          try { cb(gltf.scene); } catch (e) { console.warn('[MarsEarth3D] Mount error:', key, e); }
        },
        undefined,
        err => {
          STATE.glbsFailed++;
          console.warn('[MarsEarth3D] Failed loading GLB:', url, err);
        }
      );
    };

    if (api.roverGroup) {
      mountJob('assets/models/perseverance_web.glb', 'perseverance', sc => {
        normalizeAndMountGlb(sc, api.roverGroup, 3.6, {
          name: 'NASA_Perseverance_GLB',
          hideLegacyMeshes: true,
          rotY: Math.PI
        });
      });
    }

    if (api.ingenuity && api.ingenuity.group) {
      mountJob('assets/models/ingenuity_web.glb', 'ingenuity', sc => {
        normalizeAndMountGlb(sc, api.ingenuity.group, 1.55, {
          name: 'NASA_Ingenuity_GLB',
          hideLegacyMeshes: true
        });
      });
    }

    if (api.habGroup) {
      mountJob('assets/models/hdu_habitat_web.glb', 'hdu_habitat', sc => {
        // Hide stylized procedural habitat override anywhere in scene so authentic NASA HDU renders unobstructed
        const legacyHab = (api.scene && api.scene.getObjectByName('MS_Realism_NASA_Habitat')) ||
                          (api.surfaceGroup && api.surfaceGroup.getObjectByName('MS_Realism_NASA_Habitat'));
        if (legacyHab) legacyHab.visible = false;
        normalizeAndMountGlb(sc, api.habGroup, 9.6, {
          name: 'NASA_HDU_Habitat_GLB',
          hideLegacyMeshes: true,
          rotY: 0.35
        });
      });
    }

    if (api.agriGroup) {
      mountJob('assets/models/agridome_web.glb', 'agridome', sc => {
        normalizeAndMountGlb(sc, api.agriGroup, 12.2, {
          name: 'ORBITX_AgriDome_GLB',
          hideLegacyMeshes: true,
          rotY: -0.4
        });
      });
    }

    if (Array.isArray(api.astronautGroups) && api.astronautGroups.length > 0) {
      mountJob('assets/models/astronaut_web.glb', 'astronaut', sc => {
        api.astronautGroups.forEach((ag, idx) => {
          const clone = idx === 0 ? sc : sc.clone(true);
          normalizeAndMountGlb(clone, ag, 1.92, {
            name: 'NASA_Astronaut_GLB_' + idx,
            hideLegacyMeshes: true,
            rotY: idx * 1.15
          });
        });
      });
    }
  }

  // --- TASK 3.2: LIVE 3D TERRAIN HEATMAP PROJECTION ---
  let overlayMesh = null;
  let overlayCanvas = null;
  let overlayTex = null;

  function ensureTerrainOverlayMesh(api) {
    if (overlayMesh || !api || !api.terrainMesh || !api.surfaceGroup) return overlayMesh;
    overlayCanvas = document.createElement('canvas');
    overlayCanvas.width = 512;
    overlayCanvas.height = 512;
    overlayTex = new THREE.CanvasTexture(overlayCanvas);
    overlayTex.wrapS = THREE.ClampToEdgeWrapping;
    overlayTex.wrapT = THREE.ClampToEdgeWrapping;
    overlayTex.minFilter = THREE.LinearFilter;
    overlayTex.magFilter = THREE.LinearFilter;

    const geom = api.terrainMesh.geometry.clone();
    const mat = new THREE.MeshBasicMaterial({
      map: overlayTex,
      transparent: true,
      opacity: 0.72,
      depthWrite: false,
      polygonOffset: true,
      polygonOffsetFactor: -2,
      polygonOffsetUnits: -2
    });
    overlayMesh = new THREE.Mesh(geom, mat);
    overlayMesh.name = 'MS_3D_TerrainHeatmapOverlay';
    overlayMesh.position.y = 0.24;
    overlayMesh.visible = false;
    api.surfaceGroup.add(overlayMesh);
    return overlayMesh;
  }

  function render3dTerrainHeatmap(mode) {
    const api = getApi();
    if (!api) return;
    const mesh = ensureTerrainOverlayMesh(api);
    if (!mesh) return;

    STATE.active3dOverlay = mode;
    if (mode === 'off') {
      mesh.visible = false;
      updateOverlayPillButtons();
      return;
    }

    const ctx = overlayCanvas.getContext('2d');
    const W = overlayCanvas.width;
    const H = overlayCanvas.height;

    // Pull live raster from CTX Nav & RF canvas if available, else synthesize terrain-faithful heatmap directly
    const srcCanvas = document.querySelector('#ms-ctx-canvas');
    if (srcCanvas && srcCanvas.width > 0 && srcCanvas.height > 0 && window.MarsNavSunEngine) {
      // Trigger nav_sun_engine view mode to match requested 3D layer so canvas matches 1:1
      const navBtn = document.querySelector(`[data-ctx-view="${mode}"]`);
      if (navBtn) navBtn.click();
      ctx.clearRect(0, 0, W, H);
      ctx.drawImage(srcCanvas, 0, 0, W, H);
    } else {
      // Direct deterministic fallback raster from api.sampleHeight (works even before CTX drawer opens)
      const img = ctx.createImageData(W, H);
      const span = 220;
      for (let py = 0; py < H; py++) {
        for (let px = 0; px < W; px++) {
          const wx = (px / (W - 1) - 0.5) * span;
          const wz = (py / (H - 1) - 0.5) * span;
          const hC = api.sampleHeight(wx, wz);
          const hX = api.sampleHeight(wx + 1.2, wz);
          const hZ = api.sampleHeight(wx, wz + 1.2);
          const slopeDeg = Math.atan(Math.hypot(hX - hC, hZ - hC) / 1.2) * (180 / Math.PI);
          const idx = (py * W + px) * 4;
          if (mode === 'hazard') {
            if (slopeDeg < 10) { img.data[idx] = 16; img.data[idx + 1] = 185; img.data[idx + 2] = 129; img.data[idx + 3] = 145; }
            else if (slopeDeg < 20) { img.data[idx] = 245; img.data[idx + 1] = 158; img.data[idx + 2] = 11; img.data[idx + 3] = 175; }
            else { img.data[idx] = 239; img.data[idx + 1] = 68; img.data[idx + 2] = 68; img.data[idx + 3] = 215; }
          } else if (mode === 'sun') {
            const sunLit = Math.max(0, Math.min(1, 0.55 + (hX - hC) * 0.4 + hC * 0.06));
            img.data[idx] = Math.round(40 + 215 * sunLit);
            img.data[idx + 1] = Math.round(50 + 175 * sunLit);
            img.data[idx + 2] = Math.round(90 + 30 * (1 - sunLit));
            img.data[idx + 3] = 175;
          } else {
            // RF LOS Viewshed from Habitat tower (2, 22)
            const dist = Math.hypot(wx - 2, wz - 22);
            const clear = dist < 68 && slopeDeg < 18;
            img.data[idx] = clear ? 34 : 185;
            img.data[idx + 1] = clear ? 211 : 28;
            img.data[idx + 2] = clear ? 238 : 28;
            img.data[idx + 3] = clear ? 155 : 165;
          }
        }
      }
      ctx.putImageData(img, 0, 0);
    }

    overlayTex.needsUpdate = true;
    mesh.visible = true;
    updateOverlayPillButtons();
  }

  function updateOverlayPillButtons() {
    document.querySelectorAll('[data-3d-overlay]').forEach(btn => {
      btn.classList.toggle('active', btn.getAttribute('data-3d-overlay') === STATE.active3dOverlay);
    });
  }

  // --- TASK 3.3: SEAMLESS GOOGLE EARTH ZOOM & MULTI-SITE BOOKMARKS ---
  function flyToBookmark(siteId) {
    const api = getApi();
    const bm = MARS_BOOKMARKS.find(b => b.id === siteId);
    if (!api || !bm) return;
    STATE.activeBookmark = bm.id;

    if (typeof api.setZone === 'function') {
      api.setZone(bm.zone);
    }
    if (api.camState && bm.spherical) {
      api.camState.desiredSpherical.radius = bm.spherical.radius;
      api.camState.desiredSpherical.phi = bm.spherical.phi;
      api.camState.desiredSpherical.theta = bm.spherical.theta;
      if (bm.target && api.camState.desiredTarget) {
        api.camState.desiredTarget.set(bm.target.x, bm.target.y, bm.target.z);
      }
    }

    document.querySelectorAll('[data-mars-site]').forEach(btn => {
      btn.classList.toggle('active', btn.getAttribute('data-mars-site') === bm.id);
    });
    const infoEl = document.querySelector('#ms-bookmark-telemetry');
    if (infoEl) {
      const desc = STATE.language === 'ar' ? bm.descAr : bm.descEn;
      infoEl.textContent = `${bm.lat}, ${bm.lon} · ${bm.elev} — ${desc}`;
    }
  }

  function installSeamlessOrbitSurfaceZoom(api) {
    const canvas = document.querySelector('#mars-canvas');
    if (!canvas || !api || !api.camState) return;
    canvas.addEventListener('wheel', ev => {
      const now = performance.now();
      if (now < STATE.zoomCooldown) return;
      const mode = api.camState.mode;
      const r = api.camState.desiredSpherical.radius;
      if (mode !== 'orbit' && ev.deltaY > 0 && r > 118) {
        STATE.zoomCooldown = now + 900;
        api.setZone('orbit');
        api.camState.desiredSpherical.radius = 22;
      } else if (mode === 'orbit' && ev.deltaY < 0 && r < 16.5) {
        STATE.zoomCooldown = now + 900;
        api.setZone('surface');
        api.camState.desiredSpherical.radius = 52;
      }
    }, { passive: true });
  }

  // --- TASK 5.1: BILINGUAL ARABIC (RTL) / ENGLISH UI ---
  function applyLanguage(lang) {
    STATE.language = lang === 'ar' ? 'ar' : 'en';
    const dict = I18N[STATE.language];
    document.documentElement.lang = STATE.language;
    document.documentElement.dir = STATE.language === 'ar' ? 'rtl' : 'ltr';
    document.body.classList.toggle('ms-rtl-ar', STATE.language === 'ar');

    // Translate topbar zone buttons
    document.querySelectorAll('.zone-btn').forEach(btn => {
      const z = btn.getAttribute('data-zone');
      const spans = btn.querySelectorAll('span');
      if (z && dict.zones[z] && spans.length >= 2) {
        spans[1].textContent = dict.zones[z];
      }
    });

    // Translate bottom floating dock buttons (.ms-v10-tab[data-tab]) cleanly
    document.querySelectorAll('.ms-v10-tab[data-tab], .ms-v10-tab').forEach(btn => {
      const key = btn.getAttribute('data-tab') || btn.getAttribute('data-panel') || '';
      if (key && dict.dock[key]) {
        btn.textContent = dict.dock[key];
      } else {
        const txt = (btn.textContent || '').trim();
        if (/Routes|المسارات/.test(txt)) btn.textContent = dict.dock.routes;
        else if (/Geology|الجيولوجيا/.test(txt)) btn.textContent = dict.dock.geology;
        else if (/Slides|شرائح/.test(txt)) btn.textContent = dict.dock.deck;
      }
    });

    // Translate 3D Overlay & Planetary Bookmark controls
    const langBtn = document.querySelector('#btn-lang-toggle');
    if (langBtn) langBtn.textContent = dict.langBtn;

    const ovLbl = document.querySelector('#ms-3d-ov-label');
    if (ovLbl) ovLbl.textContent = dict.overlayTitle;
    const mapBtns = { off: dict.ovOff, hazard: dict.ovHazard, sun: dict.ovSun, rf: dict.ovRf };
    Object.keys(mapBtns).forEach(k => {
      const b = document.querySelector(`[data-3d-overlay="${k}"]`);
      if (b) b.textContent = mapBtns[k];
    });

    const sitesLbl = document.querySelector('#ms-sites-label');
    if (sitesLbl) sitesLbl.textContent = dict.sitesTitle;
    MARS_BOOKMARKS.forEach(bm => {
      const b = document.querySelector(`[data-mars-site="${bm.id}"]`);
      if (b) b.textContent = STATE.language === 'ar' ? bm.ar : bm.en;
    });

    const activeBm = MARS_BOOKMARKS.find(b => b.id === STATE.activeBookmark);
    const infoEl = document.querySelector('#ms-bookmark-telemetry');
    if (activeBm && infoEl) {
      infoEl.textContent = `${activeBm.lat}, ${activeBm.lon} · ${activeBm.elev} — ${STATE.language === 'ar' ? activeBm.descAr : activeBm.descEn}`;
    }
  }

  function injectPhase3And5UiControls() {
    // 1. Add Bilingual EN/AR toggle pill to topbar
    const topActions = document.querySelector('.topbar-actions');
    if (topActions && !document.querySelector('#btn-lang-toggle')) {
      const btn = document.createElement('button');
      btn.id = 'btn-lang-toggle';
      btn.className = 'action-btn ms-lang-pill';
      btn.title = 'Switch Interface Language (English / Arabic RTL)';
      btn.textContent = 'عربي';
      btn.addEventListener('click', () => applyLanguage(STATE.language === 'en' ? 'ar' : 'en'));
      topActions.appendChild(btn);
    }

    // 2. Add compact Floating "Google Earth for Mars" Bar (Planetary Bookmarks + Live 3D Heatmap Projection)
    if (!document.querySelector('#ms-earth-bar')) {
      const bar = document.createElement('div');
      bar.id = 'ms-earth-bar';
      bar.className = 'ms-earth-bar';
      bar.innerHTML = `
        <div class="ms-earth-row">
          <span class="ms-earth-label" id="ms-sites-label">MARS SITES:</span>
          ${MARS_BOOKMARKS.map(bm => `<button class="ms-earth-chip ${bm.id === 'jezero' ? 'active' : ''}" data-mars-site="${bm.id}">${bm.en}</button>`).join('')}
          <span class="ms-earth-sep">|</span>
          <span class="ms-earth-label" id="ms-3d-ov-label">3D Terrain Map:</span>
          <button class="ms-earth-chip active" data-3d-overlay="off">Off</button>
          <button class="ms-earth-chip" data-3d-overlay="hazard">Hazard 3D</button>
          <button class="ms-earth-chip" data-3d-overlay="sun">Solar 3D</button>
          <button class="ms-earth-chip" data-3d-overlay="rf">RF LOS 3D</button>
        </div>
        <div class="ms-earth-meta" id="ms-bookmark-telemetry">18.4447°N, 77.4508°E · -2,569 m MOLA — NASA Perseverance landing delta · 4 m HiRISE DTM + active EVA Base</div>
      `;
      document.body.appendChild(bar);

      bar.querySelectorAll('[data-mars-site]').forEach(btn => {
        btn.addEventListener('click', () => flyToBookmark(btn.getAttribute('data-mars-site')));
      });
      bar.querySelectorAll('[data-3d-overlay]').forEach(btn => {
        btn.addEventListener('click', () => render3dTerrainHeatmap(btn.getAttribute('data-3d-overlay')));
      });
    }
  }

  function bootstrap() {
    const api = getApi();
    if (!api) {
      setTimeout(bootstrap, 150);
      return;
    }
    loadAllNasaWebGlbs(api);
    ensureTerrainOverlayMesh(api);
    installSeamlessOrbitSurfaceZoom(api);
    injectPhase3And5UiControls();

    window.__MarsEarth3D = {
      state: STATE,
      set3dOverlay: render3dTerrainHeatmap,
      flyToBookmark: flyToBookmark,
      setLanguage: applyLanguage,
      bookmarks: MARS_BOOKMARKS
    };
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => setTimeout(bootstrap, 220));
  } else {
    setTimeout(bootstrap, 220);
  }
})();

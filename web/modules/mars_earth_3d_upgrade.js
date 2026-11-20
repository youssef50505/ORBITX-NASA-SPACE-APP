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
            // Prevent chalk-white overexposure on untextured white GLB materials under Martian sun
            if (m.color && !m.map) {
              const lum = m.color.r * 0.3 + m.color.g * 0.59 + m.color.b * 0.11;
              if (lum > 0.82) {
                if (options.tintMode === 'rover') {
                  m.color.setHex(0xdcd0be); // Warm NASA titanium-cream thermal paint
                } else if (options.tintMode === 'astronaut') {
                  m.color.setHex(0xe5ddd0); // Crisp NASA xEMU suit cream-white
                } else {
                  m.color.setHex(0xd8cbb8); // Warm aerospace composite
                }
              }
            }
            if (typeof m.metalness === 'number') m.metalness = Math.min(m.metalness, 0.48);
            if (typeof m.roughness === 'number') m.roughness = Math.max(m.roughness, 0.38);
            m.needsUpdate = true;
          });
        }
      }
    });

    // For Perseverance & Astronaut single-material GLBs, apply vertex-color shading (dark titanium wheels, gold visor/foil, warm shadows)
    if (options.tintMode === 'rover' || options.tintMode === 'astronaut') {
      applyVertexColorRealismToGlb(glbScene, targetSize, options.tintMode);
    }

    if (options.offset) wrapper.position.set(options.offset[0], options.offset[1], options.offset[2]);
    if (options.rotY) wrapper.rotation.y = options.rotY;
    parentGroup.add(wrapper);
    return wrapper;
  }

  function applyVertexColorRealismToGlb(glbScene, targetSize, mode) {
    const worldBox = new THREE.Box3().setFromObject(glbScene);
    const minY = worldBox.min.y;
    const spanY = Math.max(0.1, worldBox.max.y - minY);
    const vWorld = new THREE.Vector3();

    glbScene.traverse(node => {
      if (!node.isMesh || !node.geometry || !node.geometry.attributes.position) return;
      const pos = node.geometry.attributes.position;
      const norm = node.geometry.attributes.normal;
      const colors = new Float32Array(pos.count * 3);

      for (let i = 0; i < pos.count; i++) {
        vWorld.fromBufferAttribute(pos, i).applyMatrix4(node.matrixWorld);
        const hNorm = Math.max(0, Math.min(1, (vWorld.y - minY) / spanY));
        const ny = norm ? norm.getY(i) : 0.5;
        const ao = 0.76 + 0.24 * Math.max(0, ny * 0.5 + 0.5);

        if (mode === 'rover') {
          if (hNorm < 0.27) {
            // Anodized dark titanium-aluminum cleated wheels & lower rocker-bogie hubs
            colors[i * 3] = 0.20 * ao;
            colors[i * 3 + 1] = 0.19 * ao;
            colors[i * 3 + 2] = 0.21 * ao;
          } else if (hNorm < 0.44 && Math.abs(vWorld.x) > 0.55) {
            // Rocker-bogie titanium suspension struts
            colors[i * 3] = 0.34 * ao;
            colors[i * 3 + 1] = 0.33 * ao;
            colors[i * 3 + 2] = 0.36 * ao;
          } else if (hNorm > 0.78) {
            // Mastcam-Z camera head & gold Kapton neck accents
            colors[i * 3] = 0.88 * ao;
            colors[i * 3 + 1] = 0.68 * ao;
            colors[i * 3 + 2] = 0.36 * ao;
          } else {
            // Warm NASA Perseverance deck & MMRTG housing with subtle Martian dust gradient
            colors[i * 3] = 0.86 * ao;
            colors[i * 3 + 1] = 0.79 * ao;
            colors[i * 3 + 2] = 0.69 * ao;
          }
        } else if (mode === 'astronaut') {
          if (hNorm > 0.82) {
            // Gold-tinted helmet & upper visor region
            colors[i * 3] = 0.90 * ao;
            colors[i * 3 + 1] = 0.72 * ao;
            colors[i * 3 + 2] = 0.36 * ao;
          } else if (hNorm < 0.16) {
            // Dusty Martian regolith-coated EVA boots
            colors[i * 3] = 0.42 * ao;
            colors[i * 3 + 1] = 0.25 * ao;
            colors[i * 3 + 2] = 0.18 * ao;
          } else if (hNorm > 0.52 && hNorm < 0.64) {
            // NASA rust-orange chest/PLSS harness stripe
            colors[i * 3] = 0.84 * ao;
            colors[i * 3 + 1] = 0.44 * ao;
            colors[i * 3 + 2] = 0.24 * ao;
          } else {
            // Crisp xEMU spacesuit fabric
            colors[i * 3] = 0.88 * ao;
            colors[i * 3 + 1] = 0.84 * ao;
            colors[i * 3 + 2] = 0.78 * ao;
          }
        }
      }
      node.geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));
      const mats = Array.isArray(node.material) ? node.material : [node.material];
      mats.forEach(m => {
        m.vertexColors = true;
        if (m.color) m.color.setHex(0xffffff);
        m.roughness = mode === 'rover' ? 0.44 : 0.52;
        m.metalness = mode === 'rover' ? 0.32 : 0.18;
        m.needsUpdate = true;
      });
    });
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
          tintMode: 'rover',
          rotY: Math.PI
        });
      });
    }

    if (api.ingenuity && api.ingenuity.group) {
      mountJob('assets/models/ingenuity_web.glb', 'ingenuity', sc => {
        normalizeAndMountGlb(sc, api.ingenuity.group, 1.55, {
          name: 'NASA_Ingenuity_GLB',
          hideLegacyMeshes: true,
          tintMode: 'rover'
        });
      });
    }

    if (api.habGroup) {
      mountJob('assets/models/hdu_habitat_web.glb', 'hdu_habitat', sc => {
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
        // Also hide any extra procedural astronauts added by mars_realism_upgrade
        if (api.scene) {
          api.scene.traverse(o => {
            if (o.name === 'MS_Realism_Astronaut') o.visible = false;
          });
        }
        api.astronautGroups.forEach((ag, idx) => {
          const clone = idx === 0 ? sc : sc.clone(true);
          normalizeAndMountGlb(clone, ag, 1.92, {
            name: 'NASA_Astronaut_GLB_' + idx,
            hideLegacyMeshes: true,
            tintMode: 'astronaut',
            rotY: idx * 1.15
          });
        });
      });
    }

    // Mount PBR Hero Medical Bay, EDL Lander & Launch Pad GLBs, replacing primitive white boxes in surfaceGroup!
    if (api.surfaceGroup) {
      const hFn = typeof api.sampleHeight === 'function' ? api.sampleHeight : (() => 0);
      const hidePrimitiveFacilityAt = (tx, tz, rad) => {
        api.surfaceGroup.children.forEach(ch => {
          if (ch.isGroup && !ch.userData.isHeroGlb && ch !== api.roverGroup && ch !== api.habGroup && ch !== api.agriGroup) {
            const dx = ch.position.x - tx;
            const dz = ch.position.z - tz;
            if (dx * dx + dz * dz < rad * rad) ch.visible = false;
          }
        });
      };

      mountJob('assets/models/medbay_web.glb', 'medbay', sc => {
        hidePrimitiveFacilityAt(-9.5, 23.0, 4.5);
        const g = new THREE.Group();
        g.position.set(-9.5, hFn(-9.5, 23.0), 23.0);
        api.surfaceGroup.add(g);
        normalizeAndMountGlb(sc, g, 8.6, { name: 'ORBITX_MedBay_GLB', rotY: -0.15 });
      });

      mountJob('assets/models/edl_lander_web.glb', 'edl_lander', sc => {
        hidePrimitiveFacilityAt(24.0, -11.0, 4.5);
        const g = new THREE.Group();
        g.position.set(24.0, hFn(24.0, -11.0), -11.0);
        api.surfaceGroup.add(g);
        normalizeAndMountGlb(sc, g, 8.2, { name: 'ORBITX_EDL_Lander_GLB', rotY: -0.65 });
      });

      mountJob('assets/models/launchpad_web.glb', 'launchpad', sc => {
        hidePrimitiveFacilityAt(26.5, 13.5, 6.5);
        const g = new THREE.Group();
        g.position.set(42.0, hFn(42.0, 24.0), 24.0);
        api.surfaceGroup.add(g);
        normalizeAndMountGlb(sc, g, 13.5, { name: 'ORBITX_LaunchPad_GLB', rotY: 0.0 });
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

  // --- STUDIO-GRADE MARTIAN 3D COLOR, LIGHTING, ROCK & TERRAIN OVERHAUL ---
  function buildOrganicSandNormalMap() {
    const size = 512;
    const c = document.createElement('canvas');
    c.width = size; c.height = size;
    const ctx = c.getContext('2d');
    const img = ctx.createImageData(size, size);
    const d = img.data;
    const h = new Float32Array(size * size);
    const tau = Math.PI * 2;
    for (let y = 0; y < size; y++) {
      const v = (y / size) * tau;
      for (let x = 0; x < size; x++) {
        const u = (x / size) * tau;
        // Organic curved wind-carved Martian ripples (warped domain — zero straight corduroy lines!)
        const warp = Math.sin(v * 2.0 + Math.cos(u * 2.0) * 1.2) * 1.35 + Math.cos(u * 3.0 - v * 2.0) * 0.65;
        const r1 = Math.sin(u * 5.0 + v * 2.0 + warp) * 0.45;
        const r2 = Math.sin(u * 11.0 - v * 5.0 + warp * 1.4) * 0.22;
        const grit = Math.sin(u * 29.0 + v * 23.0) * Math.cos(u * 37.0 - v * 31.0) * 0.14;
        h[y * size + x] = r1 + r2 + grit;
      }
    }
    const scale = 2.1;
    for (let y = 0; y < size; y++) {
      const yp = (y - 1 + size) % size, yn = (y + 1) % size;
      for (let x = 0; x < size; x++) {
        const xp = (x - 1 + size) % size, xn = (x + 1) % size;
        const dx = (h[y * size + xn] - h[y * size + xp]) * scale;
        const dy = (h[yn * size + x] - h[yp * size + x]) * scale;
        const dz = 1.0;
        const inv = 1.0 / Math.sqrt(dx * dx + dy * dy + dz * dz);
        const i = (y * size + x) * 4;
        d[i] = Math.floor((-dx * inv * 0.5 + 0.5) * 255);
        d[i + 1] = Math.floor((-dy * inv * 0.5 + 0.5) * 255);
        d[i + 2] = Math.floor((dz * inv * 0.5 + 0.5) * 255);
        d[i + 3] = 255;
      }
    }
    ctx.putImageData(img, 0, 0);
    const tex = new THREE.CanvasTexture(c);
    tex.wrapS = THREE.RepeatWrapping;
    tex.wrapT = THREE.RepeatWrapping;
    tex.repeat.set(22, 22);
    return tex;
  }

  function applyStudioGradeMars3dColorsAndLighting(api) {
    if (!api || !api.scene) return;

    // 1. Rich Martian Terracotta-Ochre & Dark Basalt Terrain Color Grading + Organic Wind-Ripple Normal Map
    if (api.terrainMesh && api.terrainMesh.geometry) {
      const geo = api.terrainMesh.geometry;
      const pos = geo.attributes.position;
      const norm = geo.attributes.normal;
      const col = geo.attributes.color;
      if (pos && col) {
        for (let i = 0; i < pos.count; i++) {
          const vx = pos.getX(i), vy = pos.getY(i), vz = pos.getZ(i);
          const ny = norm ? Math.max(0, Math.min(1, norm.getY(i))) : 1.0;
          const slope = Math.max(0, Math.min(1, (1.0 - ny) / 0.16));
          const n1 = Math.sin(vx * 0.08 + Math.cos(vz * 0.07) * 2.1) * Math.cos(vz * 0.09 - vx * 0.05);
          const n2 = Math.sin(vx * 0.28 - vz * 0.22) * 0.5 + Math.cos(vx * 0.65 + vz * 0.55) * 0.25;
          const v = 0.88 + 0.18 * n1 + 0.09 * n2;

          // Deep authentic NASA Curated Martian Iron-Oxide Terracotta & Dark Basalt Scree
          const sandR = 0.56 * v;
          const sandG = 0.23 * v;
          const sandB = 0.11 * v;

          const basaltR = 0.20 * v;
          const basaltG = 0.10 * v;
          const basaltB = 0.07 * v;

          const dust = Math.max(-0.04, Math.min(0.06, vy * 0.018));
          col.setXYZ(
            i,
            THREE.MathUtils.clamp(sandR * (1 - slope) + basaltR * slope + dust, 0.08, 0.78),
            THREE.MathUtils.clamp(sandG * (1 - slope) + basaltG * slope + dust * 0.45, 0.04, 0.42),
            THREE.MathUtils.clamp(sandB * (1 - slope) + basaltB * slope + dust * 0.25, 0.02, 0.24)
          );
        }
        col.needsUpdate = true;
      }
      if (api.terrainMesh.material) {
        api.terrainMesh.material.normalMap = buildOrganicSandNormalMap();
        api.terrainMesh.material.normalScale = new THREE.Vector2(0.55, 0.55);
        api.terrainMesh.material.roughness = 0.91;
        api.terrainMesh.material.metalness = 0.03;
        api.terrainMesh.material.needsUpdate = true;
      }
    }

    // 2. Golden-Hour Martian Lighting & Rich Dusty Sienna Atmosphere (prevents chalk-white washout!)
    const siennaBg = new THREE.Color(0x9e4b28);
    if (api.scene.background && api.scene.background.isColor) {
      api.scene.background.copy(siennaBg);
    }
    if (api.scene.fog) {
      api.scene.fog.color.copy(siennaBg);
      if (typeof api.scene.fog.density === 'number') api.scene.fog.density = 0.0026;
    }
    if (api.sunLight) {
      api.sunLight.color.setHex(0xffcca0);
      api.sunLight.intensity = 1.48;
      api.sunLight.position.set(-52, 34, -36);
    }
    if (api.hemiLight) {
      api.hemiLight.color.setHex(0xeb925e);
      api.hemiLight.groundColor.setHex(0x3d180c);
      api.hemiLight.intensity = 0.56;
    }

    // 3. Convert Smooth Tan Egg Blobs (DodecahedronGeometry detail=1) into Sharp Angular Faceted Dark Martian Basalt Boulders!
    api.scene.traverse(obj => {
      if (!obj.isMesh || !obj.geometry) return;
      if (obj.geometry.type === 'DodecahedronGeometry') {
        if (!obj.geometry.userData.facetedBasalt) {
          const nonIdx = obj.geometry.toNonIndexed();
          const p = nonIdx.attributes.position;
          for (let i = 0; i < p.count; i++) {
            const x = p.getX(i), y = p.getY(i), z = p.getZ(i);
            // Deterministic craggy rock fracture perturbation
            const crag = 0.84 + 0.28 * Math.sin(x * 9.3 + z * 7.1) * Math.cos(y * 11.7 - x * 5.3);
            p.setXYZ(i, x * crag, y * crag * 0.78, z * crag);
          }
          nonIdx.computeVertexNormals();
          nonIdx.userData.facetedBasalt = true;
          obj.geometry = nonIdx;
        }
        if (obj.material && obj.material.color) {
          // Rich dark Martian volcanic basalt & iron-stained breccia (never smooth tan skin-tone!)
          const isMetallicBolide = typeof obj.material.metalness === 'number' && obj.material.metalness > 0.5;
          if (!isMetallicBolide) {
            obj.material.color.setHex(0x472315);
            obj.material.roughness = 0.93;
            obj.material.metalness = 0.05;
          }
          obj.material.flatShading = true;
          obj.material.needsUpdate = true;
        }
      }
    });

    // 4. Scale down giant 1.35m yellow sample-tube pillars into realistic 0.32m NASA Titanium Sample Cache Tubes
    if (api.tubesOverlayGroup) {
      api.tubesOverlayGroup.children.forEach(m => {
        m.scale.set(0.38, 0.24, 0.38);
        if (typeof api.sampleHeight === 'function') {
          m.position.y = api.sampleHeight(m.position.x, m.position.z) + 0.18;
        }
      });
    }

    // 5. Hide the 72 jagged cyan Ingenuity flight spiderweb lines in default 3D surface view (only show when Drones tab active)
    if (api.ingenuityOverlayGroup) {
      api.ingenuityOverlayGroup.visible = false;
    }

    // 6. Refine A* Route Ribbons from thick opaque neon tubes into sleek translucent ground-hugging telemetry paths
    if (api.routeMeshes) {
      const routePalette = { safest: 0x10b981, shortest: 0xf59e0b, science: 0x06b6d4 };
      Object.keys(api.routeMeshes).forEach(k => {
        const rm = api.routeMeshes[k];
        if (rm && rm.material) {
          rm.material.color.setHex(routePalette[k] || 0x10b981);
          if (rm.material.emissive) rm.material.emissive.setHex(routePalette[k] || 0x10b981);
          rm.material.emissiveIntensity = 0.45;
          rm.material.transparent = true;
          rm.material.opacity = 0.82;
          rm.position.y = -0.07; // Hug regolith contour closely
          rm.material.needsUpdate = true;
        }
      });
    }

    // 7. Hide legacy boxy procedural truck at (22.5, 13.5) so only authentic NASA GLB spacecraft & facilities appear
    if (api.surfaceGroup) {
      api.surfaceGroup.children.forEach(ch => {
        if (ch.isGroup && !ch.userData.isHeroGlb && Math.abs(ch.position.x - 22.5) < 3.5 && Math.abs(ch.position.z - 13.5) < 3.5) {
          ch.visible = false;
        }
      });
    }
  }

  function bootstrap() {
    const api = getApi();
    if (!api) {
      setTimeout(bootstrap, 150);
      return;
    }
    applyStudioGradeMars3dColorsAndLighting(api);
    setTimeout(() => applyStudioGradeMars3dColorsAndLighting(api), 900);
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

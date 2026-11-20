// MARS SENTINEL — Interactive 3D Web Digital Twin (Three.js + Mission Control HUD)

(() => {
  const canvas = document.getElementById('mars-canvas');
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance' });
  renderer.setSize(window.innerWidth, window.innerHeight);
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.12;
  renderer.outputEncoding = THREE.sRGBEncoding;
  window.__MARS_WEB_BRIDGE = window.__MARS_WEB_BRIDGE || {};
  window.__MARS_WEB_BRIDGE.renderer = renderer;
  const __origRender = renderer.render.bind(renderer);
  renderer.render = function (sc, cam) {
    window.__MARS_WEB_BRIDGE.scene = sc;
    window.__MARS_WEB_BRIDGE.camera = cam;
    if (window.__MarsRealism && typeof window.__MarsRealism.onBeforeRender === 'function') {
      window.__MarsRealism.onBeforeRender(sc, cam, renderer);
    }
    return __origRender(sc, cam);
  };

  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0xb5835a);
  scene.fog = new THREE.FogExp2(0xb5835a, 0.0042);

  const camera = new THREE.PerspectiveCamera(48, window.innerWidth / window.innerHeight, 0.2, 8000);

  // Camera Orbit State
  const camState = {
    target: new THREE.Vector3(-2, 1.8, 12),
    spherical: { radius: 28, phi: 1.38, theta: 0.08 },
    desiredTarget: new THREE.Vector3(-2, 1.8, 12),
    desiredSpherical: { radius: 28, phi: 1.38, theta: 0.08 },
    mode: 'surface', // 'surface' | 'orbit' | 'agriculture' | 'habitat' | 'tactical'
    timeOfDay: 'DAY'
  };

  // --- GROUPS ---
  const surfaceGroup = new THREE.Group();
  const orbitGroup = new THREE.Group();
  orbitGroup.visible = false;
  scene.add(surfaceGroup);
  scene.add(orbitGroup);

  // --- STARFIELD (90-Node Milky Way & Voronoi Equivalent for Orbit + Night) ---
  const starGeo = new THREE.BufferGeometry();
  const starCount = 3600;
  const starPos = new Float32Array(starCount * 3);
  const starCol = new Float32Array(starCount * 3);
  for (let i = 0; i < starCount; i++) {
    const r = 1200 + Math.random() * 1500;
    const theta = Math.random() * Math.PI * 2;
    const phi = Math.acos(2 * Math.random() - 1);
    starPos[i * 3] = r * Math.sin(phi) * Math.cos(theta);
    starPos[i * 3 + 1] = Math.abs(r * Math.cos(phi)) + 40; // Above horizon + full sphere
    starPos[i * 3 + 2] = r * Math.sin(phi) * Math.sin(theta);

    const tint = Math.random();
    if (tint < 0.25) {
      starCol[i * 3] = 1.0; starCol[i * 3 + 1] = 0.88; starCol[i * 3 + 2] = 0.72;
    } else if (tint < 0.5) {
      starCol[i * 3] = 0.78; starCol[i * 3 + 1] = 0.88; starCol[i * 3 + 2] = 1.0;
    } else {
      starCol[i * 3] = 0.96; starCol[i * 3 + 1] = 0.96; starCol[i * 3 + 2] = 0.98;
    }
  }
  starGeo.setAttribute('position', new THREE.BufferAttribute(starPos, 3));
  starGeo.setAttribute('color', new THREE.BufferAttribute(starCol, 3));
  const starTex = (function () {
    const c = document.createElement('canvas');
    c.width = 64;
    c.height = 64;
    const g = c.getContext('2d');
    const gr = g.createRadialGradient(32, 32, 0, 32, 32, 32);
    gr.addColorStop(0.0, 'rgba(255,255,255,1.0)');
    gr.addColorStop(0.28, 'rgba(225,238,255,0.85)');
    gr.addColorStop(0.6, 'rgba(180,210,255,0.22)');
    gr.addColorStop(1.0, 'rgba(0,0,0,0.0)');
    g.fillStyle = gr;
    g.fillRect(0, 0, 64, 64);
    return new THREE.CanvasTexture(c);
  })();
  const starMat = new THREE.PointsMaterial({
    size: 3.4,
    map: starTex,
    vertexColors: true,
    transparent: true,
    opacity: 0.0,
    fog: false,
    depthWrite: false,
    blending: THREE.AdditiveBlending
  });
  const starField = new THREE.Points(starGeo, starMat);
  scene.add(starField);

  // --- LIGHTING ---
  const hemiLight = new THREE.HemisphereLight(0xe58754, 0x3b160a, 0.54);
  scene.add(hemiLight);

  const sunLight = new THREE.DirectionalLight(0xffcfa3, 1.38);
  sunLight.position.set(-45, 55, -30);
  sunLight.castShadow = true;
  sunLight.shadow.mapSize.width = 2048;
  sunLight.shadow.mapSize.height = 2048;
  sunLight.shadow.camera.near = 1;
  sunLight.shadow.camera.far = 220;
  const d = 55;
  sunLight.shadow.camera.left = -d;
  sunLight.shadow.camera.right = d;
  sunLight.shadow.camera.top = d;
  sunLight.shadow.camera.bottom = -d;
  scene.add(sunLight);

  // ==============================================================================
  // FEATURE 18: DYNAMIC 3D SUN DISK, CORONA GLOW BILLBOARD & JEZERO DIURNAL ARC
  // ==============================================================================
  function createMartianSunTexture() {
    const c = document.createElement('canvas');
    c.width = 256;
    c.height = 256;
    const ctx = c.getContext('2d');
    const grad = ctx.createRadialGradient(128, 128, 6, 128, 128, 128);
    grad.addColorStop(0.0, 'rgba(255, 255, 250, 1.0)');  // Brilliant core photosphere
    grad.addColorStop(0.12, 'rgba(255, 245, 215, 0.95)'); // Solar limb
    grad.addColorStop(0.32, 'rgba(125, 185, 255, 0.60)'); // Martian blue forward-scattered corona
    grad.addColorStop(0.65, 'rgba(70, 130, 210, 0.22)');  // High-altitude aerosol halo
    grad.addColorStop(1.0, 'rgba(30, 70, 150, 0.0)');    // Outer boundary
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, 256, 256);
    return new THREE.CanvasTexture(c);
  }

  const sunGroup = new THREE.Group();
  sunGroup.name = 'MS_Surface_Sun_Group';
  const sunTexture = createMartianSunTexture();

  const sunSpriteMat = new THREE.SpriteMaterial({
    map: sunTexture,
    color: 0xffffff,
    transparent: true,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    fog: false
  });
  const sunCoronaSprite = new THREE.Sprite(sunSpriteMat);
  sunCoronaSprite.scale.set(64, 64, 1.0);
  sunGroup.add(sunCoronaSprite);

  const sunCoreGeo = new THREE.CircleGeometry(13, 48);
  const sunCoreMat = new THREE.MeshBasicMaterial({
    color: 0xfff8e6,
    transparent: true,
    opacity: 0.98,
    depthWrite: false,
    fog: false,
    side: THREE.DoubleSide
  });
  const sunCoreMesh = new THREE.Mesh(sunCoreGeo, sunCoreMat);
  sunGroup.add(sunCoreMesh);
  scene.add(sunGroup);

  // Diurnal Solar Calculation for Jezero Crater (18.38°N, 77.58°E)
  function calculateJezeroSolarVector(hourAngleRad, declinationRad = 0.0) {
    const latRad = 18.38 * Math.PI / 180;
    const sinH = Math.sin(latRad) * Math.sin(declinationRad) +
                 Math.cos(latRad) * Math.cos(declinationRad) * Math.cos(hourAngleRad);
    const elev = Math.asin(Math.max(-1.0, Math.min(1.0, sinH)));
    const cosH = Math.cos(elev);
    const sinA = -Math.sin(hourAngleRad) * Math.cos(declinationRad) / (Math.abs(cosH) > 1e-5 ? cosH : 1e-5);
    const cosA = (Math.sin(declinationRad) - Math.sin(latRad) * sinH) /
                 (Math.cos(latRad) * (Math.abs(cosH) > 1e-5 ? cosH : 1e-5));
    const az = Math.atan2(sinA, cosA);

    const dir = new THREE.Vector3(
      -Math.cos(elev) * Math.sin(az),
      Math.sin(elev),
      -Math.cos(elev) * Math.cos(az)
    ).normalize();

    return { elevation: elev, azimuth: az, sinH: sinH, dir: dir };
  }

  // ==============================================================================
  // FEATURE 20: HIGH-FIDELITY MARTIAN SAND SHADER & PROCEDURAL MICRO-NORMAL TEXTURE
  // ==============================================================================
  function createMartianSandNormalTexture() {
    const size = 512;
    const canvas = document.createElement('canvas');
    canvas.width = size;
    canvas.height = size;
    const ctx = canvas.getContext('2d');
    const imgData = ctx.createImageData(size, size);
    const data = imgData.data;

    const height = new Float32Array(size * size);
    const twoPi = Math.PI * 2;

    for (let y = 0; y < size; y++) {
      const v = (y / size) * twoPi;
      for (let x = 0; x < size; x++) {
        const u = (x / size) * twoPi;

        // Primary asymmetric aeolian dune ripples (wind direction ~30 deg)
        const ripplePhase = 8 * u + 4 * v;
        const sinR = Math.sin(ripplePhase);
        const primaryWave = Math.sign(sinR) * Math.pow(Math.abs(sinR), 0.7) * 0.55;

        // Secondary transverse cross-dune ripples (wind turbulence)
        const crossWave = Math.sin(16 * u - 8 * v + 1.2) * 0.22;

        // Fine secondary ripple crests
        const fineRipples = Math.sin(32 * u + 12 * v) * 0.12;

        // High-frequency basaltic regolith grit / sand grain micro-noise
        const microGrit = (Math.sin(64 * u - 32 * v) * Math.cos(48 * u + 48 * v)) * 0.08 +
                          (Math.sin(128 * u + 64 * v) * Math.cos(96 * u - 32 * v)) * 0.03;

        height[y * size + x] = primaryWave + crossWave + fineRipples + microGrit;
      }
    }

    const bumpScale = 4.5;
    for (let y = 0; y < size; y++) {
      const yPrev = (y - 1 + size) % size;
      const yNext = (y + 1) % size;
      for (let x = 0; x < size; x++) {
        const xPrev = (x - 1 + size) % size;
        const xNext = (x + 1) % size;

        const dhdx = (height[y * size + xNext] - height[y * size + xPrev]) * 0.5;
        const dhdy = (height[yNext * size + x] - height[yPrev * size + x]) * 0.5;

        let nx = -dhdx * bumpScale;
        let ny = -dhdy * bumpScale;
        let nz = 1.0;

        const invLen = 1.0 / Math.sqrt(nx * nx + ny * ny + nz * nz);
        nx *= invLen;
        ny *= invLen;
        nz *= invLen;

        const idx = (y * size + x) * 4;
        data[idx]     = Math.floor((nx * 0.5 + 0.5) * 255);
        data[idx + 1] = Math.floor((ny * 0.5 + 0.5) * 255);
        data[idx + 2] = Math.floor((nz * 0.5 + 0.5) * 255);
        data[idx + 3] = 255;
      }
    }

    ctx.putImageData(imgData, 0, 0);

    const normalTexture = new THREE.CanvasTexture(canvas);
    normalTexture.wrapS = THREE.RepeatWrapping;
    normalTexture.wrapT = THREE.RepeatWrapping;
    normalTexture.repeat.set(40, 40);
    normalTexture.generateMipmaps = true;
    normalTexture.minFilter = THREE.LinearMipmapLinearFilter;
    normalTexture.magFilter = THREE.LinearFilter;
    return normalTexture;
  }

  // --- SURFACE TERRAIN (NASA HiRISE Rusty-Brown Regolith + Aeolian Ripples) ---
  function sampleHeight(x, z) {
    const crater = Math.exp(-((x - 18) ** 2 + (z - 28) ** 2) / 450) * -2.2;
    const ridge = Math.sin(x * 0.045 + z * 0.035) * 1.4 + Math.cos(x * 0.12 - z * 0.08) * 0.45;
    const ripples = Math.sin(x * 0.8 + z * 0.6) * 0.08;
    // Flatten around base camp (-32..28, 0..32)
    const flatMask = Math.exp(-((x + 4) ** 2 + (z - 16) ** 2) / 900);
    return (crater + ridge + ripples) * (1 - 0.82 * flatMask);
  }

  const terrGeo = new THREE.PlaneGeometry(360, 360, 180, 180);
  terrGeo.rotateX(-Math.PI / 2);
  const posAttr = terrGeo.attributes.position;
  const colAttr = new Float32Array(posAttr.count * 3);

  // Pass 1: Heightfield displacement
  for (let i = 0; i < posAttr.count; i++) {
    const vx = posAttr.getX(i);
    const vz = posAttr.getZ(i);
    const vy = sampleHeight(vx, vz);
    posAttr.setY(i, vy);
  }
  terrGeo.computeVertexNormals();

  // Pass 2: Authentic slope-dependent and elevation-dependent geologic shading
  const normAttr = terrGeo.attributes.normal;
  for (let i = 0; i < posAttr.count; i++) {
    const vx = posAttr.getX(i);
    const vy = posAttr.getY(i);
    const vz = posAttr.getZ(i);

    // Normal Ny: 1.0 on flat plain, lower on steep crater slopes
    const ny = Math.max(0.0, Math.min(1.0, normAttr.getY(i)));
    // Slope factor: 0.0 on flat regolith dunes, 1.0 on steep crater rims and scarps (>25 deg)
    const slopeFactor = Math.max(0.0, Math.min(1.0, (1.0 - ny) / 0.18));

    // High-frequency subtle noise for natural sand grain heterogeneity
    const noise = 0.92 + 0.16 * Math.sin(vx * 0.35) * Math.cos(vz * 0.35) + 0.06 * Math.sin(vx * 1.2 - vz * 0.9);

    // Flat regolith sand: warm iron-oxide rusty ochre (Perseverance / HiRISE calibrated)
    const sandR = 0.42 * noise;
    const sandG = 0.24 * noise;
    const sandB = 0.15 * noise;

    // Steep scarp rock: dark exposed basaltic bedrock / scree talus
    const rockR = 0.18 * noise;
    const rockG = 0.13 * noise;
    const rockB = 0.10 * noise;

    // Elevation tint: slightly lighter dust on high ridges, darker basalt traps in crater hollows
    const elevFactor = Math.max(-1.0, Math.min(1.0, vy / 3.0));
    const dustTint = elevFactor > 0 ? elevFactor * 0.05 : elevFactor * 0.03;

    // Geologic slope blend: eliminates flat polygon shading
    colAttr[i * 3]     = THREE.MathUtils.clamp((sandR * (1.0 - slopeFactor) + rockR * slopeFactor) + dustTint, 0.05, 0.85);
    colAttr[i * 3 + 1] = THREE.MathUtils.clamp((sandG * (1.0 - slopeFactor) + rockG * slopeFactor) + dustTint * 0.6, 0.04, 0.65);
    colAttr[i * 3 + 2] = THREE.MathUtils.clamp((sandB * (1.0 - slopeFactor) + rockB * slopeFactor) + dustTint * 0.4, 0.03, 0.50);
  }
  terrGeo.setAttribute('color', new THREE.BufferAttribute(colAttr, 3));

  const sandNormalTexture = createMartianSandNormalTexture();
  const terrMat = new THREE.MeshStandardMaterial({
    vertexColors: true,
    normalMap: sandNormalTexture,
    normalScale: new THREE.Vector2(0.85, 0.85),
    roughness: 0.90,
    metalness: 0.06
  });
  const terrainMesh = new THREE.Mesh(terrGeo, terrMat);
  terrainMesh.receiveShadow = true;
  surfaceGroup.add(terrainMesh);

  // Scatter Martian Basaltic Rocks
  const rockGeo = new THREE.DodecahedronGeometry(0.45, 1);
  const rockMat = new THREE.MeshStandardMaterial({ color: 0x3b2318, roughness: 0.92 });
  for (let i = 0; i < 95; i++) {
    const rx = (Math.random() - 0.5) * 160;
    const rz = (Math.random() - 0.5) * 160;
    if (Math.abs(rx + 4) < 28 && Math.abs(rz - 16) < 18) continue;
    const rock = new THREE.Mesh(rockGeo, rockMat);
    const s = 0.4 + Math.random() * 1.4;
    rock.scale.set(s * 1.3, s * 0.65, s);
    rock.position.set(rx, sampleHeight(rx, rz) + 0.1, rz);
    rock.rotation.y = Math.random() * Math.PI;
    rock.castShadow = true;
    rock.receiveShadow = true;
    surfaceGroup.add(rock);
  }

  // --- SPHERICAL 4-LEGGED HABITAT BASE POD (Sketchfab_model.012) at (2, y, 22) ---
  const habGroup = new THREE.Group();
  const habY = sampleHeight(2, 22);
  habGroup.position.set(2, habY, 22);

  const podMat = new THREE.MeshStandardMaterial({ color: 0x1b1e24, metalness: 0.75, roughness: 0.32 });
  const accentOrange = new THREE.MeshStandardMaterial({ color: 0xe0532c, emissive: 0x8a2810, emissiveIntensity: 0.4 });
  const winGreenMat = new THREE.MeshStandardMaterial({ color: 0x38f26a, emissive: 0x22c55e, emissiveIntensity: 1.4 });

  const podBody = new THREE.Mesh(new THREE.SphereGeometry(3.5, 32, 24, 0, Math.PI * 2, 0, Math.PI * 0.78), podMat);
  podBody.position.y = 4.2;
  podBody.castShadow = true;
  habGroup.add(podBody);

  // Glowing Habitat Windows
  for (let w = -1; w <= 1; w += 2) {
    const win = new THREE.Mesh(new THREE.BoxGeometry(0.95, 0.38, 0.2), winGreenMat);
    win.position.set(w * 1.35, 4.6, -3.28);
    habGroup.add(win);
  }

  // 4 Articulated Landing Legs
  for (let l = 0; l < 4; l++) {
    const ang = (l * Math.PI) / 2 + Math.PI / 4;
    const leg = new THREE.Mesh(new THREE.BoxGeometry(0.45, 4.4, 0.55), podMat);
    leg.position.set(Math.cos(ang) * 3.4, 2.1, Math.sin(ang) * 3.4);
    leg.rotation.z = -Math.cos(ang) * 0.22;
    leg.rotation.x = Math.sin(ang) * 0.22;
    leg.castShadow = true;
    habGroup.add(leg);

    const stripe = new THREE.Mesh(new THREE.BoxGeometry(0.48, 3.8, 0.12), accentOrange);
    stripe.position.copy(leg.position);
    stripe.rotation.copy(leg.rotation);
    habGroup.add(stripe);
  }

  const habLight = new THREE.PointLight(0xffb478, 1.4, 24);
  habLight.position.set(0, 3.5, -4.5);
  habGroup.add(habLight);
  surfaceGroup.add(habGroup);

  // --- MARS AGRICULTURE, HYDROPONICS & AEROPONICS ZONE (MS_Agriculture_Zone) ---
  const agriGroup = new THREE.Group();
  const agY = sampleHeight(-18, 18);
  agriGroup.position.set(-18, agY, 18);

  const frameMat = new THREE.MeshStandardMaterial({ color: 0x262a32, metalness: 0.8, roughness: 0.3 });
  const whiteCompMat = new THREE.MeshStandardMaterial({ color: 0xdcdde2, roughness: 0.35 });
  const glassMat = new THREE.MeshPhysicalMaterial({
    color: 0xc8f7e5,
    transparent: true,
    opacity: 0.34,
    roughness: 0.08,
    metalness: 0.05
  });
  const cropLushMat = new THREE.MeshStandardMaterial({ color: 0x22c55e, emissive: 0x15803d, emissiveIntensity: 0.45, roughness: 0.5 });
  const cropLimeMat = new THREE.MeshStandardMaterial({ color: 0x84cc16, emissive: 0x4d7c0f, emissiveIntensity: 0.45, roughness: 0.5 });
  const cropRedMat = new THREE.MeshStandardMaterial({ color: 0x991b1b, emissive: 0x450a0a, emissiveIntensity: 0.3, roughness: 0.5 });
  const growLedMat = new THREE.MeshStandardMaterial({ color: 0xf0abfc, emissive: 0xe879f9, emissiveIntensity: 2.2 });

  // 1. Vaulted Hydroponics Greenhouse (GH-01)
  const deck1 = new THREE.Mesh(new THREE.BoxGeometry(7.4, 0.4, 11.6), frameMat);
  deck1.position.set(0, 0.2, 0);
  agriGroup.add(deck1);

  const vaultGeo = new THREE.CylinderGeometry(3.5, 3.5, 11.2, 28, 1, false, 0, Math.PI);
  vaultGeo.rotateZ(Math.PI / 2);
  vaultGeo.rotateY(Math.PI / 2);
  const vaultGlass = new THREE.Mesh(vaultGeo, glassMat);
  vaultGlass.position.set(0, 0.4, 0);
  agriGroup.add(vaultGlass);

  // Structural Arch Ribs
  for (let r = -5; r <= 5; r += 2) {
    const ribGeo = new THREE.TorusGeometry(3.55, 0.09, 10, 28, Math.PI);
    const rib = new THREE.Mesh(ribGeo, r % 4 === 0 ? frameMat : whiteCompMat);
    rib.position.set(0, 0.4, r);
    agriGroup.add(rib);
  }

  // Front Airlock Module
  const airlock = new THREE.Mesh(new THREE.BoxGeometry(2.1, 2.3, 2.1), whiteCompMat);
  airlock.position.set(0, 1.35, -6.4);
  airlock.castShadow = true;
  agriGroup.add(airlock);

  // Multi-Tier Hydroponic Racks & Crops inside GH-01
  [-2.0, 0.0, 2.0].forEach((rx, sIdx) => {
    [0.85, 1.6, 2.3].forEach((tz, tIdx) => {
      if (rx === 0 && tIdx === 2) return;
      const tray = new THREE.Mesh(new THREE.BoxGeometry(0.95, 0.1, 9.4), frameMat);
      tray.position.set(rx, tz, 0);
      agriGroup.add(tray);

      const led = new THREE.Mesh(new THREE.BoxGeometry(0.55, 0.05, 9.2), growLedMat);
      led.position.set(rx, tz + 0.55, 0);
      agriGroup.add(led);

      for (let c = -4.2; c <= 4.2; c += 0.7) {
        const cMat = (Math.abs(Math.round(c)) + tIdx + sIdx) % 3 === 0 ? cropLushMat : ((Math.abs(Math.round(c)) + tIdx) % 2 === 0 ? cropLimeMat : cropRedMat);
        const plant = new THREE.Mesh(new THREE.SphereGeometry(0.22, 10, 8), cMat);
        plant.scale.set(1.1, 0.75, 1.1);
        plant.position.set(rx + (c % 2 === 0 ? 0.2 : -0.2), tz + 0.18, c);
        agriGroup.add(plant);
      }
    });
  });

  // 2. Geodesic Aeroponics Dome (GH-02) at (-9.2, 0, 0.5)
  const domeGlass = new THREE.Mesh(new THREE.SphereGeometry(4.4, 24, 16, 0, Math.PI * 2, 0, Math.PI / 2), glassMat);
  domeGlass.position.set(-9.2, 0.35, 0.5);
  agriGroup.add(domeGlass);

  const domeWire = new THREE.Mesh(
    new THREE.SphereGeometry(4.45, 14, 10, 0, Math.PI * 2, 0, Math.PI / 2),
    new THREE.MeshStandardMaterial({ color: 0xe2e8f0, wireframe: true })
  );
  domeWire.position.set(-9.2, 0.35, 0.5);
  agriGroup.add(domeWire);

  // Central Algae Bioreactor + 8 Aeroponic Towers
  const bioreactor = new THREE.Mesh(
    new THREE.CylinderGeometry(0.55, 0.55, 3.2, 20),
    new THREE.MeshStandardMaterial({ color: 0x06b6d4, emissive: 0x0891b2, emissiveIntensity: 0.6 })
  );
  bioreactor.position.set(-9.2, 1.95, 0.5);
  agriGroup.add(bioreactor);

  for (let a = 0; a < 8; a++) {
    const ang = (a * Math.PI * 2) / 8;
    const tx = -9.2 + Math.cos(ang) * 2.4;
    const tz = 0.5 + Math.sin(ang) * 2.4;
    const tower = new THREE.Mesh(new THREE.CylinderGeometry(0.24, 0.24, 2.3, 12), whiteCompMat);
    tower.position.set(tx, 1.5, tz);
    agriGroup.add(tower);

    for (let h = 0.7; h <= 2.3; h += 0.4) {
      const leaf = new THREE.Mesh(new THREE.SphereGeometry(0.24, 8, 8), a % 2 === 0 ? cropLushMat : cropLimeMat);
      leaf.position.set(tx, h, tz);
      agriGroup.add(leaf);
    }
  }

  // 3. Exterior Water Storage & Recycling Tanks + Color-Coded Piping
  const pipeColors = [0x0ea5e9, 0x22c55e, 0xeab308];
  [ -2.2, 0.0, 2.2 ].forEach((tz, idx) => {
    const tank = new THREE.Mesh(new THREE.CylinderGeometry(0.82, 0.82, 2.6, 20), whiteCompMat);
    tank.position.set(5.4, 1.4, tz + 2.5);
    tank.castShadow = true;
    agriGroup.add(tank);

    const band = new THREE.Mesh(
      new THREE.CylinderGeometry(0.86, 0.86, 0.22, 20),
      new THREE.MeshStandardMaterial({ color: pipeColors[idx] || 0x06b6d4 })
    );
    band.position.set(5.4, 1.8, tz + 2.5);
    agriGroup.add(band);
  });

  const agriLight1 = new THREE.PointLight(0xa8ff78, 1.8, 22);
  agriLight1.position.set(0, 2.6, 0);
  agriGroup.add(agriLight1);

  const agriLight2 = new THREE.PointLight(0x78ffd6, 1.6, 18);
  agriLight2.position.set(-9.2, 2.8, 0.5);
  agriGroup.add(agriLight2);

  surfaceGroup.add(agriGroup);

  // --- PERSEVERANCE ROVER & HEAVY EXPLORATION VEHICLE & ASTRONAUT CREW ---
  function createAstronaut(x, z, suitColor) {
    const g = new THREE.Group();
    const y = sampleHeight(x, z);
    g.position.set(x, y, z);
    const matSuit = new THREE.MeshStandardMaterial({ color: suitColor, roughness: 0.45 });
    const matVisor = new THREE.MeshStandardMaterial({ color: 0xd97706, metalness: 0.9, roughness: 0.15 });

    const torso = new THREE.Mesh(new THREE.CylinderGeometry(0.32, 0.28, 1.15, 12), matSuit);
    torso.position.y = 1.15;
    torso.castShadow = true;
    g.add(torso);

    const helmet = new THREE.Mesh(new THREE.SphereGeometry(0.28, 16, 16), matSuit);
    helmet.position.y = 1.92;
    g.add(helmet);

    const visor = new THREE.Mesh(new THREE.SphereGeometry(0.25, 16, 16), matVisor);
    visor.position.set(0, 1.92, -0.08);
    g.add(visor);

    const plss = new THREE.Mesh(new THREE.BoxGeometry(0.52, 0.75, 0.35), matSuit);
    plss.position.set(0, 1.25, 0.28);
    g.add(plss);

    return g;
  }

  const astronautGroups = [
    createAstronaut(4.2, 11.5, 0xd94814),
    createAstronaut(-7.8, 10.2, 0xd94814),
    createAstronaut(-10.5, 6.2, 0x2b5b84),
    createAstronaut(-12.2, 6.4, 0x2b5b84)
  ];
  astronautGroups.forEach(a => surfaceGroup.add(a));

  // ==============================================================================
  // FEATURE 21: INGENUITY MARS HELICOPTER (WRIGHT BROTHERS FIELD AIRFIELD)
  // ==============================================================================
  function createIngenuityHelicopter(x, z) {
    const heliGroup = new THREE.Group();
    const groundY = sampleHeight(x, z);
    heliGroup.position.set(x, groundY, z);

    const goldFoilMat = new THREE.MeshStandardMaterial({
      color: 0xd97706,
      metalness: 0.85,
      roughness: 0.22
    });
    const carbonMat = new THREE.MeshStandardMaterial({
      color: 0x1e293b,
      metalness: 0.3,
      roughness: 0.5
    });
    const solarMat = new THREE.MeshStandardMaterial({
      color: 0x1e3a8a,
      metalness: 0.7,
      roughness: 0.3
    });

    // 1. Fuselage / Avionics Warm Electronics Box
    const fuselage = new THREE.Mesh(new THREE.BoxGeometry(0.38, 0.32, 0.38), goldFoilMat);
    fuselage.position.y = 0.48;
    fuselage.castShadow = true;
    heliGroup.add(fuselage);

    // Downward optical navigation camera lens
    const camLens = new THREE.Mesh(
      new THREE.CylinderGeometry(0.04, 0.04, 0.08, 12),
      new THREE.MeshStandardMaterial({ color: 0x0f172a, roughness: 0.1 })
    );
    camLens.position.set(0, 0.30, 0);
    heliGroup.add(camLens);

    // 2. 4 Angled Composite Landing Legs
    for (let i = 0; i < 4; i++) {
      const legAng = (i * Math.PI) / 2 + Math.PI / 4;
      const leg = new THREE.Mesh(new THREE.CylinderGeometry(0.016, 0.016, 0.52, 8), carbonMat);
      leg.position.set(Math.cos(legAng) * 0.28, 0.24, Math.sin(legAng) * 0.28);
      leg.rotation.z = -Math.cos(legAng) * 0.45;
      leg.rotation.x = Math.sin(legAng) * 0.45;
      leg.castShadow = true;
      heliGroup.add(leg);

      const foot = new THREE.Mesh(new THREE.SphereGeometry(0.035, 8, 8), carbonMat);
      foot.position.set(Math.cos(legAng) * 0.44, 0.04, Math.sin(legAng) * 0.44);
      heliGroup.add(foot);
    }

    // 3. Central Vertical Rotor Mast
    const mast = new THREE.Mesh(new THREE.CylinderGeometry(0.022, 0.022, 0.72, 10), carbonMat);
    mast.position.y = 0.82;
    heliGroup.add(mast);

    // 4. Lower Rotor Assembly (Group)
    const lowerRotorGroup = new THREE.Group();
    lowerRotorGroup.position.y = 0.76;
    const lowerHub = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.05, 0.06, 12), goldFoilMat);
    lowerRotorGroup.add(lowerHub);
    const lowerBlades = new THREE.Mesh(new THREE.BoxGeometry(1.35, 0.014, 0.085), carbonMat);
    lowerBlades.castShadow = true;
    lowerRotorGroup.add(lowerBlades);
    heliGroup.add(lowerRotorGroup);

    // 5. Upper Rotor Assembly (Group)
    const upperRotorGroup = new THREE.Group();
    upperRotorGroup.position.y = 0.94;
    const upperHub = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.05, 0.06, 12), goldFoilMat);
    upperRotorGroup.add(upperHub);
    const upperBlades = new THREE.Mesh(new THREE.BoxGeometry(1.35, 0.014, 0.085), carbonMat);
    upperBlades.castShadow = true;
    upperRotorGroup.add(upperBlades);
    heliGroup.add(upperRotorGroup);

    // 6. Top Solar Panel
    const solarPanel = new THREE.Mesh(new THREE.BoxGeometry(0.36, 0.02, 0.32), solarMat);
    solarPanel.position.y = 1.14;
    solarPanel.castShadow = true;
    heliGroup.add(solarPanel);

    return {
      group: heliGroup,
      upperRotor: upperRotorGroup,
      lowerRotor: lowerRotorGroup,
      baseY: groundY
    };
  }

  const ingenuity = createIngenuityHelicopter(13.8, 6.2);
  surfaceGroup.add(ingenuity.group);

  // Perseverance Rover at (8.5, y, 9.8)
  const roverGroup = new THREE.Group();
  const rBaseX = 8.5;
  const rBaseZ = 9.8;
  const rY = sampleHeight(rBaseX, rBaseZ);
  roverGroup.position.set(rBaseX, rY, rBaseZ);
  const rChassis = new THREE.Mesh(new THREE.BoxGeometry(2.1, 0.85, 2.8), whiteCompMat);
  rChassis.position.y = 1.05;
  rChassis.castShadow = true;
  roverGroup.add(rChassis);

  const rMast = new THREE.Mesh(new THREE.CylinderGeometry(0.09, 0.09, 1.2, 10), frameMat);
  rMast.position.set(-0.5, 1.85, -0.95);
  roverGroup.add(rMast);

  const rHead = new THREE.Mesh(new THREE.BoxGeometry(0.45, 0.28, 0.32), whiteCompMat);
  rHead.position.set(-0.5, 2.45, -0.95);
  roverGroup.add(rHead);

  const roverWheels = [];
  [-1.1, 1.1].forEach(wx => {
    [-1.1, 0, 1.1].forEach(wz => {
      const wheel = new THREE.Mesh(new THREE.CylinderGeometry(0.38, 0.38, 0.32, 16), frameMat);
      wheel.rotation.z = Math.PI / 2;
      wheel.position.set(wx, 0.38, wz);
      wheel.castShadow = true;
      roverGroup.add(wheel);
      roverWheels.push(wheel);
    });
  });
  surfaceGroup.add(roverGroup);

  // Heavy Exploration Pressurized Crew Vehicle parked cleanly in base motor pool
  const heavyVeh = new THREE.Group();
  heavyVeh.position.set(22.5, sampleHeight(22.5, 13.5), 13.5);
  heavyVeh.rotation.y = -0.65;
  const hvChassis = new THREE.Mesh(
    new THREE.BoxGeometry(2.6, 0.45, 5.2),
    frameMat
  );
  hvChassis.position.y = 0.85;
  hvChassis.castShadow = true;
  heavyVeh.add(hvChassis);
  const hvBody = new THREE.Mesh(
    new THREE.BoxGeometry(2.5, 1.35, 3.8),
    whiteCompMat
  );
  hvBody.position.set(0, 1.72, -0.35);
  hvBody.castShadow = true;
  heavyVeh.add(hvBody);
  const hvTrim = new THREE.Mesh(
    new THREE.BoxGeometry(2.56, 0.22, 3.84),
    new THREE.MeshStandardMaterial({ color: 0xea580c, metalness: 0.25, roughness: 0.45 })
  );
  hvTrim.position.set(0, 1.32, -0.35);
  heavyVeh.add(hvTrim);
  const hvCockpit = new THREE.Mesh(
    new THREE.BoxGeometry(2.2, 0.95, 1.15),
    new THREE.MeshStandardMaterial({ color: 0x0f172a, metalness: 0.85, roughness: 0.14 })
  );
  hvCockpit.position.set(0, 1.66, 1.65);
  hvCockpit.rotation.x = 0.28;
  heavyVeh.add(hvCockpit);
  const hvHeadlight = new THREE.Mesh(
    new THREE.BoxGeometry(1.9, 0.18, 0.12),
    new THREE.MeshStandardMaterial({ color: 0xe0f2fe, emissive: 0x38bdf8, emissiveIntensity: 2.0 })
  );
  hvHeadlight.position.set(0, 1.05, 2.62);
  heavyVeh.add(hvHeadlight);
  [-1.45, 1.45].forEach(wx => {
    [-1.75, 0, 1.75].forEach(wz => {
      const w = new THREE.Mesh(new THREE.CylinderGeometry(0.56, 0.56, 0.38, 16), frameMat);
      w.rotation.z = Math.PI / 2;
      w.position.set(wx, 0.56, wz);
      w.castShadow = true;
      heavyVeh.add(w);
    });
  });
  surfaceGroup.add(heavyVeh);

  const nasaData = (typeof window !== 'undefined' && window.MARS_NASA_DATA) ? window.MARS_NASA_DATA : null;

  function routeGridToSceneXZ(east, north) {
    const landE = (nasaData && nasaData.origin && typeof nasaData.origin.local_east_m === 'number')
      ? nasaData.origin.local_east_m : 512.73;
    const landN = (nasaData && nasaData.origin && typeof nasaData.origin.local_north_m === 'number')
      ? nasaData.origin.local_north_m : 945.43;
    const scale = 0.10; // Conformal 1:10 scale keeping 67.88m lateral separation crisp in tactical basin
    return [
      8.5 - (east - landE) * scale,
      9.8 - (north - landN) * scale
    ];
  }

  function enuToSceneXZ(eastM, northM) {
    const tacticalScale = 0.10; // Exact 1:10 parity with routeGridToSceneXZ across Jezero HiRISE basin
    const dx = -eastM * tacticalScale;
    const dz = -northM * tacticalScale;
    const r = Math.hypot(dx, dz);
    const innerR = 115.0;
    if (r <= innerR || r === 0) {
      return [8.5 + dx, 9.8 + dz];
    }
    const mappedR = innerR + 53.0 * (1.0 - Math.exp(-(r - innerR) / 280.0));
    const k = mappedR / r;
    return [8.5 + dx * k, 9.8 + dz * k];
  }

  // --- 3 A* TRAVERSE ROUTES ON TERRAIN (REAL HiRISE DATA WITH SAFE FALLBACK) ---
  function buildRouteRibbon(points, colorHex) {
    const curve = new THREE.CatmullRomCurve3(
      points.map(([x, z]) => new THREE.Vector3(x, sampleHeight(x, z) + 0.18, z))
    );
    const geo = new THREE.TubeGeometry(curve, 64, 0.16, 8, false);
    const mat = new THREE.MeshStandardMaterial({
      color: colorHex,
      emissive: colorHex,
      emissiveIntensity: 0.75,
      roughness: 0.3
    });
    return new THREE.Mesh(geo, mat);
  }

  const safestPts = (nasaData && nasaData.routes && nasaData.routes.safest && nasaData.routes.safest.points.length >= 2)
    ? nasaData.routes.safest.points.map(([e, n]) => routeGridToSceneXZ(e, n))
    : [[4, 8], [8, 24], [14, 45], [24, 75], [38, 110]];
  const shortestPts = (nasaData && nasaData.routes && nasaData.routes.shortest && nasaData.routes.shortest.points.length >= 2)
    ? nasaData.routes.shortest.points.map(([e, n]) => routeGridToSceneXZ(e, n))
    : [[4, 8], [16, 30], [28, 60], [38, 110]];
  const sciencePts = (nasaData && nasaData.routes && nasaData.routes.science && nasaData.routes.science.points.length >= 2)
    ? nasaData.routes.science.points.map(([e, n]) => routeGridToSceneXZ(e, n))
    : [[4, 8], [-4, 28], [6, 55], [22, 85], [38, 110]];

  const routeMeshes = {
    safest: buildRouteRibbon(safestPts, 0x22c55e),
    shortest: buildRouteRibbon(shortestPts, 0xf59e0b),
    science: buildRouteRibbon(sciencePts, 0x2cd4d9)
  };
  Object.values(routeMeshes).forEach(m => surfaceGroup.add(m));

  // --- NASA MULTI-MISSION JEZERO OVERLAYS (22 SAMPLE TUBES, 72 INGENUITY FLIGHTS, PERSEVERANCE TRACK) ---
  const tubesOverlayGroup = new THREE.Group();
  const ingenuityOverlayGroup = new THREE.Group();
  const persevHistoryGroup = new THREE.Group();
  surfaceGroup.add(tubesOverlayGroup);
  surfaceGroup.add(ingenuityOverlayGroup);
  surfaceGroup.add(persevHistoryGroup);

  if (nasaData) {
    // 1. All 22 Perseverance Sample Tubes
    const tubeGeo = new THREE.CylinderGeometry(0.24, 0.24, 1.35, 10);
    const tubeMatSed = new THREE.MeshStandardMaterial({ color: 0x2cd4d9, emissive: 0x0e7490, emissiveIntensity: 0.7 });
    const tubeMatIgn = new THREE.MeshStandardMaterial({ color: 0xf59e0b, emissive: 0xb45309, emissiveIntensity: 0.65 });
    (nasaData.sample_tubes || []).forEach(tube => {
      const [tx, tz] = enuToSceneXZ(tube.east_m, tube.north_m);
      const isSed = String(tube.rock || '').toLowerCase().includes('sedimentary');
      const m = new THREE.Mesh(tubeGeo, isSed ? tubeMatSed : tubeMatIgn);
      m.position.set(tx, sampleHeight(tx, tz) + 0.75, tz);
      m.castShadow = true;
      tubesOverlayGroup.add(m);
    });

    // 2. All 72 Ingenuity Helicopter Flight Segments
    const ingMat = new THREE.LineBasicMaterial({ color: 0x38bdf8, transparent: true, opacity: 0.9 });
    (nasaData.ingenuity_flights || []).forEach(fl => {
      if (!fl.start_enu || !fl.end_enu) return;
      const [x0, z0] = enuToSceneXZ(fl.start_enu[0], fl.start_enu[1]);
      const [x1, z1] = enuToSceneXZ(fl.end_enu[0], fl.end_enu[1]);
      const midX = (x0 + x1) * 0.5;
      const midZ = (z0 + z1) * 0.5;
      const y0 = sampleHeight(x0, z0) + 0.4;
      const y1 = sampleHeight(x1, z1) + 0.4;
      const yMid = Math.max(y0, y1) + 1.15;
      const pts = [new THREE.Vector3(x0, y0, z0), new THREE.Vector3(midX, yMid, midZ), new THREE.Vector3(x1, y1, z1)];
      ingenuityOverlayGroup.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(pts), ingMat));
    });

    // 3. Dropout-Filtered Perseverance Rover Historical Traverse (segmented across leg gaps)
    const rawPts = (nasaData.perseverance_traverse && nasaData.perseverance_traverse.enu_points) || [];
    if (rawPts.length >= 2) {
      const histMat = new THREE.LineBasicMaterial({ color: 0xe07a47, transparent: true, opacity: 0.85 });
      let segVecs = [];
      let prevEnu = null;
      for (let i = 0; i < rawPts.length; i += 2) {
        const pt = rawPts[i];
        if (prevEnu && Math.hypot(pt[0] - prevEnu[0], pt[1] - prevEnu[1]) > 75.0) {
          if (segVecs.length >= 2) {
            persevHistoryGroup.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(segVecs), histMat));
          }
          segVecs = [];
        }
        const [px, pz] = enuToSceneXZ(pt[0], pt[1]);
        segVecs.push(new THREE.Vector3(px, sampleHeight(px, pz) + 0.22, pz));
        prevEnu = pt;
      }
      if (segVecs.length >= 2) {
        persevHistoryGroup.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(segVecs), histMat));
      }
    }
  }

  // --- SYNCED BASE FACILITIES (MEDICAL BAY, EDL LIFTING CRAFT, ASCENT ROCKET PAD) & CLICKABLE OUTCROPS ---
  const clickableTargets = [];
  const facilityFocusMap = {
    agri: { target: new THREE.Vector3(-20.5, 1.8, 18.0), radius: 16, phi: 1.35, theta: 2.65 },
    medical: { target: new THREE.Vector3(-9.5, 1.8, 23.0), radius: 14, phi: 1.34, theta: 3.14 },
    edl: { target: new THREE.Vector3(24.0, 2.0, -11.0), radius: 14, phi: 1.32, theta: 2.35 },
    rocket: { target: new THREE.Vector3(26.5, 5.5, 13.5), radius: 22, phi: 1.24, theta: 2.50 }
  };

  // 1. First Aid / Medical & Radiation Triage Bay (-9.5, y, 23.0)
  (function buildWebMedicalBay() {
    const g = new THREE.Group();
    const mx = -9.5, mz = 23.0;
    g.position.set(mx, sampleHeight(mx, mz), mz);
    const hullMat = new THREE.MeshStandardMaterial({ color: 0xedf2f7, roughness: 0.45, metalness: 0.25 });
    const redMat = new THREE.MeshStandardMaterial({ color: 0xdc2626, emissive: 0x7f1d1d, emissiveIntensity: 0.55 });
    const trimMat = new THREE.MeshStandardMaterial({ color: 0x1e293b, roughness: 0.5, metalness: 0.6 });

    const deck = new THREE.Mesh(new THREE.BoxGeometry(6.8, 0.25, 5.4), trimMat);
    deck.position.y = 0.12;
    g.add(deck);
    const roof = new THREE.Mesh(new THREE.BoxGeometry(7.2, 0.35, 5.8), hullMat);
    roof.position.y = 3.15;
    g.add(roof);
    const wallN = new THREE.Mesh(new THREE.BoxGeometry(6.8, 2.8, 0.25), hullMat);
    wallN.position.set(0, 1.65, 2.55);
    g.add(wallN);
    const wallW = new THREE.Mesh(new THREE.BoxGeometry(0.25, 2.8, 5.2), hullMat);
    wallW.position.set(-3.28, 1.65, 0);
    g.add(wallW);
    const wallE = new THREE.Mesh(new THREE.BoxGeometry(0.25, 2.8, 5.2), hullMat);
    wallE.position.set(3.28, 1.65, 0);
    g.add(wallE);
    const wallSL = new THREE.Mesh(new THREE.BoxGeometry(2.2, 2.8, 0.25), hullMat);
    wallSL.position.set(-2.25, 1.65, -2.55);
    g.add(wallSL);
    const wallSR = new THREE.Mesh(new THREE.BoxGeometry(2.2, 2.8, 0.25), hullMat);
    wallSR.position.set(2.25, 1.65, -2.55);
    g.add(wallSR);
    const crossH = new THREE.Mesh(new THREE.BoxGeometry(0.95, 0.30, 0.08), redMat);
    crossH.position.set(-2.1, 2.1, -2.7);
    g.add(crossH);
    const crossV = new THREE.Mesh(new THREE.BoxGeometry(0.30, 0.95, 0.08), redMat);
    crossV.position.set(-2.1, 2.1, -2.7);
    g.add(crossV);
    const crossTopH = new THREE.Mesh(new THREE.BoxGeometry(1.8, 0.08, 0.55), redMat);
    crossTopH.position.set(0, 3.36, 0);
    g.add(crossTopH);
    const crossTopV = new THREE.Mesh(new THREE.BoxGeometry(0.55, 0.08, 1.8), redMat);
    crossTopV.position.set(0, 3.36, 0);
    g.add(crossTopV);
    const medLight = new THREE.PointLight(0xe0f2fe, 1.5, 12);
    medLight.position.set(0, 2.5, 0);
    g.add(medLight);
    surfaceGroup.add(g);
  })();

  // 2. Mars EDL Lifting-Body Entry Aircraft (24.0, y, -11.0)
  (function buildWebEdlCraft() {
    const g = new THREE.Group();
    const ex = 24.0, ez = -11.0;
    g.position.set(ex, sampleHeight(ex, ez), ez);
    const shellMat = new THREE.MeshStandardMaterial({ color: 0xe2e8f0, roughness: 0.38, metalness: 0.45 });
    const heatMat = new THREE.MeshStandardMaterial({ color: 0x292524, roughness: 0.75 });
    const aeroMat = new THREE.MeshStandardMaterial({ color: 0xea580c, roughness: 0.45 });
    const dish = new THREE.Mesh(new THREE.CylinderGeometry(2.4, 2.4, 0.25, 24), heatMat);
    dish.position.y = 0.95;
    g.add(dish);
    const body = new THREE.Mesh(new THREE.CylinderGeometry(1.15, 1.15, 3.8, 18), shellMat);
    body.rotation.x = Math.PI / 2;
    body.position.y = 1.65;
    g.add(body);
    const wingL = new THREE.Mesh(new THREE.BoxGeometry(2.1, 0.12, 1.4), aeroMat);
    wingL.position.set(-1.85, 1.55, -0.4);
    wingL.rotation.z = -0.22;
    g.add(wingL);
    const wingR = new THREE.Mesh(new THREE.BoxGeometry(2.1, 0.12, 1.4), aeroMat);
    wingR.position.set(1.85, 1.55, -0.4);
    wingR.rotation.z = 0.22;
    g.add(wingR);
    const tail = new THREE.Mesh(new THREE.BoxGeometry(0.14, 1.4, 1.1), aeroMat);
    tail.position.set(0, 2.55, -1.35);
    tail.rotation.x = -0.35;
    g.add(tail);
    surfaceGroup.add(g);
  })();

  // 3. Mars Ascent Rocket & Launch Complex (26.5, y, 13.5)
  (function buildWebRocketPad() {
    const g = new THREE.Group();
    const rx = 26.5, rz = 13.5;
    g.position.set(rx, sampleHeight(rx, rz), rz);
    const padMat = new THREE.MeshStandardMaterial({ color: 0x475569, roughness: 0.7 });
    const hullMat = new THREE.MeshStandardMaterial({ color: 0xf8fafc, roughness: 0.35, metalness: 0.2 });
    const darkMat = new THREE.MeshStandardMaterial({ color: 0x1e293b, roughness: 0.5, metalness: 0.7 });
    const cryoBlue = new THREE.MeshStandardMaterial({ color: 0x0284c7, roughness: 0.3, metalness: 0.5 });
    const cryoAmber = new THREE.MeshStandardMaterial({ color: 0xd97706, roughness: 0.3, metalness: 0.5 });

    const apron = new THREE.Mesh(new THREE.BoxGeometry(9.6, 0.28, 9.6), padMat);
    apron.position.y = 0.14;
    g.add(apron);
    const stage1 = new THREE.Mesh(new THREE.CylinderGeometry(0.75, 0.75, 5.2, 20), hullMat);
    stage1.position.y = 3.0;
    g.add(stage1);
    const inter = new THREE.Mesh(new THREE.CylinderGeometry(0.72, 0.75, 0.75, 20), darkMat);
    inter.position.y = 5.95;
    g.add(inter);
    const stage2 = new THREE.Mesh(new THREE.CylinderGeometry(0.62, 0.72, 3.2, 20), hullMat);
    stage2.position.y = 7.9;
    g.add(stage2);
    const nose = new THREE.Mesh(new THREE.ConeGeometry(0.62, 1.75, 20), hullMat);
    nose.position.y = 10.38;
    g.add(nose);
    const tower = new THREE.Mesh(new THREE.BoxGeometry(1.1, 10.2, 1.1), darkMat);
    tower.position.set(-2.2, 5.2, 0);
    g.add(tower);
    const lox = new THREE.Mesh(new THREE.SphereGeometry(0.85, 16, 16), cryoBlue);
    lox.position.set(3.2, 1.1, -2.4);
    g.add(lox);
    const ch4 = new THREE.Mesh(new THREE.SphereGeometry(0.85, 16, 16), cryoAmber);
    ch4.position.set(3.2, 1.1, 2.4);
    g.add(ch4);
    surfaceGroup.add(g);
  })();

  // 4. 12 Stratified Jezero Rock Outcrops (Clickable NASA Science Beacons)
  const webOutcropEntries = [];
  if (nasaData && Array.isArray(nasaData.jezero_outcrops)) {
    const rockMatDark = new THREE.MeshStandardMaterial({ color: 0x5c3624, roughness: 0.92 });
    const rockMatSulfate = new THREE.MeshStandardMaterial({ color: 0x9b664b, roughness: 0.85 });
    const beaconMat = new THREE.MeshStandardMaterial({ color: 0x2cd4d9, emissive: 0x06b6d4, emissiveIntensity: 1.4 });
    nasaData.jezero_outcrops.forEach((o, idx) => {
      const ox = (Array.isArray(o.unity_pos) && o.unity_pos.length >= 2) ? Number(o.unity_pos[0]) : (idx - 5) * 4;
      const oz = (Array.isArray(o.unity_pos) && o.unity_pos.length >= 2) ? Number(o.unity_pos[1]) : 10;
      const sx = (Array.isArray(o.scale) && o.scale.length >= 3) ? Number(o.scale[0]) : 1.4;
      const sy = (Array.isArray(o.scale) && o.scale.length >= 3) ? Number(o.scale[1]) : 0.7;
      const sz = (Array.isArray(o.scale) && o.scale.length >= 3) ? Number(o.scale[2]) : 1.2;
      const oy = sampleHeight(ox, oz);

      const g = new THREE.Group();
      g.position.set(ox, oy, oz);
      const baseRock = new THREE.Mesh(
        new THREE.DodecahedronGeometry(0.75, 1),
        (o.style === 'layered_mudstone' || o.style === 'carbonate_rim') ? rockMatSulfate : rockMatDark
      );
      baseRock.scale.set(sx, sy, sz);
      baseRock.position.y = sy * 0.35;
      baseRock.castShadow = true;
      baseRock.userData.outcropIdx = idx;
      g.add(baseRock);

      const pin = new THREE.Mesh(new THREE.OctahedronGeometry(0.20, 0), beaconMat);
      pin.position.y = sy + 0.72;
      pin.userData.outcropIdx = idx;
      g.add(pin);

      clickableTargets.push(baseRock, pin);
      webOutcropEntries.push({ outcrop: o, x: ox, y: oy, z: oz, idx });
      surfaceGroup.add(g);
    });
  }

  // --- ORBIT MODE: MARS GLOBE + JEZERO PIN + SATELLITE (KEY 2) ---
  const globeRadius = 11.5;
  const globeGeo = new THREE.SphereGeometry(globeRadius, 128, 96);
  const gPos = globeGeo.attributes.position;
  const gCol = new Float32Array(gPos.count * 3);
  for (let i = 0; i < gPos.count; i++) {
    const vx = gPos.getX(i), vy = gPos.getY(i), vz = gPos.getZ(i);
    const lat = vy / globeRadius;
    const n = 0.75 + 0.25 * Math.sin(vx * 0.45) * Math.cos(vz * 0.45 + vy * 0.3);
    const darkBasalt = Math.sin(vx * 0.22 + vz * 0.35) > 0.45 ? 0.58 : 1.0;
    if (Math.abs(lat) > 0.92) {
      gCol[i * 3] = 0.92; gCol[i * 3 + 1] = 0.88; gCol[i * 3 + 2] = 0.84;
    } else {
      gCol[i * 3] = 0.58 * n * darkBasalt;
      gCol[i * 3 + 1] = 0.27 * n * darkBasalt;
      gCol[i * 3 + 2] = 0.12 * n * darkBasalt;
    }
  }
  globeGeo.setAttribute('color', new THREE.BufferAttribute(gCol, 3));
  const marsGlobe = new THREE.Mesh(
    globeGeo,
    new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.88, metalness: 0.04 })
  );
  marsGlobe.name = 'MS_Orbit_Mars_Globe';
  orbitGroup.add(marsGlobe);

  // Jezero Crater Pin at top crest (matching Blender Key 2)
  const pinCone = new THREE.Mesh(
    new THREE.ConeGeometry(0.32, 2.2, 16),
    new THREE.MeshBasicMaterial({ color: 0xbaf6ff })
  );
  pinCone.position.set(0, globeRadius + 1.1, 0);
  orbitGroup.add(pinCone);

  // ==============================================================================
  // FEATURE 17: REALISTIC CELESTIAL MOONS (PHOBOS & DEIMOS) IN ORBITGROUP
  // ==============================================================================
  const rPhobos = 19.2;
  const incPhobos = 1.08 * Math.PI / 180;
  const rDeimos = 29.8;
  const incDeimos = 1.79 * Math.PI / 180;

  const omegaMars = 0.0012;
  const omegaPhobos = omegaMars * 3.97; // 0.004764 rad/frame (prograde: rises West, sets East)
  const omegaDeimos = omegaMars * 0.81; // 0.000972 rad/frame (prograde: rises East, sets West)

  let phobosAngle = 0.45;
  let deimosAngle = 2.10;

  // 1. Phobos 3D Mesh (Triaxial Ellipsoid 27 x 22 x 18 km)
  const phobosGeo = new THREE.SphereGeometry(0.85, 20, 16);
  const pPos = phobosGeo.attributes.position;
  for (let i = 0; i < pPos.count; i++) {
    const vx = pPos.getX(i), vy = pPos.getY(i), vz = pPos.getZ(i);
    const sx = 1.25, sy = 1.0, sz = 0.82;
    const crater = Math.sin(vx * 4.0) * Math.cos(vz * 4.0) * 0.08;
    pPos.setXYZ(i, vx * sx * (1 + crater), vy * sy * (1 + crater), vz * sz * (1 + crater));
  }
  phobosGeo.computeVertexNormals();
  const phobosMat = new THREE.MeshStandardMaterial({
    color: 0x3d3835,
    roughness: 0.94,
    metalness: 0.04
  });
  const phobosMesh = new THREE.Mesh(phobosGeo, phobosMat);
  phobosMesh.castShadow = true;
  orbitGroup.add(phobosMesh);

  // Phobos Orbital Trajectory LineLoop
  const phobosOrbitPts = [];
  for (let i = 0; i <= 64; i++) {
    const a = (i / 64) * Math.PI * 2;
    phobosOrbitPts.push(new THREE.Vector3(
      rPhobos * Math.cos(a),
      rPhobos * Math.sin(a) * Math.sin(incPhobos),
      rPhobos * Math.sin(a) * Math.cos(incPhobos)
    ));
  }
  const phobosOrbitLine = new THREE.LineLoop(
    new THREE.BufferGeometry().setFromPoints(phobosOrbitPts),
    new THREE.LineBasicMaterial({ color: 0x93c5fd, transparent: true, opacity: 0.35 })
  );
  phobosOrbitLine.name = 'MS_Orbit_Line_Phobos';
  orbitGroup.add(phobosOrbitLine);

  // 2. Deimos 3D Mesh (15 x 12 x 10 km Smoother Regolith)
  const deimosGeo = new THREE.SphereGeometry(0.52, 16, 12);
  const dPos = deimosGeo.attributes.position;
  for (let i = 0; i < dPos.count; i++) {
    const vx = dPos.getX(i), vy = dPos.getY(i), vz = dPos.getZ(i);
    const sx = 1.15, sy = 0.92, sz = 0.78;
    const dust = Math.sin(vx * 2.5) * 0.04;
    dPos.setXYZ(i, vx * sx * (1 + dust), vy * sy * (1 + dust), vz * sz * (1 + dust));
  }
  deimosGeo.computeVertexNormals();
  const deimosMat = new THREE.MeshStandardMaterial({
    color: 0x4f4740,
    roughness: 0.88,
    metalness: 0.03
  });
  const deimosMesh = new THREE.Mesh(deimosGeo, deimosMat);
  deimosMesh.castShadow = true;
  orbitGroup.add(deimosMesh);

  // Deimos Orbital Trajectory LineLoop
  const deimosOrbitPts = [];
  for (let i = 0; i <= 64; i++) {
    const a = (i / 64) * Math.PI * 2;
    deimosOrbitPts.push(new THREE.Vector3(
      rDeimos * Math.cos(a),
      rDeimos * Math.sin(a) * Math.sin(incDeimos),
      rDeimos * Math.sin(a) * Math.cos(incDeimos)
    ));
  }
  const deimosOrbitLine = new THREE.LineLoop(
    new THREE.BufferGeometry().setFromPoints(deimosOrbitPts),
    new THREE.LineBasicMaterial({ color: 0xfde047, transparent: true, opacity: 0.30 })
  );
  deimosOrbitLine.name = 'MS_Orbit_Line_Deimos';
  orbitGroup.add(deimosOrbitLine);

  // ==============================================================================
  // FEATURE 19: FULL SATELLITE CONSTELLATION (MRO, ODYSSEY, MAVEN, GLOBAL SURVEYOR)
  // ==============================================================================
  const constellationGroup = new THREE.Group();
  orbitGroup.add(constellationGroup);

  // Helper: Calculate inclined Keplerian position
  function getKeplerianPosition(cfg, trueAnomaly) {
    const a = cfg.radius;
    const e = cfg.eccentricity;
    const r = (a * (1 - e * e)) / (1 + e * Math.cos(trueAnomaly));
    const xp = r * Math.cos(trueAnomaly);
    const zp = r * Math.sin(trueAnomaly);
    const v = new THREE.Vector3(xp, 0, zp);
    v.applyAxisAngle(new THREE.Vector3(1, 0, 0), cfg.inclination);
    v.applyAxisAngle(new THREE.Vector3(0, 1, 0), cfg.raan);
    return v;
  }

  // 1. MRO Model: Rectangular bus, High-gain dish pointing nadir, Dual solar panels
  function createMROModel() {
    const g = new THREE.Group();
    const bus = new THREE.Mesh(new THREE.BoxGeometry(0.7, 0.6, 0.9), new THREE.MeshStandardMaterial({ color: 0xd97706, metalness: 0.85, roughness: 0.25 }));
    g.add(bus);
    const dish = new THREE.Mesh(new THREE.ConeGeometry(0.55, 0.22, 16), new THREE.MeshStandardMaterial({ color: 0xf1f5f9, metalness: 0.4, roughness: 0.3 }));
    dish.rotation.x = -Math.PI / 2;
    dish.position.set(0, 0, -0.55); // Nadir pointing forward
    g.add(dish);
    const wings = new THREE.Mesh(new THREE.BoxGeometry(3.4, 0.08, 0.65), new THREE.MeshStandardMaterial({ color: 0x1d4ed8, metalness: 0.7, roughness: 0.2 }));
    g.add(wings);
    return g;
  }

  // 2. Odyssey Model: Asymmetric solar wing & THEMIS sensor mast
  function createOdysseyModel() {
    const g = new THREE.Group();
    const bus = new THREE.Mesh(new THREE.BoxGeometry(0.65, 0.55, 0.75), new THREE.MeshStandardMaterial({ color: 0xe2e8f0, metalness: 0.5, roughness: 0.35 }));
    g.add(bus);
    const wing = new THREE.Mesh(new THREE.BoxGeometry(2.2, 0.06, 0.55), new THREE.MeshStandardMaterial({ color: 0x2563eb, metalness: 0.7, roughness: 0.2 }));
    wing.position.set(1.4, 0, 0);
    g.add(wing);
    const mast = new THREE.Mesh(new THREE.CylinderGeometry(0.04, 0.04, 0.6, 8), new THREE.MeshStandardMaterial({ color: 0x475569 }));
    mast.rotation.x = Math.PI / 2;
    mast.position.set(0, -0.2, -0.45);
    g.add(mast);
    return g;
  }

  // 3. MAVEN Model: Gull-wing dihedral panels & magnetic boom
  function createMAVENModel() {
    const g = new THREE.Group();
    const bus = new THREE.Mesh(new THREE.BoxGeometry(0.6, 0.6, 0.6), new THREE.MeshStandardMaterial({ color: 0x94a3b8, metalness: 0.8, roughness: 0.2 }));
    g.add(bus);
    [-1, 1].forEach(side => {
      const wing = new THREE.Mesh(new THREE.BoxGeometry(1.4, 0.06, 0.5), new THREE.MeshStandardMaterial({ color: 0x1e40af, metalness: 0.7, roughness: 0.25 }));
      wing.position.set(side * 0.95, 0.15, 0);
      wing.rotation.z = side * 0.25; // dihedral angle
      g.add(wing);
    });
    const boom = new THREE.Mesh(new THREE.CylinderGeometry(0.03, 0.03, 1.2, 8), new THREE.MeshStandardMaterial({ color: 0x334155 }));
    boom.position.set(0, 0.45, 0.4);
    g.add(boom);
    return g;
  }

  // 4. MGS Model: Twin panels with aerobraking drag flaps & boom dish
  function createMGSModel() {
    const g = new THREE.Group();
    const bus = new THREE.Mesh(new THREE.BoxGeometry(0.6, 0.6, 0.8), new THREE.MeshStandardMaterial({ color: 0xf59e0b, metalness: 0.75, roughness: 0.3 }));
    g.add(bus);
    const wings = new THREE.Mesh(new THREE.BoxGeometry(3.6, 0.06, 0.6), new THREE.MeshStandardMaterial({ color: 0x1d4ed8, metalness: 0.7, roughness: 0.2 }));
    g.add(wings);
    const dish = new THREE.Mesh(new THREE.ConeGeometry(0.42, 0.18, 16), new THREE.MeshStandardMaterial({ color: 0xf8fafc }));
    dish.rotation.x = -Math.PI / 2;
    dish.position.set(0, 0, -0.5);
    g.add(dish);
    return g;
  }

  const CONSTELLATION_CONFIGS = [
    {
      id: 'mro',
      name: 'Mars Reconnaissance Orbiter (MRO)',
      radius: 13.6,
      inclination: 92.8 * Math.PI / 180,
      raan: 0.25,
      eccentricity: 0.01,
      speed: 0.0075,
      color: 0x06b6d4, // Cyan
      angle: 0.5,
      builder: createMROModel
    },
    {
      id: 'odyssey',
      name: 'Mars Odyssey',
      radius: 14.5,
      inclination: 93.1 * Math.PI / 180,
      raan: 1.85,
      eccentricity: 0.015,
      speed: 0.0068,
      color: 0xf59e0b, // Amber
      angle: 1.8,
      builder: createOdysseyModel
    },
    {
      id: 'maven',
      name: 'MAVEN',
      radius: 16.2,
      inclination: 75.0 * Math.PI / 180,
      raan: 3.40,
      eccentricity: 0.22, // Elliptical science orbit!
      speed: 0.0055,
      color: 0xec4899, // Magenta
      angle: 3.2,
      builder: createMAVENModel
    },
    {
      id: 'mgs',
      name: 'Mars Global Surveyor (MGS)',
      radius: 14.2,
      inclination: 92.8 * Math.PI / 180,
      raan: 4.95,
      eccentricity: 0.01,
      speed: 0.0071,
      color: 0x10b981, // Emerald
      angle: 4.6,
      builder: createMGSModel
    }
  ];

  // Instantiate Constellation Models & LineLoops
  const activeSatellites = CONSTELLATION_CONFIGS.map(cfg => {
    const mesh = cfg.builder();
    const initialPos = getKeplerianPosition(cfg, cfg.angle);
    mesh.position.copy(initialPos);
    mesh.lookAt(0, 0, 0); // Nadir pointing
    constellationGroup.add(mesh);

    // Orbit LineLoop
    const pts = [];
    for (let i = 0; i <= 96; i++) {
      const a = (i / 96) * Math.PI * 2;
      pts.push(getKeplerianPosition(cfg, a));
    }
    const orbitLine = new THREE.LineLoop(
      new THREE.BufferGeometry().setFromPoints(pts),
      new THREE.LineBasicMaterial({ color: cfg.color, transparent: true, opacity: 0.45 })
    );
    orbitLine.name = 'MS_Orbit_Line_' + cfg.name;
    constellationGroup.add(orbitLine);

    return { cfg: cfg, mesh: mesh, line: orbitLine };
  });

  // --- CAMERA ZONE SWITCHING (KEYS 1, 2, 3, 4, 6) ---
  function setZone(zone) {
    camState.mode = zone;
    document.querySelectorAll('.zone-btn').forEach(b => {
      b.classList.toggle('active', b.dataset.zone === zone);
    });

    if (zone === 'orbit') {
      surfaceGroup.visible = false;
      orbitGroup.visible = true;
      scene.background.setHex(0x040508);
      scene.fog.density = 0.0;
      starMat.opacity = 1.0;
      camState.desiredTarget.set(0, 0, 0);
      camState.desiredSpherical = { radius: 42, phi: 1.44, theta: 0.0 };
      applyTimeOfDay(camState.timeOfDay);
    } else {
      surfaceGroup.visible = true;
      orbitGroup.visible = false;
      applyTimeOfDay(camState.timeOfDay);

      if (zone === 'surface') {
        camState.desiredTarget.set(-1.0, 1.8, 14.0);
        camState.desiredSpherical = { radius: 29, phi: 1.40, theta: 3.14 };
      } else if (zone === 'agriculture') {
        camState.desiredTarget.set(-20.5, 1.8, 18.0);
        camState.desiredSpherical = { radius: 16, phi: 1.35, theta: 2.65 };
      } else if (zone === 'habitat') {
        camState.desiredTarget.set(7.5, 2.2, 18.0);
        camState.desiredSpherical = { radius: 20, phi: 1.38, theta: 3.35 };
      } else if (zone === 'tactical') {
        camState.desiredTarget.set(8.0, 0.0, 38.0);
        camState.desiredSpherical = { radius: 85, phi: 0.35, theta: 3.14 };
      }
    }
  }

  // --- MARTIAN DAY & NIGHT CYCLE (KEY 5) ---
  const TIME_CONFIGS = {
    SUNRISE: {
      sky: 0x8f3d1e, fog: 0.0028, sunCol: 0xff9b54, sunInt: 1.18, sunPos: [-65, 18, -20],
      stars: 0.18, label: 'SUNRISE (06:15 LMST)', skyDesc: 'Amber Horizon Scattering', icon: '🌅', slider: 15
    },
    DAY: {
      sky: 0x9c4522, fog: 0.0022, sunCol: 0xffcfa3, sunInt: 1.38, sunPos: [-45, 58, -30],
      stars: 0.0, label: 'DAYTIME (13:20 LMST)', skyDesc: 'Butterscotch Mie Dust', icon: '☀', slider: 38
    },
    SUNSET: {
      sky: 0x73321c, fog: 0.0030, sunCol: 0xf2854b, sunInt: 0.98, sunPos: [65, 14, 25],
      stars: 0.32, label: 'SUNSET (18:05 LMST)', skyDesc: 'Forward-Scattered Slate Glow', icon: '🌇', slider: 65
    },
    TWILIGHT: {
      sky: 0x24181c, fog: 0.0035, sunCol: 0x7c83bc, sunInt: 0.35, sunPos: [70, 4, 30],
      stars: 0.78, label: 'TWILIGHT (19:10 LMST)', skyDesc: 'High-Altitude Aerosol Glow', icon: '🌆', slider: 82
    },
    NIGHT: {
      sky: 0x05060a, fog: 0.0015, sunCol: 0x3b4c7a, sunInt: 0.12, sunPos: [0, -40, 0],
      stars: 1.0, label: 'MARTIAN NIGHT (23:45 LMST)', skyDesc: 'Clear Starfield & Milky Way', icon: '🌙', slider: 98
    }
  };

  let currentDustTau = 0.45;
  let lastSolarElevationDeg = 71.6;

  function computeBeerLambertFlux(tau, elevDeg) {
    const clampedElev = Math.max(5.0, Math.min(90.0, elevDeg));
    const cosZ = Math.max(0.087, Math.sin(clampedElev * Math.PI / 180.0));
    const trans = Math.exp(-Math.max(0.0, tau) / cosZ);
    return { trans: trans, fluxWm2: 586.2 * trans };
  }

  function updateDustAndEvaTelemetry() {
    const { trans, fluxWm2 } = computeBeerLambertFlux(currentDustTau, lastSolarElevationDeg);
    const stormLabel = currentDustTau < 0.75 ? 'Nominal' : (currentDustTau < 1.5 ? 'Regional Haze' : 'Dust Storm');
    const clearVisFactor = Math.max(0.35, Math.min(1.0, 1.0 - (currentDustTau - 0.45) * 0.18));
    const pnrRadiusM = Math.round(1800.0 * clearVisFactor * 0.94);

    const elTau = document.getElementById('readout-dust-tau');
    if (elTau) elTau.textContent = `${currentDustTau.toFixed(2)} (${stormLabel})`;
    const elFlux = document.getElementById('readout-solar-flux');
    if (elFlux) elFlux.textContent = `${Math.round(fluxWm2)} W/m² (${Math.round(trans * 100)}%)`;
    const elEva = document.getElementById('readout-eva-o2');
    if (elEva) elEva.textContent = `94% · ${pnrRadiusM.toLocaleString()} m`;

    const cfg = TIME_CONFIGS[camState.timeOfDay] || TIME_CONFIGS.DAY;
    sunLight.intensity = cfg.sunInt * (0.35 + 0.80 * trans);
    if (camState.mode !== 'orbit') {
      scene.fog.density = cfg.fog * (1.0 + (currentDustTau - 0.45) * 0.65);
    }
  }

  function applyTimeOfDay(mode) {
    camState.timeOfDay = mode;
    const cfg = TIME_CONFIGS[mode] || TIME_CONFIGS.DAY;

    document.querySelectorAll('.time-btn').forEach(b => {
      b.classList.toggle('active', b.dataset.time === mode);
    });
    document.getElementById('solar-phase-text').textContent = cfg.label;
    document.getElementById('solar-icon').textContent = cfg.icon;
    document.getElementById('readout-sky').textContent = cfg.skyDesc;
    document.getElementById('readout-stars').textContent = `${Math.round(cfg.stars * 100)}% (${mode})`;
    document.getElementById('solar-slider').value = cfg.slider;

    // Calculate Jezero Crater continuous diurnal solar vector (18.38°N, 77.58°E)
    const hourAngle = ((cfg.slider - 50) / 50) * Math.PI; // -pi at sunrise/night, 0 at noon
    const solarData = calculateJezeroSolarVector(hourAngle);
    const elevDeg = Math.round(solarData.elevation * 180 / Math.PI * 10) / 10;
    lastSolarElevationDeg = Math.max(5.0, elevDeg);
    const elevEl = document.getElementById('readout-solar-elev');
    if (elevEl) elevEl.textContent = `${elevDeg}° (${mode})`;

    if (camState.mode !== 'orbit') {
      scene.background.setHex(cfg.sky);
      scene.fog.color.setHex(cfg.sky);
      starMat.opacity = cfg.stars;

      // Position Sun disk far in sky dome
      sunGroup.position.copy(camera.position).addScaledVector(solarData.dir, 1400);
      sunGroup.visible = solarData.elevation > -0.12; // Visible above horizon & slight twilight
      sunCoreMesh.lookAt(camera.position);
    } else {
      // In Orbit mode, place sun disk at planetary illumination direction
      sunGroup.position.copy(solarData.dir).multiplyScalar(120);
      sunGroup.visible = true;
      sunCoreMesh.lookAt(0, 0, 0);
    }

    sunLight.color.setHex(cfg.sunCol);
    sunLight.position.copy(solarData.dir).multiplyScalar(70);
    updateDustAndEvaTelemetry();

    const nightBoost = (mode === 'NIGHT' || mode === 'TWILIGHT') ? 2.6 : 1.2;
    agriLight1.intensity = 1.8 * nightBoost;
    agriLight2.intensity = 1.6 * nightBoost;
    habLight.intensity = 1.4 * nightBoost;
  }

  // --- UI EVENT LISTENERS ---
  document.querySelectorAll('.zone-btn').forEach(btn => {
    btn.addEventListener('click', () => setZone(btn.dataset.zone));
  });

  document.querySelectorAll('.time-btn').forEach(btn => {
    btn.addEventListener('click', () => applyTimeOfDay(btn.dataset.time));
  });

  document.getElementById('solar-status-pill').addEventListener('click', () => {
    const order = ['DAY', 'SUNSET', 'TWILIGHT', 'NIGHT', 'SUNRISE'];
    const next = order[(order.indexOf(camState.timeOfDay) + 1) % order.length];
    applyTimeOfDay(next);
  });

  const celPill = document.getElementById('celestial-status-pill');
  if (celPill) {
    celPill.addEventListener('click', () => setZone(camState.mode === 'orbit' ? 'surface' : 'orbit'));
  }

  document.getElementById('solar-slider').addEventListener('input', e => {
    const v = Number(e.target.value);
    if (v < 22) applyTimeOfDay('SUNRISE');
    else if (v < 52) applyTimeOfDay('DAY');
    else if (v < 75) applyTimeOfDay('SUNSET');
    else if (v < 90) applyTimeOfDay('TWILIGHT');
    else applyTimeOfDay('NIGHT');
  });

  const ROUTE_STATS = {
    safest: { slope: '11.2%', wSlope: '88.8%', rough: '12.4%', wRough: '87.6%', prox: '10.8%', wProx: '89.2%' },
    shortest: { slope: '42.5%', wSlope: '57.5%', rough: '34.0%', wRough: '66.0%', prox: '29.5%', wProx: '70.5%' },
    science: { slope: '16.8%', wSlope: '83.2%', rough: '18.2%', wRough: '81.8%', prox: '15.4%', wProx: '84.6%' }
  };

  document.querySelectorAll('.route-card').forEach(card => {
    card.addEventListener('click', () => {
      const r = card.dataset.route;
      document.querySelectorAll('.route-card').forEach(c => c.classList.toggle('active', c === card));
      const st = ROUTE_STATS[r];
      if (st) {
        document.getElementById('val-slope').textContent = st.slope;
        document.getElementById('bar-slope').style.width = st.wSlope;
        document.getElementById('val-rough').textContent = st.rough;
        document.getElementById('bar-rough').style.width = st.wRough;
        document.getElementById('val-prox').textContent = st.prox;
        document.getElementById('bar-prox').style.width = st.wProx;
      }
      Object.entries(routeMeshes).forEach(([k, mesh]) => {
        mesh.scale.setScalar(k === r ? 1.45 : 0.85);
      });
    });
  });

  // --- NASA DATA HYDRATION (ROUTES, GEOLOGY, OVERLAYS, PROVENANCE) ---
  function hydrateNasaHudFromBundle() {
    if (!nasaData) return;

    const r = nasaData.routes || {};
    if (r.safest) {
      document.getElementById('route-safest-dist').textContent = `${r.safest.distance_m.toFixed(2)} m`;
      document.getElementById('route-safest-slope').textContent = `${r.safest.max_slope_deg.toFixed(2)}°`;
    }
    if (r.shortest) {
      document.getElementById('route-shortest-dist').textContent = `${r.shortest.distance_m.toFixed(2)} m`;
      document.getElementById('route-shortest-slope').textContent = `${r.shortest.max_slope_deg.toFixed(2)}°`;
    }
    if (r.science) {
      document.getElementById('route-science-dist').textContent = `${r.science.distance_m.toFixed(2)} m`;
      document.getElementById('route-science-slope').textContent = `${r.science.max_slope_deg.toFixed(2)}°`;
    }
    document.getElementById('route-science-tubes').textContent = `${(nasaData.sample_tubes || []).length} Tubes`;

    const pov = nasaData.perseverance_pov_imagery;
    if (pov) {
      document.getElementById('readout-pov-meta').textContent = `Sol ${pov.sol} · Az ${Number(pov.mast_az_deg).toFixed(1)}°`;
    }
    document.getElementById('readout-gcr-rad').textContent = '0.67 mSv/day GCR';

    const pTrav = nasaData.perseverance_traverse;
    if (pTrav) {
      document.getElementById('telemetry-overlay-summary').textContent =
        `${pTrav.raw_features} Drive Legs (${(pTrav.total_length_m / 1000).toFixed(2)} km) · ${pTrav.dropout_points_filtered} Altitude Dropouts Clamped`;
    }

    const cur = nasaData.curiosity_msl_summary;
    if (cur) {
      document.getElementById('curiosity-msl-summary').textContent =
        `Curiosity MSL (${cur.location}): ${cur.features} segments · ${cur.total_length_km} km`;
    }

    const geoSelect = document.getElementById('geology-unit-select');
    const units = nasaData.geology_units || [];
    if (geoSelect && units.length > 0) {
      geoSelect.innerHTML = '';
      units.forEach((u, idx) => {
        const opt = document.createElement('option');
        opt.value = String(idx);
        opt.textContent = `${u.symbol} — ${u.name}`;
        geoSelect.appendChild(opt);
      });
      const renderUnit = (idx) => {
        const u = units[idx] || units[0];
        document.getElementById('geo-unit-swatch').style.background = u.hex || '#C47A52';
        document.getElementById('geo-unit-epoch').textContent = u.epoch || u.system || 'Mars Geologic Unit';
        document.getElementById('geo-unit-traffic').textContent = `Trafficability: ${u.trafficability}/5`;
        document.getElementById('geo-unit-ice').textContent = `H₂O: ${u.water_ice_potential}`;
        document.getElementById('geo-unit-desc').textContent = u.trafficability_desc || u.description || '';
      };
      geoSelect.addEventListener('change', () => renderUnit(Number(geoSelect.value) || 0));
      renderUnit(0);
    }

    const liveManifestEl = document.getElementById('modal-live-manifest');
    if (liveManifestEl) {
      const resourcesHtml = (nasaData.official_resources || []).map(res =>
        `<tr><td>${res.name || res.id}</td><td><span class="tag-measured">OFFICIAL NASA RESOURCE</span></td><td>${res.portal_url || res.landing_site || res.page_url || res.geojson_endpoint || ''}</td></tr>`
      ).join('');
      const hashesHtml = Object.entries(nasaData.sha256_integrity || {}).map(([fn, sha]) =>
        `<tr><td><code>${fn}</code></td><td><span class="tag-derived">SHA-256 VERIFIED</span></td><td><code>${sha.slice(0, 20)}...${sha.slice(-10)}</code></td></tr>`
      ).join('');
      liveManifestEl.innerHTML = `<table class="prov-table"><thead><tr><th>Verified Dataset / API</th><th>Trust Tier</th><th>Digest / Endpoint</th></tr></thead><tbody>${resourcesHtml}${hashesHtml}</tbody></table>`;
    }

    // Hydrate Jezero Outcrop Inspector + MEDA / InSight / RAD + Mount Sharp
    const outSel = document.getElementById('outcrop-select');
    const outcrops = nasaData.jezero_outcrops || [];
    const renderOutcropCard = (idx, focusCamera) => {
      const o = outcrops[idx] || outcrops[0];
      if (!o) return;
      if (outSel) outSel.value = String(idx);
      const imgEl = document.getElementById('outcrop-preview-img');
      if (imgEl) {
        const fn = o.image_file || 'perseverance_sample_raw.jpg';
        imgEl.src = `assets/nasa_imagery/${fn}`;
      }
      const setT = (id, val) => { const el = document.getElementById(id); if (el) el.textContent = val; };
      setT('outcrop-title', o.name || 'Jezero Outcrop');
      setT('outcrop-formation', `${o.formation || ''} · Sol ${o.sol ?? 0}`);
      setT('outcrop-coords', `${Number(o.lat || 0).toFixed(4)}°N, ${Number(o.lon || 0).toFixed(4)}°E · ${o.elev_m || 0} m · ${o.usgs_unit || 'Mars'}`);
      setT('outcrop-chemistry', `${o.crism_mineralogy || ''} | ${o.geochemistry_wt_pct || ''} — ${o.discovery_summary || ''}`);
      const lnk = document.getElementById('outcrop-nasa-link');
      if (lnk && o.source_url) lnk.href = o.source_url;
      if (focusCamera && webOutcropEntries[idx]) {
        const ent = webOutcropEntries[idx];
        setZone('surface');
        if (window.__MarsRealism && window.__MarsRealism.unlockCamera) window.__MarsRealism.unlockCamera();
        camState.desiredTarget.set(ent.x, ent.y + 0.8, ent.z);
        camState.desiredSpherical = { radius: 8.5, phi: 1.18, theta: 2.5 };
      }
    };
    window.__selectWebOutcrop = renderOutcropCard;
    if (outSel && outcrops.length > 0) {
      outSel.innerHTML = '';
      outcrops.forEach((o, i) => {
        const opt = document.createElement('option');
        opt.value = String(i);
        opt.textContent = `Sol ${o.sol ?? 0} — ${o.name}`;
        outSel.appendChild(opt);
      });
      outSel.addEventListener('change', () => renderOutcropCard(Number(outSel.value) || 0, true));
      renderOutcropCard(Math.min(7, outcrops.length - 1), false);
    }

    const env = nasaData.environmental_science || {};
    const atm = env.atmospheric_conditions || {};
    const rad = env.radiation_environment || {};
    const medaEl = document.getElementById('env-meda-readout');
    if (medaEl && atm.surface_pressure_pa) {
      const pMean = atm.surface_pressure_pa.annual_mean ?? atm.surface_pressure_pa.annual_mean_pa ?? 715;
      const tMean = atm.air_temperature_c ? (atm.air_temperature_c.diurnal_mean ?? atm.air_temperature_c.diurnal_mean_c ?? -56) : -56;
      const wMean = atm.wind_speed_ms ? (atm.wind_speed_ms.typical_day ?? 4.8) : (atm.wind_ms ? atm.wind_ms.typical_daytime_ms : 4.8);
      medaEl.textContent = `MEDA: ${pMean} Pa · ${tMean}°C mean · Wind ${wMean} m/s`;
    }
    const radEl = document.getElementById('env-rad-readout');
    if (radEl && (rad.gcr_surface_dose_msv_per_day ?? rad.gcr_surface_dose_msv_day)) {
      const gcrDose = rad.gcr_surface_dose_msv_per_day ?? rad.gcr_surface_dose_msv_day ?? 0.67;
      const limit = rad.nasa_career_limit_msv ?? rad.nasa_astronaut_career_limit_msv ?? 600;
      radEl.textContent = `RAD GCR: ${gcrDose} mSv/day · NASA Career Limit ${limit} mSv`;
    }
    const msl = nasaData.curiosity_mount_sharp || {};
    const mslEl = document.getElementById('msl-waypoint-readout');
    if (mslEl && Array.isArray(msl.waypoints)) {
      mslEl.textContent = `Curiosity Mount Sharp: ${msl.waypoints.length} stratigraphic stops (Bradbury -> Gediz Vallis)`;
    }
  }
  hydrateNasaHudFromBundle();

  const facilityChips = document.querySelectorAll('.facility-chip');
  facilityChips.forEach(chip => {
    chip.addEventListener('click', () => {
      const cfg = facilityFocusMap[chip.dataset.fly];
      if (!cfg) return;
      facilityChips.forEach(c => c.classList.toggle('active', c === chip));
      setZone('surface');
      if (window.__MarsRealism && window.__MarsRealism.unlockCamera) window.__MarsRealism.unlockCamera();
      camState.desiredTarget.copy(cfg.target);
      camState.desiredSpherical = { radius: cfg.radius, phi: cfg.phi, theta: cfg.theta };
    });
  });

  // Procedural WebAudio Mars Wind & Discovery Chime Toggle
  let webAudioCtx = null;
  let webAudioGain = null;
  let webAudioOn = false;
  const audioBtn = document.getElementById('btn-web-audio');
  function playScanChime() {
    if (!webAudioOn || !webAudioCtx) return;
    const osc = webAudioCtx.createOscillator();
    const g = webAudioCtx.createGain();
    osc.type = 'sine';
    osc.frequency.setValueAtTime(660, webAudioCtx.currentTime);
    osc.frequency.exponentialRampToValueAtTime(990, webAudioCtx.currentTime + 0.14);
    g.gain.setValueAtTime(0.08, webAudioCtx.currentTime);
    g.gain.exponentialRampToValueAtTime(0.001, webAudioCtx.currentTime + 0.22);
    osc.connect(g).connect(webAudioCtx.destination);
    osc.start();
    osc.stop(webAudioCtx.currentTime + 0.24);
  }
  if (audioBtn) {
    audioBtn.addEventListener('click', () => {
      if (!webAudioCtx) {
        const AC = window.AudioContext || window.webkitAudioContext;
        if (AC) {
          webAudioCtx = new AC();
          const bufLen = webAudioCtx.sampleRate * 2;
          const buf = webAudioCtx.createBuffer(1, bufLen, webAudioCtx.sampleRate);
          const ch = buf.getChannelData(0);
          let lp = 0;
          for (let i = 0; i < bufLen; i++) {
            lp = lp * 0.97 + (Math.random() * 2 - 1) * 0.03;
            ch[i] = lp * 1.4;
          }
          const src = webAudioCtx.createBufferSource();
          src.buffer = buf;
          src.loop = true;
          webAudioGain = webAudioCtx.createGain();
          webAudioGain.gain.value = 0.0;
          src.connect(webAudioGain).connect(webAudioCtx.destination);
          src.start();
        }
      }
      webAudioOn = !webAudioOn;
      if (webAudioCtx && webAudioCtx.state === 'suspended') webAudioCtx.resume();
      if (webAudioGain) webAudioGain.gain.value = webAudioOn ? 0.18 : 0.0;
      audioBtn.classList.toggle('active', webAudioOn);
      audioBtn.textContent = webAudioOn ? 'Audio: ON' : 'Audio: OFF';
      if (webAudioOn) playScanChime();
    });
  }

  document.getElementById('dust-tau-slider').addEventListener('input', e => {
    currentDustTau = Math.max(0.30, Math.min(3.50, Number(e.target.value) / 100.0));
    updateDustAndEvaTelemetry();
  });

  document.getElementById('chk-sample-tubes').addEventListener('change', e => {
    tubesOverlayGroup.visible = e.target.checked;
  });
  document.getElementById('chk-ingenuity-flights').addEventListener('change', e => {
    ingenuityOverlayGroup.visible = e.target.checked;
  });
  document.getElementById('chk-persev-traverse').addEventListener('change', e => {
    persevHistoryGroup.visible = e.target.checked;
  });

  // Modal & Gallery Inspection
  const modalBackdrop = document.getElementById('modal-backdrop');
  const modalTitle = document.getElementById('modal-title');
  const modalImg = document.getElementById('modal-preview-img');
  const modalProv = document.getElementById('modal-provenance-content');

  document.getElementById('btn-provenance').addEventListener('click', () => {
    modalTitle.textContent = 'NASA Scientific Data Provenance & Integrity';
    modalImg.classList.add('hidden');
    modalProv.classList.remove('hidden');
    modalBackdrop.classList.remove('hidden');
  });

  const mastcamBtn = document.getElementById('btn-mastcam-pov');
  const mastcamThumb = document.getElementById('mastcam-pov-thumb');
  const povInfo = window.MARS_NASA_DATA && window.MARS_NASA_DATA.perseverance_pov_imagery;
  const rawPovSrc = (povInfo && povInfo.local_src) || 'assets/nasa_imagery/perseverance_sample_raw.jpg';
  const povSrc = rawPovSrc.replace('../data/imagery/', 'assets/nasa_imagery/');
  if (mastcamBtn && mastcamThumb) {
    mastcamThumb.src = povSrc;
    mastcamBtn.dataset.povImg = povSrc;
  }

  document.querySelectorAll('.thumb-btn, .pov-thumb-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      modalTitle.textContent = btn.dataset.caption;
      modalImg.src = btn.dataset.img || btn.dataset.povImg;
      modalImg.classList.remove('hidden');
      modalProv.classList.add('hidden');
      modalBackdrop.classList.remove('hidden');
    });
  });

  document.getElementById('modal-close').addEventListener('click', () => modalBackdrop.classList.add('hidden'));
  modalBackdrop.addEventListener('click', e => {
    if (e.target === modalBackdrop) modalBackdrop.classList.add('hidden');
  });

  // Keyboard Shortcuts (Keys 1-6, Escape, plus WASD/Arrow Surface Pan)
  window.addEventListener('keydown', e => {
    if (e.key === '1') setZone('surface');
    else if (e.key === '2') setZone('orbit');
    else if (e.key === '3') setZone('agriculture');
    else if (e.key === '4') setZone('habitat');
    else if (e.key === '6') setZone('tactical');
    else if (e.key === '5') {
      const order = ['DAY', 'SUNSET', 'TWILIGHT', 'NIGHT', 'SUNRISE'];
      const next = order[(order.indexOf(camState.timeOfDay) + 1) % order.length];
      applyTimeOfDay(next);
    } else if (e.key === 'Escape') {
      modalBackdrop.classList.add('hidden');
    } else if (camState.mode !== 'orbit') {
      if (e.target && (e.target.tagName === 'INPUT' || e.target.tagName === 'SELECT' || e.target.tagName === 'TEXTAREA')) return;
      const kl = e.key.toLowerCase();
      const step = 1.6;
      const th = camState.spherical.theta;
      const fwdX = -Math.sin(th), fwdZ = -Math.cos(th);
      const rightX = Math.cos(th), rightZ = -Math.sin(th);
      let moved = false;
      if (kl === 'w' || e.key === 'ArrowUp') { camState.desiredTarget.x += fwdX * step; camState.desiredTarget.z += fwdZ * step; moved = true; }
      else if (kl === 's' || e.key === 'ArrowDown') { camState.desiredTarget.x -= fwdX * step; camState.desiredTarget.z -= fwdZ * step; moved = true; }
      else if (kl === 'a' || e.key === 'ArrowLeft') { camState.desiredTarget.x -= rightX * step; camState.desiredTarget.z -= rightZ * step; moved = true; }
      else if (kl === 'd' || e.key === 'ArrowRight') { camState.desiredTarget.x += rightX * step; camState.desiredTarget.z += rightZ * step; moved = true; }
      if (moved) {
        camState.desiredTarget.x = Math.max(-155, Math.min(155, camState.desiredTarget.x));
        camState.desiredTarget.z = Math.max(-155, Math.min(155, camState.desiredTarget.z));
        camState.desiredTarget.y = Math.max(1.2, sampleHeight(camState.desiredTarget.x, camState.desiredTarget.z) + 1.8);
        if (window.__MarsRealism && window.__MarsRealism.unlockCamera) window.__MarsRealism.unlockCamera();
      }
    }
  });

  // Pointer Orbit, Zoom & 3D Rock Outcrop Click Raycast
  const webRaycaster = new THREE.Raycaster();
  const pointerNdc = new THREE.Vector2();
  let isDragging = false;
  let prevX = 0, prevY = 0;
  let downX = 0, downY = 0;
  canvas.addEventListener('pointerdown', e => {
    isDragging = true;
    prevX = downX = e.clientX;
    prevY = downY = e.clientY;
  });
  window.addEventListener('pointerup', e => {
    const moved = Math.hypot(e.clientX - downX, e.clientY - downY);
    if (isDragging && moved < 6 && e.target === canvas && camState.mode !== 'orbit' && clickableTargets.length > 0) {
      const rect = canvas.getBoundingClientRect();
      pointerNdc.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
      pointerNdc.y = -((e.clientY - rect.top) / rect.height) * 2 + 1;
      webRaycaster.setFromCamera(pointerNdc, camera);
      const hits = webRaycaster.intersectObjects(clickableTargets, false);
      if (hits.length > 0 && typeof hits[0].object.userData.outcropIdx === 'number') {
        if (typeof window.__selectWebOutcrop === 'function') {
          window.__selectWebOutcrop(hits[0].object.userData.outcropIdx, false);
        }
        playScanChime();
      }
    }
    isDragging = false;
  });
  window.addEventListener('pointermove', e => {
    if (!isDragging) return;
    const dx = e.clientX - prevX;
    const dy = e.clientY - prevY;
    prevX = e.clientX;
    prevY = e.clientY;
    camState.desiredSpherical.theta -= dx * 0.0055;
    camState.desiredSpherical.phi = Math.max(0.2, Math.min(1.52, camState.desiredSpherical.phi - dy * 0.0045));
  });
  canvas.addEventListener('wheel', e => {
    e.preventDefault();
    camState.desiredSpherical.radius = Math.max(6, Math.min(180, camState.desiredSpherical.radius + e.deltaY * 0.03));
  }, { passive: false });

  window.addEventListener('resize', () => {
    camera.aspect = window.innerWidth / window.innerHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(window.innerWidth, window.innerHeight);
  });

  // Initialize View
  setZone('surface');

  // Allow realism framing layer to keep camState synchronized so first WASD/drag never jumps
  window.__syncAppCameraTo = function (pos, look) {
    if (!pos || !look) return;
    camState.target.copy(look);
    camState.desiredTarget.copy(look);
    const offX = pos.x - look.x;
    const offY = pos.y - look.y;
    const offZ = pos.z - look.z;
    const r = Math.max(6, Math.min(180, Math.hypot(offX, offY, offZ)));
    const phi = Math.max(0.2, Math.min(1.52, Math.acos(Math.max(-1, Math.min(1, offY / r)))));
    const theta = Math.atan2(offX, offZ);
    camState.spherical.radius = camState.desiredSpherical.radius = r;
    camState.spherical.phi = camState.desiredSpherical.phi = phi;
    camState.spherical.theta = camState.desiredSpherical.theta = theta;
  };

  // Continuous Clock for Delta Time & Multi-Component Animation Loops
  const clock = new THREE.Clock();

  // Render Loop
  function animate() {
    requestAnimationFrame(animate);

    const delta = Math.min(clock.getDelta(), 0.08);
    const elapsedTime = clock.getElapsedTime();
    const animScale = delta * 60; // Normalized 60fps factor

    // 1. Smooth Critically Damped Camera Transitions (zero snapping)
    const dampFactor = 1.0 - Math.exp(-5.5 * delta);
    camState.target.lerp(camState.desiredTarget, dampFactor);
    camState.spherical.radius += (camState.desiredSpherical.radius - camState.spherical.radius) * dampFactor;
    camState.spherical.phi += (camState.desiredSpherical.phi - camState.spherical.phi) * dampFactor;

    // Shortest-path angular damping for theta (avoids full circle spin-around)
    let dTheta = (camState.desiredSpherical.theta - camState.spherical.theta) % (Math.PI * 2);
    if (dTheta > Math.PI) dTheta -= Math.PI * 2;
    if (dTheta < -Math.PI) dTheta += Math.PI * 2;
    camState.spherical.theta += dTheta * dampFactor;

    const r = camState.spherical.radius;
    const phi = camState.spherical.phi;
    const theta = camState.spherical.theta;

    camera.position.x = camState.target.x + r * Math.sin(phi) * Math.sin(theta);
    camera.position.y = camState.target.y + r * Math.cos(phi);
    camera.position.z = camState.target.z + r * Math.sin(phi) * Math.cos(theta);
    camera.lookAt(camState.target);

    // 2. CELESTIAL & ORBITAL CONTINUOUS ANIMATIONS (Always advancing in Sol time)
    // Mars Globe continuous axial rotation
    marsGlobe.rotation.y = (marsGlobe.rotation.y + omegaMars * animScale) % (Math.PI * 2);

    // Phobos prograde orbit (3.97x Mars rotation)
    phobosAngle = (phobosAngle + omegaPhobos * animScale) % (Math.PI * 2);
    phobosMesh.position.set(
      rPhobos * Math.cos(phobosAngle),
      rPhobos * Math.sin(phobosAngle) * Math.sin(incPhobos),
      rPhobos * Math.sin(phobosAngle) * Math.cos(incPhobos)
    );
    phobosMesh.rotation.y = (phobosMesh.rotation.y + omegaPhobos * animScale) % (Math.PI * 2);

    // Deimos prograde orbit (0.81x Mars rotation)
    deimosAngle = (deimosAngle + omegaDeimos * animScale) % (Math.PI * 2);
    deimosMesh.position.set(
      rDeimos * Math.cos(deimosAngle),
      rDeimos * Math.sin(deimosAngle) * Math.sin(incDeimos),
      rDeimos * Math.sin(deimosAngle) * Math.cos(incDeimos)
    );
    deimosMesh.rotation.y = (deimosMesh.rotation.y + omegaDeimos * animScale) % (Math.PI * 2);

    // Satellite Constellation Keplerian propagation along elliptical orbits & nadir tracking
    activeSatellites.forEach(sat => {
      sat.cfg.angle = (sat.cfg.angle + sat.cfg.speed * animScale) % (Math.PI * 2);
      const pos = getKeplerianPosition(sat.cfg, sat.cfg.angle);
      sat.mesh.position.copy(pos);
      sat.mesh.lookAt(0, 0, 0); // Nadir pointing towards Mars center
    });

    // 3. SURFACE CONTINUOUS ANIMATION LOOPS (When surfaceGroup is active)
    if (surfaceGroup.visible) {
      // (a) Ingenuity Counter-Rotating Blades (~2400 RPM visual equivalent)
      const rotorSpeed = 0.52 * animScale;
      ingenuity.upperRotor.rotation.y += rotorSpeed;
      ingenuity.lowerRotor.rotation.y -= rotorSpeed; // Counter-rotating blades

      // Subtle flight readiness / hover oscillation (or v10 Drone Fleet flight patrol)
      const droneOvr = window.__MarsDroneOverride;
      if (droneOvr && droneOvr.deployed) {
        const dp = droneOvr.phase = (droneOvr.phase || 0) + delta * 0.55;
        const fx = 13.8 + Math.cos(dp) * 14.0;
        const fz = 6.2 + Math.sin(dp * 1.3) * 11.5;
        ingenuity.group.position.set(fx, sampleHeight(fx, fz) + 5.8 + Math.sin(dp * 2.5) * 0.45, fz);
        ingenuity.group.rotation.y = -dp;
      } else {
        ingenuity.group.position.set(13.8, ingenuity.baseY + 0.08 + Math.sin(elapsedTime * 2.8) * 0.04, 6.2);
        ingenuity.group.rotation.z = Math.sin(elapsedTime * 3.2) * 0.012;
        ingenuity.group.rotation.x = Math.cos(elapsedTime * 2.6) * 0.012;
      }

      // (b) Perseverance Rover Exploration Movement (Auto-Patrol, Manual v10 Drive, or A* Route Autopilot)
      rHead.rotation.y = Math.sin(elapsedTime * 0.45) * 0.40;
      rHead.rotation.x = Math.sin(elapsedTime * 0.28) * 0.08;

      const rOvr = window.__MarsRoverOverride;
      if (rOvr && (rOvr.mode === 'manual' || rOvr.mode === 'autopilot')) {
        if (rOvr.mode === 'manual' && rOvr.dir) {
          const spd = 4.2 * delta;
          let moveX = 0;
          let moveZ = 0;
          if (rOvr.dir === 'forward') moveZ = -spd;
          else if (rOvr.dir === 'backward') moveZ = spd;
          else if (rOvr.dir === 'left') moveX = -spd;
          else if (rOvr.dir === 'right') moveX = spd;
          roverGroup.position.x = Math.max(-140, Math.min(140, roverGroup.position.x + moveX));
          roverGroup.position.z = Math.max(-140, Math.min(140, roverGroup.position.z + moveZ));
          roverGroup.position.y = sampleHeight(roverGroup.position.x, roverGroup.position.z);
          if (moveX !== 0 || moveZ !== 0) {
            roverGroup.rotation.y = Math.atan2(-moveX, -moveZ);
          }
          roverWheels.forEach(w => { w.rotation.x += spd * 0.9; });
        } else if (rOvr.mode === 'autopilot' && Array.isArray(rOvr.waypoints) && rOvr.waypoints.length >= 2) {
          rOvr.progress = Math.min(1.0, (rOvr.progress || 0) + delta * 0.045);
          const idxF = rOvr.progress * (rOvr.waypoints.length - 1);
          const i0 = Math.min(rOvr.waypoints.length - 2, Math.floor(idxF));
          const frac = idxF - i0;
          const p0 = rOvr.waypoints[i0];
          const p1 = rOvr.waypoints[i0 + 1];
          const nx = p0[0] + (p1[0] - p0[0]) * frac;
          const nz = p0[1] + (p1[1] - p0[1]) * frac;
          const dx = nx - roverGroup.position.x;
          const dz = nz - roverGroup.position.z;
          roverGroup.position.set(nx, sampleHeight(nx, nz), nz);
          if (Math.hypot(dx, dz) > 0.001) roverGroup.rotation.y = Math.atan2(dx, dz);
          roverWheels.forEach(w => { w.rotation.x += delta * 2.4; });
          if (rOvr.progress >= 0.999 && typeof rOvr.onArrived === 'function') {
            const cb = rOvr.onArrived;
            rOvr.onArrived = null;
            rOvr.mode = 'standby';
            cb();
          }
        }
      } else if (!rOvr || rOvr.mode !== 'standby') {
        const roverDisp = Math.sin(elapsedTime * 0.22) * 0.55;
        const roverSpeed = Math.cos(elapsedTime * 0.22) * 0.22 * 0.55;
        roverGroup.position.z = rBaseZ + roverDisp;
        roverGroup.position.y = sampleHeight(rBaseX, roverGroup.position.z);
        roverGroup.rotation.x = Math.sin(elapsedTime * 0.22) * 0.025;
        roverWheels.forEach(w => { w.rotation.x += roverSpeed * 0.45; });
      }
    }

    // 4. Dynamic Sun billboard facing camera
    if (sunGroup.visible) {
      sunCoronaSprite.lookAt(camera.position);
      sunCoreMesh.lookAt(camera.position);
    }

    renderer.render(scene, camera);
  }

  window.__MARS_WEB_BRIDGE.api = {
    scene: scene,
    camera: camera,
    renderer: renderer,
    camState: camState,
    setZone: setZone,
    applyTimeOfDay: applyTimeOfDay,
    sampleHeight: sampleHeight,
    terrainMesh: terrainMesh,
    surfaceGroup: surfaceGroup,
    orbitGroup: orbitGroup,
    sunGroup: sunGroup,
    sunLight: sunLight,
    starField: starField,
    routeMeshes: routeMeshes,
    routesPts: { safest: safestPts, shortest: shortestPts, science: sciencePts },
    roverGroup: roverGroup,
    habGroup: habGroup,
    agriGroup: agriGroup,
    astronautGroups: astronautGroups,
    ingenuity: ingenuity,
    webOutcropEntries: webOutcropEntries,
    clickableTargets: clickableTargets,
    phobosMesh: phobosMesh,
    deimosMesh: deimosMesh,
    activeSatellites: activeSatellites,
    playScanChime: playScanChime,
    tubesOverlayGroup: tubesOverlayGroup,
    ingenuityOverlayGroup: ingenuityOverlayGroup,
    persevHistoryGroup: persevHistoryGroup
  };

  animate();
})();

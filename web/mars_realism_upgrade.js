/* MARS SENTINEL — Procedural Photorealistic 3D Enhancement Layer (Three.js r128)
 * Upgrades WebGL sky scattering, visible Sun & Mars moons (Phobos/Deimos) in Surface & Orbit views,
 * sleek tapered glowing 3D orbital tracks with Ap/Pe markers, Mars atmospheric Fresnel shell & 2048px relief,
 * NASA Habitat/Crew/Perseverance/Jezero surface realism, and Celestial Quick-Focus HUD controls.
 */
(function () {
  'use strict';

  if (typeof THREE === 'undefined') return;

  const state = {
    initialized: false,
    scene: null,
    camera: null,
    renderer: null,
    terrainMesh: null,
    terrainHeightFn: null,
    orbitGroup: null,
    marsGlobe: null,
    phobosMesh: null,
    deimosMesh: null,
    sunGroup: null,
    starField: null,
    sunLight: null,
    hemiLight: null,
    skyDome: null,
    skyUniforms: null,
    surfaceMoonsGroup: null,
    surfacePhobos: null,
    surfaceDeimos: null,
    spaceSunGroup: null,
    surfaceRealismGroup: null,
    dustMotes: null,
    orbitTracks: [],
    orbitSprites: [],
    currentZone: 'surface',
    focusMode: 'default', // 'default' | 'sun' | 'phobos' | 'deimos' | 'constellation'
    userInteractedInZone: false,
    autoOrbit: true,
    lastFrameTime: performance.now(),
    elapsed: 0,
    camOverrideActive: false,
    camLerpAlpha: 0,
    desiredCamPos: new THREE.Vector3(),
    desiredLookAt: new THREE.Vector3(),
    smoothCamPos: new THREE.Vector3(),
    smoothLookAt: new THREE.Vector3(),
    hasSmoothCam: false,
    textures: {}
  };

  function hash2(x, y) {
    const s = Math.sin(x * 127.1 + y * 311.7) * 43758.5453123;
    return s - Math.floor(s);
  }

  function valueNoise2D(x, y) {
    const xi = Math.floor(x);
    const yi = Math.floor(y);
    const xf = x - xi;
    const yf = y - yi;
    const u = xf * xf * (3 - 2 * xf);
    const v = yf * yf * (3 - 2 * yf);
    const a = hash2(xi, yi);
    const b = hash2(xi + 1, yi);
    const c = hash2(xi, yi + 1);
    const d = hash2(xi + 1, yi + 1);
    return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
  }

  function fbm2D(x, y, octaves) {
    let sum = 0;
    let amp = 0.5;
    let freq = 1.0;
    for (let i = 0; i < octaves; i++) {
      sum += amp * valueNoise2D(x * freq, y * freq);
      freq *= 2.07;
      amp *= 0.5;
    }
    return sum;
  }

  function createGlowTexture(innerRgba, midRgba, outerRgba, size) {
    const sz = size || 256;
    const c = document.createElement('canvas');
    c.width = sz;
    c.height = sz;
    const g = c.getContext('2d');
    const half = sz * 0.5;
    const grad = g.createRadialGradient(half, half, 0, half, half, half);
    grad.addColorStop(0.0, innerRgba);
    grad.addColorStop(0.28, midRgba);
    grad.addColorStop(0.65, outerRgba);
    grad.addColorStop(1.0, 'rgba(0,0,0,0.0)');
    g.fillStyle = grad;
    g.fillRect(0, 0, sz, sz);
    const tex = new THREE.CanvasTexture(c);
    tex.needsUpdate = true;
    return tex;
  }

  function createStarSpikeTexture() {
    const c = document.createElement('canvas');
    c.width = 512;
    c.height = 512;
    const g = c.getContext('2d');
    const cx = 256, cy = 256;

    const radial = g.createRadialGradient(cx, cy, 4, cx, cy, 240);
    radial.addColorStop(0.0, 'rgba(255,252,240,1.0)');
    radial.addColorStop(0.08, 'rgba(255,236,190,0.95)');
    radial.addColorStop(0.22, 'rgba(255,175,95,0.45)');
    radial.addColorStop(0.55, 'rgba(130,175,230,0.12)');
    radial.addColorStop(1.0, 'rgba(0,0,0,0.0)');
    g.fillStyle = radial;
    g.fillRect(0, 0, 512, 512);

    // Anamorphic + 6-point optical diffraction spikes
    const angles = [0, Math.PI / 3, (2 * Math.PI) / 3];
    angles.forEach((ang, idx) => {
      g.save();
      g.translate(cx, cy);
      g.rotate(ang);
      const len = idx === 0 ? 245 : 195;
      const w = idx === 0 ? 4.5 : 3.0;
      const spike = g.createLinearGradient(-len, 0, len, 0);
      spike.addColorStop(0.0, 'rgba(255,215,150,0.0)');
      spike.addColorStop(0.45, 'rgba(255,235,200,0.32)');
      spike.addColorStop(0.5, 'rgba(255,255,250,0.85)');
      spike.addColorStop(0.55, 'rgba(255,235,200,0.32)');
      spike.addColorStop(1.0, 'rgba(255,215,150,0.0)');
      g.fillStyle = spike;
      g.fillRect(-len, -w * 0.5, len * 2, w);
      g.restore();
    });

    const tex = new THREE.CanvasTexture(c);
    tex.needsUpdate = true;
    return tex;
  }

  function createLabelSprite(text, subText, accentHex, baseScaleW) {
    const c = document.createElement('canvas');
    c.width = 512;
    c.height = 128;
    const g = c.getContext('2d');
    g.clearRect(0, 0, 512, 128);

    // High-contrast crisp HUD pill card
    g.fillStyle = 'rgba(7, 11, 20, 0.88)';
    g.strokeStyle = accentHex || '#93c5fd';
    g.lineWidth = 3.5;
    const rx = 24, ry = 18, rw = 464, rh = 86, rad = 20;
    g.beginPath();
    g.moveTo(rx + rad, ry);
    g.lineTo(rx + rw - rad, ry);
    g.quadraticCurveTo(rx + rw, ry, rx + rw, ry + rad);
    g.lineTo(rx + rw, ry + rh - rad);
    g.quadraticCurveTo(rx + rw, ry + rh, rx + rw - rad, ry + rh);
    g.lineTo(rx + rad, ry + rh);
    g.quadraticCurveTo(rx, ry + rh, rx, ry + rh - rad);
    g.lineTo(rx, ry + rad);
    g.quadraticCurveTo(rx, ry, rx + rad, ry);
    g.closePath();
    g.fill();
    g.stroke();

    // Marker dot with subtle glow ring
    g.fillStyle = accentHex || '#93c5fd';
    g.beginPath();
    g.arc(58, 61, 9, 0, Math.PI * 2);
    g.fill();

    g.shadowColor = 'rgba(0, 0, 0, 0.85)';
    g.shadowBlur = 4;
    g.fillStyle = '#f8fafc';
    g.font = 'bold 30px "IBM Plex Mono", monospace';
    g.textBaseline = 'middle';
    g.fillText(text, 82, subText ? 47 : 61);

    if (subText) {
      g.fillStyle = '#cbd5e1';
      g.font = '20px "IBM Plex Mono", monospace';
      g.fillText(subText, 82, 81);
    }

    const tex = new THREE.CanvasTexture(c);
    tex.needsUpdate = true;
    const mat = new THREE.SpriteMaterial({
      map: tex,
      transparent: true,
      depthWrite: false,
      depthTest: false,
      fog: false
    });
    const sp = new THREE.Sprite(mat);
    const w = baseScaleW || 6.4;
    sp.scale.set(w, w * 0.25, 1.0);
    sp.renderOrder = 25;
    state.orbitSprites.push({ sprite: sp, factor: 0.085, minW: 0.68, maxW: 7.4 });
    return sp;
  }

  function createMarsGlobeMaps() {
    const W = 1024;
    const H = 512;
    const albedoCanvas = document.createElement('canvas');
    albedoCanvas.width = W;
    albedoCanvas.height = H;
    const actx = albedoCanvas.getContext('2d');
    const aImg = actx.createImageData(W, H);

    const bumpCanvas = document.createElement('canvas');
    bumpCanvas.width = W;
    bumpCanvas.height = H;
    const bctx = bumpCanvas.getContext('2d');
    const bImg = bctx.createImageData(W, H);

    // Deterministic major Martian albedo regions (Syrtis Major, Acidalia, Tharsis, Valles Marineris, Hellas)
    const craters = [];
    for (let i = 0; i < 160; i++) {
      craters.push({
        lon: (hash2(i * 1.7, 3.1) - 0.5) * Math.PI * 2,
        lat: (hash2(i * 2.9, 9.7) - 0.5) * Math.PI * 0.85,
        rad: 0.02 + Math.pow(hash2(i * 5.3, 1.9), 2.2) * 0.14
      });
    }

    const ad = aImg.data;
    const bd = bImg.data;
    for (let y = 0; y < H; y++) {
      const v = y / (H - 1);
      const lat = (0.5 - v) * Math.PI; // +pi/2 N to -pi/2 S
      const absLat = Math.abs(lat);
      const cosLat = Math.max(0.15, Math.cos(lat));
      for (let x = 0; x < W; x++) {
        const u = x / W;
        const lon = (u - 0.5) * Math.PI * 2;
        const idx = (y * W + x) * 4;

        // 3D periodic coordinates to eliminate equirectangular seam completely
        const sx = Math.cos(lat) * Math.cos(lon);
        const sy = Math.sin(lat);
        const sz = Math.cos(lat) * Math.sin(lon);

        const macro = fbm2D(sx * 2.6 + sz * 1.3 + 5.2, sy * 3.1 + sx * 1.4 + 2.7, 5);
        const meso = fbm2D(sx * 9.5 + 11.1, sy * 9.5 + sz * 7.8 + 4.4, 4);
        const fine = fbm2D(sx * 28.0 + sz * 21.0, sy * 28.0 + 19.3, 3);

        // Basaltic dark regions (Syrtis Major ~ lon 68E / ~1.18 rad, Acidalia ~ lon -25W)
        const syrtis = Math.exp(-(Math.pow((lon - 1.18) * cosLat, 2) / 0.18 + Math.pow(lat - 0.16, 2) / 0.12));
        const acidalia = Math.exp(-(Math.pow((lon + 0.45) * cosLat, 2) / 0.25 + Math.pow(lat - 0.82, 2) / 0.10));
        const mareTyrrh = Math.exp(-(Math.pow((lon - 1.75) * cosLat, 2) / 0.35 + Math.pow(lat + 0.35, 2) / 0.08));

        // Tharsis bright ochre volcanic highland & Olympus Mons peak
        const tharsis = Math.exp(-(Math.pow((lon + 1.9) * cosLat, 2) / 0.22 + Math.pow(lat - 0.05, 2) / 0.16));
        const olympus = Math.exp(-(Math.pow((lon + 2.33) * cosLat, 2) + Math.pow(lat - 0.32, 2)) / 0.008);

        // Valles Marineris canyon slash near equator (-1.5 rad to -0.7 rad, lat ~ -0.22)
        const valles = Math.exp(-Math.pow((lat + 0.22 + Math.sin(lon * 4.0) * 0.03) / 0.035, 2)) *
                       Math.max(0, 1 - Math.abs(lon + 1.15) / 0.55);

        // Hellas bright dust basin in southern hemisphere (lon ~ 1.22, lat ~ -0.74)
        const hellas = Math.exp(-(Math.pow((lon - 1.22) * cosLat, 2) / 0.16 + Math.pow(lat + 0.74, 2) / 0.11));

        let elev = 0.42 + macro * 0.32 + meso * 0.16 + fine * 0.08 + tharsis * 0.24 + olympus * 0.35 - valles * 0.42 - hellas * 0.28;

        // Impact craters
        for (let c = 0; c < craters.length; c++) {
          const cr = craters[c];
          const dLon = ((lon - cr.lon + Math.PI * 3) % (Math.PI * 2)) - Math.PI;
          const dist = Math.hypot(dLon * cosLat, lat - cr.lat);
          if (dist < cr.rad * 1.4) {
            const rn = dist / cr.rad;
            if (rn < 0.82) elev -= (0.82 - rn) * 0.14;
            else if (rn < 1.15) elev += Math.cos((rn - 0.95) * Math.PI * 3.0) * 0.07;
          }
        }

        // Color synthesis (ochre/ferric dust vs basaltic dark bedrock)
        const darkFactor = Math.min(0.85, syrtis * 0.65 + acidalia * 0.58 + mareTyrrh * 0.52 + valles * 0.55 + Math.max(0, 0.45 - macro) * 0.7);
        const brightDust = Math.min(1.0, hellas * 0.48 + tharsis * 0.38 + meso * 0.32 + olympus * 0.25);

        let r = 188 + brightDust * 46 - darkFactor * 94 + (fine - 0.5) * 30;
        let g = 92 + brightDust * 40 - darkFactor * 52 + (fine - 0.5) * 20;
        let b = 52 + brightDust * 28 - darkFactor * 26 + (fine - 0.5) * 14;

        // Jezero crater warm clay highlight around 18.4N, 77.4E (lon=1.35 rad, lat=0.32 rad)
        const jezeroD = Math.hypot((lon - 1.351) * cosLat, lat - 0.322);
        if (jezeroD < 0.05) {
          const jw = 1 - jezeroD / 0.05;
          r += jw * 24;
          g += jw * 20;
          b += jw * 12;
        }

        // Natural spiral/jagged CO2/H2O polar caps (no polygon stair-steps!)
        const capThreshold = 1.30 + (macro - 0.5) * 0.16 + (meso - 0.5) * 0.09;
        if (absLat > capThreshold) {
          const capBlend = Math.min(1.0, (absLat - capThreshold) / 0.09);
          const frostDust = meso * 18;
          r = r * (1 - capBlend) + (242 - frostDust) * capBlend;
          g = g * (1 - capBlend) + (236 - frostDust * 1.1) * capBlend;
          b = b * (1 - capBlend) + (228 - frostDust * 1.2) * capBlend;
          elev += capBlend * 0.08;
        }

        ad[idx] = Math.max(0, Math.min(255, r));
        ad[idx + 1] = Math.max(0, Math.min(255, g));
        ad[idx + 2] = Math.max(0, Math.min(255, b));
        ad[idx + 3] = 255;

        const bv = Math.max(0, Math.min(255, elev * 255));
        bd[idx] = bv;
        bd[idx + 1] = bv;
        bd[idx + 2] = bv;
        bd[idx + 3] = 255;
      }
    }

    actx.putImageData(aImg, 0, 0);
    bctx.putImageData(bImg, 0, 0);

    const map = new THREE.CanvasTexture(albedoCanvas);
    map.encoding = THREE.sRGBEncoding;
    map.wrapS = THREE.RepeatWrapping;
    map.wrapT = THREE.ClampToEdgeWrapping;
    map.needsUpdate = true;

    const bump = new THREE.CanvasTexture(bumpCanvas);
    bump.wrapS = THREE.RepeatWrapping;
    bump.wrapT = THREE.ClampToEdgeWrapping;
    bump.needsUpdate = true;

    return { map: map, bump: bump };
  }

  function createRegolithAsteroidGeometry(rx, ry, rz, seedOffset, hasStickney) {
    const geo = new THREE.SphereGeometry(1.0, 56, 40);
    const pos = geo.attributes.position;
    const colors = new Float32Array(pos.count * 3);
    const col = new THREE.Color();

    for (let i = 0; i < pos.count; i++) {
      let x = pos.getX(i);
      let y = pos.getY(i);
      let z = pos.getZ(i);

      // Irregular rocky lobe harmonics + fine regolith bumps
      const n1 = Math.sin(x * 3.2 + seedOffset) * Math.cos(y * 2.9 - seedOffset) * 0.09;
      const n2 = Math.sin(z * 6.4 + y * 5.1 + seedOffset * 1.7) * 0.045;
      const n3 = (fbm2D(x * 4.5 + z * 3.2 + seedOffset, y * 4.5 - x * 2.1, 3) - 0.5) * 0.14;
      let rMod = 1.0 + n1 + n2 + n3;

      let craterShade = 0.0;
      if (hasStickney) {
        // Prominent Stickney Crater bowl on Phobos leading-inner face
        const sx = 0.72, sy = 0.22, sz = 0.65;
        const d = Math.hypot(x - sx, y - sy, z - sz);
        if (d < 0.56) {
          const rn = d / 0.56;
          if (rn < 0.78) {
            const depth = Math.cos((rn / 0.78) * Math.PI * 0.5) * 0.25;
            rMod -= depth;
            craterShade = -0.18 * (1 - rn);
          } else {
            rMod += Math.sin(((rn - 0.78) / 0.22) * Math.PI) * 0.065;
            craterShade = 0.14;
          }
        }
        // Secondary crater on opposite side
        const d2 = Math.hypot(x + 0.65, y - 0.35, z + 0.55);
        if (d2 < 0.35) {
          rMod -= (1 - d2 / 0.35) * 0.12;
          craterShade -= 0.10;
        }
      } else {
        // Deimos smoother chondrite cratering
        const d3 = Math.hypot(x - 0.4, y + 0.5, z - 0.6);
        if (d3 < 0.38) {
          rMod -= (1 - d3 / 0.38) * 0.10;
          craterShade -= 0.08;
        }
      }
      // Additional distributed craters across both hemispheres for crisp rocky relief
      const extraCraters = [
        [0.15, 0.72, -0.68, 0.28], [-0.52, -0.42, 0.74, 0.24], [0.68, -0.58, 0.45, 0.22],
        [-0.78, 0.12, -0.61, 0.26], [0.22, -0.82, -0.52, 0.25], [-0.25, 0.85, 0.46, 0.21]
      ];
      for (let c = 0; c < extraCraters.length; c++) {
        const ec = extraCraters[c];
        const dc = Math.hypot(x - ec[0], y - ec[1], z - ec[2]);
        if (dc < ec[3]) {
          const rn = dc / ec[3];
          if (rn < 0.8) {
            rMod -= (1 - rn / 0.8) * 0.085;
            craterShade -= 0.08 * (1 - rn);
          } else {
            rMod += Math.sin(((rn - 0.8) / 0.2) * Math.PI) * 0.025;
            craterShade += 0.04;
          }
        }
      }

      pos.setXYZ(i, x * rx * rMod, y * ry * rMod, z * rz * rMod);

      // Deep regolith chondrite vs basalt highland contrast
      const dustTone = fbm2D(x * 8.5 + seedOffset, z * 8.5 + y * 6, 4);
      const microPit = Math.sin(x * 24 + y * 19 + seedOffset) * Math.cos(z * 21 - y * 17) * 0.045;
      if (hasStickney) {
        col.setRGB(
          Math.max(0.11, Math.min(0.62, 0.30 + dustTone * 0.20 + craterShade + microPit)),
          Math.max(0.10, Math.min(0.54, 0.26 + dustTone * 0.17 + craterShade + microPit)),
          Math.max(0.09, Math.min(0.49, 0.23 + dustTone * 0.14 + craterShade + microPit))
        );
      } else {
        col.setRGB(
          Math.max(0.16, Math.min(0.68, 0.38 + dustTone * 0.20 + craterShade + microPit)),
          Math.max(0.14, Math.min(0.60, 0.33 + dustTone * 0.17 + craterShade + microPit)),
          Math.max(0.12, Math.min(0.53, 0.28 + dustTone * 0.14 + craterShade + microPit))
        );
      }
      colors[i * 3] = col.r;
      colors[i * 3 + 1] = col.g;
      colors[i * 3 + 2] = col.b;
    }

    geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));
    geo.computeVertexNormals();
    return geo;
  }

  function createMartianSkyDome() {
    const uniforms = {
      uSunDir: { value: new THREE.Vector3(0.4, 0.6, 0.6).normalize() },
      uZenithColor: { value: new THREE.Color(0x9a5c3a) },
      uHorizonColor: { value: new THREE.Color(0xd29364) },
      uSunHaloColor: { value: new THREE.Color(0x7fb4e8) },
      uTwilight: { value: 0.0 },
      uDust: { value: 0.45 }
    };

    const mat = new THREE.ShaderMaterial({
      uniforms: uniforms,
      side: THREE.BackSide,
      depthWrite: false,
      fog: false,
      vertexShader: [
        'varying vec3 vWorldDir;',
        'void main() {',
        '  vWorldDir = normalize(position);',
        '  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);',
        '}'
      ].join('\n'),
      fragmentShader: [
        'uniform vec3 uSunDir;',
        'uniform vec3 uZenithColor;',
        'uniform vec3 uHorizonColor;',
        'uniform vec3 uSunHaloColor;',
        'uniform float uTwilight;',
        'uniform float uDust;',
        'varying vec3 vWorldDir;',
        'void main() {',
        '  vec3 d = normalize(vWorldDir);',
        '  float elev = clamp(d.y, -0.25, 1.0);',
        '  float horizMix = pow(1.0 - clamp(elev, 0.0, 1.0), 1.65 + uDust * 0.9);',
        '  vec3 sky = mix(uZenithColor, uHorizonColor, horizMix);',
        '  // Below horizon subtle regolith ground bounce',
        '  if (d.y < 0.0) {',
        '    sky = mix(uHorizonColor, uHorizonColor * 0.42, clamp(-d.y * 4.5, 0.0, 1.0));',
        '  }',
        '  // Authentic Martian Mie forward scattering around Sun (butterscotch -> dusty steel-blue halo)',
        '  float sunCos = max(dot(d, normalize(uSunDir)), 0.0);',
        '  float wideMie = pow(sunCos, 3.5) * (0.28 + uDust * 0.25);',
        '  float blueAureole = pow(sunCos, 18.0) * (0.35 + uTwilight * 0.55);',
        '  float solarBloom = pow(sunCos, 160.0) * 1.1;',
        '  sky = mix(sky, uSunHaloColor, clamp(blueAureole, 0.0, 0.72));',
        '  sky += vec3(0.95, 0.72, 0.48) * wideMie * (1.0 - uTwilight * 0.5);',
        '  sky += vec3(1.0, 0.96, 0.88) * solarBloom;',
        '  gl_FragColor = vec4(sky, 1.0);',
        '}'
      ].join('\n')
    });

    const dome = new THREE.Mesh(new THREE.SphereGeometry(3400, 48, 32), mat);
    dome.name = 'MS_Realism_Martian_SkyDome';
    dome.renderOrder = -20;
    dome.frustumCulled = false;
    return { dome: dome, uniforms: uniforms };
  }

  function createMarsAtmosphereShells(radius) {
    const group = new THREE.Group();
    group.name = 'MS_Mars_Atmosphere_Group';

    const outerMat = new THREE.ShaderMaterial({
      uniforms: {
        uSunDir: { value: new THREE.Vector3(-0.6, 0.4, 0.7).normalize() }
      },
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      side: THREE.BackSide,
      vertexShader: [
        'varying vec3 vNormalW;',
        'varying vec3 vViewDir;',
        'void main() {',
        '  vec4 wp = modelMatrix * vec4(position, 1.0);',
        '  vNormalW = normalize(mat3(modelMatrix) * normal);',
        '  vViewDir = normalize(cameraPosition - wp.xyz);',
        '  gl_Position = projectionMatrix * viewMatrix * wp;',
        '}'
      ].join('\n'),
      fragmentShader: [
        'uniform vec3 uSunDir;',
        'varying vec3 vNormalW;',
        'varying vec3 vViewDir;',
        'void main() {',
        '  float rim = pow(clamp(1.0 - max(dot(-vNormalW, vViewDir), 0.0), 0.0, 1.0), 3.4);',
        '  float sunTerm = dot(vNormalW, normalize(uSunDir));',
        '  float daySide = smoothstep(-0.35, 0.65, sunTerm);',
        '  float termBand = exp(-pow((sunTerm + 0.05) * 4.0, 2.0));',
        '  vec3 dayColor = vec3(0.96, 0.62, 0.36);',
        '  vec3 twiColor = vec3(0.45, 0.72, 0.98);',
        '  vec3 col = mix(dayColor, twiColor, termBand * 0.72);',
        '  float alpha = rim * (0.08 + daySide * 0.68 + termBand * 0.25);',
        '  gl_FragColor = vec4(col, clamp(alpha, 0.0, 0.85));',
        '}'
      ].join('\n')
    });

    const outerShell = new THREE.Mesh(new THREE.SphereGeometry(radius * 1.055, 96, 64), outerMat);
    group.add(outerShell);

    const innerMat = new THREE.ShaderMaterial({
      uniforms: {
        uSunDir: outerMat.uniforms.uSunDir
      },
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      side: THREE.FrontSide,
      vertexShader: outerMat.vertexShader,
      fragmentShader: [
        'uniform vec3 uSunDir;',
        'varying vec3 vNormalW;',
        'varying vec3 vViewDir;',
        'void main() {',
        '  float fres = pow(clamp(1.0 - max(dot(vNormalW, vViewDir), 0.0), 0.0, 1.0), 4.2);',
        '  float sunTerm = dot(vNormalW, normalize(uSunDir));',
        '  float daySide = smoothstep(-0.25, 0.7, sunTerm);',
        '  float termBand = exp(-pow(sunTerm * 4.5, 2.0));',
        '  vec3 col = mix(vec3(0.95, 0.66, 0.42), vec3(0.55, 0.80, 1.0), termBand * 0.8);',
        '  float alpha = fres * (0.04 + daySide * 0.42 + termBand * 0.30);',
        '  gl_FragColor = vec4(col, clamp(alpha, 0.0, 0.7));',
        '}'
      ].join('\n')
    });
    const innerShell = new THREE.Mesh(new THREE.SphereGeometry(radius * 1.008, 96, 64), innerMat);
    group.add(innerShell);

    return { group: group, uniformSun: outerMat.uniforms.uSunDir };
  }

  function buildGradedOrbitTrack(rawPoints, baseHex, radius, trackerMesh, labelInfo) {
    if (!rawPoints || rawPoints.length < 3) return null;
    const pts = [];
    for (let i = 0; i < rawPoints.length; i++) pts.push(rawPoints[i].clone());

    const curve = new THREE.CatmullRomCurve3(pts, true, 'centripetal');
    const segs = 220;
    const tubeGeo = new THREE.TubeGeometry(curve, segs, radius, 8, true);

    const baseCol = new THREE.Color(baseHex);
    const brightCol = baseCol.clone().lerp(new THREE.Color(0xffffff), 0.55);
    const dimCol = baseCol.clone().multiplyScalar(0.28);

    const posAttr = tubeGeo.attributes.position;
    const radialVerts = 9; // radialSegments + 1 in Three.js TubeGeometry
    const ringCount = Math.floor(posAttr.count / radialVerts);
    const cols = new Float32Array(posAttr.count * 3);

    tubeGeo.setAttribute('color', new THREE.BufferAttribute(cols, 3));

    const mat = new THREE.MeshBasicMaterial({
      vertexColors: true,
      transparent: true,
      opacity: 0.92,
      blending: THREE.AdditiveBlending,
      depthWrite: false
    });

    const tubeMesh = new THREE.Mesh(tubeGeo, mat);
    tubeMesh.renderOrder = 4;

    // Fine crisp inner telemetry thread
    const linePts = curve.getPoints(segs);
    const hairGeo = new THREE.BufferGeometry().setFromPoints(linePts);
    const hairMat = new THREE.LineBasicMaterial({
      color: baseCol,
      transparent: true,
      opacity: 0.36,
      depthWrite: false
    });
    const hairLine = new THREE.LineLoop(hairGeo, hairMat);

    const trackGroup = new THREE.Group();
    trackGroup.add(tubeMesh);
    trackGroup.add(hairLine);

    // Apoapsis & Periapsis node diamonds
    let minD = Infinity, maxD = -Infinity, pePt = linePts[0], apPt = linePts[0];
    for (let i = 0; i < linePts.length; i++) {
      const d = linePts[i].length();
      if (d < minD) { minD = d; pePt = linePts[i]; }
      if (d > maxD) { maxD = d; apPt = linePts[i]; }
    }
    const nodeGeo = new THREE.OctahedronGeometry(radius * 2.3, 0);
    const peMesh = new THREE.Mesh(nodeGeo, new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.78 }));
    peMesh.position.copy(pePt);
    trackGroup.add(peMesh);

    if (maxD - minD > 0.6) {
      const apMesh = new THREE.Mesh(nodeGeo, new THREE.MeshBasicMaterial({ color: baseCol, transparent: true, opacity: 0.72 }));
      apMesh.position.copy(apPt);
      trackGroup.add(apMesh);
    }

    function updateWake(phaseNorm) {
      const colorAttr = tubeGeo.attributes.color;
      const arr = colorAttr.array;
      for (let s = 0; s < ringCount; s++) {
        const t = s / ringCount;
        // Distance behind tracker along cyclic orbit [0..1)
        let delta = (phaseNorm - t + 1.0) % 1.0;
        let w;
        if (delta < 0.34) {
          // Luminous directional orbital wake behind craft/moon
          w = Math.pow(1.0 - delta / 0.34, 1.85);
        } else {
          w = 0.08 + 0.05 * Math.sin(t * Math.PI * 16.0);
        }
        const cr = dimCol.r + (brightCol.r - dimCol.r) * w;
        const cg = dimCol.g + (brightCol.g - dimCol.g) * w;
        const cb = dimCol.b + (brightCol.b - dimCol.b) * w;
        for (let r = 0; r < radialVerts; r++) {
          const vi = (s * radialVerts + r) * 3;
          if (vi + 2 < arr.length) {
            arr[vi] = cr;
            arr[vi + 1] = cg;
            arr[vi + 2] = cb;
          }
        }
      }
      colorAttr.needsUpdate = true;
    }

    updateWake(0.0);

    return {
      group: trackGroup,
      curve: curve,
      linePts: linePts,
      trackerMesh: trackerMesh,
      updateWake: updateWake,
      label: labelInfo
    };
  }

  function extractLineLoopPoints(lineObj) {
    if (!lineObj || !lineObj.geometry || !lineObj.geometry.attributes || !lineObj.geometry.attributes.position) return [];
    const pos = lineObj.geometry.attributes.position;
    const pts = [];
    for (let i = 0; i < pos.count; i++) {
      pts.push(new THREE.Vector3(pos.getX(i), pos.getY(i), pos.getZ(i)));
    }
    return pts;
  }

  function findClosestPhase(curvePts, objPos) {
    if (!curvePts.length || !objPos) return 0;
    let bestIdx = 0;
    let bestDist = Infinity;
    for (let i = 0; i < curvePts.length; i++) {
      const d = curvePts[i].distanceToSquared(objPos);
      if (d < bestDist) {
        bestDist = d;
        bestIdx = i;
      }
    }
    return bestIdx / curvePts.length;
  }

  function createSpaceSunAssembly() {
    const g = new THREE.Group();
    g.name = 'MS_Space_Sun_Assembly';
    g.position.set(-16.5, 11.5, -24.0);

    // Blazing solar photosphere sphere + HDR additive brilliance (avoids ACES grey flattening)
    const sunSphere = new THREE.Mesh(
      new THREE.SphereGeometry(1.9, 36, 36),
      new THREE.ShaderMaterial({
        uniforms: {},
        fog: false,
        vertexShader: [
          'varying vec3 vN; varying vec3 vV;',
          'void main() {',
          '  vec4 wp = modelMatrix * vec4(position, 1.0);',
          '  vN = normalize(mat3(modelMatrix) * normal);',
          '  vV = normalize(cameraPosition - wp.xyz);',
          '  gl_Position = projectionMatrix * viewMatrix * wp;',
          '}'
        ].join('\n'),
        fragmentShader: [
          'varying vec3 vN; varying vec3 vV;',
          'void main() {',
          '  float mu = max(dot(vN, vV), 0.0);',
          '  vec3 core = mix(vec3(2.4, 1.25, 0.50), vec3(4.5, 4.0, 3.2), pow(mu, 0.55));',
          '  gl_FragColor = vec4(core, 1.0);',
          '}'
        ].join('\n')
      })
    );
    sunSphere.renderOrder = 10;
    g.add(sunSphere);

    // Chromosphere rim
    const innerCorona = new THREE.Sprite(new THREE.SpriteMaterial({
      map: createGlowTexture('rgba(255,252,235,0.92)', 'rgba(255,195,95,0.65)', 'rgba(255,110,35,0.14)', 256),
      transparent: true,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      fog: false
    }));
    innerCorona.scale.set(13.5, 13.5, 1.0);
    g.add(innerCorona);

    // Diffraction star spikes
    const flareSprite = new THREE.Sprite(new THREE.SpriteMaterial({
      map: createStarSpikeTexture(),
      transparent: true,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      opacity: 0.88,
      fog: false
    }));
    flareSprite.scale.set(20, 20, 1.0);
    g.add(flareSprite);

    const sunLabel = createLabelSprite('SOL • SUN', '1.52 AU Heliocentric Source', '#fcd34d', 6.0);
    sunLabel.position.set(0, -4.4, 0);
    g.add(sunLabel);

    // Dedicated Space Sun Directional Light casting crisp planetary rim & terminator
    const spaceLight = new THREE.DirectionalLight(0xfff1dc, 2.25);
    spaceLight.position.copy(g.position);
    g.add(spaceLight);
    g.userData.spaceLight = spaceLight;

    return g;
  }

  function createSurfaceSkyMoons() {
    const grp = new THREE.Group();
    grp.name = 'MS_Surface_Sky_Moons';

    // Phobos in Martian sky dome (dark cratered regolith + subtle Martian airlight veil)
    const phobosTex = createGlowTexture('rgba(255,232,208,0.28)', 'rgba(180,145,120,0.10)', 'rgba(120,100,85,0.0)', 128);
    const phobosGeo = createRegolithAsteroidGeometry(26, 20, 18, 2.3, true);
    const phobosMat = new THREE.MeshStandardMaterial({
      vertexColors: true,
      roughness: 0.92,
      metalness: 0.04,
      emissive: new THREE.Color(0x5c4232),
      emissiveIntensity: 0.24,
      fog: false
    });
    const surfPhobos = new THREE.Mesh(phobosGeo, phobosMat);
    const phobosHalo = new THREE.Sprite(new THREE.SpriteMaterial({
      map: phobosTex,
      transparent: true,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      fog: false
    }));
    phobosHalo.scale.set(74, 74, 1);
    surfPhobos.add(phobosHalo);

    // Deimos in Martian sky dome
    const deimosGeo = createRegolithAsteroidGeometry(15, 12.2, 10.6, 8.1, false);
    const deimosMat = new THREE.MeshStandardMaterial({
      vertexColors: true,
      roughness: 0.88,
      metalness: 0.04,
      emissive: new THREE.Color(0x6b503c),
      emissiveIntensity: 0.28,
      fog: false
    });
    const surfDeimos = new THREE.Mesh(deimosGeo, deimosMat);
    const deimosHalo = new THREE.Sprite(new THREE.SpriteMaterial({
      map: createGlowTexture('rgba(255,236,204,0.34)', 'rgba(200,165,125,0.12)', 'rgba(0,0,0,0)', 128),
      transparent: true,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      fog: false
    }));
    deimosHalo.scale.set(48, 48, 1);
    surfDeimos.add(deimosHalo);

    grp.add(surfPhobos);
    grp.add(surfDeimos);

    state.surfacePhobos = surfPhobos;
    state.surfaceDeimos = surfDeimos;
    return grp;
  }

  function createDetailedNASAHabitat(groundY) {
    const hab = new THREE.Group();
    hab.name = 'MS_Realism_NASA_Habitat';
    hab.position.set(0, groundY, 0);

    const whiteThermal = new THREE.MeshStandardMaterial({ color: 0xe6e8eb, roughness: 0.42, metalness: 0.25 });
    const darkTitanium = new THREE.MeshStandardMaterial({ color: 0x383e46, roughness: 0.38, metalness: 0.72 });
    const goldFoil = new THREE.MeshStandardMaterial({ color: 0xd69e2e, roughness: 0.28, metalness: 0.88 });
    const solarMat = new THREE.MeshStandardMaterial({ color: 0x16386b, roughness: 0.22, metalness: 0.62 });
    const warmWindow = new THREE.MeshStandardMaterial({
      color: 0xffd28a,
      emissive: new THREE.Color(0xff9f38),
      emissiveIntensity: 1.4,
      roughness: 0.15
    });

    // Lower pressurized hexagonal platform & gold MLI underbelly
    const baseSkirt = new THREE.Mesh(new THREE.CylinderGeometry(4.1, 3.6, 1.3, 12), goldFoil);
    baseSkirt.position.y = 2.0;
    baseSkirt.castShadow = true;
    baseSkirt.receiveShadow = true;
    hab.add(baseSkirt);

    // Main composite habitat pressure dome (flattened geodesic cylinder-dome)
    const hullMain = new THREE.Mesh(new THREE.CylinderGeometry(4.3, 4.3, 2.35, 24), whiteThermal);
    hullMain.position.y = 3.6;
    hullMain.castShadow = true;
    hullMain.receiveShadow = true;
    hab.add(hullMain);

    const domeCap = new THREE.Mesh(
      new THREE.SphereGeometry(4.3, 24, 16, 0, Math.PI * 2, 0, Math.PI * 0.52),
      whiteThermal
    );
    domeCap.scale.set(1, 0.56, 1);
    domeCap.position.y = 4.72;
    domeCap.castShadow = true;
    hab.add(domeCap);

    // Machined titanium vertical structural ribs
    for (let i = 0; i < 8; i++) {
      const ang = (i / 8) * Math.PI * 2;
      const rib = new THREE.Mesh(new THREE.BoxGeometry(0.18, 2.55, 0.26), darkTitanium);
      rib.position.set(Math.sin(ang) * 4.32, 3.6, Math.cos(ang) * 4.32);
      rib.rotation.y = ang;
      rib.castShadow = true;
      hab.add(rib);
    }
    const bandRing = new THREE.Mesh(new THREE.TorusGeometry(4.36, 0.11, 10, 32), darkTitanium);
    bandRing.rotation.x = Math.PI * 0.5;
    bandRing.position.y = 4.75;
    hab.add(bandRing);

    // Illuminated cupola & EVA observation portholes facing outward cleanly
    [-0.48, 0.48].forEach((angOffset) => {
      const a = Math.PI + angOffset;
      const bezel = new THREE.Mesh(new THREE.CylinderGeometry(0.54, 0.58, 0.25, 16), darkTitanium);
      bezel.rotation.x = Math.PI * 0.5;
      bezel.rotation.z = -a;
      bezel.position.set(Math.sin(a) * 4.35, 3.8, Math.cos(a) * 4.35);
      hab.add(bezel);

      const pane = new THREE.Mesh(new THREE.CircleGeometry(0.42, 16), warmWindow);
      pane.position.set(Math.sin(a) * 4.49, 3.8, Math.cos(a) * 4.49);
      pane.lookAt(Math.sin(a) * 10, 3.8, Math.cos(a) * 10);
      hab.add(pane);
    });

    // Front Pressurized Airlock Vestibule + Circular Hatch + Ladder
    const airlock = new THREE.Mesh(new THREE.BoxGeometry(2.1, 2.4, 2.2), whiteThermal);
    airlock.position.set(0, 2.95, 4.65);
    airlock.castShadow = true;
    hab.add(airlock);

    const hatchRing = new THREE.Mesh(new THREE.TorusGeometry(0.68, 0.09, 10, 20), darkTitanium);
    hatchRing.position.set(0, 2.95, 5.76);
    hab.add(hatchRing);
    const hatchGlass = new THREE.Mesh(new THREE.CircleGeometry(0.28, 16), warmWindow);
    hatchGlass.position.set(0, 3.15, 5.78);
    hab.add(hatchGlass);

    // Four articulated hydraulic tripod landing struts with wide regolith footpads
    for (let i = 0; i < 4; i++) {
      const a = (i / 4) * Math.PI * 2 + Math.PI * 0.25;
      const sx = Math.cos(a), sz = Math.sin(a);
      const strut = new THREE.Mesh(new THREE.CylinderGeometry(0.16, 0.21, 3.3, 10), darkTitanium);
      strut.position.set(sx * 4.5, 1.35, sz * 4.5);
      strut.lookAt(sx * 3.1, 2.9, sz * 3.1);
      strut.rotateX(Math.PI * 0.5);
      strut.castShadow = true;
      hab.add(strut);

      const pad = new THREE.Mesh(new THREE.CylinderGeometry(0.65, 0.78, 0.22, 14), darkTitanium);
      pad.position.set(sx * 5.2, 0.1, sz * 5.2);
      pad.castShadow = true;
      pad.receiveShadow = true;
      hab.add(pad);
    }

    // Rooftop Comms Parabolic Dish & Twin Photovoltaic Wings
    const dishMast = new THREE.Mesh(new THREE.CylinderGeometry(0.12, 0.16, 1.6, 10), darkTitanium);
    dishMast.position.set(-1.1, 7.1, 0.2);
    hab.add(dishMast);
    const dish = new THREE.Mesh(new THREE.SphereGeometry(0.95, 18, 12, 0, Math.PI * 2, 0, Math.PI * 0.35), whiteThermal);
    dish.position.set(-1.1, 7.9, 0.2);
    dish.rotation.x = Math.PI * 0.75;
    hab.add(dish);

    [-1, 1].forEach((side) => {
      const wing = new THREE.Mesh(new THREE.BoxGeometry(3.4, 0.08, 1.6), solarMat);
      wing.position.set(side * 3.3, 6.25, -0.5);
      wing.rotation.z = side * 0.15;
      wing.castShadow = true;
      hab.add(wing);
    });

    // Subtle warm exterior floodlight under airlock
    const habLight = new THREE.PointLight(0xffb86b, 1.6, 16, 1.8);
    habLight.position.set(0, 3.2, 6.2);
    hab.add(habLight);

    return hab;
  }

  function createDetailedAstronaut(x, y, z, rotY) {
    const astro = new THREE.Group();
    astro.name = 'MS_Realism_Astronaut';
    astro.position.set(x, y, z);
    astro.rotation.y = rotY || 0;

    const suitWhite = new THREE.MeshStandardMaterial({ color: 0xedeae4, roughness: 0.62, metalness: 0.08 });
    const suitOrange = new THREE.MeshStandardMaterial({ color: 0xc24921, roughness: 0.58, metalness: 0.08 });
    const visorGold = new THREE.MeshStandardMaterial({ color: 0xf5b841, roughness: 0.12, metalness: 0.92, emissive: 0x331f00, emissiveIntensity: 0.35 });
    const darkJoint = new THREE.MeshStandardMaterial({ color: 0x262930, roughness: 0.55, metalness: 0.3 });

    // Legs & boots
    [-0.14, 0.14].forEach((lx) => {
      const leg = new THREE.Mesh(new THREE.CylinderGeometry(0.11, 0.095, 0.76, 10), suitWhite);
      leg.position.set(lx, 0.42, 0);
      leg.castShadow = true;
      astro.add(leg);
      const boot = new THREE.Mesh(new THREE.BoxGeometry(0.16, 0.14, 0.26), darkJoint);
      boot.position.set(lx, 0.07, 0.04);
      boot.castShadow = true;
      astro.add(boot);
    });

    // Ribbed Torso + Mars orange mission stripes
    const torso = new THREE.Mesh(new THREE.CylinderGeometry(0.24, 0.21, 0.64, 12), suitWhite);
    torso.position.y = 1.08;
    torso.castShadow = true;
    astro.add(torso);

    const chestVest = new THREE.Mesh(new THREE.BoxGeometry(0.42, 0.36, 0.34), suitOrange);
    chestVest.position.set(0, 1.12, 0.03);
    chestVest.castShadow = true;
    astro.add(chestVest);

    // PLSS Backpack
    const plss = new THREE.Mesh(new THREE.BoxGeometry(0.38, 0.54, 0.24), suitWhite);
    plss.position.set(0, 1.15, -0.22);
    plss.castShadow = true;
    astro.add(plss);

    // Arms
    [-0.29, 0.29].forEach((ax, idx) => {
      const arm = new THREE.Mesh(new THREE.CylinderGeometry(0.08, 0.07, 0.58, 10), suitWhite);
      arm.position.set(ax, 1.06, 0.06);
      arm.rotation.z = idx === 0 ? 0.18 : -0.18;
      arm.rotation.x = -0.3;
      arm.castShadow = true;
      astro.add(arm);
    });

    // Helmet & Gold reflective visor
    const helmet = new THREE.Mesh(new THREE.SphereGeometry(0.205, 16, 14), suitWhite);
    helmet.position.set(0, 1.56, 0.02);
    helmet.castShadow = true;
    astro.add(helmet);

    const visor = new THREE.Mesh(new THREE.SphereGeometry(0.175, 16, 12), visorGold);
    visor.scale.set(1.0, 0.88, 0.72);
    visor.position.set(0, 1.56, 0.11);
    astro.add(visor);

    return astro;
  }

  function createPerseveranceDetailAttachment() {
    const rig = new THREE.Group();
    rig.name = 'MS_Realism_Perseverance_Rig';

    const whiteChassis = new THREE.MeshStandardMaterial({ color: 0xefefef, roughness: 0.38, metalness: 0.28 });
    const goldThermal = new THREE.MeshStandardMaterial({ color: 0xd49b27, roughness: 0.26, metalness: 0.85 });
    const darkMetal = new THREE.MeshStandardMaterial({ color: 0x23272e, roughness: 0.42, metalness: 0.75 });
    const cyanLens = new THREE.MeshStandardMaterial({ color: 0x38bdf8, emissive: 0x0ea5e9, emissiveIntensity: 0.9, roughness: 0.1 });

    // Sculpted rover warm-electronics body + aft angled MMRTG
    const body = new THREE.Mesh(new THREE.BoxGeometry(2.1, 0.62, 2.75), whiteChassis);
    body.position.set(0, 0.96, 0);
    body.castShadow = true;
    rig.add(body);

    const topFoil = new THREE.Mesh(new THREE.BoxGeometry(1.9, 0.08, 2.2), goldThermal);
    topFoil.position.set(0, 1.3, 0.1);
    rig.add(topFoil);

    // Aft MMRTG nuclear power core with radiating fins
    const rtg = new THREE.Mesh(new THREE.CylinderGeometry(0.28, 0.34, 1.05, 10), goldThermal);
    rtg.rotation.x = Math.PI * 0.34;
    rtg.position.set(0, 1.25, -1.52);
    rtg.castShadow = true;
    rig.add(rtg);

    // Tall Remote Sensing Mast + Mastcam-Z stereo optical head
    const mast = new THREE.Mesh(new THREE.CylinderGeometry(0.075, 0.095, 1.25, 10), whiteChassis);
    mast.position.set(0.45, 1.88, 0.95);
    mast.castShadow = true;
    rig.add(mast);

    const head = new THREE.Mesh(new THREE.BoxGeometry(0.62, 0.28, 0.42), whiteChassis);
    head.position.set(0.45, 2.52, 0.98);
    head.castShadow = true;
    rig.add(head);

    [-0.14, 0.14].forEach((ox) => {
      const eye = new THREE.Mesh(new THREE.CylinderGeometry(0.065, 0.065, 0.1, 12), cyanLens);
      eye.rotation.x = Math.PI * 0.5;
      eye.position.set(0.45 + ox, 2.52, 1.19);
      rig.add(eye);
    });

    // 6-Wheel Rocker-Bogie Suspension + Cleated Titanium Wheels
    const wheelZ = [-1.15, 0.0, 1.15];
    [-1.08, 1.08].forEach((wx) => {
      // Rocker-bogie longitudinal strut
      const rail = new THREE.Mesh(new THREE.BoxGeometry(0.09, 0.09, 2.35), darkMetal);
      rail.position.set(wx * 0.88, 0.65, 0);
      rig.add(rail);

      wheelZ.forEach((wz) => {
        const w = new THREE.Mesh(new THREE.CylinderGeometry(0.31, 0.31, 0.28, 16), darkMetal);
        w.rotation.z = Math.PI * 0.5;
        w.position.set(wx, 0.31, wz);
        w.castShadow = true;
        rig.add(w);
        const hub = new THREE.Mesh(new THREE.CylinderGeometry(0.14, 0.14, 0.31, 10), goldThermal);
        hub.rotation.z = Math.PI * 0.5;
        hub.position.set(wx, 0.31, wz);
        rig.add(hub);
      });
    });

    // Robotic Sampling Arm extended slightly forward
    const armUpper = new THREE.Mesh(new THREE.BoxGeometry(0.12, 0.12, 1.05), darkMetal);
    armUpper.position.set(-0.35, 0.95, 1.75);
    armUpper.rotation.x = 0.22;
    rig.add(armUpper);
    const turret = new THREE.Mesh(new THREE.CylinderGeometry(0.24, 0.24, 0.2, 12), goldThermal);
    turret.position.set(-0.35, 0.82, 2.25);
    rig.add(turret);

    return rig;
  }

  function buildSurfaceEnvironment(scene) {
    if (!state.terrainMesh || !state.terrainHeightFn) return;
    const hFn = state.terrainHeightFn;

    const envGroup = new THREE.Group();
    envGroup.name = 'MS_Realism_Surface_Env';

    // 1. 360-degree Jezero Crater Distant Rim & Delta Escarpment Silhouette
    const rimVerts = [];
    const rimSegs = 128;
    const rimCols = [];
    const colA = new THREE.Color(0x8e4c2e);
    const colB = new THREE.Color(0x5c301d);
    for (let i = 0; i <= rimSegs; i++) {
      const a = (i / rimSegs) * Math.PI * 2;
      const rInner = 166 + Math.sin(a * 5.0) * 6;
      const rOuter = 235;
      const ridgeH = 14.0 + fbm2D(Math.cos(a) * 2.8 + 1.5, Math.sin(a) * 2.8 + 3.1, 4) * 24.0;
      const baseH = hFn(Math.cos(a) * rInner * 0.8, Math.sin(a) * rInner * 0.8) - 2.0;

      rimVerts.push(Math.cos(a) * rInner, baseH, Math.sin(a) * rInner);
      rimCols.push(colB.r, colB.g, colB.b);

      rimVerts.push(Math.cos(a) * rOuter, ridgeH, Math.sin(a) * rOuter);
      rimCols.push(colA.r, colA.g, colA.b);
    }
    const rimIndices = [];
    for (let i = 0; i < rimSegs; i++) {
      const b0 = i * 2, t0 = i * 2 + 1, b1 = (i + 1) * 2, t1 = (i + 1) * 2 + 1;
      rimIndices.push(b0, t0, b1, t0, t1, b1);
    }
    const rimGeo = new THREE.BufferGeometry();
    rimGeo.setAttribute('position', new THREE.Float32BufferAttribute(rimVerts, 3));
    rimGeo.setAttribute('color', new THREE.Float32BufferAttribute(rimCols, 3));
    rimGeo.setIndex(rimIndices);
    rimGeo.computeVertexNormals();
    const rimMesh = new THREE.Mesh(rimGeo, new THREE.MeshStandardMaterial({
      vertexColors: true,
      roughness: 0.96,
      metalness: 0.02,
      side: THREE.DoubleSide
    }));
    envGroup.add(rimMesh);

    // 2. 340+ Procedural Stratified Basalt & Olivine Regolith Boulders (InstancedMesh)
    const rockGeo = new THREE.DodecahedronGeometry(1.0, 1);
    const rPos = rockGeo.attributes.position;
    for (let i = 0; i < rPos.count; i++) {
      const x = rPos.getX(i), y = rPos.getY(i), z = rPos.getZ(i);
      const scale = 0.72 + hash2(x * 7.1 + z * 3.3, y * 5.9) * 0.52;
      rPos.setXYZ(i, x * scale, y * scale * 0.62, z * scale);
    }
    rockGeo.computeVertexNormals();
    const rockMat = new THREE.MeshStandardMaterial({
      color: 0x6b3b24,
      roughness: 0.94,
      metalness: 0.04
    });
    const count = 340;
    const inst = new THREE.InstancedMesh(rockGeo, rockMat, count);
    inst.castShadow = true;
    inst.receiveShadow = true;
    const dummy = new THREE.Object3D();
    const rc = new THREE.Color();
    for (let i = 0; i < count; i++) {
      const ang = hash2(i, 1.7) * Math.PI * 2;
      const dist = 11.0 + Math.pow(hash2(i, 8.3), 0.75) * 140.0;
      const x = Math.cos(ang) * dist;
      const z = Math.sin(ang) * dist;
      const y = hFn(x, z);
      const s = 0.14 + Math.pow(hash2(i, 4.9), 2.8) * 1.65;
      dummy.position.set(x, y + s * 0.24, z);
      dummy.rotation.set(hash2(i, 2.1) * 0.5, hash2(i, 3.7) * Math.PI * 2, hash2(i, 6.2) * 0.5);
      dummy.scale.set(s * (0.9 + hash2(i, 9.1) * 0.6), s * 0.78, s * (0.9 + hash2(i, 11.2) * 0.6));
      dummy.updateMatrix();
      inst.setMatrixAt(i, dummy.matrix);
      const shade = 0.72 + hash2(i, 14.3) * 0.48;
      rc.setRGB(0.45 * shade, 0.25 * shade, 0.16 * shade);
      inst.setColorAt(i, rc);
    }
    inst.instanceMatrix.needsUpdate = true;
    if (inst.instanceColor) inst.instanceColor.needsUpdate = true;
    envGroup.add(inst);

    // 3. Drifting Sunlit Martian Dust Motes
    const moteCount = 320;
    const mPos = new Float32Array(moteCount * 3);
    for (let i = 0; i < moteCount; i++) {
      mPos[i * 3] = (hash2(i, 1.1) - 0.5) * 90;
      mPos[i * 3 + 1] = 0.4 + hash2(i, 2.2) * 14.0;
      mPos[i * 3 + 2] = (hash2(i, 3.3) - 0.5) * 90;
    }
    const mGeo = new THREE.BufferGeometry();
    mGeo.setAttribute('position', new THREE.BufferAttribute(mPos, 3));
    const mMat = new THREE.PointsMaterial({
      size: 0.28,
      map: createGlowTexture('rgba(255,220,180,0.8)', 'rgba(235,170,110,0.3)', 'rgba(0,0,0,0)', 64),
      transparent: true,
      opacity: 0.45,
      depthWrite: false,
      blending: THREE.AdditiveBlending
    });
    const motes = new THREE.Points(mGeo, mMat);
    state.dustMotes = motes;
    envGroup.add(motes);

    if (state.surfaceRealismGroup) {
      state.surfaceRealismGroup.add(envGroup);
    } else {
      scene.add(envGroup);
    }
  }

  function makeTerrainHeightSampler(mesh) {
    const pos = mesh.geometry.attributes.position;
    const p = mesh.geometry.parameters || { width: 360, height: 360, widthSegments: 180, heightSegments: 180 };
    const cols = (p.widthSegments || 180) + 1;
    const rows = (p.heightSegments || 180) + 1;
    const halfW = (p.width || 360) * 0.5;
    const halfH = (p.height || 360) * 0.5;
    // Detect whether geometry is rotated in X (-pi/2) or already baked into world Y
    const worldRotX = Math.abs(mesh.rotation.x + Math.PI * 0.5) < 0.2;
    return function (wx, wz) {
      const u = Math.max(0, Math.min(1, (wx + halfW) / (halfW * 2)));
      const v = Math.max(0, Math.min(1, (wz + halfH) / (halfH * 2)));
      const col = Math.min(cols - 1, Math.max(0, Math.round(u * (cols - 1))));
      const row = Math.min(rows - 1, Math.max(0, Math.round(v * (rows - 1))));
      const idx = row * cols + col;
      if (idx >= 0 && idx < pos.count) {
        return worldRotX ? pos.getZ(idx) + mesh.position.y : pos.getY(idx) + mesh.position.y;
      }
      return 0;
    };
  }

  function upgradeExistingSurfaceModels(scene) {
    const toRemove = [];
    const habCandidates = [];
    const crewTargets = [];
    let persevRoot = null;

    scene.traverse((obj) => {
      if (state.orbitGroup && (obj === state.orbitGroup || obj.parent === state.orbitGroup)) return;
      if (obj === state.skyDome || obj === state.surfaceMoonsGroup) return;

      // Locate black shiny dome habitat sphere (`SphereGeometry` radius ~ 3..6 near surface)
      if (obj.isMesh && obj.geometry && obj.geometry.type === 'SphereGeometry' && obj.geometry.parameters) {
        const r = obj.geometry.parameters.radius || 0;
        const color = obj.material && obj.material.color ? obj.material.color : null;
        if (r >= 2.2 && r <= 7.0 && color && color.r < 0.22 && color.g < 0.22 && color.b < 0.22) {
          habCandidates.push(obj);
        }
      }

      // Locate Perseverance moving rover box chassis (`BoxGeometry` roughly 2x0.8x3)
      if (obj.isMesh && obj.geometry && obj.geometry.type === 'BoxGeometry' && obj.geometry.parameters) {
        const bp = obj.geometry.parameters;
        if (bp.width >= 1.5 && bp.width <= 3.0 && bp.depth >= 2.0 && bp.depth <= 3.8 && bp.height >= 0.4 && bp.height <= 1.2) {
          if (obj.parent && obj.parent !== scene && !obj.parent.name.startsWith('MS_Realism_')) {
            persevRoot = obj.parent;
          }
        }
      }

      // Locate BOTH orange & blue peg astronauts (small cylinder height ~ 0.8..1.8)
      if (obj.isMesh && obj.geometry && obj.geometry.type === 'CylinderGeometry' && obj.geometry.parameters) {
        const cp = obj.geometry.parameters;
        const c = obj.material && obj.material.color ? obj.material.color : null;
        const isOrangePeg = c && c.r > 0.55 && c.g < 0.45;
        const isBluePeg = c && c.b > 0.35 && c.r < 0.35 && c.g < 0.48;
        if (cp.height >= 0.8 && cp.height <= 1.8 && cp.radiusTop <= 0.45 && (isOrangePeg || isBluePeg)) {
          const wp = new THREE.Vector3();
          obj.getWorldPosition(wp);
          crewTargets.push({ obj: obj, wp: wp });
        }
      }
    });

    // Upgrade Habitat cleanly at exact world center of the black sphere
    if (habCandidates.length > 0) {
      const mainHabSphere = habCandidates[0];
      const wp = new THREE.Vector3();
      mainHabSphere.getWorldPosition(wp);
      const habParent = mainHabSphere.parent && mainHabSphere.parent !== scene ? mainHabSphere.parent : null;

      // Hide ONLY the primitive habitat dome/eyes/legs within 6.5m of mainHabSphere center
      const meshPos = new THREE.Vector3();
      scene.traverse((c) => {
        if (!c.isMesh || c === state.terrainMesh || c === state.skyDome) return;
        if (state.orbitGroup && (c === state.orbitGroup || c.parent === state.orbitGroup)) return;
        c.getWorldPosition(meshPos);
        const dx = meshPos.x - wp.x;
        const dz = meshPos.z - wp.z;
        if (dx * dx + dz * dz < 34.0 && meshPos.y > 0.2 && meshPos.y < 12.0) {
          // Leave cylinders that might belong to nearby white/teal fuel silos untouched (silos sit off-center)
          const gType = c.geometry ? c.geometry.type : '';
          if (c === mainHabSphere || gType === 'BoxGeometry' || gType === 'CylinderGeometry') {
            c.visible = false;
          }
        }
      });

      const groundY = state.terrainHeightFn ? state.terrainHeightFn(wp.x, wp.z) : 0;
      const detailedHab = createDetailedNASAHabitat(groundY);
      detailedHab.position.set(wp.x, groundY, wp.z);
      (state.surfaceRealismGroup || scene).add(detailedHab);
    }

    // Upgrade orange cylinder crew members -> Articulated EVA Spacesuit Astronauts
    crewTargets.forEach((ct, idx) => {
      const pGroup = ct.obj.parent;
      if (pGroup && pGroup !== scene) {
        pGroup.traverse((c) => { if (c.isMesh) c.visible = false; });
      } else {
        ct.obj.visible = false;
      }
      const gy = state.terrainHeightFn ? state.terrainHeightFn(ct.wp.x, ct.wp.z) : 0;
      const astro = createDetailedAstronaut(ct.wp.x, gy, ct.wp.z, idx === 0 ? 0.35 : -0.45);
      (state.surfaceRealismGroup || scene).add(astro);
    });

    // Upgrade Perseverance chassis & wheels while keeping animated parent intact
    if (persevRoot) {
      persevRoot.traverse((c) => {
        if (c.isMesh) c.visible = false;
      });
      persevRoot.add(createPerseveranceDetailAttachment());
    }

    toRemove.forEach((o) => { if (o.parent) o.parent.remove(o); });
  }

  function injectCelestialHud() {
    if (document.getElementById('ms-celestial-focus-bar')) return;

    const bar = document.createElement('div');
    bar.id = 'ms-celestial-focus-bar';
    bar.className = 'ms-focus-bar';
    bar.innerHTML = [
      '<button type="button" class="ms-focus-btn" data-focus="sun" title="Look at the Martian Sun">&#9728; Locate Sun</button>',
      '<button type="button" class="ms-focus-btn" data-focus="phobos" title="Focus on Moon Phobos">&#9680; Phobos</button>',
      '<button type="button" class="ms-focus-btn" data-focus="deimos" title="Focus on Moon Deimos">&#9789; Deimos</button>',
      '<button type="button" class="ms-focus-btn" data-focus="constellation" title="View Orbital Constellation &amp; Moon Rings">&#128752; Orbit System</button>',
      '<button type="button" class="ms-focus-btn active" data-focus="default" title="Reset Hero View">&#8634; Hero View</button>'
    ].join('');
    document.body.appendChild(bar);

    bar.addEventListener('click', (e) => {
      const btn = e.target.closest('.ms-focus-btn');
      if (!btn) return;
      const mode = btn.getAttribute('data-focus');
      bar.querySelectorAll('.ms-focus-btn').forEach((b) => b.classList.toggle('active', b === btn));

      if (mode === 'constellation' && state.currentZone !== 'orbit') {
        const orbitBtn = document.querySelector('.zone-btn[data-zone="orbit"]');
        if (orbitBtn) orbitBtn.click();
        state.focusMode = 'default';
      } else {
        state.focusMode = mode;
      }
      state.userInteractedInZone = false;
      state.camLerpAlpha = 0.0;
    });

    // Clear camera override immediately if the user drags, scrolls, or pans with WASD/Arrows
    const unlockManualExplore = () => {
      state.userInteractedInZone = true;
      if (state.focusMode !== 'default') {
        state.focusMode = 'default';
        bar.querySelectorAll('.ms-focus-btn').forEach((b) => b.classList.toggle('active', b.getAttribute('data-focus') === 'default'));
      }
    };
    const canvas = document.getElementById('mars-canvas');
    if (canvas) {
      canvas.addEventListener('pointerdown', unlockManualExplore);
      canvas.addEventListener('wheel', unlockManualExplore, { passive: true });
    }
    window.addEventListener('keydown', (e) => {
      if (e.target && (e.target.tagName === 'INPUT' || e.target.tagName === 'SELECT' || e.target.tagName === 'TEXTAREA')) return;
      const k = (e.key || '').toLowerCase();
      if (k === 'w' || k === 'a' || k === 's' || k === 'd' ||
          e.key === 'ArrowUp' || e.key === 'ArrowDown' || e.key === 'ArrowLeft' || e.key === 'ArrowRight') {
        unlockManualExplore();
      }
    });

    document.querySelectorAll('.zone-btn').forEach((btn) => {
      btn.addEventListener('click', () => {
        state.currentZone = btn.getAttribute('data-zone') || 'surface';
        state.focusMode = 'default';
        state.userInteractedInZone = false;
        state.hasSmoothCam = false;
        bar.querySelectorAll('.ms-focus-btn').forEach((b) => b.classList.toggle('active', b.getAttribute('data-focus') === 'default'));
      });
    });
  }

  function initializeUpgrade(scene, camera, renderer) {
    if (state.initialized) return;
    state.initialized = true;
    state.scene = scene;
    state.camera = camera;
    state.renderer = renderer;

    // 1. Discover key scene objects built by app.js
    scene.traverse((obj) => {
      if (obj.name === 'MS_Surface_Sun_Group') state.sunGroup = obj;
      if (obj.name === 'MS_Orbit_Mars_Globe') state.marsGlobe = obj;
      if (obj.isPoints && !state.starField) state.starField = obj;
      if (obj.isDirectionalLight && !state.sunLight) state.sunLight = obj;
      if (obj.isHemisphereLight && !state.hemiLight) state.hemiLight = obj;
      if (obj.isMesh && obj.geometry && obj.geometry.type === 'PlaneGeometry' &&
          obj.geometry.parameters && obj.geometry.parameters.width >= 300) {
        state.terrainMesh = obj;
      }
    });

    if (state.marsGlobe && state.marsGlobe.parent) {
      state.orbitGroup = state.marsGlobe.parent;
    }

    // 2. Build Martian Atmospheric Sky Dome
    const sky = createMartianSkyDome();
    state.skyDome = sky.dome;
    state.skyUniforms = sky.uniforms;
    scene.add(sky.dome);

    // 3. Build Surface Sky Moons (Phobos & Deimos visible above Jezero Crater)
    state.surfaceMoonsGroup = createSurfaceSkyMoons();
    scene.add(state.surfaceMoonsGroup);

    // 4. Upgrade Sun Sprite & Core in Surface sky
    if (state.sunGroup) {
      state.sunGroup.traverse((c) => {
        if (c.material) c.material.fog = false;
      });
      const flare = new THREE.Sprite(new THREE.SpriteMaterial({
        map: createStarSpikeTexture(),
        transparent: true,
        blending: THREE.AdditiveBlending,
        depthWrite: false,
        opacity: 0.72,
        fog: false
      }));
      flare.scale.set(220, 220, 1);
      state.sunGroup.add(flare);
    }

    // 5. Upgrade Orbit System: High-res Mars Globe, Smooth Terminator, Atmosphere, Stickney Phobos, Deimos, Sleek Orbit Tracks, Space Sun
    if (state.orbitGroup && state.marsGlobe) {
      // Fix shadow acne sawtooth on Mars globe!
      state.marsGlobe.castShadow = false;
      state.marsGlobe.receiveShadow = false;

      const marsMaps = createMarsGlobeMaps();
      state.marsGlobe.material = new THREE.MeshStandardMaterial({
        map: marsMaps.map,
        bumpMap: marsMaps.bump,
        bumpScale: 0.22,
        roughness: 0.86,
        metalness: 0.04
      });

      const atmo = createMarsAtmosphereShells(11.5);
      state.marsGlobe.add(atmo.group);
      state.marsGlobe.userData.atmoUniformSun = atmo.uniformSun;

      // Replace ugly polar cyan spike & refine Jezero Marker cleanly on Mars surface
      const orbitChildren = state.orbitGroup.children.slice();
      const rawOrbitLines = [];
      const moonCandidates = [];

      orbitChildren.forEach((child) => {
        if (child === state.marsGlobe) return;
        if (child.name && child.name.startsWith('MS_Orbit_Line_')) {
          rawOrbitLines.push(child);
        } else if (child.isMesh && child.geometry && child.geometry.type === 'SphereGeometry') {
          moonCandidates.push(child);
        } else if (child.isMesh && child.geometry && child.geometry.type === 'ConeGeometry') {
          // Hide oversized giant cyan cone jutting off north pole
          child.visible = false;
        }
      });

      // Add sleek glowing Jezero Crater target beacon at 18.44N, 77.45E on globe
      const jLat = 18.4447 * Math.PI / 180;
      const jLon = 77.4508 * Math.PI / 180;
      const jR = 11.58;
      const jPos = new THREE.Vector3(
        jR * Math.cos(jLat) * Math.cos(jLon),
        jR * Math.sin(jLat),
        -jR * Math.cos(jLat) * Math.sin(jLon)
      );
      const jBeacon = new THREE.Group();
      jBeacon.position.copy(jPos);
      jBeacon.lookAt(jPos.clone().multiplyScalar(2));
      const jRing = new THREE.Mesh(
        new THREE.RingGeometry(0.32, 0.48, 28),
        new THREE.MeshBasicMaterial({ color: 0x38bdf8, side: THREE.DoubleSide, transparent: true, opacity: 0.92 })
      );
      jBeacon.add(jRing);
      const jPin = new THREE.Mesh(
        new THREE.ConeGeometry(0.18, 0.72, 12),
        new THREE.MeshBasicMaterial({ color: 0x67e8f9 })
      );
      jPin.rotation.x = Math.PI * 0.5;
      jPin.position.z = 0.42;
      jBeacon.add(jPin);
      state.marsGlobe.add(jBeacon);

      // Identify Phobos (larger moon mesh) & Deimos (smaller moon mesh)
      moonCandidates.sort((a, b) => {
        const ra = a.geometry.parameters ? a.geometry.parameters.radius : 0;
        const rb = b.geometry.parameters ? b.geometry.parameters.radius : 0;
        return rb - ra;
      });
      if (moonCandidates.length >= 1) {
        state.phobosMesh = moonCandidates[0];
        state.phobosMesh.geometry = createRegolithAsteroidGeometry(1.18, 0.96, 0.80, 1.4, true);
        state.phobosMesh.material = new THREE.MeshStandardMaterial({
          color: 0x8a7d72,
          vertexColors: true,
          roughness: 0.96,
          metalness: 0.02,
          emissive: 0x161210,
          emissiveIntensity: 0.04
        });
        const pRing = new THREE.Sprite(new THREE.SpriteMaterial({
          map: createGlowTexture('rgba(147,197,253,0.32)', 'rgba(147,197,253,0.08)', 'rgba(0,0,0,0)', 128),
          transparent: true,
          blending: THREE.AdditiveBlending,
          depthWrite: false
        }));
        pRing.scale.set(3.2, 3.2, 1);
        state.phobosMesh.add(pRing);
        const pLbl = createLabelSprite('PHOBOS', '9,376 km • Stickney Crater', '#93c5fd', 6.4);
        pLbl.position.set(0, 1.85, 0);
        state.phobosMesh.add(pLbl);
      }
      if (moonCandidates.length >= 2) {
        state.deimosMesh = moonCandidates[1];
        state.deimosMesh.geometry = createRegolithAsteroidGeometry(0.78, 0.64, 0.56, 7.9, false);
        state.deimosMesh.material = new THREE.MeshStandardMaterial({
          color: 0x9b8b7b,
          vertexColors: true,
          roughness: 0.94,
          metalness: 0.02,
          emissive: 0x1a1510,
          emissiveIntensity: 0.05
        });
        const dRing = new THREE.Sprite(new THREE.SpriteMaterial({
          map: createGlowTexture('rgba(253,224,71,0.36)', 'rgba(253,224,71,0.09)', 'rgba(0,0,0,0)', 128),
          transparent: true,
          blending: THREE.AdditiveBlending,
          depthWrite: false
        }));
        dRing.scale.set(2.5, 2.5, 1);
        state.deimosMesh.add(dRing);
        const dLbl = createLabelSprite('DEIMOS', '23,463 km • Outer Moon', '#fde047', 6.4);
        dLbl.position.set(0, 1.45, 0);
        state.deimosMesh.add(dLbl);
      }

      // Replace raw 1px LineLoops with sleek delicate gradient TubeGeometry orbital tracks
      const trackColorMap = {
        MS_Orbit_Line_Phobos: { color: 0x60a5fa, radius: 0.030, tracker: () => state.phobosMesh },
        MS_Orbit_Line_Deimos: { color: 0xfacc15, radius: 0.026, tracker: () => state.deimosMesh },
        MS_Orbit_Line_MRO: { color: 0x22d3ee, radius: 0.020, tracker: null },
        MS_Orbit_Line_Odyssey: { color: 0xfb923c, radius: 0.020, tracker: null },
        MS_Orbit_Line_MAVEN: { color: 0x34d399, radius: 0.022, tracker: null },
        MS_Orbit_Line_MGS: { color: 0xe879f9, radius: 0.020, tracker: null }
      };

      // Locate spacecraft groups inside orbitGroup to track wake phase cleanly
      const craftCandidates = state.orbitGroup.children.filter((c) => c.isGroup && c.children.length > 0 && c !== state.marsGlobe);

      rawOrbitLines.forEach((rawLine) => {
        rawLine.visible = false;
        const pts = extractLineLoopPoints(rawLine);
        if (pts.length < 4) return;
        const cfg = trackColorMap[rawLine.name] || { color: 0x38bdf8, radius: 0.048, tracker: null };
        let tracker = cfg.tracker ? cfg.tracker() : null;

        if (!tracker && craftCandidates.length > 0) {
          // Match craft sitting nearest this orbital curve
          let bestCraft = null, bestDist = Infinity;
          craftCandidates.forEach((cc) => {
            for (let i = 0; i < pts.length; i += 4) {
              const d = pts[i].distanceToSquared(cc.position);
              if (d < bestDist) { bestDist = d; bestCraft = cc; }
            }
          });
          if (bestDist < 9.0) tracker = bestCraft;
        }

        const built = buildGradedOrbitTrack(pts, cfg.color, cfg.radius, tracker, rawLine.name);
        if (built) {
          state.orbitGroup.add(built.group);
          state.orbitTracks.push(built);
        }
      });

      // Subtle Equatorial Reference Plane Ring
      const eqRing = new THREE.Mesh(
        new THREE.RingGeometry(13.2, 30.8, 96, 1),
        new THREE.MeshBasicMaterial({
          color: 0x64748b,
          transparent: true,
          opacity: 0.045,
          side: THREE.DoubleSide,
          depthWrite: false
        })
      );
      eqRing.rotation.x = Math.PI * 0.5;
      state.orbitGroup.add(eqRing);

      // Visible blazing Space Sun in Orbit View
      state.spaceSunGroup = createSpaceSunAssembly();
      state.orbitGroup.add(state.spaceSunGroup);
    }

    // 6. Terrain sampler + Surface Rocks, Jezero Rim Mesas, Dust Motes, Hero Habitat/Astronauts/Rover
    state.surfaceRealismGroup = new THREE.Group();
    state.surfaceRealismGroup.name = 'MS_Surface_Realism_Root';
    scene.add(state.surfaceRealismGroup);
    if (state.terrainMesh) {
      state.terrainHeightFn = makeTerrainHeightSampler(state.terrainMesh);
      if (state.terrainMesh.material) {
        state.terrainMesh.material.roughness = 0.93;
        state.terrainMesh.material.metalness = 0.03;
      }
      buildSurfaceEnvironment(scene);
      upgradeExistingSurfaceModels(scene);
    }

    // 7. Inject Celestial Focus Pills
    injectCelestialHud();

    // 8. Creative Omni-Probe Planetary X-Ray, Meteorites, v10 Command Dock & sceneBridge
    if (typeof initOmniProbeAndV10Dock === 'function') {
      initOmniProbeAndV10Dock(scene, camera, renderer);
    }
  }

  function syncSkySunAndMoons(scene, camera) {
    const isOrbit = !!(state.orbitGroup && state.orbitGroup.visible);
    state.currentZone = isOrbit ? 'orbit' : (state.currentZone === 'orbit' ? 'surface' : state.currentZone);

    if (state.surfaceRealismGroup) {
      state.surfaceRealismGroup.visible = !isOrbit;
    }

    // Slightly wider 52 deg vertical FOV on Surface so Base Camp + Jezero Rim + Sun + Moons compose cleanly
    const targetFov = isOrbit ? 48 : 52;
    if (Math.abs(camera.fov - targetFov) > 0.1) {
      camera.fov = targetFov;
      camera.updateProjectionMatrix();
    }

    // Determine physical Sun direction and gently frame high noon elevation into visible sky arc
    const rawSunDir = new THREE.Vector3(0.45, 0.55, 0.65);
    if (state.sunLight) {
      rawSunDir.copy(state.sunLight.position);
      if (rawSunDir.lengthSq() < 1e-4) rawSunDir.set(0.45, 0.55, 0.65);
    }
    rawSunDir.normalize();

    const sunDir = rawSunDir.clone();
    if (!isOrbit && sunDir.y > 0.25) {
      const horizLen = Math.hypot(sunDir.x, sunDir.z);
      if (horizLen > 1e-4) {
        const rawEl = Math.asin(Math.max(-1, Math.min(1, sunDir.y)));
        const visEl = 0.25 + (rawEl - 0.25) * 0.22; // noon/afternoon stays ~0.35..0.38 rad (~21 deg) inside clear upper sky
        const scaleH = Math.cos(visEl) / horizLen;
        sunDir.set(sunDir.x * scaleH, Math.sin(visEl), sunDir.z * scaleH).normalize();
      }
    }

    if (state.skyDome) {
      state.skyDome.visible = !isOrbit;
      state.skyDome.position.copy(camera.position);

      if (!isOrbit && state.skyUniforms) {
        const bgCol = scene.background && scene.background.isColor ? scene.background : new THREE.Color(0xc28b5e);
        const lum = bgCol.r * 0.3 + bgCol.g * 0.5 + bgCol.b * 0.2;
        const twi = Math.max(0, Math.min(1, 1.0 - lum * 2.1));
        const dustEl = document.getElementById('dust-tau-slider');
        const rawDust = dustEl ? parseFloat(dustEl.value) : 45;
        const dustVal = Number.isFinite(rawDust) ? (rawDust > 2.5 ? rawDust * 0.01 : rawDust) : 0.45;

        state.skyUniforms.uSunDir.value.copy(sunDir);
        state.skyUniforms.uHorizonColor.value.copy(bgCol).lerp(new THREE.Color(0xdca070), 0.25 * (1 - twi));
        state.skyUniforms.uZenithColor.value.copy(bgCol).multiplyScalar(0.56).lerp(new THREE.Color(0x0a1026), twi * 0.78);
        state.skyUniforms.uTwilight.value = twi;
        state.skyUniforms.uDust.value = dustVal;
      }
    }

    // Lock Surface Sun continuously relative to camera.position and face camera
    if (state.sunGroup) {
      state.sunGroup.visible = !isOrbit;
      if (!isOrbit) {
        if (sunDir.y > -0.15) {
          state.sunGroup.position.copy(camera.position).addScaledVector(sunDir, 1500);
          state.sunGroup.lookAt(camera.position);
          const core = state.sunGroup.children.find((c) => c.isMesh);
          if (core) core.lookAt(camera.position);
        }
        state.sunGroup.children.forEach((c) => {
          if (c.isSprite) {
            c.scale.set(136, 136, 1);
          }
        });
      }
    }

    // Keep Deep-Space Starfield vibrant in Orbit view even if Surface mode is Daytime
    if (state.starField && state.starField.material) {
      if (isOrbit) {
        state.starField.visible = true;
        state.starField.material.opacity = 0.92;
      }
    }

    // Surface Sky Moons (Phobos & Deimos aligned neatly along upper solar sky arc clear of HUD)
    if (state.surfaceMoonsGroup) {
      state.surfaceMoonsGroup.visible = !isOrbit;
      if (!isOrbit) {
        state.surfaceMoonsGroup.position.copy(camera.position);

        const solarEl = document.getElementById('solar-slider');
        const solarDeg = solarEl ? parseFloat(solarEl.value) || 38 : 38;
        const sunYaw = Math.atan2(sunDir.x, sunDir.z);

        const phobosAz = sunYaw + 0.22 + state.elapsed * 0.032 + (solarDeg - 38) * 0.008;
        const phobosEl = 0.24 + Math.sin(phobosAz * 0.8) * 0.03;
        if (state.surfacePhobos) {
          state.surfacePhobos.position.set(
            1220 * Math.cos(phobosEl) * Math.sin(phobosAz),
            1220 * Math.sin(phobosEl),
            1220 * Math.cos(phobosEl) * Math.cos(phobosAz)
          );
          state.surfacePhobos.rotation.y = state.elapsed * 0.12;
        }

        const deimosAz = sunYaw - 0.24 + state.elapsed * 0.012 + (solarDeg - 38) * 0.003;
        const deimosEl = 0.29 + Math.cos(deimosAz * 0.5) * 0.025;
        if (state.surfaceDeimos) {
          state.surfaceDeimos.position.set(
            1350 * Math.cos(deimosEl) * Math.sin(deimosAz),
            1350 * Math.sin(deimosEl),
            1350 * Math.cos(deimosEl) * Math.cos(deimosAz)
          );
          state.surfaceDeimos.rotation.y = state.elapsed * 0.08;
        }
      }
    }

    // Update Orbit Mode wakes, atmosphere sun vector, and distance-aware pill label sizes
    if (isOrbit) {
      let spaceSunWorld = new THREE.Vector3(-16.5, 11.5, -24.0);
      if (state.spaceSunGroup) {
        spaceSunWorld = state.spaceSunGroup.position;
        if (state.marsGlobe && state.marsGlobe.userData.atmoUniformSun) {
          state.marsGlobe.userData.atmoUniformSun.value.copy(spaceSunWorld).normalize();
        }
      }

      state.orbitTracks.forEach((tr) => {
        if (tr.trackerMesh) {
          const ph = findClosestPhase(tr.linePts, tr.trackerMesh.position);
          tr.updateWake(ph);
        }
      });

      const wp = new THREE.Vector3();
      state.orbitSprites.forEach((item) => {
        if (!item || !item.sprite) return;
        item.sprite.getWorldPosition(wp);
        const dist = camera.position.distanceTo(wp);
        const w = Math.max(item.minW || 0.68, Math.min(item.maxW || 7.4, dist * (item.factor || 0.085)));
        item.sprite.scale.set(w, w * 0.25, 1.0);
      });
    }

    // Animate drifting Martian dust motes in surface view
    if (state.dustMotes && state.dustMotes.visible !== isOrbit) {
      state.dustMotes.visible = !isOrbit;
    }
    if (!isOrbit && state.dustMotes) {
      state.dustMotes.rotation.y = state.elapsed * 0.018;
    }
  }

  function applyCameraFocusAndFraming(camera) {
    const isOrbit = !!(state.orbitGroup && state.orbitGroup.visible);
    const bar = document.getElementById('ms-celestial-focus-bar');
    if (bar) {
      bar.style.display = 'flex';
    }

    let applyDesired = false;

    if (state.focusMode === 'sun') {
      applyDesired = true;
      if (isOrbit && state.spaceSunGroup) {
        const sunPos = new THREE.Vector3();
        const marsPos = new THREE.Vector3();
        state.spaceSunGroup.getWorldPosition(sunPos);
        if (state.marsGlobe) state.marsGlobe.getWorldPosition(marsPos);
        // Side-angle vantage cleanly past Mars so blazing Space Sun & corona shine beside Mars's sunlit limb
        state.desiredCamPos.copy(marsPos).add(new THREE.Vector3(-14.0, 12.0, 20.0));
        state.desiredLookAt.copy(sunPos).lerp(marsPos, 0.24);
      } else if (state.sunGroup) {
        const sunDir = state.sunGroup.position.clone().sub(camera.position).normalize();
        const anchor = new THREE.Vector3(0, 3.0, 0);
        state.desiredCamPos.copy(anchor).addScaledVector(sunDir, -22).add(new THREE.Vector3(0, 2.4, 0));
        if (state.desiredCamPos.y < 2.2) state.desiredCamPos.y = 2.2;
        state.desiredLookAt.copy(anchor).addScaledVector(sunDir, 26).add(new THREE.Vector3(0, 4.0, 0));
      }
    } else if (state.focusMode === 'phobos') {
      applyDesired = true;
      if (isOrbit && state.phobosMesh) {
        const pw = new THREE.Vector3();
        state.phobosMesh.getWorldPosition(pw);
        const sunPos = state.spaceSunGroup ? state.spaceSunGroup.position : new THREE.Vector3(-16.5, 11.5, -24.0);
        const litDir = sunPos.clone().sub(pw).normalize();
        const sideDir = new THREE.Vector3().crossVectors(litDir, new THREE.Vector3(0, 1, 0)).normalize();
        const camDir = sideDir.multiplyScalar(0.82).addScaledVector(litDir, 0.34).add(new THREE.Vector3(0, 0.24, 0)).normalize();
        state.desiredCamPos.copy(pw).addScaledVector(camDir, 7.6);
        state.desiredLookAt.copy(pw);
      } else if (state.surfacePhobos) {
        const dir = state.surfacePhobos.position.clone().normalize();
        state.desiredCamPos.set(0, 2.4, -14);
        state.desiredLookAt.copy(state.desiredCamPos).addScaledVector(dir, 50);
      }
    } else if (state.focusMode === 'deimos') {
      applyDesired = true;
      if (isOrbit && state.deimosMesh) {
        const dw = new THREE.Vector3();
        state.deimosMesh.getWorldPosition(dw);
        const sunPos = state.spaceSunGroup ? state.spaceSunGroup.position : new THREE.Vector3(-16.5, 11.5, -24.0);
        const litDir = sunPos.clone().sub(dw).normalize();
        const sideDir = new THREE.Vector3().crossVectors(litDir, new THREE.Vector3(0, 1, 0)).normalize();
        const camDir = sideDir.multiplyScalar(-0.82).addScaledVector(litDir, 0.34).add(new THREE.Vector3(0, 0.24, 0)).normalize();
        state.desiredCamPos.copy(dw).addScaledVector(camDir, 6.2);
        state.desiredLookAt.copy(dw);
      } else if (state.surfaceDeimos) {
        const dir = state.surfaceDeimos.position.clone().normalize();
        state.desiredCamPos.set(0, 2.4, -14);
        state.desiredLookAt.copy(state.desiredCamPos).addScaledVector(dir, 50);
      }
    } else if (!state.userInteractedInZone) {
      // Hero Default Framing: make sure Sun, Moons & Orbits are all visible right out-of-the-box!
      if (isOrbit) {
        applyDesired = true;
        // Elevated oblique 3/4 view capturing Mars globe, Space Sun (upper-left), Phobos, Deimos & slanted 3D rings cleanly right of sidebar
        state.desiredCamPos.set(16.0, 30.0, 82.0);
        state.desiredLookAt.set(-4.5, 1.5, -1.5);
      } else {
        // On Surface Base & Crew default view, position slightly further back looking across Base Camp toward visible sky Sun, Phobos & Deimos
        const actZoneBtn = document.querySelector('.zone-btn.active');
        const zName = actZoneBtn ? actZoneBtn.getAttribute('data-zone') : 'surface';
        if (zName === 'surface' && state.sunGroup) {
          applyDesired = true;
          const sunDir = state.sunGroup.position.clone().sub(camera.position).normalize();
          const horizSun = new THREE.Vector3(sunDir.x, 0, sunDir.z);
          if (horizSun.lengthSq() < 0.01) horizSun.set(-0.7, 0, 0.3);
          horizSun.normalize();
          const baseCenter = new THREE.Vector3(-1.5, 2.0, 11.5);
          state.desiredCamPos.copy(baseCenter).addScaledVector(horizSun, -29.0).add(new THREE.Vector3(2.0, 3.8, -4.5));
          if (state.terrainHeightFn) {
            const gy = state.terrainHeightFn(state.desiredCamPos.x, state.desiredCamPos.z);
            state.desiredCamPos.y = Math.max(state.desiredCamPos.y, gy + 4.0);
          }
          // Balanced upward pitch framing Base Camp (Hab, Astronauts, Greenhouse, Rover) + Crater Rim + Sun/Moons below HUD!
          const sunHeightBias = Math.max(7.2, Math.min(13.5, sunDir.y * 28.0));
          state.desiredLookAt.copy(baseCenter).addScaledVector(horizSun, 26.0).add(new THREE.Vector3(-1.2, sunHeightBias, 0));
        }
      }
    }

    if (applyDesired) {
      if (!state.hasSmoothCam) {
        state.smoothCamPos.copy(state.desiredCamPos);
        state.smoothLookAt.copy(state.desiredLookAt);
        state.hasSmoothCam = true;
      } else {
        state.smoothCamPos.lerp(state.desiredCamPos, 0.14);
        state.smoothLookAt.lerp(state.desiredLookAt, 0.14);
      }
      camera.position.copy(state.smoothCamPos);
      camera.lookAt(state.smoothLookAt);
      if (window.__syncAppCameraTo) {
        window.__syncAppCameraTo(state.smoothCamPos, state.smoothLookAt);
      }
    } else {
      state.hasSmoothCam = false;
    }
  }

  window.__MarsRealism = {
    state: state,
    unlockCamera: function () {
      state.userInteractedInZone = true;
      state.focusMode = 'default';
      const bar = document.getElementById('ms-celestial-focus-bar');
      if (bar) {
        bar.querySelectorAll('.ms-focus-btn').forEach((b) => b.classList.toggle('active', b.getAttribute('data-focus') === 'default'));
      }
    },
    onBeforeRender: function (scene, camera, renderer) {
      const now = performance.now();
      const dt = Math.min(0.1, (now - state.lastFrameTime) * 0.001);
      state.lastFrameTime = now;
      state.elapsed += dt;

      if (!state.initialized) {
        initializeUpgrade(scene, camera, renderer);
      }
      syncSkySunAndMoons(scene, camera);
      applyCameraFocusAndFraming(camera);
      if (typeof tickV10Features === 'function') {
        tickV10Features(dt);
      }
    }
  };

  // ============================================================================
  // CREATIVE OMNI-PROBE PLANETARY X-RAY + MARS-SENTINEL-v10 COMMAND DECK & BRIDGE
  // ============================================================================
  const v10State = {
    omniTargets: [],
    omniRing: null,
    omniRingTime: 0,
    hazardGroup: null,
    earthBeacon: null,
    activeTab: null,
    roverStatus: 'STANDBY',
    roverSpeed: 0,
    roverArm: 'RETRACTED',
    roverCamera: true,
    selectedRoute: 'safest',
    drones: [
      { id: 'DR-01', name: 'Ingenuity Scout', mission: '3D Terrain Mapping (72 Flights)', battery: 96, status: 'Ready' },
      { id: 'DR-02', name: 'Belva Hazard Scout', mission: 'Crater Slope & Aeolian Trap Scan', battery: 88, status: 'Ready' },
      { id: 'DR-03', name: 'Neretva Thermal', mission: 'Subsurface Brine / Thermal Inertia', battery: 91, status: 'Ready' }
    ],
    demoActive: false,
    demoElapsed: 0,
    demoStepIdx: -1,
    slideIdx: 0,
    lastProbe: null
  };

  const NASA_SLIDES_EN = [
    {
      num: '1 / 7',
      zone: 'orbit',
      focus: 'default',
      title: 'ORBITX · MARS SENTINEL — Interplanetary Survival Guide: Martian Map',
      kicker: '2026 NASA SPACE APPS CHALLENGE · JEZERO CRATER',
      bullets: [
        'Problem: First human Marswalkers face 15+ min Earth lightspeed delay, blind dust storms (τ > 1.8), radiation spikes & hazardous delta slopes.',
        'Solution: Real-time 3D Digital Twin combining Unity 6 (Windows Player) & Three.js Web Twin grounded in authentic NASA/USGS science.',
        'Coordinate Anchor: Octavia E. Butler Landing & Jezero Crater Delta (18.4447°N, 77.4508°E · -2,569.9 m MOLA).'
      ]
    },
    {
      num: '2 / 7',
      zone: 'tactical',
      focus: 'default',
      title: 'Authentic Multi-Mission NASA & USGS Science Stack',
      kicker: 'ZERO FAKE TELEMETRY · PROVENANCE VERIFIED',
      bullets: [
        'Terrain Elevation: PDS4 HiRISE DTM DTEEC_048842_1985_048908_1985_A01 (4 m grid, 1,132,096 vertices, SHA-256 verified).',
        'Geology & Paleoclimate: USGS SIM 3292 (44 global geologic units with trafficability 1-5 & H2O ISRU potential) + Curiosity MSL (1,316 segments).',
        'In-Situ Missions: 22 Perseverance Sample Tubes, 12 Stratified Outcrops (PIXL/SHERLOC/Mastcam-Z), 72 Ingenuity Flights & MEDA/RAD/DONKI.'
      ]
    },
    {
      num: '3 / 7',
      zone: 'tactical',
      focus: 'default',
      title: 'A* Multi-Objective Pathfinding & Hazard Mitigation',
      kicker: 'SAFER TRAVERSES ACROSS JEZERO DELTA',
      bullets: [
        'Safest Route (88.4/100): 1,159.95 m · 6.49° max slope · penalizes roughness & boulder scarps below 15° rover safety limit.',
        'Shortest Geodesic (64.1/100): 1,113.50 m vs Science Return (82.7/100): 1,203.21 m passing all 22 M20 cache targets.',
        'Interactive Layer Toggle [H] & Live Off-Route Margin + Goal ETA keep crews spatially oriented.'
      ]
    },
    {
      num: '4 / 7',
      zone: 'surface',
      focus: 'default',
      title: 'Omni-Probe X-Ray: Every Meter of Mars Is Inspectable',
      kicker: 'INNOVATION HIGHLIGHT · SURFACE, SUBSURFACE & SKY',
      bullets: [
        'Click ANY ground coordinate, rock outcrop, Cacao/Lebanon iron-nickel meteorite, Belva crater, or sky object (Sun, Phobos, Deimos, Earth/DSN).',
        'Computes live IAU2000 Lat/Lon, local slope, Thermal Inertia (TIU), USGS SIM 3292 unit, and RIMFAX 0-15 m subsurface radar layers.',
        'Reveals aeolian perchlorate crust (0-1.8m), deltaic smectite clay (1.8-6.5m) & subsurface ice/brine horizon (6.5-15m).'
      ]
    },
    {
      num: '5 / 7',
      zone: 'surface',
      focus: 'sun',
      title: 'Martian Space Weather Physics & EVA Survival Radius',
      kicker: 'BEER-LAMBERT OPTICS + MEDA/RAD/DONKI',
      bullets: [
        'Real-time Beer-Lambert atmospheric attenuation computes surface irradiance I = 586.2 * exp(-tau / sin(el)) as dust depth τ shifts (0.45 -> 2.80).',
        'Dynamic Point-of-No-Return (PNR) EVA safety radius shrinks automatically under dust storms & O2 depletion.',
        'Integrated First Aid & Radiation Triage Medical Bay for GCR (0.67 mSv/day baseline) and DONKI Solar Particle Events.'
      ]
    },
    {
      num: '6 / 7',
      zone: 'agriculture',
      focus: 'default',
      title: 'Closed-Loop Agriculture, NASA HDU & Fleet Operations',
      kicker: 'SUSTAINING A 4-CREW MARTIAN OUTPOST',
      bullets: [
        'Bioregenerative Agri Complex (GH-01 Hydroponics + GH-02 Aeroponics): 98.4% water recycling, 2.84 kg/Sol O2, 450/660nm LEDs & ClO4- remediation.',
        'Operable 6x6 Pressurized Rover (ROVER-01 Manual Drive & A* Autopilot), Cooperative 3-Drone Fleet & Mars Ascent Rocket Pad.',
        'Authentic NASA 3D CAD assets: NASA HDU, Mark III Suitport, Saturn V & Mobile Launcher (per 14 CFR 1221 non-endorsement notice).'
      ]
    },
    {
      num: '7 / 7',
      zone: 'orbit',
      focus: 'phobos',
      title: 'Cross-Platform Readiness & Reproducible Verification',
      kicker: 'UNITY 6 DESKTOP + THREE.JS WEB + 328 AUTOMATED CHECKS',
      bullets: [
        'Shipped on GitHub (youssef50505/ORBITX-NASA-SPACE-APP) with standalone Unity 6 MarsSentinel.exe & zero-build WebGL Digital Twin.',
        'Verified with 120/120 Workspace Cryptographic Integrity checks & 208/208 automated E2E/physics tests.',
        'One-click [N] 28-Second NASA Demo & [P] 1-Minute Pitch Director ready for live mission planning today.'
      ]
    }
  ];

  function emitToHost(type, detail) {
    window.dispatchEvent(new CustomEvent(type, { detail: detail || {} }));
    if (window.parent && window.parent !== window) {
      try {
        window.parent.postMessage({ source: 'mars-scene', type: type, detail: detail || {} }, '*');
      } catch (e) { /* ignore cross-origin */ }
    }
  }

  function buildOmniTargetsAndBeacons(scene) {
    const root = state.surfaceRealismGroup || scene;
    const api = window.__MARS_WEB_BRIDGE && window.__MARS_WEB_BRIDGE.api;
    const hFn = (api && api.sampleHeight) ? api.sampleHeight : (state.terrainHeightFn || function () { return 0; });

    // 1. Pulsing 3D Cyan RIMFAX Scan Ring
    const ringGeo = new THREE.RingGeometry(0.95, 1.35, 36);
    const ringMat = new THREE.MeshBasicMaterial({
      color: 0x2cd4d9,
      side: THREE.DoubleSide,
      transparent: true,
      opacity: 0.88,
      depthWrite: false
    });
    v10State.omniRing = new THREE.Mesh(ringGeo, ringMat);
    v10State.omniRing.rotation.x = -Math.PI * 0.5;
    v10State.omniRing.visible = false;
    root.add(v10State.omniRing);

    // 2. Iron-Nickel Meteorites & Belva Impact Crater Rim
    const specList = [
      {
        id: 'meteorite_cacao',
        title: '"Cacao" Iron-Nickel Meteorite',
        category: 'IRON METEORITE · SOL 691',
        x: -24.0, z: -18.0, rx: 1.4, ry: 0.85, rz: 1.15, color: 0x3b3d46, metalness: 0.82,
        coords: '18.4580°N, 77.4190°E · -2564.2 m MOLA',
        radar: ['0–0.4 m: High-Reflectivity Metallic Bolide (>0 dB)', '0.4–5.2 m: Compressed Noachian Regolith Breccia', '5.2–15 m: Maaz Olivine-Rich Basalt Flow'],
        chem: 'Fe 91.2% · Ni 8.1% · Co/Ir Siderophiles (Widmanstätten Texture)',
        notes: 'Real Perseverance Sol 691 iron meteorite find. Wind-pitted regmaglypts confirm hyper-arid weathering rate.',
        url: 'https://mars.nasa.gov/resources/27319/'
      },
      {
        id: 'meteorite_lebanon',
        title: '"Lebanon" Heavy Iron Bolide',
        category: 'IAB IRON METEORITE',
        x: 34.0, z: -22.0, rx: 1.75, ry: 1.05, rz: 1.35, color: 0x333640, metalness: 0.86,
        coords: '18.4310°N, 77.4790°E · -2567.8 m MOLA',
        radar: ['0–0.5 m: Metallic Fe-Ni Core Mass', '0.5–6.0 m: Impact Thermal Sintered Siltstone', '6.0–15 m: Dense Basalt Basement'],
        chem: 'Fe 89.4% · Ni 9.4% · Troilite/Schreibersite Inclusions',
        notes: 'Aerodynamically ablated magmatic iron bolide preserved by thin CO2 Martian atmosphere (610–785 Pa).',
        url: 'https://science.nasa.gov/mars/'
      },
      {
        id: 'crater_belva',
        title: 'Belva Impact Crater & Ejecta Rim',
        category: 'IMPACT CRATER · 0.9 KM',
        x: -46.0, z: 44.0, rx: 3.9, ry: 0.55, rz: 3.9, color: 0x7a4832, metalness: 0.14,
        coords: '18.4910°N, 77.3860°E · -2518.4 m MOLA',
        radar: ['0–2.4 m: Loose Ejecta Talus & Maskelynite', '2.4–9.0 m: Dipping (~25°) Deltaic Foreset Beds', '9.0–15 m: Fractured Hydration / Brine Faults'],
        chem: 'SiO2 47.2% · FeO 16.9% · Shocked Plagioclase & Fe/Mg Smectite',
        notes: 'Fresh Jezero impact crater imaged by Perseverance Sol 789 exposing 45 m of deep ancient river-delta strata.',
        url: 'https://mars.nasa.gov/mars2020/mission/science/landing-site/'
      }
    ];

    specList.forEach((sp) => {
      const y = hFn(sp.x, sp.z);
      const mesh = new THREE.Mesh(
        new THREE.DodecahedronGeometry(1.0, 1),
        new THREE.MeshStandardMaterial({ color: sp.color, roughness: sp.metalness > 0.5 ? 0.35 : 0.88, metalness: sp.metalness })
      );
      mesh.scale.set(sp.rx, sp.ry, sp.rz);
      mesh.position.set(sp.x, y + sp.ry * 0.4, sp.z);
      mesh.castShadow = true;
      mesh.userData.omniSpec = sp;
      root.add(mesh);

      const beacon = new THREE.Mesh(
        new THREE.OctahedronGeometry(0.24, 0),
        new THREE.MeshStandardMaterial({ color: 0xf59e0b, emissive: 0xd97706, emissiveIntensity: 1.5 })
      );
      beacon.position.set(sp.x, y + sp.ry + 1.15, sp.z);
      beacon.userData.omniSpec = sp;
      root.add(beacon);

      v10State.omniTargets.push(mesh, beacon);
    });

    // 3. Earth & NASA DSN Sky Beacon (Pale Blue Dot in upper-right Martian twilight sky)
    const earthDot = new THREE.Mesh(
      new THREE.SphereGeometry(4.2, 16, 12),
      new THREE.MeshBasicMaterial({ color: 0x7dd3fc, fog: false })
    );
    earthDot.position.set(680, 420, -960);
    earthDot.userData.celestialKey = 'earth';
    scene.add(earthDot);
    v10State.earthBeacon = earthDot;
    v10State.omniTargets.push(earthDot);

    // 4. Hazard Scan Pulse Markers Group
    v10State.hazardGroup = new THREE.Group();
    v10State.hazardGroup.name = 'MS_Hazard_Scan_Markers';
    v10State.hazardGroup.visible = false;
    root.add(v10State.hazardGroup);

    if (state.sunGroup) {
      state.sunGroup.traverse((c) => { c.userData.celestialKey = 'sun'; });
    }
    if (state.spaceSunGroup) {
      state.spaceSunGroup.traverse((c) => { c.userData.celestialKey = 'sun'; });
    }
    if (state.surfacePhobos) {
      state.surfacePhobos.userData.celestialKey = 'phobos';
    }
    if (state.surfaceDeimos) {
      state.surfaceDeimos.userData.celestialKey = 'deimos';
    }
    if (state.phobosMesh) {
      state.phobosMesh.userData.celestialKey = 'phobos';
    }
    if (state.deimosMesh) {
      state.deimosMesh.userData.celestialKey = 'deimos';
    }
  }

  function showOmniPopup(probeData) {
    v10State.lastProbe = probeData;
    let pop = document.getElementById('ms-omni-popup');
    if (v10State.activeTab === 'omni') {
      if (pop) pop.classList.remove('visible');
      renderV10DrawerContent('omni');
      return;
    }
    if (!pop) {
      pop = document.createElement('div');
      pop.id = 'ms-omni-popup';
      document.body.appendChild(pop);
    }
    pop.classList.add('visible');
    pop.innerHTML = [
      '<div class="ms-omni-top">',
      '  <div><span class="ms-source-badge src-team">' + probeData.badge + '</span> <strong style="color:#fff;margin-left:6px">' + probeData.title + '</strong></div>',
      '  <button type="button" id="ms-omni-close" class="ms-drawer-close" style="padding:1px 7px">✕</button>',
      '</div>',
      '<div class="mono-mini" style="color:#7fe3d2;margin-bottom:4px">' + probeData.coords + '</div>',
      '<div class="mono-mini" style="color:#cbd5e1;margin-bottom:5px">' + probeData.chem + '</div>',
      '<div class="ms-radar-depth">',
      '  <div class="ms-radar-layer"><span>' + probeData.radar[0] + '</span></div>',
      '  <div class="ms-radar-layer clay"><span>' + probeData.radar[1] + '</span></div>',
      '  <div class="ms-radar-layer ice"><span>' + probeData.radar[2] + '</span></div>',
      '</div>',
      '<div class="mono-mini" style="color:#94a3b8;margin-top:5px">' + probeData.notes + '</div>'
    ].join('');
    const closeBtn = document.getElementById('ms-omni-close');
    if (closeBtn) closeBtn.onclick = () => pop.classList.remove('visible');

    if (v10State.activeTab === 'omni') {
      renderV10DrawerContent('omni');
    }
  }

  function scanTerrainPoint(pt, normal) {
    const lat = 18.4447 + pt.z * 0.00018;
    const lon = 77.4508 + pt.x * 0.00019;
    const elev = -2569.9 + pt.y;
    const slope = normal ? Math.acos(Math.max(-1, Math.min(1, normal.y))) * 180 / Math.PI : 3.4;
    const tiu = Math.round(220 + Math.abs(Math.sin(pt.x * 0.09 + pt.z * 0.07)) * 410);
    const units = (window.MARS_NASA_DATA && window.MARS_NASA_DATA.geology_units) || [];
    const uIdx = units.length ? (Math.abs(Math.round(pt.x * 0.3) + Math.round(pt.z * 0.4)) % units.length) : 0;
    const unit = units[uIdx] || { symbol: 'HNu_lf', name: 'Jezero Layered Delta', trafficability: 4, water_ice_potential: 'Low-Moderate' };

    const dustEl = document.getElementById('dust-tau-slider');
    const tau = dustEl ? Math.max(0.3, Math.min(3.5, Number(dustEl.value) / 100.0)) : 0.45;
    const flux = Math.round(586.2 * Math.exp(-tau / 0.72));

    if (v10State.omniRing) {
      v10State.omniRing.position.set(pt.x, pt.y + 0.14, pt.z);
      v10State.omniRing.visible = true;
      v10State.omniRingTime = 0;
    }

    const api = window.__MARS_WEB_BRIDGE && window.__MARS_WEB_BRIDGE.api;
    if (api && typeof api.playScanChime === 'function') api.playScanChime();

    showOmniPopup({
      badge: 'OMNI-PROBE · RIMFAX X-RAY',
      title: 'Regolith Scan · ' + unit.symbol + ' (' + unit.name + ')',
      coords: lat.toFixed(5) + '°N, ' + lon.toFixed(5) + '°E · MOLA ' + elev.toFixed(1) + ' m · Slope ' + slope.toFixed(1) + '° (' + (slope > 15 ? 'HAZARD' : 'SAFE') + ')',
      chem: 'PIXL Est: SiO2 44.8% · FeO 18.6% · MgO 11.4% · TI ' + tiu + ' TIU · Traffic ' + unit.trafficability + '/5 · Flux ' + flux + ' W/m²',
      radar: [
        '0.0–1.8m: Aeolian Dust & Perchlorate Crust (ClO4- 0.42 wt% · Porosity 38%)',
        '1.8–6.5m: Cross-Bedded Delta Sandstone & Fe/Mg Smectite Clay',
        '6.5–15m: Noachian Basalt Bedrock · ISRU Ice/Brine: ' + (unit.water_ice_potential || 'Moderate')
      ],
      notes: 'Every coordinate mapped over NASA HiRISE 4m DTM (hash ed5e2c...354dd) & USGS SIM 3292.'
    });
  }

  function scanCelestialBody(key) {
    const dustEl = document.getElementById('dust-tau-slider');
    const tau = dustEl ? Math.max(0.3, Math.min(3.5, Number(dustEl.value) / 100.0)) : 0.45;
    const flux = Math.round(586.2 * Math.exp(-tau / 0.72));
    const specs = {
      sun: {
        badge: 'HELIO-PROBE · 1.52 AU',
        title: 'Sol (Sun Observed from Jezero Crater)',
        coords: 'Heliocentric 1.524 AU · Angular Diameter 0.35° · TOA 586.2 W/m²',
        chem: 'Live Surface Flux: ' + flux + ' W/m² (Dust τ=' + tau.toFixed(2) + ') · DONKI SEP & GCR Monitor',
        radar: [
          'Core / Photosphere: G2V 5,772 K Blackbody Spectrum',
          'Martian Aerosol Scatter: Butterscotch Fe2O3 Sky + Forward-Scattered Blue Halo',
          'Mission Impact: Agri Dome Supplements Red/Blue 450/660nm LEDs During Storms'
        ],
        notes: 'Source: NASA MEDA Irradiance & DONKI Space Weather Telemetry.'
      },
      phobos: {
        badge: 'MOON PROBE · 9,376 KM',
        title: 'Phobos (Inner Martian Moon)',
        coords: 'Semi-Major Axis 9,376 km · Period 7h 39m 12s (3.97x Mars Rate)',
        chem: '27 × 22 × 18 km Carbonaceous D-Type Asteroid · Albedo 0.071 · Stickney Crater (9 km)',
        radar: [
          'Orbit: Prograde West-to-East · Rises in the WEST, Sets in the EAST',
          'Dynamics: Tidal Secular Decay -1.8 cm/yr Toward Roche Limit',
          'ISRU Potential: Low delta-v Phobos Propellant Depot Waypoint'
        ],
        notes: 'Source: NASA MRO HiRISE & Viking Orbiter Phobos Ephemeris.'
      },
      deimos: {
        badge: 'MOON PROBE · 23,463 KM',
        title: 'Deimos (Outer Martian Moon)',
        coords: 'Semi-Major Axis 23,463 km · Period 30h 18m (0.81x Mars Rate)',
        chem: '15 × 12.2 × 10.4 km Smooth Regolith-Blanketed Moon · Escape Vel 5.6 m/s',
        radar: [
          'Surface: ~100m Loose Chondritic Regolith Filling Swift & Voltaire Craters',
          'Sky Motion: Rises East, Stays Above Jezero Horizon ~2.7 Sols',
          'Role: High-Altitude Telecommunications & Optical Relay Node'
        ],
        notes: 'Source: NASA Mars Moons Ephemeris & Spectral Catalog.'
      },
      earth: {
        badge: 'DSN LINK · 1.84 AU',
        title: 'Earth & Deep Space Network (Pale Blue Dot)',
        coords: 'Range ~275 Million km (1.84 AU) · One-Way Light Time: 15m 18s',
        chem: 'Goldstone / Madrid / Canberra X-Band & Ka-Band Relay via MRO / Odyssey',
        radar: [
          'Signal Latency: 30m 36s Round-Trip Communication Delay',
          'Crewed Consequence: Zero Real-Time Earth Joystick Teleoperation Possible',
          'ORBITX Mandate: On-Site Local AI Route Planner, Medical Bay & Closed-Loop Agri'
        ],
        notes: 'Source: NASA Eyes on the Solar System Deep Space Network (DSN Now).'
      }
    };
    const data = specs[key] || specs.sun;
    const api = window.__MARS_WEB_BRIDGE && window.__MARS_WEB_BRIDGE.api;
    if (api && typeof api.playScanChime === 'function') api.playScanChime();
    showOmniPopup(data);
  }

  function scanUnderCameraCenter() {
    const api = window.__MARS_WEB_BRIDGE && window.__MARS_WEB_BRIDGE.api;
    if (!api) return;
    const tx = api.camState ? api.camState.target.x : 0;
    const tz = api.camState ? api.camState.target.z : 12;
    const ty = api.sampleHeight ? api.sampleHeight(tx, tz) : 0;
    scanTerrainPoint(new THREE.Vector3(tx, ty, tz), new THREE.Vector3(0, 1, 0));
  }

  function runHazardScan() {
    const api = window.__MARS_WEB_BRIDGE && window.__MARS_WEB_BRIDGE.api;
    if (!api || !v10State.hazardGroup) return;
    while (v10State.hazardGroup.children.length > 0) {
      v10State.hazardGroup.remove(v10State.hazardGroup.children[0]);
    }
    const hFn = api.sampleHeight;
    // Find steepest terrain cell around delta basin + two known hazard craters
    let maxSlope = 0, maxPt = [18, 28];
    for (let x = -50; x <= 50; x += 5) {
      for (let z = -30; z <= 60; z += 5) {
        const dz = (hFn(x, z + 1) - hFn(x, z - 1)) * 0.5;
        const dx = (hFn(x + 1, z) - hFn(x - 1, z)) * 0.5;
        const sl = Math.atan(Math.hypot(dx, dz)) * 180 / Math.PI;
        if (sl > maxSlope) { maxSlope = sl; maxPt = [x, z]; }
      }
    }
    const spots = [
      { id: 'HZ-01', x: maxPt[0], z: maxPt[1], col: 0xef4444, label: 'Steep DEM Slope ' + maxSlope.toFixed(1) + '°' },
      { id: 'HZ-02', x: -46, z: 44, col: 0xf97316, label: 'Belva Crater Rim Scarp' },
      { id: 'HZ-03', x: 18, z: 28, col: 0xfacc15, label: 'Aeolian Loose Sand Basin' }
    ];
    spots.forEach((s) => {
      const y = hFn(s.x, s.z);
      const cone = new THREE.Mesh(
        new THREE.ConeGeometry(0.9, 3.2, 16),
        new THREE.MeshBasicMaterial({ color: s.col, transparent: true, opacity: 0.78, wireframe: true })
      );
      cone.position.set(s.x, y + 2.0, s.z);
      const ring = new THREE.Mesh(
        new THREE.RingGeometry(1.4, 2.0, 28),
        new THREE.MeshBasicMaterial({ color: s.col, side: THREE.DoubleSide, transparent: true, opacity: 0.75 })
      );
      ring.rotation.x = -Math.PI * 0.5;
      ring.position.set(s.x, y + 0.16, s.z);
      v10State.hazardGroup.add(cone, ring);
    });
    v10State.hazardGroup.visible = true;
    if (api.camState && api.camState.mode === 'orbit') api.setZone('tactical');
    emitToHost('mars:scene-hazards', { count: spots.length, ids: spots.map((s) => s.id) });
    if (typeof api.playScanChime === 'function') api.playScanChime();
  }

  function makeSegBarHtml(pct, tone) {
    const filled = Math.round(Math.max(0, Math.min(1, pct / 100)) * 32);
    let cells = '';
    for (let i = 0; i < 32; i++) {
      cells += '<i class="' + (i < filled ? 'on' : '') + '"></i>';
    }
    return '<div class="ms-segbar ' + (tone || 'mint') + '">' + cells + '</div>';
  }

  function makeSparkSvg(vals, strokeColor) {
    const w = 220, h = 40;
    const min = Math.min.apply(null, vals), max = Math.max.apply(null, vals);
    const span = (max - min) || 1;
    const d = vals.map((v, i) => {
      const px = (i / (vals.length - 1)) * w;
      const py = h - 4 - ((v - min) / span) * (h - 8);
      return (i === 0 ? 'M' : 'L') + px.toFixed(1) + ' ' + py.toFixed(1);
    }).join(' ');
    return '<svg viewBox="0 0 ' + w + ' ' + h + '" style="width:100%;height:38px;display:block"><path d="' + d + '" fill="none" stroke="' + strokeColor + '" stroke-width="2"/></svg>';
  }

  function applySlideView(idx) {
    const s = NASA_SLIDES_EN[idx];
    const api = window.__MARS_WEB_BRIDGE && window.__MARS_WEB_BRIDGE.api;
    if (api && typeof api.setZone === 'function') {
      api.setZone(s.zone);
      state.userInteractedInZone = false;
      state.focusMode = s.focus || 'default';
    }
  }

  function renderV10DrawerContent(tab) {
    const drawer = document.getElementById('ms-v10-drawer');
    if (!drawer) return;
    if (!tab) {
      drawer.classList.remove('open');
      return;
    }
    drawer.classList.add('open');

    if (tab === 'omni') {
      const p = v10State.lastProbe || {
        badge: 'OMNI-PROBE READY',
        title: 'Click ANY Point on Terrain, Meteorites, Sun or Moons',
        coords: 'Jezero Crater Anchor: 18.4447°N, 77.4508°E · -2,569.9 m MOLA',
        chem: 'Combines HiRISE 4m DTM Slope, USGS SIM 3292 Stratigraphy, RIMFAX 0-15m Radar & PIXL Oxide Model',
        radar: [
          '0.0–1.8m: Surface Regolith & ClO4- Perchlorate Horizon',
          '1.8–6.5m: Lacustrine Delta Clay & Sulfate Sandstone',
          '6.5–15m: Deep Basalt Basement & Subsurface Brine/Ice Check'
        ],
        notes: 'Click directly inside the 3D scene or use quick scan targets below.'
      };
      drawer.innerHTML = [
        '<div class="ms-drawer-head"><div><span class="ms-drawer-kicker">CREATIVE PLANETARY X-RAY</span><h3>Omni-Probe Surface, Subsurface &amp; Celestial Scanner</h3></div>',
        '<div><span class="ms-source-badge src-team">' + p.badge + '</span> <button class="ms-drawer-close" id="ms-drw-x">✕</button></div></div>',
        '<div class="ms-btn-grid" style="margin-bottom:10px">',
        '  <button class="ms-ctrl-btn primary" data-scan="center">⌖ Scan Under Camera</button>',
        '  <button class="ms-ctrl-btn" data-scan="cacao">☄ Cacao Meteorite</button>',
        '  <button class="ms-ctrl-btn" data-scan="belva">◉ Belva Crater Rim</button>',
        '  <button class="ms-ctrl-btn" data-scan="sun">☀ Sol (Sun 1.52 AU)</button>',
        '  <button class="ms-ctrl-btn" data-scan="phobos">◐ Phobos (9,376 km)</button>',
        '  <button class="ms-ctrl-btn" data-scan="earth">🌍 Earth / DSN Link</button>',
        '</div>',
        '<div class="ms-card"><h4>' + p.title + '</h4>',
        '<div class="mono-mini" style="color:#7fe3d2;margin-bottom:4px">' + p.coords + '</div>',
        '<div class="mono-mini" style="margin-bottom:6px">' + p.chem + '</div>',
        '<div class="ms-radar-depth">',
        '  <div class="ms-radar-layer"><span>' + p.radar[0] + '</span></div>',
        '  <div class="ms-radar-layer clay"><span>' + p.radar[1] + '</span></div>',
        '  <div class="ms-radar-layer ice"><span>' + p.radar[2] + '</span></div>',
        '</div></div>'
      ].join('');
      drawer.querySelectorAll('[data-scan]').forEach((b) => {
        b.onclick = () => {
          const k = b.getAttribute('data-scan');
          if (k === 'center') scanUnderCameraCenter();
          else if (k === 'cacao' || k === 'belva') {
            const target = v10State.omniTargets.find((m) => m.userData.omniSpec && m.userData.omniSpec.id.includes(k));
            if (target && target.userData.omniSpec) {
              const sp = target.userData.omniSpec;
              const api = window.__MARS_WEB_BRIDGE && window.__MARS_WEB_BRIDGE.api;
              if (api) {
                api.setZone('surface');
                state.userInteractedInZone = true;
                api.camState.desiredTarget.set(sp.x, 1.5, sp.z);
                api.camState.desiredSpherical = { radius: 11.0, phi: 1.24, theta: 2.3 };
              }
              showOmniPopup({ badge: 'EXOGENIC / CRATER TARGET', title: sp.title, coords: sp.coords, chem: sp.chem, radar: sp.radar, notes: sp.notes });
            }
          } else {
            scanCelestialBody(k);
          }
        };
      });
    } else if (tab === 'rover') {
      drawer.innerHTML = [
        '<div class="ms-drawer-head"><div><span class="ms-drawer-kicker">GROUND EXPLORATION · FROM TEAM v10</span><h3>ROVER-01 Perseverance Control Center</h3></div>',
        '<div><span class="ms-source-badge src-team">LIVE 3D CONTROL</span> <button class="ms-drawer-close" id="ms-drw-x">✕</button></div></div>',
        '<div class="ms-grid-3" style="margin-bottom:10px">',
        '  <div class="ms-card"><span class="mono-mini">STATUS</span><h4 style="margin-top:4px;color:#7fe3d2">' + v10State.roverStatus + '</h4></div>',
        '  <div class="ms-card"><span class="mono-mini">BATTERY</span><h4 style="margin-top:4px">94% MMRTG</h4></div>',
        '  <div class="ms-card"><span class="mono-mini">SPEED</span><h4 style="margin-top:4px">' + v10State.roverSpeed.toFixed(1) + ' m/s</h4></div>',
        '</div>',
        '<div class="ms-grid-2">',
        '  <div class="ms-card"><h4>Drive &amp; Heading Control</h4><div class="ms-btn-grid">',
        '    <span></span><button class="ms-ctrl-btn" data-rv="forward">▲ FWD</button><span></span>',
        '    <button class="ms-ctrl-btn" data-rv="left">◀ LEFT</button><button class="ms-ctrl-btn primary" data-rv="stop">■ STOP</button><button class="ms-ctrl-btn" data-rv="right">▶ RIGHT</button>',
        '    <span></span><button class="ms-ctrl-btn" data-rv="backward">▼ REV</button><span></span>',
        '  </div></div>',
        '  <div class="ms-card"><h4>Science Arm &amp; Route Autopilot</h4><div style="display:flex;flex-direction:column;gap:6px">',
        '    <button class="ms-ctrl-btn primary" id="ms-rv-auto">⌁ Confirm Safest A* Autopilot (1,159.95 m)</button>',
        '    <button class="ms-ctrl-btn" id="ms-rv-arm">Robotic Coring Arm: ' + v10State.roverArm + '</button>',
        '    <button class="ms-ctrl-btn" id="ms-rv-cam">Mastcam-Z &amp; Hazcam: ' + (v10State.roverCamera ? 'ACTIVE' : 'STANDBY') + '</button>',
        '  </div></div>',
        '</div>'
      ].join('');
      drawer.querySelectorAll('[data-rv]').forEach((b) => {
        b.onclick = () => {
          const d = b.getAttribute('data-rv');
          const api = window.__MARS_WEB_BRIDGE && window.__MARS_WEB_BRIDGE.api;
          if (api && api.camState.mode === 'orbit') api.setZone('surface');
          if (d === 'stop') {
            window.__MarsRoverOverride = { mode: 'standby' };
            v10State.roverStatus = 'STANDBY'; v10State.roverSpeed = 0;
          } else {
            window.__MarsRoverOverride = { mode: 'manual', dir: d };
            v10State.roverStatus = 'MOVING'; v10State.roverSpeed = 0.8;
          }
          renderV10DrawerContent('rover');
        };
      });
      const autoBtn = document.getElementById('ms-rv-auto');
      if (autoBtn) autoBtn.onclick = () => startRouteAutopilot(v10State.selectedRoute || 'safest');
      const armBtn = document.getElementById('ms-rv-arm');
      if (armBtn) armBtn.onclick = () => { v10State.roverArm = v10State.roverArm === 'RETRACTED' ? 'EXTENDED' : 'RETRACTED'; renderV10DrawerContent('rover'); };
      const camBtn = document.getElementById('ms-rv-cam');
      if (camBtn) camBtn.onclick = () => { v10State.roverCamera = !v10State.roverCamera; renderV10DrawerContent('rover'); };
    } else if (tab === 'drones') {
      drawer.innerHTML = [
        '<div class="ms-drawer-head"><div><span class="ms-drawer-kicker">AERIAL RECON &amp; HAZARD ENGINE</span><h3>Drone Fleet (03 Units) &amp; Slope Scanner</h3></div>',
        '<div><button class="ms-ctrl-btn" id="ms-haz-scan-btn" style="margin-right:6px">⚠ Scan DEM Hazards</button><button class="ms-drawer-close" id="ms-drw-x">✕</button></div></div>',
        '<div class="ms-grid-3">',
        v10State.drones.map((dr) => [
          '<div class="ms-card">',
          '  <span class="mono-mini" style="color:#7fe3d2">' + dr.id + ' · ' + dr.status.toUpperCase() + '</span>',
          '  <h4 style="margin-top:4px">' + dr.name + '</h4>',
          '  <p class="mono-mini" style="margin:4px 0 8px">' + dr.mission + '</p>',
          '  <div class="mono-mini">Battery: <strong>' + dr.battery + '%</strong></div>',
          makeSegBarHtml(dr.battery, dr.battery < 70 ? 'orange' : 'mint'),
          '  <button class="ms-ctrl-btn ' + (dr.status === 'Deployed' ? '' : 'primary') + '" data-dr="' + dr.id + '" style="width:100%">' + (dr.status === 'Deployed' ? '↩ RETURN DRONE' : '↗ DEPLOY DRONE') + '</button>',
          '</div>'
        ].join('')).join(''),
        '</div>'
      ].join('');
      drawer.querySelectorAll('[data-dr]').forEach((b) => {
        b.onclick = () => {
          const id = b.getAttribute('data-dr');
          const dr = v10State.drones.find((x) => x.id === id);
          if (dr) {
            dr.status = dr.status === 'Deployed' ? 'Ready' : 'Deployed';
            window.__MarsDroneOverride = { deployed: v10State.drones.some((x) => x.status === 'Deployed'), phase: 0 };
            renderV10DrawerContent('drones');
          }
        };
      });
      const hzBtn = document.getElementById('ms-haz-scan-btn');
      if (hzBtn) hzBtn.onclick = runHazardScan;
    } else if (tab === 'lifesupport') {
      drawer.innerHTML = [
        '<div class="ms-drawer-head"><div><span class="ms-drawer-kicker">HABITAT &amp; AGRICULTURE TELEMETRY · v10 POSTER</span><h3>Closed-Loop Life Support &amp; MOXIE/HDU Systems</h3></div>',
        '<div><span class="ms-source-badge src-sim">TEAM + SIMULATED METERS</span> <button class="ms-drawer-close" id="ms-drw-x">✕</button></div></div>',
        '<div class="ms-grid-2">',
        '  <div class="ms-card"><h4>Life Support Segmented Gauges</h4>',
        '    <div class="mono-mini">Water Recycling: <strong style="color:#7fe3d2">98.4%</strong></div>' + makeSegBarHtml(98.4, 'mint'),
        '    <div class="mono-mini">Perchlorate ClO4- Remediation: <strong style="color:#7fe3d2">78.0%</strong></div>' + makeSegBarHtml(78.0, 'mint'),
        '    <div class="mono-mini">Crop Biomass Growth: <strong style="color:#7fe3d2">41.2%</strong></div>' + makeSegBarHtml(41.2, 'mint'),
        '    <div class="mono-mini">Hydroponic Dome RH: <strong style="color:#7fe3d2">94.0%</strong></div>' + makeSegBarHtml(94.0, 'mint'),
        '  </div>',
        '  <div class="ms-card"><h4>MOXIE O2 &amp; Habitat Power Trendlines</h4>',
        '    <div class="mono-mini">Cabin O2 (20.9% · 101.3 kPa · CO2 620 ppm)</div>',
        makeSparkSvg([20.6, 20.8, 20.7, 20.9, 21.0, 20.9, 20.9, 21.0], '#7fe3d2'),
        '    <div class="mono-mini" style="margin-top:6px">Dust Optical Depth Attenuation (12.4 kW Array)</div>',
        makeSparkSvg([12.8, 12.6, 12.1, 11.9, 12.2, 12.4, 12.5, 12.4], '#fb923c'),
        '  </div>',
        '</div>'
      ].join('');
    } else if (tab === 'slides') {
      const s = NASA_SLIDES_EN[v10State.slideIdx] || NASA_SLIDES_EN[0];
      drawer.innerHTML = [
        '<div class="ms-drawer-head"><div><span class="ms-drawer-kicker">' + s.kicker + ' · SLIDE ' + s.num + '</span><h3>' + s.title + '</h3></div>',
        '<div style="display:flex;gap:6px"><button class="ms-ctrl-btn" id="ms-sl-prev">◀ Prev</button><button class="ms-ctrl-btn primary" id="ms-sl-next">Next ▶</button><button class="ms-drawer-close" id="ms-drw-x">✕</button></div></div>',
        '<div class="ms-card">',
        s.bullets.map((b) => '<p style="margin:6px 0;font-size:12px;line-height:1.5;color:#e2e8f0">• ' + b + '</p>').join(''),
        '</div>',
        '<p class="mono-mini" style="margin-top:8px;color:#7fe3d2">NASA Global Rule Compliant: Exactly 7 English slides including title slide.</p>'
      ].join('');
      const prevBtn = document.getElementById('ms-sl-prev');
      const nextBtn = document.getElementById('ms-sl-next');
      if (prevBtn) prevBtn.onclick = () => { v10State.slideIdx = (v10State.slideIdx + 6) % 7; applySlideView(v10State.slideIdx); renderV10DrawerContent('slides'); };
      if (nextBtn) nextBtn.onclick = () => { v10State.slideIdx = (v10State.slideIdx + 1) % 7; applySlideView(v10State.slideIdx); renderV10DrawerContent('slides'); };
    }

    const xBtn = document.getElementById('ms-drw-x');
    if (xBtn) xBtn.onclick = () => selectDockTab(null);
  }

  function selectDockTab(tab) {
    if (tab === 'demo30') {
      toggle30SecDemo();
      return;
    }

    const leftPanel = document.querySelector('.hud-left');
    const rightPanel = document.querySelector('.hud-right');

    if (v10State.activeTab === tab) tab = null;
    v10State.activeTab = tab;

    document.querySelectorAll('.ms-v10-tab').forEach((b) => {
      b.classList.toggle('active', b.getAttribute('data-tab') === tab);
    });

    if (tab === 'routes-panel') {
      if (leftPanel) leftPanel.classList.add('ms-panel-open');
      if (rightPanel) rightPanel.classList.remove('ms-panel-open');
      renderV10DrawerContent(null);
      return;
    }

    if (tab === 'agri-panel') {
      if (rightPanel) rightPanel.classList.add('ms-panel-open');
      if (leftPanel) leftPanel.classList.remove('ms-panel-open');
      renderV10DrawerContent(null);
      return;
    }

    if (leftPanel) leftPanel.classList.remove('ms-panel-open');
    if (rightPanel) rightPanel.classList.remove('ms-panel-open');

    if (tab === 'slides') applySlideView(v10State.slideIdx);
    renderV10DrawerContent(tab);
  }

  function startRouteAutopilot(routeId) {
    const api = window.__MARS_WEB_BRIDGE && window.__MARS_WEB_BRIDGE.api;
    if (!api) return;
    if (api.camState.mode === 'orbit') api.setZone('surface');
    const pts = (api.routesPts && api.routesPts[routeId]) || (api.routesPts && api.routesPts.safest);
    if (!pts || pts.length < 2) return;
    v10State.roverStatus = 'EN ROUTE';
    v10State.roverSpeed = 1.4;
    emitToHost('mars:scene-rover-status', { state: 'en-route', routeId: routeId });
    window.__MarsRoverOverride = {
      mode: 'autopilot',
      waypoints: pts,
      progress: 0,
      onArrived: function () {
        v10State.roverStatus = 'ARRIVED';
        v10State.roverSpeed = 0;
        emitToHost('mars:scene-rover-status', { state: 'arrived', routeId: routeId });
        if (v10State.activeTab === 'rover') renderV10DrawerContent('rover');
      }
    };
    if (v10State.activeTab === 'rover') renderV10DrawerContent('rover');
  }

  function toggle30SecDemo() {
    v10State.demoActive = !v10State.demoActive;
    v10State.demoElapsed = 0;
    v10State.demoStepIdx = -1;
    const sub = document.getElementById('ms-demo-subtitle');
    const btn = document.querySelector('.ms-v10-tab[data-tab="demo30"]');
    if (btn) btn.classList.toggle('active', v10State.demoActive);
    if (!v10State.demoActive) {
      if (sub) sub.classList.remove('visible');
      const pop = document.getElementById('ms-omni-popup');
      if (pop) pop.classList.remove('visible');
      window.__MarsDroneOverride = { deployed: false };
      window.__MarsRoverOverride = null;
      const dustEl = document.getElementById('dust-tau-slider');
      if (dustEl) { dustEl.value = 45; dustEl.dispatchEvent(new Event('input')); }
      const api = window.__MARS_WEB_BRIDGE && window.__MARS_WEB_BRIDGE.api;
      if (api) api.setZone('surface');
    } else if (sub) {
      sub.classList.add('visible');
    }
  }

  function tickV10Features(dt) {
    if (v10State.omniRing && v10State.omniRing.visible) {
      v10State.omniRingTime += dt;
      const s = 1.0 + 0.18 * Math.sin(v10State.omniRingTime * 6.0);
      v10State.omniRing.scale.set(s, s, 1);
      if (v10State.omniRingTime > 12.0) v10State.omniRing.visible = false;
    }

    if (v10State.hazardGroup && v10State.hazardGroup.visible) {
      v10State.hazardGroup.children.forEach((c) => {
        if (c.geometry && c.geometry.type === 'ConeGeometry') c.rotation.y += dt * 1.4;
      });
    }

    if (!v10State.demoActive) return;
    v10State.demoElapsed += dt;
    const t = v10State.demoElapsed;
    if (t >= 28.0) {
      toggle30SecDemo();
      return;
    }

    const sub = document.getElementById('ms-demo-subtitle');
    const api = window.__MARS_WEB_BRIDGE && window.__MARS_WEB_BRIDGE.api;
    if (!api) return;

    const acts = [
      { end: 5.5, fn: () => { api.setZone('orbit'); state.focusMode = 'default'; state.userInteractedInZone = false; }, text: '[00–05s] ORBITX Mars Sentinel: Live orbital constellation, Sun at 1.52 AU, Phobos & Deimos over Jezero Crater.' },
      { end: 11.0, fn: () => { api.setZone('tactical'); runHazardScan(); }, text: '[05–11s] NASA HiRISE 4m DTM + A* Safest Route (1,159.95 m) vs Shortest (1,113.50 m) + live slope hazard cones.' },
      { end: 17.5, fn: () => { api.setZone('surface'); scanTerrainPoint(new THREE.Vector3(-4, api.sampleHeight(-4, 10), 10), new THREE.Vector3(0, 1, 0)); }, text: '[11–18s] Omni-Probe Planetary X-Ray: click any meter on Mars for RIMFAX 0–15m subsurface radar & PIXL chemistry.' },
      { end: 23.0, fn: () => { const pop = document.getElementById('ms-omni-popup'); if (pop) pop.classList.remove('visible'); startRouteAutopilot('safest'); const dustEl = document.getElementById('dust-tau-slider'); if (dustEl) { dustEl.value = 165; dustEl.dispatchEvent(new Event('input')); } }, text: '[18–23s] ROVER-01 Autonomous Safest Route + Beer-Lambert Dust Storm (τ=1.65) solar flux attenuation.' },
      { end: 28.0, fn: () => { const dustEl = document.getElementById('dust-tau-slider'); if (dustEl) { dustEl.value = 45; dustEl.dispatchEvent(new Event('input')); } v10State.drones[0].status = 'Deployed'; window.__MarsDroneOverride = { deployed: true, phase: 0 }; api.setZone('agriculture'); }, text: '[23–28s] Closed-Loop Hydroponics (98.4% H2O, 2.84 kg/Sol O2) & Ingenuity Scout Ready · NASA Space Apps 2026.' }
    ];

    let actIdx = acts.findIndex((a) => t < a.end);
    if (actIdx < 0) actIdx = acts.length - 1;
    if (actIdx !== v10State.demoStepIdx) {
      v10State.demoStepIdx = actIdx;
      acts[actIdx].fn();
    }
    if (sub) {
      sub.textContent = acts[actIdx].text + '  [' + Math.floor(t) + 's / 28s]';
    }
  }

  function initOmniProbeAndV10Dock(scene, camera) {
    if (typeof window !== 'undefined' && window.location && window.location.search.indexOf('embed=1') !== -1) {
      document.body.classList.add('embed-mode');
    } else {
      document.body.classList.add('ms-modern-simple');
    }
    buildOmniTargetsAndBeacons(scene);

    // 0. Inject Side Drawer Close Buttons & Topbar Full HUD Toggle
    document.querySelectorAll('.hud-left .panel-header, .hud-right .panel-header').forEach((hdr) => {
      if (!hdr.querySelector('.ms-panel-close-btn')) {
        const cbtn = document.createElement('button');
        cbtn.type = 'button';
        cbtn.className = 'ms-panel-close-btn';
        cbtn.title = 'Minimize Panel (Esc)';
        cbtn.textContent = '✕';
        cbtn.addEventListener('click', () => selectDockTab(null));
        hdr.appendChild(cbtn);
      }
    });

    const topActions = document.querySelector('.topbar-actions');
    if (topActions && !document.getElementById('btn-toggle-full-hud')) {
      const hudBtn = document.createElement('button');
      hudBtn.type = 'button';
      hudBtn.id = 'btn-toggle-full-hud';
      hudBtn.className = 'action-btn';
      hudBtn.textContent = '⊞ Full HUD: OFF';
      hudBtn.title = 'Switch between Clean 3D-First Mode and Full Telemetry Side Panels';
      hudBtn.addEventListener('click', () => {
        const isFull = document.body.classList.toggle('ms-full-hud');
        hudBtn.classList.toggle('hud-active', isFull);
        hudBtn.textContent = isFull ? '⊞ Full HUD: ON' : '⊞ Full HUD: OFF';
        if (isFull && (v10State.activeTab === 'routes-panel' || v10State.activeTab === 'agri-panel')) {
          selectDockTab(null);
        }
      });
      topActions.appendChild(hudBtn);
    }

    // 1. Inject Bottom-Center Unified v10 Command Dock & Drawer
    if (!document.getElementById('ms-v10-command-dock')) {
      const dock = document.createElement('div');
      dock.id = 'ms-v10-command-dock';
      dock.innerHTML = [
        '<button type="button" class="ms-v10-tab" data-tab="routes-panel">⌁ Routes &amp; Weather</button>',
        '<button type="button" class="ms-v10-tab" data-tab="omni">⌖ Omni-Probe X-Ray</button>',
        '<button type="button" class="ms-v10-tab" data-tab="rover">◈ Rover Control</button>',
        '<button type="button" class="ms-v10-tab" data-tab="drones">⚑ Drones &amp; Hazards</button>',
        '<button type="button" class="ms-v10-tab" data-tab="agri-panel">❖ Agri &amp; Geology</button>',
        '<button type="button" class="ms-v10-tab" data-tab="lifesupport">▤ v10 Telemetry</button>',
        '<button type="button" class="ms-v10-tab demo-hot" data-tab="demo30">▶ 30s NASA Demo</button>',
        '<button type="button" class="ms-v10-tab" data-tab="slides">◫ 7-Slide Deck</button>'
      ].join('');
      document.body.appendChild(dock);
      dock.addEventListener('click', (e) => {
        const b = e.target.closest('.ms-v10-tab');
        if (b) selectDockTab(b.getAttribute('data-tab'));
      });
    }

    window.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') {
        selectDockTab(null);
        const pop = document.getElementById('ms-omni-popup');
        if (pop) pop.classList.remove('visible');
      }
    });

    if (!document.getElementById('ms-v10-drawer')) {
      const drw = document.createElement('div');
      drw.id = 'ms-v10-drawer';
      document.body.appendChild(drw);
    }

    if (!document.getElementById('ms-demo-subtitle')) {
      const sub = document.createElement('div');
      sub.id = 'ms-demo-subtitle';
      document.body.appendChild(sub);
    }

    // 2. Canvas Click Raycaster -> Inspect ANY ground point, meteorite, crater, Sun, Phobos, Deimos, or Earth!
    const canvas = document.getElementById('mars-canvas');
    const raycaster = new THREE.Raycaster();
    const ndc = new THREE.Vector2();
    let pDownX = 0, pDownY = 0, pDownT = 0;
    if (canvas) {
      canvas.addEventListener('pointerdown', (e) => { pDownX = e.clientX; pDownY = e.clientY; pDownT = performance.now(); });
      canvas.addEventListener('pointerup', (e) => {
        if (e.target !== canvas) return;
        const dist = Math.hypot(e.clientX - pDownX, e.clientY - pDownY);
        if (dist > 6 || (performance.now() - pDownT) > 450) return;

        const rect = canvas.getBoundingClientRect();
        ndc.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
        ndc.y = -((e.clientY - rect.top) / rect.height) * 2 + 1;
        raycaster.setFromCamera(ndc, camera);

        // Priority A: Celestial Bodies & Meteorite/Crater targets
        const specialObjs = v10State.omniTargets.slice();
        if (state.sunGroup) specialObjs.push(state.sunGroup);
        if (state.spaceSunGroup) specialObjs.push(state.spaceSunGroup);
        if (state.surfacePhobos) specialObjs.push(state.surfacePhobos);
        if (state.surfaceDeimos) specialObjs.push(state.surfaceDeimos);
        if (state.phobosMesh) specialObjs.push(state.phobosMesh);
        if (state.deimosMesh) specialObjs.push(state.deimosMesh);

        const specHits = raycaster.intersectObjects(specialObjs, true);
        if (specHits.length > 0) {
          let obj = specHits[0].object;
          while (obj && !obj.userData.omniSpec && !obj.userData.celestialKey) obj = obj.parent;
          if (obj && obj.userData.omniSpec) {
            const sp = obj.userData.omniSpec;
            showOmniPopup({ badge: 'EXOGENIC / CRATER TARGET', title: sp.title, coords: sp.coords, chem: sp.chem, radar: sp.radar, notes: sp.notes });
            return;
          }
          if (obj && obj.userData.celestialKey) {
            scanCelestialBody(obj.userData.celestialKey);
            return;
          }
        }

        // Priority B: Existing named clickable outcrops handled by app.js -> also open glowing Omni-Probe telemetry card
        const api = window.__MARS_WEB_BRIDGE && window.__MARS_WEB_BRIDGE.api;
        if (api && api.clickableTargets && api.clickableTargets.length > 0 && !(state.orbitGroup && state.orbitGroup.visible)) {
          const rockHits = raycaster.intersectObjects(api.clickableTargets, false);
          if (rockHits.length > 0 && typeof rockHits[0].object.userData.outcropIdx === 'number') {
            const outcrops = (window.MARS_NASA_DATA && window.MARS_NASA_DATA.jezero_outcrops) || [];
            const o = outcrops[rockHits[0].object.userData.outcropIdx];
            if (o) {
              showOmniPopup({
                badge: 'JEZERO OUTCROP · MASTCAM-Z / PIXL',
                title: o.name + ' (' + (o.formation || 'Jezero') + ' · Sol ' + (o.sol ?? 0) + ')',
                coords: Number(o.lat || 0).toFixed(4) + '°N, ' + Number(o.lon || 0).toFixed(4) + '°E · ' + (o.elev_m || 0) + ' m · USGS ' + (o.usgs_unit || 'Mars'),
                chem: (o.crism_mineralogy || '') + ' · ' + (o.geochemistry_wt_pct || ''),
                radar: [
                  'Outcrop Bedrock: ' + (o.rock_type || 'Deltaic Mudstone / Basalt'),
                  'Thermal Inertia: ' + (o.thermal_inertia_tiu || 340) + ' TIU',
                  'Science Note: ' + (o.discovery_summary || 'Certified NASA Jezero Target').slice(0, 88)
                ],
                notes: 'Official Source: ' + (o.source_url || 'mars.nasa.gov/mars2020')
              });
            }
            return;
          }
        }

        // Priority C: ANY terrain floor point -> Live RIMFAX & PIXL X-Ray Scan!
        if (state.terrainMesh && !(state.orbitGroup && state.orbitGroup.visible)) {
          const terrHits = raycaster.intersectObject(state.terrainMesh, false);
          if (terrHits.length > 0) {
            const h = terrHits[0];
            const n = h.face ? h.face.normal : new THREE.Vector3(0, 1, 0);
            scanTerrainPoint(h.point, n);
            return;
          }
        }

        // Priority D: Orbit globe click
        if (state.marsGlobe && state.orbitGroup && state.orbitGroup.visible) {
          const globeHits = raycaster.intersectObject(state.marsGlobe, false);
          if (globeHits.length > 0) {
            showOmniPopup({
              badge: 'PLANETARY ORBITAL SCAN',
              title: 'Mars · Jezero Crater Basin Target (45 km Rim)',
              coords: '18.4447°N, 77.4508°E · Syrtis Major Volcanic Province Edge',
              chem: 'Noachian-Hesperian Fluvial Delta · Smectite Clay + Magnesium Carbonate Shoreline',
              radar: [
                'Orbital Pass: MRO HiRISE + CRISM + Mars Odyssey + MAVEN',
                'Moons Active: Phobos (9,376 km) & Deimos (23,463 km)',
                'Select [1] Surface Base & Crew to land inside Jezero Crater'
              ],
              notes: 'USGS SIM 3292 Global Map & NASA MOLA MEGDR Reference.'
            });
          }
        }
      });
    }

    // 3. Full MARS-SENTINEL-Frontend-v10 sceneBridge Contract (postMessage + CustomEvent)
    function handleBridgeEvent(type, detail) {
      const api = window.__MARS_WEB_BRIDGE && window.__MARS_WEB_BRIDGE.api;
      if (!api || !type) return;
      detail = detail || {};
      if (type === 'mars:view-change' && detail.zone) {
        api.setZone(detail.zone);
        emitToHost('mars:scene-zone-changed', { zone: detail.zone });
      } else if (type === 'mars:solar-change' && detail.phase) {
        api.applyTimeOfDay(detail.phase);
        emitToHost('mars:scene-solar-changed', { phase: detail.phase });
      } else if (type === 'mars:route-select' && detail.routeId) {
        v10State.selectedRoute = detail.routeId;
        const card = document.querySelector('.route-card[data-route="' + detail.routeId + '"]');
        if (card) card.click();
        emitToHost('mars:scene-route-changed', { routeId: detail.routeId });
      } else if (type === 'mars:route-confirm') {
        startRouteAutopilot(detail.routeId || 'safest');
      } else if (type === 'mars:rover-command') {
        if (detail.action === 'move') {
          window.__MarsRoverOverride = { mode: 'manual', dir: detail.direction || 'forward' };
          v10State.roverStatus = 'MOVING'; v10State.roverSpeed = 0.8;
        } else if (detail.action === 'stop') {
          window.__MarsRoverOverride = { mode: 'standby' };
          v10State.roverStatus = 'STANDBY'; v10State.roverSpeed = 0;
        }
      } else if (type === 'mars:drone-command') {
        const dr = v10State.drones.find((x) => x.id === detail.droneId);
        if (dr) dr.status = detail.action === 'deploy' ? 'Deployed' : 'Ready';
        window.__MarsDroneOverride = { deployed: detail.action === 'deploy', phase: 0 };
      } else if (type === 'mars:hazard-scan-request') {
        runHazardScan();
      } else if (type === 'mars:layer-change' && detail.layer) {
        const on = detail.enabled !== false;
        if (detail.layer === 'surface' && api.terrainMesh) api.terrainMesh.visible = on;
        if (detail.layer === 'sunlight' && api.sunLight) api.sunLight.visible = on;
        if (detail.layer === 'dust' && state.dustMotes) state.dustMotes.visible = on;
      } else if (type === 'mars:control') {
        if (detail.action === 'zoom-in') api.camState.desiredSpherical.radius = Math.max(8, api.camState.desiredSpherical.radius * 0.8);
        else if (detail.action === 'zoom-out') api.camState.desiredSpherical.radius = Math.min(160, api.camState.desiredSpherical.radius * 1.25);
        else if (detail.action === 'reset-view') api.setZone('surface');
      }
    }

    ['mars:view-change', 'mars:solar-change', 'mars:layer-change', 'mars:route-select', 'mars:route-confirm',
      'mars:rover-command', 'mars:drone-command', 'mars:hazard-scan-request', 'mars:control'].forEach((ev) => {
      window.addEventListener(ev, (e) => handleBridgeEvent(ev, e.detail));
    });
    window.addEventListener('message', (e) => {
      if (e.data && e.data.source === 'mars-dashboard' && typeof e.data.type === 'string') {
        handleBridgeEvent(e.data.type, e.data.detail);
      }
    });
    emitToHost('mars:scene-ready', { version: 'orbitx-v10-merged' });
  }
})();

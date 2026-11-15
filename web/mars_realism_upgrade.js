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
    }
  };
})();

/* ==========================================================================
   MARS SENTINEL — UNIFIED 5-CHAPTER STORY DIRECTOR ("SOL 1042: ECHOES OF JEZERO")
   Synchronizes narrative chapters across WebGL, Unity 6, and Unreal Engine 5.8
   with bilingual English + Arabic RTL Aerospace Radio Comms subtitles.
   ========================================================================== */
(function () {
  'use strict';

  const CHAPTERS = [
    {
      id: 1,
      tagEn: 'CHAPTER 1 / 5 · DUSTFALL ARRIVAL',
      tagAr: 'الفصل 1 / 5 · الوصول إلى جيزيرو',
      spkEn: 'HOUSTON CAPCOM (18m 42s DELAY)',
      spkAr: 'غرفة التحكم هيوستن (تأخير 18د 42ث)',
      lineEn: 'ORBITX-1, welcome to Octavia E. Butler Base in Jezero Crater. With zero GPS and an 18-minute Earth comms lag, NASA HDU and Perseverance are your lifeline.',
      lineAr: 'أوربت إكس-1، أهلاً بك في قاعدة أوكتافيا باتلر بفوهة جيزيرو. بدون GPS ومع تأخير 18 دقيقة مع الأرض، مسكن NASA HDU والروفر بيرسيفيرانس هما شريان حياتك.',
      action: api => {
        if (api && api.setZone) api.setZone('surface');
        if (window.__MarsEarth3D) window.__MarsEarth3D.set3dOverlay('off');
      }
    },
    {
      id: 2,
      tagEn: 'CHAPTER 2 / 5 · CHEYAVA FALLS BIOSIGNATURE',
      tagAr: 'الفصل 2 / 5 · البصمة الحيوية في شيفا فولز',
      spkEn: 'DR. NADIA (EVA-02 WINGMAN)',
      spkAr: 'د. نادية (الرائدة المرافقة EVA-02)',
      lineEn: 'RIMFAX radar just detected layered mudstone beneath Cheyava Falls! Activating 3D Slope Hazard projection along the A* Safest Route (8.1° max slope).',
      lineAr: 'رادار RIMFAX رصد طبقة صخرية عضوية تحت شيفا فولز! تفعيل إسقاط مخاطر الانحدار 3D على طول مسار A* الآمن (أقصى ميل 8.1°).',
      action: api => {
        if (api && api.setZone) api.setZone('surface');
        if (window.__MarsEarth3D) window.__MarsEarth3D.set3dOverlay('hazard');
      }
    },
    {
      id: 3,
      tagEn: 'CHAPTER 3 / 5 · DONKI SOLAR FLARE & STORM TRIAGE',
      tagAr: 'الفصل 3 / 5 · عاصفة شمسية وغبارية (Tau 2.4)',
      spkEn: 'MAVEN / DONKI SPACE WEATHER ALERT',
      spkAr: 'إنذار الطقس الفضائي — MAVEN / DONKI',
      lineEn: 'WARNING! Regional dust storm (Tau 2.4) & Solar Particle Event incoming! Beer-Lambert sunlight drops 62% — retreat into the closed-loop Agriculture Dome & HDU shelter!',
      lineAr: 'تحذير! عاصفة غبارية إقليمية وتوهج شمسي يضربان جيزيرو! تنخفض الطاقة الشمسية 62% — احتمِ فوراً داخل قبة الزراعة ومسكن HDU!',
      action: api => {
        if (api && api.setZone) api.setZone('agriculture');
        if (api && api.applyTimeOfDay) api.applyTimeOfDay('SUNSET');
        if (window.__MarsEarth3D) window.__MarsEarth3D.set3dOverlay('sun');
      }
    },
    {
      id: 4,
      tagEn: 'CHAPTER 4 / 5 · BELVA RIM RELAY & CACAO METEORITE',
      tagAr: 'الفصل 4 / 5 · برج بيلفا ونيزك كاكاو',
      spkEn: 'AUTONOMOUS SCOUT-AI-01 TELEMETRY',
      spkAr: 'الروبوت الكشاف الذكي SCOUT-AI-01',
      lineEn: 'Storm cleared! Projecting live 3D RF Line-of-Sight Viewshed from Habitat Tower to verify UHF communications across Belva Rim and the Cacao Fe-Ni Meteorite.',
      lineAr: 'انقشعت العاصفة! إسقاط خريطة مجال الاتصال اللاسلكي RF 3D من برج المسكن لضمان الاتصال نحو حافة بيلفا ونيزك كاكاو الحديدي.',
      action: api => {
        if (api && api.setZone) api.setZone('tactical');
        if (api && api.applyTimeOfDay) api.applyTimeOfDay('DAY');
        if (window.__MarsEarth3D) window.__MarsEarth3D.set3dOverlay('rf');
      }
    },
    {
      id: 5,
      tagEn: 'CHAPTER 5 / 5 · MARS SAMPLE RETURN DEPARTURE',
      tagAr: 'الفصل 5 / 5 · إقلاع صاروخ عودة العينات للأرض',
      spkEn: 'MISSION COMMANDER — ORBITAL TRANSFER',
      spkAr: 'قائد المهمة — المسار المداري للأرض',
      lineEn: 'All 3 Jezero cores secured! Mars Ascent Vehicle clears atmosphere past Phobos, Deimos & the MRO/MAVEN relay constellation — scaling toward Google Earth for Mars.',
      lineAr: 'تم تأمين العينات الثلاث بنجاح! صاروخ العودة يخترق الغلاف الجوي مروراً بفوبوس وديموس وأقمار NASA — نحو بناء Google Earth للمريخ.',
      action: api => {
        if (window.__MarsEarth3D) window.__MarsEarth3D.set3dOverlay('off');
        if (api && api.setZone) api.setZone('orbit');
      }
    }
  ];

  const STORY = {
    open: false,
    chapterIdx: 0
  };

  function isArabic() {
    return document.body.classList.contains('ms-rtl-ar') || document.documentElement.lang === 'ar';
  }

  function renderChapter() {
    const ch = CHAPTERS[STORY.chapterIdx];
    const ar = isArabic();
    const tagEl = document.querySelector('#ms-story-tag');
    const spkEl = document.querySelector('#ms-story-spk');
    const lineEl = document.querySelector('#ms-story-line');
    const prevBtn = document.querySelector('#ms-story-prev');
    const nextBtn = document.querySelector('#ms-story-next');
    if (tagEl) tagEl.textContent = ar ? ch.tagAr : ch.tagEn;
    if (spkEl) spkEl.textContent = ar ? ch.spkAr : ch.spkEn;
    if (lineEl) lineEl.textContent = ar ? ch.lineAr : ch.lineEn;
    if (prevBtn) prevBtn.textContent = ar ? 'السابق ◀' : '◀ Prev';
    if (nextBtn) nextBtn.textContent = STORY.chapterIdx === CHAPTERS.length - 1
      ? (ar ? 'إعادة ↻' : 'Replay ↻')
      : (ar ? 'التالي ▶' : 'Next ▶');

    const api = window.__MARS_WEB_BRIDGE && window.__MARS_WEB_BRIDGE.api;
    if (typeof ch.action === 'function') ch.action(api);
  }

  function openStory(idx) {
    STORY.open = true;
    STORY.chapterIdx = Math.max(0, Math.min(CHAPTERS.length - 1, idx || 0));
    const card = document.querySelector('#ms-story-card');
    if (card) card.classList.add('open');
    renderChapter();
  }

  function closeStory() {
    STORY.open = false;
    const card = document.querySelector('#ms-story-card');
    if (card) card.classList.remove('open');
    if (window.__MarsEarth3D) window.__MarsEarth3D.set3dOverlay('off');
  }

  function mountStoryUi() {
    if (document.querySelector('#ms-story-card')) return;

    // Add crisp Story Mode trigger chip to the Google Earth bar
    const row = document.querySelector('#ms-earth-bar .ms-earth-row');
    if (row && !document.querySelector('#btn-story-launch')) {
      const sep = document.createElement('span');
      sep.className = 'ms-earth-sep';
      sep.textContent = '|';
      const btn = document.createElement('button');
      btn.id = 'btn-story-launch';
      btn.className = 'ms-earth-chip ms-story-chip';
      btn.textContent = '★ Story (5 Ch)';
      btn.addEventListener('click', () => (STORY.open ? closeStory() : openStory(0)));
      row.appendChild(sep);
      row.appendChild(btn);
    }

    const card = document.createElement('div');
    card.id = 'ms-story-card';
    card.className = 'ms-story-card';
    card.innerHTML = `
      <div class="ms-story-head">
        <span id="ms-story-tag" class="ms-story-tag">CHAPTER 1 / 5</span>
        <span id="ms-story-spk" class="ms-story-spk">HOUSTON CAPCOM</span>
        <button id="ms-story-close" class="ms-story-x" title="Close Story">✕</button>
      </div>
      <div id="ms-story-line" class="ms-story-line"></div>
      <div class="ms-story-foot">
        <button id="ms-story-prev" class="ms-earth-chip">◀ Prev</button>
        <div class="ms-story-dots">${CHAPTERS.map((_, i) => `<button class="ms-story-dot" data-story-idx="${i}">${i + 1}</button>`).join('')}</div>
        <button id="ms-story-next" class="ms-earth-chip active">Next ▶</button>
      </div>
    `;
    document.body.appendChild(card);

    card.querySelector('#ms-story-close').addEventListener('click', closeStory);
    card.querySelector('#ms-story-prev').addEventListener('click', () => {
      STORY.chapterIdx = (STORY.chapterIdx - 1 + CHAPTERS.length) % CHAPTERS.length;
      renderChapter();
    });
    card.querySelector('#ms-story-next').addEventListener('click', () => {
      STORY.chapterIdx = (STORY.chapterIdx + 1) % CHAPTERS.length;
      renderChapter();
    });
    card.querySelectorAll('[data-story-idx]').forEach(dot => {
      dot.addEventListener('click', () => {
        STORY.chapterIdx = parseInt(dot.getAttribute('data-story-idx'), 10) || 0;
        renderChapter();
      });
    });

    // Re-render story language live if EN/AR toggle is clicked
    const langBtn = document.querySelector('#btn-lang-toggle');
    if (langBtn) {
      langBtn.addEventListener('click', () => {
        setTimeout(() => {
          const b = document.querySelector('#btn-story-launch');
          if (b) b.textContent = isArabic() ? '★ القصة (5 فصول)' : '★ Story (5 Ch)';
          if (STORY.open) renderChapter();
        }, 40);
      });
    }

    window.__MarsStoryMode = {
      state: STORY,
      chapters: CHAPTERS,
      open: openStory,
      close: closeStory
    };
  }

  function boot() {
    if (!document.querySelector('#ms-earth-bar')) {
      setTimeout(boot, 200);
      return;
    }
    mountStoryUi();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => setTimeout(boot, 350));
  } else {
    setTimeout(boot, 350);
  }
})();

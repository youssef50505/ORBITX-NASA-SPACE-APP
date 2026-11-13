# الدليل العلمي والتقني الشامل لموارد وبيانات ناسا المريخية
## NASA Space Apps Challenge: Interplanetary Survival Guide — Martian Map
### الدليل العربي الاحترافي الشامل للتطوير والمحاكاة في Blender و Unity Engine (Unity 6) و Unreal Engine 5.8 وتطبيقات الويب التفاعلية ثلاثية الأبعاد (Web 3D / GIS)

---

## الفهرس العام للمحتويات

1. [المقدمة والرؤية العلمية لتحدي ناسا Space Apps](#1-المقدمة-والرؤية-العلمية-لتحدي-ناسا-space-apps)
2. [التشريح العلمي والتقني للموارد الخمسة الرسمية من وكالة ناسا](#2-التشريح-العلمي-والتقني-للموارد-الخمسة-الرسمية-من-وكالة-ناسا)
   - [2.1 بوابة ناسا مارس تريك (JPL Mars Trek)](#21-بوابة-ناسا-مارس-تريك-jpl-mars-trek)
   - [2.2 الخريطة الجيولوجية الموحدة للمريخ (USGS SIM 3292)](#22-الخريطة-الجيولوجية-الموحدة-للمريخ-usgs-sim-3292)
   - [2.3 مسار المركبة كيوريوسيتي نحو جبل شارب (Curiosity Mount Sharp Route)](#23-مسار-المركبة-كيوريوسيتي-نحو-جبل-شارب-curiosity-mount-sharp-route)
   - [2.4 منصة ناسا وورلد فيو (NASA Worldview & GIBS) وتطبيقاتها المريخية](#24-منصة-ناسا-وورلد-فيو-nasa-worldview--gibs-وتطبيقاتها-المريخية)
   - [2.5 معرض والواجهات البرمجية لصور بيرسيفيرانس المباشرة (Perseverance Raw Images API)](#25-معرض-والواجهات-البرمجية-لصور-بيرسيفيرانس-المباشرة-perseverance-raw-images-api)
3. [كيفية الاستفادة القصوى والتكامل في محرك بلندر (Blender 5.2 / 5.3)](#3-كيفية-الاستفادة-القصوى-والتكامل-في-محرك-بلندر-blender-52--53)
   - [3.1 محاكاة التضاريس فائقة الدقة باستخدام Adaptive Displacement و Micro-Polygons](#31-محاكاة-التضاريس-فائقة-الدقة-باستخدام-adaptive-displacement-و-micro-polygons)
   - [3.2 التحويل الرياضي المباشر من إحداثيات المريخ الجيوديسية إلى ENU المحلية](#32-التحويل-الرياضي-المباشر-من-إحداثيات-المريخ-الجيوديسية-إلى-enu-المحلية)
   - [3.3 بناء مسار العربة والمروحية ثلاثي الأبعاد وإضاءة المشهد الفيزيائية](#33-بناء-مسار-العربة-والمروحية-ثلاثي-الأبعاد-وإضاءة-المشهد-الفيزيائية)
4. [كيفية الاستفادة القصوى في محرك أنريل إنجن 5.8 (Unreal Engine 5.8)](#4-كيفية-الاستفادة-القصوى-في-محرك-أنريل-إنجن-58-unreal-engine-58)
   - [4.1 إنشاء تضاريس نانيت (Nanite Landscape) باستخدام خرائط الارتفاع 16-bit Heightmaps](#41-إنشاء-تضاريس-نانيت-nanite-landscape-باستخدام-خرائط-الارتفاع-16-bit-heightmaps)
   - [4.2 خامات التضاريس الافتراضية أثناء التشغيل (Runtime Virtual Texturing - RVT)](#42-خامات-التضاريس-الافتراضية-أثناء-التشغيل-runtime-virtual-texturing---rvt)
   - [4.3 نظام المسارات والشرائط التفاعلية (Splines & Niagara Dust Tracks)](#43-نظام-المسارات-والشرائط-التفاعلية-splines--niagara-dust-tracks)
5. [كيفية الاستفادة القصوى في تطبيقات الويب والخرائط ثلاثية الأبعاد (Web 3D / GIS)](#5-كيفية-الاستفادة-القصوى-في-تطبيقات-الويب-والخرائط-ثلاثية-الأبعاد-web-3d--gis)
   - [5.1 إعداد بيئة CesiumJS مع كرواني المريخ الرسمي (IAU2000 Mars Ellipsoid)](#51-إعداد-بيئة-cesiumjs-مع-كرواني-المريخ-الرسمي-iau2000-mars-ellipsoid)
   - [5.2 تحميل بلاطات التضاريس ثلاثية الأبعاد Quantized Mesh والخرائط الموزعة WMTS](#52-تحميل-بلاطات-التضاريس-ثلاثية-الأبعاد-quantized-mesh-والخرائط-الموزعة-wmts)
   - [5.3 عرض مسارات GeoJSON الحية ومحطات التوقف والربط مع صور ناسا المباشرة](#53-عرض-مسارات-geojson-الحية-ومحطات-التوقف-والربط-مع-صور-ناسا-المباشرة)
6. [استراتيجيات البقاء والنجاة المعتمدة على البيانات الحقيقية (Data-Driven Survival)](#6-استراتيجيات-البقاء-والنجاة-المعتمدة-على-البيانات-الحقيقية-data-driven-survival)
   - [6.1 دراسة حركة العربات وتقييم المخاطر (Trafficability & Sand Dune Hazards)](#61-دراسة-حركة-العربات-وتقييم-المخاطر-trafficability--sand-dune-hazards)
   - [6.2 استخراج المياه والجليد تحت السطحي (ISRU Water Extraction Across Epochs)](#62-استخراج-المياه-والجليد-تحت-السطحي-isru-water-extraction-across-epochs)
   - [6.3 الحماية من الإشعاع والعواصف الشمسية (Radiation Shielding & Lava Tubes)](#63-الحماية-من-الإشعاع-والعواصف-الشمسية-radiation-shielding--lava-tubes)
   - [6.4 إدارة الطاقة والنجاة من العواصف الترابية الشاملة (Solar Budget & Dust Storms)](#64-إدارة-الطاقة-والنجاة-من-العواصف-الترابية-الشاملة-solar-budget--dust-storms)
7. [مكتبة الأكواد والسكريبتات الجاهزة للتطبيق الفوري (Production Code Vault)](#7-مكتبة-الأكواد-والسكريبتات-الجاهزة-للتطبيق-الفوري-production-code-vault)

---

## 1. المقدمة والرؤية العلمية لتحدي ناسا Space Apps

يهدف تحدي **"Interplanetary Survival Guide: Martian Map"** في مسابقة **NASA Space Apps Challenge** إلى الخروج من حيز الخرائط المسطحة النظرية إلى فضاء النظم التفاعلية المتكاملة، والتي تمكّن رواد الفضاء المستقبليين من التخطيط الآمن وتنفيذ رحلات السير الاستكشافية على سطح المريخ (Extravehicular Activities - EVAs المعروفة بـ "Marswalks") بأقصى درجات الكفاءة والأمان وأعلى عائد علمي ممكن.

على مدى أكثر من خمسة عقود، جمعت بعثات وكالة ناسا المدارية والسطحية (مثل MGS، Mars Odyssey، MRO، MER Spirit/Opportunity، MSL Curiosity، و Mars 2020 Perseverance) مئات التيرابايتات من البيانات الجيوديسية، الطيفية، والطبقية. غير أن الخطر القاتل الذي يواجه رائد الفضاء على المريخ ليس نقص البيانات، بل **كيفية دمج هذه البيانات متعددة المصادر والأبعاد (Multi-modal Data Fusion)** في واجهة لحظية موحدة (مثل خوذة الواقع المعزز HUD، أو الحاسب اللوحي الميداني، أو التوأم الرقمي ثلاثي الأبعاد).

يتطلب الدليل العلمي للنجاة دمج أربعة محاور رئيسية لا غنى عنها:
1. **طبوغرافيا السطح بدقة سنتيمترية وميلان المنحدرات (Slopes & Micro-Topography)** لتجنب انقلاب المركبات أو تمزق البدلات الفضائية في الحقول الصخرية الحادة.
2. **الطبقات والتكوينات الجيولوجية والمعادن المائية (Lithology, Stratigraphy & Hydrous Minerals)** لتحديد مواقع استخراج المياه (In-Situ Resource Utilization - ISRU) ومصادر المعادن الصناعية.
3. **بيانات المسارات الحقيقية لرواد الروبوتات (Robotic Precursor Traverses)** للاستفادة من الطرق المختبرة مسبقاً وتجنب الكثبان الرملية الناعمة القاتلة التي احتجزت مركبة سبيريت وأنهت مهمتها.
4. **المراقبة البيئية والظروف الجوية (Atmospheric Dust & Radiative Transfer)** لرصد العواصف الترابية الكوكبية وحساب ميزانية الطاقة الشمسية وتحديد الجرعات الإشعاعية القاتلة.

---

## 2. التشريح العلمي والتقني للموارد الخمسة الرسمية من وكالة ناسا

حددت وكالة ناسا في صفحة الموارد الرسمية للتحدي خمسة موارد أساسية تمثل قمة التكنولوجيا وعلوم الفضاء الأرضية والمريخية. فيما يلي التشريح التفصيلي الدقيق لكل مورد، مع روابطه الحية وهياكل بياناته وطرق الوصول البرمجية إليه:

### 2.1 بوابة ناسا مارس تريك (JPL Mars Trek)
- **الرابط الرسمي المباشر**: `https://trek.nasa.gov/mars/`
- **الجهة المطورة**: مختبر الدفع النفاث التابع لناسا (NASA JPL Solar System Treks Project).
- **الوصف العلمي**: نظام معلومات جغرافي كوكبي متكامل (Planetary GIS) يعمل عبر المتصفح، ويضم أكثر من 2,000 طبقة بيانات تم جمعها ومعايرتها على مدى عقود.
- **البروتوكولات البرمجية المكتشفة والمفحوصة**:
  * **محرك البحث البرمجي Solr REST API**:
    `https://trek.nasa.gov/mars/TrekServices/ws/index/eq/searchItems`  
    يتيح الاستعلام البرمجي عن أي طبقة أو معلم جغرافي أو إحداثيات هبوط.
  * **خدمة بلاطات الخرائط العالمية OGC WMTS**:
    `https://trek.nasa.gov/tiles/Mars/EQ/{LayerName}/1.0.0/default/default028mm/{TileMatrix}/{TileRow}/{TileCol}.jpg`  
    تتيح استدعاء البلاطات الموزائيكية للارتفاعات والإشعاع الحراري والصور الضوئية مباشرة في محركات الألعاب أو الويب.
  * **خدمة استخراج الارتفاعات الفورية (ArcGIS ImageServer Export)**:
    `https://trek.nasa.gov/mars/trekarcgis/rest/services/mola128_mola64_merge_90Nto90S_SimpleC_clon0/ImageServer/exportImage?bbox={minLon},{minLat},{maxLon},{maxLat}&bboxSR=4326&size={w},{h}&format=tiff&f=image`  
    تتيح استخراج نماذج الارتفاع الرقمية كملفات GeoTIFF. تجدر الإشارة إلى أن ملف الارتفاع النموذجي المرفق في المشروع `sample_mola_dem_slice.tif` هو ملف GeoTIFF بصيغة **عدد صحيح 16-بت ذي إشارة (`int16`, `BitsPerSample = 16`, `SampleFormat = 2`)**، حيث تمثل كل وحدة عددية متراً صحيحاً واحداً (1-meter integer quantization) وتتراوح الارتفاعات في فوهة جيزيرو بين -2667 متراً إلى -1472 متراً نسبة إلى مسند MOLA Aeroid datum. بينما تدعم أجهزة HiRISE DTM تصدير قيم عائمة 32-bit Float للمناطق السطحية فائقة الدقة.
  * **مولد المجسمات ثلاثية الأبعاد (3D STL Generator)**:
    `https://trek.nasa.gov/mars/TrekServices/ws/model/mars/trekSTL.stl?imageServiceURL={DEM}&bbox={minLon},{minLat},{maxLon},{maxLat}&width={w}&height={h}&exag=1.5&target=mars`  
    خدمة فريدة تنتج ملفات STL مثلثة جاهزة للطباعة ثلاثية الأبعاد أو الاستيراد المباشر كشبكة مجسمة (Mesh) في بلندر وأنريل.  
    **ملاحظة تقنية هامة حول متجهات النواظم (STL Face Normals)**: تقوم خوادم Mars Trek بتعيين متجهات النواظم في ملفات STL الثنائية المصدرة إلى أصفار `[0, 0, 0]` افتراضياً لتقليص زمن المعالجة بالخادم؛ لذا يجب على بيئات التطوير ثلاثية الأبعاد (Blender / Unreal) إعادة حساب متجهات النواظم للأوجه (Face Normals) عبر الضرب الاتجاهي للمثلثات $(v_2 - v_1) \times (v_3 - v_1)$ وتطبيعها لمتجهات وحدة (Unit Normalization)، واستبعاد أي مثلثات متدهورة منعدمة المساحة (Degenerate Triangles) لضمان صحة الإضاءة والتظليل.
  * **خدمة حساب مقاطع الارتفاع (Elevation Profile)**:
    `https://trek.nasa.gov/mars/TrekServices/ws/elevationProfile/calculate`  
    تستقبل مسار خطي متعدد النقاط وترجع مصفوفة JSON دقيقة بمسافة المسار والارتفاع الحقيقي عند كل نقطة.

---

### 2.2 الخريطة الجيولوجية الموحدة للمريخ (USGS SIM 3292)
- **المرجع العلمي**: *Geologic Map of Mars*, Scientific Investigations Map 3292 (Tanaka et al., 2014, مقياس 1:20,000,000).
- **الجهة المطورة**: هيئة المساحة الجيولوجية الأمريكية (USGS Astrogeology Science Center) بالتعاون مع وكالة ناسا.
- **الوصف العلمي**: الخريطة الجيولوجية القياسية المعتمدة دولياً، والتي وحدت جيولوجيا الكوكب الأحمر في **44 وحدة جيولوجية معيارية** تغطي العصور الثلاثة الكبرى: النواكي (Noachian)، الهسبيري (Hesperian)، والأمازوني (Amazonian).
- **الملفات والمصادر الرسمية**:
  * قاعدة البيانات الجغرافية الكاملة: `https://pubs.usgs.gov/sim/3292/downloads/sim3292_database.zip` (~790 ميجابايت)
  * خريطة الحائط المطبوعة بدقة فائقة (PDF): `https://pubs.usgs.gov/sim/3292/pdf/sim3292_map.pdf`
  * الكتيب التفسيري العلمي (43 صفحة): `https://pubs.usgs.gov/sim/3292/pdf/sim3292_pamphlet.pdf`
  * جدول العلاقات الطبقية (Stratigraphic Chart): `https://pubs.usgs.gov/sim/3292/pdf/sim3292_figure2.pdf`
- **اكتشاف خادم ناسا الحي (Live MapServer Discovery)**:
  * قامت وكالة ناسا باستضافة الخريطة الجيولوجية بالكامل على خادم خرائط حي عبر الرابط:
    `https://trek.nasa.gov/mars/trekarcgis/rest/services/SIM3292_Global_Geology/MapServer/0`
  * يمكن تصدير قطاعات الخريطة بدقة عالية وبشفافية كاملة عبر:
    `https://trek.nasa.gov/mars/trekarcgis/rest/services/SIM3292_Global_Geology/MapServer/export?bbox={minLon},{minLat},{maxLon},{maxLat}&bboxSR=4326&size=512,512&format=png32&f=image`

---

### 2.3 مسار المركبة كيوريوسيتي نحو جبل شارب (Curiosity Mount Sharp Route)
- **الرابط المرجعي**: `https://science.nasa.gov/resource/curiositys-proposed-path-up-mount-sharp/`
- **دفتر محلل علوم المريخ (PDS Analyst's Notebook)**: `https://an.rsl.wustl.edu/msl`
- **الأهمية العلمية**: تقدم هذه الخريطة النموذج المثالي لكيفية دمج 4 أجهزة استشعار فضائية مختلفة لتوجيه مركبة على المريخ:
  1. كاميرا HiRISE المدارية (بدقة 25 سم لكل بكسل) لدراسة الصخور والعوائق السطحية.
  2. مطياف CRISM بالأشعة تحت الحمراء لتحديد توقيعات المعادن الطينية المائية (Phyllosilicates) والمعادن الكبريتية (Sulfates).
  3. كاميرا CTX السياقية (بدقة 6 أمتار) لدراسة التضاريس الإقليمية.
  4. نموذج الارتفاع الرقمي من كاميرا HRSC الاستريوسكوبية لتحديد ميول المنحدرات.
- **خلاصة مسار كيوريوسيتي الحقيقي**:
  * بدأت من موقع الهبوط (Bradbury Landing) على ارتفاع **$-4501$ متراً** وصعدت مسافة تجاوزت **35 كيلومتراً** وتسلقت عمودياً أكثر من **$388$ متراً** في تكوينات جبل شارب (Aeolis Mons) قاطعة حافة فيرا روبين (Vera Rubin Ridge) ووحدة الغلين توريدون (Glen Torridon) وقناة وادي غيديز (Gediz Vallis).

---

### 2.4 منصة ناسا وورلد فيو (NASA Worldview & GIBS) وتطبيقاتها المريخية
- **الرابط الرسمي**: `https://worldview.earthdata.nasa.gov/`
- **خادم البلاطات العالمي GIBS**: `https://gibs.earthdata.nasa.gov/wmts/epsg4326/best/wmts.cgi`
- **الأهمية المنهجية للتحدي**:
  على الرغم من أن وورلد فيو يراقب الأرض يومياً عبر أقمار MODIS و VIIRS، فإن **الفيزياء الكوكبية للاستشعار عن بعد متطابقة عبر الكواكب الصخرية**. يقدم هذا المورد النموذج الهندسي الأكمل لبناء واجهة مراقبة حية لرواد الفضاء:
  * قياس عمق الغلاف الجوي البصري (Aerosol Optical Depth - AOD / $\tau$) لمراقبة عواصف الغبار.
  * دراسة درجة حرارة السطح (Land Surface Temperature - LST) والقصور الذاتي الحراري (Thermal Inertia) للتنبؤ بقوة تماسك التربة تحت عجلات العربة.
  * شريط زمني تفاعلي (Temporal Timeline Slider) يتيح للرائد استعراض التغيرات الفصلية قبل التحرك.

---

### 2.5 معرض والواجهات البرمجية لصور بيرسيفيرانس المباشرة (Perseverance Raw Images API)
- **البوابة الرسمية**: `https://mars.nasa.gov/mars2020/multimedia/raw-images/`
- **الواجهة البرمجية الحية (Live REST JSON API)**:
  `https://mars.nasa.gov/rss/api/?feed=raw_images&category=mars2020&feedtype=json`
- **المعاملات المدعومة (Query Parameters)**:
  * `sol`: اليوم المريخي (Sol 0 إلى أكثر من Sol 2031).
  * `search`: اسم الكاميرا أو الأداة (مثل `MCZ_RIGHT`، `NAVCAM_LEFT`، `FRONT_HAZCAM`، `SUPERCAM_RMI`، `SKYCAM`).
  * `num`: عدد العناصر في الصفحة (حتى 100).
  * `page`: رقم الصفحة المقسمة.
- **الإحصائيات المكتشفة**:
  يحتوي الأرشيف على أكثر من **1,055,868 صورة خام** حقيقية ملتقطة من كاميرات المستكشفة في فوهة جيزيرو (Jezero Crater)، مما يوفر الحقيقة الأرضية الميدانية (Ground Truth) من منظور عين الإنسان على السطح.

---

## 3. كيفية الاستفادة القصوى والتكامل في محرك بلندر (Blender 5.2 / 5.3)

يقدم بلندر البيئة الرائدة للمحاكاة العلمية الدقيقة وإنتاج المشاهد السينمائية لسطح المريخ. فيما يلي الخطوات العملية الموثقة:

### 3.1 محاكاة التضاريس فائقة الدقة باستخدام Adaptive Displacement و Micro-Polygons
بدلاً من استيراد شبكات مضلعة ثقيلة تشل حركة البرنامج، يتم الاعتماد على خريطة الارتفاعات الرقمية المباشرة (سواء كانت بصيغة 32-bit Float OpenEXR لخرائط HiRISE أو بتنسيق 16-bit Signed Integer لخرائط MOLA DEM مثل `sample_mola_dem_slice.tif`):
1. في إعدادات محرك الرندر **Cycles**، اختر المعالج **GPU Compute** وقم بتفعيل **Feature Set: Experimental**.
2. أنشئ مسطحاً أولياً (Grid / Plane) وأضف إليه معدل **Subdivision Surface** وفعل خيار **Adaptive Subdivision** مع ضبط **Dicing Rate = 1.0 px**. هذا يضمن تجزيء التضاريس إلى ميكرو-بوليغونات عند المناطق القريبة من الكاميرا تلقائياً.
3. في نافذة المواد (Material Settings $\rightarrow$ Surface)، غيّر نوع الإزاحة إلى **Displacement Only**.
4. قم بربط عقدة الصورة (Image Texture) بعقدة الإزاحة (Displacement Node):
   - اضبط نوع الألوان على **Non-Color** (بيانات خطية بحتة).
   - اربط مخرج اللون بمدخل **Height**.
   - اضبط **Midlevel = 0.0**، ومقياس الإزاحة **Scale** مساوياً للمدى الرأسي الحقيقي للتضاريس مقسوماً على معامل مقياس المشهد (أو بمعدل $1.0\text{ m}$ لكل وحدة عددية في بيانات MOLA int16 لتمثيل الارتفاعات الحقيقية بمقياس 1:1).

### 3.2 التحويل الرياضي المباشر من إحداثيات المريخ الجيوديسية إلى ENU المحلية
تأتي إحداثيات ناسا في ملفات الـ GeoJSON كإحداثيات كروية كوكبية مركزية (Planetocentric) بالدرجات: خط الطول شرقاً ($\lambda$)، خط العرض شمالاً ($\phi$)، والارتفاع بالأمتار ($h$) نسبة إلى مجسم الجاذبية السطحي للمريخ (MOLA Aeroid Datum).  
لتحويلها إلى إحداثيات محلية ديكارتية (East-North-Up - ENU) مناسبة لبلندر ومحركات الألعاب حول مركز محلي محدد $(\phi_0, \lambda_0, h_0)$:

$$\Delta\lambda_{\text{deg}} = ((\lambda - \lambda_0 + 180.0) \pmod{360.0}) - 180.0$$
$$\Delta\lambda_{\text{rad}} = \Delta\lambda_{\text{deg}} \cdot \frac{\pi}{180}$$
$$X_{\text{East}} = R_{\text{Mars}} \cdot \cos\left(\frac{\phi + \phi_0}{2}\right) \cdot \Delta\lambda_{\text{rad}}$$
$$Y_{\text{North}} = R_{\text{Mars}} \cdot (\phi - \phi_0) \cdot \frac{\pi}{180}$$
$$Z_{\text{Up}} = h - h_0$$

حيث نصف قطر المريخ المعتمد دولياً: $R_{\text{Mars}} = 3,396,190.0\text{ m}$.

**أهمية التطبيع المعياري لخطوط الطول (Modular Longitude Normalization)**:  
تعتمد بعثات ناسا وأنظمتها أحياناً تمثيل خطوط الطول في النطاق $[0^\circ, 360^\circ]$ شرقاً (كما في Mars Trek) وأحياناً في النطاق $[-180^\circ, +180^\circ]$ (كما في طبقات IAU2000 GIS). في حال عدم استخدام التطبيع المعياري $\Delta\lambda_{\text{deg}} = ((\lambda - \lambda_0 + 180.0) \pmod{360.0}) - 180.0$، فإن إدخال إحداثيات موقع مثل ميريدياني لعربة أوبورتيونيتي ($354.47^\circ\text{E}$ مقابل $-5.53^\circ\text{W}$) ينتج عنه فرق زائف قدره $360^\circ$ يترجم إلى خطأ كارثي في الإزاحة الأفقية يتجاوز **$-21,326,581\text{ متراً}$**! كما تضمن هذه المعادلة سلامة الحسابات عند عبور خط الزوال المقابل (Antimeridian عند $180^\circ$) أو خط الطول الصفري (Prime Meridian).

**حدود الصلاحية الهندسية (Curvature Flat-Plane Limits)**:  
تمثل هذه المعادلة إسقاطاً مستوياً مماساً متساوي المستطيلات (Equirectangular Tangent-Plane Approximation)؛ وهي دقيقة هندسياً لمسارات العربات والمشاهد السطحية المحلية ذات الامتداد الأصغر من **$30\text{ كم}$** حيث يكون انحناء الكوكب الرأسي (Sagitta Curvature Drop $\Delta z \approx \frac{d^2}{2 R_{\text{Mars}}}$) صغيراً جداً ($\le 132.5\text{ m}$ عند 30 كم و $\le 14.7\text{ m}$ عند 10 كم)، وتُدكَن مسارات الروفر على طبوغرافيا DTM المحلية بسلاسة.

### 3.3 بناء مسار العربة والمروحية ثلاثي الأبعاد وإضاءة المشهد الفيزيائية
- **بناء المسار الآمن (Traverse Curve & Dimensionality Handling)**:  
  يتم تحويل مصفوفة النقاط المحولة إلى منحنى ثلاثي الأبعاد من نوع `Poly Spline` مع تفعيل خاصية `Bevel Depth = 0.25m` لإنشاء شريط مسار العربة، وإعطائه مادة ذات توهج ضوئي ذهبي (`Emission Strength = 3.0`).  
  * **التعامل الآمن مع الإحداثيات ثنائية الأبعاد (2D Coordinates)**: بعض مجموعات البيانات الحقيقية لمهمات المريخ (مثل مسار رحلات المروحية إنجينويتي `ingenuity_flights_m20.geojson`) تخزن إحداثيات ثنائية الأبعاد فقط `[lon, lat]` دون إحداثي ارتفاع. لضمان عدم حدوث انهيار برمجي (`IndexError`)، يتم التحقق دائماً من بُعد النقطة واستخدام ارتفاع نقطة الأصل أو الارتفاع السابق الصالح كبديل تلقائي: `raw_alt = pt[2] if len(pt) > 2 else origin[2]`.  
  * **ترشيح قراءات مستشعر الارتفاع الساقطة (Telemetry Altitude Dropout Filter)**: تحتوي بيانات مسار بيرسيفيرانس `perseverance_traverse_m20.geojson` على 15 نقطة بها قراءات ارتفاع خام تقارب الصفر المطلق ($alt \approx 2.2 \times 10^{-5}\text{ m}$) نتيجة انقطاع مؤقت في قراءة المستشعر (Sensor Dropouts)، بينما تقع أرضية فوهة جيزيرو الحقيقية عند نحو $-2570\text{ m}$. بدون ترشيح هذه النقاط، تتشكل أعمدة شاذة ترتفع فجأة بمقدار **$+2.57\text{ km}$** عمودياً في المشهد ثلاثي الأبعاد. لذا يقوم الكود بترشيح أي نقطة بارتفاع خام $alt > -1000\text{ m}$ داخل جيزيرو واستبدالها بآخر ارتفاع صالح مسجل لمنع التشويه البصري.
- **الإضاءة الفيزيائية لغلاف جيزيرو**:
  * ضبط مصباح الشمس (Sun Light) عند شدة إشعاع حقيقية لمدار المريخ: $I_{\text{sun}} \approx 590\text{ W/m}^2$ مع زاوية قطرية $0.35^\circ$ لتكوين ظلال شبه حادة.
  * إضافة كتلة حبرية محيطية (Principled Volume) لتمثيل الغبار العالق في الهواء، مع ضبط معامل التشتت الأمامي (Anisotropy: $g = 0.65$) ولون امتصاص برتقالي مريخي دافئ (`#C16238`).

---

## 4. كيفية الاستفادة القصوى في محرك أنريل إنجن 5.8 (Unreal Engine 5.8)

يعد أنريل إنجن 5.8 البيئة المثالية لبناء محاكي النجاة التفاعلي للرواد وخوذة الواقع المعزز (Visor HUD):

### 4.1 إنشاء تضاريس نانيت (Nanite Landscape) باستخدام خرائط الارتفاع 16-bit Heightmaps
1. **طبيعة بيانات MOLA DEM وتوافقها مع 16-bit**:  
   يتميز ملف الارتفاع النموذجي `sample_mola_dem_slice.tif` بأنه مخزن أصلاً بتنسيق **عدد صحيح 16-بت ذي إشارة (`int16`, `BitsPerSample = 16`, `SampleFormat = 2`)** بدقة تكميم قدرها متر صحيح واحد لكل وحدة عددية (1-meter integer elevation quantization)، حيث تتراوح الارتفاعات في رقعة فوهة جيزيرو بين $-2667\text{ m}$ و $-1472\text{ m}$. أما في حال استخدام خرائط HiRISE DTM العائمة (32-bit Float)، فيتم تحويلها إلى 16-bit Grayscale PNG بأبعاد قياسية مناسبة للتقطيع في أنريل (مثل $2017 \times 2017$ أو $4033 \times 4033$).
2. في وضع التضاريس (Landscape Mode)، اختر استيراد خريطة الارتفاع وحدد الملف.
3. ضبط مقياس الأبعاد (Scale):
   - مقياس $X$ و $Y$: لبيانات HiRISE بدقة 1 متر يكون المقياس $100\%$. لبيانات MOLA بدقة ~231.5 متر/بكسل ($1/256^\circ$) يكون المقياس $23,150\%$.
   - مقياس $Z$ الرأسي في Unreal Engine 5.8:
     * عند التعامل مع بيانات MOLA `int16` المباشرة حيث كل وحدة عددية تمثل $1.0\text{ m}$، وبما أن وحدة العالم الافتراضي في أنريل هي السنتيمتر ($1\text{ Unreal Unit} = 1\text{ cm} = 0.01\text{ m}$)، فإن كل وحدة تكميم تمثل $100\text{ cm}$ رأسياً في المشهد (مقياس $Z = 1.0\text{ m}$ لكل خطوة عددية صحيحة).
     * عند تصدير الارتفاعات كخريطة رمادية 16-bit معيارية ممتدة على كامل المجال (Normalized 0 to 65535)، يُحسب مقياس $Z$ بالمعادلة القياسية لأنريل:  
       $$Z_{\text{scale}} = \Delta Z_{\text{meters}} \times 0.1953125$$  
       (لفارق ارتفاع قدره 1195 متراً في شريحة جيزيرو، يكون المقياس $Z \approx 233.4\%$).
4. تفعيل خاصية **Enable Nanite** في لوحة تفاصيل التضاريس للاستفادة من ملايين المضلعات دون أي فقدان في الأداء (LOD Streaming).

### 4.2 خامات التضاريس الافتراضية أثناء التشغيل (Runtime Virtual Texturing - RVT)
لتغطية مساحات شاسعة من التضاريس المريخية دون تجاوز حدود ذاكرة كرت الشاشة:
1. إنشاء أصلين من نوع RVT في متصفح المحتوى: `RVT_Mars_Material` لخامات الألوان والخشونة، و `RVT_Mars_Height` لمعلومات الارتفاع العالمي.
2. إضافة حجم `RuntimeVirtualTextureVolume` في المستوى وضبط أبعاده ليحيط بالتضاريس بالكامل.
3. في خامة التضاريس الأساسية، ربط طبقات الصخور الجيولوجية الممزوجة بمخرج **Runtime Virtual Texture Output**.
4. بالنسبة لعجلات العربة والصخور السطحية الملقاة، يتم أخذ عينة من الخامة الافتراضية (RVT Sample) لمزج قاعدة الصخور مع غبار التضاريس بانسيابية تامة تمنع الحواف القاطعة غير الواقعية.

### 4.3 نظام المسارات والشرائط التفاعلية (Splines & Niagara Dust Tracks)
- استخدام عنصر `USplineComponent` لاستيراد مسار بيرسيفيرانس وكيوريوسيتي من ملفات GeoJSON.
- إجراء تتبع خطي رأسي (Line Trace) نحو التضاريس لإلصاق كل نقطة مسار بسطح الأرض المريخي مع رفع طفيف بمقدار 10 سم لمنع التداخل البصري.
- ربط نظام جسيمات **Niagara** بنقاط تلامس عجلات الروفر لرسم آثار العجلات المضغوطة (Compressed Dust Decals) وتطاير رذاذ الغبار الناعم عند الحركة.

---

## 4.5 محرك الألعاب الرئيسي المعتمد: Unity Engine (Unity 6 — `6000.6.9f1` & `Unity CLI 1.0.0`)

يمثل **Unity 6 (`6000.6.9f1`)** المحرك الرئيسي المعتمد لبناء اللعبة التفاعلية وتصديرها عبر **WebGL** (للتقييم الفوري من المتصفح) و**Windows Standalone** و**OpenXR**:
1. **إدارة المشروع عبر `Unity CLI 1.0.0`**:
   - إنشاء المشروع وإدارته مباشرة عبر `unity projects create` وتشغيل مهارات Antigravity الخاصة بـ Unity (`unity setup antigravity`).
2. **استيراد المشهد من Blender 5.2**:
   - تصدير التضاريس (`NASA_HiRISE_DTM_Jezero_FieldSite_4m`) والأسطول السطحي والمداري من `MARS_SENTINEL_HERO_PRODUCTION.blend` بصيغة **FBX** أو **GLB** بمقياس `1 unit = 1 meter` مع تضمين خامات PBR.
   - استخدام خامات **URP / HDRP Lit** أو **Terrain Layers** مع خريطة الجيولوجيا `USGS SIM 3292` لتلوين الصخور والرمال المريخية بدقة علمية.
3. **تشغيل مسارات الروفر والبيانات العلمية في C#**:
   - قراءة ملفات `data/routes_traverses/*.geojson` (`Perseverance`, `Curiosity`, `Ingenuity`) وتحويلها عبر `latlon_to_enu` إلى نقاط `Vector3` في Unity لعرض المسارات عبر `LineRenderer` / `Splines` وتوجيه حركة المركبات والدرون.
   - محاكاة الإضاءة والغلاف الجوي المريخي في Unity عبر `Directional Light` (شمس المريخ بشدة وإمالة واقعية) مع `Volume` (Exponential/Volumetric Fog بلون الغبار المريخي) ودورة الليل والنهار.

---

## 5. كيفية الاستفادة القصوى في تطبيقات الويب والخرائط ثلاثية الأبعاد (Web 3D / GIS)

تتيح تقنيات الويب الحديثة بناء توأم رقمي (Digital Twin) مريخي يعمل على أي جهاز أو متصفح بدون تثبيت برامج:

### 5.1 إعداد بيئة CesiumJS مع كرواني المريخ الرسمي (IAU2000 Mars Ellipsoid)
بشكل افتراضي، تعمل مكتبة CesiumJS على مجسم الأرض الإهليلجي (WGS84). يتم تكييفها للعمل على كوكب المريخ عبر تعريف الكرواني الرسمي:

```javascript
import * as Cesium from 'cesium';

// تعريف كرواني المريخ المعتمد من الاتحاد الفلكي الدولي (نصف القطر 3,396,190 متر)
const marsEllipsoid = new Cesium.Ellipsoid(3396190.0, 3396190.0, 3376200.0);
Cesium.Ellipsoid.WGS84 = marsEllipsoid;

const viewer = new Cesium.Viewer('cesiumContainer', {
    globe: new Cesium.Globe(marsEllipsoid),
    baseLayerPicker: false,
    skyAtmosphere: new Cesium.SkyAtmosphere(marsEllipsoid),
    imageryProvider: false
});

// ضبط لون الغلاف الجوي ليتطابق مع تشتت الغبار المريخي المحمر
viewer.scene.globe.atmosphereHueShift = 0.1;
viewer.scene.globe.atmosphereSaturationShift = 0.35;
```

### 5.2 تحميل بلاطات التضاريس ثلاثية الأبعاد Quantized Mesh والخرائط الموزعة WMTS
- **استدعاء بلاطات التضاريس ثلاثية الأبعاد من سحابة ناسا**:
  ```javascript
  viewer.terrainProvider = new Cesium.CesiumTerrainProvider({
      url: 'https://marshub.s3.amazonaws.com/mars_v14/',
      ellipsoid: marsEllipsoid
  });
  ```
- **استدعاء طبقة صور الفايكنج أو MOLA الملونة عبر WMTS**:
  ```javascript
  const molaLayer = new Cesium.WebMapTileServiceImageryProvider({
      url: 'https://trek.nasa.gov/tiles/Mars/EQ/Mars_MGS_MOLA_ClrShade_merge_global_463m/1.0.0/default/default028mm/{TileMatrix}/{TileRow}/{TileCol}.jpg',
      layer: 'Mars_MGS_MOLA_ClrShade_merge_global_463m',
      style: 'default',
      format: 'image/jpeg',
      tileMatrixSetID: 'default028mm',
      maximumLevel: 7,
      ellipsoid: marsEllipsoid
  });
  viewer.imageryLayers.addImageryProvider(molaLayer);
  ```

### 5.3 عرض مسارات GeoJSON الحية ومحطات التوقف والربط مع صور ناسا المباشرة
- استدعاء ملف `perseverance_traverse_m20.geojson` مباشرة وتثبيته على سطح التضاريس (`clampToGround: true`).
- عند الضغط على أي نقطة مسار (Waypoint)، يظهر إشعار فوري يعرض الصورة الميدانية الخام عالية الدقة الملتقطة بواسطة كاميرا Mastcam-Z في ذلك اليوم المريخي مع قراءة زاوية الكاميرا والمسافة المقطوعة.

---

## 6. استراتيجيات البقاء والنجاة المعتمدة على البيانات الحقيقية (Data-Driven Survival)

إن جوهر "دليل البقاء المريخي" يكمن في تحويل قراءات الأجهزة الفضائية إلى قرارات بقاء حاسمة تنقذ حياة الطاقم:

### 6.1 دراسة حركة العربات وتقييم المخاطر (Trafficability & Sand Dune Hazards)
تظهر بيانات USGS SIM 3292 ومهمات المريخ أن أخطر العوائق ليست المنحدرات الصخرية فقط، بل **حقول الكثبان الرملية الحديثة من العصر الأمازوني (`lAd`)**:
- **تصنيف ملاءمة السير (Trafficability Index)**:
  * **الدرجة 5 (ممتاز - تدفقات البازلت الهسبيرية `lHv`)**: أرضيات صلبة ذات قدرة تحمل عالية؛ تسمح بسير العربات بسرعات قصوى مع تآكل منخفض للعجلات.
  * **الدرجة 3 (متوسط - هضاب العصر النواكي `mNh`)**: صخور متناثرة ورمال متماسكة جزئياً؛ تتطلب نظام تعليق متكيف وتجنب الحجارة الحادة.
  * **الدرجة 1 (شديد الخطورة - الكثبان والرمال النشطة `lAd`)**: حبيبات بازلتية ناعمة منعدمة التماسك تتسبب في انغراس العجلات ودورانها في الفراغ (Wheel Slippage) وفقدان الحركة التام. **قاعدة البقاء**: يحظر على المركبات الثقيلة دخول مساحات `lAd` إلا عبر مسارات صخرية محددة مسبقاً.

### 6.2 استخراج المياه والجليد تحت السطحي (ISRU Water Extraction Across Epochs)
المياه هي شريان الحياة لإنتاج وقود الصواريخ (الميثان والأكسجين السائلين عبر تفاعل ساباتييه) وتوفير مياه الشرب:
1. **في العصر الأمازوني (Amazonian Glacial Aprons - `lAa`)**:
   تحتوي ألسنة الحطام الجليدية (Lobate Debris Aprons) عند خطوط العرض المتوسطة (30° إلى 50° شمالاً وجنوباً) على **أنهار جليدية مائية شبه نقية (نقاء 80-90%)** محمية بطبقة من الركام والحصى بسماكة 0.5 إلى 2 متر. هذا هو الموقع الأمثل للتعدين السطحي المباشر.
2. **في العصر الهسبيري (Hesperian Sulfate Deposits)**:
   تحتوي معادن الكبريتات متعددة الهيدرات (Polyhydrated Sulfates مثل الجبس والكيزاريت) على جزيئات ماء متبلورة يمكن تحريرها بالتسخين المعتدل عند درجات حرارة بين 150 إلى 300 درجة مئوية.
3. **في العصر النواكي (Noachian Phyllosilicates - `eNh` / `mNh`)**:
   تحتوي أطيان السميكتيت (Smectite Clays) على مياه مرتبطة كيميائياً بنسبة 5% إلى 10%، وتتطلب أفران ميكروويف عالية الحرارة (450-600 درجة مئوية) لاستخراجها.

### 6.3 الحماية من الإشعاع والعواصف الشمسية (Radiation Shielding & Lava Tubes)
يتعرض سطح المريخ لجرعات إشعاعية قاتلة تصل إلى 250-300 ملي سيفرت سنوياً بسبب فقدان المجال المغناطيسي وضعف الغلاف الجوي:
- **أنفاق الحمم البركانية (Lava Tubes)**:  
  تضم مناطق ثارسيس وإليسيوم أنفاق حمم عملاقة سليمة يتجاوز عرضها 100 إلى 250 متراً وبارتفاع يصل إلى عشرات الأمتار، مغطاة بسقوف بازلتية توفر **حماية بنسبة 100% من الأشعة الكونية المجرية (GCR) والعواصف الشمسية المفاجئة (Solar Particle Events - SPE)**.
- **تلبيد الثرى المريخي (Regolith Microwave Sintering)**:  
  استخدام الثرى الغني بالأطيان والحديد لصهره بالميكروويف لإنتاج كتل وطوب بنائي عالي الكثافة لتغليف المساكن السطحية بسماكة لا تقل عن 1.5 متر لتخفيض الإشعاع إلى الحدود الآمنة بيولوجياً.

### 6.4 إدارة الطاقة والنجاة من العواصف الترابية الشاملة (Solar Budget & Dust Storms)
عندما ترتفع العواصف الترابية الإقليمية إلى عاصفة كوكبية شاملة (كما حدث عام 2018)، ينخفض ضوء الشمس المباشر بشكل دراماتيكي يخضع لقانون بير-لامبرت:

$$I = I_0 \cdot \exp\left(-\frac{\tau}{\cos\theta_z}\right)$$

حيث $I_0 \approx 590\text{ W/m}^2$ هو الإشعاع الشمسي أعلى الغلاف الجوي للمريخ، و $\tau$ هو عمق الغلاف الجوي البصري (Aerosol Optical Depth).  
- **بروتوكول البقاء في حالات الطوارئ**:
  * عندما يتجاوز $\tau > 3.0$، ينخفض ضوء الشمس المباشر بنسبة تتجاوز $95\%$.
  * يجب على المستعمرة إيقاف كافة الرحلات الاستكشافية غير الضرورية فوراً، والدخول في وضع الحفاظ على الطاقة (Survival Power Mode)، والاعتماد الحصري على المولدات النووية الحرارية (RTG) ومخزون خلايا الوقود، مع تغطية الأجهزة الحساسة لمنع تراكم ذرات الغبار الكهروستاتيكية الدقيقة.

---

## 7. مكتبة الأكواد والسكريبتات الجاهزة للتطبيق الفوري (Production Code Vault)

### 7.1 كود بايثون لمعالجة وتحويل مسار الروفر إلى منحنى ثلاثي الأبعاد في بلندر

```python
"""
Blender 5.2 Python Tool: Import NASA Rover Traverse into Local 3D Curve
"""
import bpy
import json
import math
import os

MARS_RADIUS = 3396190.0  # IAU2000 Mars Radius in meters

def latlon_to_enu(lat, lon, alt, origin_lat, origin_lon, origin_alt):
    """Transform planetocentric lat/lon/alt to Local East-North-Up (ENU) coordinates.
    Includes robust [-180, 180] vs [0, 360] longitude normalization and antimeridian handling.
    """
    d_lat = math.radians(lat - origin_lat)
    d_lon_deg = (lon - origin_lon + 180.0) % 360.0 - 180.0
    d_lon = math.radians(d_lon_deg)
    mid_lat = math.radians((lat + origin_lat) / 2.0)
    
    x = MARS_RADIUS * math.cos(mid_lat) * d_lon
    y = MARS_RADIUS * d_lat
    z = (alt - origin_alt) if alt is not None else 0.0
    return x, y, z

def build_traverse_curve(geojson_path, origin=(18.44215, 77.44287, -2569.0), curve_name="Perseverance_Track"):
    if not os.path.exists(geojson_path):
        print(f"Error: {geojson_path} not found.")
        return None
        
    with open(geojson_path, "r", encoding="utf-8") as f:
        data = json.load(f)
        
    points = []
    last_valid_alt = origin[2]

    def extract_point(pt):
        nonlocal last_valid_alt
        # Safely handle 2D coordinates ([lon, lat]) vs 3D ([lon, lat, alt])
        raw_alt = pt[2] if len(pt) > 2 else last_valid_alt
        
        # Filter telemetry altitude dropouts in Jezero floor (~-2570m):
        # raw_alt near 0.0m (> -1000m) represents uninitialized sensor readings
        if len(pt) > 2 and raw_alt > -1000.0 and origin[2] < -2000.0:
            raw_alt = last_valid_alt
        else:
            last_valid_alt = raw_alt
            
        return latlon_to_enu(pt[1], pt[0], raw_alt, origin[0], origin[1], origin[2])

    for feature in data.get("features", []):
        geom = feature.get("geometry", {})
        coords = geom.get("coordinates", [])
        gtype = geom.get("type")
        
        if gtype == "LineString":
            for pt in coords:
                points.append(extract_point(pt))
        elif gtype == "MultiLineString":
            for segment in coords:
                for pt in segment:
                    points.append(extract_point(pt))

    if not points:
        print("No valid points found.")
        return None

    # Create 3D curve in Blender
    curve_data = bpy.data.curves.new(name=curve_name, type='CURVE')
    curve_data.dimensions = '3D'
    curve_data.bevel_depth = 0.2  # 20cm thickness ribbon
    curve_data.bevel_resolution = 4
    
    spline = curve_data.splines.new(type='POLY')
    spline.points.add(len(points) - 1)
    for i, p in enumerate(points):
        spline.points[i].co = (p[0], p[1], p[2], 1.0)
        
    obj = bpy.data.objects.new(curve_name, curve_data)
    bpy.context.collection.objects.link(obj)
    print(f"Created {curve_name} with {len(points)} vertices.")
    return obj
```

### 7.2 سكريبت بايثون لحساب ميزانية الطاقة الشمسية بناءً على عمق الغبار البصري ($\tau$)

```python
"""
Martian Solar Energy Budget Calculator
Calculates surface direct & diffuse solar irradiance under dust storm conditions.
"""
import math

def calculate_martian_solar_power(optical_depth_tau, zenith_angle_deg=30.0, panel_area_m2=2.2, efficiency=0.28):
    """
    Computes available solar panel wattage on Mars given atmospheric tau.
    """
    S0 = 590.0  # Top of Atmosphere Martian Solar Irradiance (W/m^2)
    theta_rad = math.radians(zenith_angle_deg)
    cos_theta = math.cos(theta_rad)
    
    if cos_theta <= 0:
        return 0.0  # Nighttime
        
    # Beer-Lambert direct beam flux
    direct_flux = S0 * cos_theta * math.exp(-optical_depth_tau / cos_theta)
    
    # Diffuse forward scattering flux estimation
    diffuse_ratio = 0.35 * (1.0 - math.exp(-0.8 * optical_depth_tau))
    diffuse_flux = S0 * cos_theta * diffuse_ratio * math.exp(-0.25 * optical_depth_tau)
    
    total_irradiance = direct_flux + diffuse_flux
    total_power_watts = total_irradiance * panel_area_m2 * efficiency
    
    return {
        "optical_depth_tau": optical_depth_tau,
        "direct_flux_w_m2": round(direct_flux, 2),
        "diffuse_flux_w_m2": round(diffuse_flux, 2),
        "total_irradiance_w_m2": round(total_irradiance, 2),
        "generated_power_watts": round(total_power_watts, 2),
        "status": "CRITICAL POWER WARNING" if total_power_watts < 35.0 else "NOMINAL OPERATIONS"
    }

# Example usage:
if __name__ == "__main__":
    for tau in [0.5, 1.5, 3.5, 6.0]:
        res = calculate_martian_solar_power(tau)
        print(f"Tau: {tau:3.1f} -> Irradiance: {res['total_irradiance_w_m2']:6.1f} W/m^2 | Power: {res['generated_power_watts']:5.1f} W [{res['status']}]")
```

---

## 8. الخاتمة وخارطة طريق المشاركين في الهاكاثون

يمثل هذا الدليل التوليفة الأكثر عمقاً وشمولية للبيانات المريخية المتاحة حتى اليوم. من خلال استثمار ملفات الـ GeoJSON لمسارات الروبوتات، وخرائط الارتفاع الرقمية (MOLA 16-bit Signed Integer DTM و HiRISE 32-bit Float)، وقواعد البيانات الجيولوجية لـ USGS SIM 3292، والصور الميدانية المباشرة لبيرسيفيرانس، يمتلك فريق المشروع الأساس العلمي المتين والتقنيات البرمجية المتطورة للفوز بالمركز الأول في **NASA Space Apps Challenge**.

# TimeRush — ملاحظات التطوير

## النطاق

هذه النسخة تطوّر لعبة TimeRush الحالية كما هي: لعبة Endless Arcade بثلاثة مسارات ثابتة، لاعب Cyan، عوائق تسقط من الأمام، وتدفق Boot → MenuHub → Game → Results. لم تتم إضافة نسخة ويب أو تغيير نوع اللعبة.

## الحركة

يفرض `PlayerController` المسارات الثلاثة `-2.5` و`0` و`+2.5` ويحوّل إدخال A/D أو الأسهم الأفقية إلى تغيير Lane مباشر. أضيفت أيضاً حركة عمق محدودة على محور الطريق عبر W/S أو الأسهم العمودية، ضمن نطاق آمن ±2 حول نقطة بدء اللاعب. الانتقال يستخدم `SmoothDamp` بزمن قصير وحد أقصى واضح للسرعة، مع دعم السحب الأفقي والعمودي على اللمس. أضيف ميلان بصري على كائن `Visual` أثناء التبديل، من دون تغيير كوليدر اللاعب أو Rigidbody.

## العوائق والعدالة

يحافظ `ObstacleSpawner` على العشوائية، منع التكرار الفوري، الفجوة الرأسية، إشغال منطقة اللاعب، وقواعد منع الفخاخ. يثبت أيضاً مصفوفة المسارات إلى القيم الثلاث المطلوبة. تُجمع كل المسارات الصالحة أولاً، ثم يُمنح المسار الأقل استخداماً أولوية عشوائية ناعمة عند تأخره، من دون تسلسل ثابت أو اختيار مسار خطر. تضاف ثلاثة مواضع عمق عشوائية متباعدة، ويُرفض موضع العمق المشغول في نفس Lane؛ لذلك تظهر عوائق تتطلب تغيير المسار وأخرى يمكن تفاديها بتقدم أو تراجع محدود، من دون إغلاق جميع المسارات أو فرض استجابة مستحيلة. في الضبط النهائي زادت الفجوة الرأسية في المسار نفسه إلى 7 وحدات، ونافذة حماية القرار إلى ثانية واحدة، ونافذة منع قفل الجانبين إلى 1.8 ثانية؛ وهذه القيمة تغطي فعلياً عقبتين متتاليتين عند أسرع فاصل توليد (0.85 ثانية لكل منهما)، فتمنع تسلسل الجانبين المتقابلين من أن يتبعه عائق جانبي ثالث قبل عودة خيار الوسط الآمن.

في Phase 1 أصبحت `GameBalanceConfig` هي ملكية قيم الصعوبة والتوليد والعدالة، وأصبحت `TrackLayoutConfig` هي ملكية مواقع المسارات الثلاثة ومواضع العمق ونطاق العمق الآمن. قيم الأصول مطابقة لضبط الجولة السابق: لا تغيير في السرعات أو الفواصل أو حجم المسارات. يمر مرشح العائق الآن، قبل `Instantiate`، عبر `FairnessValidator` الخالص الذي يفحص ما إذا كان من موضع اللاعب الفعلي إجراء نجاة قابل للوصول زمنياً عبر lane أو depth أو الجمع بينهما، مع أخذ العوائق الحية وسرعاتها في الاعتبار. يبقى التوليد العادي عشوائياً، بينما يتيح خيار seed حتمي مخصص للتصحيح والاختبارات فقط. توفر `FairnessSimulation` ومشغّل محرر Unity عينات قابلة لإعادة التشغيل بعدد 10,000 سيناريو للمستويات المبكرة والمتوسطة والقصوى، من دون UI runtime أو تأثير على اللعب.

## الصعوبة والنتيجة

يحافظ `GameController` على score وbest score وGame Over وRestart وContinue، مع مرحلة تعلم واضحة مدتها 25 ثانية: يبدأ فاصل التوليد عند 1.9 ثانية وتبقى أول الجولة منخفضة الكثافة، ثم يزداد الإيقاع تدريجياً حتى يصل الفاصل إلى 0.85 والسرعة من 4.1 إلى 8.5. يُثبت منحنى الصعوبة عند 120 ثانية، فلا تستمر السرعة أو الكثافة أو تنوع العمق في الارتفاع بعد ذلك. تتسع خيارات عمق التوليد تدريجياً ضمن نفس السقف بدلاً من الاعتماد على زيادة السرعة وحدها. أضيف `AliveTime` للـHUD، كما أصبح `scorePerSecond` في مشهد Game يساوي 10 حتى تكون الجولة محسوسة في زمن قصير.

## Near Miss

تم تفعيل `NearMissDetector` الحالي runtime داخل منطقة حول اللاعب. تُحتسب مكافأة النظام فقط للعائق المجاور الذي يمر قريباً من اللاعب، ويُستبعد العائق الموجود في نفس Lane أو الاصطدام الفعلي. بنيت هذه المخاطرة في سلسلة Flow قصيرة: كل ثلاث Near Misses ضمن نافذة ست ثوانٍ ترفع المضاعف من x1 حتى x4 كحد أقصى، وتُطبق المكافأة على المرور الدقيق فقط لا على نقاط البقاء العادية. يظهر في HUD feedback قصير يوضح المكافأة والمضاعف، ويختفي مؤشر Flow إذا انقطعت السلسلة.

## المشهد والواجهة

تم تفعيل `CameraFollow` الموجود في Game.unity وربطه بتحويل اللاعب مع زاوية علوية وخلفية أوسع وFOV 64 ونقطة نظر متقدمة لإظهار اللاعب والعوائق والمساحة الأمامية في إطار واحد أوضح. كُبّر Visual اللاعب إلى 1.18 فقط، بينما بقي collider اللاعب بحجمه 1×1×1. يظهر العائق أصغر عند ظهوره (0.62 من حجمه المرئي) ثم يكبر تدريجياً حتى 1.12 قرب منطقة الخطر، مع نبضة بصرية خفيفة، من دون تغيير collider العائق أو physics. يستجيب اللاعب أيضاً بنبضة حجم خفيفة وميلان أثناء تغيير المسار أو العمق، لتأكيد الإدخال من دون أي تغيير في السرعة أو collider. تم إصلاح مقياس جذر HUD الصفرِي وتوحيد Canvas إلى 1920×1080. يعرض HUD الآن Score وBest وTIME وPACE وFLOW والحالة الحالية، مع نص تحكم مختصر يوضح A/D للمسارات وW/S للعمق، ثم يخف تدريجياً بعد مرحلة التعلم. أعيد بناء MenuHub runtime بصرياً مع الحفاظ على زر START وانتقال Game، وصُقلت Results runtime بألوان TimeRush وأزرار Continue وRestart وMenu نفسها.

## المواد

تم صقل ألوان ونعومة المواد الموجودة فقط: Cyan للاعب وخطوط المسارات، Orange/Red للعوائق، وViolet للحدود. لم تتم إضافة أصول خارجية أو تغيير colliders أو prefab references.

## التحقق

تم تشغيل `git diff --check` وفحص توازن الأقواس في ملفات C#، والتحقق من مراجع المشهد والـprefab والـGUIDات الأساسية. كما مر اختبار static لعقد الحركة والعوائق؛ وقت الاستجابة عند الحد الأقصى هو 1.059 ثانية مقابل 0.673 ثانية لتحرك اللاعب عبر كامل نطاق العمق، ويتحقق الاختبار من ثبات الصعوبة بعد 120 ثانية. لا يتوفر Unity Editor أو Unity CLI في بيئة التنفيذ الحالية، لذلك يلزم فتح المشروع في Unity 2022.3.62f3 لمراجعة Console وPlay Mode فعلياً قبل النشر.

## Phase 3 — Game Feel / Juice & Feedback Foundation

تستخدم Phase 3 قناة أحداث قوية النوع `GameFeedbackSignals` تملك `FeedbackEventHub` مستقلاً، ولا تستخدم أسماء أحداث نصية. تصدر طبقات اللعب إشارات lane وdepth بعد اعتماد حركة اللاعب، وإشارة Near Miss بعد تحقق الكاشف الحقيقي ومنح النقاط، وإشارة Collision قبل طلب Game Over، وإشارة Game Over من `GameController`. يحوّل `FeedbackStateRelay` انتقالات FSM إلى إشارات بدء الجولة والإيقاف والاستئناف، ويصدر `PaceFeedbackEmitter` milestone مرئياً فقط من Pace القائم من دون تغيير منحنى الصعوبة.

طبقات الاستهلاك مستقلة عن Gameplay: `FeedbackVfxPresenter` يعيد استخدام pool صغير من ParticleSystems ينشأ مرة واحدة في Boot، و`FeedbackAudioPresenter` يوفر hooks صامتة اختيارية للـclips المستقبلية، و`CameraFeedbackController` يضيف shake قصيراً إلى `CameraFollow` من خلال offset إضافي فقط، بينما يعرض `ScreenFeedbackPresenter` flash خفيفاً في HUD عند Near Miss أو hit. يحتوي `FeedbackConfig` على كل قيم الـjuice القابلة للضبط، ويحفظ `FeedbackPreferences` خيارات camera shake وReduce Flashing والصوت محلياً من دون واجهة Settings جديدة في هذه المرحلة.

لا تتغير Phase 3 سرعة اللاعب أو المسارات أو حدود العمق أو colliders أو FairnessValidator أو توقيت التوليد أو الصعوبة أو النقاط أو Flow. أزيلت قناة `FeedbackRaised` النصية القديمة من GameController وScoreUIBinder؛ يستقبل HUD الآن Near Miss typed ويحافظ على النص والمكافأة والمضاعف نفسيهما. تشترك كل طبقة مستهلكة في `OnEnable` وتلغي اشتراكها في `OnDisable`، وتُمسح المؤثرات المؤقتة عند Pause.

أضيفت اختبارات محرر لـtyped feedback payloads وإلغاء الاشتراك، ووُسّع التدقيق الساكن لعقود الأحداث وغياب القناة النصية وغياب Instantiate لكل VFX event ومراجع الأصول. يحتاج Compile وNUnit وPlay Mode وConsole وProfiler في Unity 2022.3.62f3 إلى تحقق فعلي عند توفر المحرر؛ لا تسجل هذه المرحلة Runtime PASS من دون ذلك.

## Phase 4 — HUD, Menus & Results UX Foundation

أضيف `SafeAreaFitter` كطبقة عرض صغيرة ومعاد استخدامها فقط على جذر HUD (UGUI) وجذر MenuHub (UI Toolkit) وCanvas النتائج. يحوّل Safe Area إلى anchors معيارية للـUGUI ويضيف padding محسوباً للـUI Toolkit، مع إعادة الحساب فقط عند تغير أبعاد الشاشة أو منطقة الأمان. لا يملك هذا المكوّن أي منطق لعب أو مشهد أو انتقالات، ولا يغيّر المسارات أو الكاميرا أو colliders أو المقاييس الفيزيائية.

يتوقف `ScoreUIBinder` عن تحديث العرض خارج حالة `Playing` التي يملكها FSM، فيمنع تحديث HUD أثناء Pause أو الانتقال. يعرض MenuHub أفضل نتيجة محلية ضمن سطر الهوية، ويوضح تحكم lane والعمق، ويمنع الضغط السريع المكرر على START؛ يبقى انتقال بدء الجولة معتمداً على `GameStateMachine.StartRunFromMenu()` عند وجود FSM، مع fallback محرر المشروع السابق فقط عند عدم وجوده.

تستعيد Results واجهة قابلة للاستخدام إذا احتوى المشهد القديم على Canvas بمقياس صفري: يعاد مقياس Canvas إلى واحد ويُنشأ فقط ما ينقص من لوحة Score وأزرار Continue/Restart/Menu، من دون حذف الحقول serialized القديمة أو تبديل آلية Continue. تعرض النتائج Score وBest وحالة `NEW BEST` من flag typed يلتقط قبل حفظ Best، ولا تعرض سبب خسارة محدداً إلا عندما يمرر `GameController` سبب `ObstacleCollision` المؤكد من `KillOnHit`؛ في جميع الحالات غير المصنفة تستخدم العبارة العامة `RUN ENDED`. تستمر Restart وMenu في تفويض الانتقال إلى FSM، مع guard يمنع طلبات اللمس المتكررة.

أضيفت اختبارات Edit Mode لـتحويل Safe Area وللقرارات النصية في Results، بما في ذلك عدم اختراع سبب خسارة. نجحت تدقيقات Phase 1–4 الساكنة، وتدقيق التواقيع C#، و`git diff --check`، وتدقيق GUIDات ومراجع scene/prefab، والتحقق من عدم تغيير `GameBalanceConfig` أو `TrackLayoutConfig`. **(ملاحظة تاريخية)** في وقت كتابة هذا التدقيق سابقاً، لم يكن Unity Editor أو Unity CLI متاحاً في تلك البيئة، فبقي حينها Compile وNUnit وPlay Mode وConsole وProfiler **محجوبة** بانتظار تنفيذها في Unity 2022.3.62f3؛ هذه العبارة لا تمثل حالة المشروع الحالية.

**تحقق حالي (حقائق مؤكدة فقط):** يستطيع Unity 2022.3.62f3 في وضع batchmode تحميل المشروع بنجاح. اختبارات Unity EditMode: 26/26 PASS. تحقق runtime لـPhase 10 (mock للإعلان المكافئ): 5/5 سيناريوهات PASS، والسيناريوهات الخمسة المتحققة هي RewardGranted وClosedWithoutReward وUnavailable وFailed وDoubleClick. لم يُتحقق بعد من Play Mode الكامل أو الاختبار اليدوي للانحدار، ولا يُدّعى ذلك ما لم يُتحقق منه صراحةً. ولا يُدّعى تحقق Profiler.

## Phase 7 — Runtime Verification Checklist (Feedback + Accessibility)

MENU:
- Open MenuHub.
- Toggle Camera Shake, Reduce Flashing, and Audio.
- Leave/re-enter MenuHub and verify saved values persist.

GAME:
- Start run from MenuHub.
- Pause, open Settings, change each toggle, close Settings, Resume.
- Verify run continues without restart and without score/difficulty/spawn/movement changes.

FEEDBACK:
- Verify Lane and Depth movement feedback remains functional.
- Verify Near Miss and Collision feedback fire as before (collision flash remains intentional).
- Verify Pause/Resume and Pace milestone feedback behavior.
- Verify Game Over feedback and transition behavior.

FLOW:
- Verify full loop: Boot -> MenuHub -> Game -> Results -> Menu.

## Phase 8 — Pre-Merge Verification Gate

Before accepting Scene/Prefab changes:

1. C# diagnostics PASS.
2. Existing tests PASS.
3. FeedbackConfigReferenceValidator PASS.
4. Missing Script scan PASS.
5. GUID resolution PASS.
6. .cs/.meta integrity PASS.
7. `git diff --check` PASS.
8. Unity Play Mode verification when Unity Editor is available.

CI-ready editor validation entry point:
- Run Unity in batch mode with `FeedbackConfigReferenceValidator.ValidateOrThrow` when a Unity-capable environment is available.
- Actual Unity Editor execution remains environment-dependent and is not replaced by `dotnet test` in this repository.

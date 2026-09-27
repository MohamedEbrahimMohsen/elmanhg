/* Elmanhg wireframe prototype - seed data (window.SEED).
   All timestamps are relative to page-load time so the demo always looks "fresh".
   Question compact format: [type, stem, body, gradingSpec, difficulty, objectiveIndex, explanation, overrides?] */
(function () {
  'use strict';
  var now = Date.now(), H = 3600e3, D = 24 * H;

  var subjects = [
    { id: 1, name: 'الفيزياء', order: 1, defaultBlueprintId: 'bp-s1' },
    { id: 2, name: 'الرياضيات', order: 2, defaultBlueprintId: 'bp-s2' }
  ];
  var units = [
    { id: 11, subjectId: 1, name: 'الكهربية التيارية', order: 1, blueprintId: 'bp-u11' },
    { id: 12, subjectId: 1, name: 'المغناطيسية', order: 2, blueprintId: 'bp-u12' },
    { id: 21, subjectId: 2, name: 'التفاضل', order: 1, blueprintId: 'bp-u21' },
    { id: 22, subjectId: 2, name: 'التكامل', order: 2, blueprintId: 'bp-u22' }
  ];

  function lesson(id, unitId, subjectId, order, name, state, p1, p2, objs, summary) {
    return {
      id: id, unitId: unitId, subjectId: subjectId, order: order, name: name, state: state,
      explanation: p1 + '\n\n' + p2,
      objectives: objs.map(function (t, i) { return { id: 'o' + id + '-' + (i + 1), text: t }; }),
      summary: summary, publishedAt: state === 'published' ? now - 30 * D : null
    };
  }

  var lessons = [
    lesson(111, 11, 1, 1, 'التيار الكهربي وقانون أوم', 'published',
      'التيار الكهربي هو معدل سريان الشحنة الكهربية خلال مقطع من موصل، ويُقاس بوحدة الأمبير. تتحرك الإلكترونات الحرة في الموصل عند وجود فرق جهد بين طرفيه.',
      'ينص قانون أوم على أن شدة التيار المار في موصل تتناسب طرديًا مع فرق الجهد بين طرفيه عند ثبوت درجة الحرارة، أي أن V = I × R حيث R هي المقاومة الكهربية وتقاس بالأوم.',
      ['تعريف التيار الكهربي ووحدة قياسه', 'تطبيق قانون أوم لحساب الجهد أو التيار أو المقاومة', 'تحديد العوامل المؤثرة على مقاومة الموصل'],
      'التيار = الشحنة ÷ الزمن. قانون أوم: V = IR عند ثبوت درجة الحرارة. المقاومة تزداد بزيادة الطول وتقل بزيادة مساحة المقطع.'),
    lesson(112, 11, 1, 2, 'توصيل المقاومات', 'published',
      'عند توصيل المقاومات على التوالي يمر نفس التيار في جميع المقاومات، وتكون المقاومة المكافئة مساوية لمجموع المقاومات.',
      'عند التوصيل على التوازي يكون فرق الجهد متساويًا على جميع المقاومات، ومقلوب المقاومة المكافئة يساوي مجموع مقلوبات المقاومات، لذلك تكون المكافئة أصغر من أصغر مقاومة.',
      ['حساب المقاومة المكافئة على التوالي', 'حساب المقاومة المكافئة على التوازي', 'المقارنة بين التوصيلين من حيث التيار والجهد'],
      'توالي: R = R1 + R2 والتيار ثابت. توازي: 1/R = 1/R1 + 1/R2 والجهد ثابت.'),
    lesson(121, 12, 1, 1, 'المجال المغناطيسي', 'published',
      'المجال المغناطيسي هو الحيز المحيط بالمغناطيس أو بالموصل الذي يمر به تيار، وتظهر فيه آثار القوة المغناطيسية. تُقاس كثافة الفيض بوحدة التسلا.',
      'كثافة الفيض المغناطيسي حول سلك مستقيم تتناسب طرديًا مع شدة التيار وعكسيًا مع البعد عن السلك، ويُحدَّد اتجاهه بقاعدة قبضة اليد اليمنى.',
      ['تعريف المجال المغناطيسي ووحدته', 'تحديد اتجاه المجال بقاعدة قبضة اليد اليمنى', 'حساب كثافة الفيض حول سلك مستقيم'],
      'B = μI / 2πd حول سلك مستقيم. الوحدة: تسلا. الاتجاه: قاعدة قبضة اليد اليمنى.'),
    lesson(122, 12, 1, 2, 'الحث الكهرومغناطيسي', 'published',
      'الحث الكهرومغناطيسي هو تولد قوة دافعة كهربية مستحثة في موصل عند تغير الفيض المغناطيسي الذي يقطعه.',
      'ينص قانون فاراداي على أن القوة الدافعة المستحثة تتناسب طرديًا مع معدل تغير الفيض وعدد اللفات، ويحدد قانون لنز اتجاه التيار المستحث بحيث يقاوم التغير المسبب له.',
      ['تعريف الحث الكهرومغناطيسي', 'تطبيق قانون فاراداي', 'تحديد اتجاه التيار المستحث بقانون لنز'],
      'emf = −N ΔΦ/Δt. إشارة السالب تعبر عن قانون لنز: التيار المستحث يقاوم سبب حدوثه.'),
    lesson(211, 21, 2, 1, 'قواعد الاشتقاق', 'published',
      'المشتقة تقيس معدل تغير الدالة بالنسبة للمتغير. مشتقة الدالة الثابتة صفر، ومشتقة x^n هي n·x^(n−1).',
      'مشتقة مجموع دالتين تساوي مجموع مشتقتيهما، ومشتقة حاصل ضرب دالتين تُحسب بقاعدة الضرب: (fg)\' = f\'g + fg\'.',
      ['اشتقاق دوال القوى', 'تطبيق قاعدتي المجموع والضرب', 'حساب قيمة المشتقة عند نقطة'],
      'مشتقة x^n هي n x^(n−1). مشتقة المجموع = مجموع المشتقات. قاعدة الضرب: f\'g + fg\'.'),
    lesson(212, 21, 2, 2, 'تطبيقات المشتقة', 'published',
      'تُستخدم المشتقة الأولى لإيجاد ميل المماس لمنحنى عند نقطة، ولتحديد فترات تزايد الدالة وتناقصها.',
      'النقاط الحرجة هي التي تنعدم عندها المشتقة الأولى، وقد تكون نقاط قيم عظمى أو صغرى محلية، ويُحدَّد نوعها باختبار المشتقة الثانية.',
      ['إيجاد ميل المماس', 'تحديد فترات التزايد والتناقص', 'إيجاد القيم العظمى والصغرى'],
      'ميل المماس = f\'(x). إذا كانت f\' موجبة فالدالة متزايدة، وإذا كانت سالبة فهي متناقصة. f\'\' موجبة تعني صغرى و f\'\' سالبة تعني عظمى.'),
    lesson(221, 22, 2, 1, 'التكامل غير المحدد', 'published',
      'التكامل غير المحدد هو العملية العكسية للاشتقاق، ونضيف دائمًا ثابت التكامل c لأن مشتقة الثابت صفر.',
      'تكامل x^n يساوي x^(n+1)/(n+1) + c بشرط n ≠ −1، وتكامل مجموع دالتين يساوي مجموع تكامليهما.',
      ['فهم التكامل كعملية عكسية للاشتقاق', 'تكامل دوال القوى', 'إضافة ثابت التكامل واستخدام الشروط الابتدائية'],
      '∫ x^n dx = x^(n+1)/(n+1) + c حيث n ≠ −1. لا تنسَ ثابت التكامل.'),
    lesson(222, 22, 2, 2, 'التكامل المحدد', 'draft',
      'التكامل المحدد لدالة على فترة [a, b] يعطي قيمة عددية، ويُحسب بإيجاد دالة أصلية F ثم حساب F(b) − F(a).',
      'يُستخدم التكامل المحدد لحساب المساحة المحصورة بين منحنى الدالة ومحور السينات على فترة معينة.',
      ['حساب التكامل المحدد', 'استخدام النظرية الأساسية للتفاضل والتكامل', 'حساب المساحات تحت المنحنى'],
      '∫ من a إلى b لـ f(x) dx = F(b) − F(a). النتيجة عدد وليست دالة.')
  ];

  function mcq(o, c) { return [{ options: o }, { correct: c }]; }
  function multi(o, c) { return [{ options: o }, { correct: c }]; }
  function num(v, unit, tol) { return [{ kind: 'numeric', unit: unit || '' }, { kind: 'numeric', value: v, tol: tol || 0, tolType: 'abs' }]; }
  function txt(acc) { return [{ kind: 'text' }, { kind: 'text', accepted: acc }]; }
  function fill(acc) { return [{ blanks: acc.length }, { accepted: acc }]; }

  var Q = {
    111: [
      ['mcq', 'وحدة قياس شدة التيار الكهربي هي:', mcq(['الفولت', 'الأمبير', 'الأوم', 'الوات'], 1), 'easy', 0, 'شدة التيار تقاس بالأمبير وهو كولوم لكل ثانية.'],
      ['mcq', 'مقاومة يمر بها تيار 2 أمبير وفرق الجهد بين طرفيها 10 فولت، قيمتها:', mcq(['5 أوم', '20 أوم', '12 أوم', '0.2 أوم'], 0), 'medium', 1, 'R = V / I = 10 / 2 = 5 أوم.'],
      ['mcq', 'إذا زاد طول سلك إلى الضعف مع ثبات مساحة مقطعه فإن مقاومته:', mcq(['تقل للنصف', 'تزداد للضعف', 'لا تتغير', 'تزداد أربع مرات'], 1), 'medium', 2, 'المقاومة تتناسب طرديًا مع طول السلك.', { status: 'pending' }],
      ['multi', 'أي مما يلي يؤثر على مقاومة موصل؟ (اختر كل ما ينطبق)', multi(['طول الموصل', 'مساحة المقطع', 'نوع المادة', 'لون العازل'], [0, 1, 2]), 'medium', 2, 'المقاومة تعتمد على الطول ومساحة المقطع ونوع المادة ودرجة الحرارة، ولا علاقة للون العازل.'],
      ['tf', 'يسري التيار الاصطلاحي من القطب السالب إلى القطب الموجب خارج المصدر.', [{}, { correct: false }], 'easy', 0, 'الاتجاه الاصطلاحي للتيار من الموجب إلى السالب خارج المصدر.'],
      ['fill', 'ينص قانون ____ على أن شدة التيار تتناسب طرديًا مع ____.', fill([['أوم'], ['فرق الجهد', 'الجهد']]), 'easy', 1, 'قانون أوم: التيار يتناسب طرديًا مع فرق الجهد.'],
      ['short', 'سلك مقاومته 4 أوم يمر به تيار 3 أمبير. احسب فرق الجهد بالفولت.', num(12, 'فولت'), 'medium', 1, 'V = I × R = 3 × 4 = 12 فولت.'],
      ['math_steps', 'مقاومة 8 أوم موصلة بمصدر جهده 24 فولت. احسب شدة التيار بالأمبير واكتب خطوات الحل.', [{}, { finalAnswers: ['3', '3 امبير', '3A'] }], 'hard', 1, 'I = V / R = 24 / 8 = 3 أمبير.', { maxScore: 2 }]
    ],
    112: [
      ['mcq', 'المقاومة المكافئة لمقاومتين 3 أوم و 6 أوم على التوالي تساوي:', mcq(['2 أوم', '9 أوم', '18 أوم', '3 أوم'], 1), 'easy', 0, 'على التوالي: R = 3 + 6 = 9 أوم.'],
      ['mcq', 'المقاومة المكافئة لمقاومتين 3 أوم و 6 أوم على التوازي تساوي:', mcq(['9 أوم', '2 أوم', '18 أوم', '4.5 أوم'], 1), 'medium', 1, '1/R = 1/3 + 1/6 = 1/2 إذن R = 2 أوم.'],
      ['mcq', 'في التوصيل على التوازي تكون الكمية الثابتة على جميع المقاومات هي:', mcq(['التيار', 'فرق الجهد', 'المقاومة', 'القدرة'], 1), 'easy', 2, 'فرق الجهد متساوٍ على الفروع المتوازية.'],
      ['multi', 'أي العبارات التالية صحيحة عن التوصيل على التوالي؟', multi(['التيار واحد في جميع المقاومات', 'المقاومة المكافئة أكبر من أكبر مقاومة', 'الجهد متساوٍ على كل المقاومات', 'تلف مقاومة واحدة يقطع الدائرة'], [0, 1, 3]), 'medium', 2, 'على التوالي: التيار ثابت، والمكافئة مجموع المقاومات، وأي قطع يفتح الدائرة.', { status: 'pending' }],
      ['tf', 'المقاومة المكافئة على التوازي أصغر من أصغر مقاومة في المجموعة.', [{}, { correct: true }], 'medium', 1, 'لأن مقلوبها يساوي مجموع المقلوبات فهو أكبر من أي مقلوب منفرد.'],
      ['fill', 'في التوصيل على ____ يكون التيار ثابتًا في جميع المقاومات.', fill([['التوالي', 'توالي']]), 'easy', 2, 'التوالي: مسار واحد للتيار.'],
      ['short', 'ثلاث مقاومات كل منها 6 أوم وُصّلت على التوازي. احسب المقاومة المكافئة بالأوم.', num(2, 'أوم'), 'medium', 1, 'للمقاومات المتساوية على التوازي: R = 6 / 3 = 2 أوم.'],
      ['essay', 'اشرح لماذا تُوصَّل الأجهزة المنزلية على التوازي وليس على التوالي.', [{}, { keywords: ['التوازي', 'الجهد', 'مستقل', 'تلف', 'نفس'], modelAnswer: 'تُوصل الأجهزة على التوازي ليحصل كل جهاز على نفس الجهد الكامل للمصدر، وليعمل كل جهاز بشكل مستقل، فإذا حدث تلف لجهاز لا تنقطع الدائرة عن باقي الأجهزة.' }], 'hard', 2, 'التوازي يوفر نفس الجهد لكل جهاز ويجعل الأجهزة مستقلة.', { maxScore: 5 }]
    ],
    121: [
      ['mcq', 'وحدة قياس كثافة الفيض المغناطيسي هي:', mcq(['الويبر', 'التسلا', 'الهنري', 'الأمبير'], 1), 'easy', 0, 'كثافة الفيض تقاس بالتسلا (ويبر/م²).'],
      ['mcq', 'عند مضاعفة شدة التيار في سلك مستقيم فإن كثافة الفيض عند نقطة ثابتة:', mcq(['تتضاعف', 'تقل للنصف', 'لا تتغير', 'تزداد أربع مرات'], 0), 'medium', 2, 'B تتناسب طرديًا مع I.'],
      ['mcq', 'تُستخدم قاعدة قبضة اليد اليمنى لتحديد:', mcq(['مقدار التيار', 'اتجاه المجال حول سلك', 'مقاومة السلك', 'فرق الجهد'], 1), 'easy', 1, 'الإبهام في اتجاه التيار والأصابع تلتف في اتجاه المجال.', { status: 'pending' }],
      ['multi', 'كثافة الفيض حول سلك مستقيم تعتمد على:', multi(['شدة التيار', 'البعد عن السلك', 'نفاذية الوسط', 'لون السلك'], [0, 1, 2]), 'medium', 2, 'B = μI / 2πd.'],
      ['tf', 'تزداد كثافة الفيض المغناطيسي كلما ابتعدنا عن السلك.', [{}, { correct: false }], 'easy', 2, 'B تتناسب عكسيًا مع البعد d.'],
      ['fill', 'تُقاس كثافة الفيض المغناطيسي بوحدة ____.', fill([['تسلا', 'التسلا']]), 'easy', 0, 'الوحدة هي التسلا.'],
      ['short', 'ما اسم القاعدة التي تحدد اتجاه المجال حول سلك مستقيم؟', txt(['قبضة اليد اليمنى', 'قاعدة قبضة اليد اليمنى', 'اليد اليمنى']), 'medium', 1, 'قاعدة قبضة اليد اليمنى لأمبير.']
    ],
    122: [
      ['mcq', 'يتولد تيار مستحث في ملف عندما:', mcq(['يكون الفيض ثابتًا', 'يتغير الفيض الذي يقطعه', 'يُوصل بمقاومة كبيرة', 'يُسخَّن الملف'], 1), 'easy', 0, 'شرط الحث هو تغير الفيض.'],
      ['mcq', 'ملف 100 لفة تغير الفيض خلاله بمعدل 0.02 ويبر/ث، القوة الدافعة المستحثة تساوي:', mcq(['2 فولت', '0.2 فولت', '20 فولت', '5000 فولت'], 0), 'medium', 1, 'emf = N × ΔΦ/Δt = 100 × 0.02 = 2 فولت.'],
      ['mcq', 'الإشارة السالبة في قانون فاراداي تعبر عن:', mcq(['قانون أوم', 'قانون لنز', 'قاعدة اليد اليمنى', 'قانون كولوم'], 1), 'medium', 2, 'قانون لنز: التيار المستحث يقاوم التغير المسبب له.'],
      ['multi', 'لزيادة القوة الدافعة المستحثة في ملف يمكن:', multi(['زيادة عدد اللفات', 'زيادة سرعة حركة المغناطيس', 'تقليل عدد اللفات', 'استخدام مغناطيس أقوى'], [0, 1, 3]), 'medium', 1, 'emf تزداد بزيادة N ومعدل تغير الفيض.'],
      ['tf', 'يعمل التيار المستحث على مقاومة التغير المسبب له.', [{}, { correct: true }], 'easy', 2, 'هذا نص قانون لنز.'],
      ['fill', 'القانون الذي يحدد اتجاه التيار المستحث هو قانون ____.', fill([['لنز']]), 'easy', 2, 'قانون لنز.'],
      ['short', 'ملف عدد لفاته 50 تغير الفيض خلاله بمقدار 0.1 ويبر في 0.5 ثانية. احسب مقدار القوة الدافعة المستحثة بالفولت.', num(10, 'فولت'), 'medium', 1, 'emf = 50 × 0.1 / 0.5 = 10 فولت.']
    ],
    211: [
      ['mcq', 'مشتقة الدالة x³ هي:', mcq(['3x²', 'x²', '3x', 'x³/3'], 0), 'easy', 0, 'مشتقة x^n = n x^(n−1) إذن 3x².'],
      ['mcq', 'مشتقة الدالة الثابتة 5 هي:', mcq(['5', '1', '0', '5x'], 2), 'easy', 0, 'مشتقة أي ثابت صفر.'],
      ['mcq', 'مشتقة x² + 3x هي:', mcq(['2x + 3', 'x + 3', '2x', '2x + 3x'], 0), 'easy', 1, 'مشتقة المجموع = مجموع المشتقات: 2x + 3.', { status: 'pending' }],
      ['multi', 'أي مما يلي صحيح؟', multi(['مشتقة x² هي 2x', 'مشتقة الثابت صفر', 'مشتقة x هي x', 'مشتقة 4x هي 4'], [0, 1, 3]), 'medium', 0, 'مشتقة x هي 1 وليست x.'],
      ['tf', 'مشتقة حاصل ضرب دالتين تساوي حاصل ضرب مشتقتيهما.', [{}, { correct: false }], 'medium', 1, 'الصحيح: (fg)\' = f\'g + fg\'.'],
      ['fill', 'مشتقة x^n تساوي n·x^(____).', fill([['n-1', 'n - 1', 'n−1']]), 'medium', 0, 'نطرح واحدًا من الأس.'],
      ['short', 'إذا كانت f(x) = x² فاحسب f\'(3).', num(6), 'medium', 2, 'f\'(x) = 2x إذن f\'(3) = 6.'],
      ['math_steps', 'أوجد مشتقة f(x) = x²(x + 1) ثم احسب f\'(1). اكتب خطواتك ثم الإجابة النهائية.', [{}, { finalAnswers: ['5'] }], 'hard', 1, 'f = x³ + x² إذن f\' = 3x² + 2x و f\'(1) = 5.', { maxScore: 2 }]
    ],
    212: [
      ['mcq', 'ميل المماس للمنحنى y = x² عند x = 2 يساوي:', mcq(['2', '4', '8', '1'], 1), 'easy', 0, 'y\' = 2x = 4.'],
      ['mcq', 'إذا كانت f\'(x) > 0 في فترة فإن الدالة فيها:', mcq(['متناقصة', 'متزايدة', 'ثابتة', 'غير معرفة'], 1), 'easy', 1, 'المشتقة الموجبة تعني تزايد.'],
      ['mcq', 'للدالة f(x) = x² − 4x نقطة حرجة عند x =', mcq(['4', '2', '−2', '0'], 1), 'medium', 2, 'f\'(x) = 2x − 4 = 0 إذن x = 2.'],
      ['multi', 'أي مما يلي من استخدامات المشتقة؟', multi(['إيجاد ميل المماس', 'تحديد القيم العظمى والصغرى', 'حساب مساحة تحت منحنى', 'تحديد فترات التزايد'], [0, 1, 3]), 'medium', 1, 'حساب المساحة من استخدامات التكامل.', { status: 'pending' }],
      ['tf', 'إذا كانت f\'\'(a) < 0 عند نقطة حرجة فهي نقطة قيمة صغرى محلية.', [{}, { correct: false }], 'medium', 2, 'المشتقة الثانية السالبة تعني قيمة عظمى.'],
      ['fill', 'إذا كانت المشتقة الأولى سالبة في فترة فإن الدالة فيها ____.', fill([['متناقصة', 'تتناقص']]), 'easy', 1, 'المشتقة السالبة تعني تناقص.'],
      ['short', 'أوجد القيمة الصغرى للدالة f(x) = x² − 4x + 7.', num(3), 'hard', 2, 'عند x = 2: f(2) = 4 − 8 + 7 = 3.']
    ],
    221: [
      ['mcq', '∫ 2x dx =', mcq(['x² + c', '2x² + c', 'x + c', '2 + c'], 0), 'easy', 1, 'لأن مشتقة x² هي 2x.'],
      ['mcq', '∫ 5 dx =', mcq(['5 + c', '5x + c', '0', 'x/5 + c'], 1), 'easy', 1, 'تكامل الثابت k هو kx + c.'],
      ['mcq', '∫ x dx =', mcq(['x² + c', 'x²/2 + c', '2x + c', 'x + c'], 1), 'easy', 1, 'x^(1+1)/(1+1) = x²/2.', { status: 'rejected', reason: 'السؤال مكرر تقريبًا مع سؤال ∫ 2x dx؛ يرجى تغيير الدالة (مثلاً x³) لقياس المهارة نفسها بسؤال مختلف.' }],
      ['multi', 'أي مما يلي صحيح؟', multi(['التكامل عكس الاشتقاق', 'نضيف ثابت c للتكامل غير المحدد', 'قاعدة القوى تصلح لكل قيم n', 'مشتقة التكامل تعيد الدالة الأصلية'], [0, 1, 3]), 'medium', 0, 'قاعدة القوى لا تصلح عند n = −1.'],
      ['tf', 'لا نحتاج إلى ثابت التكامل في التكامل غير المحدد.', [{}, { correct: false }], 'easy', 2, 'يجب إضافة c دائمًا.'],
      ['fill', '∫ x² dx = x^____ / 3 + c', fill([['3']]), 'easy', 1, 'x^(2+1)/(2+1) = x³/3.'],
      ['short', 'إذا كانت F(x) = ∫ 3x² dx و F(0) = 2، فاحسب F(1).', num(3), 'medium', 2, 'F(x) = x³ + c و F(0) = 2 إذن c = 2 و F(1) = 3.'],
      ['essay', 'اشرح الفرق بين التكامل المحدد والتكامل غير المحدد مع ذكر مثال.', [{}, { keywords: ['ثابت', 'عدد', 'دالة', 'حدود', 'المحدد'], modelAnswer: 'التكامل غير المحدد ناتجه دالة مضافًا إليها ثابت التكامل c، أما التكامل المحدد فله حدود a و b وناتجه عدد يساوي F(b) − F(a).' }], 'hard', 0, 'غير المحدد: دالة + c. المحدد: عدد بين حدين.', { maxScore: 5 }]
    ],
    222: [
      ['mcq', '∫ من 0 إلى 2 لـ x dx =', mcq(['1', '2', '4', '0'], 1), 'easy', 0, '[x²/2] من 0 إلى 2 = 2.'],
      ['mcq', '∫ من 1 إلى 3 لـ 2 dx =', mcq(['2', '4', '6', '3'], 1), 'easy', 0, '2 × (3 − 1) = 4.'],
      ['mcq', 'ناتج التكامل المحدد يكون:', mcq(['دالة', 'عددًا', 'ثابت تكامل', 'مشتقة'], 1), 'easy', 1, 'التكامل المحدد يعطي قيمة عددية.'],
      ['multi', 'لحساب ∫ من a إلى b لـ f(x) dx نحتاج:', multi(['دالة أصلية F', 'حساب F(b) − F(a)', 'إضافة ثابت c للنتيجة', 'معرفة حدود التكامل'], [0, 1, 3]), 'medium', 1, 'لا نضيف c في التكامل المحدد.'],
      ['tf', 'التكامل من a إلى a لأي دالة يساوي صفرًا.', [{}, { correct: true }], 'easy', 0, 'F(a) − F(a) = 0.'],
      ['fill', '∫ من a إلى b لـ f(x) dx = F(____) − F(a)', fill([['b']]), 'easy', 1, 'النظرية الأساسية: F(b) − F(a).'],
      ['short', 'احسب المساحة تحت المنحنى y = 3x² من x = 0 إلى x = 1.', num(1), 'medium', 2, '[x³] من 0 إلى 1 = 1.']
    ]
  };

  var teacherOf = { 1: 't1', 2: 't2' };
  var questions = [], audit = [], nextId = 1001;
  lessons.forEach(function (l) {
    (Q[l.id] || []).forEach(function (a, i) {
      var ov = a[6] || {};
      var created = now - (14 - i) * D - (l.id % 7) * H;
      var status = ov.status || 'approved';
      var q = {
        id: nextId++, lessonId: l.id, subjectId: l.subjectId, type: a[0], stem: a[1],
        difficulty: a[3], objectiveId: l.objectives[a[4]].id, explanation: a[5],
        body: a[2][0], gradingSpec: a[2][1], maxScore: ov.maxScore || 1, version: 1,
        validationStatus: status,
        validatedBy: status === 'pending' ? null : teacherOf[l.subjectId],
        validatedAt: status === 'pending' ? null : created + (1 + (i % 3)) * D,
        rejectionReason: ov.reason || null, tags: [], createdAt: created, retiredAt: null
      };
      if (status === 'pending') q.createdAt = now - (2 + i % 3) * D;
      questions.push(q);
      audit.push({ id: 'au-s' + q.id + 'c', actorId: 'a1', action: 'create', entity: 'question', entityId: q.id, diff: { version: 1 }, at: q.createdAt });
      if (status !== 'pending') {
        audit.push({ id: 'au-s' + q.id + 'v', actorId: q.validatedBy, action: status === 'approved' ? 'approve' : 'reject', entity: 'question', entityId: q.id, diff: status === 'rejected' ? { reason: q.rejectionReason } : null, at: q.validatedAt });
      }
    });
  });

  function bp(id, name, scope, refId, counts, time, pass) {
    return { id: id, name: name, scope: scope, refId: refId, typeCounts: counts, timeLimitMin: time, passMark: pass };
  }
  var blueprints = [
    bp('bp-u11', 'امتحان الكهربية التيارية', 'unit', 11, { mcq: 4, tf: 2, fill: 2, short: 1 }, 20, 50),
    bp('bp-u12', 'امتحان المغناطيسية', 'unit', 12, { mcq: 4, tf: 2, fill: 2, short: 1 }, 20, 50),
    bp('bp-u21', 'امتحان التفاضل', 'unit', 21, { mcq: 4, tf: 2, fill: 2, short: 1 }, 20, 50),
    bp('bp-u22', 'امتحان التكامل', 'unit', 22, { mcq: 4, tf: 2, fill: 2, short: 1 }, 20, 50),
    bp('bp-s1', 'النموذج الافتراضي - الفيزياء', 'subject', 1, { mcq: 6, multi: 2, tf: 3, fill: 3, short: 2 }, 30, 50),
    bp('bp-s2', 'النموذج الافتراضي - الرياضيات', 'subject', 2, { mcq: 6, multi: 2, tf: 3, fill: 3, short: 2 }, 30, 50)
  ];

  var users = [
    { id: 's1', role: 'student', name: 'أحمد', phone: '01000000001', email: 'ahmed@example.com', status: 'active', joinedAt: now - 40 * D },
    { id: 's2', role: 'student', name: 'سارة', phone: '01000000002', email: 'sara@example.com', status: 'active', joinedAt: now - 3 * D },
    { id: 't1', role: 'teacher', name: 'أ. محمد', subjects: [1], status: 'active', joinedAt: now - 90 * D },
    { id: 't2', role: 'teacher', name: 'أ. هدى', subjects: [2], status: 'active', joinedAt: now - 90 * D },
    { id: 'a1', role: 'admin', name: 'المدير', status: 'active', joinedAt: now - 120 * D }
  ];

  var subscriptions = [
    { id: 'sub1', studentId: 's1', plan: 'base', status: 'active', startedAt: now - 20 * D, periodEnd: now + 10 * D, paymobRef: 'pm_1001' },
    { id: 'sub2', studentId: 's1', plan: 'ask', status: 'active', startedAt: now - 20 * D, periodEnd: now + 10 * D, paymobRef: 'pm_1002' }
  ];
  var payments = [
    { id: 'pay1', studentId: 's1', plan: 'base', amount: 199, currency: 'EGP', status: 'success', paymobTxnId: 'txn_88001', rawWebhook: { hmac: 'verified (simulated)', success: true }, at: now - 20 * D },
    { id: 'pay2', studentId: 's1', plan: 'ask', amount: 99, currency: 'EGP', status: 'success', paymobTxnId: 'txn_88002', rawWebhook: { hmac: 'verified (simulated)', success: true }, at: now - 20 * D + H },
    { id: 'pay3', studentId: 's2', plan: 'base', amount: 199, currency: 'EGP', status: 'failed', paymobTxnId: 'txn_88003', rawWebhook: { hmac: 'verified (simulated)', success: false }, at: now - 2 * D }
  ];

  // --- Synthetic history: Ahmed practised Physics unit 1 over the past days; Sara tried once.
  var sessions = [], attempts = [], seed = 7;
  function rnd() { seed = (seed * 9301 + 49297) % 233280; return seed / 233280; }
  function servableSeed(lid) { return questions.filter(function (q) { return q.lessonId === lid && q.validationStatus === 'approved'; }); }
  function fakeQuiz(sid, lid, dayAgo, pCorrect, n) {
    var qs = servableSeed(lid).slice(0, n), t = now - dayAgo * D - 2 * H, id = 'ses-seed-' + sid + '-' + lid + '-' + dayAgo;
    var results = {}, total = 0, got = 0;
    qs.forEach(function (q, i) {
      var ok = rnd() < pCorrect ? 1 : 0;
      results[q.id] = { answer: null, score: ok * q.maxScore, normalised: ok, feedback: '' };
      total += q.maxScore; got += ok * q.maxScore;
      attempts.push({ id: 'att-' + id + '-' + i, sessionId: id, kind: 'quiz', studentId: sid, questionId: q.id, questionVersion: 1, answer: null, score: ok * q.maxScore, normalised: ok, gradedBy: q.type === 'essay' ? 'ai' : 'auto', timeMs: 20000 + Math.round(rnd() * 40000), at: t + i * 60000 });
    });
    sessions.push({ id: id, kind: 'quiz', studentId: sid, subjectId: 1, lessonId: lid, questionIds: qs.map(function (q) { return q.id; }), idx: qs.length, results: results, startedAt: t, submittedAt: t + qs.length * 60000, scorePct: Math.round(got / total * 100) });
  }
  fakeQuiz('s1', 111, 5, 0.6, 7); fakeQuiz('s1', 112, 4, 0.7, 7); fakeQuiz('s1', 111, 3, 0.85, 7);
  fakeQuiz('s1', 112, 2, 0.8, 7); fakeQuiz('s1', 111, 1, 0.9, 7); fakeQuiz('s1', 121, 0, 0.7, 5);
  fakeQuiz('s2', 111, 1, 0.5, 5);

  // One past unit exam for Ahmed (unit 11)
  (function () {
    var qs = questions.filter(function (q) { return (q.lessonId === 111 || q.lessonId === 112) && q.validationStatus === 'approved' && ['mcq', 'tf', 'fill', 'short'].indexOf(q.type) >= 0; }).slice(0, 9);
    var id = 'ses-seed-exam-1', t = now - 2 * D, results = {}, answers = {}, got = 0, tot = 0;
    qs.forEach(function (q, i) {
      var ok = i % 3 === 2 ? 0 : 1; results[q.id] = { answer: null, score: ok, normalised: ok, feedback: '' }; answers[q.id] = null;
      got += ok; tot += 1;
      attempts.push({ id: 'att-' + id + '-' + i, sessionId: id, kind: 'unit', studentId: 's1', questionId: q.id, questionVersion: 1, answer: null, score: ok, normalised: ok, gradedBy: 'auto', timeMs: 45000, at: t + i * 60000 });
    });
    sessions.push({ id: id, kind: 'unit', title: 'امتحان الكهربية التيارية', studentId: 's1', subjectId: 1, unitIds: [11], questionIds: qs.map(function (q) { return q.id; }), answers: answers, results: results, startedAt: t, submittedAt: t + 15 * 60000, timeLimitMin: 20, passMark: 50, scorePct: Math.round(got / tot * 100) });
  })();

  var qParallel = questions.filter(function (q) { return q.lessonId === 112 && q.type === 'tf'; })[0];
  var qMath = questions.filter(function (q) { return q.lessonId === 212 && q.type === 'tf'; })[0];
  var threads = [
    { id: 'th1', studentId: 's1', teacherId: null, subjectId: 1, context: { unitId: 11, lessonId: 112, questionId: qParallel.id }, status: 'open', followupUsed: false,
      submittedAt: now - 30 * H, slaDueAt: now - 6 * H, closedAt: null, rating: null,
      messages: [{ id: 'm1', senderId: 's1', kind: 'text', text: 'لماذا تكون المقاومة المكافئة على التوازي أصغر من أصغر مقاومة؟ لم أفهم الفكرة.', at: now - 30 * H }] },
    { id: 'th2', studentId: 's1', teacherId: 't2', subjectId: 2, context: { unitId: 21, lessonId: 212, questionId: qMath.id }, status: 'answered', followupUsed: false,
      submittedAt: now - 50 * H, slaDueAt: now - 26 * H, closedAt: null, rating: null,
      messages: [
        { id: 'm2', senderId: 's1', kind: 'text', text: 'كيف أعرف أن النقطة الحرجة عظمى أم صغرى؟', at: now - 50 * H },
        { id: 'm3', senderId: 't2', kind: 'voice', text: 'بص يا أحمد، بعد ما تلاقي النقطة الحرجة عوّض في المشتقة الثانية: لو الناتج سالب تبقى قيمة عظمى، ولو موجب تبقى قيمة صغرى. جرّب على الدالة x² − 4x.', audioSec: 42, at: now - 40 * H }
      ] }
  ];

  var avatarConvs = [
    { id: 'av1', studentId: 's1', context: { lessonId: 111 }, model: 'avatar-sim-0', promptVersion: 'p-v1', startedAt: now - 3 * D,
      messages: [
        { role: 'user', text: 'اشرح لي قانون أوم ببساطة', at: now - 3 * D },
        { role: 'assistant', text: 'خطوة 1: قانون أوم يربط الجهد بالتيار والمقاومة: V = I × R.\nخطوة 2: لو زاد الجهد مع ثبات المقاومة يزيد التيار بنفس النسبة.\n(المصدر: الشرح - التيار الكهربي وقانون أوم)', at: now - 3 * D + 2000 }
      ] }
  ];

  audit.sort(function (a, b) { return a.at - b.at; });

  window.SEED = {
    schema: 1,
    subjects: subjects, units: units, lessons: lessons, questions: questions, revisions: [],
    blueprints: blueprints, users: users, subscriptions: subscriptions, payments: payments,
    sessions: sessions, attempts: attempts, threads: threads, avatarConvs: avatarConvs, audit: audit,
    ui: { role: 'student', users: { student: 's1', teacher: 't1', admin: 'a1' }, avatar: { open: false, convId: null } }
  };
})();

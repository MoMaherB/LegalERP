-- Clear previous dummy seed if needed, or safely insert non-duplicate records
DO $$
DECLARE
    i INT;
    case_types TEXT[] := ARRAY['Civil', 'Administrative', 'Criminal', 'PersonalStatus'];
    case_statuses TEXT[] := ARRAY['Active', 'Closed'];
    courts TEXT[] := ARRAY[
        'محكمة جنوب القاهرة الابتدائية - الدائرة 3 تجاري',
        'محكمة شمال الجيزة الابتدائية - الدائرة 7 تعويضات',
        'محكمة استئناف القاهرة - الدائرة 12 مدني',
        'مجلس الدولة - محكمة القضاء الإداري دائرة العقود',
        'محكمة القاهرة الاقتصادية - الدائرة الاستئنافية الأولى'
    ];
    judges TEXT[] := ARRAY[
        'المستشار د. أحمد عبد الرحمن',
        'المستشار طارق سامي النجار',
        'المستشار محمود عزت الدسوقي',
        'المستشار شريف فوزي القاضي',
        'المستشار عماد عبد الوهاب'
    ];
    case_topics TEXT[] := ARRAY[
        'دعوى تعويض عن إخلال تعاقدي ومطالبة بالشرط الجزائي',
        'دعوى صحة ونفاذ عقد بيع عقار ونقل ملكية تجارية',
        'دعوى مطالبة بمستحقات وتصفية حسابات شريك تجاري',
        'طعن بالاستئناف على حكم إلغاء قرار إداري تنفيذي',
        'دعوى فسخ عقد إيجار تجاري وإخلاء وتسليم العين',
        'دعوى براءة ذمة من مديونية بنكية وشطب قيد الرهن',
        'دعوى منازعة تنفيذ موضوعية ووقف إجراءات البيع الجبري',
        'دعوى محاسبة حارس قضائي وبطلان محاضر الجرد السنوية',
        'دعوى تثبيت ملكية أراضي فضاء وبطلان حجج الشراء العرفية',
        'دعوى إلزام بتعويض مادي وأدبي عن إساءة استخدام حق التقاضي'
    ];

    comp_categories TEXT[] := ARRAY['CapitalCompany', 'Partnership', 'SoleProprietorship'];
    comp_entities TEXT[] := ARRAY['شركة مساهمة مصرية', 'شركة ذات مسؤولية محدودة', 'شركة تضامن', 'شركة توصية بسيطة', 'منشأة فردية'];
    comp_prefixes TEXT[] := ARRAY['شركة', 'مجموعة', 'مؤسسة'];
    comp_names TEXT[] := ARRAY[
        'الأهرام للتجارة والمقاولات العامة',
        'النيل للاستثمار العقاري وإدارة المشروعات',
        'الدلتا للصناعات الهندسية والتوريدات البحرية',
        'الشرق الأوسط للخدمات اللوجستية وسلاسل الإمداد',
        'الأمل للحلول البرمجية وتكنولوجيا الذكاء الاصطناعي',
        'الفرسان الدولية للملاحة والتخليص الجمركي',
        'طيبة للصناعات الدوائية والمستلزمات الطبية',
        'الفيروز للتطوير الزراعي واستصلاح الأراضي',
        'رواد المستقبل للمقاولات الكهروميكانيكية',
        'سندباد للتوريدات الفندقية والقرى السياحية',
        'العاصمة للمراكز التجارية والتسويق الرقمي',
        'البركة لتصنيع وتعبئة المواد الغذائية والتصدير'
    ];
    comp_addresses TEXT[] := ARRAY[
        'شارع مصدق، حي الدقي، محافظة الجيزة',
        'شارع التسعين الشمالي، التجمع الخامس، القاهرة الجديدة',
        'طريق النصر تقاطع مكرم عبيد، مدينة نصر، القاهرة',
        'المحور المركزي بجوار جامعة أكتوبر، 6 أكتوبر',
        'طريق الكورنيش، منطقة لوران، الإسكندرية',
        'المنطقة الصناعية الثالثة، مدينة العاشر من رمضان'
    ];

    first_names TEXT[] := ARRAY['أحمد', 'محمد', 'محمود', 'طارق', 'عمر', 'يوسف', 'سارة', 'منى', 'كريم', 'حازم', 'شريف', 'إبراهيم', 'مصطفى', 'ياسر', 'خالد'];
    last_names TEXT[] := ARRAY['الشناوي', 'السعدني', 'المنشاوي', 'الدسوقي', 'القاضي', 'شريف', 'الباز', 'هلال', 'عكاشة', 'فهمي', 'سليمان', 'النمر', 'عطية', 'بدوي'];
    cities TEXT[] := ARRAY['القاهرة', 'الجيزة', 'الإسكندرية', 'المنصورة', 'طنطا', 'أسيوط', 'بورسعيد'];

BEGIN
    -- 1. Insert 65 Cases
    FOR i IN 1..65 LOOP
        INSERT INTO cases (
            "Id", "CaseNumber", "Title", "CaseType", "Status", "FilingDate", 
            "CourtName", "JudgeName", "Notes", "AgreedFee", "Outcome", 
            "IsDeleted", "CreatedAt", "UpdatedAt"
        )
        VALUES (
            gen_random_uuid(),
            'CASE-2026-' || LPAD(i::TEXT, 4, '0'),
            case_topics[1 + ((i - 1) % array_length(case_topics, 1))] || ' (ملف ' || i || ')',
            case_types[1 + ((i - 1) % array_length(case_types, 1))],
            case_statuses[1 + ((i - 1) % array_length(case_statuses, 1))],
            (DATE '2026-01-01' + (i * 4 || ' days')::INTERVAL)::DATE,
            courts[1 + ((i - 1) % array_length(courts, 1))],
            judges[1 + ((i - 1) % array_length(judges, 1))],
            'قضية تم إدراجها ضمن بيانات الاختبار للتحقق من أداء واستجابة التصفح والترقيم الصفحي.',
            15000 + (i * 1250),
            CASE WHEN i % 3 = 0 THEN 'Won' WHEN i % 3 = 1 THEN 'Settled' ELSE NULL END,
            false,
            NOW() - (i || ' hours')::INTERVAL,
            NOW()
        );
    END LOOP;

    -- 2. Insert 65 Companies
    FOR i IN 1..65 LOOP
        INSERT INTO companies (
            "Id", "FileNumber", "Category", "CompanyName", "CompanyNameEn", "TradeName",
            "EstablishmentDate", "RegistrationNumber", "CommercialRegisterNumber", "TaxNumber",
            "LegalEntity", "Address", "Notes", "AgreedFee", "HasManyPartners",
            "IsDeleted", "CreatedAt", "UpdatedAt"
        )
        VALUES (
            gen_random_uuid(),
            'COMP-' || LPAD(i::TEXT, 4, '0'),
            comp_categories[1 + ((i - 1) % array_length(comp_categories, 1))],
            comp_prefixes[1 + ((i - 1) % array_length(comp_prefixes, 1))] || ' ' || 
                comp_names[1 + ((i - 1) % array_length(comp_names, 1))] || ' فرع ' || i,
            'Company Group ' || i || ' LLC',
            'علامة تجارية مسجلة ' || i,
            (DATE '2015-01-01' + (i * 45 || ' days')::INTERVAL)::DATE,
            'CR-' || (100000 + i),
            'REG-' || (300000 + i),
            'TAX-' || (200000 + i),
            comp_entities[1 + ((i - 1) % array_length(comp_entities, 1))],
            comp_addresses[1 + ((i - 1) % array_length(comp_addresses, 1))],
            'بيانات شركة نموذجية لفحص التقسيم والبحث على السيرفر.',
            25000 + (i * 1500),
            false,
            false,
            NOW() - (i || ' hours')::INTERVAL,
            NOW()
        );
    END LOOP;

    -- 3. Insert 65 Clients
    FOR i IN 1..65 LOOP
        INSERT INTO clients (
            "Id", "FileNumber", "FullName", "FullNameEn", "NationalIdNumber",
            "PhoneNumber", "Email", "Address", "Notes",
            "IsDeleted", "CreatedAt", "UpdatedAt"
        )
        VALUES (
            gen_random_uuid(),
            'CLI-' || LPAD(i::TEXT, 4, '0'),
            first_names[1 + ((i - 1) % array_length(first_names, 1))] || ' ' ||
                first_names[1 + (i % array_length(first_names, 1))] || ' ' ||
                last_names[1 + ((i - 1) % array_length(last_names, 1))],
            'Client Name ' || i,
            '29' || LPAD((i * 12345)::TEXT, 12, '0'),
            '010' || LPAD((10000000 + i)::TEXT, 8, '0'),
            'client.' || i || '@test-legalerp.com',
            cities[1 + ((i - 1) % array_length(cities, 1))] || ' - شارع ' || (i * 3),
            'عميل مسجل لاختبار الترقيم والبحث والفلترة الفورية.',
            false,
            NOW() - (i || ' hours')::INTERVAL,
            NOW()
        );
    END LOOP;

END $$;

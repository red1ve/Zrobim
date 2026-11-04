-- Run on a development database after applying the EF Core migrations.
-- The demo master is visible in the catalog and has no usable login password.
BEGIN;

INSERT INTO categories (name)
VALUES ('Клининг (демо)')
ON CONFLICT (name) DO NOTHING;

INSERT INTO users (id, email, password_hash, role, first_name, last_name)
VALUES ('c8140b79-0b31-4888-a849-57b68d6f8a31', 'catalog-master@zrobim.example',
        'Ag==', 'MASTER', 'Анна', 'Демо')
ON CONFLICT DO NOTHING;

INSERT INTO masters (user_id, description, city, verification_status)
SELECT id, 'Демонстрационный профиль для проверки каталога.', 'Минск', 'APPROVED'
FROM users
WHERE id = 'c8140b79-0b31-4888-a849-57b68d6f8a31' AND email = 'catalog-master@zrobim.example'
ON CONFLICT (user_id) DO NOTHING;

INSERT INTO services (id, master_id, category_id, name, description, price, duration_minutes, location_type)
SELECT demo.id, m.id, c.id, demo.name, demo.description, demo.price, demo.duration_minutes, 'CLIENT_PLACE'
FROM (VALUES
    ('b6242cef-109c-4f2a-b865-69ef262384a2'::uuid, 'Поддерживающая уборка',
     'Уборка комнат, кухни и санузла.', 45.00, 60),
    ('f6e4f9e1-55d8-47b9-9082-206e665856c8'::uuid, 'Генеральная уборка',
     'Уборка с чисткой труднодоступных поверхностей.', 90.00, 120)
) AS demo(id, name, description, price, duration_minutes)
CROSS JOIN masters m
CROSS JOIN categories c
WHERE m.user_id = 'c8140b79-0b31-4888-a849-57b68d6f8a31'
  AND c.name = 'Клининг (демо)'
ON CONFLICT (id) DO NOTHING;

INSERT INTO master_working_hours (master_id, day_of_week, start_time, end_time)
SELECT m.id, day, '09:00', '18:00'
FROM masters m
CROSS JOIN generate_series(1, 6) AS day
WHERE m.user_id = 'c8140b79-0b31-4888-a849-57b68d6f8a31'
ON CONFLICT (master_id, day_of_week) DO NOTHING;

COMMIT;

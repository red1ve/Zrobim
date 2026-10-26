-- =============================================================================
-- Зробим — схема БД: безопасная сделка + подписки мастеров
-- V001 (база из BD.pdf, приведённая к модели безопасной сделки)
--
-- Применять целиком в одной транзакции:
--   psql -1 -v ON_ERROR_STOP=1 -h localhost -U zrobim -d zrobim_dev -f V001__safe_deal_schema.sql
--
-- Два денежных потока, которые никогда не смешиваются:
--   №1  MASTER → ЗРОБИМ             subscription_payments → master_subscriptions
--   №2  CLIENT → ЗРОБИМ → MASTER    bookings → payments (HELD) → payouts
--
-- Сделка: заявка → мастер принял → клиент платит 100% → услуга → окно спора → мастеру.
-- 30% цены — залог: при вине клиента (поздняя отмена, неявка) он уходит мастеру,
-- остаток возвращается клиенту; мастер может от залога отказаться.
-- =============================================================================

-- btree_gist нужен для exclusion constraint по (uuid =, tstzrange &&)
CREATE EXTENSION IF NOT EXISTS btree_gist;

-- -----------------------------------------------------------------------------
-- ENUMS
-- -----------------------------------------------------------------------------
CREATE TYPE user_role AS ENUM ('CLIENT', 'MASTER', 'ADMIN');
CREATE TYPE verification_status AS ENUM ('PENDING', 'APPROVED', 'REJECTED');
CREATE TYPE location_type AS ENUM ('MASTER_PLACE', 'CLIENT_PLACE');

CREATE TYPE booking_status AS ENUM (
    'PENDING',           -- заявка отправлена, ждём ответа мастера; время держится за клиентом
    'AWAITING_PAYMENT',  -- мастер принял заявку, ждём оплату 100%
    'CONFIRMED',         -- оплачено (payment HELD) — запись полностью подтверждена, открыт чат
    'COMPLETED',         -- мастер отметил выполнение → окно спора
    'CLIENT_NO_SHOW',    -- мастер отметил неявку клиента → окно спора, залог мастеру
    'CANCELLED',
    'DISPUTED'           -- клиент открыл спор
);

CREATE TYPE payment_status AS ENUM (
    'PENDING',             -- «корзина»: клиент нажал «Оплатить», провайдер ещё не подтвердил
    'HELD',                -- деньги получены платформой и удерживаются
    'RELEASED',            -- вся сумма мастеру
    'REFUNDED',            -- вся сумма клиенту
    'PARTIALLY_REFUNDED',  -- залог мастеру, остаток клиенту
    'FAILED'               -- не оплачено (отказ провайдера или истекла корзина)
);
CREATE TYPE payout_status AS ENUM ('PENDING', 'PROCESSING', 'COMPLETED', 'FAILED');

-- судьба залога при вине клиента (поздняя отмена / неявка)
CREATE TYPE deposit_outcome AS ENUM (
    'KEPT_BY_MASTER',      -- мастер решил оставить залог
    'WAIVED_BY_MASTER',    -- мастер вошёл в положение и вернул залог
    'KEPT_AUTOMATICALLY'   -- мастер не ответил за 24 ч — залог ему по умолчанию
);

CREATE TYPE dispute_reason AS ENUM ('SERVICE_NOT_PROVIDED', 'MASTER_NO_SHOW', 'POOR_QUALITY', 'OTHER');
CREATE TYPE dispute_status AS ENUM ('OPEN', 'RESOLVED');
CREATE TYPE dispute_resolution AS ENUM (
    'RELEASE_TO_MASTER',   -- всё мастеру
    'REFUND_TO_CLIENT',    -- всё клиенту
    'DEPOSIT_TO_MASTER'    -- залог мастеру, остаток клиенту
);

CREATE TYPE subscription_plan_code AS ENUM ('BASIC', 'PLUS');
CREATE TYPE subscription_payment_status AS ENUM ('PENDING', 'PAID', 'FAILED', 'REFUNDED');

-- -----------------------------------------------------------------------------
-- USERS / MASTERS
-- -----------------------------------------------------------------------------
CREATE TABLE users (
    id            uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    email         varchar(255) NOT NULL,
    phone         varchar(30),
    password_hash varchar(255) NOT NULL,
    role          user_role    NOT NULL,
    first_name    varchar(100) NOT NULL,
    last_name     varchar(100),
    avatar_url    varchar(500),
    is_active     boolean      NOT NULL DEFAULT true,
    created_at    timestamptz  NOT NULL DEFAULT now(),
    updated_at    timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT users_phone_key UNIQUE (phone)
);
-- email уникален без учёта регистра: Ivan@Mail.by и ivan@mail.by — один аккаунт
CREATE UNIQUE INDEX users_email_lower_key ON users (lower(email));

CREATE TABLE masters (
    id                  uuid                PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             uuid                NOT NULL REFERENCES users (id),
    description         text,
    city                varchar(100),
    address             text,
    latitude            decimal(10,7),
    longitude           decimal(10,7),
    verification_status verification_status NOT NULL DEFAULT 'PENDING',
    rating              decimal(3,2)        NOT NULL DEFAULT 0,
    reviews_count       int                 NOT NULL DEFAULT 0,
    -- IBAN или токен платёжного провайдера. Номер карты здесь хранить нельзя.
    payout_account      varchar(255),
    created_at          timestamptz         NOT NULL DEFAULT now(),
    updated_at          timestamptz         NOT NULL DEFAULT now(),

    CONSTRAINT masters_user_id_key            UNIQUE (user_id),
    CONSTRAINT masters_rating_range           CHECK (rating BETWEEN 0 AND 5),
    CONSTRAINT masters_reviews_count_nonneg   CHECK (reviews_count >= 0),
    CONSTRAINT masters_coordinates_pair       CHECK ((latitude IS NULL) = (longitude IS NULL)),
    CONSTRAINT masters_latitude_range         CHECK (latitude BETWEEN -90 AND 90),
    CONSTRAINT masters_longitude_range        CHECK (longitude BETWEEN -180 AND 180),
    CONSTRAINT masters_payout_account_not_blank CHECK (payout_account IS NULL OR btrim(payout_account) <> ''),
    -- страховка от случайного сохранения сырого номера карты (12+ цифр подряд)
    CONSTRAINT masters_payout_account_not_card_number CHECK (payout_account IS NULL OR payout_account !~ '^[0-9 -]{12,}$')
);
CREATE INDEX masters_verification_status_idx ON masters (verification_status);
CREATE INDEX masters_city_idx ON masters (city);

-- -----------------------------------------------------------------------------
-- CATALOG
-- -----------------------------------------------------------------------------
CREATE TABLE categories (
    id         uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    name       varchar(100) NOT NULL,
    image_url  varchar(500),
    is_active  boolean      NOT NULL DEFAULT true,
    created_at timestamptz  NOT NULL DEFAULT now(),
    updated_at timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT categories_name_key UNIQUE (name)
);

CREATE TABLE services (
    id               uuid          PRIMARY KEY DEFAULT gen_random_uuid(),
    master_id        uuid          NOT NULL REFERENCES masters (id),
    category_id      uuid          NOT NULL REFERENCES categories (id),
    name             varchar(150)  NOT NULL,
    description      text,
    price            decimal(12,2) NOT NULL,
    duration_minutes int           NOT NULL,
    location_type    location_type NOT NULL,
    is_active        boolean       NOT NULL DEFAULT true,
    created_at       timestamptz   NOT NULL DEFAULT now(),
    updated_at       timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT services_price_positive  CHECK (price > 0),
    CONSTRAINT services_duration_range  CHECK (duration_minutes > 0 AND duration_minutes <= 1440),
    -- цель для составного FK из bookings: услуга обязана принадлежать мастеру записи
    CONSTRAINT services_id_master_key   UNIQUE (id, master_id)
);
CREATE INDEX services_master_id_idx ON services (master_id);
CREATE INDEX services_category_active_idx ON services (category_id) WHERE is_active;

-- -----------------------------------------------------------------------------
-- SCHEDULE
-- -----------------------------------------------------------------------------
CREATE TABLE master_working_hours (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    master_id   uuid NOT NULL REFERENCES masters (id),
    day_of_week int  NOT NULL,  -- 1 = понедельник … 7 = воскресенье; нет строки = выходной
    start_time  time NOT NULL,
    end_time    time NOT NULL,

    CONSTRAINT master_working_hours_master_day_key UNIQUE (master_id, day_of_week),
    CONSTRAINT master_working_hours_day_range      CHECK (day_of_week BETWEEN 1 AND 7),
    CONSTRAINT master_working_hours_time_order     CHECK (end_time > start_time)
);

CREATE TABLE master_time_blocks (
    id        uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    master_id uuid         NOT NULL REFERENCES masters (id),
    start_at  timestamptz  NOT NULL,
    end_at    timestamptz  NOT NULL,
    reason    varchar(255),

    CONSTRAINT master_time_blocks_time_order CHECK (end_at > start_at)
);
CREATE INDEX master_time_blocks_master_start_idx ON master_time_blocks (master_id, start_at);

-- -----------------------------------------------------------------------------
-- BOOKINGS — состояние услуги (не денег)
-- -----------------------------------------------------------------------------
CREATE TABLE bookings (
    id                  uuid           PRIMARY KEY DEFAULT gen_random_uuid(),
    client_id           uuid           NOT NULL REFERENCES users (id),
    master_id           uuid           NOT NULL REFERENCES masters (id),
    service_id          uuid           NOT NULL,
    start_at            timestamptz    NOT NULL,
    end_at              timestamptz    NOT NULL,            -- сервер: start_at + duration
    price               decimal(12,2)  NOT NULL,            -- snapshot services.price
    deposit_amount      decimal(12,2)  NOT NULL,            -- залог: snapshot 30% цены на момент заявки
    location_type       location_type  NOT NULL,
    address             text,
    status              booking_status NOT NULL DEFAULT 'PENDING',
    client_comment      text,
    response_deadline_at timestamptz   NOT NULL,            -- до когда мастер должен ответить (12 ч, но ≤ start − 2 ч)
    accepted_at         timestamptz,                        -- мастер принял заявку
    payment_deadline_at timestamptz,                        -- до когда бронь держится за клиентом без оплаты
    deposit_outcome     deposit_outcome,                    -- только при вине клиента
    cancelled_by        uuid           REFERENCES users (id), -- NULL при отмене системой (таймаут)
    cancellation_reason text,
    cancelled_at        timestamptz,
    completed_at        timestamptz,
    created_at          timestamptz    NOT NULL DEFAULT now(),
    updated_at          timestamptz    NOT NULL DEFAULT now(),

    -- услуга существует и принадлежит именно этому мастеру
    CONSTRAINT bookings_service_master_fk
        FOREIGN KEY (service_id, master_id) REFERENCES services (id, master_id),
    -- цель для составного FK из reviews
    CONSTRAINT bookings_id_client_master_key UNIQUE (id, client_id, master_id),

    CONSTRAINT bookings_time_order       CHECK (end_at > start_at),
    CONSTRAINT bookings_price_positive   CHECK (price > 0),
    CONSTRAINT bookings_deposit_range    CHECK (deposit_amount > 0 AND deposit_amount <= price),
    CONSTRAINT bookings_response_deadline_before_start CHECK (response_deadline_at <= start_at),
    -- после «мастер принял» всегда есть срок оплаты; до этого — нет
    CONSTRAINT bookings_payment_deadline_iff_accepted
        CHECK ((accepted_at IS NULL) = (payment_deadline_at IS NULL)),
    CONSTRAINT bookings_accepted_for_later_statuses
        CHECK (status IN ('PENDING', 'CANCELLED') OR accepted_at IS NOT NULL),
    CONSTRAINT bookings_deposit_outcome_only_on_client_fault
        CHECK (deposit_outcome IS NULL OR status IN ('CANCELLED', 'CLIENT_NO_SHOW', 'DISPUTED')),
    CONSTRAINT bookings_client_place_needs_address
        CHECK (location_type <> 'CLIENT_PLACE' OR (address IS NOT NULL AND btrim(address) <> '')),
    CONSTRAINT bookings_cancelled_at_iff_cancelled
        CHECK ((status = 'CANCELLED') = (cancelled_at IS NOT NULL)),
    CONSTRAINT bookings_cancelled_by_only_when_cancelled
        CHECK (cancelled_by IS NULL OR status = 'CANCELLED'),
    CONSTRAINT bookings_completed_has_timestamp
        CHECK (status <> 'COMPLETED' OR completed_at IS NOT NULL),

    -- защита от двойной записи: у мастера не может быть двух активных броней,
    -- пересекающихся по времени. Конфликт → SQLSTATE 23P01 → backend отвечает 409.
    CONSTRAINT bookings_no_overlap EXCLUDE USING gist (
        master_id WITH =,
        tstzrange(start_at, end_at) WITH &&
    ) WHERE (status IN ('PENDING', 'AWAITING_PAYMENT', 'CONFIRMED'))
);
CREATE INDEX bookings_client_start_idx ON bookings (client_id, start_at DESC);
CREATE INDEX bookings_master_start_idx ON bookings (master_id, start_at);
-- для фоновой задачи: мастер не ответил / клиент не оплатил / никто не отметил результат
CREATE INDEX bookings_pending_response_idx ON bookings (response_deadline_at) WHERE status = 'PENDING';
CREATE INDEX bookings_awaiting_payment_idx ON bookings (payment_deadline_at) WHERE status = 'AWAITING_PAYMENT';
CREATE INDEX bookings_confirmed_end_idx    ON bookings (end_at)              WHERE status = 'CONFIRMED';

-- -----------------------------------------------------------------------------
-- PAYMENTS — поток №2, деньги клиента (CLIENT → ЗРОБИМ)
-- payments.id передаётся провайдеру как order id / idempotency key.
-- client_id и master_id НЕ хранятся: payment → booking → client / master.
-- -----------------------------------------------------------------------------
CREATE TABLE payments (
    id                  uuid           PRIMARY KEY DEFAULT gen_random_uuid(),
    booking_id          uuid           NOT NULL REFERENCES bookings (id),
    amount              decimal(12,2)  NOT NULL,            -- snapshot bookings.price, никогда из запроса
    currency            char(3)        NOT NULL DEFAULT 'BYN',
    status              payment_status NOT NULL DEFAULT 'PENDING',
    provider            varchar(50)    NOT NULL,
    external_payment_id varchar(255),
    expires_at          timestamptz    NOT NULL,            -- «корзина»: 20 минут с момента нажатия «Оплатить»
    -- таймер распределения удержанных денег: после COMPLETED — всё мастеру,
    -- после поздней отмены / неявки клиента — залог мастеру, остаток клиенту.
    -- NULL = не запланирован (идёт спор или деньги ждут полного возврата).
    release_after       timestamptz,
    released_amount     decimal(12,2)  NOT NULL DEFAULT 0,  -- сколько ушло мастеру
    refunded_amount     decimal(12,2)  NOT NULL DEFAULT 0,  -- сколько вернулось клиенту
    failure_reason      text,
    paid_at             timestamptz,                        -- деньги получены и удерживаются
    released_at         timestamptz,
    refunded_at         timestamptz,
    created_at          timestamptz    NOT NULL DEFAULT now(),
    updated_at          timestamptz    NOT NULL DEFAULT now(),

    CONSTRAINT payments_provider_external_id_key UNIQUE (provider, external_payment_id),
    CONSTRAINT payments_amount_positive  CHECK (amount > 0),
    CONSTRAINT payments_currency_format  CHECK (currency ~ '^[A-Z]{3}$'),
    CONSTRAINT payments_expires_after_created CHECK (expires_at > created_at),
    -- деньги были получены ⇔ HELD / RELEASED / REFUNDED / PARTIALLY_REFUNDED
    CONSTRAINT payments_paid_at_iff_received
        CHECK ((status IN ('HELD', 'RELEASED', 'REFUNDED', 'PARTIALLY_REFUNDED')) = (paid_at IS NOT NULL)),
    CONSTRAINT payments_released_at_iff_released
        CHECK ((status IN ('RELEASED', 'PARTIALLY_REFUNDED')) = (released_at IS NOT NULL)),
    CONSTRAINT payments_refunded_at_iff_refunded
        CHECK ((status IN ('REFUNDED', 'PARTIALLY_REFUNDED')) = (refunded_at IS NOT NULL)),
    -- суммы распределения согласованы со статусом и в сумме дают amount
    CONSTRAINT payments_split_consistency CHECK (
        (status IN ('PENDING', 'HELD', 'FAILED') AND released_amount = 0      AND refunded_amount = 0)
     OR (status = 'RELEASED'                     AND released_amount = amount AND refunded_amount = 0)
     OR (status = 'REFUNDED'                     AND released_amount = 0      AND refunded_amount = amount)
     OR (status = 'PARTIALLY_REFUNDED'           AND released_amount > 0      AND refunded_amount > 0
                                                 AND released_amount + refunded_amount = amount)
    ),
    CONSTRAINT payments_release_after_only_when_received
        CHECK (release_after IS NULL OR status IN ('HELD', 'RELEASED', 'PARTIALLY_REFUNDED'))
);
CREATE INDEX payments_booking_id_idx ON payments (booking_id);
-- несколько попыток оплаты после FAILED, но не более одного «живого» платежа на запись
CREATE UNIQUE INDEX payments_one_alive_per_booking ON payments (booking_id) WHERE status <> 'FAILED';
-- для фоновой задачи: распределение денег по таймеру
CREATE INDEX payments_auto_release_idx ON payments (release_after)
    WHERE status = 'HELD' AND release_after IS NOT NULL;
-- для фоновой задачи: истёкшие «корзины»
CREATE INDEX payments_pending_expires_idx ON payments (expires_at) WHERE status = 'PENDING';

-- -----------------------------------------------------------------------------
-- DISPUTES — спор по записи; пока OPEN, деньги остаются HELD
-- -----------------------------------------------------------------------------
CREATE TABLE disputes (
    id                 uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    booking_id         uuid               NOT NULL REFERENCES bookings (id),
    opened_by          uuid               NOT NULL REFERENCES users (id),
    reason             dispute_reason     NOT NULL,
    description        text,
    status             dispute_status     NOT NULL DEFAULT 'OPEN',
    resolution         dispute_resolution,
    resolved_by        uuid               REFERENCES users (id),
    resolution_comment text,
    created_at         timestamptz        NOT NULL DEFAULT now(),
    resolved_at        timestamptz,

    CONSTRAINT disputes_booking_id_key UNIQUE (booking_id),
    CONSTRAINT disputes_resolution_consistency CHECK (
        (status = 'OPEN'     AND resolution IS NULL     AND resolved_by IS NULL     AND resolved_at IS NULL)
     OR (status = 'RESOLVED' AND resolution IS NOT NULL AND resolved_by IS NOT NULL AND resolved_at IS NOT NULL)
    )
);
CREATE INDEX disputes_open_created_idx ON disputes (created_at) WHERE status = 'OPEN';

-- -----------------------------------------------------------------------------
-- PAYOUTS — поток №2, передача удержанных денег мастеру (ЗРОБИМ → MASTER)
-- Создаётся только в транзакции HELD → RELEASED. master_id НЕ хранится:
-- payout → payment → booking → master.
-- -----------------------------------------------------------------------------
CREATE TABLE payouts (
    id                 uuid          PRIMARY KEY DEFAULT gen_random_uuid(),
    payment_id         uuid          NOT NULL REFERENCES payments (id),
    amount             decimal(12,2) NOT NULL,
    currency           char(3)       NOT NULL DEFAULT 'BYN',
    status             payout_status NOT NULL DEFAULT 'PENDING',
    provider           varchar(50)   NOT NULL,
    external_payout_id varchar(255),
    destination        varchar(255)  NOT NULL,              -- snapshot masters.payout_account
    failure_reason     text,
    completed_at       timestamptz,
    created_at         timestamptz   NOT NULL DEFAULT now(),
    updated_at         timestamptz   NOT NULL DEFAULT now(),

    -- одна выплата на один платёж: двойная выплата невозможна
    CONSTRAINT payouts_payment_id_key            UNIQUE (payment_id),
    CONSTRAINT payouts_provider_external_id_key  UNIQUE (provider, external_payout_id),
    CONSTRAINT payouts_amount_positive           CHECK (amount > 0),
    CONSTRAINT payouts_currency_format           CHECK (currency ~ '^[A-Z]{3}$'),
    CONSTRAINT payouts_completed_at_iff_completed CHECK ((status = 'COMPLETED') = (completed_at IS NOT NULL)),
    CONSTRAINT payouts_destination_not_blank     CHECK (btrim(destination) <> ''),
    CONSTRAINT payouts_destination_not_card_number CHECK (destination !~ '^[0-9 -]{12,}$')
);
CREATE INDEX payouts_open_status_idx ON payouts (status) WHERE status IN ('PENDING', 'PROCESSING', 'FAILED');

-- Последняя линия защиты: payout можно создать только после того, как деньги
-- выпущены мастеру (RELEASED — всё, PARTIALLY_REFUNDED — залог), и только
-- на выпущенную сумму (комиссии с услуги у платформы нет).
CREATE FUNCTION payouts_require_released_payment() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE
    p payments%ROWTYPE;
BEGIN
    SELECT * INTO p FROM payments WHERE id = NEW.payment_id;

    IF p.status IS NULL OR p.status NOT IN ('RELEASED', 'PARTIALLY_REFUNDED') THEN
        RAISE EXCEPTION 'payout requires a RELEASED or PARTIALLY_REFUNDED payment (payment %, status %)',
            NEW.payment_id, p.status
            USING ERRCODE = 'check_violation';
    END IF;

    IF NEW.amount <> p.released_amount OR NEW.currency <> p.currency THEN
        RAISE EXCEPTION 'payout must equal the released amount of payment % (% %)',
            NEW.payment_id, p.released_amount, p.currency
            USING ERRCODE = 'check_violation';
    END IF;

    RETURN NEW;
END $$;

CREATE TRIGGER payouts_require_released_payment
    BEFORE INSERT OR UPDATE OF payment_id, amount, currency ON payouts
    FOR EACH ROW EXECUTE FUNCTION payouts_require_released_payment();

-- -----------------------------------------------------------------------------
-- REVIEWS / FAVORITES / NOTIFICATIONS
-- -----------------------------------------------------------------------------
CREATE TABLE reviews (
    id         uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    booking_id uuid        NOT NULL,
    client_id  uuid        NOT NULL REFERENCES users (id),
    master_id  uuid        NOT NULL REFERENCES masters (id),
    rating     int         NOT NULL,
    text       text,
    created_at timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT reviews_booking_id_key UNIQUE (booking_id),
    -- client_id и master_id обязаны совпадать с участниками записи
    CONSTRAINT reviews_booking_participants_fk
        FOREIGN KEY (booking_id, client_id, master_id) REFERENCES bookings (id, client_id, master_id),
    CONSTRAINT reviews_rating_range CHECK (rating BETWEEN 1 AND 5)
);
CREATE INDEX reviews_master_created_idx ON reviews (master_id, created_at DESC);

CREATE TABLE favorites (
    client_id  uuid        NOT NULL REFERENCES users (id),
    master_id  uuid        NOT NULL REFERENCES masters (id),
    created_at timestamptz NOT NULL DEFAULT now(),

    PRIMARY KEY (client_id, master_id)
);

CREATE TABLE notifications (
    id         uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id    uuid         NOT NULL REFERENCES users (id),
    type       varchar(50)  NOT NULL,
    title      varchar(255) NOT NULL,
    message    text,
    booking_id uuid         REFERENCES bookings (id),
    is_read    boolean      NOT NULL DEFAULT false,
    created_at timestamptz  NOT NULL DEFAULT now()
);
CREATE INDEX notifications_user_created_idx ON notifications (user_id, created_at DESC);

-- -----------------------------------------------------------------------------
-- CHAT — личный чат клиента и мастера (как на Kufar).
-- Один диалог на пару «клиент + мастер»; открывается backend'ом, когда
-- первая запись этой пары оплачена (CONFIRMED). Деньги здесь не участвуют.
-- -----------------------------------------------------------------------------
CREATE TABLE conversations (
    id              uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    client_id       uuid        NOT NULL REFERENCES users (id),
    master_id       uuid        NOT NULL REFERENCES masters (id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    last_message_at timestamptz,                         -- для сортировки списка чатов

    CONSTRAINT conversations_client_master_key UNIQUE (client_id, master_id)
);
CREATE INDEX conversations_master_last_idx ON conversations (master_id, last_message_at DESC);
CREATE INDEX conversations_client_last_idx ON conversations (client_id, last_message_at DESC);

-- Сообщение — текст, фото или и то и другое (одно фото на сообщение).
-- Фото хранится в файловом хранилище, в БД — только ключ файла; отдаётся
-- только участникам диалога через авторизованный endpoint.
CREATE TABLE messages (
    id                      uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    conversation_id         uuid         NOT NULL REFERENCES conversations (id),
    sender_id               uuid         NOT NULL REFERENCES users (id),
    text                    text,
    attachment_key          varchar(500),
    attachment_content_type varchar(100),
    attachment_size_bytes   int,
    created_at              timestamptz  NOT NULL DEFAULT now(),
    read_at                 timestamptz,

    CONSTRAINT messages_not_empty
        CHECK ((text IS NOT NULL AND btrim(text) <> '') OR attachment_key IS NOT NULL),
    CONSTRAINT messages_attachment_fields_together
        CHECK ((attachment_key IS NULL) = (attachment_content_type IS NULL)
           AND (attachment_key IS NULL) = (attachment_size_bytes IS NULL)),
    CONSTRAINT messages_attachment_is_image
        CHECK (attachment_content_type IS NULL OR attachment_content_type IN ('image/jpeg', 'image/png', 'image/webp')),
    CONSTRAINT messages_attachment_size_limit
        CHECK (attachment_size_bytes IS NULL OR attachment_size_bytes BETWEEN 1 AND 10485760),  -- до 10 МБ
    CONSTRAINT messages_text_length CHECK (text IS NULL OR length(text) <= 4000)
);
CREATE INDEX messages_conversation_created_idx ON messages (conversation_id, created_at);

-- -----------------------------------------------------------------------------
-- SUBSCRIPTIONS — поток №1, доход платформы (MASTER → ЗРОБИМ)
-- -----------------------------------------------------------------------------
CREATE TABLE subscription_plans (
    id            uuid                   PRIMARY KEY DEFAULT gen_random_uuid(),
    code          subscription_plan_code NOT NULL,
    name          varchar(100)           NOT NULL,
    price_monthly decimal(12,2)          NOT NULL,   -- текущая цена; меняется без миграций
    is_promoted   boolean                NOT NULL DEFAULT false,  -- поиск смотрит сюда, а не на code
    is_active     boolean                NOT NULL DEFAULT true,
    created_at    timestamptz            NOT NULL DEFAULT now(),
    updated_at    timestamptz            NOT NULL DEFAULT now(),

    CONSTRAINT subscription_plans_code_key       UNIQUE (code),
    CONSTRAINT subscription_plans_price_positive CHECK (price_monthly > 0)
);

CREATE TABLE subscription_payments (
    id                  uuid                        PRIMARY KEY DEFAULT gen_random_uuid(),
    master_id           uuid                        NOT NULL REFERENCES masters (id),
    plan_id             uuid                        NOT NULL REFERENCES subscription_plans (id),
    amount              decimal(12,2)               NOT NULL,   -- snapshot price_monthly
    currency            char(3)                     NOT NULL DEFAULT 'BYN',
    status              subscription_payment_status NOT NULL DEFAULT 'PENDING',
    provider            varchar(50)                 NOT NULL,
    external_payment_id varchar(255),
    paid_at             timestamptz,
    created_at          timestamptz                 NOT NULL DEFAULT now(),
    updated_at          timestamptz                 NOT NULL DEFAULT now(),

    CONSTRAINT subscription_payments_provider_external_id_key UNIQUE (provider, external_payment_id),
    -- цель для составного FK из master_subscriptions
    CONSTRAINT subscription_payments_id_master_plan_key UNIQUE (id, master_id, plan_id),
    CONSTRAINT subscription_payments_amount_positive CHECK (amount > 0),
    CONSTRAINT subscription_payments_currency_format CHECK (currency ~ '^[A-Z]{3}$'),
    CONSTRAINT subscription_payments_paid_at_iff_received
        CHECK ((status IN ('PAID', 'REFUNDED')) = (paid_at IS NOT NULL))
);
CREATE INDEX subscription_payments_master_created_idx ON subscription_payments (master_id, created_at DESC);

-- Выданные периоды доступа. Статуса нет: активность = даты + cancelled_at.
-- Продление / смена тарифа = новая строка; история не удаляется.
CREATE TABLE master_subscriptions (
    id                      uuid          PRIMARY KEY DEFAULT gen_random_uuid(),
    master_id               uuid          NOT NULL REFERENCES masters (id),
    plan_id                 uuid          NOT NULL REFERENCES subscription_plans (id),
    subscription_payment_id uuid,         -- NULL только для бесплатного trial / выдачи админом
    price                   decimal(12,2) NOT NULL,   -- snapshot цены на момент покупки
    starts_at               timestamptz   NOT NULL,
    expires_at              timestamptz   NOT NULL,
    cancelled_at            timestamptz,
    created_at              timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT master_subscriptions_payment_key UNIQUE (subscription_payment_id),
    -- оплата принадлежит тому же мастеру и тому же тарифу
    CONSTRAINT master_subscriptions_payment_fk
        FOREIGN KEY (subscription_payment_id, master_id, plan_id)
        REFERENCES subscription_payments (id, master_id, plan_id),
    CONSTRAINT master_subscriptions_price_nonneg CHECK (price >= 0),
    -- без оплаты (trial / админ) — только бесплатно; с оплатой — только платно
    CONSTRAINT master_subscriptions_free_iff_no_payment
        CHECK ((subscription_payment_id IS NULL) = (price = 0)),
    CONSTRAINT master_subscriptions_time_order CHECK (expires_at > starts_at),
    -- у мастера не может быть двух действующих периодов одновременно
    CONSTRAINT master_subscriptions_no_overlap EXCLUDE USING gist (
        master_id WITH =,
        tstzrange(starts_at, expires_at) WITH &&
    ) WHERE (cancelled_at IS NULL)
);
CREATE INDEX master_subscriptions_master_expires_idx ON master_subscriptions (master_id, expires_at);

-- Период по оплате выдаётся только после успешной оплаты (PAID).
CREATE FUNCTION master_subscriptions_require_paid_payment() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE
    s subscription_payment_status;
BEGIN
    IF NEW.subscription_payment_id IS NULL THEN
        RETURN NEW;
    END IF;

    SELECT status INTO s FROM subscription_payments WHERE id = NEW.subscription_payment_id;

    IF s IS DISTINCT FROM 'PAID' THEN
        RAISE EXCEPTION 'subscription period requires a PAID subscription payment (payment %, status %)',
            NEW.subscription_payment_id, s
            USING ERRCODE = 'check_violation';
    END IF;

    RETURN NEW;
END $$;

CREATE TRIGGER master_subscriptions_require_paid_payment
    BEFORE INSERT OR UPDATE OF subscription_payment_id ON master_subscriptions
    FOR EACH ROW EXECUTE FUNCTION master_subscriptions_require_paid_payment();

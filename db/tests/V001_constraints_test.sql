-- =============================================================================
-- Проверка ограничений схемы на настоящем PostgreSQL.
-- Всё создаётся во временной схеме zrobim_test внутри одной транзакции и в конце
-- откатывается (ROLLBACK): настоящая схема public и данные не затрагиваются.
--
-- Обычно запускается через db/tests/run-tests.ps1 (проверяет схему из миграций EF Core).
-- Вручную, против эталонного SQL:
--   psql -X -q -v ON_ERROR_STOP=1 -h localhost -U zrobim -d zrobim_dev -f V001_constraints_test.sql
-- Против другого скрипта схемы:  ... -v schema=путь/к/schema.sql
--
-- Каждая проверка печатает NOTICE "PASS ...". Первая же ошибка останавливает скрипт.
-- Услуга в примерах стоит 100 BYN, залог 30 BYN.
-- =============================================================================
\set ON_ERROR_STOP on
\set QUIET on

BEGIN;

CREATE SCHEMA zrobim_test;
SET LOCAL search_path = zrobim_test, public;

\if :{?schema}
\i :schema
\else
\ir ../migrations/V001__safe_deal_schema.sql
\endif

-- ---------- helpers ----------------------------------------------------------
CREATE PROCEDURE pg_temp.expect_error(p_label text, p_sql text, p_state text)
LANGUAGE plpgsql AS $$
BEGIN
    BEGIN
        EXECUTE p_sql;
    EXCEPTION WHEN OTHERS THEN
        IF SQLSTATE = p_state THEN
            RAISE NOTICE 'PASS  %  [rejected: %]', p_label, SQLSTATE;
            RETURN;
        END IF;
        RAISE EXCEPTION 'FAIL  %: expected %, got % (%)', p_label, p_state, SQLSTATE, SQLERRM
            USING ERRCODE = 'ZT999';
    END;
    RAISE EXCEPTION 'FAIL  %: expected error %, but statement succeeded', p_label, p_state
        USING ERRCODE = 'ZT999';
END $$;

CREATE PROCEDURE pg_temp.expect_rows(p_label text, p_sql text, p_expected int)
LANGUAGE plpgsql AS $$
DECLARE
    n int;
BEGIN
    EXECUTE p_sql;
    GET DIAGNOSTICS n = ROW_COUNT;
    IF n <> p_expected THEN
        RAISE EXCEPTION 'FAIL  %: expected % row(s), got %', p_label, p_expected, n USING ERRCODE = 'ZT999';
    END IF;
    RAISE NOTICE 'PASS  %  [% row(s)]', p_label, n;
END $$;

CREATE PROCEDURE pg_temp.ok(p_label text)
LANGUAGE plpgsql AS $$
BEGIN
    RAISE NOTICE 'PASS  %', p_label;
END $$;

-- ---------- fixtures ---------------------------------------------------------
-- users: 01 client1, 02 client2, 03 master1, 04 master2, 05 admin
INSERT INTO users (id, email, password_hash, role, first_name) VALUES
  ('00000000-0000-0000-0000-000000000001', 'client1@test.by', 'x', 'CLIENT', 'Client1'),
  ('00000000-0000-0000-0000-000000000002', 'client2@test.by', 'x', 'CLIENT', 'Client2'),
  ('00000000-0000-0000-0000-000000000003', 'master1@test.by', 'x', 'MASTER', 'Master1'),
  ('00000000-0000-0000-0000-000000000004', 'master2@test.by', 'x', 'MASTER', 'Master2'),
  ('00000000-0000-0000-0000-000000000005', 'admin@test.by',   'x', 'ADMIN',  'Admin');

INSERT INTO masters (id, user_id, city, verification_status, payout_account) VALUES
  ('10000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000003', 'Minsk', 'APPROVED', 'BY20OLMP31350000001000000933'),
  ('10000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000004', 'Minsk', 'APPROVED', NULL);

INSERT INTO categories (id, name) VALUES ('20000000-0000-0000-0000-000000000001', 'Маникюр');

INSERT INTO services (id, master_id, category_id, name, price, duration_minutes, location_type) VALUES
  ('30000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', 'Маникюр',         100.00, 60, 'MASTER_PLACE'),
  ('30000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000001', 'Маникюр (М2)',     80.00, 60, 'MASTER_PLACE'),
  ('30000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', 'Маникюр на дому', 120.00, 60, 'CLIENT_PLACE');

-- Replace migration seed data with deterministic test plans in this throw-away schema.
DELETE FROM subscription_plans;
INSERT INTO subscription_plans (id, code, name, price_monthly, is_promoted) VALUES
  ('80000000-0000-0000-0000-000000000001', 'BASIC', 'Basic', 15.00, false),
  ('80000000-0000-0000-0000-000000000002', 'PLUS',  'Plus',  30.00, true);

-- ---------- users / masters / schedule ---------------------------------------
CALL pg_temp.expect_error('users: email unique case-insensitive',
  $q$INSERT INTO users (email, password_hash, role, first_name) VALUES ('CLIENT1@TEST.BY', 'x', 'CLIENT', 'Dup')$q$, '23505');

CALL pg_temp.expect_error('masters: raw card number rejected in payout_account',
  $q$UPDATE masters SET payout_account = '4111 1111 1111 1111' WHERE id = '10000000-0000-0000-0000-000000000002'$q$, '23514');

INSERT INTO master_working_hours (master_id, day_of_week, start_time, end_time)
VALUES ('10000000-0000-0000-0000-000000000001', 1, '09:00', '18:00');

CALL pg_temp.expect_error('working_hours: one row per day',
  $q$INSERT INTO master_working_hours (master_id, day_of_week, start_time, end_time) VALUES ('10000000-0000-0000-0000-000000000001', 1, '10:00', '12:00')$q$, '23505');
CALL pg_temp.expect_error('working_hours: day_of_week 1..7',
  $q$INSERT INTO master_working_hours (master_id, day_of_week, start_time, end_time) VALUES ('10000000-0000-0000-0000-000000000001', 8, '10:00', '12:00')$q$, '23514');
CALL pg_temp.expect_error('working_hours: end after start',
  $q$INSERT INTO master_working_hours (master_id, day_of_week, start_time, end_time) VALUES ('10000000-0000-0000-0000-000000000001', 2, '18:00', '09:00')$q$, '23514');

-- ---------- bookings: заявка → мастер принял → оплата -------------------------
CALL pg_temp.expect_error('bookings: old NO_SHOW status does not exist',
  $q$SELECT 'NO_SHOW'::booking_status$q$, '22P02');

CALL pg_temp.expect_error('bookings: service must belong to booking master',
  $q$INSERT INTO bookings (client_id, master_id, service_id, start_at, end_at, price, deposit_amount, location_type, response_deadline_at)
     VALUES ('00000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000002',
             '2026-11-02 10:00+03', '2026-11-02 11:00+03', 80.00, 24.00, 'MASTER_PLACE', '2026-11-02 08:00+03')$q$, '23503');

CALL pg_temp.expect_error('bookings: CLIENT_PLACE requires address',
  $q$INSERT INTO bookings (client_id, master_id, service_id, start_at, end_at, price, deposit_amount, location_type, response_deadline_at)
     VALUES ('00000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000003',
             '2026-11-02 16:00+03', '2026-11-02 17:00+03', 120.00, 36.00, 'CLIENT_PLACE', '2026-11-02 14:00+03')$q$, '23514');

CALL pg_temp.expect_error('bookings: deposit cannot exceed price',
  $q$INSERT INTO bookings (client_id, master_id, service_id, start_at, end_at, price, deposit_amount, location_type, response_deadline_at)
     VALUES ('00000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000001',
             '2026-11-02 16:00+03', '2026-11-02 17:00+03', 100.00, 130.00, 'MASTER_PLACE', '2026-11-02 14:00+03')$q$, '23514');

CALL pg_temp.expect_error('bookings: master response deadline must be before start',
  $q$INSERT INTO bookings (client_id, master_id, service_id, start_at, end_at, price, deposit_amount, location_type, response_deadline_at)
     VALUES ('00000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000001',
             '2026-11-02 16:00+03', '2026-11-02 17:00+03', 100.00, 30.00, 'MASTER_PLACE', '2026-11-02 18:00+03')$q$, '23514');

-- b1: заявка client1 на 10:00–11:00 (PENDING — время держится за первым)
INSERT INTO bookings (id, client_id, master_id, service_id, start_at, end_at, price, deposit_amount, location_type, response_deadline_at) VALUES
  ('40000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001',
   '30000000-0000-0000-0000-000000000001', '2026-11-02 10:00+03', '2026-11-02 11:00+03', 100.00, 30.00, 'MASTER_PLACE', '2026-11-02 08:00+03');

CALL pg_temp.expect_error('bookings: second request for a held slot (overlap)',
  $q$INSERT INTO bookings (client_id, master_id, service_id, start_at, end_at, price, deposit_amount, location_type, response_deadline_at)
     VALUES ('00000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000001',
             '2026-11-02 10:30+03', '2026-11-02 11:30+03', 100.00, 30.00, 'MASTER_PLACE', '2026-11-02 08:30+03')$q$, '23P01');

-- b3: 11:00–12:00 (стык с b1 — не пересечение)
INSERT INTO bookings (id, client_id, master_id, service_id, start_at, end_at, price, deposit_amount, location_type, response_deadline_at) VALUES
  ('40000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001',
   '30000000-0000-0000-0000-000000000001', '2026-11-02 11:00+03', '2026-11-02 12:00+03', 100.00, 30.00, 'MASTER_PLACE', '2026-11-02 09:00+03');
CALL pg_temp.ok('bookings: adjacent slot 11:00–12:00 allowed');

CALL pg_temp.expect_error('bookings: AWAITING_PAYMENT requires master acceptance',
  $q$UPDATE bookings SET status = 'AWAITING_PAYMENT' WHERE id = '40000000-0000-0000-0000-000000000003'$q$, '23514');
CALL pg_temp.expect_error('bookings: CANCELLED requires cancelled_at',
  $q$UPDATE bookings SET status = 'CANCELLED' WHERE id = '40000000-0000-0000-0000-000000000001'$q$, '23514');

-- мастер не ответил вовремя → система отменяет заявку b1 (cancelled_by = NULL)
CALL pg_temp.expect_rows('bookings: unanswered request PENDING → CANCELLED',
  $q$UPDATE bookings SET status = 'CANCELLED', cancelled_at = now(), cancellation_reason = 'MASTER_RESPONSE_TIMEOUT'
     WHERE id = '40000000-0000-0000-0000-000000000001' AND status = 'PENDING'$q$, 1);

-- b2: client2 занимает освободившийся слот 10:00–11:00
INSERT INTO bookings (id, client_id, master_id, service_id, start_at, end_at, price, deposit_amount, location_type, response_deadline_at) VALUES
  ('40000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001',
   '30000000-0000-0000-0000-000000000001', '2026-11-02 10:00+03', '2026-11-02 11:00+03', 100.00, 30.00, 'MASTER_PLACE', '2026-11-02 08:00+03');
CALL pg_temp.ok('bookings: cancelled request frees its slot');

CALL pg_temp.expect_rows('bookings: master accepts PENDING → AWAITING_PAYMENT',
  $q$UPDATE bookings SET status = 'AWAITING_PAYMENT', accepted_at = now(), payment_deadline_at = now() + interval '6 hours'
     WHERE id = '40000000-0000-0000-0000-000000000002' AND status = 'PENDING'$q$, 1);

-- ---------- booking notifications --------------------------------------------
UPDATE bookings SET status = status WHERE id = '40000000-0000-0000-0000-000000000002';

DO $$
DECLARE
    b1 constant uuid := '40000000-0000-0000-0000-000000000001';
    b2 constant uuid := '40000000-0000-0000-0000-000000000002';
    client1 constant uuid := '00000000-0000-0000-0000-000000000001';
    client2 constant uuid := '00000000-0000-0000-0000-000000000002';
    master1 constant uuid := '00000000-0000-0000-0000-000000000003';
BEGIN
    IF (SELECT count(*) FROM notifications WHERE booking_id = b2) <> 2
       OR NOT EXISTS (SELECT FROM notifications WHERE booking_id = b2 AND user_id = master1 AND type = 'BOOKING_REQUESTED' AND NOT is_read)
       OR NOT EXISTS (SELECT FROM notifications WHERE booking_id = b2 AND user_id = client2 AND type = 'BOOKING_ACCEPTED' AND NOT is_read) THEN
        RAISE EXCEPTION 'Unexpected notification recipients or duplicates for booking acceptance';
    END IF;
    IF (SELECT count(*) FROM notifications WHERE booking_id = b1 AND type = 'BOOKING_RESPONSE_TIMEOUT'
          AND user_id IN (client1, master1)) <> 2 THEN
        RAISE EXCEPTION 'Both participants must receive a response timeout notification';
    END IF;
    RAISE NOTICE 'PASS  booking notifications: recipients, unread state and no duplicates on unchanged status';
END $$;

-- ---------- payments: «корзина» 20 минут, webhook -----------------------------
INSERT INTO payments (id, booking_id, amount, provider, expires_at) VALUES
  ('50000000-0000-0000-0000-000000000001', '40000000-0000-0000-0000-000000000002', 100.00, 'mock', now() + interval '20 minutes');

CALL pg_temp.expect_error('payments: only one alive payment per booking',
  $q$INSERT INTO payments (booking_id, amount, provider, expires_at) VALUES ('40000000-0000-0000-0000-000000000002', 100.00, 'mock', now() + interval '20 minutes')$q$, '23505');

-- корзина истекла → FAILED, клиент нажимает «Оплатить» ещё раз
UPDATE payments SET status = 'FAILED', failure_reason = 'cart expired' WHERE id = '50000000-0000-0000-0000-000000000001';
INSERT INTO payments (id, booking_id, amount, provider, expires_at) VALUES
  ('50000000-0000-0000-0000-000000000002', '40000000-0000-0000-0000-000000000002', 100.00, 'mock', now() + interval '20 minutes');
CALL pg_temp.ok('payments: new 20-minute cart allowed after FAILED');

CALL pg_temp.expect_error('payments: HELD requires paid_at',
  $q$UPDATE payments SET status = 'HELD' WHERE id = '50000000-0000-0000-0000-000000000002'$q$, '23514');
CALL pg_temp.expect_error('payments: release_after only after money received',
  $q$UPDATE payments SET release_after = now() WHERE id = '50000000-0000-0000-0000-000000000002'$q$, '23514');

CALL pg_temp.expect_rows('webhook: PENDING → HELD (first delivery)',
  $q$UPDATE payments SET status = 'HELD', paid_at = now(), external_payment_id = 'mock_pay_1'
     WHERE id = '50000000-0000-0000-0000-000000000002' AND status = 'PENDING'$q$, 1);
CALL pg_temp.expect_rows('webhook: repeated delivery is a no-op',
  $q$UPDATE payments SET status = 'HELD', paid_at = now(), external_payment_id = 'mock_pay_1'
     WHERE id = '50000000-0000-0000-0000-000000000002' AND status = 'PENDING'$q$, 0);
UPDATE bookings SET status = 'CONFIRMED' WHERE id = '40000000-0000-0000-0000-000000000002';

CALL pg_temp.expect_error('payments: (provider, external_payment_id) unique',
  $q$INSERT INTO payments (booking_id, amount, provider, external_payment_id, expires_at)
     VALUES ('40000000-0000-0000-0000-000000000003', 100.00, 'mock', 'mock_pay_1', now() + interval '20 minutes')$q$, '23505');

CALL pg_temp.expect_error('bookings: deposit_outcome not allowed on a normal CONFIRMED booking',
  $q$UPDATE bookings SET deposit_outcome = 'KEPT_BY_MASTER' WHERE id = '40000000-0000-0000-0000-000000000002'$q$, '23514');

-- ---------- нормальная услуга: всё мастеру ------------------------------------
CALL pg_temp.expect_error('payouts: cannot create payout for HELD payment',
  $q$INSERT INTO payouts (payment_id, amount, provider, destination)
     VALUES ('50000000-0000-0000-0000-000000000002', 100.00, 'mock', 'BY20OLMP31350000001000000933')$q$, '23514');

-- мастер отметил выполнение; окно спора уже прошло (release_after в прошлом)
UPDATE bookings SET status = 'COMPLETED', completed_at = now() WHERE id = '40000000-0000-0000-0000-000000000002';
UPDATE payments SET release_after = now() - interval '1 minute' WHERE id = '50000000-0000-0000-0000-000000000002';

CALL pg_temp.expect_error('payments: RELEASED must move the full amount to master',
  $q$UPDATE payments SET status = 'RELEASED', released_at = now(), released_amount = 30.00
     WHERE id = '50000000-0000-0000-0000-000000000002'$q$, '23514');

CALL pg_temp.expect_rows('auto-release: HELD → RELEASED (100 to master)',
  $q$UPDATE payments SET status = 'RELEASED', released_at = now(), released_amount = amount
     WHERE status = 'HELD' AND release_after <= now() AND id = '50000000-0000-0000-0000-000000000002'$q$, 1);
CALL pg_temp.expect_rows('auto-release: second run is a no-op',
  $q$UPDATE payments SET status = 'RELEASED', released_at = now(), released_amount = amount
     WHERE status = 'HELD' AND release_after <= now() AND id = '50000000-0000-0000-0000-000000000002'$q$, 0);
CALL pg_temp.expect_rows('refund after release is a no-op',
  $q$UPDATE payments SET status = 'REFUNDED', refunded_at = now(), refunded_amount = amount
     WHERE id = '50000000-0000-0000-0000-000000000002' AND status = 'HELD'$q$, 0);

CALL pg_temp.expect_error('payouts: amount must equal released amount',
  $q$INSERT INTO payouts (payment_id, amount, provider, destination)
     VALUES ('50000000-0000-0000-0000-000000000002', 150.00, 'mock', 'BY20OLMP31350000001000000933')$q$, '23514');
CALL pg_temp.expect_error('payouts: raw card number rejected in destination',
  $q$INSERT INTO payouts (payment_id, amount, provider, destination)
     VALUES ('50000000-0000-0000-0000-000000000002', 100.00, 'mock', '4111111111111111')$q$, '23514');

INSERT INTO payouts (id, payment_id, amount, provider, destination) VALUES
  ('60000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000002', 100.00, 'mock', 'BY20OLMP31350000001000000933');
CALL pg_temp.ok('payouts: payout of 100 for RELEASED payment created');

CALL pg_temp.expect_error('payouts: second payout for the same payment',
  $q$INSERT INTO payouts (payment_id, amount, provider, destination)
     VALUES ('50000000-0000-0000-0000-000000000002', 100.00, 'mock', 'BY20OLMP31350000001000000933')$q$, '23505');
CALL pg_temp.expect_error('payouts: COMPLETED requires completed_at',
  $q$UPDATE payouts SET status = 'COMPLETED' WHERE id = '60000000-0000-0000-0000-000000000001'$q$, '23514');

-- ---------- поздняя отмена клиентом: залог мастеру ----------------------------
-- b4: 13:00–14:00, оплачено (CONFIRMED + HELD)
INSERT INTO bookings (id, client_id, master_id, service_id, start_at, end_at, price, deposit_amount, location_type,
                      response_deadline_at, accepted_at, payment_deadline_at, status) VALUES
  ('40000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001',
   '30000000-0000-0000-0000-000000000001', '2026-11-02 13:00+03', '2026-11-02 14:00+03', 100.00, 30.00, 'MASTER_PLACE',
   '2026-11-02 11:00+03', now(), now() + interval '6 hours', 'CONFIRMED');
INSERT INTO payments (id, booking_id, amount, provider, expires_at, status, paid_at, external_payment_id) VALUES
  ('50000000-0000-0000-0000-000000000004', '40000000-0000-0000-0000-000000000004', 100.00, 'mock', now() + interval '20 minutes', 'HELD', now(), 'mock_pay_4');

-- клиент отменил менее чем за 24 ч; у мастера 24 ч на решение (таймер уже истёк)
UPDATE bookings SET status = 'CANCELLED', cancelled_at = now(), cancelled_by = '00000000-0000-0000-0000-000000000001',
                    cancellation_reason = 'late cancellation by client'
WHERE id = '40000000-0000-0000-0000-000000000004';
UPDATE payments SET release_after = now() - interval '1 minute' WHERE id = '50000000-0000-0000-0000-000000000004';

CALL pg_temp.expect_error('payments: split must add up to the full amount (30 + 60 ≠ 100)',
  $q$UPDATE payments SET status = 'PARTIALLY_REFUNDED', released_amount = 30.00, refunded_amount = 60.00,
                         released_at = now(), refunded_at = now()
     WHERE id = '50000000-0000-0000-0000-000000000004'$q$, '23514');

-- мастер не ответил за 24 ч → залог ему автоматически
UPDATE bookings SET deposit_outcome = 'KEPT_AUTOMATICALLY' WHERE id = '40000000-0000-0000-0000-000000000004';
CALL pg_temp.expect_rows('deposit kept automatically: HELD → PARTIALLY_REFUNDED (30 master / 70 client)',
  $q$UPDATE payments SET status = 'PARTIALLY_REFUNDED', released_amount = 30.00, refunded_amount = 70.00,
                         released_at = now(), refunded_at = now()
     WHERE id = '50000000-0000-0000-0000-000000000004' AND status = 'HELD' AND release_after <= now()$q$, 1);

CALL pg_temp.expect_error('payouts: deposit payout cannot be the full 100',
  $q$INSERT INTO payouts (payment_id, amount, provider, destination)
     VALUES ('50000000-0000-0000-0000-000000000004', 100.00, 'mock', 'BY20OLMP31350000001000000933')$q$, '23514');
INSERT INTO payouts (payment_id, amount, provider, destination) VALUES
  ('50000000-0000-0000-0000-000000000004', 30.00, 'mock', 'BY20OLMP31350000001000000933');
CALL pg_temp.ok('payouts: payout of the 30 BYN deposit created');

-- ---------- неявка клиента, мастер вошёл в положение --------------------------
-- b5: 15:00–16:00, client2, оплачено
INSERT INTO bookings (id, client_id, master_id, service_id, start_at, end_at, price, deposit_amount, location_type,
                      response_deadline_at, accepted_at, payment_deadline_at, status) VALUES
  ('40000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001',
   '30000000-0000-0000-0000-000000000001', '2026-11-02 15:00+03', '2026-11-02 16:00+03', 100.00, 30.00, 'MASTER_PLACE',
   '2026-11-02 13:00+03', now(), now() + interval '6 hours', 'CONFIRMED');
INSERT INTO payments (id, booking_id, amount, provider, expires_at, status, paid_at, external_payment_id) VALUES
  ('50000000-0000-0000-0000-000000000005', '40000000-0000-0000-0000-000000000005', 100.00, 'mock', now() + interval '20 minutes', 'HELD', now(), 'mock_pay_5');

UPDATE bookings SET status = 'CLIENT_NO_SHOW', deposit_outcome = 'WAIVED_BY_MASTER' WHERE id = '40000000-0000-0000-0000-000000000005';
CALL pg_temp.expect_rows('deposit waived by master: HELD → REFUNDED (100 to client)',
  $q$UPDATE payments SET status = 'REFUNDED', refunded_amount = amount, refunded_at = now(), release_after = NULL
     WHERE id = '50000000-0000-0000-0000-000000000005' AND status = 'HELD'$q$, 1);
CALL pg_temp.expect_error('payouts: nothing to pay out for a fully REFUNDED payment',
  $q$INSERT INTO payouts (payment_id, amount, provider, destination)
     VALUES ('50000000-0000-0000-0000-000000000005', 30.00, 'mock', 'BY20OLMP31350000001000000933')$q$, '23514');

-- ---------- спор: администратор решает «залог мастеру» ------------------------
-- b3: оплачено, мастер отметил COMPLETED → окно спора открыто
UPDATE bookings SET status = 'AWAITING_PAYMENT', accepted_at = now(), payment_deadline_at = now() + interval '6 hours'
WHERE id = '40000000-0000-0000-0000-000000000003';
INSERT INTO payments (id, booking_id, amount, provider, expires_at, status, paid_at, release_after, external_payment_id) VALUES
  ('50000000-0000-0000-0000-000000000003', '40000000-0000-0000-0000-000000000003', 100.00, 'mock', now() + interval '20 minutes',
   'HELD', now(), now() + interval '24 hours', 'mock_pay_3');
UPDATE bookings SET status = 'COMPLETED', completed_at = now() WHERE id = '40000000-0000-0000-0000-000000000003';

-- клиент открывает спор: booking → DISPUTED, таймер выпуска останавливается
INSERT INTO disputes (id, booking_id, opened_by, reason) VALUES
  ('70000000-0000-0000-0000-000000000001', '40000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001', 'POOR_QUALITY');
UPDATE bookings SET status = 'DISPUTED' WHERE id = '40000000-0000-0000-0000-000000000003';
UPDATE payments SET release_after = NULL WHERE id = '50000000-0000-0000-0000-000000000003';

CALL pg_temp.expect_rows('auto-release skips disputed payment (release_after IS NULL)',
  $q$UPDATE payments SET status = 'RELEASED', released_at = now(), released_amount = amount
     WHERE status = 'HELD' AND release_after <= now() AND id = '50000000-0000-0000-0000-000000000003'$q$, 0);
CALL pg_temp.expect_error('disputes: max one dispute per booking',
  $q$INSERT INTO disputes (booking_id, opened_by, reason)
     VALUES ('40000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001', 'OTHER')$q$, '23505');
CALL pg_temp.expect_error('disputes: RESOLVED requires resolution/resolved_by/resolved_at',
  $q$UPDATE disputes SET status = 'RESOLVED' WHERE id = '70000000-0000-0000-0000-000000000001'$q$, '23514');

UPDATE disputes SET status = 'RESOLVED', resolution = 'DEPOSIT_TO_MASTER', resolved_by = '00000000-0000-0000-0000-000000000005', resolved_at = now()
WHERE id = '70000000-0000-0000-0000-000000000001';
CALL pg_temp.expect_rows('dispute resolved DEPOSIT_TO_MASTER: HELD → PARTIALLY_REFUNDED',
  $q$UPDATE payments SET status = 'PARTIALLY_REFUNDED', released_amount = 30.00, refunded_amount = 70.00,
                         released_at = now(), refunded_at = now()
     WHERE id = '50000000-0000-0000-0000-000000000003' AND status = 'HELD'$q$, 1);

-- ---------- reviews ----------------------------------------------------------
CALL pg_temp.expect_error('reviews: client must be the booking client',
  $q$INSERT INTO reviews (booking_id, client_id, master_id, rating)
     VALUES ('40000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 5)$q$, '23503');
CALL pg_temp.expect_error('reviews: rating 1..5',
  $q$INSERT INTO reviews (booking_id, client_id, master_id, rating)
     VALUES ('40000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', 6)$q$, '23514');
INSERT INTO reviews (booking_id, client_id, master_id, rating) VALUES
  ('40000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', 5);
CALL pg_temp.expect_error('reviews: one review per booking',
  $q$INSERT INTO reviews (booking_id, client_id, master_id, rating)
     VALUES ('40000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', 4)$q$, '23505');

-- ---------- chat -------------------------------------------------------------
INSERT INTO conversations (id, client_id, master_id) VALUES
  ('b0000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001');
CALL pg_temp.expect_error('chat: one conversation per client–master pair',
  $q$INSERT INTO conversations (client_id, master_id) VALUES ('00000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001')$q$, '23505');

INSERT INTO messages (conversation_id, sender_id, text) VALUES
  ('b0000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000002', 'Здравствуйте! Можно на 10 минут позже?');
INSERT INTO messages (conversation_id, sender_id, attachment_key, attachment_content_type, attachment_size_bytes) VALUES
  ('b0000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000002', 'chat/b0000000/photo1.jpg', 'image/jpeg', 245000);
CALL pg_temp.ok('chat: text message and photo message stored');

CALL pg_temp.expect_error('chat: empty message rejected',
  $q$INSERT INTO messages (conversation_id, sender_id, text) VALUES ('b0000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000002', '   ')$q$, '23514');
CALL pg_temp.expect_error('chat: only images as attachments',
  $q$INSERT INTO messages (conversation_id, sender_id, attachment_key, attachment_content_type, attachment_size_bytes)
     VALUES ('b0000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000002', 'chat/x/file.pdf', 'application/pdf', 1000)$q$, '23514');
CALL pg_temp.expect_error('chat: attachment fields go together',
  $q$INSERT INTO messages (conversation_id, sender_id, attachment_key)
     VALUES ('b0000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000002', 'chat/x/photo.jpg')$q$, '23514');
CALL pg_temp.expect_error('chat: photo larger than 10 MB rejected',
  $q$INSERT INTO messages (conversation_id, sender_id, attachment_key, attachment_content_type, attachment_size_bytes)
     VALUES ('b0000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000002', 'chat/x/big.png', 'image/png', 20000000)$q$, '23514');

-- ---------- subscriptions (поток №1) -----------------------------------------
-- пробный период 7 дней: бесплатно, без оплаты
INSERT INTO master_subscriptions (id, master_id, plan_id, price, starts_at, expires_at) VALUES
  ('a0000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '80000000-0000-0000-0000-000000000001', 0,
   '2026-11-01 00:00+03', '2026-11-08 00:00+03');
CALL pg_temp.ok('subscriptions: free 7-day trial without payment');

CALL pg_temp.expect_error('subscriptions: two active periods cannot overlap',
  $q$INSERT INTO master_subscriptions (master_id, plan_id, price, starts_at, expires_at)
     VALUES ('10000000-0000-0000-0000-000000000001', '80000000-0000-0000-0000-000000000001', 0, '2026-11-05 00:00+03', '2026-12-05 00:00+03')$q$, '23P01');
CALL pg_temp.expect_error('subscriptions: paid period requires a payment',
  $q$INSERT INTO master_subscriptions (master_id, plan_id, price, starts_at, expires_at)
     VALUES ('10000000-0000-0000-0000-000000000001', '80000000-0000-0000-0000-000000000001', 15.00, '2026-11-08 00:00+03', '2026-12-08 00:00+03')$q$, '23514');

INSERT INTO subscription_payments (id, master_id, plan_id, amount, provider) VALUES
  ('90000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '80000000-0000-0000-0000-000000000001', 15.00, 'mock');

CALL pg_temp.expect_error('subscriptions: period requires PAID payment (not PENDING)',
  $q$INSERT INTO master_subscriptions (master_id, plan_id, subscription_payment_id, price, starts_at, expires_at)
     VALUES ('10000000-0000-0000-0000-000000000001', '80000000-0000-0000-0000-000000000001', '90000000-0000-0000-0000-000000000001',
             15.00, '2026-11-08 00:00+03', '2026-12-08 00:00+03')$q$, '23514');

CALL pg_temp.expect_rows('subscription webhook: PENDING → PAID',
  $q$UPDATE subscription_payments SET status = 'PAID', paid_at = now(), external_payment_id = 'mock_sub_1'
     WHERE id = '90000000-0000-0000-0000-000000000001' AND status = 'PENDING'$q$, 1);

-- продление: новая строка встык с пробным периодом
INSERT INTO master_subscriptions (id, master_id, plan_id, subscription_payment_id, price, starts_at, expires_at) VALUES
  ('a0000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', '80000000-0000-0000-0000-000000000001',
   '90000000-0000-0000-0000-000000000001', 15.00, '2026-11-08 00:00+03', '2026-12-08 00:00+03');
CALL pg_temp.ok('subscriptions: renewal as a new adjacent row');

CALL pg_temp.expect_error('subscriptions: one payment = one period',
  $q$INSERT INTO master_subscriptions (master_id, plan_id, subscription_payment_id, price, starts_at, expires_at)
     VALUES ('10000000-0000-0000-0000-000000000001', '80000000-0000-0000-0000-000000000001', '90000000-0000-0000-0000-000000000001',
             15.00, '2026-12-08 00:00+03', '2027-01-08 00:00+03')$q$, '23505');

-- оплата мастера 2 не может активировать подписку мастера 1
INSERT INTO subscription_payments (id, master_id, plan_id, amount, provider, status, paid_at, external_payment_id) VALUES
  ('90000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000002', '80000000-0000-0000-0000-000000000002', 30.00, 'mock', 'PAID', now(), 'mock_sub_2');
CALL pg_temp.expect_error('subscriptions: payment must belong to the same master and plan',
  $q$INSERT INTO master_subscriptions (master_id, plan_id, subscription_payment_id, price, starts_at, expires_at)
     VALUES ('10000000-0000-0000-0000-000000000001', '80000000-0000-0000-0000-000000000002', '90000000-0000-0000-0000-000000000002',
             30.00, '2026-12-08 00:00+03', '2027-01-08 00:00+03')$q$, '23503');

-- переход BASIC → PLUS 20 ноября: старый период отменяется (остаток сгорает), PLUS начинается сейчас
INSERT INTO subscription_payments (id, master_id, plan_id, amount, provider, status, paid_at, external_payment_id) VALUES
  ('90000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000001', '80000000-0000-0000-0000-000000000002', 30.00, 'mock', 'PAID', now(), 'mock_sub_3');
UPDATE master_subscriptions SET cancelled_at = '2026-11-20 12:00+03' WHERE id = 'a0000000-0000-0000-0000-000000000002';
INSERT INTO master_subscriptions (id, master_id, plan_id, subscription_payment_id, price, starts_at, expires_at) VALUES
  ('a0000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000001', '80000000-0000-0000-0000-000000000002',
   '90000000-0000-0000-0000-000000000003', 30.00, '2026-11-20 12:00+03', '2026-12-20 12:00+03');
CALL pg_temp.ok('subscriptions: upgrade BASIC → PLUS (cancel + new row)');

-- цена тарифа меняется, snapshot в купленных периодах остаётся прежним
UPDATE subscription_plans SET price_monthly = 20.00 WHERE code = 'BASIC';
DO $$
DECLARE
    snap decimal;
BEGIN
    SELECT price INTO snap FROM master_subscriptions WHERE id = 'a0000000-0000-0000-0000-000000000002';
    IF snap <> 15.00 THEN RAISE EXCEPTION 'FAIL  price snapshot changed: %', snap USING ERRCODE = 'ZT999'; END IF;
    RAISE NOTICE 'PASS  subscriptions: plan price change keeps old snapshot (15.00)';
END $$;

-- активная подписка на 25 ноября — PLUS, is_promoted = true (по датам, без статуса EXPIRED)
DO $$
DECLARE
    promoted boolean;
    cnt int;
BEGIN
    SELECT count(*), bool_or(sp.is_promoted) INTO cnt, promoted
    FROM master_subscriptions ms
    JOIN subscription_plans sp ON sp.id = ms.plan_id
    WHERE ms.master_id = '10000000-0000-0000-0000-000000000001'
      AND ms.cancelled_at IS NULL
      AND ms.starts_at <= timestamptz '2026-11-25 00:00+03'
      AND ms.expires_at >  timestamptz '2026-11-25 00:00+03';
    IF cnt <> 1 OR NOT promoted THEN
        RAISE EXCEPTION 'FAIL  active subscription lookup: count %, promoted %', cnt, promoted USING ERRCODE = 'ZT999';
    END IF;
    RAISE NOTICE 'PASS  subscriptions: active period found by dates, is_promoted = true';
END $$;

-- ---------- финансовые потоки не смешаны -------------------------------------
DO $$
DECLARE
    bad int;
BEGIN
    SELECT count(*) INTO bad
    FROM information_schema.columns
    WHERE table_schema = current_schema()
      AND ((table_name = 'payments' AND column_name IN ('client_id', 'master_id', 'idempotency_key', 'held_at', 'subscription_id'))
        OR (table_name = 'payouts'  AND column_name = 'master_id')
        OR (table_name = 'subscription_payments' AND column_name = 'booking_id')
        OR (table_name = 'bookings' AND column_name = 'platform_fee'));
    IF bad <> 0 THEN RAISE EXCEPTION 'FAIL  forbidden financial columns present: %', bad USING ERRCODE = 'ZT999'; END IF;
    RAISE NOTICE 'PASS  money flows separated: no forbidden columns';
END $$;

DO $$ BEGIN RAISE NOTICE '==== ALL CHECKS PASSED (rolling back) ===='; END $$;

ROLLBACK;

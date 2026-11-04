using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zrobim.Api.Data.Migrations
{
    public partial class BookingNotifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION notify_booking_change() RETURNS trigger
                LANGUAGE plpgsql
                SET search_path FROM CURRENT
                AS $$
                DECLARE
                    event_type text;
                    event_title text;
                    send_client boolean := true;
                    send_master boolean := false;
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'PENDING' THEN
                            RETURN NEW;
                        END IF;
                        event_type := 'BOOKING_REQUESTED';
                        event_title := 'Новая заявка';
                        send_client := false;
                        send_master := true;
                    ELSIF NEW.status = OLD.status THEN
                        RETURN NEW;
                    ELSIF OLD.status = 'PENDING' AND NEW.status = 'AWAITING_PAYMENT' THEN
                        event_type := 'BOOKING_ACCEPTED';
                        event_title := 'Мастер принял заявку';
                    ELSIF NEW.status = 'CANCELLED' THEN
                        CASE NEW.cancellation_reason
                            WHEN 'MASTER_DECLINED' THEN
                                event_type := 'BOOKING_DECLINED';
                                event_title := 'Мастер отклонил заявку';
                            WHEN 'CLIENT_CANCELLED' THEN
                                event_type := 'BOOKING_WITHDRAWN';
                                event_title := 'Клиент отозвал заявку';
                                send_client := false;
                                send_master := true;
                            WHEN 'MASTER_RESPONSE_TIMEOUT' THEN
                                event_type := 'BOOKING_RESPONSE_TIMEOUT';
                                event_title := 'Заявка отменена: мастер не ответил вовремя';
                                send_master := true;
                            WHEN 'PAYMENT_TIMEOUT' THEN
                                event_type := 'BOOKING_PAYMENT_TIMEOUT';
                                event_title := 'Заявка отменена: истёк срок ожидания оплаты';
                                send_master := true;
                            ELSE
                                RETURN NEW;
                        END CASE;
                    ELSE
                        RETURN NEW;
                    END IF;

                    INSERT INTO notifications (user_id, type, title, booking_id)
                    SELECT recipient.user_id, event_type, event_title, NEW.id
                    FROM (
                        SELECT NEW.client_id AS user_id WHERE send_client
                        UNION
                        SELECT m.user_id FROM masters m WHERE send_master AND m.id = NEW.master_id
                    ) AS recipient;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER notify_booking_change
                    AFTER INSERT OR UPDATE OF status ON bookings
                    FOR EACH ROW EXECUTE FUNCTION notify_booking_change();
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS notify_booking_change ON bookings;
                DROP FUNCTION IF EXISTS notify_booking_change();
                """);
        }
    }
}

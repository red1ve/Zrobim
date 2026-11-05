using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zrobim.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BasicSubscriptionPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO subscription_plans (id, code, name, price_monthly, is_promoted)
                VALUES ('9609acbd-6038-4e60-9a36-e08faf751801', 'BASIC', 'Подписка мастера', 15.00, false)
                ON CONFLICT (code) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Retain plans referenced by purchase history.
            migrationBuilder.Sql("""
                DELETE FROM subscription_plans p
                WHERE p.id = '9609acbd-6038-4e60-9a36-e08faf751801'
                  AND NOT EXISTS (SELECT 1 FROM subscription_payments sp WHERE sp.plan_id = p.id)
                  AND NOT EXISTS (SELECT 1 FROM master_subscriptions ms WHERE ms.plan_id = p.id);
                """);
        }
    }
}

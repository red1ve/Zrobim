using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Zrobim.Api.Bookings;
using Zrobim.Api.Catalog;
using Zrobim.Api.Disputes;
using Zrobim.Api.Masters;
using Zrobim.Api.Payments;
using Zrobim.Api.Payouts;
using Zrobim.Api.Subscriptions;
using Zrobim.Api.Users;

#nullable disable

namespace Zrobim.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:booking_status", "AWAITING_PAYMENT,CANCELLED,CLIENT_NO_SHOW,COMPLETED,CONFIRMED,DISPUTED,PENDING")
                .Annotation("Npgsql:Enum:deposit_outcome", "KEPT_AUTOMATICALLY,KEPT_BY_MASTER,WAIVED_BY_MASTER")
                .Annotation("Npgsql:Enum:dispute_reason", "MASTER_NO_SHOW,OTHER,POOR_QUALITY,SERVICE_NOT_PROVIDED")
                .Annotation("Npgsql:Enum:dispute_resolution", "DEPOSIT_TO_MASTER,REFUND_TO_CLIENT,RELEASE_TO_MASTER")
                .Annotation("Npgsql:Enum:dispute_status", "OPEN,RESOLVED")
                .Annotation("Npgsql:Enum:location_type", "CLIENT_PLACE,MASTER_PLACE")
                .Annotation("Npgsql:Enum:payment_status", "FAILED,HELD,PARTIALLY_REFUNDED,PENDING,REFUNDED,RELEASED")
                .Annotation("Npgsql:Enum:payout_status", "COMPLETED,FAILED,PENDING,PROCESSING")
                .Annotation("Npgsql:Enum:subscription_payment_status", "FAILED,PAID,PENDING,REFUNDED")
                .Annotation("Npgsql:Enum:subscription_plan_code", "BASIC,PLUS")
                .Annotation("Npgsql:Enum:user_role", "ADMIN,CLIENT,MASTER")
                .Annotation("Npgsql:Enum:verification_status", "APPROVED,PENDING,REJECTED")
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<SubscriptionPlanCode>(type: "subscription_plan_code", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    price_monthly = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    is_promoted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_plans", x => x.id);
                    table.CheckConstraint("subscription_plans_price_positive", "price_monthly > 0");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role = table.Column<UserRole>(type: "user_role", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    avatar_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "masters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    verification_status = table.Column<VerificationStatus>(type: "verification_status", nullable: false, defaultValue: VerificationStatus.Pending),
                    rating = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false, defaultValue: 0m),
                    reviews_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    payout_account = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_masters", x => x.id);
                    table.CheckConstraint("masters_coordinates_pair", "(latitude IS NULL) = (longitude IS NULL)");
                    table.CheckConstraint("masters_latitude_range", "latitude BETWEEN -90 AND 90");
                    table.CheckConstraint("masters_longitude_range", "longitude BETWEEN -180 AND 180");
                    table.CheckConstraint("masters_payout_account_not_blank", "payout_account IS NULL OR btrim(payout_account) <> ''");
                    table.CheckConstraint("masters_payout_account_not_card_number", "payout_account IS NULL OR payout_account !~ '^[0-9 -]{12,}$'");
                    table.CheckConstraint("masters_rating_range", "rating BETWEEN 0 AND 5");
                    table.CheckConstraint("masters_reviews_count_nonneg", "reviews_count >= 0");
                    table.ForeignKey(
                        name: "fk_masters_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    master_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_message_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversations", x => x.id);
                    table.ForeignKey(
                        name: "fk_conversations_masters_master_id",
                        column: x => x.master_id,
                        principalTable: "masters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_conversations_users_client_id",
                        column: x => x.client_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "favorites",
                columns: table => new
                {
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    master_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_favorites", x => new { x.client_id, x.master_id });
                    table.ForeignKey(
                        name: "fk_favorites_masters_master_id",
                        column: x => x.master_id,
                        principalTable: "masters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_favorites_users_client_id",
                        column: x => x.client_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "master_time_blocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    master_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_master_time_blocks", x => x.id);
                    table.CheckConstraint("master_time_blocks_time_order", "end_at > start_at");
                    table.ForeignKey(
                        name: "fk_master_time_blocks_masters_master_id",
                        column: x => x.master_id,
                        principalTable: "masters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "master_working_hours",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    master_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_master_working_hours", x => x.id);
                    table.CheckConstraint("master_working_hours_day_range", "day_of_week BETWEEN 1 AND 7");
                    table.CheckConstraint("master_working_hours_time_order", "end_time > start_time");
                    table.ForeignKey(
                        name: "fk_master_working_hours_masters_master_id",
                        column: x => x.master_id,
                        principalTable: "masters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "services",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    master_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    location_type = table.Column<LocationType>(type: "location_type", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_services", x => x.id);
                    table.UniqueConstraint("services_id_master_key", x => new { x.id, x.master_id });
                    table.CheckConstraint("services_duration_range", "duration_minutes > 0 AND duration_minutes <= 1440");
                    table.CheckConstraint("services_price_positive", "price > 0");
                    table.ForeignKey(
                        name: "fk_services_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_services_masters_master_id",
                        column: x => x.master_id,
                        principalTable: "masters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscription_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    master_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "BYN"),
                    status = table.Column<SubscriptionPaymentStatus>(type: "subscription_payment_status", nullable: false, defaultValue: SubscriptionPaymentStatus.Pending),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    external_payment_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_payments", x => x.id);
                    table.UniqueConstraint("subscription_payments_id_master_plan_key", x => new { x.id, x.master_id, x.plan_id });
                    table.CheckConstraint("subscription_payments_amount_positive", "amount > 0");
                    table.CheckConstraint("subscription_payments_currency_format", "currency ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("subscription_payments_paid_at_iff_received", "(status IN ('PAID', 'REFUNDED')) = (paid_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_subscription_payments_masters_master_id",
                        column: x => x.master_id,
                        principalTable: "masters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscription_payments_subscription_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: true),
                    attachment_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    attachment_content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    attachment_size_bytes = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messages", x => x.id);
                    table.CheckConstraint("messages_attachment_fields_together", "(attachment_key IS NULL) = (attachment_content_type IS NULL) AND (attachment_key IS NULL) = (attachment_size_bytes IS NULL)");
                    table.CheckConstraint("messages_attachment_is_image", "attachment_content_type IS NULL OR attachment_content_type IN ('image/jpeg', 'image/png', 'image/webp')");
                    table.CheckConstraint("messages_attachment_size_limit", "attachment_size_bytes IS NULL OR attachment_size_bytes BETWEEN 1 AND 10485760");
                    table.CheckConstraint("messages_not_empty", "(text IS NOT NULL AND btrim(text) <> '') OR attachment_key IS NOT NULL");
                    table.CheckConstraint("messages_text_length", "text IS NULL OR length(text) <= 4000");
                    table.ForeignKey(
                        name: "fk_messages_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_messages_users_sender_id",
                        column: x => x.sender_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    master_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    deposit_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    location_type = table.Column<LocationType>(type: "location_type", nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<BookingStatus>(type: "booking_status", nullable: false, defaultValue: BookingStatus.Pending),
                    client_comment = table.Column<string>(type: "text", nullable: true),
                    response_deadline_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payment_deadline_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deposit_outcome = table.Column<DepositOutcome>(type: "deposit_outcome", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookings", x => x.id);
                    table.UniqueConstraint("bookings_id_client_master_key", x => new { x.id, x.client_id, x.master_id });
                    table.CheckConstraint("bookings_accepted_for_later_statuses", "status IN ('PENDING', 'CANCELLED') OR accepted_at IS NOT NULL");
                    table.CheckConstraint("bookings_cancelled_at_iff_cancelled", "(status = 'CANCELLED') = (cancelled_at IS NOT NULL)");
                    table.CheckConstraint("bookings_cancelled_by_only_when_cancelled", "cancelled_by IS NULL OR status = 'CANCELLED'");
                    table.CheckConstraint("bookings_client_place_needs_address", "location_type <> 'CLIENT_PLACE' OR (address IS NOT NULL AND btrim(address) <> '')");
                    table.CheckConstraint("bookings_completed_has_timestamp", "status <> 'COMPLETED' OR completed_at IS NOT NULL");
                    table.CheckConstraint("bookings_deposit_outcome_only_on_client_fault", "deposit_outcome IS NULL OR status IN ('CANCELLED', 'CLIENT_NO_SHOW', 'DISPUTED')");
                    table.CheckConstraint("bookings_deposit_range", "deposit_amount > 0 AND deposit_amount <= price");
                    table.CheckConstraint("bookings_payment_deadline_iff_accepted", "(accepted_at IS NULL) = (payment_deadline_at IS NULL)");
                    table.CheckConstraint("bookings_price_positive", "price > 0");
                    table.CheckConstraint("bookings_response_deadline_before_start", "response_deadline_at <= start_at");
                    table.CheckConstraint("bookings_time_order", "end_at > start_at");
                    table.ForeignKey(
                        name: "bookings_service_master_fk",
                        columns: x => new { x.service_id, x.master_id },
                        principalTable: "services",
                        principalColumns: new[] { "id", "master_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_masters_master_id",
                        column: x => x.master_id,
                        principalTable: "masters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_users_cancelled_by",
                        column: x => x.cancelled_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_users_client_id",
                        column: x => x.client_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "master_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    master_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_master_subscriptions", x => x.id);
                    table.CheckConstraint("master_subscriptions_free_iff_no_payment", "(subscription_payment_id IS NULL) = (price = 0)");
                    table.CheckConstraint("master_subscriptions_price_nonneg", "price >= 0");
                    table.CheckConstraint("master_subscriptions_time_order", "expires_at > starts_at");
                    table.ForeignKey(
                        name: "fk_master_subscriptions_masters_master_id",
                        column: x => x.master_id,
                        principalTable: "masters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_master_subscriptions_subscription_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "master_subscriptions_payment_fk",
                        columns: x => new { x.subscription_payment_id, x.master_id, x.plan_id },
                        principalTable: "subscription_payments",
                        principalColumns: new[] { "id", "master_id", "plan_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "disputes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opened_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<DisputeReason>(type: "dispute_reason", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<DisputeStatus>(type: "dispute_status", nullable: false, defaultValue: DisputeStatus.Open),
                    resolution = table.Column<DisputeResolution>(type: "dispute_resolution", nullable: true),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_comment = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_disputes", x => x.id);
                    table.CheckConstraint("disputes_resolution_consistency", "(status = 'OPEN'     AND resolution IS NULL     AND resolved_by IS NULL     AND resolved_at IS NULL)\nOR (status = 'RESOLVED' AND resolution IS NOT NULL AND resolved_by IS NOT NULL AND resolved_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_disputes_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disputes_users_opened_by",
                        column: x => x.opened_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disputes_users_resolved_by",
                        column: x => x.resolved_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    message = table.Column<string>(type: "text", nullable: true),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "BYN"),
                    status = table.Column<PaymentStatus>(type: "payment_status", nullable: false, defaultValue: PaymentStatus.Pending),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    external_payment_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    release_after = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    released_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    refunded_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    released_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    refunded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.CheckConstraint("payments_amount_positive", "amount > 0");
                    table.CheckConstraint("payments_currency_format", "currency ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("payments_expires_after_created", "expires_at > created_at");
                    table.CheckConstraint("payments_paid_at_iff_received", "(status IN ('HELD', 'RELEASED', 'REFUNDED', 'PARTIALLY_REFUNDED')) = (paid_at IS NOT NULL)");
                    table.CheckConstraint("payments_refunded_at_iff_refunded", "(status IN ('REFUNDED', 'PARTIALLY_REFUNDED')) = (refunded_at IS NOT NULL)");
                    table.CheckConstraint("payments_release_after_only_when_received", "release_after IS NULL OR status IN ('HELD', 'RELEASED', 'PARTIALLY_REFUNDED')");
                    table.CheckConstraint("payments_released_at_iff_released", "(status IN ('RELEASED', 'PARTIALLY_REFUNDED')) = (released_at IS NOT NULL)");
                    table.CheckConstraint("payments_split_consistency", "(status IN ('PENDING', 'HELD', 'FAILED') AND released_amount = 0      AND refunded_amount = 0)\nOR (status = 'RELEASED'                  AND released_amount = amount AND refunded_amount = 0)\nOR (status = 'REFUNDED'                  AND released_amount = 0      AND refunded_amount = amount)\nOR (status = 'PARTIALLY_REFUNDED'        AND released_amount > 0      AND refunded_amount > 0\n                                         AND released_amount + refunded_amount = amount)");
                    table.ForeignKey(
                        name: "fk_payments_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    master_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reviews", x => x.id);
                    table.CheckConstraint("reviews_rating_range", "rating BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_reviews_masters_master_id",
                        column: x => x.master_id,
                        principalTable: "masters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reviews_users_client_id",
                        column: x => x.client_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "reviews_booking_participants_fk",
                        columns: x => new { x.booking_id, x.client_id, x.master_id },
                        principalTable: "bookings",
                        principalColumns: new[] { "id", "client_id", "master_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payouts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "BYN"),
                    status = table.Column<PayoutStatus>(type: "payout_status", nullable: false, defaultValue: PayoutStatus.Pending),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    external_payout_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    destination = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payouts", x => x.id);
                    table.CheckConstraint("payouts_amount_positive", "amount > 0");
                    table.CheckConstraint("payouts_completed_at_iff_completed", "(status = 'COMPLETED') = (completed_at IS NOT NULL)");
                    table.CheckConstraint("payouts_currency_format", "currency ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("payouts_destination_not_blank", "btrim(destination) <> ''");
                    table.CheckConstraint("payouts_destination_not_card_number", "destination !~ '^[0-9 -]{12,}$'");
                    table.ForeignKey(
                        name: "fk_payouts_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "bookings_awaiting_payment_idx",
                table: "bookings",
                column: "payment_deadline_at",
                filter: "status = 'AWAITING_PAYMENT'");

            migrationBuilder.CreateIndex(
                name: "bookings_client_start_idx",
                table: "bookings",
                columns: new[] { "client_id", "start_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "bookings_confirmed_end_idx",
                table: "bookings",
                column: "end_at",
                filter: "status = 'CONFIRMED'");

            migrationBuilder.CreateIndex(
                name: "bookings_master_start_idx",
                table: "bookings",
                columns: new[] { "master_id", "start_at" });

            migrationBuilder.CreateIndex(
                name: "bookings_pending_response_idx",
                table: "bookings",
                column: "response_deadline_at",
                filter: "status = 'PENDING'");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_cancelled_by",
                table: "bookings",
                column: "cancelled_by");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_service_id_master_id",
                table: "bookings",
                columns: new[] { "service_id", "master_id" });

            migrationBuilder.CreateIndex(
                name: "categories_name_key",
                table: "categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "conversations_client_last_idx",
                table: "conversations",
                columns: new[] { "client_id", "last_message_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "conversations_client_master_key",
                table: "conversations",
                columns: new[] { "client_id", "master_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "conversations_master_last_idx",
                table: "conversations",
                columns: new[] { "master_id", "last_message_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "disputes_booking_id_key",
                table: "disputes",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "disputes_open_created_idx",
                table: "disputes",
                column: "created_at",
                filter: "status = 'OPEN'");

            migrationBuilder.CreateIndex(
                name: "ix_disputes_opened_by",
                table: "disputes",
                column: "opened_by");

            migrationBuilder.CreateIndex(
                name: "ix_disputes_resolved_by",
                table: "disputes",
                column: "resolved_by");

            migrationBuilder.CreateIndex(
                name: "ix_favorites_master_id",
                table: "favorites",
                column: "master_id");

            migrationBuilder.CreateIndex(
                name: "ix_master_subscriptions_plan_id",
                table: "master_subscriptions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_master_subscriptions_subscription_payment_id_master_id_plan",
                table: "master_subscriptions",
                columns: new[] { "subscription_payment_id", "master_id", "plan_id" });

            migrationBuilder.CreateIndex(
                name: "master_subscriptions_master_expires_idx",
                table: "master_subscriptions",
                columns: new[] { "master_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "master_subscriptions_payment_key",
                table: "master_subscriptions",
                column: "subscription_payment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "master_time_blocks_master_start_idx",
                table: "master_time_blocks",
                columns: new[] { "master_id", "start_at" });

            migrationBuilder.CreateIndex(
                name: "master_working_hours_master_day_key",
                table: "master_working_hours",
                columns: new[] { "master_id", "day_of_week" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "masters_city_idx",
                table: "masters",
                column: "city");

            migrationBuilder.CreateIndex(
                name: "masters_user_id_key",
                table: "masters",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "masters_verification_status_idx",
                table: "masters",
                column: "verification_status");

            migrationBuilder.CreateIndex(
                name: "ix_messages_sender_id",
                table: "messages",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "messages_conversation_created_idx",
                table: "messages",
                columns: new[] { "conversation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_booking_id",
                table: "notifications",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "notifications_user_created_idx",
                table: "notifications",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "payments_auto_release_idx",
                table: "payments",
                column: "release_after",
                filter: "status = 'HELD' AND release_after IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "payments_booking_id_idx",
                table: "payments",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "payments_one_alive_per_booking",
                table: "payments",
                column: "booking_id",
                unique: true,
                filter: "status <> 'FAILED'");

            migrationBuilder.CreateIndex(
                name: "payments_pending_expires_idx",
                table: "payments",
                column: "expires_at",
                filter: "status = 'PENDING'");

            migrationBuilder.CreateIndex(
                name: "payments_provider_external_id_key",
                table: "payments",
                columns: new[] { "provider", "external_payment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "payouts_open_status_idx",
                table: "payouts",
                column: "status",
                filter: "status IN ('PENDING', 'PROCESSING', 'FAILED')");

            migrationBuilder.CreateIndex(
                name: "payouts_payment_id_key",
                table: "payouts",
                column: "payment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "payouts_provider_external_id_key",
                table: "payouts",
                columns: new[] { "provider", "external_payout_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reviews_booking_id_client_id_master_id",
                table: "reviews",
                columns: new[] { "booking_id", "client_id", "master_id" });

            migrationBuilder.CreateIndex(
                name: "ix_reviews_client_id",
                table: "reviews",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "reviews_booking_id_key",
                table: "reviews",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "reviews_master_created_idx",
                table: "reviews",
                columns: new[] { "master_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "services_category_active_idx",
                table: "services",
                column: "category_id",
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "services_master_id_idx",
                table: "services",
                column: "master_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_payments_plan_id",
                table: "subscription_payments",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "subscription_payments_master_created_idx",
                table: "subscription_payments",
                columns: new[] { "master_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "subscription_payments_provider_external_id_key",
                table: "subscription_payments",
                columns: new[] { "provider", "external_payment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "subscription_plans_code_key",
                table: "subscription_plans",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "users_phone_key",
                table: "users",
                column: "phone",
                unique: true);

            // ---- Objects EF Core cannot model: written as raw SQL -------------------------------

            // email is unique case-insensitively: Ivan@Mail.by and ivan@mail.by are one account
            migrationBuilder.Sql("CREATE UNIQUE INDEX users_email_lower_key ON users (lower(email));");

            // No double booking: a master cannot have two active bookings overlapping in time;
            // the slot is held for whoever requested it first.
            // Conflict → SQLSTATE 23P01 → the API answers 409 Conflict.
            migrationBuilder.Sql("""
                ALTER TABLE bookings ADD CONSTRAINT bookings_no_overlap EXCLUDE USING gist (
                    master_id WITH =,
                    tstzrange(start_at, end_at) WITH &&
                ) WHERE (status IN ('PENDING', 'AWAITING_PAYMENT', 'CONFIRMED'));
                """);

            // A master cannot have two active subscription periods at the same time.
            migrationBuilder.Sql("""
                ALTER TABLE master_subscriptions ADD CONSTRAINT master_subscriptions_no_overlap EXCLUDE USING gist (
                    master_id WITH =,
                    tstzrange(starts_at, expires_at) WITH &&
                ) WHERE (cancelled_at IS NULL);
                """);

            // Last line of defence: a payout only after the money was released to the master
            // (RELEASED — everything, PARTIALLY_REFUNDED — the deposit) and only for the released amount.
            migrationBuilder.Sql("""
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
                """);

            // A paid subscription period is granted only for a PAID subscription payment.
            migrationBuilder.Sql("""
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // raw-SQL objects first; the exclusion constraints and the email index go with their tables
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS master_subscriptions_require_paid_payment ON master_subscriptions;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS master_subscriptions_require_paid_payment();");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS payouts_require_released_payment ON payouts;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS payouts_require_released_payment();");

            migrationBuilder.DropTable(
                name: "disputes");

            migrationBuilder.DropTable(
                name: "favorites");

            migrationBuilder.DropTable(
                name: "master_subscriptions");

            migrationBuilder.DropTable(
                name: "master_time_blocks");

            migrationBuilder.DropTable(
                name: "master_working_hours");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "payouts");

            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropTable(
                name: "subscription_payments");

            migrationBuilder.DropTable(
                name: "conversations");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "subscription_plans");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "services");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "masters");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}

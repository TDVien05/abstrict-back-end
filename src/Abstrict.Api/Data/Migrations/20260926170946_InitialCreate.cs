using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abstrict.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    before_json = table.Column<string>(type: "text", nullable: true),
                    after_json = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "promotion_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    audience = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    duration_days = table.Column<int>(type: "integer", nullable: false),
                    price_vnd = table.Column<long>(type: "bigint", nullable: false),
                    priority_radius_km = table.Column<int>(type: "integer", nullable: true),
                    worker_limit = table.Column<int>(type: "integer", nullable: true),
                    benefits_json = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotion_plans", x => x.id);
                    table.CheckConstraint("ck_promotion_plan_values", "duration_days > 0 AND price_vnd >= 0");
                });

            migrationBuilder.CreateTable(
                name: "providers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    approval_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_accepting_bookings = table.Column<bool>(type: "boolean", nullable: false),
                    avatar_object_key = table.Column<string>(type: "text", nullable: true),
                    bio = table.Column<string>(type: "text", nullable: true),
                    average_rating = table.Column<double>(type: "double precision", nullable: false),
                    rating_count = table.Column<int>(type: "integer", nullable: false),
                    completed_booking_count = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_providers", x => x.id);
                    table.CheckConstraint("ck_provider_rating", "average_rating >= 0 AND average_rating <= 5");
                });

            migrationBuilder.CreateTable(
                name: "service_areas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    city = table.Column<string>(type: "text", nullable: false),
                    district = table.Column<string>(type: "text", nullable: false),
                    ward_or_complex = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_areas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    email_verified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    phone_verified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "availability_windows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_availability_windows", x => x.id);
                    table.CheckConstraint("ck_availability_range", "ends_at_utc > starts_at_utc");
                    table.ForeignKey(
                        name: "FK_availability_windows_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bank_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_code = table.Column<string>(type: "text", nullable: false),
                    account_number_encrypted = table.Column<string>(type: "text", nullable: false),
                    account_holder_name = table.Column<string>(type: "text", nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_bank_accounts_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provider_approval_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_admin_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_by_admin_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    review_started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_reason = table.Column<string>(type: "text", nullable: true),
                    requested_changes_json = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_approval_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_provider_approval_reviews_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provider_assessments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    maximum_score = table.Column<int>(type: "integer", nullable: false),
                    passing_score = table.Column<int>(type: "integer", nullable: false),
                    passed = table.Column<bool>(type: "boolean", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    result_json = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_assessments", x => x.id);
                    table.CheckConstraint("ck_provider_assessment_score", "score >= 0 AND maximum_score > 0 AND score <= maximum_score AND passing_score >= 0 AND passing_score <= maximum_score");
                    table.ForeignKey(
                        name: "FK_provider_assessments_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provider_metrics_daily",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric_date = table.Column<DateOnly>(type: "date", nullable: false),
                    impression_count = table.Column<int>(type: "integer", nullable: false),
                    profile_view_count = table.Column<int>(type: "integer", nullable: false),
                    booking_conversion_count = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_metrics_daily", x => x.id);
                    table.ForeignKey(
                        name: "FK_provider_metrics_daily_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_wallets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_wallets", x => x.id);
                    table.ForeignKey(
                        name: "FK_provider_wallets_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "verification_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    object_key = table.Column<string>(type: "text", nullable: false),
                    original_file_name = table.Column<string>(type: "text", nullable: false),
                    issuer = table.Column<string>(type: "text", nullable: true),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    extracted_data_json = table.Column<string>(type: "text", nullable: true),
                    verified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verified_by_admin_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verification_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_verification_documents_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provider_service_areas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_area_id = table.Column<Guid>(type: "uuid", nullable: false),
                    travel_radius_km = table.Column<int>(type: "integer", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_service_areas", x => x.id);
                    table.ForeignKey(
                        name: "FK_provider_service_areas_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_provider_service_areas_service_areas_service_area_id",
                        column: x => x.service_area_id,
                        principalTable: "service_areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "checklist_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checklist_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_checklist_templates_service_categories_service_category_id",
                        column: x => x.service_category_id,
                        principalTable: "service_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_services",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hourly_rate_vnd = table.Column<long>(type: "bigint", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_services", x => x.id);
                    table.CheckConstraint("ck_provider_service_rate", "hourly_rate_vnd >= 0");
                    table.ForeignKey(
                        name: "FK_provider_services_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_provider_services_service_categories_service_category_id",
                        column: x => x.service_category_id,
                        principalTable: "service_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "company_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_name = table.Column<string>(type: "text", nullable: false),
                    tax_code = table.Column<string>(type: "text", nullable: false),
                    hotline = table.Column<string>(type: "text", nullable: false),
                    headquarters_address = table.Column<string>(type: "text", nullable: false),
                    workforce_size = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    liability_coverage_vnd = table.Column<long>(type: "bigint", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_company_profiles_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_profiles_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_addresses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    apartment_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    floor = table.Column<string>(type: "text", nullable: true),
                    building_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    street_address = table.Column<string>(type: "text", nullable: false),
                    ward = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    district = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    city = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    access_instructions = table.Column<string>(type: "text", nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_addresses", x => x.id);
                    table.ForeignKey(
                        name: "FK_customer_addresses_users_customer_user_id",
                        column: x => x.customer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    avatar_object_key = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_customer_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "freelancer_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_full_name = table.Column<string>(type: "text", nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    gender = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    permanent_address = table.Column<string>(type: "text", nullable: false),
                    current_address = table.Column<string>(type: "text", nullable: false),
                    experience_years = table.Column<int>(type: "integer", nullable: false),
                    auto_accept_bookings = table.Column<bool>(type: "boolean", nullable: false),
                    face_match_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_freelancer_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_freelancer_profiles_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_freelancer_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    data_json = table.Column<string>(type: "text", nullable: true),
                    read_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "checklist_template_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    checklist_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    area_name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checklist_template_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_checklist_template_items_checklist_template_items_parent_it~",
                        column: x => x.parent_item_id,
                        principalTable: "checklist_template_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_checklist_template_items_checklist_templates_checklist_temp~",
                        column: x => x.checklist_template_id,
                        principalTable: "checklist_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "company_representatives",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    job_title = table.Column<string>(type: "text", nullable: false),
                    work_email = table.Column<string>(type: "text", nullable: false),
                    phone_number = table.Column<string>(type: "text", nullable: false),
                    identity_number_encrypted = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_representatives", x => x.id);
                    table.ForeignKey(
                        name: "FK_company_representatives_company_profiles_company_profile_id",
                        column: x => x.company_profile_id,
                        principalTable: "company_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "company_workers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    phone_number = table.Column<string>(type: "text", nullable: false),
                    employee_code = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_workers", x => x.id);
                    table.ForeignKey(
                        name: "FK_company_workers_company_profiles_company_profile_id",
                        column: x => x.company_profile_id,
                        principalTable: "company_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_number = table.Column<string>(type: "text", nullable: false),
                    customer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_address_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    starts_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    hold_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    provider_confirmation_deadline_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    checked_in_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    unit_price_vnd = table.Column<long>(type: "bigint", nullable: false),
                    base_amount_vnd = table.Column<long>(type: "bigint", nullable: false),
                    surcharge_amount_vnd = table.Column<long>(type: "bigint", nullable: false),
                    deposit_amount_vnd = table.Column<long>(type: "bigint", nullable: false),
                    extension_amount_vnd = table.Column<long>(type: "bigint", nullable: false),
                    address_snapshot = table.Column<string>(type: "text", nullable: false),
                    customer_note = table.Column<string>(type: "text", nullable: true),
                    cancelled_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.id);
                    table.CheckConstraint("ck_booking_amounts", "unit_price_vnd >= 0 AND base_amount_vnd >= 0 AND surcharge_amount_vnd >= 0 AND deposit_amount_vnd >= 0 AND extension_amount_vnd >= 0");
                    table.CheckConstraint("ck_booking_range", "ends_at_utc > starts_at_utc");
                    table.ForeignKey(
                        name: "FK_bookings_customer_addresses_customer_address_id",
                        column: x => x.customer_address_id,
                        principalTable: "customer_addresses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_bookings_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bookings_service_categories_service_category_id",
                        column: x => x.service_category_id,
                        principalTable: "service_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bookings_users_customer_user_id",
                        column: x => x.customer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booking_extensions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    additional_minutes = table.Column<int>(type: "integer", nullable: false),
                    additional_amount_vnd = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    customer_accepted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    provider_accepted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_extensions", x => x.id);
                    table.CheckConstraint("ck_extension_values", "additional_minutes > 0 AND additional_amount_vnd >= 0");
                    table.ForeignKey(
                        name: "FK_booking_extensions_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    object_key = table.Column<string>(type: "text", nullable: false),
                    caption = table.Column<string>(type: "text", nullable: true),
                    taken_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_photos", x => x.id);
                    table.ForeignKey(
                        name: "FK_booking_photos_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    to_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_booking_status_history_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    area_name_snapshot = table.Column<string>(type: "text", nullable: false),
                    description_snapshot = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_tasks", x => x.id);
                    table.ForeignKey(
                        name: "FK_booking_tasks_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "check_in_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    redeemed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    redeemed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    redeemed_by_company_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_check_in_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_check_in_tokens_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "company_worker_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_worker_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_company_worker_assignments_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_worker_assignments_company_workers_company_worker_id",
                        column: x => x.company_worker_id,
                        principalTable: "company_workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "disputes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opened_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    response_deadline_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    appeal_deadline_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    appeal_used = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disputes", x => x.id);
                    table.ForeignKey(
                        name: "FK_disputes_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    method = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount_vnd = table.Column<long>(type: "bigint", nullable: false),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    provider_transaction_id = table.Column<string>(type: "text", nullable: true),
                    provider_name = table.Column<string>(type: "text", nullable: true),
                    paid_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    refunded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.CheckConstraint("ck_payment_amount", "amount_vnd > 0");
                    table.ForeignKey(
                        name: "FK_payments_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_users_payer_user_id",
                        column: x => x.payer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ratings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    subject_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_provider_id = table.Column<Guid>(type: "uuid", nullable: true),
                    score = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    publish_after_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ratings", x => x.id);
                    table.CheckConstraint("ck_rating_score", "score >= 1 AND score <= 5");
                    table.ForeignKey(
                        name: "FK_ratings_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispute_decisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_id = table.Column<Guid>(type: "uuid", nullable: false),
                    admin_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    outcome_code = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    refund_amount_vnd = table.Column<long>(type: "bigint", nullable: true),
                    provider_amount_vnd = table.Column<long>(type: "bigint", nullable: true),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispute_decisions", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispute_decisions_disputes_dispute_id",
                        column: x => x.dispute_id,
                        principalTable: "disputes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dispute_evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_key = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_new_appeal_evidence = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispute_evidence", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispute_evidence_disputes_dispute_id",
                        column: x => x.dispute_id,
                        principalTable: "disputes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dispute_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    is_internal_admin_note = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispute_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispute_messages_disputes_dispute_id",
                        column: x => x.dispute_id,
                        principalTable: "disputes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "money_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    beneficiary_provider_id = table.Column<Guid>(type: "uuid", nullable: true),
                    provider_wallet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    amount_vnd = table.Column<long>(type: "bigint", nullable: false),
                    reference = table.Column<string>(type: "text", nullable: false),
                    posted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_money_movements", x => x.id);
                    table.CheckConstraint("ck_money_movement_amount", "amount_vnd > 0");
                    table.ForeignKey(
                        name: "FK_money_movements_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_money_movements_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_money_movements_provider_wallets_provider_wallet_id",
                        column: x => x.provider_wallet_id,
                        principalTable: "provider_wallets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_money_movements_providers_beneficiary_provider_id",
                        column: x => x.beneficiary_provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "promotion_purchases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    promotion_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    price_snapshot_vnd = table.Column<long>(type: "bigint", nullable: false),
                    starts_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ends_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotion_purchases", x => x.id);
                    table.ForeignKey(
                        name: "FK_promotion_purchases_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_promotion_purchases_promotion_plans_promotion_plan_id",
                        column: x => x.promotion_plan_id,
                        principalTable: "promotion_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_promotion_purchases_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_actor_user_id_created_at_utc",
                table: "audit_logs",
                columns: new[] { "actor_user_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_entity_type_entity_id_created_at_utc",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_availability_windows_provider_id_starts_at_utc_ends_at_utc",
                table: "availability_windows",
                columns: new[] { "provider_id", "starts_at_utc", "ends_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_accounts_provider_id_is_default",
                table: "bank_accounts",
                columns: new[] { "provider_id", "is_default" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_extensions_booking_id",
                table: "booking_extensions",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_photos_booking_id",
                table: "booking_photos",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_status_history_booking_id",
                table: "booking_status_history",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_tasks_booking_id",
                table: "booking_tasks",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_booking_number",
                table: "bookings",
                column: "booking_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_customer_address_id",
                table: "bookings",
                column: "customer_address_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_customer_user_id",
                table: "bookings",
                column: "customer_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_provider_id_starts_at_utc_ends_at_utc",
                table: "bookings",
                columns: new[] { "provider_id", "starts_at_utc", "ends_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_service_category_id",
                table: "bookings",
                column: "service_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_check_in_tokens_booking_id_expires_at_utc",
                table: "check_in_tokens",
                columns: new[] { "booking_id", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_check_in_tokens_token_hash",
                table: "check_in_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_checklist_template_items_checklist_template_id",
                table: "checklist_template_items",
                column: "checklist_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_checklist_template_items_parent_item_id",
                table: "checklist_template_items",
                column: "parent_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_checklist_templates_service_category_id_version",
                table: "checklist_templates",
                columns: new[] { "service_category_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_profiles_owner_user_id",
                table: "company_profiles",
                column: "owner_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_profiles_provider_id",
                table: "company_profiles",
                column: "provider_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_profiles_tax_code",
                table: "company_profiles",
                column: "tax_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_representatives_company_profile_id",
                table: "company_representatives",
                column: "company_profile_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_worker_assignments_booking_id_company_worker_id_ass~",
                table: "company_worker_assignments",
                columns: new[] { "booking_id", "company_worker_id", "assigned_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_company_worker_assignments_company_worker_id",
                table: "company_worker_assignments",
                column: "company_worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_workers_company_profile_id_employee_code",
                table: "company_workers",
                columns: new[] { "company_profile_id", "employee_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_addresses_customer_user_id",
                table: "customer_addresses",
                column: "customer_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_profiles_user_id",
                table: "customer_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dispute_decisions_dispute_id_type",
                table: "dispute_decisions",
                columns: new[] { "dispute_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dispute_evidence_dispute_id",
                table: "dispute_evidence",
                column: "dispute_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispute_messages_dispute_id_created_at_utc",
                table: "dispute_messages",
                columns: new[] { "dispute_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_disputes_booking_id_status",
                table: "disputes",
                columns: new[] { "booking_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_freelancer_profiles_provider_id",
                table: "freelancer_profiles",
                column: "provider_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_freelancer_profiles_user_id",
                table: "freelancer_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_money_movements_beneficiary_provider_id",
                table: "money_movements",
                column: "beneficiary_provider_id");

            migrationBuilder.CreateIndex(
                name: "IX_money_movements_booking_id",
                table: "money_movements",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_money_movements_payment_id",
                table: "money_movements",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_money_movements_provider_wallet_id",
                table: "money_movements",
                column: "provider_wallet_id");

            migrationBuilder.CreateIndex(
                name: "IX_money_movements_reference",
                table: "money_movements",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id_read_at_utc",
                table: "notifications",
                columns: new[] { "user_id", "read_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_booking_id",
                table: "payments",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_idempotency_key",
                table: "payments",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_payer_user_id",
                table: "payments",
                column: "payer_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_provider_name_provider_transaction_id",
                table: "payments",
                columns: new[] { "provider_name", "provider_transaction_id" });

            migrationBuilder.CreateIndex(
                name: "IX_promotion_plans_code",
                table: "promotion_plans",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_promotion_purchases_payment_id",
                table: "promotion_purchases",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_promotion_purchases_promotion_plan_id",
                table: "promotion_purchases",
                column: "promotion_plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_promotion_purchases_provider_id_status_ends_at_utc",
                table: "promotion_purchases",
                columns: new[] { "provider_id", "status", "ends_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_provider_approval_reviews_assigned_admin_user_id_decision",
                table: "provider_approval_reviews",
                columns: new[] { "assigned_admin_user_id", "decision" });

            migrationBuilder.CreateIndex(
                name: "IX_provider_approval_reviews_provider_id_decision_submitted_at~",
                table: "provider_approval_reviews",
                columns: new[] { "provider_id", "decision", "submitted_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_provider_assessments_provider_id_type_completed_at_utc",
                table: "provider_assessments",
                columns: new[] { "provider_id", "type", "completed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_provider_metrics_daily_provider_id_metric_date",
                table: "provider_metrics_daily",
                columns: new[] { "provider_id", "metric_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provider_service_areas_provider_id_service_area_id",
                table: "provider_service_areas",
                columns: new[] { "provider_id", "service_area_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provider_service_areas_service_area_id",
                table: "provider_service_areas",
                column: "service_area_id");

            migrationBuilder.CreateIndex(
                name: "IX_provider_services_provider_id_service_category_id",
                table: "provider_services",
                columns: new[] { "provider_id", "service_category_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provider_services_service_category_id",
                table: "provider_services",
                column: "service_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_provider_wallets_provider_id_type",
                table: "provider_wallets",
                columns: new[] { "provider_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_providers_type_approval_status_is_accepting_bookings",
                table: "providers",
                columns: new[] { "type", "approval_status", "is_accepting_bookings" });

            migrationBuilder.CreateIndex(
                name: "IX_ratings_booking_id_reviewer_user_id",
                table: "ratings",
                columns: new[] { "booking_id", "reviewer_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_areas_city_district_ward_or_complex",
                table: "service_areas",
                columns: new[] { "city", "district", "ward_or_complex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_categories_code",
                table: "service_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_phone_number",
                table: "users",
                column: "phone_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_verification_documents_provider_id_type",
                table: "verification_documents",
                columns: new[] { "provider_id", "type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "availability_windows");

            migrationBuilder.DropTable(
                name: "bank_accounts");

            migrationBuilder.DropTable(
                name: "booking_extensions");

            migrationBuilder.DropTable(
                name: "booking_photos");

            migrationBuilder.DropTable(
                name: "booking_status_history");

            migrationBuilder.DropTable(
                name: "booking_tasks");

            migrationBuilder.DropTable(
                name: "check_in_tokens");

            migrationBuilder.DropTable(
                name: "checklist_template_items");

            migrationBuilder.DropTable(
                name: "company_representatives");

            migrationBuilder.DropTable(
                name: "company_worker_assignments");

            migrationBuilder.DropTable(
                name: "customer_profiles");

            migrationBuilder.DropTable(
                name: "dispute_decisions");

            migrationBuilder.DropTable(
                name: "dispute_evidence");

            migrationBuilder.DropTable(
                name: "dispute_messages");

            migrationBuilder.DropTable(
                name: "freelancer_profiles");

            migrationBuilder.DropTable(
                name: "money_movements");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "promotion_purchases");

            migrationBuilder.DropTable(
                name: "provider_approval_reviews");

            migrationBuilder.DropTable(
                name: "provider_assessments");

            migrationBuilder.DropTable(
                name: "provider_metrics_daily");

            migrationBuilder.DropTable(
                name: "provider_service_areas");

            migrationBuilder.DropTable(
                name: "provider_services");

            migrationBuilder.DropTable(
                name: "ratings");

            migrationBuilder.DropTable(
                name: "verification_documents");

            migrationBuilder.DropTable(
                name: "checklist_templates");

            migrationBuilder.DropTable(
                name: "company_workers");

            migrationBuilder.DropTable(
                name: "disputes");

            migrationBuilder.DropTable(
                name: "provider_wallets");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "promotion_plans");

            migrationBuilder.DropTable(
                name: "service_areas");

            migrationBuilder.DropTable(
                name: "company_profiles");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "customer_addresses");

            migrationBuilder.DropTable(
                name: "providers");

            migrationBuilder.DropTable(
                name: "service_categories");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}

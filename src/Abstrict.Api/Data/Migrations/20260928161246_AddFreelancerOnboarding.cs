using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abstrict.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFreelancerOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "original_file_name",
                table: "verification_documents",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "object_key",
                table: "verification_documents",
                type: "character varying(400)",
                maxLength: 400,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "application_id",
                table: "verification_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "byte_size",
                table: "verification_documents",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "content_hash",
                table: "verification_documents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "content_type",
                table: "verification_documents",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "revision",
                table: "verification_documents",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "scan_status",
                table: "verification_documents",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "superseded_at_utc",
                table: "verification_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "freelancer_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    current_step = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    legal_full_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    gender = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    permanent_address = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    current_address = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    experience_years = table.Column<int>(type: "integer", nullable: true),
                    service_category_ids_json = table.Column<string>(type: "text", nullable: true),
                    service_area_ids_json = table.Column<string>(type: "text", nullable: true),
                    bank_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    bank_account_number_encrypted = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    bank_account_number_last4 = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    bank_account_holder_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    bank_account_holder_name_overridden = table.Column<bool>(type: "boolean", nullable: false),
                    confirmed_identity_full_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    confirmed_identity_document_number_last4 = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submitted_version = table.Column<int>(type: "integer", nullable: true),
                    last_decision_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_freelancer_applications", x => x.id);
                    table.CheckConstraint("ck_freelancer_application_experience", "experience_years IS NULL OR experience_years >= 0");
                    table.ForeignKey(
                        name: "FK_freelancer_applications_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_freelancer_applications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kyc_consents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    consent_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content_version = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    accepted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    withdrawn_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kyc_consents", x => x.id);
                    table.ForeignKey(
                        name: "FK_kyc_consents_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "application_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    decision_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    requested_changes_json = table.Column<string>(type: "text", nullable: true),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    review_started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_admin_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_submissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_application_submissions_freelancer_applications_application~",
                        column: x => x.application_id,
                        principalTable: "freelancer_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identity_claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    reserved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    released_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_claims", x => x.id);
                    table.ForeignKey(
                        name: "FK_identity_claims_freelancer_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "freelancer_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identity_verification_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_version = table.Column<int>(type: "integer", nullable: false),
                    front_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    back_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    selfie_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ocr_state = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    face_state = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    liveness_state = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    document_number_encrypted = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    document_number_last4 = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    document_number_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    full_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    gender = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    permanent_address = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: true),
                    issued_place = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    document_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    field_confidences_json = table.Column<string>(type: "text", nullable: true),
                    corrections_json = table.Column<string>(type: "text", nullable: true),
                    face_score = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    face_threshold = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    result_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ocr_provider_request_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    face_provider_request_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    policy_version = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ocr_completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    face_completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    superseded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_verification_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_identity_verification_attempts_freelancer_applications_appl~",
                        column: x => x.application_id,
                        principalTable: "freelancer_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kyc_operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    state = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    input_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    result_json = table.Column<string>(type: "text", nullable: true),
                    provider_request_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kyc_operations", x => x.id);
                    table.ForeignKey(
                        name: "FK_kyc_operations_freelancer_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "freelancer_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_verification_documents_application_id_type_superseded_at_utc",
                table: "verification_documents",
                columns: new[] { "application_id", "type", "superseded_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_application_submissions_application_id_version",
                table: "application_submissions",
                columns: new[] { "application_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_application_submissions_status_submitted_at_utc",
                table: "application_submissions",
                columns: new[] { "status", "submitted_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_freelancer_applications_provider_id",
                table: "freelancer_applications",
                column: "provider_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_freelancer_applications_user_id",
                table: "freelancer_applications",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_identity_claims_application_id_status",
                table: "identity_claims",
                columns: new[] { "application_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_identity_claims_fingerprint",
                table: "identity_claims",
                column: "fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_identity_verification_attempts_application_id_superseded_at~",
                table: "identity_verification_attempts",
                columns: new[] { "application_id", "superseded_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_kyc_consents_user_id_consent_type_content_version",
                table: "kyc_consents",
                columns: new[] { "user_id", "consent_type", "content_version" });

            migrationBuilder.CreateIndex(
                name: "IX_kyc_operations_application_id",
                table: "kyc_operations",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_kyc_operations_state_next_attempt_at_utc_lease_expires_at_u~",
                table: "kyc_operations",
                columns: new[] { "state", "next_attempt_at_utc", "lease_expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_kyc_operations_user_id_type_idempotency_key",
                table: "kyc_operations",
                columns: new[] { "user_id", "type", "idempotency_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_submissions");

            migrationBuilder.DropTable(
                name: "identity_claims");

            migrationBuilder.DropTable(
                name: "identity_verification_attempts");

            migrationBuilder.DropTable(
                name: "kyc_consents");

            migrationBuilder.DropTable(
                name: "kyc_operations");

            migrationBuilder.DropTable(
                name: "freelancer_applications");

            migrationBuilder.DropIndex(
                name: "IX_verification_documents_application_id_type_superseded_at_utc",
                table: "verification_documents");

            migrationBuilder.DropColumn(
                name: "application_id",
                table: "verification_documents");

            migrationBuilder.DropColumn(
                name: "byte_size",
                table: "verification_documents");

            migrationBuilder.DropColumn(
                name: "content_hash",
                table: "verification_documents");

            migrationBuilder.DropColumn(
                name: "content_type",
                table: "verification_documents");

            migrationBuilder.DropColumn(
                name: "revision",
                table: "verification_documents");

            migrationBuilder.DropColumn(
                name: "scan_status",
                table: "verification_documents");

            migrationBuilder.DropColumn(
                name: "superseded_at_utc",
                table: "verification_documents");

            migrationBuilder.AlterColumn<string>(
                name: "original_file_name",
                table: "verification_documents",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "object_key",
                table: "verification_documents",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(400)",
                oldMaxLength: 400);
        }
    }
}

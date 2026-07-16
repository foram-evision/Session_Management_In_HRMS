using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Migrations
{
    public partial class InitialDbSetupWithRoutines : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- CORE TABLES SCAN HERE (If EF generated them) ---

            // --- YOUR CUSTOM ROUTINES ---
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION calculate_service_years(join_date DATE)
                RETURNS INT AS $$
                BEGIN
                    RETURN EXTRACT(YEAR FROM AGE(NOW(), join_date));
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE PROCEDURE log_auth_audit(
                    p_user_id TEXT, 
                    p_action TEXT, 
                    p_details TEXT
                )
                AS $$
                BEGIN
                    INSERT INTO ""AuthAuditLogs"" (""UserId"", ""Action"", ""Details"", ""Timestamp"")
                    VALUES (p_user_id, p_action, p_details, NOW());
                END;
                $$ LANGUAGE plpgsql;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS log_auth_audit(TEXT, TEXT, TEXT);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS calculate_service_years(DATE);");
        }
    }
}
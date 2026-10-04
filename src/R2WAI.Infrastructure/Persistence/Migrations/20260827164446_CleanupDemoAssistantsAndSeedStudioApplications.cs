using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Data-only migration (no schema change) for the R2WAI Studio Assistant data reset.
    ///
    /// On a brand-new database this migration is a no-op: the guarded INSERTs require the default
    /// Tenant/Department to already exist, which they don't yet at migration time (ApplicationDbContextSeed
    /// runs after migrations and creates the fresh 5 applications/assistants itself — see
    /// ApplicationDbContextSeed.SeedAsync).
    ///
    /// On an existing database (Tenants already populated, so ApplicationDbContextSeed is a no-op and
    /// never recreates the old demo data), this migration:
    ///   1. Soft-deletes the 11 legacy demo AssistantDefinitions seeded by the pre-reset
    ///      ApplicationDbContextSeed (fixed GUIDs ...0101-...0111), which the global IsDeleted query
    ///      filter then hides from every API/UI list without touching FK-dependent rows (prompt
    ///      history/test cases cascade at the DB level; chatbots/test runs SetNull).
    ///   2. Soft-deletes the coaching-center demo cluster (KnowledgeBase + Source + Chatbot) that
    ///      existed solely to back the removed Coaching assistant.
    ///   3. Inserts the 5 fresh Draft applications + assistants (Employee/Supplier/Sampark/Niryaat/
    ///      Enterprise), idempotently — safe to apply more than once.
    /// </summary>
    /// <inheritdoc />
    public partial class CleanupDemoAssistantsAndSeedStudioApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "AssistantDefinitions"
                SET "IsDeleted" = true, "ModifiedAt" = now()
                WHERE "Id" IN (
                    '00000000-0000-0000-0000-000000000101',
                    '00000000-0000-0000-0000-000000000102',
                    '00000000-0000-0000-0000-000000000103',
                    '00000000-0000-0000-0000-000000000104',
                    '00000000-0000-0000-0000-000000000105',
                    '00000000-0000-0000-0000-000000000106',
                    '00000000-0000-0000-0000-000000000107',
                    '00000000-0000-0000-0000-000000000108',
                    '00000000-0000-0000-0000-000000000109',
                    '00000000-0000-0000-0000-000000000110',
                    '00000000-0000-0000-0000-000000000111'
                ) AND "IsDeleted" = false;

                UPDATE "KnowledgeBaseSources" SET "IsDeleted" = true, "ModifiedAt" = now()
                WHERE "Id" = '00000000-0000-0000-0000-000000000502' AND "IsDeleted" = false;

                UPDATE "KnowledgeBases" SET "IsDeleted" = true, "ModifiedAt" = now()
                WHERE "Id" = '00000000-0000-0000-0000-000000000501' AND "IsDeleted" = false;

                UPDATE "Chatbots" SET "IsDeleted" = true, "ModifiedAt" = now()
                WHERE "Id" = '00000000-0000-0000-0000-000000000401' AND "IsDeleted" = false;

                INSERT INTO "Applications"
                    ("Id","TenantId","DepartmentId","Name","Code","Description","BaseUrl","Environment","Status","CreatedAt","ModifiedAt","IsDeleted")
                SELECT v."Id", v."TenantId", v."DepartmentId", v."Name", v."Code", v."Description", NULL, 'Development', 'Draft', now(), NULL, false
                FROM (VALUES
                    ('00000000-0000-0000-0000-000000000801'::uuid, '00000000-0000-0000-0000-000000000001'::uuid, '00000000-0000-0000-0000-000000000601'::uuid, 'Employee Portal', 'EMP-PORTAL', 'Employee self-service portal.'),
                    ('00000000-0000-0000-0000-000000000802'::uuid, '00000000-0000-0000-0000-000000000001'::uuid, '00000000-0000-0000-0000-000000000601'::uuid, 'Supplier Portal', 'SUP-PORTAL', 'Supplier-facing services and business processes.'),
                    ('00000000-0000-0000-0000-000000000803'::uuid, '00000000-0000-0000-0000-000000000001'::uuid, '00000000-0000-0000-0000-000000000601'::uuid, 'Sampark Portal', 'SAMPARK-PORTAL', 'Sampark Portal services.'),
                    ('00000000-0000-0000-0000-000000000804'::uuid, '00000000-0000-0000-0000-000000000001'::uuid, '00000000-0000-0000-0000-000000000601'::uuid, 'Niryaat Portal', 'NIRYAAT-PORTAL', 'Niryaat Portal services.'),
                    ('00000000-0000-0000-0000-000000000805'::uuid, '00000000-0000-0000-0000-000000000001'::uuid, '00000000-0000-0000-0000-000000000601'::uuid, 'Enterprise / Cross Application', 'ENTERPRISE', 'Future cross-application enterprise scope.')
                ) AS v("Id","TenantId","DepartmentId","Name","Code","Description")
                WHERE EXISTS (SELECT 1 FROM "Tenants" WHERE "Id" = v."TenantId")
                  AND EXISTS (SELECT 1 FROM "Departments" WHERE "Id" = v."DepartmentId")
                  AND NOT EXISTS (SELECT 1 FROM "Applications" a WHERE a."Id" = v."Id");

                INSERT INTO "AssistantDefinitions"
                    ("Id","TenantId","ApplicationId","Name","Description","Type","SystemPrompt","ModelConfigurationId","KnowledgeBaseId","Tools","Settings","IsActive","PublishStatus","PublishedVersion","PublishedAt","Tags","AvatarUrl","UsageCount","CreatedAt","ModifiedAt","IsDeleted")
                SELECT v."Id", v."TenantId", v."ApplicationId", v."Name", v."Description", 'General', v."SystemPrompt", '00000000-0000-0000-0000-000000000001'::uuid, NULL, NULL, NULL, false, 'Draft', 0, NULL, NULL, NULL, 0, now(), NULL, false
                FROM (VALUES
                    ('00000000-0000-0000-0000-000000000901'::uuid, '00000000-0000-0000-0000-000000000001'::uuid, '00000000-0000-0000-0000-000000000801'::uuid, 'Employee Assistant', 'Employee self-service and intelligent access to Employee Portal capabilities.', 'You are the Employee Assistant for the Employee Portal. Help employees with self-service tasks and questions. Only reference capabilities that have been explicitly configured for you — do not claim to perform actions you cannot verify.'),
                    ('00000000-0000-0000-0000-000000000902'::uuid, '00000000-0000-0000-0000-000000000001'::uuid, '00000000-0000-0000-0000-000000000802'::uuid, 'Supplier Assistant', 'Supplier-facing intelligent access to supplier information, services and business processes.', 'You are the Supplier Assistant for the Supplier Portal. Help suppliers with information, services, and business processes. Only reference capabilities that have been explicitly configured for you — do not claim to perform actions you cannot verify.'),
                    ('00000000-0000-0000-0000-000000000903'::uuid, '00000000-0000-0000-0000-000000000001'::uuid, '00000000-0000-0000-0000-000000000803'::uuid, 'Sampark Assistant', 'Intelligent access to Sampark Portal services.', 'You are the Sampark Assistant for the Sampark Portal. Only reference capabilities that have been explicitly configured for you — do not claim to perform actions you cannot verify.'),
                    ('00000000-0000-0000-0000-000000000904'::uuid, '00000000-0000-0000-0000-000000000001'::uuid, '00000000-0000-0000-0000-000000000804'::uuid, 'Niryaat Assistant', 'Intelligent access to Niryaat Portal services.', 'You are the Niryaat Assistant for the Niryaat Portal. Only reference capabilities that have been explicitly configured for you — do not claim to perform actions you cannot verify.'),
                    ('00000000-0000-0000-0000-000000000905'::uuid, '00000000-0000-0000-0000-000000000001'::uuid, '00000000-0000-0000-0000-000000000805'::uuid, 'Enterprise Assistant', 'Future cross-application enterprise assistant.', 'You are the Enterprise Assistant, scoped for future cross-application enterprise use. Only reference capabilities that have been explicitly configured for you — do not claim to perform actions you cannot verify.')
                ) AS v("Id","TenantId","ApplicationId","Name","Description","SystemPrompt")
                WHERE EXISTS (SELECT 1 FROM "Applications" ap WHERE ap."Id" = v."ApplicationId")
                  AND NOT EXISTS (SELECT 1 FROM "AssistantDefinitions" ad WHERE ad."Id" = v."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "AssistantDefinitions" WHERE "Id" IN (
                    '00000000-0000-0000-0000-000000000901',
                    '00000000-0000-0000-0000-000000000902',
                    '00000000-0000-0000-0000-000000000903',
                    '00000000-0000-0000-0000-000000000904',
                    '00000000-0000-0000-0000-000000000905'
                );

                DELETE FROM "Applications" WHERE "Id" IN (
                    '00000000-0000-0000-0000-000000000801',
                    '00000000-0000-0000-0000-000000000802',
                    '00000000-0000-0000-0000-000000000803',
                    '00000000-0000-0000-0000-000000000804',
                    '00000000-0000-0000-0000-000000000805'
                );

                UPDATE "Chatbots" SET "IsDeleted" = false, "ModifiedAt" = now()
                WHERE "Id" = '00000000-0000-0000-0000-000000000401';

                UPDATE "KnowledgeBases" SET "IsDeleted" = false, "ModifiedAt" = now()
                WHERE "Id" = '00000000-0000-0000-0000-000000000501';

                UPDATE "KnowledgeBaseSources" SET "IsDeleted" = false, "ModifiedAt" = now()
                WHERE "Id" = '00000000-0000-0000-0000-000000000502';

                UPDATE "AssistantDefinitions"
                SET "IsDeleted" = false, "ModifiedAt" = now()
                WHERE "Id" IN (
                    '00000000-0000-0000-0000-000000000101',
                    '00000000-0000-0000-0000-000000000102',
                    '00000000-0000-0000-0000-000000000103',
                    '00000000-0000-0000-0000-000000000104',
                    '00000000-0000-0000-0000-000000000105',
                    '00000000-0000-0000-0000-000000000106',
                    '00000000-0000-0000-0000-000000000107',
                    '00000000-0000-0000-0000-000000000108',
                    '00000000-0000-0000-0000-000000000109',
                    '00000000-0000-0000-0000-000000000110',
                    '00000000-0000-0000-0000-000000000111'
                );
                """);
        }
    }
}

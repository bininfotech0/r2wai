using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Gives vector_embeddings its own tenant_id so PgVectorService can filter every read, write and
    /// delete by tenant; before this the collection name (kb_{id:N}) was the only isolation.
    ///
    /// vector_embeddings is not an EF entity. PgVectorService creates it the first time a knowledge
    /// base is created, so on a database that has never had one the table does not exist yet: the
    /// whole migration is skipped and CreateCollectionAsync adds the column itself.
    ///
    /// Backfill takes the owner from the KnowledgeBases row whose VectorCollectionName matches, soft-
    /// deleted rows included. A collection name claimed by more than one tenant is left NULL rather
    /// than guessed. Rows left NULL (collections with no matching KB) match no tenant and are never
    /// returned; the column stays nullable so they don't block this migration.
    /// </summary>
    public partial class AddVectorEmbeddingTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF to_regclass('vector_embeddings') IS NULL THEN
                        RETURN;
                    END IF;

                    ALTER TABLE vector_embeddings ADD COLUMN IF NOT EXISTS tenant_id uuid;

                    UPDATE vector_embeddings AS v
                    SET tenant_id = owners.tenant_id
                    FROM (
                        SELECT "VectorCollectionName" AS collection_name, min("TenantId"::text)::uuid AS tenant_id
                        FROM "KnowledgeBases"
                        WHERE "VectorCollectionName" IS NOT NULL
                        GROUP BY "VectorCollectionName"
                        HAVING count(DISTINCT "TenantId") = 1
                    ) AS owners
                    WHERE v.tenant_id IS NULL AND v.collection_name = owners.collection_name;

                    CREATE INDEX IF NOT EXISTS idx_ve_tenant_collection
                        ON vector_embeddings (tenant_id, collection_name);
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF to_regclass('vector_embeddings') IS NULL THEN
                        RETURN;
                    END IF;

                    DROP INDEX IF EXISTS idx_ve_tenant_collection;
                    ALTER TABLE vector_embeddings DROP COLUMN IF EXISTS tenant_id;
                END $$;
                """);
        }
    }
}

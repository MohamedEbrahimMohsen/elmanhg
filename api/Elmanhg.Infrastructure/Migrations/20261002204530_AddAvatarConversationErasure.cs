using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAvatarConversationErasure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION reject_avatar_mutation_unless_erasing() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' AND OLD."ConversationId"::text = current_setting('elmanhg.erase_avatar_conversation', true) THEN
                        RETURN OLD;
                    END IF;
                    RAISE EXCEPTION '% is append-only', TG_TABLE_NAME;
                END; $$;
                DROP TRIGGER avatar_messages_append_only ON "AvatarMessages";
                CREATE TRIGGER avatar_messages_append_only BEFORE UPDATE OR DELETE ON "AvatarMessages" FOR EACH ROW EXECUTE FUNCTION reject_avatar_mutation_unless_erasing();
                DROP TRIGGER avatar_training_records_append_only ON "AvatarTrainingRecords";
                CREATE TRIGGER avatar_training_records_append_only BEFORE UPDATE OR DELETE ON "AvatarTrainingRecords" FOR EACH ROW EXECUTE FUNCTION reject_avatar_mutation_unless_erasing();
                CREATE FUNCTION erase_avatar_conversation(p_conversation_id uuid) RETURNS integer LANGUAGE plpgsql AS $$
                DECLARE erased integer;
                BEGIN
                    PERFORM set_config('elmanhg.erase_avatar_conversation', p_conversation_id::text, true);
                    DELETE FROM "AvatarTrainingRecords" WHERE "ConversationId" = p_conversation_id;
                    DELETE FROM "AvatarMessages" WHERE "ConversationId" = p_conversation_id;
                    GET DIAGNOSTICS erased = ROW_COUNT;
                    PERFORM set_config('elmanhg.erase_avatar_conversation', '', true);
                    RETURN erased;
                END; $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER avatar_training_records_append_only ON "AvatarTrainingRecords";
                CREATE TRIGGER avatar_training_records_append_only BEFORE UPDATE OR DELETE ON "AvatarTrainingRecords" FOR EACH ROW EXECUTE FUNCTION reject_append_only_mutation();
                DROP TRIGGER avatar_messages_append_only ON "AvatarMessages";
                CREATE TRIGGER avatar_messages_append_only BEFORE UPDATE OR DELETE ON "AvatarMessages" FOR EACH ROW EXECUTE FUNCTION reject_append_only_mutation();
                DROP FUNCTION erase_avatar_conversation(uuid);
                DROP FUNCTION reject_avatar_mutation_unless_erasing();
                """);
        }
    }
}

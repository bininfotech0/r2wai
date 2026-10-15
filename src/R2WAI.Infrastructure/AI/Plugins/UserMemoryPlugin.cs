using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace R2WAI.Infrastructure.AI.Plugins;

/// <summary>
/// The agent-managed side of user memory: an agent whose memory mode is "ReadWrite" keeps notes
/// about the signed-in user in its own partition. Attached only when IUserMemoryScope says this
/// turn allows it (see SemanticKernelService), and every call re-checks the user's settings, so the
/// user switching agent notes off takes effect mid-conversation. The agent never names the user or
/// the partition: both come from the request, so one user's notes can't be written to another's.
/// </summary>
public class UserMemoryPlugin(IUserMemoryService memory)
{
    public const string RememberFunction = "remember_user_note";
    public const string ForgetFunction = "forget_user_note";

    [KernelFunction(RememberFunction)]
    [Description("Save a durable note about the current user that you should remember in future conversations, such as a lasting preference or a fact about their work. Saving under an existing key replaces that note.")]
    [return: Description("Whether the note was saved")]
    public Task<string> RememberAsync(
        [Description("Short snake_case name for the note, e.g. preferred_language")] string key,
        [Description("The note itself, one short sentence, e.g. Prefers answers in Hindi.")] string note,
        [Description("One of Profile, Preference, Instruction, Fact, Goal")] string category = "Fact",
        CancellationToken ct = default)
        => memory.RememberAsync(key, note, category, ct);

    [KernelFunction(ForgetFunction)]
    [Description("Delete one of your notes about the current user, when they ask you to forget something or the note is wrong.")]
    [return: Description("Whether the note was removed")]
    public Task<string> ForgetAsync(
        [Description("The key of the note to forget")] string key,
        CancellationToken ct = default)
        => memory.ForgetAsync(key, ct);
}

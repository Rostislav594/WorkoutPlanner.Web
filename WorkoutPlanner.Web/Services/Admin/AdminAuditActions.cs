namespace WorkoutPlanner.Web.Services.Admin;

public static class AdminAuditActions
{
    public const string SupportTicketStatusChanged = nameof(SupportTicketStatusChanged);
    public const string SupportReplySent = nameof(SupportReplySent);
    public const string PublicationCreated = nameof(PublicationCreated);
    public const string PublicationUpdated = nameof(PublicationUpdated);
    public const string PublicationDeleted = nameof(PublicationDeleted);
    public const string UserBlocked = nameof(UserBlocked);
    public const string UserUnblocked = nameof(UserUnblocked);
    public const string UserSignedOutEverywhere = nameof(UserSignedOutEverywhere);
    public const string UserMessageSent = nameof(UserMessageSent);
}

/// <summary>Над чем совершено действие из журнала.</summary>
public static class AdminAuditTargets
{
    public const string User = "User";
}

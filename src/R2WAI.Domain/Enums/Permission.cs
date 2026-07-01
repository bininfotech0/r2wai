namespace R2WAI.Domain.Enums;

[Flags]
public enum Permission : long
{
    None = 0,

    // User permissions
    UserRead = 1L << 0,
    UserCreate = 1L << 1,
    UserUpdate = 1L << 2,
    UserDelete = 1L << 3,

    // Role permissions
    RoleRead = 1L << 4,
    RoleCreate = 1L << 5,
    RoleUpdate = 1L << 6,
    RoleDelete = 1L << 7,

    // Tenant permissions
    TenantRead = 1L << 8,
    TenantUpdate = 1L << 9,

    // Document permissions
    DocumentRead = 1L << 10,
    DocumentUpload = 1L << 11,
    DocumentDelete = 1L << 12,

    // Knowledge Base permissions
    KnowledgeBaseRead = 1L << 13,
    KnowledgeBaseCreate = 1L << 14,
    KnowledgeBaseUpdate = 1L << 15,
    KnowledgeBaseDelete = 1L << 16,

    // Chatbot permissions
    ChatbotRead = 1L << 17,
    ChatbotCreate = 1L << 18,
    ChatbotUpdate = 1L << 19,
    ChatbotDelete = 1L << 20,

    // Workflow permissions
    WorkflowRead = 1L << 24,
    WorkflowCreate = 1L << 25,
    WorkflowUpdate = 1L << 26,
    WorkflowExecute = 1L << 27,

    // Conversation permissions
    ConversationRead = 1L << 28,
    ConversationSend = 1L << 29,
    ConversationDelete = 1L << 30,

    // Admin permissions
    AuditView = 1L << 31,
    ConfigurationManage = 1L << 32,

    // Aggregate permissions
    UserManage = UserRead | UserCreate | UserUpdate | UserDelete,
    RoleManage = RoleRead | RoleCreate | RoleUpdate | RoleDelete,
    DocumentManage = DocumentRead | DocumentUpload | DocumentDelete,
    KnowledgeBaseManage = KnowledgeBaseRead | KnowledgeBaseCreate | KnowledgeBaseUpdate | KnowledgeBaseDelete,
    ChatbotManage = ChatbotRead | ChatbotCreate | ChatbotUpdate | ChatbotDelete,
    WorkflowManage = WorkflowRead | WorkflowCreate | WorkflowUpdate | WorkflowExecute,
    ConversationManage = ConversationRead | ConversationSend | ConversationDelete,
    Admin = AuditView | ConfigurationManage,

    All = UserManage | RoleManage | TenantRead | TenantUpdate |
          DocumentManage | KnowledgeBaseManage | ChatbotManage |
          WorkflowManage | ConversationManage | Admin
}

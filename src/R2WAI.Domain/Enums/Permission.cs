namespace R2WAI.Domain.Enums;

[Flags]
public enum Permission
{
    None = 0,

    // User permissions
    UserRead = 1 << 0,
    UserCreate = 1 << 1,
    UserUpdate = 1 << 2,
    UserDelete = 1 << 3,

    // Role permissions
    RoleRead = 1 << 4,
    RoleCreate = 1 << 5,
    RoleUpdate = 1 << 6,
    RoleDelete = 1 << 7,

    // Tenant permissions
    TenantRead = 1 << 8,
    TenantUpdate = 1 << 9,

    // Document permissions
    DocumentRead = 1 << 10,
    DocumentUpload = 1 << 11,
    DocumentDelete = 1 << 12,

    // Knowledge Base permissions
    KnowledgeBaseRead = 1 << 13,
    KnowledgeBaseCreate = 1 << 14,
    KnowledgeBaseUpdate = 1 << 15,
    KnowledgeBaseDelete = 1 << 16,

    // Chatbot permissions
    ChatbotRead = 1 << 17,
    ChatbotCreate = 1 << 18,
    ChatbotUpdate = 1 << 19,
    ChatbotDelete = 1 << 20,

    // Workflow permissions
    WorkflowRead = 1 << 24,
    WorkflowCreate = 1 << 25,
    WorkflowUpdate = 1 << 26,
    WorkflowExecute = 1 << 27,

    // Conversation permissions
    ConversationRead = 1 << 28,
    ConversationSend = 1 << 29,
    ConversationDelete = 1 << 30,

    // Admin permissions
    AuditView = 1 << 31,
    ConfigurationManage = 1 << 32,

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

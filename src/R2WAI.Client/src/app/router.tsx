import type { ComponentType } from 'react'
import { createBrowserRouter, Navigate } from 'react-router-dom'
import type { LazyRouteFunction, RouteObject } from 'react-router-dom'
import { AuthLayout } from '../layouts/AuthLayout'
import { MainLayout } from '../layouts/MainLayout'
import { RequireAuth } from '../lib/auth/RequireAuth'
import { RouteErrorBoundary } from '../components/RouteErrorBoundary'
import { LegacyRedirect } from './LegacyRedirect'

// Use the data router's lazy route modules so feature pages stay out of the initial shell bundle.
// Layouts and access guards remain eager, and their navigation state provides a skeleton while a
// destination module is fetched.
function lazyPage(load: () => Promise<{ default: ComponentType }>): LazyRouteFunction<RouteObject> {
  return async () => ({ Component: (await load()).default })
}

export const router = createBrowserRouter([
  {
    errorElement: <RouteErrorBoundary />,
    children: [
      {
        element: <AuthLayout />,
        errorElement: <RouteErrorBoundary />,
        children: [
          { path: '/login', lazy: lazyPage(() => import('../features/auth/pages/LoginPage').then((m) => ({ default: m.LoginPage }))) },
          { path: '/forgot-password', lazy: lazyPage(() => import('../features/auth/pages/ForgotPasswordPage').then((m) => ({ default: m.ForgotPasswordPage }))) },
          { path: '/reset-password', lazy: lazyPage(() => import('../features/auth/pages/ResetPasswordPage').then((m) => ({ default: m.ResetPasswordPage }))) },
        ],
      },
      {
        element: <RequireAuth />,
        errorElement: <RouteErrorBoundary />,
        children: [
          {
            element: <MainLayout />,
            errorElement: <RouteErrorBoundary />,
            children: [
              { path: '/', lazy: lazyPage(() => import('../features/dashboard/pages/HomePage').then((m) => ({ default: m.HomePage }))) },
              { path: '/home', element: <LegacyRedirect to={() => '/'} /> },
              { path: '/departments', lazy: lazyPage(() => import('../features/departments/pages/DepartmentsPage').then((m) => ({ default: m.DepartmentsPage }))) },
              { path: '/assistants', lazy: lazyPage(() => import('../features/assistants/pages/AssistantsLibraryPage').then((m) => ({ default: m.AssistantsLibraryPage }))) },
              { path: '/assistants/:id', lazy: lazyPage(() => import('../features/assistants/pages/AssistantStudioPage').then((m) => ({ default: m.AssistantStudioPage }))) },
              { path: '/agents', element: <LegacyRedirect to={() => '/assistants'} /> },
              { path: '/agents/:agentId', element: <LegacyRedirect to={({ agentId }) => `/assistants/${agentId}`} /> },
              { path: '/playground', lazy: lazyPage(() => import('../features/playground/pages/PlaygroundPage').then((m) => ({ default: m.PlaygroundPage }))) },
              { path: '/automations', lazy: lazyPage(() => import('../features/automations/pages/AutomationsListPage').then((m) => ({ default: m.AutomationsListPage }))) },
              { path: '/automations/:id', lazy: lazyPage(() => import('../features/automations/pages/AutomationDetailPage').then((m) => ({ default: m.AutomationDetailPage }))) },
              { path: '/automations/:id/builder', lazy: lazyPage(() => import('../features/automations/pages/AutomationBuilderPage').then((m) => ({ default: m.AutomationBuilderPage }))) },
              { path: '/knowledge', lazy: lazyPage(() => import('../features/knowledge/pages/KnowledgeLibraryPage').then((m) => ({ default: m.KnowledgeLibraryPage }))) },
              { path: '/knowledge/new', element: <LegacyRedirect to={() => '/knowledge'} /> },
              { path: '/knowledge/:id', lazy: lazyPage(() => import('../features/knowledge/pages/KnowledgeBaseDetailPage').then((m) => ({ default: m.KnowledgeBaseDetailPage }))) },
              { path: '/integrations', lazy: lazyPage(() => import('../features/integrations/pages/IntegrationsLibraryPage').then((m) => ({ default: m.IntegrationsLibraryPage }))) },
              { path: '/mcp-connections', lazy: lazyPage(() => import('../features/mcp/pages/McpConnectionsPage').then((m) => ({ default: m.McpConnectionsPage }))) },
              { path: '/chatbots', lazy: lazyPage(() => import('../features/chatbots/pages/ChatbotsPage').then((m) => ({ default: m.ChatbotsPage }))) },
              { path: '/publish', element: <LegacyRedirect to={() => '/chatbots'} /> },
              { path: '/chatbots/:id', lazy: lazyPage(() => import('../features/chatbots/pages/ChatbotDetailPage').then((m) => ({ default: m.ChatbotDetailPage }))) },
              // Distribution (brief "Create Once. Connect Everywhere. Publish."). /deploy/widget
              // doubles as the chatbot picker when no id is present, so a tenant with several
              // chatbots has somewhere to choose from.
              { path: '/deploy', lazy: lazyPage(() => import('../features/deploy/pages/DeployChannelsPage').then((m) => ({ default: m.DeployChannelsPage }))) },
              { path: '/deploy/widget', lazy: lazyPage(() => import('../features/deploy/pages/WidgetDeploymentPage').then((m) => ({ default: m.WidgetDeploymentPage }))) },
              { path: '/deploy/widget/:chatbotId', lazy: lazyPage(() => import('../features/deploy/pages/WidgetDeploymentPage').then((m) => ({ default: m.WidgetDeploymentPage }))) },
              { path: '/deploy/whatsapp/:chatbotId', lazy: lazyPage(() => import('../features/deploy/pages/WhatsAppSetupPage').then((m) => ({ default: m.WhatsAppSetupPage }))) },
              // Developer hub. The nav has pointed at /developer since the Deploy section landed,
              // so this route is what makes that link resolve rather than 404. It composes the
              // existing settings DeveloperTab for keys/webhooks instead of duplicating the forms.
              { path: '/developer', lazy: lazyPage(() => import('../features/developer/pages/DeveloperPage').then((m) => ({ default: m.DeveloperPage }))) },
              { path: '/workspaces', lazy: lazyPage(() => import('../features/applications/pages/ApplicationsPage').then((m) => ({ default: m.ApplicationsPage }))) },
              { path: '/workspaces/:id', lazy: lazyPage(() => import('../features/applications/pages/ApplicationWorkspacePage').then((m) => ({ default: m.ApplicationWorkspacePage }))) },
              { path: '/connections', element: <LegacyRedirect to={() => '/workspaces'} /> },
              { path: '/connections/:id', element: <LegacyRedirect to={({ id }) => `/workspaces/${id}`} /> },
              // Back-compat for the pre-rename "Application" URLs (bookmarks, stale links). Route path
              // stays /workspaces even though the UI label is now "Connected Systems" (see roleNav.ts) —
              // "Workspace" was redefined to mean the bigger Assistant/Capability container, so this
              // page — always just ConnectedApplication under the hood — moved off that word, not the URL.
              { path: '/applications', element: <Navigate to="/workspaces" replace /> },
              { path: '/applications/:id', lazy: lazyPage(() => import('./ApplicationRedirect').then((m) => ({ default: m.ApplicationRedirect }))) },
              { path: '/runs', lazy: lazyPage(() => import('../features/runs/pages/RunsPage').then((m) => ({ default: m.RunsPage }))) },
              { path: '/activity', element: <LegacyRedirect to={() => '/runs'} /> },
              { path: '/activity/:executionId', element: <LegacyRedirect to={() => '/runs'} /> },
              { path: '/approvals', lazy: lazyPage(() => import('../features/approvals/pages/ApprovalsPage').then((m) => ({ default: m.ApprovalsPage }))) },
              { path: '/monitor', lazy: lazyPage(() => import('../features/monitor/pages/MonitorPage').then((m) => ({ default: m.MonitorPage }))) },
              { path: '/users', lazy: lazyPage(() => import('../features/admin/pages/UsersPage').then((m) => ({ default: m.UsersPage }))) },
              { path: '/models', lazy: lazyPage(() => import('../features/admin/pages/ModelsPage').then((m) => ({ default: m.ModelsPage }))) },
              { path: '/security', lazy: lazyPage(() => import('../features/security/pages/SecurityPolicyCenterPage').then((m) => ({ default: m.SecurityPolicyCenterPage }))) },
              { path: '/settings', lazy: lazyPage(() => import('../features/settings/pages/SettingsPage').then((m) => ({ default: m.SettingsPage }))) },
              { path: '/tools', lazy: lazyPage(() => import('../features/tools/pages/ToolsListPage').then((m) => ({ default: m.ToolsListPage }))) },
              { path: '/tools/:id', lazy: lazyPage(() => import('../features/tools/pages/ToolDetailPage').then((m) => ({ default: m.ToolDetailPage }))) },
              { path: '/profile', lazy: lazyPage(() => import('../features/profile/pages/ProfilePage').then((m) => ({ default: m.ProfilePage }))) },
              { path: '/inbox', lazy: lazyPage(() => import('../features/notifications/pages/InboxPage').then((m) => ({ default: m.InboxPage }))) },
              { path: '/about', lazy: lazyPage(() => import('../features/misc/pages/AboutPage').then((m) => ({ default: m.AboutPage }))) },
              { path: '*', lazy: lazyPage(() => import('../features/misc/pages/NotFoundPage').then((m) => ({ default: m.NotFoundPage }))) },
            ],
          },
        ],
      },
    ],
  },
])
